"""本轮中文文档生命周期迁移。恢复原执行批次，不复制旧设计或 Decision Log 正文。"""
from pathlib import Path
import json, re, hashlib, zipfile, os, collections

ROOT = Path(__file__).resolve().parents[3]
BACKUP = Path(os.environ['LOCALAPPDATA'])/'Karolina/recovery/20261002-document-lifecycle'
if not (BACKUP/'Docs.zip').exists(): raise RuntimeError('先完成本轮文档恢复快照')
def read(p): return p.read_text(encoding='utf-8-sig')
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def save(p, value): p.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
metadata = {m['id']: (p,m) for p in (ROOT/'Docs').rglob('*.meta.json') if (m:=json.loads(read(p)))}
if not any(i.startswith('PLAN-FEAT-') for i in metadata):raise RuntimeError('本轮迁移已经执行；禁止在当前独立计划库重复运行')
coverage = json.loads(read(ROOT/'Docs/工程/迁移覆盖清单.json'))
recovery = zipfile.ZipFile(Path(os.environ['LOCALAPPDATA'])/'Karolina/recovery/20261002-document-revision/Docs.zip')
titles = {
0:'工程基线调查与确定性框架规划',1:'逻辑 Tick 上下文与随机状态',2:'稳定单位 UID 值合同',3:'确定性随机集合操作',4:'规范基础类型字节写入',5:'单位稳定注册内核',6:'点圆空间实体基础',7:'确定性随机几何操作',8:'线段矩形几何查询',9:'生成 Tick 的主动逻辑门禁',10:'单位分类与子分类查询',11:'单位生命状态权威状态机',12:'圆形目标窄相查询',13:'能力状态存储与死亡门禁',14:'队伍身份与队伍查询',15:'空间查询信息与注册',16:'空间世界注册内核',17:'单位生成身份',18:'属性 Handler 基础',19:'空间网格与单位最终网格',20:'回滚合同与属性快照',21:'战斗 Modifier 集合',22:'单位原型与定义表',23:'Buff 系统基础',24:'帧同步 Tick 管线基础',25:'控制系统基础',26:'投射物系统基础',27:'框架合同审计与修复',28:'装备与金币基础',29:'单位事件总线与空间快照',30:'装备交易与效果分发',31:'玩家输入系统',32:'非英雄单位基础',34:'技能命令与类型化派发',35:'移动系统补全',36:'非英雄快照与回滚',37:'Buff 与装备被动重建',38:'死亡复活与命中反应',39:'投射物命中与定义注册',40:'命中反应集成',41:'寻路基础设施',42:'表现事件桥接基础',43:'攻击特效管线基础',44:'经验与升级系统',45:'金币装备交易补全',46:'完整合同恢复',47:'确定性 A星寻路',48:'技能输入配置烘焙',49:'小兵波次生成',50:'技能指示器基础',51:'表现事件桥接集成',53:'Lua 界面桥接',54:'技能创作与烘焙',55:'商店界面基础',58:'Buff 效果库',59:'战斗结算增强',60:'表现桥接补全',61:'技能 Stage 定义库',62:'攻击特效管线补全',63:'装备被动运行时',87:'计分板与小地图',88:'野怪营地配置烘焙',89:'技能冷却与指示器',90:'对局流程启动',91:'寻路集成',92:'结果界面',93:'英雄选择界面',94:'完整 Gameplay 链路集成测试',109:'设计合同恢复总计划',110:'可运行组合根与中立测试夹具',111:'快照与共享校验完整性',112:'本地命令金币与权威比赛流程',113:'普攻战斗来源合同恢复',114:'投射物战斗命中管线',115:'通用技能创作与玩家输入',116:'强制移动冲刺与 RVO',117:'通用非英雄与比赛拓扑',118:'表现事件历史与商店视图',119:'权威帧恢复与预测限制',120:'UOS NGO 应用与大厅流程',121:'开局载荷与运行时初始化',122:'确定性地图拓扑与目标夹具',123:'客户端控制表现与界面组合',124:'大厅与 UOS 端到端组合',125:'单位预制体界面生命周期与寻路',126:'Lua 界面重建',127:'装备商店合同补全',128:'Buff 合同补全',129:'场景拆分与应用流程',130:'控制系统合同补全',131:'防御塔与小兵',132:'鬼索狂暴之刃装备',133:'亚托克斯英雄内容',134:'UOS 回滚与客户端反馈',135:'相机与指针表现',136:'单调时钟开局与 Tick率独立创作',137:'单位动作仲裁迁移',138:'客户端与服务器资源构建待验收',139:'同 Tick 战斗公平性',140:'动作键暴击与投射物平局',141:'对局范围 Addressables 内容',142:'核心层单位释放策略',143:'加载移交与指示器 Shader',144:'玩家指示器运行时材质',145:'指示器 Bundle Shader 绑定',146:'客户端核心指示器 Shader',147:'死亡普攻目标原子失效',148:'可配置插值动画采样',149:'动画输入与相机回归修复',150:'真实客户端战斗表现回归',151:'韦鲁斯输入指示器与英雄测试回归',152:'命令 Bundle 幂等与发送门禁',153:'UOS 指示器重绑定与韦鲁斯 Q 权威恢复',154:'W 特效奖励与 CDN 收尾',155:'正式游戏启动器',156:'启动器资源范围收尾',157:'UOS 启动器完整安装与增量更新',158:'CDN 控制台安全分片上传',159:'CDN 扁平内容布局',160:'可选发布客户端 CDN 构建',161:'自适应命令时间与结构效果过滤',162:'三狼运行行为修正',163:'工程资源与代码清理',164:'地图营地运行时组合修复',165:'三狼回营与动态导航改造',166:'首期工程工作台历史实施'}

