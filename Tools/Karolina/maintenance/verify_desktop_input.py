"""真实桌面鼠标/键盘回归；不直接设置 DOM value，不截图，不写生产 Git。"""
import ctypes, json, os, re, subprocess, tempfile, urllib.request
from pathlib import Path
from verify_workbench import Service, until, ROOT, EXE, RESULT, check
from verify_current_additions import ps, USER, tray_callback, tray_exit, children, window_for

def attach(p):
    port=until(lambda:ps(f'Get-NetTCPConnection -State Listen -OwningProcess {p.pid} -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty LocalPort'),25)
    service=Service.__new__(Service);service.p=p;service.url='http://127.0.0.1:'+port
    service.token=re.search(r'name="karolina-session" content="([^"]+)"',urllib.request.urlopen(service.url).read().decode())[1]
    until(lambda:service.api('window')['content'],30)
    return service

def startup_and_isolation():
    env=dict(os.environ,KAROLINA_APPSERVER_FIXTURE='1',KAROLINA_CODEX_EXE=str(ROOT/'Tools/Karolina/Karolina.Tests/bin/Debug/net8.0/Karolina.Tests.exe'))
    with tempfile.TemporaryDirectory(prefix='karolina-window-isolation-') as folder:
        processes=[];services=[]
        try:
            for name in ['first','second']:
                root=Path(folder)/name;root.mkdir();subprocess.run(['git','init','-q',str(root)],check=True)
                p=subprocess.Popen([str(EXE),str(root)],env=env,creationflags=subprocess.CREATE_NO_WINDOW);processes.append(p)
                if name=='first':
                    # 在原生窗口首次出现时立即发送系统关闭；不等待网页初始化。
                    handle=until(lambda:window_for({p.pid}),10)
                    USER.PostMessageW(ctypes.c_void_p(handle),0x0112,0xF060,0)
                    until(lambda:not USER.IsWindowVisible(ctypes.c_void_p(handle)),5)
                    check(p.poll() is None,'启动时立即点击 X 收起窗口，进程继续初始化')
                s=attach(p);services.append(s)
                if name=='first':
                    tray_callback(p.pid,0x0203);until(lambda:USER.IsWindowVisible(ctypes.c_void_p(handle)),5)
                    check(input_action(handle,'chat','startup 恢复')['value']=='startup 恢复','启动期间收起后可由托盘恢复并正常输入')
            owned_first=children(processes[0].pid);owned_second=children(processes[1].pid)
            check(bool(owned_first) and bool(owned_second) and not owned_first.intersection(owned_second),'两个工程使用各自独立的 WebView2 和 Codex 进程')
            services[0].api('shutdown',{});processes[0].wait(timeout=15)
            check(processes[0].returncode==0 and processes[1].poll() is None,'退出第一个工程不终止第二个工作台')
            handle=services[1].api('window')['handle']
            check(input_action(handle,'chat','isolated 独立输入')['value']=='isolated 独立输入','其他工程退出后，剩余工作台仍可真实键盘输入')
            services[1].close();check(processes[1].returncode==0,'剩余工程也可正常完全退出')
        finally:
            for index,p in enumerate(processes):
                if p.poll() is None:
                    try:
                        if index<len(services):services[index].close()
                        else:raise RuntimeError('启动未完成')
                    except Exception:subprocess.run(['taskkill','/PID',str(p.pid),'/T','/F'],capture_output=True);p.wait()

def input_action(handle, mode, text):
    result=subprocess.run(['powershell','-NoProfile','-File',str(ROOT/'Tools/Karolina/maintenance/desktop-input.ps1'),'-WindowHandle',str(handle),'-Mode',mode,'-Text',text],capture_output=True,text=True,encoding='utf-8')
    if result.returncode:raise AssertionError(result.stderr)
    return json.loads(result.stdout.strip().splitlines()[-1])

