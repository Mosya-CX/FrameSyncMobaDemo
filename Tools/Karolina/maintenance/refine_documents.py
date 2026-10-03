"""进一步按功能拆分过长附录，补齐历史计划的具体类型、参数和测试入口。"""
from pathlib import Path
import json,re,hashlib
ROOT=Path(__file__).resolve().parents[3]
if not (ROOT/"Docs/Requirements/catalog.json").exists():raise RuntimeError("一次性迁移工具：旧树已退役，请在恢复快照的隔离副本中使用，不能重跑当前资料库。")
def write(p,text): (ROOT/p).write_text(text,encoding='utf-8')
catalog=json.loads((ROOT/'Docs/catalog.json').read_text(encoding='utf-8'))
specs={
'控制实例与模块参数':[('控制处理器装配与生命周期',None),('控制实例增删与信号查询','Add'),('控制定义Bake与模块执行','只保留一套 Definition'),('控制实例身份与参数块','Instance 的最小字段'),('标准控制效果与复合实例','Stun'),('控制数字规则与快照恢复','Inspector 精度边界')],
'同步生成死亡复活与回池':[('同步单位生成与AI注册',None),('正式死亡清理与处置','致死、死亡回调与死亡清理'),('英雄复活与对象池新生命周期','英雄死亡与复活'),('生命周期稳定顺序与恢复','生命周期清理顺序'),('开局生成与事件接入流程','开局初始化')],
'单位根与能力装配':[('单位身份与根状态',None),('单位能力状态与战斗修正','默认动作能力由 Handler 决定'),('Handler装配与单向依赖','Handler 总体结构'),('单位移动与空间写入接缝','MovementHandler')],
'技能信号与会话状态':[('技能信号接受与施法结束',None),('技能本地指示器查询','AbilityIndicatorController：本地指示器如何接入'),('技能界面查询与单位事件','AbilityCastView：外部系统读取当前施法状态'),('技能目录运行时与会话归属','二、AbilityRuntime 与 AbilitySession：技能运行状态'),('技能状态快照与统一Tick','AbilityHandlerSnapshot 与 IRollback')],
'提交运动寿命与回收':[('投射物状态与空间归属',None),('投射物延迟提交与查询','Spawn 输入'),('投射物运动命中与寿命顺序','固定 Tick 边界'),('投射物实体池与回收','为什么实体池返回 `PhysicsEntity2D`'),('投射物快照恢复与稳定遍历','字段标记'),('投射物跨模块接缝与主流程','与物理系统的边界')],
'普通移动冲刺与强制位移':[('移动职责与提交接口',None),('移动运行数据与普通路线','运行数据'),('冲刺控制位移与传送','Dash'),('移动子管线与双网格顺序','移动子管线推荐顺序'),('移动状态快照与重建','恢复后的移动系统重建需求'),('移动改造实施顺序与帧算法','阶段一：冻结职责与接口')]
}
for entry in list(catalog['entries']):
    meta=json.loads((ROOT/entry['metadata']).read_text(encoding='utf-8'))
    if meta['type']!='requirement' or meta['title'] not in specs:continue
    text=(ROOT/entry['path']).read_text(encoding='utf-8');cut=text.index('## 附录：精确接口、公式与配置');main=text[:cut]
    appendix=text[cut:];heads=list(re.finditer(r'(?m)^### (.+)$',appendix));parts=[];groups=specs[meta['title']]
    positions=[0]+[next(h.start() for h in heads if h[1]==anchor) for _,anchor in groups[1:]]+[len(appendix)]
    for i,(title,_) in enumerate(groups):
        path=str(Path(entry['path']).parent.as_posix()+'/'+title+'.md');id=meta['id']+'-PART-'+str(i+1)
        clauses=appendix[positions[i]:positions[i+1]].replace('## 附录：精确接口、公式与配置','## 附录：精确接口、公式与配置')
        source=main[main.index('## 目标实现'):main.index('## 工程实际核查')]
        body=f'# {title}\n\n## 本功能范围\n\n本案细化“{meta["title"]}”中的{title}，仅覆盖下列明确接口与边界。\n\n'+source+'## 工程实际核查\n\n实现证据与已有测试位置关联总案；字段存在不能认定行为已验收。\n\n'+clauses
        write(path,body);child=dict(meta,id=id,title=title,path=path,parent=meta['id'],related=[meta['id']],history=meta.get('history',[])+[dict(date='2026-10-02',summary='按具体功能进一步拆分过长附录')]);mp=path[:-3]+'.meta.json';write(mp,json.dumps(child,ensure_ascii=False,indent=2));catalog['entries'].append(dict(id=id,path=path,metadata=mp));parts.append((title,path,id))
    write(entry['path'],main+'## 细化功能与技术附录\n\n'+ '\n'.join(f'- [{title}]({Path(path).name})：接口、数据流、公式、边界与配置。' for title,path,_ in parts)+'\n')
    meta['related']=[id for _,_,id in parts];meta['history']=meta.get('history',[])+[dict(date='2026-10-02',summary='总案保留目标和事实，精确细节拆为独立功能')];write(entry['metadata'],json.dumps(meta,ensure_ascii=False,indent=2))
