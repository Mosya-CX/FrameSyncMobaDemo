# 资源边界与内容闭包

## 现行组织

GlobalPrefabTable 是唯一 PrefabKind+PrefabId 聚合。正式根索引 Core、Map、Hero 子表地址、版本与依赖哈希。大厅锁定 MapConfigId 和全部槽位去重 HeroConfigId 集合，先异步加载逻辑分区并验证，再冻结同步查询表后进入初始快照和 Tick 0。

Addressables 在客户端和服务器都是本地、Tick 前的资源输送。客户端含 Logic-* 和 Client-*，服务器仅含 Logic-*，排除表现程序集、模型、Animator、材质、VFX、音频和 UI。旧“服务器完全不使用 Addressables”已被内容闭包改造替换。

远程 catalog、运行下载/更新、Remote Addressables 热更新均不启用。玩家启动器对 Player 文件的 CDN 分发是独立流程。

## 工程位置

- Assets/Scripts/RuntimeConfig/GlobalPrefabTable.cs、GlobalPrefabSubTableAsset.cs：正式根/子表合同。
- Assets/Scripts/ClientContent：客户端视图和句柄 owner；异步回执必须核对逻辑生命及池化代数。
- Assets/Scripts/Bootstrap：载荷、角色、网络调度、Editor 构建资源审计。
- Assets/AddressableAssetsData：当前实际分组，需用 Unity AssetDatabase 读取引用与依赖。
- Assets/Scenes：启动/大厅/对局场景，禁止手改 YAML 当作引用校验。

## 维护操作

新增资源选择已有 Kind 与稳定 ID，在所属分区添加路径型条目，维护内容版本/哈希。Unity 工具校验重复 ID、缺地址、跨分区依赖、客户端/服务器依赖泄露。当前工程事实见工程索引，正式功能合同见配置资源目录。

移动或重命名资源使用 Unity API 保持 GUID。不从异步完成顺序或 AssetDatabase 遍历分配玩法身份。全部句柄唯一拥有，失败或对局结束恰好释放一次。
