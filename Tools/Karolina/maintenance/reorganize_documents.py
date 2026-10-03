"""语义映射清单与中文案生成。旧资料先完整备份到本机，清理另行执行。"""
from pathlib import Path
import hashlib,json,os,re,zipfile,datetime,collections
ROOT=Path(__file__).resolve().parents[3]
if not (ROOT/"Docs/Requirements/catalog.json").exists():raise RuntimeError("一次性迁移工具：旧树已退役，请在恢复快照的隔离副本中使用，不能重跑当前资料库。")
LOCAL=Path(os.environ['LOCALAPPDATA'])/'Karolina'
STAMP='20261002-document-revision'
BACKUP=LOCAL/'recovery'/STAMP
BACKUP.mkdir(parents=True,exist_ok=True)
snapshot=BACKUP/'Docs.zip'
if not snapshot.exists():
    hashes={}
    with zipfile.ZipFile(snapshot,'w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted((ROOT/'Docs').rglob('*')):
            if p.is_file():
                name=p.relative_to(ROOT).as_posix();z.write(p,name);hashes[name]=hashlib.sha256(p.read_bytes()).hexdigest()
    (BACKUP/'manifest.json').write_text(json.dumps(hashes,ensure_ascii=False,indent=2),encoding='utf-8')
def read(p):return (ROOT/p).read_text(encoding='utf-8-sig')
def sha(p):return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
def write(p,text):
    p=ROOT/p;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text,encoding='utf-8')
def dump(p,obj):write(p,json.dumps(obj,ensure_ascii=False,indent=2)+'\n')
entries=[];features={};source_coverage=[];plan_coverage=[]
def document(id,title,kind,domain,body,status='已接受',**meta):
    folder={'requirement':'需求','plan':'计划','rule':'规则'}[kind]
    path=f'Docs/{folder}/{domain}/{title}.md'
    write(path,f'# {title}\n\n{body.strip()}\n')
    metadata=dict(id=id,title=title,type=kind,status=status,domain=domain,path=path,version=1,**meta)
    dump(path[:-3]+'.meta.json',metadata);entries.append(dict(id=id,path=path,metadata=path[:-3]+'.meta.json'))
    return metadata

# 每行是一个可独立讨论、实施、验收的功能。技术方案由功能语义确定，不从旧标题生成。
def feature(key,domain,title,goal,tech,boundary,symbols,decisions=''):
    features[key]=dict(domain=domain,title=title,goal=goal,tech=tech,boundary=boundary,symbols=symbols.split(),decisions=[int(n) for n in decisions.split(',') if n],clauses=[],sources=[])

