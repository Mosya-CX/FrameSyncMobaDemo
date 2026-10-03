using System.Text.Json;
using System.Text.RegularExpressions;

namespace Karolina.Core;

public sealed class ExtensionTool
{
    public string Id { get; set; }="";
    public string Title { get; set; }="";
    public string Description { get; set; }="";
    public string Kind { get; set; }="manual";
    public string GuideId { get; set; }="";
    public string? ToolName { get; set; }
    public string? Executable { get; set; }
    public string[] Arguments { get; set; }=[];
    public JsonElement DefaultArguments { get; set; }=JsonSerializer.SerializeToElement(new {});
    public bool ReadOnly { get; set; }
    public bool UnityBuild { get; set; }
    public int TimeoutSeconds { get; set; }=60;
    public bool RequiresTask => Kind!="manual"&&(!ReadOnly||Kind=="unity-mcp"&&ToolName is not ("editor-application-get-state" or "console-get-logs" or "assets-find" or "assets-get-data" or "ping" or "package-list" or "tests-list"));
}
public sealed record ToolDescriptor(ExtensionTool Definition,string Hash);
public sealed class ExtensionToolCatalog(ProjectContext project, DocumentLibrary documents)
{
    private string DirectoryPath => documents.SafePath("Docs/tools/registry");
    public ToolDescriptor[] List() => Directory.Exists(DirectoryPath)?Directory.EnumerateFiles(DirectoryPath,"*.tool.json").Order(StringComparer.Ordinal).Select(Read).ToArray():[];
    private ToolDescriptor Read(string path)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("工具声明不能是链接");
        byte[] bytes=File.ReadAllBytes(path);var tool=JsonSerializer.Deserialize<ExtensionTool>(bytes,DocumentLibrary.Json)??throw new InvalidDataException("工具声明损坏");Validate(tool);
        if(Path.GetFileName(path)!=tool.Id+".tool.json")throw new InvalidDataException("工具编号与声明文件不一致");
        if(!string.IsNullOrWhiteSpace(tool.GuideId))documents.Find(tool.GuideId);
        return new(tool,RequirementStore.Hash(bytes));
    }
    public ToolDescriptor Find(string id) => List().SingleOrDefault(t=>t.Definition.Id==id)??throw new KeyNotFoundException("拓展工具不存在");
    public ToolDescriptor Register(ExtensionTool tool,string manual)
    {
        Validate(tool);if(string.IsNullOrWhiteSpace(manual)||manual.Length>100000)throw new ArgumentException("注册工具需要配套使用说明");
        using var guard=WorkspaceLock.Acquire(project.Root);
        string guideId="TOOL-GUIDE-"+tool.Id; if(documents.List().Any(d=>d.Id==guideId)||List().Any(t=>t.Definition.Id==tool.Id))throw new ArgumentException("工具编号已存在；可编辑现有声明文件");
        string guidePath=$"Docs/tools/guides/{guideId}_usage.md";
        Directory.CreateDirectory(DirectoryPath);Directory.CreateDirectory(Path.GetDirectoryName(documents.SafePath(guidePath))!);
        tool.GuideId=guideId;
        string metadata=guidePath[..^3]+".meta.json",declaration="Docs/tools/registry/"+tool.Id+".tool.json",catalogPath=documents.SafePath("Docs/catalog.json");
        var writes=new Dictionary<string,string> {
            [guidePath]="# "+tool.Title+"使用说明\n\n"+manual.Trim()+"\n",
            [metadata]=JsonSerializer.Serialize(new{id=guideId,title=tool.Title+"使用说明",type="resource",domain="拓展工具",status="当前资料",path=guidePath,version=1},DocumentLibrary.Json),
            [declaration]=JsonSerializer.Serialize(tool,DocumentLibrary.Json)
        };
        foreach(string relative in writes.Keys)if(File.Exists(documents.SafePath(relative))||File.Exists(documents.SafePath(relative+".register.tmp")))throw new IOException("工具文件已经存在，保留原文件："+relative);
        byte[] oldCatalog=File.ReadAllBytes(catalogPath);
        var catalog=System.Text.Json.Nodes.JsonNode.Parse(oldCatalog)!.AsObject();
        if(catalog["schemaVersion"]?.GetValue<int>()!=2||catalog["entries"] is not System.Text.Json.Nodes.JsonArray entries)throw new InvalidDataException("资料索引不是版本2");
        if(entries.Any(e=>e?["id"]?.GetValue<string>()==guideId))throw new InvalidDataException("指南编号已在索引内");
        entries.Add(JsonSerializer.SerializeToNode(new{id=guideId,path=guidePath,metadata},DocumentLibrary.Json));
        string catalogText=catalog.ToJsonString(DocumentLibrary.Json),catalogHash=RequirementStore.Hash(System.Text.Encoding.UTF8.GetBytes(catalogText));
        string catalogTemp=documents.SafePath("Docs/catalog.json.register.tmp");var owned=new Dictionary<string,string>();bool catalogWritten=false;
        void WriteNew(string path,string text){using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);owned.Add(path,RequirementStore.Hash(System.Text.Encoding.UTF8.GetBytes(text)));using var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false));writer.Write(text);}
        try
        {
            foreach(var (relative,text) in writes)
            {string target=documents.SafePath(relative),temp=documents.SafePath(relative+".register.tmp");WriteNew(temp,text);File.Move(temp,target,false);string hash=owned[temp];owned.Remove(temp);owned.Add(target,hash);}
            WriteNew(catalogTemp,catalogText);
            if(!oldCatalog.AsSpan().SequenceEqual(File.ReadAllBytes(catalogPath)))throw new IOException("资料索引被外部修改，请重新注册");
            File.Move(catalogTemp,catalogPath,true);owned.Remove(catalogTemp);catalogWritten=true;
            documents.List();return Find(tool.Id);
        }
        catch(Exception failure)
        {
            var conflicts=new List<string>();
            string expectedCatalogHash=catalogWritten?catalogHash:RequirementStore.Hash(oldCatalog);
            if(!File.Exists(catalogPath)||RequirementStore.Hash(File.ReadAllBytes(documents.SafePath("Docs/catalog.json")))!=expectedCatalogHash)conflicts.Add("Docs/catalog.json");
            foreach(var (file,hash) in owned)
            {
                string safe=documents.SafePath(Path.GetRelativePath(project.Root,file));
                if(!File.Exists(safe)||RequirementStore.Hash(File.ReadAllBytes(safe))!=hash)conflicts.Add(Path.GetRelativePath(project.Root,safe));
            }
            // catalog、指南和声明相互关联：任一冲突保留整组最终文件，不能留下断链入口。
            if(conflicts.Count>0)
            {
                foreach(var (file,hash) in owned.Where(e=>e.Key.EndsWith(".tmp",StringComparison.Ordinal)))
                {string safe=documents.SafePath(Path.GetRelativePath(project.Root,file));if(File.Exists(safe)&&RequirementStore.Hash(File.ReadAllBytes(safe))==hash)File.Delete(safe);}
                throw new IOException("注册失败；关联文件组整体保留，需人工核对冲突："+string.Join("、",conflicts),failure);
            }
            if(catalogWritten)
            {
                string rollback=documents.SafePath("Docs/catalog.json.rollback-"+Guid.NewGuid().ToString("N")+".tmp");
                using(var stream=new FileStream(rollback,FileMode.CreateNew,FileAccess.Write,FileShare.None))stream.Write(oldCatalog);
                File.Move(rollback,catalogPath,true);
            }
            foreach(string file in owned.Keys)File.Delete(documents.SafePath(Path.GetRelativePath(project.Root,file)));
            throw;
        }
    }
    private static void Validate(ExtensionTool tool)
    {
        if(!Regex.IsMatch(tool.Id,"^[a-z][a-z0-9-]{0,69}$")||string.IsNullOrWhiteSpace(tool.Title)||tool.Title.Length>100||tool.TimeoutSeconds is <1 or >3600)throw new ArgumentException("工具需有效英文编号、标题与1–3600秒超时");
        if(tool.Kind is not ("manual" or "unity-mcp" or "external"))throw new ArgumentException("工具类型应为manual、unity-mcp或external");
        if(tool.DefaultArguments.ValueKind!=JsonValueKind.Object)throw new ArgumentException("默认参数必须为JSON对象");
        if(tool.Kind=="external"&&(string.IsNullOrWhiteSpace(tool.Executable)||tool.Arguments.Length>100))throw new ArgumentException("外部工具需可执行程序与参数列表");
        if(tool.Kind=="unity-mcp"&&string.IsNullOrWhiteSpace(tool.ToolName))throw new ArgumentException("Unity工具需明确MCP名称");
        if(tool.UnityBuild&&tool.Kind!="unity-mcp")throw new ArgumentException("Unity构建标记仅支持Unity MCP工具");
        if(tool.UnityBuild&&tool.ReadOnly)throw new ArgumentException("Unity构建不是只读操作");
    }
}
public interface IExtensionToolRunner
{
    string Kind { get; }
    Task<string> Run(ExtensionTool tool,JsonElement arguments,CancellationToken ct);
}
public sealed class ExternalToolRunner(ProjectContext project) : IExtensionToolRunner
{
    public string Kind=>"external";
    public static string[] ExpandArguments(ExtensionTool tool,JsonElement arguments)=>tool.Arguments.Select(arg=>Regex.Replace(arg,@"\{([A-Za-z][A-Za-z0-9_]*)\}",m=>arguments.TryGetProperty(m.Groups[1].Value,out var value)?value.ValueKind==JsonValueKind.String?value.GetString()!:value.GetRawText():throw new ArgumentException("缺少参数："+m.Groups[1].Value))).ToArray();
    public async Task<string> Run(ExtensionTool tool,JsonElement arguments,CancellationToken ct)
    {
        string executable=tool.Executable!;
        if(executable.Contains('/')||executable.Contains('\\')) executable=Path.GetFullPath(Path.Combine(project.Root,executable));
        string[] args=ExpandArguments(tool,arguments);
        var result=await ProcessRunner.RunAsync(executable,args,project.Root,ct:ct,ownDescendants:true);
        if(result.ExitCode!=0)throw new InvalidOperationException($"外部工具退出码 {result.ExitCode}\n{result.Stderr}\n{result.Stdout}");
        return result.Stdout+(string.IsNullOrWhiteSpace(result.Stderr)?"":"\n标准错误：\n"+result.Stderr);
    }
}
public interface IUnityToolClient
{
    bool Connected { get; }
    void ValidateTool(string name);
    Task<JsonElement> Call(string name,object arguments,CancellationToken ct=default);
    Task VerifyProject(string root,CancellationToken ct=default);
}
public sealed class UnityToolRunner(ProjectContext project,Func<IUnityToolClient?> connection,Action<ExtensionTool>? beforeCall=null) : IExtensionToolRunner
{
    public string Kind=>"unity-mcp";
    public async Task<string> Run(ExtensionTool tool,JsonElement arguments,CancellationToken ct)
    {
        var client=connection(); if(client==null||!client.Connected)throw new InvalidOperationException("请在AI对话页连接并核对Unity");
        client.ValidateTool(tool.ToolName!);
        await client.VerifyProject(project.Root,ct);
        ct.ThrowIfCancellationRequested();beforeCall?.Invoke(tool);
        return (await client.Call(tool.ToolName!,arguments,ct)).GetRawText();
    }
}
public sealed class ExtensionToolRun
{
    public string Id { get; set; }=Guid.NewGuid().ToString("N");
    public string ToolId { get; set; }="";
    public string State { get; set; }="执行";
    public string Output { get; set; }="";
    public string? ReviewId { get; set; }
    public DateTimeOffset Started { get; set; }=DateTimeOffset.Now;
    public DateTimeOffset? Ended { get; set; }
}
public sealed class ExtensionToolService(ProjectContext project,ExtensionToolCatalog catalog,IEnumerable<IExtensionToolRunner> runners)
{
    private readonly Dictionary<string,IExtensionToolRunner> adapters=runners.ToDictionary(r=>r.Kind,StringComparer.Ordinal);
    public ExtensionToolRun[] ListRuns()
    {
        string folder=project.StatePath("tool-runs");
        return Directory.Exists(folder)?Directory.EnumerateFiles(folder,"*.json").Select(p=>JsonSerializer.Deserialize<ExtensionToolRun>(File.ReadAllText(project.StatePath("tool-runs/"+Path.GetFileName(p))),DocumentLibrary.Json)??throw new InvalidDataException("工具运行记录损坏")).OrderByDescending(r=>r.Started).Take(30).ToArray():[];
    }
    public void Save(ExtensionToolRun run)
    {
        if(!Regex.IsMatch(run.Id,"^[a-f0-9]{32}$"))throw new ArgumentException("工具运行编号无效");
        string folder=project.StatePath("tool-runs");Directory.CreateDirectory(folder);
        string path=project.StatePath("tool-runs/"+run.Id+".json"),temp=project.StatePath("tool-runs/"+run.Id+".json.tmp");File.WriteAllText(temp,JsonSerializer.Serialize(run,DocumentLibrary.Json));File.Move(temp,path,true);
    }
    public void ValidateExecution(ToolDescriptor descriptor,JsonElement arguments)
    {
        var fresh=catalog.Find(descriptor.Definition.Id);if(fresh.Hash!=descriptor.Hash)throw new InvalidOperationException("工具声明已变，请重新载入");
        if(arguments.ValueKind!=JsonValueKind.Object||arguments.GetRawText().Length>100000)throw new ArgumentException("工具参数必须为不超过十万字的JSON对象");
        if(!adapters.ContainsKey(fresh.Definition.Kind))throw new InvalidOperationException("此项只有说明，尚未配置执行入口");
        if(fresh.Definition.Kind=="external")ExternalToolRunner.ExpandArguments(fresh.Definition,arguments);
    }
    public async Task Execute(ExtensionToolRun run,ToolDescriptor descriptor,JsonElement arguments,CancellationToken ct)
    {
        try
        {
            ValidateExecution(descriptor,arguments);Save(run);
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(descriptor.Definition.TimeoutSeconds));
            run.Output=await adapters[descriptor.Definition.Kind].Run(descriptor.Definition,arguments,deadline.Token);run.State="完成";
        }
        catch(Exception e){run.Output=e.Message;run.State=e is OperationCanceledException?"已取消或超时":"失败";}
        finally{run.Ended=DateTimeOffset.Now;Save(run);}
    }
}