# 金币的明确数值与公式不应被抽象描述掩盖；只保留真正未决的生产入口。
gold=next(e for e in catalog['entries'] if '死亡奖励整数分配' in e['path']) if any('死亡奖励整数分配' in e['path'] for e in catalog['entries']) else next(e for e in catalog['entries'] if e['path'].startswith('Docs/需求/') and '奖励' in e['path'])
write(gold['path'],(ROOT/gold['path']).read_text(encoding='utf-8')+'''\n## 附录：已接受的金币数值与整数分配

正式初始金币 1500；HeroTest 测试金币 10000。近战兵击杀奖励 21，远程兵 14，英雄基础击杀奖励 300。

英雄奖励先给击杀者 floor(300×3/5)=180，余下 120 由有效助攻者分配，整数余数按稳定身份顺序处理；没有有效助攻者时击杀者得到 300。小兵金币只给击杀者，经验分配另遵经验需求。

分配生产入口的旧合同与源码不一致，见待确认清单；不能因为本附录有明确数值就擅改该 owner。\n''')
# 每个历史来源保留其类型、测试、接口代码和数值片段，用原标识而非泛泛实施模板。
for entry in catalog['entries']:
    if not entry['id'].startswith('PLAN-FEAT-'):continue
    meta=json.loads((ROOT/entry['metadata']).read_text(encoding='utf-8'));rows=[];artifacts=[]
    for source in meta.get('sources',[]):
        path=ROOT/source['source'];old=path.read_text(encoding='utf-8-sig');batch=re.search(r'\d{4}',path.name)[0]
        symbols=list(dict.fromkeys(re.findall(r'`([A-Za-z_][A-Za-z0-9_.]*(?:\([^`\n]*\))?)`',old)))
        tests=list(dict.fromkeys(re.findall(r'\b([A-Za-z_]\w*(?:Tests|_\w+))\b',old)))
        code=[block for language,block in re.findall(r'```(\w*)\n(.*?)```',old,re.S) if language.lower() in ['csharp','cs','json','lua']]
        unchecked=len(re.findall(r'(?m)^\s*- \[ \]',old));reported=source['status'];scope='、'.join('`'+x+'`' for x in symbols[:20]) or '该批次没有明确类型标识，范围需人工确认'
        rows.append(f'### 历史批次 {batch}\n\n- 当期记录：{reported}；这不代表本轮重跑通过。\n- 具体接口或数据对象：{scope}。\n- 原未勾选进度项：{unchecked} 项；'+('该批次当前完成情况列入待确认，不能删除未完成含义。' if unchecked else '未发现未勾选条目。')+'\n- 测试入口：'+('、'.join('`'+x+'`' for x in tests[:15]) or '原计划没有可直接提取的测试函数，需要结合关联需求补充。')+'。\n')
        if code:rows.append('原实施参数和接口代码（按本案关联需求的现行修订复核后使用）：\n\n'+'\n\n'.join('```'+('json' if block.strip().startswith('{') else 'csharp')+'\n'+block.strip()+'\n```' for block in code[:5]))
        artifacts.append(dict(batch=batch,source=source['source'],interfaces=symbols,testEntrypoints=tests,uncheckedProgressCount=unchecked))
    write(entry['path'],(ROOT/entry['path']).read_text(encoding='utf-8')+'\n## 历史实施细节与逐批次核查\n\n'+'\n\n'.join(rows));meta['legacyImplementationArtifacts']=artifacts;write(entry['metadata'],json.dumps(meta,ensure_ascii=False,indent=2))
write('Docs/catalog.json',json.dumps(catalog,ensure_ascii=False,indent=2))
print('Refined Chinese document entries:',len(catalog['entries']))
