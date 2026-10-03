"""自动连接、直接所选提交、自动差异；仅临时工程，无截图。"""
import json, os, socket, subprocess, tempfile, threading, time
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from verify_workbench import Service, Browser, check, until, RESULT, ROOT

def git(root, *args):
    return subprocess.run(['git', '-C', str(root), *args], check=True, capture_output=True, text=True, encoding='utf-8').stdout

def run():
    with tempfile.TemporaryDirectory(prefix='karolina-direct-') as folder:
        root = Path(folder)
        git(root, 'init', '-q'); git(root, 'config', 'user.name', 'Fixture'); git(root, 'config', 'user.email', 'fixture@local.invalid')
        (root/'.git/info/exclude').write_text('Docs/\n.mcp.json\n', encoding='utf-8')
        files = [f'file {i}.txt' for i in range(6)]
        for name in files: (root/name).write_text('baseline\n', encoding='utf-8')
        git(root, 'add', '--', *files); git(root, 'commit', '-qm', 'Baseline')
        (root/files[5]).write_text('untouched staged\n', encoding='utf-8'); git(root, 'add', '--', files[5])
        for name in files[:5]: (root/name).write_text('baseline\nchanged\n', encoding='utf-8')
        with socket.socket() as sock: sock.bind(('127.0.0.1',0)); port=sock.getsockname()[1]
        (root/'.mcp.json').write_text(json.dumps({'mcpServers':{'ai-game-developer':{'url':f'http://127.0.0.1:{port}/mcp'}}}), encoding='utf-8')
        env=dict(os.environ, KAROLINA_APPSERVER_FIXTURE='1', KAROLINA_CODEX_EXE=str(ROOT/'Tools/Karolina/Karolina.Tests/bin/Debug/net8.0/Karolina.Tests.exe'))
        s=Service(root, env); b=None; server=None; calls=[]
        try:
            s.api('document/create', dict(type='requirement', title='交互验收', domain='测试', englishTitle='interaction'))
            b=Browser(s.url)
            until(lambda:s.api('state')['codex']['connected'],15)
            check(until(lambda:b.eval("document.getElementById('model').options.length>0"),10),'启动无需点击连接即取得 Codex 模型')
            until(lambda:s.api('state')['unity']['state']=='连接失败',15)
            check(True,'启动自动尝试离线 Unity，并显示真实连接失败')
            class Mcp(BaseHTTPRequestHandler):
                def log_message(self,*args): pass
                def do_POST(self):
                    data=json.loads(self.rfile.read(int(self.headers['Content-Length'])))
                    method=data['method']; calls.append(method)
                    if 'id' not in data: self.send_response(202); self.end_headers(); return
                    if method=='initialize': result={'protocolVersion':'2025-06-18','capabilities':{},'serverInfo':{'name':'ControlledUnity','version':'1'}}
                    elif method=='tools/list': result={'tools':[{'name':'script-execute','inputSchema':{'type':'object'}}]}
                    elif method=='tools/call': result={'structuredContent':{'value':str(root/'Assets')}}
                    else: raise AssertionError(method)
                    body=json.dumps({'jsonrpc':'2.0','id':data['id'],'result':result}).encode()
                    self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
            server=ThreadingHTTPServer(('127.0.0.1',port),Mcp);threading.Thread(target=server.serve_forever,daemon=True).start()
            until(lambda:s.api('state')['unity']['state']=='已连接',25)
            check('tools/call' in calls,'Unity 服务稍后开启，自动重试连接并核对工程路径')
            b.eval("navigate('git')");until(lambda:b.eval('gitChanges.length')==6,15)
            paths=json.dumps(files[:5]); b.eval(f"for(const p of {paths}){{document.querySelector('input[aria-label=\"提交 '+p+'\"]').click();}}document.getElementById('commitMessage').value='Commit five selected';document.getElementById('commitMessage').dispatchEvent(new Event('input'));")
            check(b.eval("!document.getElementById('commit').disabled && gitSelection.size===5 && !document.getElementById('stage') && !document.getElementById('unstage')"),'勾选五个文件并填说明即可 Commit，没有加入/移除暂存按钮')
            b.eval('refreshGit()')
            check(b.eval("gitSelection.size===5 && document.querySelectorAll('#gitList input:checked').length===5"),'刷新仍保留五个文件的勾选且文件留在统一列表')
            b.eval(f"showDiff(gitChanges.find(c=>c.path==={json.dumps(files[0])}))")
            until(lambda:b.eval("document.getElementById('diff').textContent.includes('+changed')"),10)
            (root/files[0]).write_text('baseline\nchanged\nlatest\n', encoding='utf-8')
            until(lambda:b.eval("document.getElementById('diff').textContent.includes('+latest')"),15)
            check(True,'外部编辑后自动更新所选 diff，无需手动刷新')
            lock=root/'.git/index.lock';lock.write_text('',encoding='utf-8');b.eval("document.getElementById('commit').click()")
            until(lambda:b.eval("!gitWriting && document.getElementById('toast').textContent.includes('index.lock')"),10)
            check(b.eval("gitSelection.size===5 && document.getElementById('commitMessage').value==='Commit five selected'"),'提交失败保留选择与说明，可修复后重试')
            lock.unlink(); b.eval("document.getElementById('commit').click()")
            until(lambda:git(root,'log','-1','--format=%s').strip()=='Commit five selected',10)
            changed=git(root,'diff-tree','--no-commit-id','--name-only','-r','HEAD').splitlines()
            check(set(changed)==set(files[:5]),'真实提交恰好五个所选文件，包含当前最新正文')
            check(git(root,'diff','--cached','--name-only').splitlines()==[files[5]],'未选的既有暂存文件没有被提交且索引内容保留')
            until(lambda:b.eval("document.getElementById('directory').textContent.includes('Commit five selected')"),15)
            check(b.eval('gitSelection.size===0 && gitChanges.length===1'),'提交后自动更新左侧历史及文件列表，清除已提交选择')
            b.eval("document.querySelector('#directory [data-commit]').click()")
            until(lambda:b.eval("document.getElementById('diff').textContent.includes('+latest')"),10)
            (root/'external.txt').write_text('external\n',encoding='utf-8')
            try:until(lambda:b.eval("gitChanges.some(c=>c.path==='external.txt')"),15)
            except Exception:
                print('AUTO DEBUG',b.eval("JSON.stringify({page,updating,hidden:document.hidden,reading:!!gitReading,writing:gitWriting,sync:lastGitSync,now:Date.now(),info:document.getElementById('githubInfo').textContent,status:document.getElementById('taskStatus').textContent})"),s.api('git'),flush=True);raise
            check(b.eval("activeCommit!==null && document.getElementById('diffTitle').textContent.includes('Commit five selected')"),'自动新增文件刷新不覆盖正在审查的历史 diff')
            b.eval(f"showDiff(gitChanges.find(c=>c.path==={json.dumps(files[5])}))")
            until(lambda:b.eval("document.getElementById('diff').textContent.includes('+untouched staged')"),10)
            (root/files[5]).write_text('baseline\n',encoding='utf-8')
            until(lambda:b.eval('diffSelection===null'),15)
            check(b.eval('gitChanges.length===1'),'索引有变化但当前正文与 HEAD 相同的文件不显示')
            (root/files[5]).write_text('untouched staged\n',encoding='utf-8')
            until(lambda:b.eval('gitChanges.length===2'),15)
            b.eval(f"showDiff(gitChanges.find(c=>c.path==={json.dumps(files[5])}))")
            until(lambda:b.eval("document.getElementById('diff').textContent.includes('+untouched staged')"),10)
            check(True,'恢复相同补丁后正常显示，清空差异没有残留缓存')
            # 人为延迟 dirty=false 回执，验证新输入仍串行成为最终状态。
            b.eval("window.baseApi=api;window.sentDirty=[];api=async(p,d)=>{if(p==='window/editor'){sentDirty.push(d.dirty);if(!d.dirty)await new Promise(r=>setTimeout(r,300));}return baseApi(p,d);};lastEditorDirty=true;docDirty=false;syncEditorDirty();docDirty=true;syncEditorDirty();")
            until(lambda:b.eval('!editorSyncing'),10)
            check(b.eval('JSON.stringify(sentDirty)==="[false,true]" && lastEditorDirty===true'),'未保存状态按顺序同步，新输入不会被旧保存回执覆盖')
            b.eval('api=baseApi;docDirty=false;syncEditorDirty()')
        finally:
            if b:b.close()
            s.close()
            if server:server.shutdown();server.server_close()

if __name__=='__main__':
    run()
    out=Path(os.environ['LOCALAPPDATA'])/'Karolina/revision-inventory/direct-interaction.json'
    out.write_text(json.dumps({'checks':RESULT,'noScreenshots':True,'temporaryRepositoriesOnly':True},ensure_ascii=False,indent=2),encoding='utf-8')
    print('TOTAL',len(RESULT),'passed',out,flush=True)