def run():
    with tempfile.TemporaryDirectory(prefix='karolina-keyboard-') as folder:
        root=Path(folder);subprocess.run(['git','init','-q',folder],check=True)
        env=dict(os.environ,KAROLINA_APPSERVER_FIXTURE='1',KAROLINA_CODEX_EXE=str(ROOT/'Tools/Karolina/Karolina.Tests/bin/Debug/net8.0/Karolina.Tests.exe'))
        setup=Service(root,env)
        try:document=setup.api('document/create',dict(type='requirement',title='真实桌面输入验收',domain='测试'))
        finally:setup.close()
        p=subprocess.Popen([str(EXE),folder],env=env,creationflags=subprocess.CREATE_NO_WINDOW)
        service=None
        try:
            port=until(lambda:ps(f'Get-NetTCPConnection -State Listen -OwningProcess {p.pid} -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty LocalPort'),25)
            service=Service.__new__(Service);service.p=p;service.url='http://127.0.0.1:'+port
            service.token=re.search(r'name="karolina-session" content="([^"]+)"',urllib.request.urlopen(service.url).read().decode())[1]
            until(lambda:service.api('window')['content'],30)
            handle=service.api('window')['handle']
            text='Karolina keyboard 中文输入'
            result=input_action(handle,'probe',text)
            print('原生输入观察',json.dumps(result,ensure_ascii=False),flush=True)
            check(result['input']['value']==text and result['input']['keyboardFocus'],'真实窗口鼠标点击 AI 输入框后，Windows 键盘输入完整进入正文')
            USER.GetWindowLongW.argtypes=[ctypes.c_void_p,ctypes.c_int]
            style=USER.GetWindowLongW(ctypes.c_void_p(handle),-16)
            frames=[]; callback=ctypes.WINFUNCTYPE(ctypes.c_bool,ctypes.c_void_p,ctypes.c_void_p)
            @callback
            def visit(window,param):
                if USER.GetWindowLongW(ctypes.c_void_p(window),-16)&0x00C00000:frames.append(window)
                return True
            USER.EnumChildWindows(ctypes.c_void_p(handle),visit,0)
            check((style&0x00CF0000)==0x00CF0000 and not frames,'只有外层具有标题栏/最小化/最大化/关闭，所有页面子窗口都无标题栏')
            result=input_action(handle,'git','Git commit 中文说明')
            check(result['value']=='Git commit 中文说明' and result['keyboardFocus'],'真实点击 Git 提交说明并输入中英文')
            result=input_action(handle,'document',' Native doc 中文正文')
            check(result['value'].endswith(' Native doc 中文正文') and result['keyboardFocus'],'文档正文编辑使用真实键盘输入')
            until(lambda:service.api('document/'+document['id'])['markdown'].endswith(' Native doc 中文正文'),10)
            check(True,'真实点击保存写入临时文档，输入经过完整保存流程')
            result=input_action(handle,'search','搜索输入')
            check(result['value']=='搜索输入' and result['keyboardFocus'],'目录搜索框真实中英文键盘输入')
            USER.PostMessageW(ctypes.c_void_p(handle),0x0112,0xF030,0)
            until(lambda:USER.IsZoomed(ctypes.c_void_p(handle)),5)
            result=input_action(handle,'git',' max')
            check(result['value']=='Git commit 中文说明 max','原生最大化后输入焦点和已输入内容保持正常')
            USER.PostMessageW(ctypes.c_void_p(handle),0x0112,0xF120,0)
            until(lambda:not USER.IsZoomed(ctypes.c_void_p(handle)),5)
            service.api('window/minimize',{});until(lambda:USER.IsIconic(ctypes.c_void_p(handle)),5)
            service.api('window/restore',{});until(lambda:not USER.IsIconic(ctypes.c_void_p(handle)),5)
            result=input_action(handle,'git',' restore')
            check(result['value']=='Git commit 中文说明 max restore','最小化恢复后可继续输入，不丢原内容')
            USER.PostMessageW(ctypes.c_void_p(handle),0x0112,0xF060,0)
            until(lambda:not USER.IsWindowVisible(ctypes.c_void_p(handle)),5)
            check(p.poll() is None,'X 收起到托盘保留进程')
            tray_callback(p.pid,0x0203);until(lambda:USER.IsWindowVisible(ctypes.c_void_p(handle)),5)
            result=input_action(handle,'git',' tray')
            check(result['value']=='Git commit 中文说明 max restore tray','托盘恢复后真实鼠标与键盘继续可用')
            owned=children(p.pid)
            tray_callback(p.pid,0x0205);check(until(lambda:tray_exit(p.pid),10)=='invoked','真实托盘菜单完整退出')
            p.wait(timeout=12);check(p.returncode==0,'窗口、服务和 Web 页面正常退出')
            live=json.loads(ps("@(Get-CimInstance Win32_Process -Filter \"name='msedgewebview2.exe' OR name='Karolina.Tests.exe'\" | Select-Object -ExpandProperty ProcessId) | ConvertTo-Json -Compress") or '[]')
            check(not owned.intersection(live),'完全退出后本窗口拥有的 WebView2 和 Codex 进程均结束')
        finally:
            if p.poll() is None:
                try:service.api('shutdown',{});p.wait(timeout=15)
                except Exception:subprocess.run(['taskkill','/PID',str(p.pid),'/T','/F'],capture_output=True);p.wait()

if __name__=='__main__':
    USER.IsZoomed.argtypes=[ctypes.c_void_p]
    run()
    startup_and_isolation()
    out=Path(os.environ['LOCALAPPDATA'])/'Karolina/revision-inventory/desktop-input.json'
    out.write_text(json.dumps({'checks':RESULT,'noScreenshots':True,'realWindowKeyboardInput':True},ensure_ascii=False,indent=2),encoding='utf-8')
    print('TOTAL',len(RESULT),'passed',out,flush=True)
