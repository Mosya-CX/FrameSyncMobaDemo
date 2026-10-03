# Unity资源与打包规则

## Unity 操作

使用已连接 Unity MCP 执行工程/包/场景/Prefab/ScriptableObject 查询、资源创建修改、刷新编译、Console 和测试。手动 YAML 不能绕开可用 Unity API。MCP 失败记操作、失败、fallback 风险与最终 Unity 验证。

## 资产边界

资产移动保持 GUID；逻辑空间权威与表现分离。GlobalPrefabTable 是唯一 PrefabKind+PrefabId 聚合，Core/Map/Hero 子表不是第二套注册表。客户端与服务器的内容闭包和依赖审计按现行需求。

## 打包纪律

打包只发一次，发出后停止所有 Unity 操作，等待用户报告结束；不轮询、不重发。LocalNgoBuildMenu.BuildBoth() 是本地 C/S 入口；BuildServerLinux() 是 UOS Linux 入口。

Builds 是忽略的生成输出。只有用户接受的分发 ZIP 放 Release/<version>/Client、Server，不 force-add Builds。正式可选 CDN 与测试包目录分离，私钥/凭据不提交。
