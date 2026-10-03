from revise_current import ROOT,replace
p='Tools/Karolina/Karolina.Desktop/Web/app.js'
replace(p,"$('commit').textContent='提交暂存区'", "$('commit').textContent='Commit · 提交到本地'")
replace(p,"$('push').disabled=!!state.busy;", "$('push').disabled=!!state.busy; $('pull').disabled=!!state.busy; $('connectGithub').disabled=!!state.busy;")
replace(p,"gitChanges=data.changes;gitUpstream=data.upstream;", """gitChanges=data.changes;gitUpstream=data.upstream;
  $('connectGithub').textContent=data.connection.authenticated?'GitHub · 已连接':'GitHub · 连接';
  $('githubInfo').textContent=(data.connection.accounts.length?'GitHub · '+data.connection.accounts.join('、')+' · ':'')+data.connection.message+(data.connection.remote?' · '+data.connection.remote:'');
  $('gitLockWarning').hidden=!data.indexLock.exists;
  $('gitLockText').textContent=data.indexLock.exists?`Git 索引被锁定（${data.indexLock.bytes} 字节；${data.indexLock.gitRunning?'仍有 Git 进程':'未发现 Git 进程'}）。` : '';
  $('recoverGitLock').disabled=data.indexLock.gitRunning||data.indexLock.bytes!==0;
  """)
replace(p,"[['worktree','工作区'],['index','暂存区']]", "[['worktree','尚未加入本次提交'],['index','本次提交内容（Git 索引）']]")
f=ROOT/p;t=f.read_text(encoding='utf-8')
t=t.replace("['已关闭','已取消']","['关闭']").replace("({草案:['草案','执行中','已取消'],执行中:['执行中','暂缓','待验收','已取消'],暂缓:['暂缓','执行中','已取消'],待验收:['待验收','执行中','已关闭','已取消']})","({准备:['准备','执行'],执行:['执行','测试'],测试:['测试','校正','验收'],校正:['校正','执行','测试'],验收:['验收','校正','关闭']})")
start=t.index("$('historyButton').onclick=");end=t.index('\n',start)
t=t[:start]+"$('historyButton').onclick=()=>{if(!currentDoc||currentDoc.document.type!=='requirement')return;if(docDirty){toast('演进属于正文，请保存或在编辑器定位「需求演进」段落。');return;}$('readMode').click();const h=[...$('markdown').querySelectorAll('h2')].find(h=>h.textContent==='需求演进');if(h)h.scrollIntoView({behavior:'smooth',block:'start'});else toast('正文尚未记录需求演进；下一次语义变更时补充。');};"+t[end:]
t=t.replace('<label>模块<input id="newDomain"','<label>英文文件标题<input id="newEnglishTitle" placeholder="例如 document-lifecycle（编号自动生成）"></label><label>模块<input id="newDomain"').replace("domain:$('newDomain').value", "domain:$('newDomain').value,englishTitle:$('newEnglishTitle').value")
t=t.replace('草案','准备').replace('已关闭','关闭')
t=t.replace("action('push',async()=>", """action('connectGithub',async()=>{toast('正在连接 GitHub；需要授权时请在浏览器完成。');const r=await api('git/action',{action:'connect-github',paths:[]});toast(r.result);await refreshGit();});
action('recoverGitLock',async()=>{const r=await api('git/action',{action:'recover-lock',paths:[]});toast(r.result);await refreshGit();});
action('pull',async()=>{await refreshGit();if(!gitUpstream)throw new Error('请先在 Git 工具配置上游');if(!confirm('从 '+gitUpstream+' 拉取当前分支？工作区必须干净；只允许快进。'))return;const r=await api('git/action',{action:'pull',paths:[]});toast(r.result||'拉取完成');await refreshGit();});
action('gitHistory',async()=>{const history=await api('git/history');drawer('提交历史 · 最近 100 条',history.map(c=>`<button class="history-commit" data-commit="${esc(c.hash)}"><strong>${esc(c.title)}</strong><small>${esc(c.hash.slice(0,8))} · ${esc(c.author)} · ${esc(c.date)}</small></button>`).join('')||'<p>尚无提交。</p>');document.querySelectorAll('[data-commit]').forEach(b=>b.onclick=async()=>{try{const request=++diffRequest;const d=await api('git/commit/'+b.dataset.commit);if(request!==diffRequest||page!=='git')return;$('drawer').close();diffSelection=null;$('diffTitle').textContent='提交 · '+b.dataset.commit.slice(0,8);$('diff').replaceChildren();for(const line of d.diff.split('\\n')){const r=document.createElement('div');r.className='history-diff-line '+(line.startsWith('+')?'added':line.startsWith('-')?'deleted':'');r.textContent=line;$('diff').append(r);}}catch(e){toast(e.message);}});});
action('minimizeWindow',async()=>{const r=await api('window/minimize',{});if(!r.minimized)toast('当前为浏览器服务模式，请使用浏览器窗口最小化。');});
action('exitApp',async()=>{if(docDirty&&!confirm('正文尚未保存，放弃编辑并完全退出？'))return;if(state.busy&&!confirm('Codex 正在执行，完全退出会终止子进程。已写入的文件保留，需要随后核查。继续退出？'))return;await api('shutdown',{});$('taskStatus').textContent='已退出';});
action('push',async()=>""")
f.write_text(t,encoding='utf-8')
css=ROOT/'Tools/Karolina/Karolina.Desktop/Web/style.css'
css.write_text(css.read_text(encoding='utf-8')+'\n.window-actions{display:flex;gap:4px;margin-left:auto}.window-actions button{font-size:20px;min-width:36px}.git-info,.git-lock-warning,.git-selection-tools{padding:8px 18px;font-size:12px;display:flex;gap:10px;align-items:center;flex-wrap:wrap}.git-info{color:var(--muted);overflow-wrap:anywhere}.git-lock-warning{background:#fff1d5;color:#604200}.history-commit{display:flex;flex-direction:column;gap:5px;width:100%;text-align:left;padding:12px;border-bottom:1px solid var(--border)}.history-diff-line{white-space:pre-wrap;overflow-wrap:anywhere;font:12px/1.6 monospace;padding:0 12px}.history-diff-line.added{background:#e4f3e8;color:#18562d}.history-diff-line.deleted{background:#fae8e8;color:#952d2d}.change-summary textarea{width:100%;resize:vertical}.commit-box small{display:block;color:var(--muted);font-size:11px;margin-top:8px}\n',encoding='utf-8')
