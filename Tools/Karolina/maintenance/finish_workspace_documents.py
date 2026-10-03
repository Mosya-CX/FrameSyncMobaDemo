"""整理本轮文档路由和事实，不修改 Unity 内容或审批结论。"""
from pathlib import Path
import json, hashlib
from datetime import datetime

root=Path(__file__).resolve().parents[3]
now=datetime.now().astimezone().isoformat()
def read(path):return json.loads((root/path).read_text(encoding='utf-8-sig'))
def save(path,data):(root/path).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
relocations={
    'Docs/engineering/assembly-dependencies.json':'Docs/rules/index/assembly-dependencies.json',
    'Docs/engineering/migration-coverage.json':'Docs/rules/facts/migration-coverage.json',
}
for old,new in relocations.items():
    origin=root/old;target=root/new
    if origin.exists():
        if target.exists():raise RuntimeError('目标已存在，原文件保留：'+new)
        origin.rename(target)
for p in [*root.joinpath('Docs').rglob('*.md'),*root.joinpath('Docs').rglob('*.json'),root/'README.md',root/'AGENTS.md',root/'.agents/PLANS.md']:
    raw=p.read_bytes();text=raw.decode('utf-8-sig');updated=text
    for old,new in relocations.items():updated=updated.replace(old,new)
    if updated!=text:p.write_bytes(updated.encode('utf-8'))

fact='Docs/rules/facts/FACT-KAR-001_karolina-architecture-review.md'
save(fact[:-3]+'.meta.json',dict(id='FACT-KAR-001',title='Karolina架构审查',type='rule',domain='工程事实',section='facts',path=fact,version=1,updatedAt=now))
catalog=read('Docs/catalog.json')
if not any(e['id']=='FACT-KAR-001' for e in catalog['entries']):
    catalog['entries'].append(dict(id='FACT-KAR-001',path=fact,metadata=fact[:-3]+'.meta.json'))
catalog['activePlan']='PLAN-KAR-006'
save('Docs/catalog.json',catalog)

req='Docs/requirements/karolina/REQ-KAR-006_task-change-approval.meta.json'
meta=read(req);meta.update(version=3,updatedAt=now);save(req,meta)
reqbody=root/meta['path'];text=reqbody.read_text(encoding='utf-8')
heading='### 默认隐藏 Unity 元数据文件'
if heading not in text:
    reqbody.write_text(text+'\n'+heading+'\n\n用户希望审批页面自动去掉 .meta 列表噪声。页面默认隐藏这类文件，提供显示开关并保存偏好；切换不删除文件、记录或意见。整体审批仍对应完整任务文件集合，按钮和确认内容明确包含隐藏 .meta 数量。单文件审批只作用当前文件，隐藏文件不被自动单独批准；隐藏状态下的驳回仍阻止整体通过。\n',encoding='utf-8')

planpath='Docs/plans/karolina/PLAN-KAR-006_workspace-modes-tools.meta.json'
plan=read(planpath);plan.update(status='测试',version=2,updatedAt=now)
for ref in plan['requirementRefs']:
    entry=next(e for e in catalog['entries'] if e['id']==ref['id']);reqmeta=read(entry['metadata'])
    ref.update(title=reqmeta['title'],status=reqmeta['status'],version=reqmeta['version'],path=entry['path'],hash=hashlib.sha256((root/entry['path']).read_bytes()).hexdigest())
plan['verification']=dict(build='Release 0错误0警告',javascript='4个前端脚本语法检查通过',nativeReview='真实原生API读到原三狼待审批条目、修订2、100文件',behavior='本轮未新增或运行行为测试；模式、工具实际调用与人工验收待完成',screenshots=False)
save(planpath,plan)
planbody=root/plan['path'];text=planbody.read_text(encoding='utf-8')
text=text.replace('- [ ] 校正：根据独立只读审查修复明确问题。','- [x] 校正：修复独立只读审查的状态迁移、执行取消、构建/连接闸门和工具注册数据保护问题。')
section='## 本轮实际结果'
if section not in text:
    text+='\n'+section+'\n\n- 编译：current 原生 Release 0错误0警告；四个 JS 语法检查通过。\n- 原记录：原生API读取三狼待审批100文件，修订2，编号保持；没有重建或批准任务。\n- 持久机制：状态、页面位置/选中项/模式与共享锁在工程内 .karolina/state；旧EFS内容读后重写，原文件保留。\n- 规则物理/UI目录固定四板块；指南移到工具页面，9项声明中6项为只读指南入口，3项配置了真实调用入口。指南入口不冒充已经接线的可执行工具。\n- 用户增补：审批默认隐藏.meta，显示开关与偏好保存；完整快照和审批范围保留，整体批准明确包含隐藏数量。\n- 架构结论：部分符合七原则，Workbench与前端全局状态仍待按用例拆分，见[架构审查](../../rules/facts/FACT-KAR-001_karolina-architecture-review.md)。\n- 证据边界：本轮无行为fixture、无真实Codex任务发送、无主动Unity修改/构建或截图验收；不能把此前31项审批检查当新增模式/工具通过。\n'
planbody.write_text(text,encoding='utf-8')

statepath=root/'Docs/rules/facts/FACT-82B7CF40B5E6_current-state.md'
state=statepath.read_text(encoding='utf-8')
start=state.index('## 当前任务');end=state.index('## 既有未完成工作')
state=state[:start]+'''## 当前任务

[持久工作台、对话模式与拓展工具改造](../../plans/karolina/PLAN-KAR-006_workspace-modes-tools.md)进入测试阶段：三种对话模式、审批持久状态、规则四板块及拓展工具入口已落地。架构七原则[审查结论](FACT-KAR-001_karolina-architecture-review.md)为部分符合；应用服务与前端状态边界仍需继续演进。

本轮真实 current Release 编译0错误0警告，四个JS语法检查通过。真实原生API读到原三狼条目：待审批100文件、修订2；原文件有EFS属性，已读取内容重写到工程 .karolina/state，不继承加密属性。具体命令环境与GUI的旧路径可见性机制仍未知。审批默认隐藏.meta，可显示检查，完整任务范围不变。

本轮没有新增/运行行为fixture，也未发送真实Codex任务、主动修改Unity内容、请求生产构建或截图验收。前轮13核心+18HTTP/DOM通过仅适用于旧审批机制；新增模式/工具/退出恢复仍待对应行为及人工验收。PLAN-KAR-005保持测试、未关闭；新范围由PLAN-KAR-006承接。

'''+state[end:]
statepath.write_text(state,encoding='utf-8')
statemeta=read('Docs/rules/facts/FACT-82B7CF40B5E6_current-state.meta.json');statemeta.update(version=5,updatedAt=now);save('Docs/rules/facts/FACT-82B7CF40B5E6_current-state.meta.json',statemeta)
readme=root/'Docs/README.md';text=readme.read_text(encoding='utf-8');count=sum(read(e['metadata'])['type']=='plan' for e in catalog['entries'])
import re
text=re.sub(r'共 \d+ 个独立执行计划（含新增任务审批计划）',f'共 {count} 个独立执行计划（含Karolina新增计划）',text)
readme.write_text(text,encoding='utf-8')
print(json.dumps(dict(documents=len(catalog['entries']),plans=count,activePlan=catalog['activePlan']),ensure_ascii=False))
