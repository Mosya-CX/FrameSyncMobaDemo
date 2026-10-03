"""本轮 HTTP/DOM/Git 验证，不使用截图，也不修改 Unity 工程。"""
import os,json,time,tempfile,subprocess,statistics,urllib.error
from pathlib import Path
os.environ.setdefault('KAROLINA_DESKTOP_EXE',str(Path(__file__).resolve().parents[1]/'artifacts/current/Karolina.Desktop.exe'))
from verify_workbench import Service,Browser,until,check,RESULT,ROOT

def fixtures():
    with tempfile.TemporaryDirectory(prefix='karolina-lifecycle-') as folder:
        r=Path(folder);subprocess.run(['git','init','-q',folder],check=True)
        subprocess.run(['git','-C',folder,'config','user.name','Fixture'],check=True);subprocess.run(['git','-C',folder,'config','user.email','fixture@local.invalid'],check=True)
        s=Service(r);b=None
        try:
            a=s.api('document/create',dict(type='requirement',title='目标一',domain='模块'))
            c=s.api('document/create',dict(type='requirement',title='目标二',domain='模块'))
            rule=s.api('document/create',dict(type='rule',title='统一规则',domain='规则'))
            plan=s.api('document/create',dict(type='plan',title='一次执行',domain='模块',requirements=[a['id']]))
            check(a['status']=='激活' and rule['status'] is None,'需求激活，规则没有状态')
            data=s.api('document/'+plan['id']);check('metadata' not in data and '[目标一]' in data['markdown'],'正文快读不携带元数据，计划链接具体需求')
            m=s.api('document/'+plan['id']+'/metadata');check(m['metadata']['requirementRefs'][0]['title']=='目标一','引用元数据记录具体标题与版本')
            s.api('document/details',dict(id=plan['id'],expectedMetadataHash=m['hash'],status='执行',requirements=[a['id'],c['id']],conclusion=None))
            check('[目标二]' in s.api('document/'+plan['id'])['markdown'],'修改关联同步正文需求链接')
            check(s.rejected('document/details',dict(id=plan['id'],expectedMetadataHash=m['hash'],status='测试',requirements=None,conclusion=None))==409,'旧元数据版本不能覆盖外部更新')
            b=Browser(s.url)
            b.eval(f"loadDocument({json.dumps(a['id'])})");until(lambda:b.eval('currentDoc?.document.id')==a['id'])
            b.eval("document.querySelector('[data-page=plan]').click()")
            check(b.eval("document.getElementById('pageTitle').textContent")=='目标一','切换目录分类保留正在阅读文档标题')
            check(b.eval("document.getElementById('chatActions').hidden"),'模型刷新运行详情和Unity顶部按钮仅AI对话可见')
            b.eval(f"loadDocument({json.dumps(rule['id'])})");until(lambda:b.eval('currentDoc?.document.id')==rule['id'])
            check(b.eval("document.getElementById('documentStatus').hidden && document.getElementById('historyButton').hidden"),'规则不显示状态与演进')
            b.eval(f"loadDocument({json.dumps(a['id'])})");until(lambda:b.eval('currentDoc?.document.id')==a['id'])
            b.eval("document.getElementById('metadataButton').click()")
            until(lambda:b.eval("document.getElementById('drawer').open"))
            check(b.eval("document.querySelectorAll('[data-meta-tab]').length") == 3,'信息按钮分开元数据、关联、来源证据')
            b.eval("document.getElementById('drawer').close();window.originalApi=api;api=async(path,body)=>{if(path.startsWith('document/'))await new Promise(r=>setTimeout(r,350));return originalApi(path,body)}")
            # 未缓存加载 + 用户导航。
            b.eval(f"loadDocument({json.dumps(c['id'])});document.querySelector('[data-page=chat]').click()")
            time.sleep(.7);check(b.eval('page')=='chat','旧文档回执不能拉回已离开的页面')
            b.eval(f"api=originalApi;loadDocument({json.dumps(a['id'])})");until(lambda:b.eval('currentDoc?.document.id')==a['id'])
            b.eval("document.getElementById('sourceMode').click();document.getElementById('documentSource').value+='\\n未保存草稿';document.getElementById('documentSource').dispatchEvent(new Event('input'));window.confirm=()=>true;")
            b.eval(f"loadDocument({json.dumps(c['id'])})");until(lambda:b.eval('currentDoc?.document.id')==c['id'])
            check(True,'确认放弃未保存正文后可以打开未缓存文档')
            b.eval("document.getElementById('sourceMode').click();document.getElementById('documentSource').value+='\\n已提交';document.getElementById('documentSource').dispatchEvent(new Event('input'));api=async(path,body)=>{if(path==='document/save')await new Promise(r=>setTimeout(r,350));return originalApi(path,body)};document.getElementById('saveDocument').click();document.getElementById('documentSource').value+='\\n提交后新输入';document.getElementById('documentSource').dispatchEvent(new Event('input'));")
            time.sleep(.9);check(b.eval("docDirty&&document.getElementById('documentSource').value.endsWith('提交后新输入')"),'保存等待期间的新输入不会被回执覆盖')
            b.eval('api=originalApi;docDirty=false')
            b.eval("document.getElementById('sourceMode').click();document.getElementById('documentSource').value+='\\n离页保存';document.getElementById('documentSource').dispatchEvent(new Event('input'));api=async(path,body)=>{if(path==='document/save')await new Promise(r=>setTimeout(r,300));return originalApi(path,body)};document.getElementById('saveDocument').click();document.querySelector('[data-page=chat]').click();")
            time.sleep(.8);check(b.eval("page==='chat'&&!docDirty"),'保存回执不能拉回已离开的页面或恢复放弃编辑')
            b.eval('api=originalApi')
            (r/'first.txt').write_text('one\n',encoding='utf-8');(r/'second.txt').write_text('two\n',encoding='utf-8')
            b.eval("document.querySelector('[data-page=git]').click()")
            until(lambda:b.eval("gitChanges.some(c=>c.path==='first.txt')"))
            b.eval("api=async(path,body)=>{if(path.includes('first.txt')&&path.startsWith('git/diff'))await new Promise(r=>setTimeout(r,500));return originalApi(path,body)};showDiff(gitChanges.find(c=>c.path==='first.txt'),'worktree');showDiff(gitChanges.find(c=>c.path==='second.txt'),'worktree');")
            time.sleep(.8);check(b.eval("document.getElementById('diffTitle').textContent.endsWith('second.txt')"),'快速选择差异只保留最后文件')
            b.eval("api=originalApi;gitSelection.add('second.txt');updateControls();document.getElementById('commitMessage').value='本轮临时仓库提交验收';document.getElementById('commitMessage').dispatchEvent(new Event('input'));document.getElementById('commit').click();")
            until(lambda:not any(x['path']=='second.txt' for x in s.api('git')['changes']))
            until(lambda:b.eval('diffSelection===null'));check(True,'勾选后直接提交，并清理已提交文件的差异')
            check(subprocess.run(['git','-C',folder,'log','-1','--format=%s'],capture_output=True,text=True,encoding='utf-8').stdout.strip()=='本轮临时仓库提交验收','前端实际提交所选文件成功')
            s.api('git/action',dict(action='stage',paths=['first.txt'],message=None));s.api('git/action',dict(action='commit',paths=[],message='temporary second baseline'))
            (r/'first.txt').write_text('one\nchanged\n',encoding='utf-8');b.eval("document.getElementById('refreshGit').click()")
            until(lambda:b.eval("gitChanges.some(c=>c.path==='first.txt'&&c.text)"));b.eval("showDiff(gitChanges.find(c=>c.path==='first.txt'),'worktree')")
            until(lambda:b.eval("document.getElementById('diff').textContent.includes('+changed')"))
            (r/'first.txt').write_text('one\n',encoding='utf-8');b.eval("document.getElementById('refreshGit').click()")
            until(lambda:b.eval('diffSelection===null'));check(True,'外部恢复文件后刷新确实更新列表与差异')
            # 不保留伪执行成功：真实生命周期转换通过 HTTP。
            for status,conclusion in [('测试',None),('校正',None),('执行',None),('测试',None),('验收',None),('关闭','用户临时验收明确通过')]:
                m=s.api('document/'+plan['id']+'/metadata');s.api('document/details',dict(id=plan['id'],expectedMetadataHash=m['hash'],status=status,requirements=None,conclusion=conclusion))
            closed=s.api('document/'+plan['id']);check(s.rejected('document/save',dict(id=plan['id'],markdown='覆盖已关闭计划',expectedHash=closed['hash']))==409,'服务端拒绝重写已关闭计划')
        finally:
            if b:b.close()
            s.close()

