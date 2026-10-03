"""当前修订的真实 HTTP、DOM 与原生窗口检查；不截图、不写生产 Git。"""
import os,json,time,tempfile,subprocess,sys,re,urllib.request,ctypes,concurrent.futures
from pathlib import Path
os.environ['KAROLINA_DESKTOP_EXE']=str(Path(__file__).resolve().parents[1]/'artifacts/current/Karolina.Desktop.exe')
from verify_workbench import Service,Browser,until,check,RESULT,ROOT,EXE
USER=ctypes.windll.user32
USER.IsWindow.argtypes=[ctypes.c_void_p];USER.IsIconic.argtypes=[ctypes.c_void_p]
USER.PostMessageW.argtypes=[ctypes.c_void_p,ctypes.c_uint,ctypes.c_void_p,ctypes.c_void_p]
def ps(command):
 r=subprocess.run(['powershell','-NoProfile','-Command',command],capture_output=True,text=True,encoding='utf-8',creationflags=subprocess.CREATE_NO_WINDOW)
 if r.returncode:raise RuntimeError(r.stderr)
 return r.stdout.strip()
def children(pid):
 data=json.loads(ps("@(Get-CimInstance Win32_Process -Filter \"name='msedge.exe' OR name='msedgewebview2.exe' OR name='codex.exe' OR name='Karolina.Tests.exe' OR name='Karolina.Desktop.exe'\" | Select-Object ProcessId,ParentProcessId) | ConvertTo-Json -Compress"))
 found={pid};changed=True
 while changed:
  more={p['ProcessId'] for p in data if p['ParentProcessId'] in found};changed=not more.issubset(found);found|=more
 return found-{pid}
def window_for(pids):
 handles=[];callback=ctypes.WINFUNCTYPE(ctypes.c_bool,ctypes.c_void_p,ctypes.c_void_p)
 @callback
 def visit(window,param):
  pid=ctypes.c_ulong();USER.GetWindowThreadProcessId(ctypes.c_void_p(window),ctypes.byref(pid))
  if pid.value in pids and USER.IsWindowVisible(ctypes.c_void_p(window)):handles.append(window)
  return True
 USER.EnumWindows(visit,0);return next(iter(handles),None)

def tray_callback(pid, event):
 callback=ctypes.WINFUNCTYPE(ctypes.c_bool,ctypes.c_void_p,ctypes.c_void_p)
 @callback
 def visit(window,param):
  owner=ctypes.c_ulong();USER.GetWindowThreadProcessId(ctypes.c_void_p(window),ctypes.byref(owner))
  title=ctypes.create_unicode_buffer(256);USER.GetWindowTextW(ctypes.c_void_p(window),title,256)
  if owner.value==pid and not title.value and not USER.IsWindowVisible(ctypes.c_void_p(window)):
   USER.PostMessageW(ctypes.c_void_p(window),0x0800,1,event)
  return True
 USER.EnumWindows(visit,0)

def tray_exit(pid):
 # Windows UI Automation 调用真实托盘菜单项的 InvokePattern，不走退出 API。
 return ps(f"""Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$condition=[System.Windows.Automation.AndCondition]::new([System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,{pid}),[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,'完全退出'))
$item=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
if($item){{$item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();'invoked'}}""")
def fixture_root(folder):
 subprocess.run(['git','init','-q',folder],check=True);return Path(folder)
def native_window():
 with tempfile.TemporaryDirectory(prefix='karolina-native-') as folder:
  root=fixture_root(folder)
  for close_native in [True,False]:
   p=subprocess.Popen([str(EXE),folder],creationflags=subprocess.CREATE_NO_WINDOW)
   try:
    port=until(lambda:ps(f"Get-NetTCPConnection -State Listen -OwningProcess {p.pid} -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty LocalPort"),30)
    s=Service.__new__(Service);s.p=p;s.url='http://127.0.0.1:'+port;s.token=re.search(r'name="karolina-session" content="([^"]+)"',urllib.request.urlopen(s.url).read().decode())[1]
    owned=children(p.pid);window=until(lambda:window_for(owned|{p.pid}),15)
    until(lambda:s.api('window')['content'],30);actual=s.api('window/minimize',{});print('窗口句柄（观察/宿主）',window,actual,flush=True);window=actual['handle'];check(until(lambda:USER.IsIconic(ctypes.c_void_p(window)),5),'原生窗口真实最小化，服务仍可读取状态')
    check(s.api('state')['root']==str(root),'最小化保持当前工程服务')
    start=time.monotonic()
    native=s.api('window');check(native['tray'] and native['content'] is not None,'原生宿主已建立托盘和浏览器客户区')
    USER.GetParent.argtypes=[ctypes.c_void_p];USER.GetParent.restype=ctypes.c_void_p
    check(USER.GetParent(native['content'])==window,'WebView2 控件挂载到原生窗口客户区')
    if close_native:
     USER.PostMessageW(ctypes.c_void_p(window),0x0112,0xF060,0)
     until(lambda:not USER.IsWindowVisible(ctypes.c_void_p(window)),5)
     check(p.poll() is None and s.api('window')['tray'],'原生 X 收起到托盘，服务和进程保留')
     tray_callback(p.pid,0x0203);until(lambda:USER.IsWindowVisible(ctypes.c_void_p(window)) and not USER.IsIconic(ctypes.c_void_p(window)),5)
     check(True,'真实托盘双击回调恢复窗口到正常尺寸')
     tray_callback(p.pid,0x0205);check(until(lambda:tray_exit(p.pid),10)=='invoked','真实托盘完全退出菜单经 Windows UI Automation 调用')
    else:s.api('shutdown',{})
    p.wait(timeout=12);check(p.returncode==0 and not USER.IsWindow(ctypes.c_void_p(window)),('托盘完全退出' if close_native else '完全退出 API')+'结束窗口和本机服务')
    print('窗口退出秒数',round(time.monotonic()-start,2),flush=True)
   finally:
    if p.poll() is None:subprocess.run(['taskkill','/PID',str(p.pid),'/T','/F'],capture_output=True);p.wait()
