"""本轮授权的界面/目录迁移，保留编号与原内容；不修改 Unity 文件。"""
from pathlib import Path
from datetime import datetime
import json,re,os
ROOT=Path(__file__).resolve().parents[3];DOCS=ROOT/'Docs';WEB=ROOT/'Tools/Karolina/Karolina.Desktop/Web'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,data):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
catalog=read(DOCS/'catalog.json');moves={};old_bodies={p.relative_to(ROOT).as_posix():p.read_text(encoding='utf-8-sig') for p in DOCS.rglob('*.md')}
for e in catalog['entries']:
    meta=read(ROOT/e['metadata']);old=e['path'];section=None
    if old.startswith('Docs/rules/engineering/'):
        section='templates' if e['id'] in ['RULE-003','RULE-004'] else 'execution';new='Docs/rules/'+section+'/'+Path(old).name
    elif old.startswith('Docs/engineering/') and old.endswith('.md'):
        section='index' if e['id']=='FACT-4E2A6CA25B98' else 'facts';new='Docs/rules/'+section+'/'+Path(old).name
    elif e['id']=='GUIDE-CONTENT':section='facts';new='Docs/rules/facts/'+Path(old).name
    elif old.startswith('Docs/resources/guides/'):new='Docs/tools/guides/'+Path(old).name
    else:continue
    moves[old]=new;moves[e['metadata']]=str(Path(new).with_suffix('.meta.json')).replace('\\','/')
    if section:meta['type']='rule';meta['section']=section;meta.pop('status',None)
    meta['path']=new;e['path']=new;e['metadata']=moves[e['metadata']]
    (ROOT/new).parent.mkdir(parents=True,exist_ok=True);(ROOT/old).replace(ROOT/new);write(ROOT/e['metadata'],meta)
    (ROOT/str(Path(old).with_suffix('.meta.json'))).unlink()

for old,text in old_bodies.items():
    target=moves.get(old,old);p=ROOT/target
    def link(m):
        href=m[2];bare=href.strip('<>').split('#')[0]
        if not bare or ':' in bare or bare.startswith('#'):return m[0]
        resolved=(ROOT/old).parent.joinpath(bare).resolve().relative_to(ROOT).as_posix() if (ROOT/old).parent.joinpath(bare).resolve().is_relative_to(ROOT) else ''
        actual=moves.get(resolved,resolved)
        if not actual:return m[0]
        rel=os.path.relpath(ROOT/actual,p.parent).replace('\\','/');anchor='#'+href.split('#',1)[1] if '#' in href else ''
        return '['+m[1]+']('+rel+anchor+')'
    text=re.sub(r'\[([^\]\n]+)\]\(([^)\n]+)\)',link,text)
    for before,after in sorted(moves.items(),key=lambda x:-len(x[0])):text=text.replace(before,after)
    p.write_text(text,encoding='utf-8')
for p in [ROOT/'AGENTS.md',ROOT/'.agents/PLANS.md',ROOT/'README.md']:
    text=p.read_text(encoding='utf-8-sig')
    for before,after in moves.items():text=text.replace(before,after)
    text=text.replace('Docs/resources/guides','Docs/tools/guides');p.write_text(text,encoding='utf-8')
for p in DOCS.rglob('*.meta.json'):
    meta=read(p)
    def remap(node):
        if isinstance(node,dict):
            for k,v in node.items():
                if k in ['path','metadata'] and isinstance(v,str):node[k]=moves.get(v,v)
                else:remap(v)
        elif isinstance(node,list):
            for v in node:remap(v)
    remap(meta);write(p,meta)

now=datetime.now().astimezone().isoformat()
def document(id,title,path,body,type='rule',section=None,requirements=None,code=None):
    (ROOT/path).parent.mkdir(parents=True,exist_ok=True);(ROOT/path).write_text('# '+title+'\n\n'+body.strip()+'\n',encoding='utf-8')
    meta={'id':id,'title':title,'type':type,'domain':'Karolina','path':path,'version':1,'updatedAt':now}
    if section:meta['section']=section
    if type=='requirement':meta['status']='激活'
    if type=='resource':meta['status']='当前资料'
    if type=='plan':
        meta.update(status='执行',code=code,requirements=requirements or [])
        meta['requirementRefs']=[]
        for req in requirements or []:
            entry=next(e for e in catalog['entries'] if e['id']==req);rm=read(ROOT/entry['metadata']);meta['requirementRefs'].append({'id':req,'title':rm['title'],'path':rm['path'],'version':rm['version'],'sections':['目标实现','技术方案','边界情况','附录']})
    metadata=str(Path(path).with_suffix('.meta.json')).replace('\\','/');write(ROOT/metadata,meta);catalog['entries'].append({'id':id,'path':path,'metadata':metadata})