feature('flow','比赛流程','应用启动与跨场景流程','客户端和服务器能从启动进入大厅、对局和结果页。','GameApplicationFlowManager 管理逻辑状态，GameSessionContext 负责跨场景交接；NGO 根保持单一生命周期。客户端 ClientBootstrap、服务器 ServerBootstrap 均依次进入 Lobby、GameScene。','本地直连与 UOS 在线各有唯一连接生命周期负责人；模式变化不能让两套回调同时通知连接。','GameApplicationFlowManager GameSessionContext LocalNgoEndpointDriver','26')
feature('lobby','比赛流程','大厅槽位与开局配置','锁定玩家、英雄和地图后形成各端一致的开局配置。','LobbySessionFlowNetwork 和 LobbyNetworkBridge 管理玩家槽位与屏障；GameStartConfig 固定 PlayerSlotConfig 列表。英雄可重复选择，正 HeroConfigId 由内容闭包校验。','断线、重复 Ready、人数不一致、内容或版本不一致必须有明确失败状态；大厅消息不进入 GameplayCommand。','LobbySessionFlowNetwork LobbyNetworkBridge GameStartConfig PlayerSlotConfig','28,46')
feature('launch','比赛流程','加载确认与单调时钟开局屏障','各端完成内容和初始状态加载后，在同一开局授权下启动 Tick 0。','采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。','不得继续执行旧的载荷携带 UTC 授权方案；Ready 不执行 Gameplay；延迟、重复确认与过期授权不启动第二次对局。','LobbyNetworkBridge FrameSyncNetworkBridge GameBootstrap LaunchCommit','33,44,45')
feature('uos','比赛流程','UOS 配置与连接模式','在线模式使用一处配置并正确衔接匹配与 NGO 连接。','UosApplicationConfig 读取 Unity.UOS.Common.Settings.MatchmakingConfigID；命令行仅作显式覆盖。本地模式由 LocalNgoEndpointDriver 拥有，UOS 模式由 LobbyFlowController 拥有。','匹配配置 ID 与启动 Profile ID 不可混用；令牌和服务器密钥不进源码或共享日志。','UosApplicationConfig UosClientSession LobbyFlowController','27')
feature('tick','帧同步','逻辑时钟与 Tick 推进','所有确定性系统读取同一个 Tick 含义和执行模式。','ServerTick、LocalSimulationTick 都是下一待执行 Tick；LatestAuthorityFrameTick 是最近连续接受权威帧，SnapshotTick 是恢复后下一 Tick。SimulationTickContext 提供只读上下文。','预测领先上限和每 Unity 帧执行上限明确；重演不读取渲染耗时或输入设备。','SimulationTickContext FrameSyncClock FrameSyncGameRuntime','1')
feature('pipeline','帧同步','全局阶段与同步 Tick 管线','同一 Tick 的处理顺序可解释且各端一致。','SimulationTickPipeline 按全局 Handler 子阶段推进 Tag、Buff、Equipment、HitReaction、Ability、Movement、Attack，然后封存并结算战斗波次。','UnitUid 只用于稳定遍历，不能通过先处理整只单位制造跨 Handler 优势；捕获前瞬态队列必须清空。','SimulationTickPipeline CombatSystem','8,49')
feature('command','帧同步','命令序列化与类型化派发','玩家意图成为能重发、对账和重演的确定性命令。','GameplayCommand 采用唯一 CommandHeader 与强类型负载、规范字节序；CommandDispatcher 按正式 Command 类型进入所属系统。','完整 canonical 字节参与对账；无效单位或槽位不能按示例静默修复；输入事件仅翻译一次。','GameplayCommand CommandHeader CommandDispatcher CanonicalByteWriter','2')
feature('collector','帧同步','命令合并转发与幂等重发','同一输入重复送达时不产生第二个 Gameplay 事实。','CommandCollector 保持 TargetTick、CommandSeq 与来源身份，GameplayCommandBundle 和 AcceptedCommandRelay 保留规范顺序。','已执行预测 Tick 收到 Relay 时走正常脏 Tick 纠错；相同标识但不同字节必须报冲突。','CommandCollector GameplayCommandBundle AcceptedCommandRelay','2')
feature('targettick','帧同步','自适应命令目标 Tick','网络时延变化时合理选择命令执行 Tick，仍保留静态下界。','静态下界=max(LocalSimulationTick+1, LatestSynchronizedServerTick+MinCommandLeadTicks)。RTT 使用整数 SRTT 与 RTTVar，按半 RTT、抖动预算、处理预算估计服务器 Tick，再以本地和估计服务器未来窗口封顶。','冷启动、样本过少或陈旧时返回静态下界；同一模拟 Tick 的命令复用一个 TargetTick；开局前样本年龄不能冒充 Gameplay 已推进。','CommandTargetTickResolver CommandTimingEstimator CommandTimingSnapshot','53')
feature('authority','帧同步','权威帧校验与恢复','客户端能够确认一致结果并通过现有本地快照恢复缺失权威帧。','AuthorityFrame 必须含 SharedGameplayChecksum；完整 Command 字节和金币批次摘要参与校验。AuthorityRecovery 仅补发缺失帧。','不提供进程重启恢复、局中加入或 BaseSnapshot；本地恢复锚点丢失即终止当前对局连接。','AuthorityFrameReplicator AuthorityFrame LocalFrameVerificationRecord','2,3')
feature('rollback','帧同步','预测回滚与逐帧重演','预测和权威不一致时，从合法锚点恢复并重演得到权威结果。','PredictionRollbackCoordinator 每 Tick 保存快照，普通回滚起点不早于 LatestAuthorityFrameTick+1；先 Restore，再 Resolve，最后 Rebuild。','不倒退已接受金币基线、不重读设备输入；失效确定性引用明确失败；每个 AuthorityFrame 形成单 Tick 接受屏障。','PredictionRollbackCoordinator RollbackFrameSnapshot RollbackSnapshotBuffer','1,4,6')
feature('snapshot','帧同步','快照树与字段归属','跨 Tick 状态都由其唯一模块拥有并可规范捕获、恢复和校验。','GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。','Tick 内工作缓存不进入快照；恢复不能猜测缺字段；版本与 GameplayDataVersion 同步推进。','GameplaySnapshot UnitWorldSnapshot IRollback SharedGameplayChecksum','4,11,12,50')
feature('checksum','帧同步','共享校验与分段诊断','不一致可定位到随机、单位、战斗、投射物、商店、物理或金币摘要段。','SharedGameplayChecksum 规范序列化并按稳定键排序；-checksumDetail 输出分段和逐单位 Handler 摘要。','诊断不改变 Gameplay，不依赖集合插入顺序；StatSeq、配置 ID 与正式序列规则一致。','SharedGameplayChecksum WorldDumpBuilder StatHandlerSnapshot','32,43')
feature('random','确定性基础','确定性随机与定点计算','相同种子、状态、请求得到可重演的随机与数值。','唯一随机服务维护显式 Snapshot 状态，集合采样定义稳定顺序；权威类型为 Unity.Mathematics.FixedPoint.fp，作者 float 仅在验证/Bake 边界转换一次。','不新增定点类型；动作暴击使用动作键纯哈希，不消耗此共享随机流；禁止 UnityEngine.Random。','DeterministicRandomService DeterministicRandomSnapshot','22')
feature('uid','确定性基础','稳定 UID 与参与者身份','技术实体身份和玩法随机身份均有明确来源。','UnitUid、ProjectileUid 使用生成 Tick 与归属系统序列；GameplayParticipantId 从稳定出生来源建立，OriginActionId 由参与者、来源类型/ID、逻辑 Tick 和本地动作序列组成。','参与者缺失或重复可见失败；不得用 PrefabId、对象注册顺序、实例 ID 或队伍侧生成中性随机身份。','UnitUid ProjectileUid GameplayParticipantId OriginActionId','50')
feature('match','比赛流程','比赛结束与全端统计','基地毁灭形成一致结果，客户端等待对应权威帧后展示结算。','MatchRuleRuntime 管理阶段与预测结束候选；MatchStatisticsRuntime 在所有模拟端消费 FormalDeathResult。','预测结果不先落为最终结果；统计不只在 Dedicated Server 执行；账户持久化不反写 Gameplay。','MatchRuleRuntime MatchStatisticsRuntime MatchResultState','13')
feature('global','配置资源','全局配置与离线校验','Gameplay 使用验证完毕的稳定配置和版本握手。','GlobalGameplayData Bake 为定点与整数运行配置，PrefabKind 固定 Unit、Projectile、ParticleVfx、AudioEmitter、Misc。毫秒作者时间在 TickRate 明确后转换。','Editor 不能创造 PrefabKind 运行枚举；静态错误在 Tick 前暴露；时间不能硬编码旧 30 Hz。','GlobalGameplayData TickRateTimeAuthoring BakedGlobalGameplayData','19,22,45')
feature('content','配置资源','按对局加载内容闭包','只加载 Core、选定地图和去重英雄集合，随后确定性同步查询。','GlobalPrefabTable 是唯一聚合；生产根索引路径型子表，Addressables 在 Tick 0 前异步加载，组成对局内非序列化同步表。MapConfigId 和按 ID 排序的英雄集合决定闭包。','缺分区、重复 ID、版本/哈希或阵容不一致在初始快照前失败；不回退全量旧目录；禁止 WaitForCompletion。','GlobalPrefabTable GlobalPrefabSubTableAsset MatchContentScope GameplayContentPartition','38,48,51')
feature('servercontent','配置资源','客户端视图与服务器资源隔离','客户端视图延迟出现仍安全，服务器只保留逻辑依赖。','客户端视图可重建、只读逻辑；异步句柄有唯一 owner 和一次释放。服务器保留 Logic-* 本地 Addressables catalog/bundles，排除 Client-* 及表现程序集。','D-051 已修订早期“服务器完全排除 Addressables”和“逻辑 Prefab 必须直接引用”的规则；回滚、回池或销毁期间加载成功不能绑定旧生命。','ClientContentService ClientViewHost PresentationDependencyAudit','48,51')
feature('unit','单位','单位根与能力装配','单位由明确类型、空间引用、属性与 Handler 能力组成。','Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。','不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。','Unit UnitPrototype CapabilityState UnitTag','8')
feature('order','单位','意图规划与输入 Order','玩家命令和 AI 意图通过统一规划链产生类型化动作申请。','Command 先翻译为 Order，BehaviorPlanner 读取 Intent 与 Handler 只读状态产生 ActionRequest；Order 不保存寻路策略。','AI 不模拟物理按键或生成玩家网络命令；Planner 不能直接推进技能/攻击 Runtime。','BehaviorPlanner UnitOrder UnitIntent OrderTranslator','18')
feature('arbitration','单位','动作仲裁与固定执行器','移动、技能、普攻等并发申请按固定资源矩阵被接受或拒绝。','ActionArbiter 输出类型化 ActionSubmitResult；固定 Main/Base 槽位承载资源占用，Runtime 拥有执行状态。移除旧 action 列表与反射式申请。','按仲裁资源矩阵判断并发；Snapshot 保存 Main/Base 状态，reservation 由槽位派生；运行时不保存“被接受申请”的第二份权威。','ActionArbiter ActionSubmitResult ActionRuntimeSlot MainActionRuntime BaseActionRuntime','47')
feature('stats','单位数值','属性公式与 Modifier 所有权','装备、Buff 与技能可修改属性且可精确释放自己的修改。','StatHandler 采用正式 Base、Add、Ratio、Final 等槽位和锁定运算顺序；每个来源保有自己的 StatModifierHandle，StatSeq 提供稳定排序。','死亡不全局清空所有 Modifier；静态配置不含运行 Handle；同值操作不意外改变稳定身份。','StatHandler StatModifier StatModifierHandle StatDefinition','9,22')
feature('resources','单位数值','生命资源护盾与自然恢复','生命、法力和护盾状态有唯一拥有者并参与快照。','StatHandler 管理当前状态、最大值和护盾实例；Combat 的 Regen、Heal、Shield 管线读取正式数值与修正。','恢复状态后失效 Handle 不能静默丢弃；护盾吸收顺序固定；PendingDying 时治疗和护盾按所属管线合同处理。','StatHandler ShieldInstance NaturalRegenPipeline ShieldPipeline','9')
feature('experience','单位数值','经验成长与技能点','正式奖励立即发放经验，升级影响成长并产生待分配技能点。','ExperienceSettlement 分发确定性经验；StatHandler 按配置成长，AbilityHandler 通过 PendingSkillPoints 和类型化技能升级请求更新槽位。','等级上限、无效技能槽、技能已满和资源不足明确拒绝；技能级成长与 Stage 级配置不混用。','LevelExperienceConfig XpRewardTable AbilityRankUpEffectDef','13')
feature('statdirty','单位数值','属性脏标记与只读刷新','Tick 末和恢复后属性一致，UI 只读观察数值变化。','SimulationTickPipeline 在 Tick 末调用 StatHandler.FinalizeTick；WatchHook 由稳定修订通知观察者。','恢复导致 Dirty 不得制造客户端独有校验状态；WatchHook 和展示浮点不成为权威。','StatHandler StatChange WatchHook','33')
feature('events','单位','强类型单位事件与反应','伤害、施法、死亡等事实立即进入其固定监听者。','UnitEventBus 按正式事件数据强类型路由 Handler；支持列表和即时顺序明确，Reaction 只生成所允许的后续请求。','死亡/击杀事件回调在 T 立即发生，但新普通 Shield、Damage、Heal 延迟到 T+1，合法序列缺口不重编号。','UnitEventBus DamageEventData UnitEventMask','10,14')
feature('lifecycle','单位','同步生成死亡复活与回池','单位生死、复活、规则移除和池化形成单一生命周期。','UnitWorld 同步 Spawn；生成当 Tick 可被动参与，主动工作要求 CurrentTick>SpawnLogicTick。RequestEnterDying、RequestRecoverFromDying、ConfirmUnitDeath 是正式入口。','ClearForDeath、ClearForRespawn、ClearForDespawn 按固定顺序；永久 Buff 与装备跨死亡保留所属状态；回池新生命周期不沿用旧身份。','UnitWorld UnitRegistry UnitDisposePolicy UnitRespawnConfig','8,9')
feature('combatqueue','战斗','战斗请求封存与因果波次','相同战斗请求多重集不受提交和单位遍历顺序影响。','收集强类型 Shield、Damage、Heal 请求；封存成因果波次后才分配最终 SequenceInTick；同目标基于批次开始时冻结状态结算。','同批治疗封顶、盾参与吸收、总生命伤害一次提交；由结果产生的新反应进下一波；死亡反应产生的普通请求进下一 Tick。','CombatSystem CombatRequestHeader DeferredCombatRequest','10,49')
feature('damage','战斗','伤害配方抗性与吸血','伤害按正式配方和修正阶段计算，得到可记录的实际损失。','DamageRecipe/FormulaTerm 生成伤害，固定槽位 Modifier 合并后进入暴击、抗性、盾、生命、偷取及反应；超额伤害按定点权重分摊 ActualLifeDamage。','免疫、零伤害、纯盾伤害和纯过量不计击杀优势；非法请求仍报错；结构拒绝政策合法拒绝是成功空操作。','DamagePipeline DamageFormula DamageContext DamageResult','49,54')
feature('crit','战斗','动作键暴击与中性平局','技术 UID 重新标记不改变暴击样本和完全等距命中选择。','暴击纯 64 位 hash 输入 InitialMatchSeed、OriginActionId、目标 ParticipantId、EffectOrdinal 和固定域；投射物等距排序先中性分数再完整参与者身份。','不消耗共享随机流；只有完整身份/分数碰撞才用 TargetUnitUid；跨固定种子集检查不永久偏向阵营或 Prefab。','CombatNeutralScore OriginActionId GameplayParticipantId ProjectileHitCandidate','50')
feature('heal','战斗','治疗护盾与再生结算','治疗和护盾与伤害的批次结算能恢复合法生存状态。','HealPipeline、ShieldPipeline、NaturalRegenPipeline 接受明确强类型请求，使用正式 Modifier 与属性入口；同目标批次统一可用生命与盾。','治疗不超过 MaxHealth；Dying 与正常复活边界不能混用；外源结构治疗/护盾在中央入口拒绝。','HealPipeline ShieldPipeline NaturalRegenPipeline','49,54')
feature('modifiers','战斗','战斗公式修正与动态 Operand','Buff、装备和技能通过同一可追踪修正合同影响公式。','CombatModifierRecord 提供固定槽位、Operation、匹配过滤、受限线性 Operand 和 PolicyPatch；来源持有 CombatModifierHandle 并负责清理。','不允许任意脚本表达式决定确定性公式；同槽稳定合并；普通死亡不全局擦除其他来源。','CombatModifierRecord CombatModifierSet CombatOperand','9')
feature('death','战斗','濒死批次与公平击杀归属','正式死亡与击杀者在同一结算事实下确定。','Combat 同步请求 UnitWorld 更新 Dying/Dead；致死批次按有效敌方英雄 ActualLifeDamage 总和取最大，纯中性分数处理最高伤害并列。','旧末次伤害者方案已被修订；队伍、Prefab、提交序列不决定平局；FormalDeathResult 唯一输出给统计和奖励。','CombatDeathPipeline FormalDeathResult CombatContributionEventLog','9,35,49')
feature('reward','战斗','死亡奖励与贡献窗口','小兵、英雄、野怪、塔奖励使用可解释的整数与贡献规则。','DeathRewardContext 从正式死亡与贡献日志选收受者，整数稳定分配；经验立即结算，金币统一 RequestGoldIncome，批次摘要参与共享校验。','D-041 的生产者归属与既有复仇/击杀统计日志描述存在冲突，未确认部分不能静默改写；助攻窗口不受杀手修订而丢失。','DeathRewardContext DamageContributionTracker GoldIncomeAllocation','34,35,40,41,49')
feature('structure','战斗','建筑外源效果准入','建筑只接受规定的外源普通攻击并拒绝其他外源效果。','中央入口验证 CombatSourceType.Attack + CombatBuiltinSourceId.BasicAttack；Heal、Shield、Buff、Control 和 ForcedMove 在进入结算或存储前拒绝外源。','Attack 类型技能但不同 SourceId 和 AttackEffect 也拒绝；结构自身效果可合法；没有 Control Handler 的外源拒绝不得抛缺 Handler 异常。','StructureEffectAdmission CombatBuiltinSourceId BuffHandler CrowdControlHandler','54')
feature('attack','普通攻击','攻击周期规划与 Commit','普攻追击、前摇提交和后摇可准确恢复。','AttackHandler 锁定 StartLogicTick、ImpactLogicTick、NextAttackReadyLogicTick，Planner 决定追击停距；成立后立即转向，不增加转向前提。','Commit 前取消不造成命中；Commit 后取消后摇不缩短 Ready；死亡使失效攻击立即终止；恢复上一轮后摇不重新 Commit。','AttackHandler AttackPlanStatus AttackActionRuntime','9')
feature('attackoutput','普通攻击','近战远程与攻击特效输出','一次普攻 Commit 生成一次正式近战请求或投射物生成请求。','默认普通攻击采用固定来源和配方；远程输出 ProjectileSpawnRequest；AttackSequenceIndex 为确定性 byte，音效通过独立 SfxEvent。','在途飞弹目标锁定不跟随攻击者换目标；强化攻击、On-Hit 重复必须保留来源与动作身份，防止递归二次触发。','AttackHandler ProjectileSpawnRequest OnHitRepeatModule','14,39,50')
feature('projectiledef','投射物','定义与强类型生成黑板','定义配置与每次生成数据分离，运行实例保持有限且可快照状态。','ProjectileDef 配置逻辑、PrefabId、阶段模块和形状；SpawnBoard 是静态布局的强类型黑板，RequestSpawn 形成稳定 pending record。','逻辑配置 ID 与 PrefabId 不合并；不使用任意 object 字典；OnHitDamageOverride、MaxLifetime 与动作来源均保存到所属快照。','ProjectileDef ProjectileSpawnRequest SpawnBoard','12,30,50')
feature('projectileworld','投射物','提交运动寿命与回收','投射物在固定 Tick 阶段提交、移动、命中和回收。','ProjectileWorld 拥有每 Tick spawn sequence；CommitSpawns 是唯一创建入口，随后 AdvanceMotion、UpdateLifecycle、ResolveHits、EmitEffects、FlushDestroy。','FrameSync 不维护第二个投射物 BeginTick 序列；Pending 与 Active 状态均可恢复；池化实体和逻辑实例各有释放 owner。','ProjectileWorld ProjectileRuntime ProjectileSnapshot','12')
feature('projectilehit','投射物','命中过滤记忆与等距裁决','同目标重复命中由策略控制，等距目标有中性选择。','ProjectileHitQueryService 使用 UnitFinalGrid、扫掠形状和正式 TargetFilter；ProjectileHitMemory 保存跨 Tick 命中事实，命中结果进入 Combat 或所属效果端口。','距离为主键；等距按动作/参与者纯哈希；记忆不能依赖候选枚举顺序；结构技能效果由中央准入兜底拒绝。','ProjectileHitQueryService ProjectileHitMemory ProjectileHitRules','50,54')
feature('abilitysignal','技能','技能信号与会话状态','Focus、Commit、Cancel 等信号只通过技能门面进入单次施法状态。','AbilityHandler 接受 AbilitySignal，AbilityRuntime 常驻，AbilitySession 只承载本次施法；Session 结束回传执行器，外部只读 AbilityCastView。','HandleSignal 返回是否接受，不让 Planner 私自推进 Session；同 Tick Focus 和 Commit 需正式 CommandSeq 顺序。','AbilityHandler AbilitySignal AbilitySession AbilityCastView','16,17')
feature('castmodel','技能','施法模型与阶段推进','普通提交、蓄力、引导、持续信号和切换类技能由可配置状态机组合。','CastModelDef 决定 CastStageKey、阶段进入/退出及 Timeout，StageDef 独立返回完成或失败；每阶段明确时长，0 Tick 阶段有限推进。','不重建已删除 CastFlowDef、StageDriver；切换不必触发主动施法事件；蓄力 timeout 的自动释放或取消及退款由模型明确。','CastModelDef HoldReleaseCastModelDef ToggleCastModelDef','29,31')
feature('stage','技能','阶段效果与确定性黑板','每阶段可用自己的等级数值、目标和生命周期组合效果。','StageDef 用 AbilityStageContext、AbilityPorts 和受限 AbilityBlackboard；技能范围约束按需启用，效果写入所属系统，不增加 EffectPlan/EffectStep。','Handle 由创建 Effect 自己清理；Stage 成长不放在 AbilityDef 全局重复字段；结构过滤配置之外仍保留中央准入。','StageDef AbilityBlackboard AbilityStageContext AbilityPorts','30,54')
feature('abilitycost','技能','技能目录消耗冷却与升级','槽位可含多个主动定义，费用、冷却和升级有唯一规则。','AbilityBook 登记槽位；AbilityDef 提供条件、CostPlan 与按等级默认冷却；AbilityRankUpEffectDef 管理升级瞬时效果。','资源不足、技能满级、无技能点拒绝；特殊模型冷却不回写统一默认字段；连续再施法 UI 只投影当前可用段。','AbilityBook AbilityDef CostPlan AbilityRankUpEffectDef','31,42')
feature('passive','技能','主动附带被动与固定被动','可配置被动监听正式事件，跨死亡状态和冷却能恢复。','主动技能附带单事件被动，固定 PassiveAbilityDef 可支持多事件；Runtime Blackboard 与 owning ability level 是权威状态。','强类型事件而非动态订阅；自身来源伤害不能递归引爆同一被动；固定被动生命清理与重新建立按 Handler 接缝。','PassiveAbilityDef AbilityPassiveRuntimeState ApplyBuffPassiveEffectDef','30,31,34')
feature('varus','具体内容','韦鲁斯测试套件与数值','用通用作者扩展点组成 Q 蓄力、W 开关印记、E 范围和 R 投射物。','Q 满蓄力 1.5 秒，最多保持 4 秒；伤害/距离按 ChargeRatio 线性插值，额外 AD 从 80% 到 120%，范围 925 到 1625；穿透每额外目标衰减 15%，下限 33%。W 消耗开关后开始 40 秒冷却。','怪物印记每层上限 120，不泛化到所有非英雄；英雄引爆每层退基础技能冷却 13%；复仇强化期杀非英雄只排队一个普通 Buff，不能覆盖强化值。','ChargeStageDef ChargeProjectileStageDef AbilityHitStackDetonationBuffEffect KillStatGrowthBuffEffect','29,30,31,32,34')
feature('buff','增益','Buff 施加覆写与运行身份','同配置 Buff 保持唯一运行实例，通过标准施加与移除流程更新。','BuffHandler 保存按 ConfigId 稳定的 BuffRuntime，重复 Apply 覆写、更新 stack/duration；BuffSource 与黑板只承载正式数据。','不新增平行 Buff 身份；Added、Reapplied、StackChanged、Removed 顺序明确；结构外源 Buff 先拒绝再创建。','BuffHandler BuffRuntime BuffDefinition BuffBlackboard','54')
feature('buffeffects','增益','Buff 反应与句柄生命周期','效果可响应强类型事件并准确创建、更新和释放 Modifier。','静态 BuffEffect/ReactionConfig + Runtime Blackboard；创建者持有 StatModifierHandle/CombatModifierHandle，BuffHandler 不扫描黑板兜底释放。','RemovedComplete 在 store 移除后运行；死亡回调不自行清空 Buff；普通死亡保留永久 Runtime，复活重建本生命 Handle。','BuffEffect BuffReaction BuffLifecycleReason','9,10,32')
feature('buffcap','增益','Buff 上限优先级与驱逐','超过软上限时按确定优先级考虑驱逐，永久 Buff 保留。','MaxBuffs 为 byte 默认 255；Priority 0 最高。仅新 ConfigId 首次施加检查：最低优先级非永久为候选，同级按 ConfigId 稳定顺序末项；新 Priority<=候选才驱逐。','否则允许超过软上限；驱逐使用 ManualRemove 正常移除流程；重施已有 Buff 不重复驱逐。','BuffHandler BuffDefinition','25')
feature('control','控制','控制实例与模块参数','配置型控制通过静态模块表和有界参数块执行。','CrowdControlHandler 唯一运行入口；Definition 经 Bake 形成 module op 与 ParamLayout；实例按 Key 暴露、按 Offset 存储，不创建每实例模块对象。','不新增 Kind、软硬控枚举或 Combat 控制管线；模块重入使用明确延迟规则；缺键和容量溢出可见失败。','CrowdControlHandler CrowdControlDefinition CrowdControlInstance CrowdControlModule','36')
feature('controlpolicy','控制','控制免疫净化与汇总裁决','多控制实例组合为可查询限制和单一强制行为。','TagMask、Intensity、ImmunitySpec、UnitActionBlockMask 各司其职；Restrictions 并合，ForcedBehavior 按正式胜者规则，ForcedMove 由控制系统唯一仲裁。','净化由效果拥有者选择规则；免疫不等于所有法术盾；结构外源控制在访问可选 Handler 前拒绝，自身合法控制保留。','CrowdControlStateView CrowdControlImmunitySpec ForcedBehavior','36,54')
feature('equipment','装备商店','六格装备配方与唯一标签','六格装备支持堆叠、合成、交换、变形和排他标签。','EquipmentHandler 保存 EquipmentInstance 和 EffectRuntime；装备本身不用第二个 UID，内部延迟变更通过正式 slot/instance 定位。Recipe 与唯一标签在配置校验。','满栏合成先有确定购买计划；成装重复和跨装备排他显式；固定属性使用本装备持有的句柄。','EquipmentHandler EquipmentDefinition EquipmentInstance UniqueEquipmentTagTable','39')
feature('equipmenteffects','装备商店','装备主动被动与可重复命中','多个模块统一验证后执行，跨死亡效果状态有明确边界。','EquipmentEffectDef 内嵌多态 Module，Runtime 持有 EffectUid/Module state；主动一次验证全部模块，再通过仲裁瞬发；On-Hit 重复保留源动作。','每装备最多一个主动 Effect；装备使用使撤销失效；EquipmentTargetPolicy 目前只存在概念提及，待用户确认是否采用及值域。','EquipmentEffectRuntime EquipmentEffectModule OnHitRepeatModule','39')
feature('purchase','装备商店','购买合成与 Command 二次校验','商店能在满栏、已有组件和余额变化下确定购买结果。','EquipmentShopRuntime 从装备数据库计算 EquipmentPurchasePlan；UI RequestCheck 是预检查，ProcessCommand 再以当前确定性状态验证，组件选择和剩余价格稳定。','不能从 UI 预检查直接扣款；懒创建 Trader 不形成第二金币权威；部分堆叠、重复标签和不足余额明确拒绝。','EquipmentShopRuntime EquipmentPurchasePlan EquipmentShopRequests','40')
feature('undo','装备商店','卖出撤销与交易失效','买卖与撤销以可回滚交易链表达。','OperationLog 与 UndoableOperationStack 维护 EffectiveShopGoldDelta；出售和撤销出售属于商店增量，不产生 GoldIncome 记录。','离开范围、参与战斗、使用装备等永久失效规则必须准确；金币确认不扫描后续 Purchase/Undo，也不创建金币专用脏 Tick。','EquipmentShopRuntime UndoableOperationStack EquipmentShopSnapshot','6,7')
feature('gold','装备商店','金币批次确认与可用余额','所有收入有唯一批次、摘要和确认累计，UI 查询一个派生余额。','GoldIncomeRuntime 拥有 builder、未确认批次、digest、confirmed earned total/progress。CurrentAvailableGold=GetConfirmedEarnedGoldTotal(player)+EffectiveShopGoldDelta，只读派生。','Account 不保存第二局内金币累计；派生余额不进 Snapshot；T 收入确认不主动回滚或补造本地已拒绝 Command；金币生产者待确认项单列。','GoldIncomeRuntime GoldIncomeRecordBatch GoldIncomeBatchDigest','5,6,7,41')
feature('space','空间物理','空间实体注册与写入','单位和投射物共用明确的逻辑空间实体。','PhysicsEntity2D 拥有位置、朝向、形状和稳定查询信息；PhysicsWorld 注册/反注册，Movement 与投射物通过正式写入接口修改逻辑。','不新增 PhysicsEntityHandle；Physics 不执行 Combat；PrevPosition 每 Tick 冻结，传送和恢复有明确语义；Unity Transform 为派生表现。','PhysicsEntity2D PhysicsTransform2D PhysicsEntityQueryInfo','22')
feature('shape','空间物理','定点形状与范围查询','点、圆、线段、矩形与扫掠查询得到稳定结果。','PhysicsShape2D 及 geometry 定点窄相位；RangeQueryService 使用 TeamQueryRule、分类和完整目标过滤，再按指定正式键排序。','不是 Unity 物理权威；边缘接触、零半径、退化线段、旋转矩形和候选去重都有明确结果。','PhysicsShape2D PhysicsGeometry2D RangeQueryService ProjectileHitQueryService','22')
feature('grid','空间物理','移动前后网格与碰撞事实','避让与命中各自使用正确时间点的网格。','RvoGrid 使用移动前位置，UnitFinalGrid 在全部移动提交后构建；UnitCollisionEventBuffer 使用稳定 PairKey 发布轻量接触事实。','网格不提前按业务存活状态删候选；碰撞缓存跨 Tick 部分按快照合同；恢复后 Rebuild 派生网格。','PhysicsWorld PhysicsSpatialGrid2D UnitCollisionEventBuffer','4')
feature('wall','空间物理','墙体异常挤出与表现同步','异常墙内位置可由明确修正入口恢复，Transform 不反写逻辑。','WallPenetrationResolver 生成正式修正请求；PhysicsEntity2D.LateUpdate 仅同步已提交逻辑姿态，Scene Gizmo 读取相同只读数据。','传送跳过路径或改变 PrevPosition 的规则明确；重演不逐 Tick 操作表现 Transform；单位侧拥有最终空间提交。','WallPenetrationResolver PhysicsEntity2D','22')
feature('map','寻路移动','旋转网格地图与半径通行','不同半径单位可在二维旋转网格正确判断静态通行。','PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。','旋转、边界、不可走起终点和过大半径可见处理；NavMask 与内容配置版本有一致来源。','PathGridMap2D DeterministicMapConfig DynamicNavigationFrame','22')
feature('route','寻路移动','路线选择与跟随状态','移动目的选择直达、A* 或队伍流场并维护路径进度。','UnitLocomotionAgent/RouteResolver 按 MovePurpose、目标变化和路径偏离决策；PathFollower2D 输出 LocomotionResult，不直接写空间。','控制打断不重写 Order；恢复保留正式路线状态或明确重建可派生结果；追踪目标失效按正式边界返回。','UnitLocomotionAgent RouteResolver PathFollower2D RouteRuntime','47')
feature('astar','寻路移动','A 星路径搜索与简化','点到点搜索有稳定结果和不可达反馈。','AStarPathService 采用显式 OpenSet 排序、固定邻居访问与启发式成本；候选终点和路径简化仍受半径通行验证。','相同代价稳定平局；不可达不伪造直达路线；禁止随机/集合枚举顺序影响路径。','AStarPathService PathResult','22')
feature('flowfield','寻路移动','队伍流场与兵线引导','小兵共享静态目标流场，避免每个单位独立重复 A*。','TeamFlowFieldService 从兵线目标成本场构建队伍合并流场，贴墙候选遵守成本递减再评分。','流场不可走、方向退化与队伍配置缺失有明确回退；流场是配置派生，不引入新玩法时钟。','TeamFlowFieldService FlowField','22')
feature('rvo','寻路移动','确定性局部避让','多单位移动先收集同时间点偏好速度，再稳定求可执行速度。','DeterministicRVOSystem 读取移动前 RvoGrid，以固定候选速度和稳定约束求解；全部结果计算后再应用。','技术遍历不形成先移动优势；零速度、重叠、狭窄通道和速度上限有可判定边界。','DeterministicRVOSystem RvoResult','22')
feature('movement','寻路移动','普通移动冲刺与强制位移','普通路线、Dash、控制位移与传送均由统一移动入口提交空间。','MovementHandler 依正式优先级执行 Route、Dash 和 ResolvedForcedMove；ForcedMove 胜者仅由控制系统选出。','死亡清理移动任务；仲裁决定占用而不让技能控制直接写 Transform；墙体约束与异常挤出分开。','MovementHandler ForcedMoveExecutor DashRuntime','36,47')
feature('ai','非英雄','AI 注册调度与多态快照','非英雄 AI 在 UnitWorld 中唯一注册并参与统一行为链。','UnitAIController 子类提供决策与专有快照；按稳定 Uid 调度，但主动生效晚于出生 Tick。','不增加通用模拟按键层；AI Runtime 与管理者状态区分；死亡注销不能在恢复时静默漏建。','UnitAIController UnitWorld NonHeroSnapshot','8,18')
feature('minion','非英雄','小兵波次与兵线 AI','稳定生成票据组成波次，小兵推进、协防、追击与回线。','MinionSystem 固定波次序列和出生配置；MinionAIController 复用 UnitOrder/Planner/Attack；兵线参数与单位参数两类配置。','同 Tick 生效门、目标失效、追击距离与英雄协防条件明确；初始 Buff 和 Participant 来源由票据固定。','MinionSystem MinionAIController LaneAuthoring','37,50')
feature('jungle','非英雄','野怪营地刷新与共享仇恨','营地稳定生成成员并在主怪死亡后按规则刷新。','JungleCamp 拥有营地、respawn 和 member slot；MonsterAIController 管理战斗、共享目标、追击与回营，复用普通行为链。','主怪/小怪死亡、营地清空、目标远离和不可达分别处理；三狼回营/寻路旧计划未完成，不能标成已验收。','JungleCamp MonsterAIController MurkWolfContentIds','50')
feature('tower','非英雄','防御塔目标优先级与攻击红线','塔按明确优先级固定目标，攻击周期和红线可恢复。','TowerAIController 过滤范围和合法目标；TowerAttackHandler 管理周期与 Commit，TowerTargetLinePresenter 读取当前锁定状态。','塔不追击；在途炮弹不因重新索敌改目标；英雄正在攻击己方英雄的判定来源固定；结构效果准入在中央入口。','TowerAIController TowerAttackHandler TowerTargetLinePresenter','37,54')
feature('presentation','表现界面','单位视图绑定与语义挂点','逻辑实体能与可重建视图绑定，VFX 和音效使用稳定语义挂点。','UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。','视图不能反写 Gameplay；异步加载不能复用旧生命；逻辑空间所有者不随模型层级变化。','UnitPresentationHost UnitPresentationRegistry PresentationSocketSet','14,48')
feature('animation','表现界面','攻击技能动画与插值采样','动画按逻辑状态展示且采样频率可独立配置。','UnitAnimationDriver 读取 Attack 锁定时间与 AbilityCastView；客户端默认 20 Hz 插值，Bootstrap 发布按 UnitWorld 拥有的连续逻辑时间投影。','不得跨未 Commit 的 Impact 或 Ready；loop 相位由逻辑 epoch 和实时倍率重建；未知 TickRate 不硬回退 30 Hz；独立采样不新增 Gameplay Tick。','UnitAnimationDriver AttackAnimationPlan UnitAnimationSamplingConfig','42,52')
feature('vfx','表现界面','特效音效与回滚账本','事件身份稳定，回滚不重复播放已完成一次性音效。','VfxManager、AudioManager 分别管理定义、池和回滚账本；PresentationEventId=SourceLogicTick+SourceKind+SourceRuntimeUid+EventSequence+EventKey，支持 OneShotNoReplay、DurationCorrectable、LoopState。','Gameplay 不直接 Play 音效；每 manager 独立账本；事件序号归源运行时所有，表现不新增第二序号。','VfxManager AudioManager PresentationEventId','14')
feature('uipages','表现界面','页面层级与 Lua 实例生命周期','Main、Match、Select、Load、Result 与 HUD/Shop 覆盖页按流程显示。','UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。','不由 UI 决定预测或回滚；页面关闭移除观察者；主机单元和页面实例不共用意外 mutable 状态。','UIManager UIPanel LuaManager LuaHost UIList','15,26')
feature('hud','表现界面','HUD 数值技能与小地图','局内界面只读展示权威投影，通过类型化请求互动。','HUD 查询 Unit/Handler、WatchHook 和 IEquipmentShopView；UIDisplayConvert 在显示边界把 fp 转换成显示值，技能升级与装备交互提交请求。','UI 不重复伤害或金币公式；选择英雄列表来自 HeroDisplayTable；连续再施法投影、技能可用与冷却按当前只读 Runtime。','GameFlowLuaBridge UIDisplayConvert HeroDisplayTable HudSnapshotDto','28,42')
feature('shopui','表现界面','商店商品详情与余额刷新','界面显示动态合成价格、出售金额、撤销可用性和确认余额。','Shop.lua 直接调用 IEquipmentShopView 与 Request 接口；CurrentAvailableGold 来自 GoldIncomeRuntime 确认累计加商店增量。','预测收入不提前可用；普通回滚和 AuthorityRecovery 后修订通知重刷；界面不暴露 ProcessCommand 或写交易状态。','EquipmentShopViews EquipmentShopRequests GoldIncomeRuntime','5,7,40')
feature('inputevents','玩家输入','设备事件缓冲与 UI 门禁','本地按键和鼠标进入有序事件缓冲，不直接修改 Gameplay。','PlayerInputController 显式订阅 InputAction，回调记录本地事件；GameplayInputGate 处理 ActionMap、UI 指针阻断和受控单位变化。','回放不再读设备；同帧顺序、缓冲上限、禁用时清理和松键行为明确；UI 使用 Unity Input System UI 整合。','PlayerInputController GameplayInputGate LocalInputEvent','15')
feature('inputmodes','玩家输入','技能输入组合与提交去重','按下、松开和左键提交组成已选择的技能物理输入模式。','从 CastModelDef 离线派生 PressCommit、LocalAimPrimaryCommit、PressFocusReleaseOrPrimaryCommit，运行本地 FocusRequested/CommitRequested/GameplayFocusing 状态。','激活 hold-release 后松键和左键走同一 Commit，首次成功抑制重复；右键不 Cancel 但可移动/普攻；配置不复制 Gameplay 费用/范围/时长。','PlayerAbilityInputProfile PlayerInputController','16,17')
feature('aim','玩家输入','世界鼠标 Aim 与类型化 Request','输入层输出规范 AimSnapshot，命令层锁定目标 Tick。','鼠标世界解析选择地面点或 Unit，Direction 规范化；Move/Attack/Ability typed Request 返回回执，AimSnapshot 使用唯一正式字段。','输入层不 Clamp 技能距离；本地目标仅最低检查，最终执行重查；Focus 与 Commit 同 TargetTick 保持序列顺序。','AimSnapshot GameplayCommandRequests PlayerInputController','17')
feature('indicator','玩家输入','技能指示器与本地生命周期','本地瞄准和 Gameplay 蓄力时显示当前阶段的范围投影。','SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。','受控单位变化、禁用、死亡、UI 门禁和 Session 结束关闭；游戏回滚后跟随 Runtime，不反写命令或技能距离。','SkillIndicatorDriver AbilityIndicatorController PlayerInputController','29')

