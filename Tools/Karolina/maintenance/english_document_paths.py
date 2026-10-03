"""当前资料的分类、模块和机器事实文件统一英文路径。"""
from pathlib import Path
from revise_current import ROOT,write,dump
import json,os,re
docs=ROOT/'Docs'
components={'需求':'requirements','计划':'plans','规则':'rules','工程':'engineering','资源':'resources','工程规则':'engineering','操作指南':'guides','比赛流程':'match-flow','表现界面':'presentation-ui','单位':'units','单位数值':'unit-stats','非英雄':'non-heroes','技能':'abilities','具体内容':'game-content','空间物理':'spatial-physics','控制':'crowd-control','配置资源':'configuration-content','普通攻击':'basic-attacks','启动器':'launchers','确定性基础':'determinism','投射物':'projectiles','玩家输入':'player-input','寻路移动':'pathfinding-movement','增益':'buffs','战斗':'combat','帧同步':'frame-sync','装备商店':'equipment-shop','Karolina':'karolina','程序集依赖.json':'assembly-dependencies.json','迁移覆盖清单.json':'migration-coverage.json'}
mapping={}
for p in docs.rglob('*'):
 if p.is_file():
  old=p.relative_to(ROOT).as_posix();new='/'.join(components.get(s,s) for s in old.split('/'));mapping[old]=new
  if new!=old:
   dest=ROOT/new;dest.parent.mkdir(parents=True,exist_ok=True);p.rename(dest)
reverse={v:k for k,v in mapping.items()}
for p in docs.rglob('*.md'):
 origin=reverse[p.relative_to(ROOT).as_posix()];t=p.read_text(encoding='utf-8-sig')
 def link(m):
  url,sep,anchor=m[2].partition('#')
  if '://' in url or not url:return m[0]
  target=Path(os.path.normpath(str(Path(origin).parent/url))).as_posix()
  if target not in mapping:return m[0]
  return '['+m[1]+']('+Path(os.path.relpath(ROOT/mapping[target],p.parent)).as_posix()+(sep+anchor if sep else '')+')'
 t=re.sub(r'\[([^\]]+)\]\(([^)]+)\)',link,t)
 for old,new in sorted(mapping.items(),key=lambda pair:len(pair[0]),reverse=True):t=t.replace(old,new)
 t=t.replace('Docs/需求','Docs/requirements').replace('Docs/计划','Docs/plans').replace('Docs/规则','Docs/rules').replace('Docs/工程','Docs/engineering').replace('Docs/资源','Docs/resources')
 t=t.replace('目录和标题仍中文','显示模块和标题仍中文；物理目录全部英文').replace('目录模块和 UI 标题使用中文','UI 模块和标题使用中文；物理分类、模块目录及 JSON 文件名均为英文')
 p.write_text(t,encoding='utf-8')
def values(obj):
 if isinstance(obj,dict):return {k:values(v) if k not in ['sources','provenance'] else v for k,v in obj.items()}
 if isinstance(obj,list):return [values(v) for v in obj]
 if isinstance(obj,str):
  for old,new in mapping.items():obj=obj.replace(old,new)
 return obj
for p in docs.rglob('*.json'):
 d=json.loads(p.read_text(encoding='utf-8-sig'))
 # 覆盖清单的历史来源仍原样，只更新当前 target/路径。
 p.write_text(json.dumps(values(d),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for p in sorted(docs.rglob('*'),key=lambda p:len(p.parts),reverse=True):
 if p.is_dir() and not any(p.iterdir()):p.rmdir()
for rel in ['AGENTS.md','.agents/PLANS.md','README.md','PROJECT_AUDIT.md','RESUME_TECH_ANALYSIS.md','Tools/Karolina/README.md']:
 p=ROOT/rel
 if not p.exists():continue
 t=p.read_text(encoding='utf-8-sig')
 for old,new in sorted(mapping.items(),key=lambda pair:len(pair[0]),reverse=True):t=t.replace(old,new)
 for old,new in components.items():
  if old.endswith('.json'):t=t.replace(old,new)
  else:t=t.replace('Docs/'+old,'Docs/'+new)
 t=t.replace('Docs/rules/工程规则','Docs/rules/engineering').replace('Docs/resources/操作指南','Docs/resources/guides')
 p.write_text(t,encoding='utf-8')
p=ROOT/'Tools/Karolina/Karolina.Desktop/Web/app.js';t=p.read_text(encoding='utf-8').replace('<label>模块<input id="newDomain"','<label>英文模块目录<input id="newEnglishDomain" placeholder="例如 combat；留空使用 custom"></label><label>模块显示名<input id="newDomain"').replace("englishTitle:$('newEnglishTitle').value","englishTitle:$('newEnglishTitle').value,englishDomain:$('newEnglishDomain').value");p.write_text(t,encoding='utf-8')
for id,(_,d) in []:pass
dump('Tools/Karolina/maintenance/current-directory-map.json',mapping)
print('Docs 所有路径 ASCII',all(p.relative_to(docs).as_posix().isascii() for p in docs.rglob('*')))
