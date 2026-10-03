# 客户端与服务端资源构建

## 参考需求



- [按对局加载内容闭包](../../requirements/configuration-content/REQ-FEAT-018_match-content-closure.md)：目标实现、技术方案、边界情况、附录，引用版本 1。
- [客户端视图与服务器资源隔离](../../requirements/configuration-content/REQ-FEAT-019_client-server-resources.md)：目标实现、技术方案、边界情况、附录，引用版本 1。
- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况、附录，引用版本 1。
- [攻击技能动画与插值采样](../../requirements/presentation-ui/REQ-FEAT-073_animation-sampling.md)：目标实现、技术方案、边界情况、附录，引用版本 1。
- [特效音效与回滚账本](../../requirements/presentation-ui/REQ-FEAT-074_presentation-ledger.md)：目标实现、技术方案、边界情况、附录，引用版本 1。

## 原验收范围（历史取证）

当期已记录资源拆分、客户端异步视图与 focused Editor/PlayMode 结果。以下是原验收清单。本轮依据用户确认关闭，不补造逐项机器回执：

- [ ] 当前 Windows 客户端与 Linux Dedicated Server 的配套构建完成。
- [ ] 运行客户端验证本地 catalog/Logic+Client 资源、视图延迟绑定、回池/回滚和句柄释放。
- [ ] 服务器启动只加载 Logic-*，不初始化客户端服务、无 Client-* bundle 和模型/动画/材质/VFX/音频/UI 依赖。
- [ ] 对比稳定排序的 AssetDatabase 依赖 manifest 与构建实际输出，检查版本/哈希、组和默认组恢复。
- [ ] 匹配客户端/服务器实际对局走通大厅加载、授权与 Gameplay；记录外部运行日志与结果。

## 执行与验收细节

构建通过 LocalNgoBuildMenu 既有入口，一次请求后立即停止 Unity 操作，等待用户“构建结束”。不自动重复构建。随后人工运行配套端点；确切输出目录与参数见资源/操作指南。

客户端正常/失败加载和生命周期测试输入包含加载前销毁、pool reuse、回滚重建与缺地址；预期不绑定旧生命、各句柄释放一次，失败可见。服务器按依赖审计检查禁用资源，发现泄漏构建或验收失败。Editor 用例不能代替真实构建产物检查。

## 当前结论

用户本轮明确确认该计划应已关闭，按用户确认结束原执行批次。本轮仅核对记录，没有重跑 Unity 测试、资源构建或配套运行，不把本轮关闭登记当作新机器通过证据。

## 本期核查结论

用户本轮明确确认该计划应已关闭，按用户确认结束原执行批次。本轮仅核对记录，没有重跑 Unity 测试、资源构建或配套运行，不把本轮关闭登记当作新机器通过证据。

## 当前关闭结论

用户本轮明确确认该计划应已关闭，按用户确认结束原执行批次。本轮仅核对记录，没有重跑 Unity 测试、资源构建或配套运行，不把本轮关闭登记当作新机器通过证据。

此前源码核查与历史测试范围保留为证据，不再把原批次列为待确认事项；后续补充测试/资源改造应另建计划。