feature('launcher','启动器','游戏下载校验与完整安装','启动器能安全安装完整游戏并启动正式客户端。','清单决定版本、文件与内容分片；下载先校验签名和 SHA-256，再在独立暂存目录组装，最终安装提交只覆盖清单所属内容。','断网、取消、磁盘不足、损坏分片与中断安装可见失败；不覆盖用户保存数据和无关目录。','LauncherForm GameInstaller InstallService PackageManifest')
feature('incremental','启动器','增量更新与内容寻址分片','已有版本复用未改变内容，更新得到与完整安装等价结果。','schema-v3 以 content/<sha256> 标识不超过 95,000,000 字节分片；旧完整内容经校验可复用，新文件按清单组装。','哈希不匹配不能复用；缺旧分片时走完整获取；目标路径规范化和目录穿越检查必需。','PackageManifest ChunkManifest CdnPackageBuilder')
feature('upload','启动器','安全分片上传与可选发布包','构建正式 Player 后可单独选择生成签名 CDN 发布内容。','正式客户端固定 Builds/Demo/Game/AAALOL.exe，CDN 打包默认不启用；开启后成功 Player 才生成 schema-v3 清单和内容分片。上传按哈希与清单校验、受限重试。','不修改测试 Builds/UosClient；上传失败不能报告发布成功；私钥不进库；Release ZIP 仅在用户接受后进入 Git。','CdnPackageBuilder ReleaseClientBuildWindow')

