from verify_workbench import Service,ROOT,until,check,RESULT
from pathlib import Path
import tempfile,subprocess,json,os,hashlib,uuid,shutil,contextlib
out=Path(os.environ['LOCALAPPDATA'])/'Karolina/revision-inventory'
s=Service()
try:
    s.api('unity',dict(action='connect'))
    refresh=s.api('unity',dict(action='compile'))
    editor=s.api('unity',dict(action='state'))
    console=s.api('unity',dict(action='console'))
    def payload(r):
        response=r['response']
        if 'structuredContent' in response:return response['structuredContent'].get('result',response['structuredContent'])
        for c in response.get('content',[]):
            if c.get('type')=='text':
                try:x=json.loads(c['text']);return x.get('result',x)
                except json.JSONDecodeError:pass
        raise AssertionError('没有可解读回执')
    state=payload(editor);check(not state['IsCompiling'] and not state['IsUpdating'],'Unity 刷新后处于非编译非更新状态')
    logs=payload(console);check(not any('error CS' in e.get('Message','') for e in logs),'Unity Console 无 C# 编译错误，历史插件错误保留')
    tests=s.api('unity',{'action':'tests','assembly':'FrameSyncMoba.FrameSync.Tests','class':'CommandTargetTickResolverAdaptiveTests','mode':'EditMode'})
    check('Passed 6/6' in tests['verdict'],'Unity 聚焦 EditMode 六个实际用例通过')
    data=dict(checks=RESULT.copy(),refresh=refresh,editor=editor,console=console,tests=tests,consoleErrorCount=len(logs),noScreenshots=True)
finally:s.close()
fixture_root=ROOT.parent/'KarolinaVerification';fixture_root.mkdir(parents=True,exist_ok=True)
@contextlib.contextmanager
def writable_fixture():
    # 使用普通项目目录的继承 ACL，避开 Python 3.13 的 0700 临时目录
    # 和 Codex Desktop 的 LocalAppData 虚拟化；不更改项目或系统目录权限。
    folder=fixture_root/('karolina-real-write-'+uuid.uuid4().hex);folder.mkdir()
    try:yield folder
    finally:
        if folder.resolve().parent!=fixture_root.resolve() or not folder.name.startswith('karolina-real-write-'):raise RuntimeError('拒绝越界清理')
        shutil.rmtree(folder)
        if not any(fixture_root.iterdir()):fixture_root.rmdir()
with writable_fixture() as folder:
    p=Path(folder);subprocess.run(['git','init','-q',folder],check=True)
    (p/'AGENTS.md').write_text('这是 Karolina 验收临时仓库。仅授权创建 marker.txt，其它文件与 Git 保持原样。不要运行测试或 Unity。',encoding='utf-8')
    s=Service(p)
    try:
        s.api('codex/connect',{});model=next(m for m in s.api('state')['models'] if 'luna' in m['model'])
        r=s.api('chat/send',dict(text='仅创建 marker.txt，UTF-8 内容必须精确为 KAROLINA-WRITE-OK，无换行。不要修改其它文件、不要提交、不要运行测试。',model=model['model'],effort=model['defaultReasoningEffort'],access='workspace-write',documents=[],threadId=None))
        until(lambda:not s.api('state')['busy'],240)
        def marker_valid():
            try:return (p/'marker.txt').read_bytes()==b'KAROLINA-WRITE-OK'
            except (PermissionError,FileNotFoundError):return False
        until(marker_valid,10)
        check(s.api('diagnostics')['run']['state']=='completed' and marker_valid(),'真实 Codex 工程写入权限在临时仓库创建精确内容')
        check(set(x.name for x in p.iterdir())=={'.git','AGENTS.md','marker.txt'},'真实权限验收没有增加其它工程文件')
        data['writeRun']=r;data['checks']=RESULT.copy()
    finally:s.close()
protected=json.loads((out/'protected.json').read_text(encoding='utf-8'))
if isinstance(protected,dict) and 'files' in protected:protected=protected['files']
changed=[path for path,digest in protected.items() if not (ROOT/path).exists() or hashlib.sha256((ROOT/path).read_bytes()).hexdigest()!=digest]
check(not changed,'本轮 Assets/Packages/ProjectSettings 原文件哈希全部保持一致')
data['protectedFiles']=len(protected);data['checks']=RESULT
(out/'unity-write-acceptance.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print('真实 Unity/临时写入与原工程保护验收完成',flush=True)
