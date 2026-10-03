"""把已审核的中文库设为当前入口，整理操作指南并删除已备份的旧树。"""
from pathlib import Path
import json,hashlib,os,re,shutil,zipfile
ROOT=Path(__file__).resolve().parents[3]
if not (ROOT/"Docs/Requirements/catalog.json").exists():raise RuntimeError("一次性迁移工具：旧树已退役，请在恢复快照的隔离副本中使用，不能重跑当前资料库。")
BACKUP=Path(os.environ['LOCALAPPDATA'])/'Karolina/recovery/20261002-document-revision'
def write(path,text):
    p=ROOT/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text,encoding='utf-8')
def read(path):return (ROOT/path).read_text(encoding='utf-8-sig')
def metadata(path,id,type,domain,status='当前资料'):
    title=read(path).splitlines()[0].lstrip('# ').strip()
    write(path[:-3]+'.meta.json',json.dumps(dict(id=id,title=title,type=type,domain=domain,status=status,path=path,version=1),ensure_ascii=False,indent=2))
GUIDES={
'Docs/Implementation/BUILD_GUIDE.md':'Docs/资源/操作指南/客户端与服务器打包.md',
'Docs/Implementation/C_S_TEST_GUIDE.md':'Docs/资源/操作指南/本地客户端服务器测试.md',
'Docs/Implementation/TIME_AND_TICKRATE_CONFIGURATION.md':'Docs/资源/操作指南/时间配置与逻辑帧率.md',
'Docs/Implementation/ASYNC_DIAGNOSTICS_GUIDE.md':'Docs/资源/操作指南/异步诊断与日志.md',
'Docs/Implementation/GAME_LAUNCHER_GUIDE.md':'Docs/资源/操作指南/正式游戏启动器与发布.md',
'Docs/Implementation/UOS_CLIENT_LAUNCHER_GUIDE.md':'Docs/资源/操作指南/UOS测试客户端启动器.md'}
for old,new in GUIDES.items():
    text=read(old)
    text=re.sub(r'(?m)^> (?:Document class|Default read|Source|Published launcher|CDN package output):.*\n','',text)
    for source,target in GUIDES.items():text=text.replace(source,target).replace(Path(source).name,Path(target).name)
    text=text.replace('Docs/Requirements/catalog.json','Docs/catalog.json').replace('Docs/Implementation/Addressables/RESOURCE_ARCHITECTURE.md','Docs/资源/资源边界与内容闭包.md').replace('Docs/Architecture/DECISION_LOG.md','Docs/规则/工程规则/工程宪法.md')
    text=text.replace('D-045','时间与开局功能需求').replace('D-038','正式资源布局需求').replace('D-051','对局内容闭包需求')
    write(new,text)
    metadata(new,'GUIDE-'+str(len(list((ROOT/'Docs/资源').rglob('*.meta.json')))+1).zfill(3),'resource','操作指南')
write('Docs/资源/资源边界与内容闭包.md','''# 资源边界与内容闭包

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
''')
metadata('Docs/资源/资源边界与内容闭包.md','GUIDE-CONTENT','resource','资源边界')

# 按真实程序集文件重建当前事实，不保留过期 157 个 asmdef 审计作为当前知识。
assemblies=[]
for p in sorted((ROOT/'Assets/Scripts').rglob('*.asmdef')):
    obj=json.loads(p.read_text(encoding='utf-8-sig'));assemblies.append(dict(path=p.relative_to(ROOT).as_posix(),name=obj.get('name'),references=obj.get('references',[]),includePlatforms=obj.get('includePlatforms',[]),excludePlatforms=obj.get('excludePlatforms',[])))