# 旧文档小节按功能语义归属；同一设计可以变成多个需求，决策不会独立建案。
MAP={
1:{1:'pipeline',2:'flow',3:'lobby',4:'lobby',5:'gold',6:'uid',7:'tick',8:'tick',9:'command',10:'collector',11:'command',12:'authority',13:'pipeline',14:'match',15:'rollback',16:'random',17:'global',18:'pipeline',19:'pipeline'},
2:{1:'snapshot',2:'snapshot',3:'rollback',4:'match',5:'lifecycle',6:'projectileworld',7:'combatqueue',8:'undo',9:'gold',10:'grid',11:'random',12:'snapshot',13:'rollback',14:'authority',15:'match',16:'snapshot'},
4:{1:'unit',2:'order',3:'arbitration',4:'unit',5:'stats',6:'events',7:'lifecycle',8:'global',9:'lifecycle',10:'unit'},
5:{1:'combatqueue',2:'combatqueue',3:'damage',4:'attackoutput',5:'heal',6:'heal',7:'damage',8:'heal',9:'modifiers',10:'death',11:'reward',12:'combatqueue',13:'combatqueue',14:'combatqueue'},
8:{1:'projectileworld',2:'projectiledef',3:'projectileworld',4:'projectiledef',5:'projectileworld',6:'projectiledef',7:'projectilehit',8:'projectileworld',9:'projectileworld',10:'projectiledef',11:'projectileworld',12:'projectileworld',13:'projectilehit'},
9:{1:'abilitysignal',2:'abilitysignal',3:'castmodel',4:'stage',5:'abilitycost',6:'passive',7:'stage',8:'abilitysignal'},
10:{1:'attack',2:'attack',3:'attack',4:'attack',5:'attackoutput',6:'attackoutput',7:'attack'},
11:{1:'buff',2:'buffeffects',3:'buff',4:'buff',5:'buffeffects',6:'buffeffects',7:'buffeffects',8:'buffeffects',9:'buffeffects',10:'control',11:'buff',12:'buffeffects',13:'buffeffects',14:'buff',15:'buff',16:'buff'},
12:{0:'control',1:'control',2:'control',3:'control',4:'control',5:'controlpolicy',6:'controlpolicy',7:'controlpolicy',8:'controlpolicy',9:'control',10:'control',11:'control'},
13:{1:'equipment',2:'equipment',3:'equipmenteffects',4:'equipmenteffects',5:'purchase',6:'gold',7:'equipment',8:'equipment',9:'equipment'},
14:{1:'space',2:'space',3:'space',4:'shape',5:'movement',6:'projectilehit',7:'grid',8:'shape',9:'shape',10:'grid',11:'wall',12:'wall',13:'wall',14:'grid',15:'space',16:'space'},
15:{1:'movement',2:'movement',3:'space',4:'map',5:'route',6:'route',7:'astar',8:'flowfield',9:'route',10:'rvo',11:'movement',12:'wall',13:'movement',14:'route',15:'movement',16:'movement',17:'movement',18:'movement'},
16:{1:'ai',2:'ai',3:'ai',4:'minion',5:'minion',6:'jungle',7:'jungle',8:'tower',9:'tower',10:'ai'},
17:{1:'presentation',2:'presentation',3:'animation',4:'vfx',5:'vfx',6:'presentation',7:'content',8:'vfx',9:'presentation',10:'presentation',11:'presentation'},
18:{1:'uipages',2:'uipages',3:'uipages',4:'uipages',5:'hud',6:'uipages',7:'uipages',8:'hud',9:'uipages',10:'hud',11:'shopui',12:'shopui',13:'shopui',14:'uipages',15:'uipages'},
19:{1:'inputevents',2:'inputevents',3:'inputevents',4:'inputmodes',5:'inputevents',6:'inputevents',7:'aim',8:'aim',9:'inputmodes',10:'inputmodes',11:'inputmodes',12:'aim',13:'aim',14:'ai',15:'indicator',16:'inputevents',17:'inputevents',18:'inputevents',19:'inputevents',20:'inputevents',21:'inputmodes',22:'inputevents'}}

