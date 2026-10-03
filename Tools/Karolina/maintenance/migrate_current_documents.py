"""一次性按编号+英文功能名迁移，保留中文正文；旧证据归入所属计划。"""
from revise_current import ROOT,write,dump
from pathlib import Path
import json,re,os,zipfile,hashlib
docs=ROOT/'Docs'
if any(docs.rglob('REQ-KAR-005_*.md')):raise SystemExit('本次迁移已执行，请勿重复运行')
backup=Path(os.environ['LOCALAPPDATA'])/'Karolina/recovery/current-document-layout'
backup.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(backup/'Docs.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in docs.rglob('*'):
  if p.is_file():z.write(p,p.relative_to(ROOT).as_posix())
records={}
for p in docs.rglob('*.meta.json'):
 d=json.loads(p.read_text(encoding='utf-8-sig'));records[d['id']]=(p,d)
req='REQ-KAR-005';plan='PLAN-KAR-004'
write('Docs/需求/Karolina/当前版本与最小Git工作流.md','''# 当前版本与最小 Git 工作流

## 目标实现

Karolina 只保留一个当前程序输出和一个启动入口。中文正文、中文显示标题与编号加英文功能文件名分开。需求演进写入正文；来源、证据、关系和版本信息留独立元数据。

Git 工作台提供 GitHub 连接状态、Commit、Push、Pull、最近提交历史和 diff 审查。索引选择是 Commit 的准备操作，不是保存工作区的 Stash；本期没有 Stash、分支管理、合并或强推。

## 技术方案

- 文档以稳定编号定位；正文和同名 sidecar 使用 `ID_english-functional-title.md` / `.meta.json`，目录和标题仍中文。新建 UI 提供英文文件标题字段，编号自动分配。
- 需求正文的“需求演进”保存变动前后、理由、技术选择、边界及兼容影响、相关计划。旧来源迁移记录保留实际原有细节，不补造历史理由。
- 计划按准备→执行→测试→验收→关闭推进；测试或人工验收发现问题进入校正，再执行/测试。机器检查和独立 Agent 审查属于测试；人工确认属于验收。关闭为终态，后续改造建立新计划。
- Git 使用现有 Git CLI 和 Credential Manager，HTTPS github.com 为本期远端范围。凭据存系统凭据库，不进入工程、日志和页面。连接状态表示本机账号凭据可用，不宣称某个仓库写权限已经通过。
- Commit 只提交用户主动加入索引的内容；Push 只发送当前分支的明确上游。Pull 要求干净工作区并使用 `--ff-only --no-rebase`，分叉/冲突交由外部 Git 工具处理。
- 锁诊断显示 index.lock 的存在、大小和 Git 进程；仅经用户点击恢复旧的零字节、无 Git 进程且可独占写访问的锁，移到本机恢复位置，不删非空或正在使用的锁。
- 原生窗口进程监测取代心跳延迟退出；最小化保持任务运行，完全退出关闭窗口、本机 HTTP 服务与本程序的 Codex 子进程。退出保留已写入工作区的文件。

## 边界情况

已有 GitHub 凭据可直接使用；新增登录由系统浏览器完成。网络权限不足、账号失效、远端保护分支或工作区锁均必须显示失败。没有 GitHub HTTPS 上游不猜测推送目标。索引既有内容在 Commit 前显示；其它 IDE 的活动不由 Karolina 强行终止。

只保留当前应用输出，不保留 verified/lifecycle/GUID 构建目录。源代码和唯一原始材料恢复档不是应用版本；正文保存的本机恢复能力不作为第二套需求权威。

## 验收条件

临时仓库验证实际选择/取消、Commit、history/diff、Pull 前置条件、Push 当前上游和残留锁恢复。HTTP/DOM 验证六阶段状态、正文演进、英文物理命名及窗口操作；本工程不自动提交或推送。用户对当前程序做人工验收后关闭计划。

## 附录

启动入口：`Tools/Karolina/Start-Karolina.ps1`。当前应用输出：`Tools/Karolina/artifacts/current`。使用既有 .NET 8、Git、Git Credential Manager、Edge，没有新增包。

## 需求演进

### 2026-10-02 · 接受本轮修订

此前方案把需求演进放进元数据，计划用“草案/待验收”混合机器与人工完成状态，启动时生成多个 GUID 构建目录，Git 页面缺少账号、拉取和提交历史。用户本轮要求改为正文演进、六阶段执行周期、编号加英文文件名和单一当前版本。

选择既有 GCM 浏览器登录及 Git CLI，保持凭据在系统；保留索引选择以限定 Commit 范围。人工验收与自动验证分开，计划不会因构建或 Agent 审查通过自动关闭。过时说明与验收报告归入所属计划后从当前资料目录删除。

影响范围为 Karolina 桌面、项目文档与入口路径；Gameplay 合同、Unity 资源和已有工作区改动保持各自权威。承接 REQ-KAR-004，通过 PLAN-KAR-004 实施。
''')
write('Docs/计划/Karolina/当前版本与最小Git工作流实施.md','''# 当前版本与最小 Git 工作流实施

## 参考需求

- [当前版本与最小 Git 工作流](../../需求/Karolina/当前版本与最小Git工作流.md)：目标实现、技术方案、边界情况、验收条件与附录。
- [文档生命周期与工作台交互](../../需求/Karolina/文档生命周期与工作台交互.md)：文档阅读、索引和关闭保护；与新六阶段不一致处以本轮新需求为准。

## 实施细节

DocumentLibrary 管理侧车元数据、编号、文件名与正文 hash；语义保存将多行 Markdown 演进附加到正文，计划状态按六阶段图判定。准备可进入执行；执行进入测试；测试进入校正/验收；校正回执行/测试；验收进入校正/关闭。关闭需人工结论并冻结正文/状态。取消结果为关闭计划的 outcome，不另建长期演进。

GitService 调用 GCM 账号列表/浏览器登录，不读取或显示 token。状态合并真实 Git diff 与锁诊断；选择路径通过 UTF8 无 BOM、NUL 输入传给 Git，避免 Windows 命令长度。Commit 使用索引，Push 显式 refspec，Pull 干净且仅快进。History 取最近 100 条提交，提交 diff 禁用外部 diff/textconv。

Workbench API 保留会话令牌和 loopback 限制，Git 写操作持工程锁。Program 只跟踪自己启动的 Edge 窗口，最小化调用 Win32 ShowWindow，关闭窗口停止 HTTP 并回收拥有的子进程。启动脚本输出到唯一 current 目录，已有窗口必须先完整退出才更新二进制。

一次性迁移保留原始恢复包，按编号与英文功能名移动正文/侧车，同步目录、正文相对链接和当前入口；来源的旧路径属于 provenance，不伪装成仍有效的正文。两份旧验收报告并入对应计划，复核汇总已由各计划 source/evidence/assessment 保存，整合说明归入维护规则。

## 六阶段执行

- [x] 准备：读取当前规则、进程、凭据和索引锁，确认 Git 报错与版本累积。
- [ ] 执行：修改文档、Git、窗口与启动路径。
- [ ] 测试：机器构建、临时 Git/HTTP/DOM，以及独立只读 Agent 审查。
- [ ] 校正：处理测试和审查问题后重新测试；没有问题也不补造“已校正”。
- [ ] 验收：用户实际使用最新窗口，确认审查、Commit/Pull/Push 操作与文档体验。
- [ ] 关闭：人工结论登记后冻结；后续变更用新计划。

## 测试设计

纯 .NET 临时目录夹具验证状态跳转、关闭禁止重启、需求正文演进、引用快照、英文文件名与外部更新。Git 临时仓库验证中文/空格/长路径、真实索引选择、Commit/hash/history/diff、零字节过期锁恢复、非空锁拒绝；重写 GitHub HTTPS 测试 URL 到本机 bare 仓库验证 Push/Pull，无生产远端写入。

本机服务 HTTP 与 Edge DOM 验证 GitHub 本机账号状态、锁提示、提交历史和按钮；窗口以进程/窗口句柄核验最小化与完全退出，不用截图。关闭浏览器原生 X 的检测应立即结束服务，退出活动 Agent 时进程树应结束后释放工程锁。

## 结果与限制

本计划执行中。机器/Agent 测试证据完成后填入本节；人工未验收前保持“验收”，不声称真实 GitHub Push 或 Unity 行为已通过。
''')
for id,typ,title,path in [(req,'requirement','当前版本与最小 Git 工作流','Docs/需求/Karolina/当前版本与最小Git工作流.md'),(plan,'plan','当前版本与最小 Git 工作流实施','Docs/计划/Karolina/当前版本与最小Git工作流实施.md')]:
 d={'id':id,'title':title,'type':typ,'domain':'Karolina','status':'激活' if typ=='requirement' else '执行','path':path,'version':1,'createdAt':'2026-10-02'}
 if typ=='plan':d.update(code='0169',requirements=[req,'REQ-KAR-004'],requirementRefs=[])
 else:d.update(related=['REQ-KAR-004'],sources=[{'kind':'user','date':'2026-10-02','summary':'本轮七项修订'}])
 m=ROOT/path.replace('.md','.meta.json');dump(m,d);records[id]=(m,d)

# 过期报告的证据归属到原计划，当前导航不再列出多轮验收报告。
for report,target in [('FACT-KAROLINA-ACCEPTANCE','PLAN-KAR-002'),('FACT-LIFECYCLE-ACCEPTANCE','PLAN-KAR-003')]:
 _,d=records[report];_,t=records[target];b=ROOT/t['path'];b.write_text(b.read_text(encoding='utf-8')+'\n\n## 当期验收证据归档\n\n'+(ROOT/d['path']).read_text(encoding='utf-8').replace('# '+d['title']+'\n','',1),encoding='utf-8')
delete={'FACT-KAROLINA-ACCEPTANCE','FACT-LIFECYCLE-ACCEPTANCE','FACT-775A0C483B17','FACT-SOURCE-REVIEW'}
retired={records[k][1]['path']:records['PLAN-KAR-002' if k=='FACT-KAROLINA-ACCEPTANCE' else 'PLAN-KAR-003' if k=='FACT-LIFECYCLE-ACCEPTANCE' else 'RULE-005'][1]['path'] for k in delete}
for k in delete:
 p,d=records.pop(k);p.unlink();(ROOT/d['path']).unlink()

slugs='''application-flow lobby-slots startup-barrier uos-configuration simulation-tick simulation-pipeline command-dispatch command-forwarding command-timing authoritative-frame rollback-replay snapshot-ownership shared-checksum deterministic-random stable-unit-identity match-statistics configuration-validation match-content-closure client-server-resources unit-capability-composition intent-planning action-arbitration stat-formulas health-shields-regeneration experience-levels stat-refresh unit-events unit-lifecycle combat-causal-waves damage-resistance critical-action-identity healing-settlement combat-operands death-attribution death-rewards structure-effect-admission attack-cycle attack-effects projectile-definitions projectile-lifecycle projectile-hit-order ability-signal-session cast-stages stage-effects ability-catalog ability-passives varus-test-content buff-application buff-reaction-lifecycle buff-capacity crowd-control-instances control-immunity equipment-recipes equipment-effects shop-purchase-validation shop-undo gold-accounting spatial-registration spatial-geometry collision-facts wall-recovery pathfinding-grid path-following astar-search team-flow-field local-avoidance movement-pipeline ai-scheduling minion-waves jungle-camps tower-targeting unit-view-binding animation-sampling presentation-ledger lua-ui-lifecycle hud-minimap shop-view input-event-buffer ability-input aim-requests skill-indicator game-install content-updates safe-content-upload'''.split()
assert len(slugs)==84
names={f'REQ-FEAT-{i:03d}':s for i,s in enumerate(slugs,1)}
names.update({'REQ-KAR-001':'workbench-product','REQ-KAR-003':'core-workbench-knowledge','REQ-KAR-004':'document-workbench-interaction',req:'current-version-git-workflow',plan:'current-version-git-implementation','PLAN-KAR-001':'original-mvp','PLAN-KAR-002':'workbench-reconstruction','PLAN-KAR-003':'document-interaction-revision','PLAN-WOLF-165':'wolf-return-navigation','PLAN-CONTENT-138':'addressables-server-isolation','REQ-WOLF-NAV':'wolf-return-navigation','REQ-WOLF-ANIM':'wolf-alert-animation','REQ-WOLF-PATIENCE':'camp-patience','RULE-001':'engineering-constitution','RULE-002':'agent-execution-cycle','RULE-003':'requirement-template','RULE-004':'plan-template','RULE-005':'document-maintenance','RULE-006':'verification-evidence','RULE-007':'unity-assets-build','RULE-008':'git-review-integration','FACT-4E2A6CA25B98':'engineering-index','FACT-82B7CF40B5E6':'current-state','FACT-9CD2F2F2C7BD':'open-questions','GUIDE-CONTENT':'resource-content-boundaries','GUIDE-001':'build-guide','GUIDE-002':'local-client-server','GUIDE-003':'time-tickrate','GUIDE-004':'async-diagnostics','GUIDE-005':'game-launcher-cdn','GUIDE-006':'uos-client-launcher'})
parts={20:['unit-identity-state','capability-combat-modifiers','handler-dependencies','movement-spatial-seams'],28:['spawn-ai-registration','death-disposal','hero-respawn-pooling','lifecycle-restore-order','startup-unit-events'],40:['projectile-spatial-ownership','projectile-deferred-submit','projectile-motion-order','projectile-pool','projectile-snapshot','projectile-integration'],42:['signal-cast-completion','ability-indicator-query','ability-ui-events','catalog-session-ownership','ability-snapshot-tick'],51:['control-handler-lifecycle','control-instance-signals','control-bake-execution','control-identity-parameters','standard-control-effects','control-snapshot-rules'],67:['movement-contracts','movement-routes','dash-forced-teleport','movement-grid-order','movement-snapshot','movement-implementation']}
for n,ss in parts.items():
 for i,s in enumerate(ss,1):names[f'REQ-FEAT-{n:03d}-PART-{i}']=s
mapping={}
for id,(p,d) in records.items():
 slug=names.get(id)
 if not slug and d['type']=='plan':
  source=next((s.get('source',s.get('path','')) for s in d.get('sources',[]) if 'Plans/' in s.get('source',s.get('path',''))),'')
  slug=re.sub(r'^\d+_','',Path(source).stem);slug=re.sub(r'_execplan$','',slug).replace('_','-').lower()
 if not slug or not re.fullmatch('[a-z][a-z0-9.-]*',slug):raise ValueError((id,slug))
 old=d['path'];new=str(Path(old).with_name(id+'_'+slug+'.md')).replace('\\','/');mapping[old]=new;d['path']=new
 if d['type']=='plan':
  oldstatus=d['status'];d['status']={'草案':'准备','执行中':'执行','暂缓':'准备','待验收':'验收','已关闭':'关闭','已取消':'关闭'}.get(oldstatus,oldstatus)
  if oldstatus=='已取消':d['outcome']='取消';d['conclusion']=d.get('conclusion','历史计划已取消，由后续独立计划承接。')
  if oldstatus=='暂缓':d['executionNote']='历史暂缓，已转准备阶段；部分实现和未完成范围仍按 assessment 保留，不声明已重新执行。'
 if d['type']=='requirement':
  body=(ROOT/old).read_text(encoding='utf-8');history=d.pop('history',[]);d.pop('evolution',None)
  if history:
   body+='\n\n## 需求演进\n\n'
   for h in history:
    if isinstance(h,str):body+='### 旧资料迁移\n\n'+h+'\n\n';continue
    body+='### '+str(h.get('date',h.get('importedAt','旧资料迁移')))+'\n\n'
    for key,value in h.items():
     if key in ['date','importedAt']:continue
     label={'summary':'变动内容','decision':'原补充编号','description':'详细内容','change':'修订','version':'当时版本','source':'来源'}.get(key,key)
     body+=f'{label}：{value if isinstance(value,str) else json.dumps(value,ensure_ascii=False)}\n\n'
   (ROOT/old).write_text(body,encoding='utf-8')
 # 物理改名不重启已关闭计划。
 (ROOT/old).rename(ROOT/new);p.unlink();dump(new.replace('.md','.meta.json'),d)

# 重写每个 Markdown 当前有效相对链接，历史来源字符串不改写。
reverse={v:k for k,v in mapping.items()}
for p in docs.rglob('*.md'):
 rel=p.relative_to(ROOT).as_posix();origin=reverse.get(rel,rel);text=p.read_text(encoding='utf-8-sig')
 def link(m):
  href=m[2];url,sep,anchor=href.partition('#')
  if '://' in url or not url or url.startswith('#'):return m[0]
  absolute=Path(os.path.normpath(str(Path(origin).parent/url))).as_posix()
  dest=mapping.get(retired.get(absolute,absolute),absolute)
  return '['+m[1]+']('+Path(os.path.relpath(ROOT/dest,p.parent)).as_posix()+(sep+anchor if sep else '')+')' if absolute in mapping or absolute in retired else m[0]
 text=re.sub(r'\[([^\]]+)\]\(([^)]+)\)',link,text)
 for old,new in mapping.items():text=text.replace(old,new)
 for old,target in retired.items():text=text.replace(old,mapping[target])
 p.write_text(text,encoding='utf-8')
for id,(_,d) in records.items():
 for ref in d.get('requirementRefs',[]):
  if ref.get('path') in mapping:ref['path']=mapping[ref['path']]
 for ev in d.get('evidence',[]):
  if ev.get('path') in retired:ev['path']=mapping[retired[ev['path']]];ev['kind']=str(ev.get('kind',''))+'（原报告证据已归入计划）'
  elif ev.get('path') in mapping:ev['path']=mapping[ev['path']]
 dump(d['path'].replace('.md','.meta.json'),d)
newplan=records[plan][1]
for id in newplan['requirements']:
 d=records[id][1];newplan['requirementRefs'].append({k:d[k] for k in ['id','title','status','version','path']}|{'sections':['目标实现','技术方案','边界情况','验收条件','附录'],'hash':hashlib.sha256((ROOT/d['path']).read_bytes()).hexdigest()})
dump(newplan['path'].replace('.md','.meta.json'),newplan)
catalog=json.loads((docs/'catalog.json').read_text(encoding='utf-8-sig'));catalog['entries']=[{'id':id,'path':d['path'],'metadata':d['path'].replace('.md','.meta.json')} for id,(_,d) in records.items()];catalog['activePlan']=plan;dump('Docs/catalog.json',catalog)
# 当前入口必须同步，旧的一次性迁移脚本只在恢复包中有意义。
for rel in ['AGENTS.md','.agents/PLANS.md','README.md','PROJECT_AUDIT.md','RESUME_TECH_ANALYSIS.md','Tools/Karolina/README.md']:
 p=ROOT/rel
 if not p.exists():continue
 t=p.read_text(encoding='utf-8-sig')
 for old,new in mapping.items():
  t=t.replace(old,new).replace(Path(old).name,Path(new).name)
 for old,target in retired.items():t=t.replace(old,mapping[target]).replace(Path(old).name,Path(mapping[target]).name)
 p.write_text(t,encoding='utf-8')
dump('Tools/Karolina/maintenance/current-document-map.json',mapping)
print('当前索引',len(records),'需求',sum(d['type']=='requirement' for _,d in records.values()),'计划',sum(d['type']=='plan' for _,d in records.values()),'移除过时报告/说明',len(delete))