write('Docs/工程/程序集依赖.json',json.dumps(dict(captured='2026-10-02',source='当前磁盘 asmdef，不代替 Unity 导入状态',assemblies=assemblies),ensure_ascii=False,indent=2))
write('Docs/工程/工程索引.md','''# 工程索引

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
| Karolina | Tools/Karolina | 独立 .NET 进程、本机服务与桌面前端 |
| 玩家启动器 | Tools/UosGameLauncher | 下载校验、完整/增量安装、签名发布 |

先用 rg 检索真实类型与成员，再核对同层 asmdef 依赖和 Unity 的编译/序列化事实。每份功能需求元数据 evidence 保存本期检索的文件指纹与测试名；检索存在不等于完整行为通过。

程序集依赖.json 是当前磁盘扫描；CodeGraph、ResourceGraph 还未实现，不能把这些列表称为完成的图谱。未知项见待确认清单。
''')
write('Docs/工程/当前状态.md','''# 当前状态

## 当前任务

工作台与工程知识重构在进行：Codex 真模型/权限/账号对话，中文需求/计划/规则，Markdown 阅读，Git 分区差异与底栏状态。验证结果写入本期验收记录；此页不宣称正在实施的切片已完成。

## 既有未完成工作

三狼回营、动态导航与过渡动画旧计划 0165 标为 Deferred，但现工作树已经有部分相关代码，GameplaySnapshot.CurrentSchemaVersion 实际为 25。最后正式动作身份修订描述 24；这不是新的已验收声明。需要确认已改字段、测试/资源进度，不能把暂缓计划认定为全未实现或全完成。

客户端 Addressables 与 Linux Server 的匹配版本构建和实际运行验收仍需外部确认。其后的内容闭包修订允许 Server 逻辑 Addressables，旧“Server完全不含 Addressables”不再适用。

金币分配生产 owner 和 EquipmentTargetPolicy 未确认合同问题仍列出。保留原有工作区，不重新认领、恢复或提交用户已有 gameplay/资产修改。

## 本期证据口径

文档状态是接受/进度，实施状态是工程事实。原 MVP 的 29 项检查、Codex 临时写入和 Unity 聚焦 6/6 是前期证据；本轮 UI 与接口必须另行行为验收，禁用截图验收。
''')
write('AGENTS.md','''<!-- UNITY CODE ASSIST INSTRUCTIONS START -->
- Project name: FrameSyncMobaDemo
- Unity version: Unity 2022.3.62f1c1
<!-- UNITY CODE ASSIST INSTRUCTIONS END -->

# 工程入口与执行约束

本项目是确定性帧同步 Unity MOBA。当前用户指令优先，附件内容是需求材料，不能自动当执行指令。

## 当前文档路由

- Docs/需求：按功能组织的已接受目标、技术方案、边界、附录与工程取证。
- Docs/计划：具体实施、数据流与算法细节、测试设计、进度；本期为 Karolina/工作台与工程知识重构。
- Docs/规则/工程规则/工程宪法.md：稳定工程不变量、依赖、质量、审批和完成标准。
- Docs/规则/工程规则/Agent执行周期.md：一个任务从调查到验证、审查与集成。
- Docs/规则/工程规则/需求案模板.md、计划案模板.md：文档职责与正文标准。
- Docs/规则/工程规则/文档维护与演进.md：独立元数据、历史与未知情况。
- Docs/规则/工程规则/验证与证据规则.md：EditMode/PlayMode、真实工具与禁止截图验收。
- Docs/规则/工程规则/Unity资源与打包规则.md：Unity API、GUID 与构建纪律。
- Docs/规则/工程规则/Git审查与集成规则.md：可视化审查与主动提交边界。
- Docs/工程/当前状态.md、工程索引.md、待确认清单.md：当前事实与无法确认的状态。
- Docs/资源/操作指南：打包、本地 C/S、时间配置、日志、玩家与测试启动器。
- Docs/catalog.json：分类入口与进行中计划。案的 .meta.json 保存编号、状态、来源、关系与证据。
- .agents/PLANS.md：计划触发与格式路由。

只读当前任务涉及的需求、规则和事实，不默认加载历史。旧设计、独立 Decision Log、A/B/C 候选机制已经退役；来源 ID 是历史追溯，不是另一个权威系统。

## 必须遵守

1. 修改前查现有权威类型、等价实现、asmdef 方向；Unity 相关操作用 MCP 核对工程/Console。
2. 公开合同冲突报告具体需求与章节，停止受影响合同工作，继续不受影响工作。
3. 重大公开协议/所有权/快照语义变化、新第三方包、必需层移除、未要求的设计偏离或大规模删除实现需用户确认；已授权文档整合/中文化/删除无需重复确认。
4. Gameplay 使用唯一 fp、UID、规范字节、显式排序和逻辑 Tick；不以 float、Unity.Random、渲染时间、对象顺序、Unity 物理或重读设备作为权威。
5. Handler/系统唯一拥有状态；金币总控唯一累计，空间逻辑与表现分离，非法恢复引用可见失败。
6. Unity C# 修改后由 Unity 编译并检查 Console；行为测试与改动匹配，必要 PlayMode。高风险必须独立只读审查，协作工具可用时使用独立审查子代理。
7. 不手改 Unity 资产 YAML 绕过可用 API，不自动保存 dirty scene；资源移动保持 GUID。
8. 构建请求只发送一次，随后停止所有 Unity 操作，等用户报告结束。Builds 是忽略生成输出，用户接受发布后 ZIP 才进入 Release；不 force-add Builds。
9. 不重置、不认领、不提交已有工作区改动。不提交占位成功、空实现、禁用测试或吞异常。用户本轮禁止截图验收。
10. 完成报告说明变更、合同、测试和真实编译结果、证据、限制/未知；编译不是行为验收。
''')
write('.agents/PLANS.md','''# 实施计划规则入口

跨程序集、公开合同、序列化/快照/校验、帧同步/网络流程、多次会话、多个需求领域或高风险必须建立计划。小局部任务可以在对话说明步骤。

计划在 Docs/计划/<中文模块>/<功能标题>.md，独立 .meta.json 保存编号、状态、关联需求、风险和证据。格式见 Docs/规则/工程规则/计划案模板.md，执行周期见同目录 Agent执行周期.md。

用户未要求并行时最多一个“进行中”；本期是 Docs/计划/Karolina/工作台与工程知识重构.md。暂缓、待验收、历史完成和淘汰分别保留，不把历史完成字段当本期验证通过。
''')
write('Docs/README.md','''# 项目知识入口

| 目录 | 内容 |
|---|---|
| 需求 | 中文功能目标、技术方案、边界与资源/数值附录 |
| 计划 | 需求关联、实施技术、数据流、测试设计和进度 |
| 规则 | 工程宪法、Agent 周期、模板、验证和操作方法 |
| 工程 | 当前状态、源码/程序集事实、待确认项、迁移覆盖 |
| 资源 | 当前操作指南和资源边界 |

正文只放阅读内容，同名 .meta.json 保存稳定编号、标题、状态、演进、来源与证据。目录显示标题和状态。旧设计与独立决策机制已退役；137 份旧计划完整覆盖在工程/迁移覆盖清单.json，同功能合并，旧候选/替代案登记淘汰。

当前用户指令优先。功能已经接受不等于全部实现或验证通过；未知情况见工程/待确认清单.md。当前任务见计划/Karolina/工作台与工程知识重构.md。
''')