# English 修订原文不照抄进入中文正文；关键语义见对应功能技术方案和版本演进。
AMENDMENTS={3:['arbitration','snapshot','lifecycle'],6:['pipeline','combatqueue','damage','death'],7:['uid','crit','projectilehit','snapshot']}
old_catalog=json.loads(read('Docs/Requirements/catalog.json'))
cnnums={'零':0,'一':1,'二':2,'三':3,'四':4,'五':5,'六':6,'七':7,'八':8,'九':9,'十':10,'十一':11,'十二':12,'十三':13,'十四':14,'十五':15,'十六':16,'十七':17,'十八':18,'十九':19,'二十':20,'二十一':21,'二十二':22}
def parent_number(title,previous=1):
    m=re.match(r'(\d+)',title)
    if m:return int(m[1])
    m=re.match(r'(?:专题)?([零一二三四五六七八九十]+)[、：]',title)
    return cnnums.get(m[1],previous) if m else previous
def owned(source,major,minor,title):
    key=MAP[source].get(major,next(iter(MAP[source].values())))
    if source==1 and major==9 and minor==4:key='targettick'
    if source==4 and major==5:
        if minor in [6]:key='experience'
        elif minor in [7,8]:key='resources'
        elif minor in [5]:key='statdirty'
    if source==5 and major==7 and minor==14:key='reward'
    if source==11 and (title.startswith('13A') or major==13 and '驱逐' in title):key='buffcap'
    if source==13 and major==5 and minor>=14:key='undo'
    return key