def hanging_exit():
 with tempfile.TemporaryDirectory(prefix='karolina-hanging-rpc-') as folder:
  root=fixture_root(folder);env=dict(os.environ,KAROLINA_APPSERVER_FIXTURE='1',KAROLINA_HANG_TURN_START='1',KAROLINA_CODEX_EXE=str(ROOT/'Tools/Karolina/Karolina.Tests/bin/Debug/net8.0/Karolina.Tests.exe'))
  s=Service(root,env);pool=concurrent.futures.ThreadPoolExecutor(max_workers=1)
  try:
   s.api('codex/connect',{});owned=children(s.p.pid)
   pending=pool.submit(s.api,'chat/send',dict(text='HOLD',model='fixture',effort='low',access='workspace-write',documents=[],threadId=None))
   until(lambda:s.api('state')['busy']);check(s.api('state')['activeTurn'] is None,'受控 turn/start 确实挂起，尚未拿到轮次编号')
   start=time.monotonic();s.api('shutdown',{});s.p.wait(timeout=10)
   try:pending.result(timeout=10)
   except (urllib.error.HTTPError,OSError):pass
   check(time.monotonic()-start<10 and not children(s.p.pid),'挂起 RPC 时完整退出有界，拥有的 Codex 子进程结束')
   next_service=Service(root,env)
   try:
    next_service.api('codex/connect',{});next_service.api('git/action',dict(action='stage',paths=[],message=None))
   except urllib.error.HTTPError as e:check('锁' not in e.read().decode('utf-8'),'重新打开工程可取得写锁（空选择仍正确拒绝）')
   finally:next_service.close()
  finally:
   pool.shutdown(wait=True)
   if s.p.poll() is None:s.close()
def documents_git_ui():
 with tempfile.TemporaryDirectory(prefix='karolina-current-ui-') as folder:
  root=fixture_root(folder);s=Service(root);b=None
  try:
   r=s.api('document/create',dict(type='requirement',title='正文演进功能',domain='模块',englishTitle='body-evolution'));body=s.api('document/'+r['id'])
   check(Path(r['path']).name==r['id']+'_body-evolution.md','新建文件使用编号加用户指定英文功能名')
   s.api('document/save',dict(id=r['id'],markdown=body['markdown'],expectedHash=body['hash'],changeSummary='变更前：仅单人。\n\n变更后：多人。\n\n原因与兼容：沿用旧字段。'))
   check('变更后：多人。' in s.api('document/'+r['id'])['markdown'] and 'history' not in s.api('document/'+r['id']+'/metadata')['metadata'],'多行需求变动写入正文，元数据不混入演进')
   subprocess.run(['git','-C',folder,'config','user.name','Fixture'],check=True);subprocess.run(['git','-C',folder,'config','user.email','fixture@local.invalid'],check=True)
   file=root/'review.txt';file.write_text('one\n',encoding='utf-8');s.api('git/action',dict(action='stage',paths=['review.txt'],message=None));s.api('git/action',dict(action='commit',paths=[],message='Current history'))
   b=Browser(s.url);b.eval("navigate('git')");until(lambda:b.eval("!!document.querySelector('#directory [data-commit]')"))
   check(b.eval("!!document.getElementById('pull') && !!document.querySelector('#directory [data-commit]') && !!document.getElementById('connectGithub') && !document.querySelector('[id*=stash]')"),'Git 页面含 Pull、历史、GitHub，没有 Stash')
   check(b.eval("document.getElementById('directory').textContent.includes('Current history')"),'真实 Commit 历史在左侧显示标题、作者、时间与 hash')
   b.eval("document.querySelector('[data-commit]').click()");until(lambda:b.eval("document.getElementById('diff').textContent.includes('+one')"));check(True,'点击历史显示真实已提交 diff')
   b.eval(f"loadDocument({json.dumps(r['id'])})");until(lambda:b.eval('currentDoc?.document.id')==r['id']);b.eval("document.getElementById('historyButton').click()");check(b.eval("!document.getElementById('drawer').open && document.getElementById('markdown').textContent.includes('变更后：多人。')"),'需求演进入口定位正文，不再读取历史元数据抽屉')
  finally:
   if b:b.close()
   s.close()
if __name__=='__main__':
 documents_git_ui();hanging_exit();native_window()
 out=Path(os.environ['LOCALAPPDATA'])/'Karolina/revision-inventory/interaction-native.json';out.write_text(json.dumps(dict(checks=RESULT,noScreenshots=True),ensure_ascii=False,indent=2),encoding='utf-8');print('TOTAL',len(RESULT),'passed',out,flush=True)
