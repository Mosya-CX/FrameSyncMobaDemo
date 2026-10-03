"""只读核验当前资料路径/关联和已有 Unity 文件保护快照。"""
from pathlib import Path
import os,json,re,hashlib
root=Path(__file__).resolve().parents[3];docs=root/'Docs';c=json.loads((docs/'catalog.json').read_text(encoding='utf-8'));problems=[]
for e in c['entries']:
 p=root/e['path'];m=root/e['metadata'];d=json.loads(m.read_text(encoding='utf-8'))
 if not p.exists() or d['path']!=e['path'] or p.with_suffix('.meta.json')!=m:problems.append(('identity',e['id']))
 if d['type']=='requirement' and ('history' in d or 'evolution' in d):problems.append(('meta history',e['id']))
 for ref in d.get('requirementRefs',[]):
  if not (root/ref['path']).exists():problems.append(('reference',e['id'],ref['path']))
for p in [*docs.rglob('*.md'),root/'README.md',root/'AGENTS.md',root/'.agents/PLANS.md']:
 text=re.sub(r'(?ms)^```.*?^```[^\n]*','',p.read_text(encoding='utf-8'))
 for title,target in re.findall(r'\[([^\]\n]+)\]\(([^)\n]+)\)',text):
  path=target.split('#')[0].strip('<>')
  if not path or ':' in path or path.startswith(('#','//','\\')):continue
  if not (p.parent/path).exists():problems.append(('link',p.relative_to(root).as_posix(),target))
ascii_paths=all(p.relative_to(docs).as_posix().isascii() for p in docs.rglob('*'))
protected=json.loads((Path(os.environ['LOCALAPPDATA'])/'Karolina/recovery/20261002-document-lifecycle/protected.json').read_text(encoding='utf-8'));changed=[]
for path,h in protected.items():
 p=root/path
 if not p.exists() or hashlib.sha256(p.read_bytes()).hexdigest()!=h:changed.append(path)
report=dict(documents=len(c['entries']),asciiPaths=ascii_paths,problems=problems,protectedFiles=len(protected),changedProtectedFiles=changed,noScreenshots=True)
out=Path(os.environ['LOCALAPPDATA'])/'Karolina/revision-inventory/current-documents.json';out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report,ensure_ascii=False),flush=True)
if problems or changed or not ascii_paths:raise SystemExit(1)
