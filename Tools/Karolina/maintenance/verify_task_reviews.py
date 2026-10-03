"""无截图：三狼真实条目只读，审批写操作只在其临时工程副本。"""
import hashlib, json, os, shutil, subprocess, tempfile
from pathlib import Path
from verify_workbench import ROOT, Service, Browser, until

OUT=Path(os.environ['LOCALAPPDATA'])/'Karolina/revision-inventory'
TESTS=ROOT/'Tools/Karolina/Karolina.Tests/bin/Release/net8.0/Karolina.Tests.dll'
CHECKS=[]
def check(value, title):
    if not value: raise AssertionError(title)
    CHECKS.append(title); print('PASS '+title, flush=True)
def fill(browser,id,value):
    browser.eval(f"document.getElementById({json.dumps(id)}).value={json.dumps(value)};document.getElementById({json.dumps(id)}).dispatchEvent(new Event('input',{{bubbles:true}}));true")
def click(browser,id): browser.eval(f"document.getElementById({json.dumps(id)}).click();true")

manifest=json.loads((OUT/'wolf-import-manifest.json').read_text(encoding='utf-8'))
manifest.pop('replaceId',None)
with tempfile.TemporaryDirectory(prefix='karolina-wolf-review-') as directory:
    root=Path(directory); shutil.copytree(ROOT/'Docs',root/'Docs')
    (root/'ProjectSettings').mkdir()
    for f in manifest['files']:
        source=ROOT/f['path']; destination=root/f['path']
        if source.exists(): destination.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(source,destination)
    path=root/'import.json'; path.write_text(json.dumps(manifest,ensure_ascii=False),encoding='utf-8')
    result=subprocess.run(['dotnet',str(TESTS),'--import-interrupted',str(root),str(path)],capture_output=True,text=True,encoding='utf-8',check=True)
    id=json.loads(result.stdout)['id']
    env=dict(os.environ,KAROLINA_APPSERVER_FIXTURE='1',KAROLINA_CODEX_EXE=str(TESTS.with_suffix('.exe')))
    service=Service(root,env); browser=None
    try:
        task=service.api('review/'+id)
        check(len(task['files'])==len(manifest['files']) and all(f['decision']=='待审批' for f in task['files']),'临时三狼副本完整收录候选，默认全部待审批')
        check(task['baselineKind']=='事后整理（历史参考）' and len(task['selectionEvidence'])==len(manifest['files']),'HTTP 返回历史参考来源及逐文件依据')
        code='Assets/Scripts/Gameplay/NonHero/MurkWolfContentIds.cs'
        asset='Assets/Config/Formal/Buffs/MurkWolf/GreaterMurkWolfCurrentHealthOnHit.asset'
        model=next(f['path'] for f in task['files'] if f['path'].endswith('.glb'))
        diff=service.api('review/'+id+'/diff?path='+__import__('urllib.parse').parse.quote(code))
        check(diff['text'] and any(line['kind']=='added' for line in diff['lines']),'HTTP 提供真实三狼专用代码行差异')
        diff=service.api('review/'+id+'/diff?path='+__import__('urllib.parse').parse.quote(model))
        check(not diff['text'] and diff['file']['after']['hash'] and diff['message'],'二进制模型明确显示指纹与内置视图限制')
        browser=Browser(service.url)
        browser.eval("document.querySelector('[data-page=review]').click();true")
        until(lambda:browser.eval('currentReview?.id')==id,30)
        check('事后整理' in browser.eval("document.getElementById('reviewProvenance').textContent") and '中断' in browser.eval("document.getElementById('reviewRun').textContent"),'真实页面显示事后来源与未完成中断状态')
        check(browser.eval('document.querySelectorAll("#reviewFiles button").length')==len(task['files']),'真实页面文件清单和 API 一致')
        browser.eval(f"openReviewFile(currentReview.files.find(f=>f.path==={json.dumps(asset)}));true")
        until(lambda:browser.eval('reviewFile?.path')==asset,30)
        check('收录依据' in browser.eval("document.getElementById('reviewDiff').textContent"),'资源差异视图显示收录依据')
        click(browser,'approveFile'); until(lambda:service.api('review/'+id)['revision']>task['revision'],30)
        until(lambda:not browser.eval('reviewSaving'),30)
        check(next(f for f in service.api('review/'+id)['files'] if f['path']==asset)['decision']=='通过','页面点击通过保存单文件结论')
        browser.eval(f"openReviewFile(currentReview.files.find(f=>f.path==={json.dumps(code)}));true")
        until(lambda:browser.eval('reviewFile?.path')==code,30)
        browser.eval("document.querySelector('#reviewDiff .diff-line')?.click();true")
        # 行定位必须来自页面实际生成的行节点，不借助手写服务端意见。
        if not browser.eval('noteAnchor'): browser.eval("document.querySelector('#reviewDiff [data-after]')?.click();true")
        if not browser.eval('noteAnchor'):
            browser.eval("[...document.querySelectorAll('#reviewDiff button')].find(b=>b.textContent.trim()==='1')?.click();true")
        check(bool(browser.eval('noteAnchor')),'点击差异行建立真实前后行意见定位')
        fill(browser,'reviewNote','临时机制测试：这一行需要修正，非真实三狼人工结论。')
        old=service.api('review/'+id)['revision']; click(browser,'rejectFile')
        until(lambda:service.api('review/'+id)['revision']>old,30);until(lambda:not browser.eval('reviewSaving'),30)
        rejected=next(f for f in service.api('review/'+id)['files'] if f['path']==code)
        check(rejected['decision']=='不通过' and rejected['notes'][0]['line']>=1,'页面保存文件驳回及定位行解释')
        browser.eval('window.confirm=()=>true;true'); click(browser,'approveTask'); until(lambda:not browser.eval('reviewSaving'),30)
        check(service.api('review/'+id)['state']=='待审批','有文件不通过时页面整体批准被拒绝')
        fill(browser,'reviewFeedback','临时机制测试：修正该行后再执行。');click(browser,'returnTask')
        until(lambda:service.api('review/'+id)['state']=='待再执行',30);until(lambda:not browser.eval('reviewSaving'),30)
        check(len(service.api('review/'+id)['attempts'])==1,'页面整体退回保存本轮文件意见')
        click(browser,'resumeReview');until(lambda:browser.eval('page')=='chat',30)
        check(browser.eval('taskForChat')==id and browser.eval('document.getElementById("executionPlan").value')=='PLAN-WOLF-165' and '这一行需要修正' in browser.eval('document.getElementById("prompt").value'),'返回再执行携原任务、计划、文件行意见到 AI 指令')
        check(not service.api('state')['busy'] and service.api('review/'+id)['state']=='待再执行','返回指令等待用户发送，不自动执行或改写计划')
    finally:
        if browser: browser.close()
        service.close()