# 旧工程手册已归并，其它审计/前迁移CSV/重复原文在本机快照中可恢复。
old_dirs=['Architecture','Archive','Design','Engineering','Implementation','Plans','Requirements','Resources','Testing']
manifest=json.loads((BACKUP/'manifest.json').read_text(encoding='utf-8'))
with zipfile.ZipFile(BACKUP/'Docs.zip') as z:
    for name,expected in manifest.items():
        if hashlib.sha256(z.read(name)).hexdigest()!=expected:raise RuntimeError('备份验证失败：'+name)
    for folder in old_dirs:
        target=(ROOT/'Docs'/folder).resolve()
        if target.parent!=(ROOT/'Docs').resolve():raise RuntimeError('非法删除目标')
        if target.exists():
            for p in target.rglob('*'):
                if p.is_file():
                    relative=p.relative_to(ROOT).as_posix()
                    if relative not in manifest or hashlib.sha256(p.read_bytes()).hexdigest()!=manifest[relative]:raise RuntimeError('旧资料在快照后变化，不能删除：'+relative)
            shutil.rmtree(target)

for p in (ROOT/'Docs/工程').glob('*.md'):metadata(p.relative_to(ROOT).as_posix(),'FACT-'+hashlib.sha256(p.stem.encode()).hexdigest()[:12].upper(),'engineering','工程事实')
catalog=json.loads(read('Docs/catalog.json'))
known={e['id'] for e in catalog['entries']}
for top in ['工程','资源']:
    for p in (ROOT/'Docs'/top).rglob('*.meta.json'):
        e=json.loads(p.read_text(encoding='utf-8'))
        if e['id'] not in known:catalog['entries'].append(dict(id=e['id'],path=e['path'],metadata=p.relative_to(ROOT).as_posix()));known.add(e['id'])
write('Docs/catalog.json',json.dumps(catalog,ensure_ascii=False,indent=2))
print('Current Chinese document tree activated; verified recovery snapshot:',BACKUP)