document('REQ-KAR-007','工作模式、规则板块与拓展工具','Docs/requirements/karolina/REQ-KAR-007_workspace-modes-tools.md','''## 目标实现

AI 对话提供讨论需求、制定计划、执行任务三种并列模式。讨论/计划支持仅 Docs 的文档写入，执行任务才关联 Unity 变更审批。规则案固定工程事实、执行细则、项目索引、模板四板块，规则无状态；操作指南与可调用拓展工具独立展示，配套使用说明。

## 技术方案

ProjectContext 统一规范根路径与工程身份，工程内 .karolina/state 保存审批、聊天入口、页面位置和工具结果，由 Git 忽略；浏览器端口变化不影响存储。首次恢复复制本机旧状态，保留原文件和编号。工具声明在 Docs/tools/registry，说明在 Docs/tools/guides；Unity MCP 与外部程序通过 IExtensionToolRunner 接口接入，参数以 JSON 对象和原生进程参数列表传递。

## 边界情况

注册不执行，必须人工调用。外部程序使用当前 Windows 用户权限，声明只读不等于操作系统沙箱；由注册者说明真实写入范围。未知 Unity 操作按写入处理，必须关联计划/任务。调用共享工程锁，真实失败/取消/超时记录可见。Unity 构建标记的调用只发一次，用户登记构建结束前禁止后续 Unity 操作。任务审批不批准规则或文档草稿，也不替代真实玩法验收。

## 验收条件

完全退出重开仍能在任务审批看到原三狼条目及100文件；三种模式和写入范围在服务端生效；规则导航仅四板块；指南从工具页阅读；Unity/外部工具能注册并人工调用，记录真实结果，写入进入任务审批。

## 附录

本轮审查七原则：高内聚低耦合、关注点分离、依赖倒置、清晰边界、可测试可观测、简单实用、演进式设计。当前架构问题和剩余改进记录在 Karolina 架构审查事实中。''',type='requirement')
document('PLAN-KAR-006','持久工作台、对话模式与拓展工具改造','Docs/plans/karolina/PLAN-KAR-006_workspace-modes-tools.md','''## 参考需求

- [工作模式、规则板块与拓展工具](../../requirements/karolina/REQ-KAR-007_workspace-modes-tools.md)：全部章节。
- [任务变更审批与反馈再执行](../../requirements/karolina/REQ-KAR-006_task-change-approval.md)：真实基线与审批边界。

## 实施细节

统一工程持久身份，恢复原审批到工程内被忽略状态目录；不重新生成三狼基线。WorkspaceViewStore 保存页面/条目/模式，启动加载审批再恢复页面。WorkflowModes 定义对话目的，文档模式 cwd/可写根为 Docs，执行模式才创建审批。规则按 section 固定路由，指南关联工具。工具目录/执行适配器/结果存储独立于 HTTP 接线；共享工作区锁与停止信号，写入工具复用真实任务快照和审批。

## 执行步骤

- [x] 准备：核对原记录、实际原生进程 API 与目录身份，完成七原则只读审查。
- [x] 执行：按上述范围改造；用户原工程内容不改写。
- [ ] 测试：本轮用户没有请求新增行为测试；仅编译与只读诊断，验收设计不冒充通过。
- [ ] 校正：根据独立只读审查修复明确问题。
- [ ] 验收：用户实际重启与模式/工具调用。
- [ ] 关闭：人工确认后关闭。

## Agent 测试与验收设计

后续获测试请求后：临时工程验证尾斜杠身份、旧记录恢复、完全退出重开条目/意见保持；协议夹具验证文档模式 cwd/可写根与执行模式审批；目录检查四板块和指南路由；临时命令/Unity夹具验证原生参数、失败/超时/取消、写入基线和构建闸门。禁止截图，不调用真实生产构建或新增Unity修改。

## 进度与结果

三狼原本地记录仍是待审批100文件；原生进程API曾返回空列表并找不到同名本机文件，而命令环境可读取，具体环境隔离机制未知。迁移到工程状态目录以统一两边可见内容。已确定尾斜杠身份分裂与事后任务提示误称开始基线的问题，已修正。最终真实编译/只读诊断与独立结论在本计划追加。''',type='plan',requirements=['REQ-KAR-007','REQ-KAR-006'],code='0171')
catalog['activePlan']='PLAN-KAR-006'
document('TOOL-GUIDE-UNITY-INSPECT','Unity工程只读检查使用说明','Docs/tools/guides/TOOL-GUIDE-UNITY-INSPECT_usage.md','''## 用途

读取当前已核对 Unity 工程的 Editor 状态或 Console 错误，不清日志、不切换运行、不保存场景。

## 使用

在 AI 对话页连接 Unity，再在拓展工具页选择 Editor状态或Console错误。参数是 MCP 参数 JSON；状态通常使用空对象，Console默认 maxEntries=100、logTypeFilter=Error、includeStackTrace=false。点击调用后查看真实输出和运行记录。

## 失败与边界

未连接、工程不符或MCP失败会显示实际错误。无错误日志不等于玩法通过；不能作为测试验收。外部工具声明使用 unity-mcp、明确 toolName 与 defaultArguments，配套说明必须描述副作用；未知Unity工具需执行任务计划。''',type='resource')
document('TOOL-GUIDE-DOTNET-INFO','.NET环境查看使用说明','Docs/tools/guides/TOOL-GUIDE-DOTNET-INFO_usage.md','''## 用途与入口

执行已安装 dotnet 的 --info，读取本机 SDK/运行时信息，不安装依赖、不编译工程。

## 使用

在拓展工具页选.NET环境，参数为空对象，点击调用。成功输出来自真实标准输出；失败显示退出码和标准错误。

## 自定义外部工具

注册 external 类型，填写 executable、arguments 字符串数组、defaultArguments 和配套说明。参数可用 {name} 占位，值独立传入原生 ArgumentList，不拼接 shell 命令。调用以当前Windows用户运行，readOnly是注册者的副作用声明，不是操作系统沙箱；写入工具应声明为非只读并选择执行任务计划。''',type='resource')
registry=DOCS/'tools/registry';registry.mkdir(parents=True,exist_ok=True)
for e in catalog['entries']:
    if e['path'].startswith('Docs/tools/guides/GUIDE-'):
        meta=read(ROOT/e['metadata']);write(registry/(e['id'].lower()+'.tool.json'),{'id':e['id'].lower(),'title':meta['title'],'description':'项目操作指南；阅读后在说明所述入口操作，尚未登记自动执行命令。','kind':'manual','guideId':e['id'],'defaultArguments':{},'readOnly':True})
