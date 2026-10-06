# 项目知识入口

|目录|内容|
|---|---|
|requirements · 需求案|目标、技术方案、边界、附录与正文需求演进|
|plans · 计划案|具体需求关联、算法/结构/数据流、六阶段执行与证据|
|rules/facts · 工程事实|当前状态、待确认、资源边界与架构审查|
|rules/execution · 执行细则|工程宪法、Agent 周期、资源/构建/审批纪律|
|rules/index · 项目索引|工程入口与程序集引用资料|
|rules/templates · 模板|需求案和计划案模板|
|tools/guides、tools/registry · 拓展工具|配套说明与可注册调用声明，指南不混入规则案|

文件名使用编号加英文功能标题，正文、UI 模块和标题使用中文；物理分类、模块目录及 JSON 文件名均为英文。独立元数据保存索引、关系、版本、来源、证据与最多10个标签；计划事前文件增改删预估和事后真实关联分开保存，旧计划缺失预估明确标记，不追溯补造；只有需求有正文演进。需求激活/废弃，计划准备/执行/测试/校正/验收/关闭，规则无状态。计划按编码排序，当前活动计划由 catalog.json 指定。

候选不当作执行计划。旧验收报告归入相应计划，不在工程目录继续积压。无法确认的实况见 [待确认清单](rules/facts/FACT-9CD2F2F2C7BD_open-questions.md)。用户指令优先，原材料恢复档仅作取证。

Karolina 应用源码位于[独立公开 GitHub 仓库](https://github.com/Mosya-CX/Karolina)；它的产品需求、计划、规则、架构事实与索引只保存在当前依附工程的 `Docs` 目录，不放进应用源码仓库。当前工程中的 Karolina 文档按 `requirements/karolina`、`plans/karolina`、`rules/facts` 与 `rules/execution` 分类，并由本目录 `catalog.json` 登记。有关任务审批的工程规则见 [RULE-008](rules/execution/RULE-008_git-review-integration.md)，产品目标见 [REQ-KAR-006](requirements/karolina/REQ-KAR-006_task-change-approval.md)。