for p,m in metadata.values():
    if m['type']=='requirement':
        m['status']='废弃' if m.get('status') in ['废弃','已淘汰'] else '激活'
        kept=[]; maintenance=[]
        for h in m.get('history',[]):
            if h.get('legacyDecision') or not re.search('按功能|按模块|机械迁移|取证|人工编辑|创建草案|拆分|整理为|从.*提取',h.get('summary','')):
                # 演进只记录需求变化；导入时间和文件指纹归来源。
                kept.append({k:v for k,v in h.items() if k in ['date','summary','scope','version','legacyDecision']})
            else: maintenance.append(h)
        m['history']=kept
        if maintenance:m.setdefault('provenance',[]).extend(maintenance)
    elif m['type']=='rule':
        m.pop('status',None)
        if m.get('history'):m['provenance']=m.pop('history')
    elif m['type']!='plan' and m.get('history'):m['provenance']=m.pop('history')
    save(p,m)

# 在真正的源码声明处定位旧范围；旧名字失效时使用对应功能的当前实现，而非伪造旧类型存在。
def read_code(p):
    try:return read(p)
    except UnicodeDecodeError:return p.read_text(encoding='gb18030')
code = {p.relative_to(ROOT).as_posix():read_code(p) for p in (ROOT/'Assets/Scripts').rglob('*.cs')}
symbols = collections.defaultdict(list)
for path, text in code.items():
    for symbol in re.findall(r'\b(?:class|struct|enum|interface)\s+(\w+)',text):symbols[symbol].append(path)
    for symbol in re.findall(r'(?m)^\s*(?:public|internal|protected|private)\s+(?:static\s+|virtual\s+|override\s+|async\s+|sealed\s+)*(?:[\w<>,.?\[\]]+\s+)+(\w+)\s*\(',text):symbols[symbol].append(path)
def excerpt(text, name=None, limit=45):
    lines=text.splitlines()
    start=next((i for i,l in enumerate(lines) if name and re.search(r'\b'+re.escape(name)+r'\s*\(',l)),0)
    return '\n'.join(lines[start:start+limit])
