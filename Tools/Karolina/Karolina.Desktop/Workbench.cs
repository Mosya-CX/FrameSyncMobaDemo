using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Cryptography;
using Karolina.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Karolina.Desktop;

public sealed record ChatInput(string Text, string Model, string Effort, string Access, string[] Documents, string? ThreadId, string? TaskId = null, string? PlanId = null, string Mode = "execute-task");
public sealed record ReviewStartInput(string PlanId);
public sealed record ReviewSubmitInput(string Id, int Revision, string Summary);
public sealed record ReviewFileInput(string Id, int Revision, string Path, string Fingerprint, bool? Accept, ReviewNote[] Notes);
public sealed record ReviewTaskInput(string Id, int Revision, bool Accept, string Feedback);
public sealed record ApprovalInput(string Id, bool Accept);
public sealed record UnityInput(string Action, string? Assembly, string? Class, string? Mode);
public sealed record DocumentInput(string Type, string Title, string Domain, string[]? Requirements, string? EnglishTitle = null, string? EnglishDomain = null, string? RuleSection = null);
public sealed record DocumentSave(string Id, string Markdown, string ExpectedHash, string? ChangeSummary);
public sealed record DocumentDetails(string Id, string ExpectedMetadataHash, string? Status, string[]? Requirements, string? Conclusion);
public sealed record EditorInput(bool Dirty);

