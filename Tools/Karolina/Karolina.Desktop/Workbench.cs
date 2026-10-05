using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Cryptography;
using Karolina.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Karolina.Desktop;

public sealed partial class Workbench : IAsyncDisposable
{
    private readonly string root;
    private readonly ProjectContext project;
    private readonly WorkspacePreferencesStore workspacePreferences;
    private readonly DocumentLibrary library;
    private readonly TaskReviewStore reviews;
    private readonly ReviewCompletionService reviewCompletion;
    private readonly ICodexSession codex;
    private readonly ConcurrentQueue<(long Cursor, JsonElement Event)> events = new();
    private readonly ConcurrentDictionary<string, JsonElement> approvals = new();
    private readonly SemaphoreSlim gate = new(1, 1), unityGate = new(1, 1), codexConnectionGate = new(1, 1);
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
    private readonly EvidenceStore evidence;
    private RunRecord? run;
    private string? activeReview, finalSummary;
    private StreamWriter? journal;
    private sealed record ChatSummary(string Id, string Title, DateTimeOffset Updated);
    public Workbench(string project) : this(project, new CodexConnection()) { }

    /// <summary>After successful construction, this host owns the transferred session through DisposeAsync.</summary>
    public Workbench(string project, ICodexSession session, EvidenceStore? evidence = null,
        ReviewExplanationService? explanations = null, TimeProvider? clock = null)
    {
        codex = session ?? throw new ArgumentNullException(nameof(session));
        this.evidence = evidence ?? new EvidenceStore();
        this.project = new ProjectContext(project);
        this.project.RecoverLegacyIdentity();
        root = this.project.Root; workspacePreferences = new(this.project);
        explanationService = explanations ?? new ReviewExplanationService(new JsonReviewExplanationStore(this.project), clock);
        if (!Directory.Exists(root)) throw new ArgumentException("请选择现有工程根目录");
        library = new(root); reviews = new(root);
        reviewCompletion = new ReviewCompletionService(reviews);
        var state = this.project.StateDirectory;
        Directory.CreateDirectory(state); chatsPath = this.project.StatePath("chats.json");
        chats = File.Exists(chatsPath) ? JsonSerializer.Deserialize<List<ChatSummary>>(File.ReadAllText(chatsPath), DocumentLibrary.Json) ?? [] : [];
        codex.Notification += Receive;
        codex.ServerRequest += request =>
        {
            string method = request.GetProperty("method").GetString()!;
            if (method is "item/commandExecution/requestApproval" or "item/fileChange/requestApproval" or "execCommandApproval" or "applyPatchApproval")
            { approvals[request.GetProperty("id").GetRawText()] = request; Emit(new { method = "karolina/approval", @params = request }); }
            else { _ = ObserveBackground(codex.Reject(request.GetProperty("id"), "Karolina 尚不支持此请求：" + method), "拒绝未支持的Codex请求"); Emit(new { method = "karolina/error", @params = new { message = "Codex 请求尚未支持：" + method } }); }
        };
    }
    private async Task ObserveBackground(Task task, string operation, CancellationToken stopping = default)
    {
        try { await task; }
        catch(OperationCanceledException) when(stopping.IsCancellationRequested) { }
        catch(Exception failure)
        {
            Emit(new { method="karolina/error", @params=new { message=operation+"失败："+failure.Message } });
        }
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
        QueueFileProvenance(notification);
        ReceiveExplanationProgress(notification);
        string method = notification.GetProperty("method").GetString()!;
        if (busy && method == "item/completed" && notification.GetProperty("params").TryGetProperty("threadId", out var owner) && owner.GetString()==activeThread && notification.GetProperty("params").TryGetProperty("turnId",out var turnOwner) && (activeTurn==null||turnOwner.GetString()==activeTurn) && notification.GetProperty("params").TryGetProperty("item", out var item) && item.TryGetProperty("type", out var type) && type.GetString() == "agentMessage")
            finalSummary = item.TryGetProperty("text", out var text) ? text.GetString() : null;
        if (method == "turn/started" && busy && activeTurn == null && notification.GetProperty("params").GetProperty("threadId").GetString() == activeThread) activeTurn = notification.GetProperty("params").GetProperty("turn").GetProperty("id").GetString();
        if (method is "turn/completed" or "karolina/disconnected") _ = ObserveBackground(Finish(notification, method), "处理Codex终态");
    }
    private async Task Finish(JsonElement notification, string method)
    {
        await gate.WaitAsync();
        try
        {
            if (!busy) return;
            if (method == "turn/completed" && notification.GetProperty("params").GetProperty("threadId").GetString() != activeThread) return;
            if (method == "turn/completed" && activeTurn != null && notification.GetProperty("params").GetProperty("turn").GetProperty("id").GetString() != activeTurn) return;
            bool explanationCompleted = false;
            try
            {
                Task pending; lock (provenanceGate) pending = provenanceQueue; await pending;
                if(run != null && method == "turn/completed")
                {
                    var completedTurn=notification.GetProperty("params").GetProperty("turn");
                    if(completedTurn.TryGetProperty("error",out var error) && error.ValueKind==JsonValueKind.Object && error.TryGetProperty("message",out var message))run.Error=message.GetString();
                }
                if (run != null) { run.State = method == "turn/completed" ? notification.GetProperty("params").GetProperty("turn").GetProperty("status").GetString() ?? "unknown" : "disconnected"; evidence.Save(run); if(activeReview != null) reviews.RecordRun(activeReview, run.Id, activeThread, run.State, ReviewNarratives.HumanSummary(finalSummary??"")); }
                if(activeReview != null && run?.State=="completed")
                {
                    var result=await reviewCompletion.CompleteExecution(activeReview,finalSummary??"",app!.Lifetime.ApplicationStopping);
                    if(result.UnprovenDescriptions>0)Emit(new {method="karolina/error",@params=new {message="部分文件没有匹配的Agent写入证明，未将执行回复绑定到这些文件；可从冻结差异另行补充说明。"}});
                    Emit(new {method="karolina/review-ready",@params=new {id=result.ReviewId}});
                }
                if(explainingTask is {} explanation && run?.State=="completed")
                {
                    UpdateExplanationProgress("保存", "正在核对文件指纹并保存说明");
                    int saved=await reviewCompletion.CompleteExplanation(explanation.Id,explanation.Fingerprints,finalSummary??"",app!.Lifetime.ApplicationStopping);
                    UpdateExplanationProgress("完成", $"已保存 {saved} 个文件的说明", saved:saved, active:false, persist:true, clearError:true);
                    explanationCompleted = true;
                    Emit(new {method="karolina/review-updated",@params=new {id=explanation.Id}});
                }
            }
            catch (Exception failure) when(failure is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or OperationCanceledException or JsonException or KeyNotFoundException)
            {
                if (run != null) run.Error = "终态记录或任务差异保存失败：" + failure.Message;
                UpdateExplanationProgress("失败", "说明未保存，请查看失败原因", active:false, error:failure.Message, persist:true);
                Emit(new { method = "karolina/error", @params = new { message = run?.Error } });
            }
            finally
            {
                try
                {
                    if (explainingTask != null && !explanationCompleted)
                        UpdateExplanationProgress(run?.State=="interrupted"?"已停止":"失败", run?.State=="interrupted"?"已停止生成，本轮未保存说明":"本轮说明未成功保存，请查看失败原因", active:false, error:run?.Error ?? (run?.State=="interrupted"?null:"运行终态："+run?.State), persist:true);
                }
                finally
                {
                    // Even an unexpected progress-adapter error must release the execution lease.
                    busy = false; activeTurn = null; explainingTask=null; taskLock?.Dispose(); taskLock = null;
                    lock (chats) { try { journal?.Dispose(); } catch (IOException failure) { if (run != null) run.Error = "关闭日志失败：" + failure.Message; } finally { journal = null; } }
                    approvals.Clear();
                    try { if(run!=null)evidence.Save(run); }
                    catch(Exception failure) when(failure is IOException or UnauthorizedAccessException) { Emit(new {method="karolina/error",@params=new {message="终态记录保存失败："+failure.Message}}); }
                }
            }
        }
        finally { gate.Release(); }
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
        await codexConnectionGate.WaitAsync();
        await gate.WaitAsync();
        try
        {
            try
            {
                if (busy && run != null) { run.State = "terminated"; run.Error = "工作台关闭，Codex 子进程已终止；请核查尚未完成的修改。"; evidence.Save(run); if(activeReview!=null)reviews.RecordRun(activeReview,run.Id,activeThread,"terminated",finalSummary); }
            }
            finally
            {
                codex.Dispose();
                if(busy)UpdateExplanationProgress("已中断", "程序完全退出，生成任务已终止；本轮未保存说明", active:false, error:run?.Error, persist:true);
                busy = false; unity?.Dispose(); library.Dispose(); reviews.Dispose(); taskLock?.Dispose(); taskLock = null;
                lock (chats) { try { journal?.Dispose(); } finally { journal = null; approvals.Clear(); } }
            }
        }
        finally { gate.Release(); codexConnectionGate.Release(); }
        if (app != null) await app.DisposeAsync();
    }
}