def section(text,name):
    found=re.search(r'^## '+re.escape(name)+r'\s*\n(.*?)(?=^## |\Z)',text,re.M|re.S)
    return found.group(1).strip() if found else ''
def refs(ids):
    return [{'id':i,'title':metadata[i][1]['title'],'status':metadata[i][1]['status'],'version':metadata[i][1].get('version',1),
             'path':metadata[i][1]['path'],'sections':['目标实现','技术方案','边界情况','附录'],'hash':digest(ROOT/metadata[i][1]['path'])} for i in dict.fromkeys(ids) if i in metadata and metadata[i][1]['type']=='requirement']
def ref_text(items,planpath):
    return '\n'.join(f"- [{q['title']}]({os.path.relpath(ROOT/q['path'],ROOT/planpath.parent).replace(chr(92),'/')})：目标实现、技术方案、边界情况与附录。引用版本 {q['version']}。" for q in items)

new_entries=[]; results=[]; claimed=set(); old_redirect={}
for source in coverage['plans']:
    filename=Path(source['source']).name
    if '_candidates' in filename or source.get('newPlan') is None:
        source['disposition']='候选或被替代提案保留在迁移来源覆盖，不创建可执行计划'; continue
    oldid=source['newPlan']; oldp,old=metadata[oldid]; oldbody=read(ROOT/old['path'])
    batch=int(re.search(r'(\d{4})',filename)[1]); title=titles[batch]
    variant=''
    if batch==46 and 'monobehaviour' in filename:title='单位 MonoBehaviour 与预制体组合';variant='-COMPOSITION'
    if batch==53:variant='-FOUNDATION' if 'foundation' in filename else '-BRIDGE';title+='基础实现' if variant=='-FOUNDATION' else '最初提案'
    if batch==54:variant='-AUTHORING' if 'authoring' in filename else '-BAKE';title+='创作管线' if variant=='-AUTHORING' else '最初烘焙方案'
    ident={138:'PLAN-CONTENT-138',165:'PLAN-WOLF-165',166:'PLAN-KAR-001'}.get(batch,f'PLAN-{batch:04d}{variant}')
    if ident in claimed: raise RuntimeError('重复计划编码 '+ident)
    claimed.add(ident)
    path=Path(f"Docs/计划/{old['domain']}/{title}.md")
    reqs=refs(source.get('targets',[]) or old.get('requirements',[]))
    if batch==0:reqs=refs(['REQ-KAR-001'])
    entry=source.get('recoveryEntry') or next((e for e in recovery.namelist() if e.endswith('/'+filename)),None)
    original=recovery.read(entry).decode('utf-8-sig').replace('\r','') if entry else ''
    artifacts=next((a for a in old.get('legacyImplementationArtifacts',[]) if a['source']==source['source']),{})
    tokens=set(re.findall(r'`([A-Za-z_][\w.]*)',original))|set(artifacts.get('interfaces',[]))|set(artifacts.get('testEntrypoints',[]))
    tokens={t.split('.')[0] for t in tokens}
    selected=list(dict.fromkeys(p for t in sorted(tokens) for p in symbols.get(t,[])))
    # 比较源码路径与旧范围，排除无关的共享基础类型。
    specific=[p for p in selected if Path(p).stem in tokens or any(t in Path(p).stem for t in tokens if len(t)>8)]
    selected=specific or selected
    selected=sorted(selected,key=lambda p:('Tests' in p,p))[:16]
    if not selected:selected=[e['path'] for e in old.get('evidence',[]) if e.get('path') in code][:8]
    prod=[p for p in selected if 'Tests' not in p]
    testpaths=[p for p in selected if 'Tests' in p]
    if not testpaths:testpaths=[e['path'] for e in old.get('evidence',[]) if e.get('path') in code and 'Tests' in e['path']][:3]
    source_problem=[]
    if batch==94:
        source_problem=['现有 GameplayIntegrationTests 主要验证 Tick/随机/UID/路径和配置值，没有旧计划要求的生成→AI→寻路→战斗→死亡→表现完整链路测试。人工游玩正常不等价于这项测试系统已经实现。']
    status='暂缓' if batch==165 else '待验收' if batch in [138,94] else '已取消' if batch in [53,54] and variant in ['-BRIDGE','-BAKE'] or batch==166 else '已关闭'
    result='用户本轮确认人工测试正常；本期核对对应当前实现入口、数据所有者和已有测试源码，未发现本切片新增阻塞。历史执行记录关闭，不重启；未重新运行 Unity 行为测试。'
    if batch==0:result='文档与工程基线调查当期完成；后续执行由独立计划承接，当前仅作关闭记录。'
    if status=='已取消':result='当期接口提案已被后续执行批次替代；保留关闭边界，不重新实现已淘汰接口。'
    if batch==138:result='保留 Windows 客户端与 Linux 服务器配套实际构建和运行验收；本轮用户的功能人工测试不能替代这项构建证据，未发送构建请求。'
    if batch==165:result='保留原暂缓记录，当前代码已有动态导航、营地耐心及 schema 25 部分工作。原批次不重启；继续工作应新建计划引用这些需求，逐项核对已完成范围。'
    if source_problem:result='\n'.join(source_problem)
    assessment={'date':'2026-10-02','manualEvidence':'用户本轮说明不能确认的功能已手动测试正常','sourceEvidence':selected,'conclusion':result,'unityTestsRerun':False,'issues':source_problem}
    m={'id':ident,'code':f'{batch:04d}{variant}','title':title,'type':'plan','domain':old['domain'],'status':status,'path':path.as_posix(),'version':1,
       'requirements':[q['id'] for q in reqs],'requirementRefs':reqs,'sources':[source.copy()],'assessment':assessment,
       'evidence':[{'path':p,'sha256':digest(ROOT/p),'kind':'当前源码定位，行为未重跑'} for p in selected+testpaths if p in code],
       'closedAt':'2026-10-02' if status in ['已关闭','已取消'] else None}
    source['newPlan']=ident; source['disposition']='恢复独立执行批次及生命周期，具体需求版本与章节已登记'
    m['sources'][0]['newPlan']=ident
    old_redirect.setdefault(oldid,[]).append(ident)
    if batch in [138,165,166]:
        body=re.sub(r'^# .*$',f'# {title}',oldbody,count=1,flags=re.M)
        body=re.sub(r'(^## 参考需求\s*\n).*?(?=^## |\Z)',lambda match:match[1]+'\n'+ref_text(reqs,path)+'\n\n',body,flags=re.M|re.S)
        if not re.search(r'^## 参考需求',body,re.M):body=f'# {title}\n\n## 参考需求\n\n{ref_text(reqs,path)}\n\n'+body.split('\n',1)[1]
        body+='\n## 本期核查结论\n\n'+result+'\n'
    else:
        summary=section(oldbody,'实施技术细节').split('###')[0].strip() or section(oldbody,'实施细节').split('###')[0].strip()
        body=f'# {title}\n\n## 本次执行范围\n\n本计划对应原编码 {batch:04d} 的一次执行：{title}。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。\n\n## 参考需求\n\n{ref_text(reqs,path)}\n\n## 实施技术细节\n\n{summary}\n'
        if prod:
            body+='\n### 当前类型、核心算法与数据流\n\n'
            for p in prod[:4]:
                text=code[p]; declarations=re.findall(r'\b(?:class|struct|enum|interface)\s+(\w+)',text)
                methods=[n for n in tokens if re.search(r'\b'+re.escape(n)+r'\s*\(',text) and n not in declarations]
                body+=f"- `{p}`：{ '、'.join('`'+n+'`' for n in declarations[:7]) }。\n"
                if methods:body+='  本次范围对应入口：'+ '、'.join('`'+n+'`' for n in sorted(methods)[:8])+'。\n'
            body+='\n以下摘录是当前工程的真实实现，输入校验、字段和调用顺序以代码为准；它不冒充历史版本。\n'
            for p in prod[:2]:
                names=[n for n in sorted(tokens) if re.search(r'\b'+re.escape(n)+r'\s*\(',code[p]) and n not in re.findall(r'\b(?:class|struct)\s+(\w+)',code[p])]
                body+=f'\n`{p}`：\n\n```csharp\n{excerpt(code[p],names[0] if names else None)}\n```\n'
        body+='\n### 输入输出与边界\n\n'
        for q in reqs[:4]:
            reqbody=read(ROOT/q['path']);tech=section(reqbody,'技术方案');edge=section(reqbody,'边界情况')
            body+=f"**{q['title']}**\n\n{tech[:850]}\n\n{edge[:600]}\n\n"
        body+='## 执行结果\n\n'+result+'\n\n## Agent 测试与验收\n\n'
        body+='本期只做源码复核，用户人工测试为独立证据；下面是后续回归时可以直接执行的测试设计，不记录为本轮测试通过。\n\n'
        for p in testpaths[:3]:
            text=code[p];tests=re.findall(r'\[(?:Test|TestCase)[^\]]*\]\s*(?:\[[^\]]*\]\s*)*public\s+(?:void|IEnumerator|async\s+Task|Task)\s+(\w+)\s*\(',text)
            if not tests:tests=re.findall(r'public\s+(?:void|IEnumerator|Task)\s+(\w*(?:Test|RoundTrip|Restore|Reject|Invalid|Apply|Query)\w*)\s*\(',text)
            mode='PlayMode' if 'PlayMode' in p else 'EditMode'
            body+=f"- `{p}`：{mode}，输入/夹具和期望断言见 `{tests[0] if tests else Path(p).stem}`；失败保留 Console 和测试回执，不能关闭实施计划。\n"
            if tests:body+=f'\n```csharp\n{excerpt(text,tests[0],30)}\n```\n'
        if not testpaths:body+='此历史切片没有找到可精确关联的当前测试函数；不能据此宣称自动化验收已通过。\n'
        body+='\n## 恢复与限制\n\n不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。\n'
    (ROOT/path).parent.mkdir(parents=True,exist_ok=True);(ROOT/path).write_text(body,encoding='utf-8');save((ROOT/path).with_suffix('.meta.json'),m)
    new_entries.append(m);results.append((m,source))

