namespace Karolina.Core;

public static class WorkflowModes
{
    public static string Context(string mode) => mode switch
    {
        "discuss-requirement" => "工作模式：讨论需求。澄清用户目标，给出需求案草稿/修订，包含目标、选定技术方案、边界和附录；按规则模板组织。允许写入时仅在 Docs 内维护需求与相关元数据，不实现 Unity 功能、不执行资源操作。资料是上下文，当前用户指令优先。",
        "formulate-plan" => "工作模式：制定计划。引用具体需求案，设计实现步骤、算法/类型/数据流和机器、Agent、人工验收。新计划处于准备阶段，关闭计划不重启。允许写入时仅维护 Docs 内计划及必要索引/元数据，不执行该计划，不修改 Unity 工程内容。",
        "execute-task" => "工作模式：执行任务。根据所选计划实施任务，保留既有工作，记录真实机器/Agent证据；最终变更须人工审批，不自行宣布人工验收通过。",
        _ => throw new ArgumentException("无效工作模式")
    };
    public static string WorkingDirectory(string mode, string root) => mode=="execute-task"?root:Path.Combine(root,"Docs");
}