public sealed partial class Workbench : IAsyncDisposable
{
    private readonly string root;
    private readonly ProjectContext project;
    private readonly WorkspaceViewStore workspaceView;
    private readonly DocumentLibrary library;
    private readonly TaskReviewStore reviews;
    private readonly CodexConnection codex = new();
    private readonly ConcurrentQueue<(long Cursor, JsonElement Event)> events = new();
    private readonly ConcurrentDictionary<string, JsonElement> approvals = new();
    private readonly SemaphoreSlim gate = new(1, 1), unityGate = new(1, 1);
    private WebApplication? app;
    private McpClient? unity;
    private string unityState = "未连接", unityError = "", unityLastResult = "";
    private string? activeThread, activeTurn;
    private bool busy;
    private FileStream? taskLock;
    private long cursor;
    private DateTimeOffset heartbeat = DateTimeOffset.UtcNow;
    private readonly string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly string chatsPath;
    private readonly List<ChatSummary> chats;
    private JsonElement[] models = [];
    private JsonElement account;
    private readonly EvidenceStore evidence = new();
    private RunRecord? run;
    private string? activeReview, finalSummary;
    private StreamWriter? journal;
    private sealed record ChatSummary(string Id, string Title, DateTimeOffset Updated);
    public Workbench(string project)
    {
        this.project = new ProjectContext(project);
        this.project.RecoverLegacyIdentity();
        root = this.project.Root; workspaceView = new(this.project);
        if (!Directory.Exists(root)) throw new ArgumentException("请选择现有工程根目录");
        library = new(root); reviews = new(root);
        var state = this.project.StateDirectory;
        Directory.CreateDirectory(state); chatsPath = this.project.StatePath("chats.json");
        chats = File.Exists(chatsPath) ? JsonSerializer.Deserialize<List<ChatSummary>>(File.ReadAllText(chatsPath), DocumentLibrary.Json) ?? [] : [];
        codex.Notification += Receive;
        codex.ServerRequest += request =>
        {
            string method = request.GetProperty("method").GetString()!;
            if (method is "item/commandExecution/requestApproval" or "item/fileChange/requestApproval" or "execCommandApproval" or "applyPatchApproval")
            { approvals[request.GetProperty("id").GetRawText()] = request; Emit(new { method = "karolina/approval", @params = request }); }
            else { _ = codex.Reject(request.GetProperty("id"), "Karolina 尚不支持此请求：" + method); Emit(new { method = "karolina/error", @params = new { message = "Codex 请求尚未支持：" + method } }); }
        };
    }
    private void Emit(object value)
    {
        var e = value is JsonElement json ? json.Clone() : JsonSerializer.SerializeToElement(value, DocumentLibrary.Json);
        lock (events) { events.Enqueue((++cursor, e)); while (events.Count > 4000) events.TryDequeue(out _); }
        lock (chats) { try { journal?.WriteLine(e.GetRawText()); } catch (IOException failure) { if (run != null) run.Error = "运行日志写入失败：" + failure.Message; } }
    }
    private void Receive(JsonElement notification)
    {
        Emit(notification);
        string method = notification.GetProperty("method").GetString()!;
        if (busy && method == "item/completed" && notification.GetProperty("params").TryGetProperty("threadId", out var owner) && owner.GetString()==activeThread && notification.GetProperty("params").TryGetProperty("turnId",out var turnOwner) && (activeTurn==null||turnOwner.GetString()==activeTurn) && notification.GetProperty("params").TryGetProperty("item", out var item) && item.TryGetProperty("type", out var type) && type.GetString() == "agentMessage")
            finalSummary = item.TryGetProperty("text", out var text) ? text.GetString() : null;
        if (method == "turn/started" && busy && activeTurn == null && notification.GetProperty("params").GetProperty("threadId").GetString() == activeThread) activeTurn = notification.GetProperty("params").GetProperty("turn").GetProperty("id").GetString();
        if (method is "turn/completed" or "karolina/disconnected") _ = Finish(notification, method);
    }
    private async Task Finish(JsonElement notification, string method)
    {
        await gate.WaitAsync();
        try
        {
            if (!busy) return;
            if (method == "turn/completed" && notification.GetProperty("params").GetProperty("threadId").GetString() != activeThread) return;
            if (method == "turn/completed" && activeTurn != null && notification.GetProperty("params").GetProperty("turn").GetProperty("id").GetString() != activeTurn) return;
            try
            {
                if (run != null) { run.State = method == "turn/completed" ? notification.GetProperty("params").GetProperty("turn").GetProperty("status").GetString() ?? "unknown" : "disconnected"; evidence.Save(run); if(activeReview != null) reviews.RecordRun(activeReview, run.Id, activeThread, run.State, finalSummary); }
                if(activeReview != null && run?.State=="completed")
                {
                    var task=reviews.Get(activeReview);
                    await reviews.Submit(task.Id,task.Revision,string.IsNullOrWhiteSpace(finalSummary)?"Codex 本轮已完成，未提供整体总结；请补充实际做了什么、验证结果和限制。":finalSummary[..Math.Min(finalSummary.Length,12000)],app!.Lifetime.ApplicationStopping);
                    Emit(new {method="karolina/review-ready",@params=new {id=task.Id}});
                }
            }
            catch (Exception failure) when(failure is IOException or InvalidOperationException or ArgumentException or OperationCanceledException)
            {
                if (run != null) run.Error = "终态记录或任务差异保存失败：" + failure.Message;
                Emit(new { method = "karolina/error", @params = new { message = run?.Error } });
            }
            finally
            {
                busy = false; activeTurn = null; taskLock?.Dispose(); taskLock = null;
                lock (chats) { try { journal?.Dispose(); } catch (IOException failure) { if (run != null) run.Error = "关闭日志失败：" + failure.Message; } finally { journal = null; } }
                approvals.Clear();
                try { if(run!=null)evidence.Save(run); }
                catch(Exception failure) when(failure is IOException or UnauthorizedAccessException) { Emit(new {method="karolina/error",@params=new {message="终态记录保存失败："+failure.Message}}); }
            }
        }
        finally { gate.Release(); }
    }
    public async Task<string> Start()
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Host.Host != "127.0.0.1") { context.Response.StatusCode = 403; return; }
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'";
            if (context.Request.Path.StartsWithSegments("/api") && (context.Request.Headers["X-Karolina-Session"] != token || context.Request.Headers.TryGetValue("Origin", out var origin) && origin != $"http://{context.Request.Host}")) { context.Response.StatusCode = 403; return; }
            try { await next(); }
            catch (Exception e) { context.Response.StatusCode = e is ArgumentException or InvalidDataException or KeyNotFoundException ? 400 : 409; await context.Response.WriteAsJsonAsync(new { error = e.Message }); }
        });
        app.MapGet("/", () => Results.Text(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Web/index.html")).Replace("__SESSION__", token), "text/html; charset=utf-8"));
        app.MapGet("/app.js", () => Results.File(Path.Combine(AppContext.BaseDirectory, "Web/app.js"), "text/javascript"));
        app.MapGet("/style.css", () => Results.File(Path.Combine(AppContext.BaseDirectory, "Web/style.css"), "text/css"));
        app.MapGet("/review.js", () => Results.File(Path.Combine(AppContext.BaseDirectory, "Web/review.js"), "text/javascript"));
        app.MapGet("/api/state", () => { heartbeat = DateTimeOffset.UtcNow; lock (chats) return Results.Json(new { project = Path.GetFileName(root), root, codex = new { connected = codex.Connected, executable = codex.Executable, error = codex.Connected ? null : codex.Error, account = account.ValueKind == JsonValueKind.Undefined ? (JsonElement?)null : account }, models, windowReady = MinimizeWindow != null, busy=IsBusy, toolBusy=toolCancellation!=null, activeThread, activeTurn, unity = new { state = unityState, error = unityError, lastResult = unityLastResult, busy = unityGate.CurrentCount == 0 }, chats = chats.ToArray() }); });
        app.MapPost("/api/codex/connect", async () =>
        {
            await gate.WaitAsync();
            try
            {
                if (!codex.Connected) await codex.Connect(root, app.Lifetime.ApplicationStopping);
                account = await codex.Request("account/read", new { refreshToken = false }, app!.Lifetime.ApplicationStopping);
                var list = new List<JsonElement>(); string? next = null;
                do { var response = await codex.Request("model/list", new { cursor = next, limit = 100 }, app!.Lifetime.ApplicationStopping); list.AddRange(response.GetProperty("data").EnumerateArray().Where(e => !e.GetProperty("hidden").GetBoolean()).Select(e => e.Clone())); next = response.TryGetProperty("nextCursor", out var c) ? c.GetString() : null; if (list.Count > 1000) throw new InvalidDataException("模型列表过大"); } while (next != null);
                models = list.ToArray(); return Results.Json(new { connected = true });
            }
            finally { gate.Release(); }
        });
        app.MapGet("/api/documents", () => library.List());
        app.MapGet("/api/document/{id}", (string id) => library.Snapshot(id));
        app.MapGet("/api/document/{id}/metadata", (string id) => library.MetadataSnapshot(id));
        app.MapPost("/api/document/create", (DocumentInput input) => library.Create(input.Type, input.Title, input.Domain, input.Requirements, input.EnglishTitle, input.EnglishDomain, input.RuleSection));
        app.MapPost("/api/document/details", (DocumentDetails input) => { library.UpdateDetails(input.Id, input.ExpectedMetadataHash, input.Status, input.Requirements, input.Conclusion); return new { saved = true }; });
        app.MapPost("/api/document/save", (DocumentSave input) => { library.Save(input.Id, input.Markdown, input.ExpectedHash, input.ChangeSummary); return new { saved = true }; });
        app.MapPost("/api/chat/send", Send);
        app.MapPost("/api/chat/stop", Stop);
        app.MapGet("/api/chat/{id}", async (string id) => { lock (chats) if (!chats.Any(c => c.Id == id)) throw new ArgumentException("会话不属于当前工程"); return await codex.Request("thread/read", new { threadId = id, includeTurns = true }, app!.Lifetime.ApplicationStopping); });
        app.MapPost("/api/approval", async (ApprovalInput input) =>
        {
            if (!approvals.TryRemove(input.Id, out var request)) throw new ArgumentException("审批请求已失效");
            string method = request.GetProperty("method").GetString()!;
            await codex.Respond(request.GetProperty("id"), new { decision = method is "execCommandApproval" or "applyPatchApproval" ? (input.Accept ? "approved" : "denied") : (input.Accept ? "accept" : "decline") });
            return Results.Json(new { responded = true });
        });
        app.MapGet("/api/events", (long after) => { lock (events) return new { cursor, truncated = events.TryPeek(out var first) && after < first.Cursor - 1, events = events.Where(e => e.Cursor > after).Select(e => new { cursor = e.Cursor, value = e.Event }).ToArray() }; });
        app.MapGet("/api/reviews", () => reviews.List().Select(ReviewView));
        app.MapGet("/api/workspace/view", () => workspaceView.Read());
        app.MapPost("/api/workspace/view", (WorkspaceView view) => { workspaceView.Save(view); return new { saved=true }; });
        app.MapGet("/api/review/{id}", (string id) => ReviewView(reviews.Get(id)));
        app.MapGet("/api/review/{id}/diff", (string id, string path) => reviews.Diff(id, path));
        app.MapPost("/api/review/start", async (ReviewStartInput input) => { using var guard = WorkspaceLock.Acquire(root); var plan = ReviewPlan(input.PlanId); return ReviewView(await reviews.Start(plan.Id, plan.Title, app.Lifetime.ApplicationStopping)); });
        app.MapPost("/api/review/submit", async (ReviewSubmitInput input) => { using var guard = WorkspaceLock.Acquire(root); return ReviewView(await reviews.Submit(input.Id, input.Revision, input.Summary, app.Lifetime.ApplicationStopping)); });
        app.MapPost("/api/review/file", async (ReviewFileInput input) => { using var guard = WorkspaceLock.Acquire(root); return ReviewView(await reviews.DecideFile(input.Id, input.Revision, input.Path, input.Fingerprint, input.Accept, input.Notes ?? [], app.Lifetime.ApplicationStopping)); });
        app.MapPost("/api/review/decision", async (ReviewTaskInput input) => { using var guard = WorkspaceLock.Acquire(root); return ReviewView(await reviews.DecideTask(input.Id, input.Revision, input.Accept, input.Feedback ?? "", app.Lifetime.ApplicationStopping)); });
        app.MapPost("/api/unity", UnityAction);
        app.MapGet("/api/diagnostics", () => Results.Json(new { run, desktopError = DesktopError, approvals = approvals.Values.ToArray(), project = new { root, project.Key, project.StateDirectory, reviews=reviews.List().Length, version=typeof(Workbench).Assembly.GetName().Version?.ToString() }, editorDirty=EditorDirty, purpose = "记录请求、停止、失败和验证回执，便于追查；不代替需求、计划或验收结论。", location = evidence.Root }));
        app.MapGet("/api/window", () => new { handle = WindowHandle, content = ContentHandle, visible = WindowVisible, tray = TrayAvailable });
        app.MapPost("/api/window/minimize", () => { MinimizeWindow?.Invoke(); return new { minimized = MinimizeWindow != null, handle = WindowHandle }; });
        app.MapPost("/api/window/restore", () => { RestoreWindow?.Invoke(); return new { restored = RestoreWindow != null }; });
        app.MapPost("/api/window/editor", (EditorInput input) => { EditorDirty = input.Dirty; return new { recorded = true }; });
        app.MapPost("/api/shutdown", () => { app.Lifetime.StopApplication(); return new { stopped = true }; });
        MapToolRoutes();
        await app.StartAsync(); return app.Urls.Single();
    }
    private object ReviewView(TaskReview task) => new { task.Id, task.PlanId, task.Title, task.State, task.Revision, task.Round, task.Started, task.Updated, task.Summary, task.Feedback, task.ThreadId, task.RunId, task.RunState, task.BaselineKind, task.BaselineDescription, task.SelectionEvidence, task.Files, task.Attempts, feedbackPrompt = reviews.FeedbackPrompt(task) };
    private LibraryDocument ReviewPlan(string id)
    {
        var plan = library.Find(id);
        var snapshot=JsonSerializer.SerializeToElement(library.MetadataSnapshot(id),DocumentLibrary.Json).GetProperty("metadata");
        if(plan.Type != "plan" || snapshot.GetProperty("status").GetString() == "关闭") throw new ArgumentException("请选择未关闭的具体计划");
        if(!snapshot.TryGetProperty("requirements",out var refs)||refs.GetArrayLength()==0)throw new ArgumentException("执行计划需要先关联具体需求");
        return plan;
    }
    private async Task<IResult> Stop()
    {
        await gate.WaitAsync();
        try
        {
            if (!busy) return Results.Json(new { status = run?.State ?? "already-ended" });
            if (activeThread == null || activeTurn == null) throw new InvalidOperationException("正在等待轮次身份，暂时无法中断；工作区锁仍保留");
            string thread = activeThread, turn = activeTurn;
            try { await codex.Request("turn/interrupt", new { threadId = thread, turnId = turn }, app!.Lifetime.ApplicationStopping); return Results.Json(new { status = "requested" }); }
            catch (CodexRpcException)
            {
                var history = await codex.Request("thread/read", new { threadId = thread, includeTurns = true }, app!.Lifetime.ApplicationStopping);
                var terminal = history.GetProperty("thread").GetProperty("turns").EnumerateArray().FirstOrDefault(t => t.GetProperty("id").GetString() == turn);
                if (terminal.ValueKind != JsonValueKind.Undefined && terminal.GetProperty("status").GetString() is "completed" or "interrupted" or "failed") return Results.Json(new { status = terminal.GetProperty("status").GetString() });
                throw;
            }
        }
        finally { gate.Release(); }
    }
    private async Task<IResult> Send(ChatInput input)
    {
        await gate.WaitAsync();
        bool ownsRun = false, turnSubmitted = false;
        try
        {
            if (busy) throw new InvalidOperationException("当前轮次尚未结束");
            if(toolCancellation!=null)throw new InvalidOperationException("拓展工具正在执行");
            if (!codex.Connected) throw new InvalidOperationException("请先连接 Codex");
            if (string.IsNullOrWhiteSpace(input.Text) || input.Text.Length > 100000) throw new ArgumentException("指令不能为空且不能超过十万字");
            if (!models.Any(m => m.GetProperty("model").GetString() == input.Model && m.GetProperty("supportedReasoningEfforts").EnumerateArray().Any(e => e.GetProperty("reasoningEffort").GetString() == input.Effort))) throw new ArgumentException("请选择服务支持的模型和思考档位");
            if (input.Access is not ("read-only" or "workspace-write" or "danger-full-access")) throw new ArgumentException("无效访问权限");
            string context = WorkflowModes.Context(input.Mode)+"\n\n"+library.ReferenceContext(input.Documents ?? []);
            if(input.Mode!="execute-task"&&input.Access=="danger-full-access")throw new ArgumentException("讨论需求/制定计划请选择只读或文档写入，不能使用完全访问");
            if (input.ThreadId != null) { lock (chats) if (!chats.Any(c => c.Id == input.ThreadId)) throw new ArgumentException("会话不属于当前工程"); }
            taskLock = WorkspaceLock.Acquire(root);
            ownsRun = true;
            run = null;
            activeThread = null; activeTurn = null;
            activeReview = null; finalSummary = null;
            if(input.Access != "read-only" && input.Mode == "execute-task")
            {
                if(input.TaskId != null)
                {
                    var task = reviews.Get(input.TaskId); ReviewPlan(task.PlanId);
                    if(task.State is not ("执行" or "待再执行")) throw new InvalidOperationException("先退回任务再执行，审批中的版本不能继续改写");
                    activeReview = task.Id;
                    if(task.State == "待再执行") context += "\n\n人工审批返回执行：\n" + reviews.FeedbackPrompt(task);
                }
                else if(!string.IsNullOrWhiteSpace(input.PlanId))
                {
                    var plan=ReviewPlan(input.PlanId);
                    var open=reviews.List().SingleOrDefault(t=>t.State!="已通过");
                    if(open != null && open.PlanId != plan.Id) throw new InvalidOperationException("已有其它计划的未结束任务");
                    if(open?.State is "待审批" or "待再执行") throw new InvalidOperationException("请从审批页面返回执行原任务");
                    activeReview=(open ?? await reviews.Start(plan.Id,plan.Title,app!.Lifetime.ApplicationStopping)).Id;
                }
                else throw new ArgumentException("写入工程前请选择执行计划，以便记录任务开始基线；普通讨论可使用只读权限");
            }
            run = new RunRecord { Project = root, Prompt = input.Text, Model = input.Model, Sandbox = input.Access, Requirement = string.Join(',', input.Documents ?? []), State = "running" }; evidence.Save(run);
            run.Mode=input.Mode;
            if(activeReview != null) { var task=reviews.Get(activeReview); run.Plan=task.PlanId; run.ReviewId=task.Id; context+=$"\n\n执行任务计划：{task.PlanId}（{task.Title}）。对照方式：{task.BaselineKind}。{task.BaselineDescription} 结束后需人工审批。请在最后简洁总结做了什么、机器/Agent 验证事实及限制，不宣布人工验收通过。"; }
            lock (chats) journal = new StreamWriter(Path.Combine(evidence.DirectoryFor(run.Id), "events.jsonl")) { AutoFlush = true };
            busy = true;
            activeTurn = null;
            string workDirectory=WorkflowModes.WorkingDirectory(input.Mode,root);
            object policy = input.Access switch { "workspace-write" => new { type = "workspaceWrite", writableRoots = new[] { workDirectory }, networkAccess = false }, "danger-full-access" => (object)new { type = "dangerFullAccess" }, _ => new { type = "readOnly", networkAccess = false } };
            var thread = await codex.Request(input.ThreadId == null ? "thread/start" : "thread/resume", input.ThreadId == null ? (object)new { cwd = workDirectory, model = input.Model, sandbox = input.Access, approvalPolicy = "on-request" } : new { threadId = input.ThreadId, cwd = workDirectory, model = input.Model, sandbox = input.Access, approvalPolicy = "on-request" }, app!.Lifetime.ApplicationStopping);
            activeThread = thread.GetProperty("thread").GetProperty("id").GetString()!; run.ThreadId = activeThread;
            if(activeReview != null) { var task=reviews.Get(activeReview); if(task.State=="待再执行")reviews.Resume(task.Id,task.Revision); reviews.RecordRun(task.Id,run.Id,activeThread,"running"); }
            lock (chats)
            {
                var latest = File.Exists(chatsPath) ? JsonSerializer.Deserialize<List<ChatSummary>>(File.ReadAllText(chatsPath), DocumentLibrary.Json) ?? [] : [];
                foreach (var saved in latest) if (!chats.Any(c => c.Id == saved.Id)) chats.Add(saved);
                int index = chats.FindIndex(c => c.Id == activeThread); var item = new ChatSummary(activeThread, index < 0 ? input.Text[..Math.Min(input.Text.Length, 32)] : chats[index].Title, DateTimeOffset.Now);
                if (index < 0) chats.Insert(0, item); else { chats.RemoveAt(index); chats.Insert(0, item); }
                File.WriteAllText(project.StatePath("chats.json.tmp"), JsonSerializer.Serialize(chats, DocumentLibrary.Json)); File.Move(project.StatePath("chats.json.tmp"), project.StatePath("chats.json"), true);
            }
            evidence.Save(run);
            turnSubmitted = true;
            var response = await codex.Request("turn/start", new { threadId = activeThread, model = input.Model, effort = input.Effort, approvalPolicy = "on-request", sandboxPolicy = policy, input = new[] { new { type = "text", text = context + "\n\n用户指令：\n" + input.Text, text_elements = Array.Empty<object>() } } }, app!.Lifetime.ApplicationStopping);
            activeTurn = response.GetProperty("turn").GetProperty("id").GetString();
            return Results.Json(new { threadId = activeThread, turnId = activeTurn, runId = run.Id });
        }
        catch (Exception e)
        {
            if (!ownsRun) throw;
            if (turnSubmitted && e is not CodexRpcException && codex.Connected)
            {
                // 超时不证明服务未受理。保持锁，等待真实 turn 终态，或关闭程序终止进程树。
                if (run != null) { run.State = "unknown"; run.Error = "启动回执未知，保持工作区锁；等待终态或停止当前轮次。" + e.Message; evidence.Save(run); if(activeReview != null)reviews.RecordRun(activeReview,run.Id,activeThread,"unknown"); }
                throw;
            }
            busy = false; taskLock?.Dispose(); taskLock = null;
            if (run != null) { run.State = "failed"; run.Error = e.Message; evidence.Save(run); }
            if(activeReview != null && run != null) reviews.RecordRun(activeReview,run.Id,activeThread,"failed");
            lock (chats) { journal?.Dispose(); journal = null; }
            throw;
        }
        finally { gate.Release(); }
    }
    private async Task<IResult> UnityAction(UnityInput input)
    {
        if(BuildPending)throw new InvalidOperationException("Unity构建尚未确认结束，暂停所有Unity操作");
        if(toolCancellation!=null)throw new InvalidOperationException("拓展工具正在执行，请等待结束");
        if (!await unityGate.WaitAsync(0)) throw new InvalidOperationException("Unity 操作正在执行");
        try
        {
            if(BuildPending)throw new InvalidOperationException("Unity构建尚未确认结束，暂停所有Unity操作");
            if(toolCancellation!=null)throw new InvalidOperationException("拓展工具正在执行，请等待结束");
            if (input.Action == "connect")
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(app!.Lifetime.ApplicationStopping);
                deadline.CancelAfter(TimeSpan.FromSeconds(12));
                unity?.Dispose(); unity = new McpClient(McpClient.Discover(root));
                await unity.Connect(deadline.Token); await unity.VerifyProject(root, deadline.Token);
                unityState = "已连接"; unityError = ""; return Results.Json(new { state = unityState, tools = unity.Tools.Length });
            }
            if (unity == null || !unity.Connected || unityState != "已连接") throw new InvalidOperationException("请先连接并核对 Unity 工程");
            using var guard = WorkspaceLock.Acquire(root);
            string tool; object arguments;
            switch (input.Action)
            {
                case "state": tool = "editor-application-get-state"; arguments = new { }; break;
                case "console": tool = "console-get-logs"; arguments = new { logTypeFilter = "Error", maxEntries = 100, includeStackTrace = false }; break;
                case "compile": tool = "assets-refresh"; arguments = new { options = "ForceSynchronousImport" }; break;
                case "tests":
                    if (string.IsNullOrWhiteSpace(input.Assembly) || string.IsNullOrWhiteSpace(input.Class) || input.Mode is not ("EditMode" or "PlayMode")) throw new ArgumentException("请填写测试程序集、类和模式");
                    tool = "tests-run"; arguments = new { testMode = input.Mode, testAssembly = input.Assembly, testClass = input.Class, includePassingTests = true, includeMessages = true, includeStacktrace = false }; break;
                default: throw new ArgumentException("不支持的 Unity 操作");
            }
            var response = await unity.Call(tool, arguments, app!.Lifetime.ApplicationStopping); var id = Guid.NewGuid().ToString("N"); evidence.Validation(id, tool, response.GetRawText());
            string? verdict = input.Action == "tests" ? McpClient.TestVerdict(response) : null;
            unityError = "";
            unityLastResult = input.Action switch { "tests" => "测试 " + System.Text.RegularExpressions.Regex.Match(verdict!, @"\d+/\d+").Value + " 通过", "compile" => "已刷新，待核查 Console", "console" => "Console " + McpClient.Payload(response).GetArrayLength() + " 条错误", _ => "Editor 状态已读取" };
            return Results.Json(new { tool, response, verdict, evidenceId = id });
        }
        catch (Exception e) { unityError = e.Message; unityLastResult = input.Action == "tests" ? "测试未通过或尚未取得终态" : "操作失败"; if (input.Action == "connect") unityState = "连接失败"; throw; }
        finally { unityGate.Release(); }
    }
    public Task Wait() => app!.WaitForShutdownAsync();
    public Action? MinimizeWindow { get; set; }
    public Action? RestoreWindow { get; set; }
    public long? WindowHandle { get; set; }
    public long? ContentHandle { get; set; }
    public bool WindowVisible { get; set; }
    public bool TrayAvailable { get; set; }
    public bool IsBusy => busy || toolCancellation!=null;
    public bool EditorDirty { get; set; }
    public string? DesktopError { get; private set; }
    public void ReportDesktopError(string message)
    {
        if (DesktopError == message) return;
        DesktopError = message;
        Emit(new { method = "karolina/error", @params = new { message } });
    }
    public void Close() => app?.Lifetime.StopApplication();
    public async ValueTask DisposeAsync()
    {
        await unityGate.WaitAsync();
        try { unity?.Dispose(); }
        finally { unityGate.Release(); }
        await gate.WaitAsync();
        try
        {
            try
            {
                if (busy && run != null) { run.State = "terminated"; run.Error = "工作台关闭，Codex 子进程已终止；请核查尚未完成的修改。"; evidence.Save(run); if(activeReview!=null)reviews.RecordRun(activeReview,run.Id,activeThread,"terminated",finalSummary); }
            }
            finally
            {
                busy = false; codex.Dispose(); unity?.Dispose(); library.Dispose(); taskLock?.Dispose(); taskLock = null;
                lock (chats) { try { journal?.Dispose(); } finally { journal = null; approvals.Clear(); } }
            }
        }
        finally { gate.Release(); }
        if (app != null) await app.DisposeAsync();
    }
}
