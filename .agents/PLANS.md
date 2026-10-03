# 实施计划规则入口

跨程序集、公开合同、序列化/快照/校验、帧同步/网络、多次会话或高风险建立计划；小任务可在对话列步骤。

计划路径 `Docs/plans/<模块>/<编号>_<英文功能标题>.md`，正文和界面中文；同名 `.meta.json` 保存编号、编码、六阶段状态、关系和证据。模板见 Docs/rules/templates/RULE-004_plan-template.md，周期见 RULE-002_agent-execution-cycle.md。

六阶段为准备、执行、测试、校正、验收、关闭。测试含机器/Agent，验收含用户人工，失败进校正。关闭不重启；本轮 activePlan 由 Docs/catalog.json 定位，人工验收前不自动关闭。
