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

旧计划 137 份来源保留完整追溯，共 134 个独立执行计划（含Karolina新增计划），候选不当作执行计划。旧验收报告归入相应计划，不在工程目录继续积压。无法确认的实况见 [待确认清单](rules/facts/FACT-9CD2F2F2C7BD_open-questions.md)。用户指令优先，原材料恢复档仅作取证。

当前 Karolina 产品改造：[工作模式、规则板块与拓展工具](requirements/karolina/REQ-KAR-007_workspace-modes-tools.md)，[实施计划](plans/karolina/PLAN-KAR-006_workspace-modes-tools.md)。[架构审查](rules/facts/FACT-KAR-001_karolina-architecture-review.md)列明当前符合程度。任务审批仍遵循[任务变更审批与反馈再执行](requirements/karolina/REQ-KAR-006_task-change-approval.md)，Git 产品界面已退役。

现有功能打磨入口：[文档与资源索引](requirements/karolina/REQ-KAR-010_document-context-and-resource-index.md)、[有来源的实时审批](requirements/karolina/REQ-KAR-011_attributed-live-task-review.md)、[构建与GUI工具](requirements/karolina/REQ-KAR-012_build-and-launcher-tools.md)，[实施与验收](plans/karolina/PLAN-KAR-009_existing-function-polish.md)。旧计划资源归属无法确认的目录/路径见[索引迁移清单](plans/evidence/PLAN-KAR-009_resource-index.json)，不据文件存在推定已完成。

2026-10-05校正：参考资料类型/标签检索与资源入口明确化；资源审批以逐文件操作说明呈现，人工GLB排除，Markdown单独缩放，工具同页多函数。实现与当前实测见PLAN-KAR-009；冻结差异分析不证明作者或人工验收。