# 聚合稿已备份；当前树只保留具有独立执行边界的计划。
preserve={'PLAN-KAR-002','PLAN-CONTENT-138','PLAN-WOLF-165','PLAN-KAR-001'}
for ident,(p,m) in metadata.items():
    if m['type']=='plan' and ident not in preserve:
        for target in [p,ROOT/m['path']]:
            absolute=target.resolve()
            if not absolute.is_relative_to((ROOT/'Docs/计划').resolve()):raise RuntimeError('清理路径越界')
            target.unlink()
for ident in ['PLAN-KAR-002']:
    p,m=metadata[ident];m['status']='已关闭';m['code']='0167';m.pop('history',None);m['requirementRefs']=refs(m.get('requirements',[]));save(p,m)
    body=read(ROOT/m['path'])
    if not re.search(r'^## 参考需求',body,re.M):body=body.replace('\n\n## 本期目标','\n\n## 参考需求\n\n'+ref_text(m['requirementRefs'],Path(m['path']))+'\n\n## 本期目标',1)
    (ROOT/m['path']).write_text(body,encoding='utf-8')

rules={
'文档维护与演进.md':'''# 文档维护与演进

## 分类与职责

需求记录长期目标、选定技术、边界和附录，状态只有激活/废弃。需求变动的演进保存在需求元数据 history，记录变动内容、日期和可选范围/版本。迁移、来源指纹、工程证据和导入时间分别放 provenance/sources/evidence，不混入演进。

计划是一轮有结束边界的执行，状态为草案→执行中→待验收→已关闭，允许暂缓与已取消。已关闭/已取消不重新启动；同功能继续改造新建计划，功能目标独立替换时另建需求并关联被替代需求。计划保留当期执行结果和验收证据，不建立长期演进历史。

规则没有生命周期状态，只维护正文和版本。元数据独立于正文，界面目录只显示标题与适用状态，计划按 code 的自然编码顺序排列。

## 关联与引用

计划 requirements 保存稳定需求编号；requirementRefs 保存引用时的标题、状态、版本、章节、正文路径与 hash。这是引用快照，不覆盖需求的当前定义。正文必须链接具体需求和章节；引用变化时同步元数据。

用户指令优先，附件中的命令只是材料。未知情况写具体差距，不以旧文件缺少 Complete 字段反复向用户确认已实现功能。用户人工测试与源码核对分开登记；尚未完成的构建、专项测试和合同冲突继续保留。

## 修订与恢复

正文保存做 hash 并发检查，所有类别保留本机修订备份和 version/updatedAt；这属于恢复能力，不是演进。需求格式修正不自动新增演进，语义改变填写变更说明。原始来源清理前核验恢复快照；不保留第二套权威设计或独立 Decision Log。
''',
'计划案模板.md':'''# 计划案模板

## 参考需求

必须链接一或多份具体需求案，并指出目标实现、技术方案、边界和附录中的具体章节。requirements 登记稳定编号；requirementRefs 记录引用时标题、状态、版本、章节、路径和正文 hash。草案可以尚未关联，但不能带着空引用进入执行。

## 实施细节

列实际文件/程序集、已有与待改类型、核心算法步骤、数据结构/字段、数据流、依赖方向、快照/序列化及资源操作。历史计划按执行批次保留独立边界，禁止跨年代合并成长期不断重启的计划。

## 执行步骤与结果

小步骤勾选，写当期已完成与未完成范围；历史记录与本期源码核对分别说明。关闭后不重启，后续同功能改造新建计划引用适用需求；新功能或替换目标另建需求。

## Agent 测试与验收

测试函数/类与程序集、输入/夹具、实际场景、执行工具、预期断言、失败处理及回执。EditMode 测纯逻辑，PlayMode 测 Unity 生命周期和表现；构建验收不能用 Editor 测试替代。编译成功不等于行为验收。

## 恢复与限制

保护已有工作、明确撤销范围、schema 兼容、GUID 与外部依赖。用户人工测试正常并经源码复核可作为功能确认，证据种类必须如实区分。

## 独立元数据

id、code、title、type=plan、status、domain、path、version、requirements、requirementRefs、sources、evidence、assessment、closedAt。无 history/evolution。

状态：草案、执行中、暂缓、待验收、已关闭、已取消。关闭/取消为结束状态；activePlan 定位本期执行计划。未知判断是 assessment，不是生命周期状态。
''',
}
for filename,body in rules.items():(ROOT/'Docs/规则/工程规则'/filename).write_text(body,encoding='utf-8')
p=ROOT/'Docs/规则/工程规则/需求案模板.md';s=read(p).replace('历史决策作为本功能 evolution/history','历史需求修订作为本功能 history').replace('id、title、type=requirement、status','状态只有激活/废弃，不执行计划的测试/关闭流程。id、title、type=requirement、status');p.write_text(s,encoding='utf-8')
for p in [ROOT/'.agents/PLANS.md',ROOT/'Docs/规则/工程规则/Agent执行周期.md']:
    s=read(p).replace('进行中','执行中').replace('已完成','已关闭').replace('和淘汰','和取消');p.write_text(s,encoding='utf-8')