for e in old_catalog['entries'][:19]:
    n=int(e['id'][-3:]);text=read(e['path']).split('<!-- BEGIN MIGRATED CONTRACT -->')[-1].split('<!-- END MIGRATED CONTRACT -->')[0]
    source=dict(path=e['sources'][0]['original'],sha256=e['sources'][0]['sha256'],recovery=f'{STAMP}/Docs.zip')
    if n in AMENDMENTS:
        for key in AMENDMENTS[n]:features[key]['sources'].append(source)
        if n in (6,7):
            amendment_map={6:{2:'pipeline',3:'combatqueue',4:'damage',5:'damage',6:'death',7:'death',8:'snapshot',9:'combatqueue'},7:{2:'uid',3:'uid',4:'crit',5:'projectilehit',6:'snapshot',7:'crit'}}[n]
            for match in re.finditer(r'(?ms)^## (\d+)\. (.+?)\n(.*?)(?=^## |\Z)',text):
                number=int(match[1])
                if number in amendment_map:features[amendment_map[number]]['clauses'].append((match[2],match[3].strip()))
        source_coverage.append(dict(source=source,disposition='修订内容合入功能现行方案与演进',targets=AMENDMENTS[n]));continue
    # H1 是旧系统章节，H2 是可归属的功能小节；技术附录保留精确参数和接口。
    chunks=re.split(r'(?m)^(#{1,2}) (.+)\n',text);major=1;targets=set();detail_count=0
    for i in range(1,len(chunks),3):
        level,title,content=chunks[i],chunks[i+1],chunks[i+2];content=content.strip()
        if level=='#':major=parent_number(title,major)
        m=re.match(r'(\d+)[A-Z]?\.(\d+)',title);minor=int(m[2]) if m else 0
        if m:major=int(m[1])
        if not content or title=='目录' or '设计案' in title or title.startswith('参考设计'):continue
        # 删除旧版本比较、推荐落地顺序和复述结论，不复制固定设计文档套壳。
        if any(x in title for x in ['本版调整','核心调整','本版删除','推荐落地','删除项审查','最终结构','最终结论','核心结论','版本修订摘要','编码准入','总体结论','ADR-']):
            source_coverage.append(dict(source=source,section=title,disposition='历史结构与方法说明退役；现行规则替代',targets=[MAP[n].get(major,next(iter(MAP[n].values()))) ]));continue
        key=owned(n,major,minor,title);targets.add(key);detail_count+=1
        # 旧冲突版本不是现行条款；修订后的中文方案覆盖其所属功能。
        obsolete=(n==4 and major==3) or (n==5 and (major==1 or major==2 or major==10 or major==7 and minor==7))
        if obsolete:
            source_coverage.append(dict(source=source,section=title,disposition='已有接受修订覆盖；采用现行功能方案',targets=[key]));continue
        # 清掉旧交叉设计版本权威声明，具体接口/公式/边界仍保留为细化附录。
        content=re.sub(r'(?m)^>.*(?:对齐|适配|参考|版本|日期|设计案|v\d).*\n?','',content)
        content=re.sub(r'(?m)^#{1,5} (.+)',lambda m:'#### '+re.sub(r'^\d+(?:\.\d+)*\s*','',m[1]),content)
        # 旧正文明确的等距 UID 主键已被动作身份修订覆盖，避免双重权威。
        if n==14 and major==8 and minor==6 or n==8 and major==7 and ('Uid' in content and '等距' in content):
            content=features['projectilehit']['tech']+'\n\n'+features['projectilehit']['boundary']
        if n==17 and major==3 and '30Hz' in content and '采样' in content:content=features['animation']['tech']+'\n\n'+features['animation']['boundary']
        features[key]['clauses'].append((title,content));features[key]['sources'].append(dict(**source,section=title))
        source_coverage.append(dict(source=source,section=title,disposition='拆分归属到功能附录的精确约束',targets=[key]))
    source_coverage.append(dict(source=source,disposition='原设计按功能拆分，封面目录和重复结构不再作为案',targets=sorted(targets),sections=detail_count))

# 全部决策成为功能演进或规则修订。中文摘要说明当前有效边界，旧编号仅在元数据。
DECISIONS={
1:'明确所有 Tick 表示下一待执行帧，普通回滚不穿越连续权威确认边界。',2:'权威帧必须校验完整规范命令字节及共享校验，包含金币批次摘要。',3:'权威恢复只重传缺失帧，不支持新进程/局中加入；本地锚点丢失终止。',4:'一 Tick 一快照，恢复拆为 Restore、Resolve、Rebuild。',5:'金币总控唯一拥有 builder、未确认批次、摘要和确认累计。',6:'确认金币不主动重演后续商店命令或补建本地已拒绝命令。',7:'可用金币为确认累计加有效商店增量，只读派生且不保存快照。',8:'生成 Tick 可被动参与，主动工作晚于出生 Tick。',9:'正式死亡由 UnitWorld 同步执行，来源系统仅清理自己的句柄。',10:'死亡和击杀反应即时分发，产生的新普通战斗请求延至下一 Tick。',11:'战斗 Tick 末仅保存贡献跟踪和跨 Tick 延迟请求。',12:'投射物保存 pending/active 状态，生成序列由 ProjectileWorld 拥有。',13:'全模拟端消费正式死亡统计，不只服务器。',14:'稳定表现事件身份由逻辑来源持有，Gameplay 不直接播放音效。',15:'设备回调只入本地缓冲，UI 输入独立，回滚不重读设备。',16:'输入模式从施法模型离线派生，不重复 Gameplay 配置。',17:'松键和左键合并为一次 Commit，右键不取消蓄力。',18:'AI 直接使用已有意图与技能语言，不模拟物理输入。',19:'PrefabKind 为代码固定枚举，Editor 仅管理 ID/条目。',20:'框架例子不是生产内容任务，明确请求的内容通过已有作者扩展点实现。',21:'旧路径和命名可修正；Unity 资产更名保持 GUID。固定设计索引权威已由本期文档规则替代。',22:'作者 float 仅在 Bake/初始化边界转正式 fp，Tick 内不回转作为权威。',23:'实现生产最小切片并附比例合适的行为测试，不优先扩大测试框架。',24:'2026-07-19 已接受工作树为新基线，不复活 616 个故意删除的旧文件。',25:'软 Buff 上限按优先级考虑驱逐，永久 Buff 不驱逐。',26:'Bootstrap、Lobby、GameScene 分离，跨场景数据与 NGO 根有唯一 owner。',27:'UOS 配置唯一来源，命令行显式覆盖，密钥不进入文档。',28:'英雄列表按目录驱动，正 HeroConfigId 可重复选择，由最终内容闭包验证。',29:'韦鲁斯测试套件使用普通点/方向提交、hold-release 与纯开关组合。',30:'Q 蓄力和 W 印记由通用 Stage、Buff 与投射物覆盖实现。',31:'按等级冷却、蓄力减速/超时退款和复仇被动；旧启动授权携带方案已由后续修订替代。',32:'强化复仇结束后最多补一个普通 Buff；分段 checksum 和 StatSeq 排序便于定位。',33:'曾采用 UTC 载荷启动；当前改为两阶段授权和单调调度。Tick 末 FinalizeTick 仍有效。',34:'助攻事件和复仇反应接入正式贡献链。',35:'保留贡献事件和助攻窗口；末次伤害击杀归属已被最高有效伤害修订替代。',36:'控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。',37:'小兵初始 Buff、塔攻击与兵线/单位配置拆分。',38:'正式资源路径唯一，重复目录和旧引用退役。',39:'正式装备目录和可重复 On-Hit 的来源防重入。',40:'商店 Trader 懒创建，小兵奖励距离按正式数值边界转换。',41:'击杀金币整数分配和测试金币；生产者边界冲突留在待确认清单。',42:'连续再施法界面只读投影，施法朝向保持 Gameplay 授权。',43:'异步诊断队列有界并可编译开关，不改变确定性输出。',44:'加载确认和开局 Commit 两阶段，取代载荷内启动授权。',45:'开局采用单调时钟，作者毫秒独立于 TickRate；后续目标 Tick 估计补充此规则。',46:'英雄出生槽位从大厅锁定选择绑定，不依赖场景对象顺序。',47:'结构化仲裁和固定 Main/Base Runtime；覆盖旧申请列表/执行器所有权。',48:'客户端视图可重建、句柄唯一释放。直接逻辑引用和完全服务器排除 Addressables 部分已被后续内容闭包修订取代。',49:'战斗封存波次和冻结批次起始状态；击杀按最高有效生命伤害，取代末次伤害者。',50:'动作键暴击和等距中性裁决；覆盖先前对 UID 耦合随机样本和目标平局的许可。',51:'Core+Map+去重 Hero 对局闭包在 Tick 前加载，逻辑 Addressables 允许在服务器；客户端表现仍排除。',52:'客户端动画默认 20 Hz 插值，按逻辑 epoch 重建相位且不会推进 Gameplay。',53:'整数 RTT 平滑与目标 Tick 自适应，仍保留静态下界和冷启动回退。',54:'建筑中央准入仅允许规定外源普通攻击，自身效果允许，合法拒绝是成功空操作。'}
decision_targets=collections.defaultdict(list)
for key,f in features.items():
    for n in f['decisions']:decision_targets[n].append(key)
governance={20:'工程宪法',21:'文档维护与演进',23:'Agent执行周期',24:'工程宪法',43:'验证与证据规则'}
for n in range(1,55):
    targets=decision_targets[n] or [governance.get(n,'工程宪法')]
    source_coverage.append(dict(source=dict(path='Docs/Architecture/DECISION_LOG.md',decision=f'D-{n:03}',sha256=sha('Docs/Archive/PreKarolina/Architecture/DECISION_LOG.md'),recovery=f'{STAMP}/Docs.zip'),disposition='合入功能历史或规则；不创建独立需求',targets=targets,summary=DECISIONS[n]))

