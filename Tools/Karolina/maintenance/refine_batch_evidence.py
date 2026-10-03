"""按原执行文件里的确切文件/类型匹配当前源码，修正宽泛字面匹配的错模块证据。"""
from pathlib import Path
import re,json,os,zipfile,hashlib,collections
ROOT=Path(__file__).resolve().parents[3]
def read(p):
    try:return p.read_text(encoding='utf-8-sig')
    except UnicodeDecodeError:return p.read_text(encoding='gb18030')
def save(p,m):p.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def fragment(text,name=None,limit=70):
    lines=text.splitlines();start=0
    if name:
        # 从声明开始，不从更早的调用点开始。
        start=next((i for i,l in enumerate(lines) if re.search(r'^\s*(?:public|private|internal|protected).*\b'+re.escape(name)+r'\s*\(',l)),0)
    out=[];depth=0;begun=False
    for line in lines[start:start+limit]:
        out.append(line);depth+=line.count('{')-line.count('}')
        if '{' in line:begun=True
        if begun and depth==0:break
    if begun and depth>0:out.append('// 方法后续请阅读上述真实源码；这里是节选。')
    return '\n'.join(out)
files=list((ROOT/'Assets/Scripts').rglob('*.cs'))+[p for p in (ROOT/'Tools').rglob('*.cs') if 'Karolina' not in p.parts and 'obj' not in p.parts and 'bin' not in p.parts]
code={p.relative_to(ROOT).as_posix():read(p) for p in files}
archive=zipfile.ZipFile(Path(os.environ['LOCALAPPDATA'])/'Karolina/recovery/20261002-document-revision/Docs.zip')
coverage=json.loads(read(ROOT/'Docs/工程/迁移覆盖清单.json'))
byid={m['id']:(p,m) for p in (ROOT/'Docs/计划').rglob('*.meta.json') if (m:=json.loads(read(p)))}
rows=[]
for source in coverage['plans']:
    ident=source.get('newPlan')
    if not ident or ident=='PLAN-0000' or ident in ['PLAN-CONTENT-138','PLAN-WOLF-165','PLAN-KAR-001']:continue
    p,m=byid[ident];entry=source.get('recoveryEntry') or m.get('sources',[{}])[0].get('recoveryEntry') or next((e for e in archive.namelist() if e.endswith('/'+Path(source['source']).name)),None)
    if not entry:continue
    original=archive.read(entry).decode('utf-8-sig').replace('\r','')
    mentioned=set(re.findall(r'\b([A-Z][A-Za-z0-9_]*)\.cs\b',original))
    def score(path):
        stem=Path(path).stem;count=len(re.findall(r'\b'+re.escape(stem)+r'\b',original))
        return (10000 if stem in mentioned else 0)+count*20 if count else 0
    matched=sorted([path for path in code if score(path)>0],key=lambda path:(-score(path),path))
    prod=[path for path in matched if '/Tests/' not in path and not Path(path).stem.endswith('Tests')][:8]
    tests=[path for path in matched if '/Tests/' in path or Path(path).stem.endswith('Tests')][:4]
    batch=int(re.search(r'\d{4}',Path(source['source']).name)[0])
    # 值对象切片不能被原文“暂不实现”的系统引用扩大；启动器属于Tools而非Unity运行时。
    if batch==2:
        prod=['Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs'];tests=['Assets/Scripts/Gameplay/Tests/UnitUidTests.cs','Assets/Scripts/Gameplay/Tests/UnitAssemblyBoundaryTests.cs']
    if 155<=batch<=160:
        filenames={155:['MainForm','LauncherServices','LauncherModels'],156:['MainForm','BootstrapPackageBuilder'],157:['CdnUpdater','CdnContracts','LauncherServices'],158:['CdnPackageBuilder','CdnContracts'],159:['CdnPackageBuilder','CdnUpdater','CdnContracts'],160:['CdnPackageBuilder','BootstrapPackageBuilder']}[batch]
        prod=[f'Tools/UosGameLauncher/{name}.cs' for name in filenames if f'Tools/UosGameLauncher/{name}.cs' in code]
        tests=['Tools/UosGameLauncher/LauncherSelfTest.cs'] if 'Tools/UosGameLauncher/LauncherSelfTest.cs' in code else []
    if not prod:prod=[e['path'] for e in m.get('evidence',[]) if e['path'] in code and '/Tests/' not in e['path']][:3]
    if not tests:
        related=[Path(path).stem for path in prod if Path(path).stem not in ['Unit','AssemblyInfo','GlobalGameplayData','GameBootstrap','GameplaySnapshot']]
        def test_score(path):
            text=code[path]
            return sum((500 if Path(path).stem.startswith(name) else 0)+len(re.findall(r'\b'+re.escape(name)+r'\b',text)) for name in related)
        tests=sorted([path for path in code if '/Tests/' in path and '[Test' in code[path] and test_score(path)>1],key=lambda path:(-test_score(path),path))[:3]
    body=read(ROOT/m['path']);start=body.find('### 当前类型、核心算法与数据流');end=body.find('### 输入输出与边界',start)
    if start<0:
        end=body.find('### 输入输出与边界');start=end
    if end<0:continue
    replacement='### 当前类型、核心算法与数据流\n\n本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。\n\n'
    for path in prod:
        text=code[path];types=re.findall(r'\b(?:class|struct|enum|interface)\s+(\w+)',text)
        replacement+=f"- `{path}`："+'、'.join('`'+t+'`' for t in types[:7])+'。\n'
    replacement+='\n以下代码为当前真实实现节选，完整方法以对应源码为准。\n'
    for path in prod[:2]:
        text=code[path]
        methods=re.findall(r'(?m)^\s*(?:public|private|internal|protected)\s+(?:(?:static|override|virtual|async|sealed)\s+)*(?:[\w<>,.?\[\]]+\s+)+(\w+)\s*\(',text)
        named=[name for name in methods if re.search(r'\b'+re.escape(name)+r'\b',original)]
        named=sorted(set(named),key=lambda n:(-len(re.findall(r'\b'+re.escape(n)+r'\b',original)),n))
        if batch==2:named=['CompareTo','Equals']
        replacement+=f'\n`{path}`：\n\n```csharp\n{fragment(text,named[0] if named else None)}\n```\n'
    body=body[:start]+replacement+'\n'+body[end:]
    start=body.index('## Agent 测试与验收');end=body.index('## 恢复与限制',start)
    testbody='## Agent 测试与验收\n\n本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。\n\n'
    for path in tests:
        text=code[path];names=re.findall(r'\[(?:Test|TestCase)[^\]]*\]\s*(?:\[[^\]]*\]\s*)*public\s+(?:void|IEnumerator|async\s+Task|Task)\s+(\w+)\s*\(',text)
        preferred=[n for n in names if re.search(r'\b'+re.escape(n)+r'\b',original)]
        if path.startswith('Tools/') and not names:names=re.findall(r'(?m)^\s*(?:public|private|internal)\s+static\s+(?:[\w<>]+\s+)+(\w+)\s*\(',text)
        name=(preferred or names or [None])[0]
        assembly=None
        for parent in (ROOT/path).parents:
            if parent==ROOT/'Assets':break
            candidates=list(parent.glob('*.asmdef'))
            if candidates:assembly=json.loads(read(candidates[0])).get('name');break
        mode='启动器自检（本轮不执行）' if path.startswith('Tools/') else 'PlayMode' if 'PlayMode' in path else 'EditMode'
        testbody+=f"- `{path}`：{mode}，程序集 `{assembly or '独立桌面项目或实际asmdef'}`，函数 `{name or Path(path).stem}`；输入/夹具与期望见真实断言，失败保留回执与 Console。\n"
        if name:testbody+=f'\n```csharp\n{fragment(text,name,50)}\n```\n'
    if not tests:testbody+='没有找到原范围精确命名的当前测试文件；关闭记录依据用户人工功能确认，不伪造自动化用例。\n'
    body=body[:start]+testbody+'\n'+body[end:]
    (ROOT/m['path']).write_text(body,encoding='utf-8')
    paths=list(dict.fromkeys(prod+tests));m['evidence']=[dict(path=q,sha256=hashlib.sha256((ROOT/q).read_bytes()).hexdigest(),kind='原执行批次对应的当前源码，未重跑行为测试') for q in paths]
    m['assessment']['sourceEvidence']=paths
    m['assessment']['scopeBasis']='原独立执行批次的具体文件/类型，后续接口扩展另有执行计划'
    if m['status']=='已关闭':
        old=m['assessment']['conclusion'];new='用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。'
        m['assessment']['conclusion']=new;body=body.replace(old,new);(ROOT/m['path']).write_text(body,encoding='utf-8')
    save(p,m);rows.append((m['code'],m['title'],prod[:2],tests[:2]))
print('重新核对',len(rows),'独立批次')
for row in rows:print(row)
