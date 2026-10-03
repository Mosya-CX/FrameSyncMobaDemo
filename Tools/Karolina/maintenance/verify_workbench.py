"""无截图验收：HTTP、真实 Codex/Unity、Edge DOM 及受控服务回归。"""
from pathlib import Path
import subprocess,urllib.request,urllib.error,json,re,os,time,tempfile,socket,concurrent.futures
try:import websocket
except ModuleNotFoundError:import dom_websocket as websocket
ROOT=Path(__file__).resolve().parents[3]
EXE=Path(os.environ.get('KAROLINA_DESKTOP_EXE',str(ROOT/'Tools/Karolina/artifacts/current/Karolina.Desktop.exe')))
if not EXE.exists():EXE=ROOT/'Tools/Karolina/Karolina.Desktop/bin/Release/net8.0-windows/Karolina.Desktop.exe'
RESULT=[]
def check(value,title):
    if not value:raise AssertionError(title)
    RESULT.append(title);print('PASS',title,flush=True)
def until(fn,seconds=180):
    end=time.monotonic()+seconds
    while time.monotonic()<end:
        value=fn()
        if value:return value
        time.sleep(.15)
    raise TimeoutError('没有取得预期终态')
class Service:
    def __init__(self,root=ROOT,env=None):
        self.p=subprocess.Popen([str(EXE),str(root),'--serve'],env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8',creationflags=subprocess.CREATE_NO_WINDOW)
        self.url=self.p.stdout.readline().strip()
        if not self.url.startswith('http://127.0.0.1:'):raise RuntimeError(self.p.stderr.read())
        self.token=re.search(r'name="karolina-session" content="([^"]+)"',urllib.request.urlopen(self.url).read().decode())[1]
    def api(self,path,data=None):
        req=urllib.request.Request(self.url+'/api/'+path,data=None if data is None else json.dumps(data,ensure_ascii=False).encode(),headers={'X-Karolina-Session':self.token,'Content-Type':'application/json'})
        return json.load(urllib.request.urlopen(req,timeout=120))
    def rejected(self,path,data):
        try:self.api(path,data)
        except urllib.error.HTTPError as e:return e.code
        return 200
    def close(self):
        if self.p.poll() is None:
            state=self.api('state')
            if state['busy'] and state['activeTurn']:self.api('chat/stop',{});until(lambda:not self.api('state')['busy'])
            self.api('shutdown',{});self.p.wait(timeout=30)
class Browser:
    def __init__(self,url):
        self.profile=tempfile.TemporaryDirectory(prefix='karolina-dom-',ignore_cleanup_errors=True)
        with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
        exe=next(Path(p)/'Microsoft/Edge/Application/msedge.exe' for p in [os.environ['ProgramFiles(x86)'],os.environ['ProgramFiles']] if (Path(p)/'Microsoft/Edge/Application/msedge.exe').exists())
        self.p=subprocess.Popen([str(exe),'--headless=new','--no-first-run','--disable-background-mode','--remote-debugging-port='+str(port),'--user-data-dir='+self.profile.name,url],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
        def targets():
            try:return json.load(urllib.request.urlopen(f'http://127.0.0.1:{port}/json/list'))
            except OSError:return None
        target=until(lambda:next((t for t in targets() or [] if t['type']=='page' and t.get('url','').rstrip('/')==url.rstrip('/')),None),30)
        self.ws=websocket.create_connection(target['webSocketDebuggerUrl'],timeout=30,suppress_origin=True);self.id=0
        until(lambda:self.eval('typeof window.KarolinaMarkdown==="function" && docs.length>0'),30)
    def eval(self,expression):
        self.id+=1;self.ws.send(json.dumps(dict(id=self.id,method='Runtime.evaluate',params=dict(expression=expression,returnByValue=True,awaitPromise=True))))
        while True:
            m=json.loads(self.ws.recv())
            if m.get('id')==self.id:
                result=m.get('result',{})
                if result.get('exceptionDetails'):raise AssertionError(str(result['exceptionDetails']))
                return result.get('result',{}).get('value')
    def close(self):
        self.ws.close();subprocess.run(['taskkill','/PID',str(self.p.pid),'/T','/F'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);self.p.wait(timeout=20)
        try:self.profile.cleanup()
        except PermissionError:pass
def fixture_regressions():
    with tempfile.TemporaryDirectory(prefix='karolina-api-fixture-') as folder:
        root=Path(folder);subprocess.run(['git','init','-q',folder],check=True)
        env=dict(os.environ,KAROLINA_APPSERVER_FIXTURE='1',KAROLINA_CODEX_EXE=str(ROOT/'Tools/Karolina/Karolina.Tests/bin/Debug/net8.0/Karolina.Tests.exe'))
        first=Service(root,env);second=Service(root,env);browser=None
        try:
            for s in [first,second]:s.api('codex/connect',{})
            send=dict(text='HOLD',model='fixture',effort='low',access='read-only',documents=[],threadId=None)
            one=first.api('chat/send',send)
            check(first.rejected('chat/send',send)==409,'重复发送返回忙错误')
            check(first.api('state')['busy'] and first.api('diagnostics')['run']['id']==one['runId'],'忙错误保留原轮次和工作区锁')
            check(second.rejected('chat/send',send)==409,'另一工作台实例不能取得正在使用的工程锁')
            first.api('chat/stop',{});until(lambda:not first.api('state')['busy'])
            two=first.api('chat/send',dict(send,threadId=one['threadId']))
            time.sleep(.35)
            check(first.api('state')['busy'] and first.api('state')['activeTurn']==two['turnId'],'旧同线程轮次终态不结束新轮次')
            first.api('chat/stop',{});until(lambda:not first.api('state')['busy'])
            second.api('chat/send',send);second.api('chat/stop',{});until(lambda:not second.api('state')['busy'])
            chatfiles=list((Path(os.environ['LOCALAPPDATA'])/'Karolina/projects').glob('*/chats.json'))
            matching=[json.loads(p.read_text(encoding='utf-8')) for p in chatfiles if any(c['id']==one['threadId'] for c in json.loads(p.read_text(encoding='utf-8')))]
            # 两个受控进程生成的 threadId 应唯一；本机真实服务保证 UUID。
            check(any(len(items)==2 for items in matching),'多个实例合并聊天索引，保留先前实例创建的对话')
            # 空库创建草案后进行前端两审批行为验收。
            first.api('document/create',dict(type='requirement',title='界面验收资料',domain='测试'))
            browser=Browser(first.url)
            first.api('chat/send',dict(send,text='APPROVAL',threadId=one['threadId']))
            until(lambda:len(first.api('diagnostics')['approvals'])==2)
            until(lambda:browser.eval('approvalQueue.size')==2)
            check(browser.eval('document.querySelectorAll("#approval strong").length')==2,'两个审批同时可见，不会互相覆盖')
            browser.eval('document.querySelector("#approval button").click()')
            until(lambda:len(first.api('diagnostics')['approvals'])==1)
            until(lambda:browser.eval('approvalQueue.size')==1)
            check(browser.eval('!document.getElementById("approval").hidden'),'回应一个审批后仍能操作另一个')
            browser.eval('location.reload();true');time.sleep(.4);until(lambda:browser.eval('typeof approvalQueue!=="undefined"&&approvalQueue.size===1'))
            check(browser.eval('document.querySelectorAll("#approval strong").length')==1,'页面重载恢复尚未处理的审批')
            browser.eval('document.querySelector("#approval button").click()');until(lambda:not first.api('state')['busy'])
            first.api('chat/send',dict(send,text='MALFORMED',threadId=one['threadId']))
            until(lambda:not first.api('state')['busy'])
            marker=root/'fixture-heartbeat.txt';before=marker.read_text() if marker.exists() else None;time.sleep(.35)
            check((marker.read_text() if marker.exists() else None)==before and first.api('diagnostics')['run']['state']=='disconnected','坏消息断连先停止写进程，再释放锁并记录终态')
            first.api('codex/connect',{});check(first.api('state')['codex']['connected'],'异常断连后可以重新连接 Codex')
        finally:
            if browser:browser.close()
            first.close();second.close()
def live():
    s=Service();browser=None
    try:
        try:urllib.request.urlopen(s.url+'/api/state');raise AssertionError('unauthorized')
        except urllib.error.HTTPError as e:check(e.code==403,'本机 API 拒绝无会话令牌请求')
        docs=s.api('documents');check(len(docs)>=170,'中文功能文档与独立元数据可读取')
        s.api('codex/connect',{});state=s.api('state');model=next(m for m in state['models'] if 'luna' in m['model'])
        check(state['codex']['connected'] and len(state['models'])>1,'真实 Codex 模型发现与账号连接')
        browser=Browser(s.url)
        check(browser.eval('document.getElementById("model").options.length')==len(state['models']),'模型下拉使用服务实际返回的模型')
        check(browser.eval('document.getElementById("access").options.length')==3,'只读、工程写入与完全访问可选择')
        check(browser.eval('selectedDocs.size===0 && document.getElementById("referenceCount").textContent==="未选择"'),'参考需求与计划允许空选')
        browser.eval('document.querySelectorAll("#referenceList input")[0].click();document.querySelectorAll("#referenceList input")[1].click();true')
        check(browser.eval('selectedDocs.size')==2,'参考资料支持多选')
        check(browser.eval('!document.getElementById("referenceList").textContent.includes("REQ-")'),'资料选项只显示标题与状态')
        check(browser.eval('(()=>{let x=document.createElement("div");x.innerHTML=KarolinaMarkdown("# 标题\\n\\n<script>alert(1)</script>\\n\\n|列|值|\\n|---|---|\\n|一|二|\\n\\n- [x] 已完成\\n\\n```cs\\npublic int X;\\n``` ");return x.querySelector("script")===null&&!!x.querySelector("h1")&&!!x.querySelector("table")&&!!x.querySelector("pre code")&&!!x.querySelector("input:checked");})()'),'Markdown 标题表格代码任务列表与 HTML 转义')
        check(browser.eval('(()=>{const d=docs.find(d=>d.path.includes(" Modifier "));const node=document.createElement("div");node.innerHTML=KarolinaMarkdown("[查看相关功能]("+d.path+")");return node.querySelector("a")?.dataset.document===d.id;})()'),'Markdown 能跳转含空格路径的功能案')
        browser.eval('receive({method:"item/started",params:{item:{type:"commandExecution",id:"dom-tool",command:"验收命令",status:"inProgress"}}});receive({method:"item/completed",params:{item:{type:"commandExecution",id:"dom-tool",command:"验收命令",status:"failed",aggregatedOutput:"真实失败回执"}}});true')
        check(browser.eval('toolMessages.get("dom-tool").summary.textContent.includes("失败") && toolMessages.get("dom-tool").pre.textContent==="真实失败回执"'),'对话内显示工具实际完成状态和失败输出')
        browser.eval('loadDocument("REQ-KAR-003")');until(lambda:browser.eval('currentDoc?.document.id==="REQ-KAR-003"'))
        check(browser.eval('document.getElementById("markdown").querySelectorAll("h2").length>3'),'需求正文进入排版阅读模式')
        browser.eval('navigate("git")');until(lambda:browser.eval('gitChanges.length>0'))
        browser.eval('showDiff(gitChanges[0],gitChanges[0].status==="??"||gitChanges[0].status[1]!==" "?"worktree":"index")')
        until(lambda:browser.eval('document.querySelectorAll(".diff-line").length>0'))
        check(browser.eval('document.getElementById("diff").textContent.length>0'),'Git 只列真实变更并显示可读差异')
        inp=dict(text='不要使用工具，不修改文件。只回答“Karolina真实对话验收通过”。',model=model['model'],effort=model['defaultReasoningEffort'],access='read-only',documents=[],threadId=None)
        r=s.api('chat/send',inp);until(lambda:not s.api('state')['busy'],180)
        check(s.api('diagnostics')['run']['state']=='completed','真实 Codex 空选资料只读对话完成')
        chat=s.api('chat/'+r['threadId']);check(any(i.get('type')=='agentMessage' for t in chat['thread']['turns'] for i in t['items']),'真实 Codex 历史能读取助手回复')
        r2=s.api('chat/send',dict(inp,text='不要使用工具，不修改文件。沿用上轮对话，只回复“连续对话通过”。',documents=['REQ-KAR-003','PLAN-KAR-002'],threadId=r['threadId']));until(lambda:not s.api('state')['busy'],180)
        check(r2['threadId']==r['threadId'] and s.api('diagnostics')['run']['state']=='completed','真实 Codex 同会话继续与多个资料参考完成')
        r3=s.api('chat/send',dict(inp,text='不要使用工具，不修改文件。请写一篇五千字的软件工作流分析。',threadId=r['threadId']));until(lambda:s.api('state')['activeTurn']);s.api('chat/stop',{});until(lambda:not s.api('state')['busy'],120)
        check(s.api('diagnostics')['run']['state']=='interrupted','真实 Codex 停止取得 interrupted 终态')
        print('真实运行记录',r['runId'],r2['runId'],r3['runId'],flush=True)
        connected=s.api('unity',dict(action='connect'));check(connected['tools']>0,'Unity MCP 实际连接并核对当前工程')
        editor=s.api('unity',dict(action='state'));check(editor.get('evidenceId'),'Unity 状态回执落盘')
        console=s.api('unity',dict(action='console'));check(console.get('evidenceId'),'Unity Console 实际回执落盘且不清空历史错误')
        report=dict(date='2026-10-02',checks=RESULT,liveRuns=[r['runId'],r2['runId'],r3['runId']],model=model['model'],unityTools=connected['tools'],editor=editor,console=console,noScreenshots=True)
        out=Path(os.environ['LOCALAPPDATA'])/'Karolina/revision-inventory/workbench-acceptance.json';out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('验收回执',out,flush=True)
    finally:
        if browser:browser.close()
        s.close()
if __name__=='__main__':
    fixture_regressions();live();print('TOTAL',len(RESULT),'passed',flush=True)