# 从当前工程取证：列出类型定义、字段/签名、真实测试函数及 asmdef 依赖。存在不等于通过。
code=[]
for p in sorted(list((ROOT/'Assets/Scripts').rglob('*.cs'))+list((ROOT/'Tools/UosGameLauncher').rglob('*.cs'))):
    if 'obj' in p.parts or 'bin' in p.parts:continue
    try:t=p.read_text(encoding='utf-8-sig')
    except UnicodeDecodeError:t=p.read_text(encoding='gb18030')
    types=re.findall(r'\b(?:class|struct|enum|interface)\s+(\w+)',t)
    tests=re.findall(r'\[(?:Test|TestCase(?:\([^\n]*\))?|UnityTest)\][\s\S]{0,180}?\b(?:void|IEnumerator|Task)\s+(\w+)\s*\(',t)
    code.append(dict(path=p.relative_to(ROOT).as_posix(),text=t,types=types,tests=tests,sha256=sha(p.relative_to(ROOT))))
def evidence_for(f):
    aliases={'ClientContentService':'AddressablesClientContentService','ClientViewHost':'ClientUnitViewBinder','PresentationDependencyAudit':'LocalAddressablesConfigurationTests','LauncherForm':'MainForm','GameInstaller':'CdnInstallService','InstallService':'CdnInstallService','PackageManifest':'ClientReleaseManifest','ChunkManifest':'CdnChunkEntry','HealPipeline':'HealRequest','ShieldPipeline':'ShieldRequest','NaturalRegenPipeline':'CombatSystem','DeathRewardContext':'CombatRewardCalculator','GoldIncomeAllocation':'GoldAllocation','ProjectileHitMemory':'ProjectileRuntime','ProjectileHitQueryService':'ProjectileHitResolver','DamagePipeline':'CombatSystem','DamageFormula':'CombatDamageCalculator'}
    symbols=set(f['symbols'])|{aliases[s] for s in f['symbols'] if s in aliases}
    defs=[c for c in code if symbols.intersection(c['types']) and '/Tests/' not in c['path']]
    def score(c):return sum(12 if s.lower() in Path(c['path']).stem.lower() else 1 for s in symbols if re.search(r'\b'+re.escape(s)+r'\b',c['text']) or s.lower() in Path(c['path']).stem.lower())
    testfiles=sorted([c for c in code if ('/Tests/' in c['path'] or c['path'].endswith('LauncherSelfTest.cs')) and (c['tests'] or 'SelfTest' in c['path']) and score(c)>0],key=score,reverse=True)
    return defs,testfiles
for index,(key,f) in enumerate(features.items(),1):
    f['id']=f'REQ-FEAT-{index:03}';defs,tests=evidence_for(f)
    f['definitions']=defs;f['tests']=tests
    evidence=[dict(path=c['path'],sha256=c['sha256'],symbols=sorted(set(c['types']).intersection(f['symbols'])),kind='类型定义') for c in defs]+[dict(path=c['path'],sha256=c['sha256'],testFunctions=c['tests'],kind='测试源码（未在本期运行）') for c in tests]
    status='已接受'
    facts='\n'.join(f'- `{c["path"]}`：当前关联实现定义 '+ '、'.join(c['types'][:5])+'（以源码为实际命名）。' for c in defs[:10]) or '当前未找到这些正式命名的类型定义；不能据此断言能力不存在，可能存在命名或架构差异。'
    test_facts='\n'.join(f'- `{c["path"]}`：'+('、'.join(c['tests'][:7]) if c['tests'] else '含关联测试引用，具体行为需复核')+'。' for c in tests[:5]) or '未检索到直接覆盖此功能的测试源码，列入待确认清单。'
    # 不抄系统封面/目录/整章，精确约束按功能小节组织；新增目标、方案、边界和工程核查。
    detail='\n\n'.join('### '+re.sub(r'^\d+[A-Z]?(?:\.\d+)*\s*','',title)+'\n\n'+content for title,content in f['clauses'])
    body=f'''## 目标实现\n\n{f['goal']}\n\n## 技术方案\n\n{f['tech']}\n\n## 边界情况\n\n{f['boundary']}\n\n## 验收条件\n\n- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。\n- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。\n- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。\n\n## 工程实际核查\n\n本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。\n\n{facts}\n\n### 已有测试位置\n\n{test_facts}\n\n## 附录：精确接口、公式与配置\n\n附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。\n\n{detail if detail else '本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。'}'''
    unique=[];seen=set()
    for s in f['sources']:
        token=json.dumps(s,sort_keys=True)
        if token not in seen:seen.add(token);unique.append(s)
    document(f['id'],f['title'],'requirement',f['domain'],body,status,implementationState='存在源码；尚未逐条行为复核' if defs else '命名或落地状态待确认',sources=unique,evidence=evidence,history=[dict(date='2026-10-02',summary=DECISIONS[n],legacyDecision=f'D-{n:03}') for n in f['decisions']],related=[])

def requirement_ids(keys):return [features[k]['id'] for k in keys]
def map_plan(name,text):
    # 文件名仅用于初步定位，源码符号和计划内容补齐交叉功能关系。
    rules=[('gold',['gold','reward','undo']),('equipment',['equipment','equipmenteffects','purchase']),('buff',['buff','buffeffects','buffcap']),('crowd_control',['control','controlpolicy']),('cc_module',['control','controlpolicy']),('physics',['space','shape','grid']),('geometry',['shape']),('random',['random']),('canonical',['command','checksum']),('snapshot',['snapshot','rollback']),('checksum',['checksum']),('rollback',['rollback','authority']),('authority',['authority']),('command',['command','collector','targettick']),('projectile',['projectiledef','projectileworld','projectilehit']),('combat',['combatqueue','damage','death','reward']),('onhit',['attackoutput','equipmenteffects']),('on_hit',['attackoutput','equipmenteffects']),('attack',['attack','attackoutput']),('hit_reaction',['events']),('ability',['abilitysignal','castmodel','stage','abilitycost','passive']),('stage_def',['stage']),('input',['inputevents','inputmodes','aim','indicator']),('indicator',['indicator']),('pathfinding',['map','route','astar']),('astar',['astar']),('movement',['movement','route','rvo']),('nonhero',['ai','minion','jungle','tower']),('minion',['minion','tower']),('tower',['tower']),('jungle',['jungle']),('wolf',['jungle']),('map',['map','jungle']),('xp',['experience']),('levelup',['experience']),('stat',['stats','statdirty']),('presentation',['presentation','animation','vfx']),('animation',['animation']),('vfx',['vfx']),('camera',['inputevents','presentation']),('lua',['uipages','hud','shopui']),('ui',['uipages','hud']),('shop',['shopui','purchase','undo']),('hero_select',['lobby','hud']),('unit_uid',['uid']),('unitworld',['lifecycle']),('unit_world',['lifecycle']),('unit_kind',['unit']),('unit_lifecycle',['lifecycle']),('unit_dispose',['lifecycle']),('unit_prototype',['unit','content']),('capability',['unit','lifecycle']),('life_state',['lifecycle']),('team_registry',['unit']),('spawn',['lifecycle','uid']),('arbitration',['arbitration']),('tick_context',['tick','random']),('framesync',['pipeline','tick']),('frame_sync',['pipeline','tick']),('uos',['uos','lobby','launch']),('lobby',['lobby','launch']),('bootstrap',['launch','flow','content']),('match_flow',['match','flow']),('match_result',['match']),('addressable',['content','servercontent']),('content',['content']),('prefab',['content','presentation']),('resource',['content']),('launcher',['uos']),('cdn',['uos']),('artifact',['uos']),('release',['uos']),('guid',['content']),('binding',['presentation']),('karolina',['pipeline'])]
    keys=[]
    for token,match in rules:
        if re.search(r'(?:^|_)'+re.escape(token)+r'(?:_|$)',name.lower()):keys+=match
    if not keys:
        for key,f in features.items():
            if any(re.search(r'\b'+re.escape(s)+r'\b',text) for s in f['symbols']):keys.append(key)
    if 'launcher' in name.lower():keys=['launcher','incremental']
    if any(token in name.lower() for token in ['cdn','chunk','artifact','release_client']):keys=['upload','incremental']
    specific=[('non_hero','ai'),('unit_life','lifecycle'),('life_state','lifecycle'),('map_config','map'),('minimap','hud'),('critical','damage'),('action_arbitration','arbitration'),('jungle','jungle'),('wolf','jungle'),('skill_point','experience')]
    for token,key in specific:
        if token in name.lower():keys.insert(0,key);break
    return list(dict.fromkeys(keys)) or ['pipeline']
def plan_status(text):
    header=text[:1600]
    m=re.search(r'(?:\*\*)?Status(?:\*\*)?\s*[:：]\s*(.+)',header,re.I)
    raw=m[1].replace('*','').strip() if m else ''
    if re.search(r'Superseded|do not execute',raw,re.I):return '已淘汰',raw
    if re.search(r'Deferred|Paused',raw,re.I):return '暂缓',raw
    if re.search(r'Verification Pending|pending.*verif',raw,re.I):return '待验收',raw
    if re.search(r'Complete|accepted|implemented',raw,re.I):return '历史完成报告',raw
    if re.search(r'Draft|Proposal',raw,re.I):return '历史草案',raw
    if re.search(r'Active|In Progress',raw,re.I):return '历史执行中（待确认）',raw
    results=re.search(r'(?is)##\s*(?:\d+\.?\s*)?(?:Results|结果|验收结果|Completion)[^\n]*\n(.+)',text)
    if results and re.search(r'\b(?:completed|complete|accepted|implemented|passed)\b',results[1],re.I) and not re.search(r'Populated after|pending|not (?:complete|implemented)|\[ \]',results[1],re.I):return '历史完成报告','结果段明确报告完成（当前未重跑）'
    return '状态待确认',raw or '无明确状态字段'
groups=collections.defaultdict(list)
oldplans=sorted((ROOT/'Docs/Implementation/Plans').rglob('*.md'))+sorted((ROOT/'Docs/Plans').glob('*.md'))
for p in oldplans:
    if p.name=='INDEX.md':continue
    relative=p.relative_to(ROOT).as_posix();t=read(relative);keys=map_plan(p.stem,t);status,raw=plan_status(t)
    retired='candidate' in p.stem.lower() or '/Archive/' in relative or status=='已淘汰'
    item=dict(source=relative,sha256=sha(relative),recovery=f'{STAMP}/Docs.zip',status=status,reportedStatus=raw,targets=requirement_ids(keys),unresolved='原计划未提供可明确判定的最新完成状态' if status in ['状态待确认','历史执行中（待确认）'] else None)
    if retired:
        item['disposition']='旧候选机制/被替代方案退役，保留来源覆盖与原因；不是未实施的现行任务'
        item['newPlan']=None
    else:
        if 'karolina' in p.stem.lower():item['newPlan']='PLAN-KAR-001'
        else:
            primary=keys[0];groups[primary].append((item,t,keys));item['newPlan']='PLAN-FEAT-'+features[primary]['id'].split('-')[-1]
        item['disposition']='按功能整合为中文计划；旧完成与当前证据分别记录'
    plan_coverage.append(item)
