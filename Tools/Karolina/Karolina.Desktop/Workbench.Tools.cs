using System.Text.Json;
using Karolina.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Karolina.Desktop;

public sealed record ToolRegisterInput(ExtensionTool Definition,string Manual);
public sealed record ToolRunInput(string Id,string Hash,JsonElement Arguments,string? PlanId=null,string? TaskId=null);
public sealed partial class Workbench
{
    private ExtensionToolCatalog toolCatalog=null!;
    private ExtensionToolService toolService=null!;
    private CancellationTokenSource? toolCancellation;
    private ExtensionToolRun? runningToolRecord;
    private bool BuildPending => File.Exists(project.StatePath("unity-build-pending.json"));
    private void MapToolRoutes()
    {
        var server=app??throw new InvalidOperationException("工作台服务尚未创建");
        toolCatalog=new(project,library);
        toolService=new(project,toolCatalog,[new ExternalToolRunner(project),new UnityToolRunner(project,()=>unity,tool=> {
            if(tool.UnityBuild)File.WriteAllText(project.StatePath("unity-build-pending.json"),JsonSerializer.Serialize(new {tool=tool.Id,run=runningToolRecord?.Id??throw new InvalidOperationException("缺少工具运行记录"),at=DateTimeOffset.Now},DocumentLibrary.Json));
        })]);
        server.MapGet("/tools.js",()=>Results.File(Path.Combine(AppContext.BaseDirectory,"Web/tools.js"),"text/javascript"));
        server.MapGet("/workspace.js",()=>Results.File(Path.Combine(AppContext.BaseDirectory,"Web/workspace.js"),"text/javascript"));
        server.MapGet("/api/tools",()=>toolCatalog.List());
        server.MapGet("/api/tools/runs",()=>toolService.ListRuns());
        server.MapGet("/api/tools/unity",()=>unity?.Tools??[]);
        server.MapGet("/api/tools/build",()=>new { pending=BuildPending });
        server.MapPost("/api/tools/register",(ToolRegisterInput input)=>toolCatalog.Register(input.Definition,input.Manual));
        server.MapPost("/api/tools/run",RunExtensionTool);
        server.MapPost("/api/tools/stop",()=>{toolCancellation?.Cancel();return new { requested=toolCancellation!=null };});
        server.MapPost("/api/tools/build-finished",()=>{if(busy||toolCancellation!=null)throw new InvalidOperationException("请等待当前调用结束");File.Delete(project.StatePath("unity-build-pending.json"));return new { pending=false };});
    }
    private async Task<IResult> RunExtensionTool(ToolRunInput input)
    {
        await gate.WaitAsync();
        bool unityHeld=false;
        try
        {
            if(busy||toolCancellation!=null)throw new InvalidOperationException("当前工程还有运行中的操作");
            var descriptor=toolCatalog.Find(input.Id);var definition=descriptor.Definition;
            if(descriptor.Hash!=input.Hash)throw new InvalidOperationException("工具声明已更新，请重新选择工具");
            if(definition.Kind=="manual")throw new ArgumentException("此项只有指南，请先注册执行入口");
            toolService.ValidateExecution(descriptor,input.Arguments);
            if(definition.Kind=="unity-mcp"&&BuildPending)throw new InvalidOperationException("构建请求已经发送；请先确认Unity构建结束，期间禁止继续Unity操作");
            using var guard=WorkspaceLock.Acquire(root);
            TaskReview? task=null;
            var record=new ExtensionToolRun { ToolId=definition.Id };
            toolCancellation=CancellationTokenSource.CreateLinkedTokenSource(app!.Lifetime.ApplicationStopping);
            runningToolRecord=record;
            toolService.Save(record);
            try
            {
                if(definition.Kind=="unity-mcp")
                {
                    unityHeld=await unityGate.WaitAsync(0,toolCancellation.Token);
                    if(!unityHeld)throw new InvalidOperationException("Unity操作正在执行，请稍后重试");
                    if(BuildPending)throw new InvalidOperationException("请先确认Unity构建结束");
                    if(unity==null||!unity.Connected||unityState!="已连接")throw new InvalidOperationException("请在AI对话页连接并核对Unity");
                    unity.ValidateTool(definition.ToolName!);
                    using var preflight=CancellationTokenSource.CreateLinkedTokenSource(toolCancellation.Token);preflight.CancelAfter(TimeSpan.FromSeconds(12));
                    await unity.VerifyProject(root,preflight.Token);
                }
                if(definition.RequiresTask)
                {
                    if(input.TaskId!=null)
                    {
                        var candidate=reviews.Get(input.TaskId);ReviewPlan(candidate.PlanId);
                        if(candidate.State=="待再执行")candidate=reviews.Resume(candidate.Id,candidate.Revision);
                        else if(candidate.State!="执行")throw new ArgumentException("审批中的任务先退回，再继续执行");
                        task=candidate;
                    }
                    else
                    {
                        var plan=ReviewPlan(input.PlanId??throw new ArgumentException("写入工具需要选择执行计划或继续原任务"));
                        var existing=reviews.List().SingleOrDefault(t=>t.State!="已通过");
                        if(existing!=null&&(existing.PlanId!=plan.Id||existing.State!="执行"))throw new InvalidOperationException("请先处理已有未结束的任务审批");
                        task=existing??await reviews.Start(plan.Id,plan.Title,toolCancellation.Token);
                    }
                    record.ReviewId=task.Id;reviews.RecordRun(task.Id,record.Id,null,"拓展工具执行");
                }
                await toolService.Execute(record,descriptor,input.Arguments,toolCancellation.Token);
            }
            catch(Exception e){record.State=e is OperationCanceledException?"已取消或超时":"失败";record.Output=e.Message;record.Ended=DateTimeOffset.Now;toolService.Save(record);}
            if(task!=null)
            {
                reviews.RecordRun(task.Id,record.Id,null,"拓展工具："+record.State);
                var latest=reviews.Get(task.Id);
                await reviews.Submit(task.Id,latest.Revision,$"调用拓展工具：{definition.Title}。实际结果：{record.State}。\n{record.Output[..Math.Min(record.Output.Length,6000)]}\n审批不代替行为验收。",CancellationToken.None);
                Emit(new {method="karolina/review-ready",@params=new {id=task.Id}});
            }
            return Results.Json(record);
        }
        finally{if(unityHeld)unityGate.Release();runningToolRecord=null;toolCancellation?.Dispose();toolCancellation=null;gate.Release();}
    }
}