def actual():
    s=Service();b=None
    try:
        docs=s.api('documents');plans=[d for d in docs if d['type']=='plan']
        check(len(plans)==132,'当前独立计划 132 份')
        check(plans[0]['code']=='0000' and plans[-1]['code']=='0169','计划按编码自然顺序排列，不按功能分组')
        times=[]
        for d in docs[:20]:
            start=time.perf_counter();value=s.api('document/'+d['id']);times.append((time.perf_counter()-start)*1000)
            check('metadata' not in value,'正文只读取阅读所需数据：'+d['title'])
        b=Browser(s.url);b.eval("document.querySelector('[data-page=plan]').click()")
        check(b.eval("[...document.querySelectorAll('#directory .title')][0].textContent")==plans[0]['title'],'前端计划首项对应最早编码')
        until(lambda:b.eval("!connectionRequests.codex&&!connectionRequests.unity"),25);connected=s.api('codex/connect',{});check(connected['connected'] and s.api('state')['models'],'真实Codex模型与账号连接')
        try:
            connected=s.api('unity',dict(action='connect'));check(connected['tools']>0,'真实Unity MCP连接并核对工程')
            editor=s.api('unity',dict(action='state'));check(bool(editor.get('evidenceId')),'只读Unity状态真实回执')
        except urllib.error.HTTPError as e:
            failure=json.loads(e.read().decode());editor=dict(connected=False,error=failure.get('error'))
            until(lambda:b.eval("state.unity.state==='连接失败'"))
            check(b.eval("document.getElementById('connectUnity').textContent.includes('连接失败')"),'Unity 未开启时在对话页显示真实连接失败')
        print('文档 HTTP 延迟 ms 中位数',round(statistics.median(times),2),'最大',round(max(times),2),flush=True)
        return dict(readMedianMs=statistics.median(times),readMaxMs=max(times),unity=editor)
    finally:
        if b:b.close()
        s.close()
if __name__=='__main__':
    fixtures();metrics=actual();out=Path(os.environ['LOCALAPPDATA'])/'Karolina/revision-inventory/interaction-regression.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(dict(date='2026-10-02',checks=RESULT,metrics=metrics,noScreenshots=True),ensure_ascii=False,indent=2),encoding='utf-8');print('TOTAL',len(RESULT),'passed',out,flush=True)