for key,group in groups.items():
    f=features[key];keys=list(dict.fromkeys(k for _,_,ks in group for k in ks));reqs=requirement_ids(keys)
    state='待确认' if any(i['unresolved'] for i,_,_ in group) else ('暂缓' if any(i['status']=='暂缓' for i,_,_ in group) else ('待验收' if any(i['status']=='待验收' for i,_,_ in group) else '历史已实施'))
    source_rows='\n'.join(f'| {re.search(r"\d{4}",Path(i["source"]).stem)[0] if re.search(r"\d{4}",Path(i["source"]).stem) else "历史来源"} | {i["status"]} | {"当前需确认" if i["unresolved"] else "当期记录，不作为本期通过证据"} |' for i,_,_ in group)
    all_defs={c['path']:c for k in keys for c in features[k]['definitions']};all_tests={c['path']:c for k in keys for c in features[k]['tests']}
    defs='\n'.join(f'- `{c["path"]}`：'+ '、'.join(c['types'][:7])+'。' for c in list(all_defs.values())[:18]) or '当前命名落地待确认，不创建重复权威类型。'
    tests='\n'.join(f'| `{c["path"]}` | '+('、'.join(c['tests'][:5]) or '类内测试入口需复核')+' | EditMode（标为 PlayMode 的文件用 PlayMode） | 正常与边界行为符合参考需求；重复/恢复/插入顺序结果一致 |' for c in list(all_tests.values())[:10])
    if not tests:tests='| 待设计聚焦测试 | 功能正常路径与失败路径 | 由实际组件依赖决定 | 必须提供明确输入与预期结果，不能只编译 |'
    # 技术实施数据以当前类型定义和字段签名取证，历史段落不机械搬运。
    signatures=[]
    for c in list(all_defs.values())[:7]:
        members=re.findall(r'(?m)^\s*(?:public|internal)\s+(?:(?:static|readonly|sealed|override|virtual)\s+)*[^\n{};]+(?:;|\))\s*$',c['text'])[:14]
        if members:signatures.append('### '+Path(c['path']).stem+'\n\n```csharp\n'+'\n'.join(m.strip() for m in members)+'\n```')
    body=f'''## 参考需求\n\n'''+ '\n'.join(f'- [{features[k]["title"]}](../../需求/{features[k]["domain"]}/{features[k]["title"]}.md)：目标、技术方案、边界与附录。' for k in keys)+f'''\n\n## 目标与当前核查\n\n{f['goal']}\n\n本计划把同一功能的历次落地整合起来。历史完成报告不自动变成本期验收通过；以下状态无法确认的项请见工程清单。\n\n| 旧计划批次 | 当期状态 | 当前判断 |\n|---|---|---|\n{source_rows}\n\n## 实施技术细节\n\n{f['tech']}\n\n### 数据结构与已有实现\n\n{defs}\n\n{chr(10).join(signatures)}\n\n### 数据流与依赖\n\n输入先经过所属模块验证，再写入唯一运行状态；只读视图/事件给下游消费者。涉及跨 Tick 的状态通过所属快照 Capture，恢复顺序为 Restore → Resolve → Rebuild。外部系统调用正式端口，不创建第二套 UID、DTO、快照或金币/空间权威。\n\n### 边界与实施顺序\n\n{f['boundary']}\n\n- [x] 从全部旧计划和当前源码定位此功能的实施记录。\n- [x] 合并中文目标、具体类型与对应需求，保留原状态和来源指纹。\n- [ ] 逐条对照上述需求的边界与附录，确认尚未实现和缺验证内容。\n- [ ] 如继续实施，补具体文件改动与必要测试，不重复执行历史完成步骤。\n- [ ] 记录真实工具回执、独立审查和最终状态。\n\n## Agent 测试与验收流程\n\n| 测试源码或入口 | 函数 | 环境/场景 | 预期结果 |\n|---|---|---|---|\n{tests}\n\n1. 用 Unity MCP 核对当前工程身份、Editor 空闲状态与 Console；本计划整理不等于重新运行旧测试。\n2. 选表中与本次改动直接有关的测试类和程序集，准备明确初始状态、输入和预期结果；在计划进度补上实际参数。\n3. Gameplay 纯逻辑使用 EditMode；场景、组件生命周期、设备回调或表现用 PlayMode，并写明当前真实场景路径。\n4. 保存逐用例 Passed/Failed/Skipped 及回执，不能把发现树 TotalTests 当执行数。\n5. 修改 C# 后由 Unity 编译并检查 Console；高风险改动独立只读审查。失败应定位/修复并重新验收，不能填占位成功。\n6. 本轮用户要求禁止截图验收；采用行为断言、连接/协议回执及人工观察结果。\n\n## 进度与结果\n\n本文是对过去工作的中文整理。状态为“{state}”，不是重新启动所有历史工程任务。未确认信息集中在 [待确认清单](../../工程/待确认清单.md)。\n\n## 恢复与限制\n\n原始计划完整备份在本机 Karolina/recovery/{STAMP}/Docs.zip，路径、SHA-256 和当期状态在独立元数据与覆盖清单。不恢复用户此前故意删除的 Gameplay 文件。'''
    document('PLAN-FEAT-'+f['id'].split('-')[-1],f['title']+'实施与验收','plan',f['domain'],body,state,requirements=reqs,sources=[i for i,_,_ in group],evidence=[dict(path=c['path'],sha256=c['sha256'],kind='源码或测试（本期未重跑）') for c in list(all_defs.values())+list(all_tests.values())],history=[dict(date='2026-10-02',summary='按功能合并历史计划，区分当期报告与当前核查')])

# 其余规则/产品需求由手写规则清单生成。
exec((ROOT/'Tools/Karolina/maintenance/rules_and_product.py').read_text(encoding='utf-8'),globals())

for item in source_coverage:
    item['targets']=[features[k]['id'] if k in features else k for k in item['targets']]
dump('Docs/工程/迁移覆盖清单.json',dict(schemaVersion=2,recovery=f'{STAMP}/Docs.zip',requirements=source_coverage,plans=plan_coverage))
dump('Docs/catalog.json',dict(schemaVersion=2,entries=entries,activePlan='PLAN-KAR-002',authority='当前用户指令优先；已接受需求与规则为现行约束；旧 ID 仅来源别名'))

unknown=[]
for key,f in features.items():
    if not f['definitions'] or not f['tests']:unknown.append(f'- **{f["title"]}**：'+('未匹配到正式命名的类型定义；可能是命名/实现架构差异。' if not f['definitions'] else '找到实现，但未匹配到直接关联测试。')+'需要确认功能实际完成程度，不能仅按文件存在标完成。')
unknown_plans=[i for i in plan_coverage if i['unresolved'] and i['newPlan']]
unknown.extend(f'- **{next(f["title"] for f in features.values() if "PLAN-FEAT-"+f["id"].split("-")[-1]==i["newPlan"])}**：旧批次 {re.search(r"\d{4}",Path(i["source"]).stem)[0] if re.search(r"\d{4}",Path(i["source"]).stem) else "未知"}，{i["unresolved"]}。源码只说明有关能力存在，无法证明此历史范围已全部完成；原文件定位保存在覆盖清单元数据。' for i in unknown_plans)
write('Docs/工程/待确认清单.md','''# 待确认清单\n\n## 当前明确保留的未完成事项\n\n1. 三狼回营、导航遮罩与 leash：旧计划 0165 明确暂缓；没有把它改成完成。请确认接下来继续还是取消哪些子项。\n2. 客户端 Addressables 与 Linux 服务器表现隔离：旧计划 0138 保留配套实际构建/运行验收，不能用 Editor 测试代替。\n3. 金币生产者归属：旧 D-041 的整数奖励/测试金币说明与既有 Combat 贡献/复仇奖励描述仍有 owner 争议。需要确认具体生产入口和最终公式；金币累计唯一 owner 已明确为 GoldIncomeRuntime。\n4. EquipmentTargetPolicy：旧装备设计提及但工程没有独立值域/实现定义。请确认需要哪些目标类型与检查规则，或删除此概念。\n\n## 当前源码或测试不能确认的功能\n\n'''+ '\n'.join(unknown)+'\n\n## 如何给出反馈\n\n可以按功能标题告诉我“已实现并在哪次测试过”“还缺哪些部分”“已经不用”。编号只保存在元数据用于关联，界面无需记忆编号。确认后更新对应功能的 implementationState、测试证据和计划进度。\n')
write('Docs/工程/文档整理说明.md',f'''# 文档整理说明\n\n需求按功能划分为 {len(features)} 个工程功能案，加上 Karolina 产品案；决策修改合入相应功能的历史与现行边界，不独立建 54 份需求。\n\n所有找到的旧计划（含历史 Archive 与原 MVP 计划）共 {len(plan_coverage)} 份均在迁移覆盖清单：同功能合并，候选机制和被替代方案登记退役。中文计划明确区分当期完成报告、当前源码检索和未经重跑的测试。\n\nDocs/需求、Docs/计划、Docs/规则 与界面类别一致；正文不放编号/状态元数据。每份文档配 .meta.json，保存标题、状态、来源、哈希、演进与源码/测试证据。Docs/工程 是可查询事实与疑问清单，Docs/资源 保留当前操作指南和实际资源索引。\n\n已接受需求不是完成证明，历史计划完成也不是今天测试通过。没有确认的事项在待确认清单；本期没有擅自补齐 Gameplay 的缺失功能。\n\n原始 Docs 已完整快照到本机 `{snapshot}`，manifest.json 有逐文件 SHA-256。快照保留旧原文和时间线，当前文档树不再积压旧副本。Git 历史也保留已跟踪旧资料。\n''')
print(json.dumps(dict(features=len(features),plans=len(groups),oldPlans=len(plan_coverage),uncertainPlans=len(unknown_plans),documents=len(entries),backup=str(snapshot)),ensure_ascii=False))