# 本轮独立需求与执行计划；已关闭上期计划不修改执行进度。
reqpath=Path('Docs/需求/Karolina/文档生命周期与工作台交互.md')
reqbody='''# 文档生命周期与工作台交互

## 目标实现

需求只有激活/废弃并保留语义变动历史；计划按编码顺序浏览，有执行、验收和关闭边界；规则不显示状态。正文阅读顺畅，切换目录分类保留正在阅读的文档标题。元数据、关联、来源与证据从独立按钮浏览，只有需求提供演进入口。

Git 暂存、取消与提交可用，刷新更新列表与当前差异；默认只展示文本差异，可主动查看二进制与结构变更。AI 对话顶部独立放置 Codex 与 Unity 连接/操作，刷新模型账号和运行详情只在此页面显示。

## 技术方案

沿用 .NET 本地服务与 Edge 前端，不新增第三方包。文档目录按 sidecar 的文件时间/长度缓存，正文一次读取后计算 hash，元数据独立按需读取。前端缓存阅读内容并后台核对，异步请求采用序列保护，保留未保存编辑。需求语义修改通过独立说明记入 history，技术来源与恢复信息另存。

计划使用 code 自然排序；requirements 是稳定 ID，requirementRefs 是标题/状态/版本/章节/路径/hash 的引用快照。关闭计划服务端拒绝保存执行正文。历史独立批次从经过核验的恢复快照重新整理，源码核查与用户人工测试分开登记。

Git 从 porcelain 获取路径，以 NUL 分隔 UTF8 标准输入与 --literal-pathspecs 传入批量暂存操作，避开 Windows 命令行长度。两个 diff --numstat -z 批量查询给出真实区域差异，过滤归一化空差异；二进制与空文件等从开关查看。仓库残留索引锁核实无使用者后移到恢复目录，不修改原 index。

## 边界情况

计划关闭/取消后不能重启；旧类型已替换时说明当前对应实现。源码证据不能虚构真实测试通过；配套构建未跑、专用测试不完整、金币合同冲突与未定义目标策略继续保留。Git 被实际进程占锁时显示真实错误，不自动删活跃锁。未暂存不能提交，推送仍限定配置上游。快速切换、保存期间继续输入和外部修改不能覆盖用户编辑。

## 附录

本轮用户十三项反馈是授权范围；来源恢复位置为 LocalAppData/Karolina/recovery/20261002-document-lifecycle/Docs.zip。没有截图验收，不改变 Gameplay、资产、包和工程设置，不自动提交本工程。
'''
(ROOT/reqpath).write_text(reqbody,encoding='utf-8');req={'id':'REQ-KAR-004','title':'文档生命周期与工作台交互','type':'requirement','status':'激活','domain':'Karolina','path':reqpath.as_posix(),'version':1,'history':[],'related':['REQ-KAR-003'],'sources':[{'kind':'user','summary':'本轮十三项反馈','date':'2026-10-02'}]};save((ROOT/reqpath).with_suffix('.meta.json'),req);metadata[req['id']]=((ROOT/reqpath).with_suffix('.meta.json'),req)
planpath=Path('Docs/计划/Karolina/文档生命周期与工作台交互修订.md');q=refs(['REQ-KAR-004'])
body=f'''# 文档生命周期与工作台交互修订

## 参考需求

{ref_text(q,planpath)}

## 实施细节

DocumentLibrary 分类状态验证、自然编码排序、目录缓存、正文快照、按需元数据与关闭保护。需求演进仅在明确说明语义变化时追加。前端元数据分栏、独立需求历史、页面/文档/编辑/差异请求序列与正文缓存。

GitService.ReviewStatus 两区域批量 numstat 过滤；Action 以 NUL stdin literal 路径执行批量 add/reset/rm，保留暂存后再编辑、初始无 HEAD 与重命名。刷新重新核对当前 diff。原独立历史执行批次恢复，补齐需求引用快照和真实源码/人工核查证据。

## 执行步骤

- [x] 核查现有接口、备份当前文档及原工作区指纹，核实并移出过期索引锁。
- [x] 实现文档生命周期、快读、元数据交互与 Git 修复。
- [x] 恢复独立旧计划批次，补齐具体引用和源码核查，隔离真实未完成项。
- [ ] 编译与无截图功能验证，独立只读审查并修复。

## Agent 测试与验收

核心临时仓库验证：需求保存不自动产生历史、带说明追加历史；规则无状态；计划关闭禁止再保存；自然编码排序；外部文件更新可见。Git 中文/空格/换行/特殊路径、超过500文件的批量 stdin、仅换行/触碰空差异、暂存/再编辑/取消/提交，以及刷新后的 diff 清理。

Edge DOM 验证：页面切换保留正文标题，信息和历史独立，规则无徽章，计划编码排序，模型/运行与Unity操作只在AI对话顶部。模拟延迟验证快速切换、放弃dirty和保存期间的新输入不会被旧回执覆盖。真实连接仅只读获取模型/账号/Unity状态；不运行 Gameplay 测试、不请求构建，不截图。

## 结果与限制

进行中；本轮人工功能确认与源码核查不声明自动化测试绿灯。原构建与测试系统缺口、公开金币合同和 EquipmentTargetPolicy 未定义继续记录。
'''
(ROOT/planpath).write_text(body,encoding='utf-8');save((ROOT/planpath).with_suffix('.meta.json'),{'id':'PLAN-KAR-003','code':'0168','title':'文档生命周期与工作台交互修订','type':'plan','status':'执行中','domain':'Karolina','path':planpath.as_posix(),'version':1,'requirements':['REQ-KAR-004'],'requirementRefs':q})
save(ROOT/'Docs/工程/迁移覆盖清单.json',coverage)
catalog=json.loads(read(ROOT/'Docs/catalog.json'));catalog['activePlan']='PLAN-KAR-003';catalog['entries']=[]
for p in sorted((ROOT/'Docs').rglob('*.meta.json')):
    m=json.loads(read(p));catalog['entries'].append({'id':m['id'],'path':m['path'],'metadata':p.relative_to(ROOT).as_posix()})
