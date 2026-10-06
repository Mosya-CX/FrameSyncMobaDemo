# 工程索引

## 查找实现

| 模块 | 真实位置 | 作用 |
|---|---|---|
| 确定性基础 | Assets/Scripts/Deterministic | Tick 上下文、随机、字节序、参与者与动作身份 |
| Gameplay | Assets/Scripts/Gameplay | 单位、行为、战斗、技能、Buff、装备、移动与非英雄 |
| 帧同步 | Assets/Scripts/FrameSync | 命令、权威帧、快照、恢复、校验和与金币总控 |
| 空间 | Assets/Scripts/Physics | 定点几何、空间实体、网格与窄相位 |
| 输入 | Assets/Scripts/PlayerInput | 本地事件到 Command、门禁、Aim 与指示器 |
| 组合根 | Assets/Scripts/Bootstrap | 场景、网络、开局授权与编译/构建 Editor 工具 |
| 配置 | Assets/Scripts/RuntimeConfig | 静态配置、稳定目录、Bake 与版本 |
| 客户端资源 | Assets/Scripts/ClientContent | 本地资源加载、视图与句柄生命周期 |
| UI | Assets/Scripts/LuaBridge | Lua/C# 桥接、页面与只读查询 |
| Karolina（独立外部工具） | [Mosya-CX/Karolina](https://github.com/Mosya-CX/Karolina) | Unity 项目工作台；源码由独立公开仓库管理，产品文档保存在当前工程 Docs |
| 玩家启动器 | Tools/UosGameLauncher | 下载校验、完整/增量安装、签名发布 |

先用 rg 检索真实类型与成员，再核对同层 asmdef 依赖和 Unity 的编译/序列化事实。每份功能需求元数据 evidence 保存本期检索的文件指纹与测试名；检索存在不等于完整行为通过。

程序集依赖.json 是当前磁盘扫描；FrameSync Moba 自身的 CodeGraph、ResourceGraph 还未实现，不能把这些列表称为完成的图谱。独立 Karolina 工作台的静态索引不等同于本工程运行时依赖图。未知项见待确认清单。