for id,title,name,args in [('unity-editor-state','Unity Editor状态','editor-application-get-state',{}),('unity-console-errors','Unity Console错误','console-get-logs',{'maxEntries':100,'logTypeFilter':'Error','includeStackTrace':False})]:
    write(registry/(id+'.tool.json'),{'id':id,'title':title,'description':'读取已连接并核对的当前Unity工程。','kind':'unity-mcp','guideId':'TOOL-GUIDE-UNITY-INSPECT','toolName':name,'defaultArguments':args,'readOnly':True,'timeoutSeconds':60})
write(registry/'dotnet-info.tool.json',{'id':'dotnet-info','title':'.NET环境','description':'显示本机SDK与运行时，不安装或修改工程。','kind':'external','guideId':'TOOL-GUIDE-DOTNET-INFO','executable':'dotnet','arguments':['--info'],'defaultArguments':{},'readOnly':True,'timeoutSeconds':60})
write(DOCS/'catalog.json',catalog)
ignore=ROOT/'.gitignore';text=ignore.read_text(encoding='utf-8-sig');
if '\n/.karolina/' not in text:text+='\n# Karolina per-project local state\n/.karolina/\n'
ignore.write_text(text,encoding='utf-8')

index=WEB/'index.html';text=index.read_text(encoding='utf-8-sig')
text=text.replace('<script src="/review.js" defer></script>','<script src="/review.js" defer></script><script src="/workspace.js" defer></script><script src="/tools.js" defer></script>')
text=text.replace('<div class="rail-spacer">','<button data-page="tools" class="nav" title="拓展工具" aria-label="拓展工具">⌘</button><div class="rail-spacer">')
text=text.replace('<label>执行计划<select id="executionPlan">','<label id="executionPlanRow">执行任务计划<select id="executionPlan">')
text=text.replace('<div class="composer-tools">','<div class="workflow-modes"><button data-workflow-mode="discuss-requirement" class="selected">讨论需求</button><button data-workflow-mode="formulate-plan">制定计划</button><button data-workflow-mode="execute-task">执行任务</button></div><small id="workflowHint" class="workflow-hint"></small><div class="composer-tools">')
text=text.replace('</main>','''<section id="toolsPage" class="page tools-page" hidden><div class="tools-toolbar"><button id="registerTool">注册工具</button><button id="refreshTools">刷新工具</button><button id="buildFinished" class="quiet">登记Unity构建结束</button><span id="toolKind"></span></div><div id="toolEmpty" class="empty">选择拓展工具阅读说明或调用。</div><div id="toolContent" hidden><p id="toolDescription"></p><div id="toolInvocation"><label>参数（JSON）<textarea id="toolArguments" rows="5"></textarea></label><div id="toolPlanRow"><label>执行任务计划<select id="toolPlan"></select></label><label>继续原任务<select id="toolTask"></select></label></div><button id="toolRun" class="primary">调用工具</button><button id="toolStop" disabled>停止</button></div><pre id="toolOutput"></pre><h2>使用说明</h2><article id="toolManual" class="markdown"></article></div></section></main>''')
index.write_text(text,encoding='utf-8')
app=WEB/'app.js';text=app.read_text(encoding='utf-8-sig')
text=text.replace("if(page==='review') {renderReviewDirectory(query);return;}","if(page==='review') {renderReviewDirectory(query);return;}\n  if(page==='rule'){renderRuleDirectory(query);return;}\n  if(page==='tools'){renderToolDirectory(query);return;}")
text=text.replace("review:'任务审批'","review:'任务审批',tools:'拓展工具'")
text=text.replace("$('reviewPage').hidden=next!=='review';","$('reviewPage').hidden=next!=='review';$('toolsPage').hidden=next!=='tools';rememberWorkspace();")
text=text.replace("if(next==='review')refreshReviews()","if(next==='tools')refreshTools().catch(e=>toast(e.message));\n  if(next==='review')refreshReviews()")
text=text.replace("$('documentToc').innerHTML=", "rememberWorkspace();$('documentToc').innerHTML=")
text=text.replace("const request=++documentRequest;\n  try {", "const request=++documentRequest;\n  try {\n    const guideTool=extensionTools.find(t=>t.definition.guideId===id);if(guideTool){navigate('tools');await openTool(guideTool);return;}")
text=text.replace("planId:$('executionPlan').value||null","planId:workflowMode==='execute-task'?$('executionPlan').value||null:null,mode:workflowMode")
text=text.replace("await refreshState();const diagnostic=", "await refreshState();await restoreWorkspace();const diagnostic=")
text=text.replace("${type==='plan'?`<details open>","${type==='rule'?`<label>规则板块<select id=\"newRuleSection\">${ruleSections.map(([id,title])=>`<option value=\"${id}\">${title}</option>`).join('')}</select></label>`:''}${type==='plan'?`<details open>")
text=text.replace("englishDomain:$('newEnglishDomain').value,requirements:","englishDomain:$('newEnglishDomain').value,ruleSection:type==='rule'?$('newRuleSection').value:null,requirements:")
app.write_text(text,encoding='utf-8')
workbench=ROOT/'Tools/Karolina/Karolina.Desktop/Workbench.cs';text=workbench.read_text(encoding='utf-8-sig').replace('windowReady = MinimizeWindow != null, busy, activeThread','windowReady = MinimizeWindow != null, busy=IsBusy, toolBusy=toolCancellation!=null, activeThread');workbench.write_text(text,encoding='utf-8')
css=WEB/'style.css';css.write_text(css.read_text(encoding='utf-8-sig')+'\n.workflow-modes{display:flex;gap:6px;padding:0 15px 8px}.workflow-modes button{background:transparent;font-size:12px}.workflow-modes .selected{background:var(--accent-soft);color:var(--accent)}.workflow-hint{display:block;padding:0 16px 10px;color:var(--muted);font-size:11px}.tools-page{overflow:auto;padding:24px 30px}.tools-toolbar{display:flex;gap:8px;align-items:center;margin-bottom:20px}#toolInvocation label{display:flex;flex-direction:column;gap:6px;margin:12px 0}#toolPlanRow{display:flex;gap:12px}#toolArguments{font-family:Consolas,monospace}#toolOutput{white-space:pre-wrap;overflow-wrap:anywhere;max-height:350px;overflow:auto;border:1px solid var(--line);padding:14px;border-radius:8px}#toolManual{padding:10px 0;overflow:visible;max-width:980px}\n',encoding='utf-8')
print('Migrated fixed rule sections and guides; created workflow/tools requirement and plan; front-end modules wired.')