save(ROOT/'Docs/catalog.json',catalog)

report='# 历史执行范围源码复核\n\n用户本轮确认手动测试正常；下表按原批次核对当前源码与已有测试函数。源码核对不虚构新执行的测试回执。老接口被替换时关闭旧提案，不重新实现它。\n\n|计划|状态|源码定位|结论|\n|---|---|---|---|\n'
for m,source in results:
    links='、'.join('`'+Path(p).stem+'`' for p in m['assessment']['sourceEvidence'][:4])
    report+=f"|[{m['title']}]({os.path.relpath(ROOT/m['path'],ROOT/'Docs/工程').replace(chr(92),'/')})|{m['status']}|{links}|{m['assessment']['conclusion']}|\n"
p=ROOT/'Docs/工程/历史执行范围源码复核.md';p.write_text(report,encoding='utf-8');save(p.with_suffix('.meta.json'),{'id':'FACT-SOURCE-REVIEW','title':'历史执行范围源码复核','type':'engineering','status':'已核查','domain':'工程事实','path':p.relative_to(ROOT).as_posix(),'version':1})
catalog['entries'].append({'id':'FACT-SOURCE-REVIEW','path':p.relative_to(ROOT).as_posix(),'metadata':p.with_suffix('.meta.json').relative_to(ROOT).as_posix()});save(ROOT/'Docs/catalog.json',catalog)
print('独立计划',len(new_entries)+2,'来源',len(coverage['plans']),'具体需求引用',sum(len(x.get('requirementRefs',[])) for x in new_entries))