service=Service(ROOT)
try:
    task=next(t for t in service.api('reviews') if t['planId']=='PLAN-WOLF-165')
    check(task['state']=='待审批' and all(f['decision']=='待审批' and not f['notes'] for f in task['files']) and not task['attempts'],'真实三狼条目始终待审批，未混入临时测试意见')
    docs=service.api('documents')
    check(all(next(d for d in docs if d['id']==id)['status']=='关闭' for id in ['PLAN-0094','PLAN-CONTENT-138']) and next(d for d in docs if d['id']=='PLAN-WOLF-165')['status']=='执行','真实文档 API 显示两个关闭计划与三狼执行状态')
finally: service.close()
before=json.loads((OUT/'wolf-protected-unity-files.json').read_text(encoding='utf-8'))
after={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for scope in ['Assets','Packages','ProjectSettings'] for p in (ROOT/scope).rglob('*') if p.is_file()}
check(before==after,'2953 个生产 Unity 文件内容与路径未被本轮机制检查改写')
index=subprocess.run(['git','--no-optional-locks','rev-parse','--git-path','index'],cwd=ROOT,capture_output=True,text=True,check=True).stdout.strip()
index=Path(index);index=index if index.is_absolute() else ROOT/index
check(hashlib.sha256(index.read_bytes()).hexdigest()==(OUT/'wolf-index-hash.txt').read_text(),'生产 Git 索引哈希未变')
report={'passed':len(CHECKS),'failed':0,'checks':CHECKS,'taskId':task['id'],'files':len(task['files']),'scope':'临时副本 HTTP/DOM 审批写入；真实条目只读；无截图，无真实 Codex 执行或三狼行为验收。'}
(OUT/'wolf-review-behavior-evidence.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(CHECKS)} passed; real task remains pending.')
