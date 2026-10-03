from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
p=ROOT/'Tools/Karolina/Karolina.Desktop/Web/app.js';t=p.read_text(encoding='utf-8')
def block(start,end,new):
 global t
 a=t.index(start);b=t.index(end,a);t=t[:a]+new+'\n'+t[b:]
t=t.replace("const esc =", "let gitHistory = [], activeCommit = null, gitReading = null, gitWriting = false, lastGitSync = 0, gitSignature = '', historySignature = '', lastEditorDirty = null;\nconst connectionRequests = {codex:null,unity:null}, connectionAttempts = {codex:0,unity:0}, connectionErrors = {codex:'',unity:''};\nconst esc =")
t=t.replace("if(page==='git') { $('directory').innerHTML='<div class=\"empty\">工作区与暂存区的变更在右侧列出。</div>'; return; }", """if(page==='git') {
    for(const c of gitHistory.filter(c=>(c.title+' '+c.author+' '+c.hash).toLowerCase().includes(query))){const b=document.createElement('button');b.classList.toggle('active',activeCommit===c.hash);b.innerHTML=`<span class="title">${esc(c.title)}</span><small>${esc(c.hash.slice(0,8))} · ${esc(c.author)}</small><small>${esc(new Date(c.date).toLocaleString('zh-CN'))}</small>`;b.title=c.hash;b.onclick=()=>showCommit(c);$('directory').append(b);}
    if(!gitHistory.length)$('directory').innerHTML='<div class="empty">尚无提交历史</div>'; return;
  }""")
t=t.replace("$('sectionTitle').textContent=names[next];", "$('sectionTitle').textContent=next==='git'?'提交历史':names[next];$('search').placeholder=next==='git'?'搜索提交标题、作者…':'搜索标题…';$('sidebarNote').textContent=next==='git'?'左侧浏览提交历史；右侧勾选文件、填写说明并直接 Commit。': '参考资料可不选，也可多选。';")
block('function updateControls()', 'function efforts()', """function updateControls() {
  $('send').disabled=!state.codex?.connected||!state.models?.length||state.busy; $('stop').hidden=!state.busy;
  ['model','effort','access','newChat'].forEach(id=>$(id).disabled=!!state.busy || (['model','effort'].includes(id)&&!state.models?.length));
  const count=gitSelection.size;$('commit').disabled=!!state.busy||gitWriting||!count||!$('commitMessage').value.trim();$('commit').textContent=gitWriting?'提交中…':'Commit'+(count?'（'+count+' 个文件）':'');
  $('selectionCount').textContent='已选择 '+count+' 个文件';$('clearGitSelection').disabled=gitWriting||!count;
  for(const id of ['push','pull','connectGithub'])$(id).disabled=!!state.busy||gitWriting;
  document.querySelectorAll('#gitList input').forEach(c=>c.disabled=!!state.busy||gitWriting);
  $('connectCodex').disabled=!!connectionRequests.codex;
}
function syncEditorDirty() {if(lastEditorDirty===docDirty)return;lastEditorDirty=docDirty;api('window/editor',{dirty:docDirty}).catch(()=>{lastEditorDirty=null;});}
function connectProvider(name,manual=false) {
  if(connectionRequests[name])return connectionRequests[name];connectionAttempts[name]=Date.now();connectionErrors[name]='';
  connectionRequests[name]=(async()=>{try{await api(name==='codex'?'codex/connect':'unity',name==='codex'?{}:{action:'connect'});if(manual)toast('已连接 Codex，模型和账号已刷新');}catch(e){connectionErrors[name]=e.message;if(manual)toast(e.message);}finally{connectionRequests[name]=null;await refreshState().catch(()=>{});}})();
  refreshState().catch(()=>{});return connectionRequests[name];
}
function autoConnect() {if(state.busy)return;for(const name of ['codex','unity']){const ready=name==='codex'?state.codex?.connected&&state.models?.length:state.unity?.state==='已连接';if(!ready&&!connectionRequests[name]&&Date.now()-connectionAttempts[name]>15000)connectProvider(name);}}
""")
t=t.replace("Codex ${state.codex.connected?'已连接':'未连接'}", "Codex ${connectionRequests.codex?'连接中…':state.codex.connected?'已连接':connectionErrors.codex?'连接失败':'未连接'}")
t=t.replace("Unity ${esc(state.unity.state)}", "Unity ${connectionRequests.unity?'连接中…':esc(state.unity.state)}")
t=t.replace("$('connectCodex').textContent=state.codex.connected?", "$('codexStatus').title=connectionErrors.codex||state.codex.error||'';$('unityStatus').title=connectionErrors.unity||state.unity.error||'';$('connectCodex').textContent=state.codex.connected?")
block('async function poll()', 'function drawer(', """async function poll() {if(updating)return;updating=true;try{const data=await api('events?after='+eventCursor);if(data.truncated)toast('实时记录已超出缓存；可从对话历史重新载入完整结果。');data.events.forEach(e=>receive(e.value));eventCursor=data.cursor;await refreshState();syncEditorDirty();autoConnect();if(page==='git'&&!document.hidden&&Date.now()-lastGitSync>2000)refreshGit().catch(e=>{$('githubInfo').textContent='Git 自动刷新失败：'+e.message;});}catch(e){$('taskStatus').textContent='服务连接失败';}finally{updating=false;}}
function gitVisible(c) {return c.text||$('showOtherChanges').checked&&c.other;}
async function refreshGit() {
  if(gitReading)return gitReading;lastGitSync=Date.now();
  gitReading=(async()=>{const request=++gitRequest;const [data,history]=await Promise.all([api('git'),api('git/history')]);if(request!==gitRequest)return;
    gitChanges=data.changes;gitUpstream=data.upstream;
    $('connectGithub').textContent=data.connection.authenticated?'GitHub · 已连接':'GitHub · 连接';
    $('githubInfo').textContent=(data.connection.accounts.length?'GitHub · '+data.connection.accounts.join('、')+' · ':'')+data.connection.message;
    $('gitLockWarning').hidden=!data.indexLock.exists;$('gitLockText').textContent=data.indexLock.exists?`Git 索引被锁定（${data.indexLock.bytes} 字节；${data.indexLock.gitRunning?'仍有 Git 进程':'未发现 Git 进程'}）。`:'';$('recoverGitLock').disabled=data.indexLock.gitRunning||data.indexLock.bytes!==0;
    $('push').title=gitUpstream?'推送到 '+gitUpstream:'请先配置当前分支的上游';$('branch').textContent='⑂ '+(data.branch||'游离 HEAD');
    const currentPaths=new Set(gitChanges.map(c=>c.path));for(const path of gitSelection)if(!currentPaths.has(path))gitSelection.delete(path);
    const signature=JSON.stringify(gitChanges);if(signature!==gitSignature){gitSignature=signature;renderGit();}
    const hs=JSON.stringify(history);gitHistory=history;if(hs!==historySignature){historySignature=hs;if(page==='git')renderDirectory();}
    updateControls();
    if(diffSelection&&!activeCommit){const c=gitChanges.find(c=>c.path===diffSelection.c.path);if(c&&gitVisible(c))await showDiff(c);else clearDiff();}
  })();try{return await gitReading;}finally{gitReading=null;}
}
function clearDiff() {++diffRequest;diffSelection=null;activeCommit=null;$('diffTitle').textContent='选择文件或历史提交查看差异';$('diff').innerHTML='<div class="empty">当前文件已无差异。</div>';}
function renderGit() {
  const scroll=$('gitList').scrollTop;$('gitList').replaceChildren();const q=$('gitSearch').value.toLowerCase();const list=gitChanges.filter(c=>c.path.toLowerCase().includes(q)&&gitVisible(c));
  for(const c of list){const row=document.createElement('div');row.className='git-file';row.classList.toggle('active',!activeCommit&&diffSelection?.c.path===c.path);const check=document.createElement('input');check.type='checkbox';check.checked=gitSelection.has(c.path);check.disabled=!!state.busy||gitWriting;check.setAttribute('aria-label','提交 '+c.path);check.onchange=()=>{check.checked?gitSelection.add(c.path):gitSelection.delete(c.path);updateControls();};const b=document.createElement('button');b.textContent=c.path;b.title=c.originalPath?`${c.originalPath} → ${c.path}`:c.path;b.onclick=()=>showDiff(c);const badge=document.createElement('small');badge.textContent=c.originalPath?'重命名':c.status.includes('D')?'删除':c.status==='??'||c.status.includes('A')?'新增':'修改';row.append(check,b,badge);$('gitList').append(row);}
  if(!list.length)$('gitList').innerHTML='<div class="empty">'+(gitChanges.length?'没有匹配的文本差异；其它变更可从开关查看。':'工作区干净')+'</div>';
  $('gitList').scrollTop=scroll;
}
function paintDiff(text) {if($('diff').dataset.patch===text)return;$('diff').dataset.patch=text;let old=0,now=0;const scroll=$('diff').scrollTop;$('diff').replaceChildren();for(const line of text.replace(/\\r\\n/g,'\\n').split('\\n')){const h=line.match(/^@@ -(\\d+)(?:,\\d+)? \\+(\\d+)/);if(h){old=+h[1];now=+h[2];}const type=h?'hunk':line.startsWith('+')&&!line.startsWith('+++')?'added':line.startsWith('-')&&!line.startsWith('---')?'deleted':'';const r=document.createElement('div');r.className='diff-line '+type;const a=document.createElement('span'),b=document.createElement('span'),t=document.createElement('span');a.className=b.className='number';if(type==='added')b.textContent=now++;else if(type==='deleted')a.textContent=old++;else if(!h&&line.startsWith(' ')){a.textContent=old++;b.textContent=now++;}t.className='text';t.textContent=line;r.append(a,b,t);$('diff').append(r);}$('diff').scrollTop=scroll;}
async function showDiff(c) {const request=++diffRequest;activeCommit=null;diffSelection={c};renderDirectory();try{const data=await api(`git/diff?path=${encodeURIComponent(c.path)}&area=combined`);if(request!==diffRequest||page!=='git')return;$('diffTitle').textContent=(c.originalPath?`${c.originalPath} → `:'')+c.path;paintDiff(data.diff);}catch(e){if(request===diffRequest)toast(e.message);}}
async function showCommit(c) {const request=++diffRequest;activeCommit=c.hash;diffSelection=null;renderDirectory();try{const data=await api('git/commit/'+c.hash);if(request!==diffRequest||page!=='git')return;$('diffTitle').textContent=c.hash.slice(0,8)+' · '+c.title;paintDiff(data.diff);}catch(e){if(request===diffRequest)toast(e.message);}}
""")
t=t.replace("$('showOtherChanges').onchange=()=>{gitSelection.clear();renderGit();updateControls();if(diffSelection&&!gitVisible(diffSelection.c,diffSelection.area))clearDiff();}","$('showOtherChanges').onchange=()=>{for(const path of gitSelection)if(!gitVisible(gitChanges.find(c=>c.path===path)))gitSelection.delete(path);renderGit();updateControls();if(diffSelection&&!gitVisible(diffSelection.c))clearDiff();}")
t=t.replace("$('documentSource').oninput=()=>{docDirty=true;++editGeneration;};", "$('documentSource').oninput=()=>{docDirty=true;++editGeneration;syncEditorDirty();};$('commitMessage').oninput=updateControls;$('clearGitSelection').onclick=()=>{gitSelection.clear();renderGit();updateControls();};")
t=t.replace("action('connectCodex',async()=>{await api('codex/connect',{});await refreshState();toast('已连接 Codex，模型和账号已刷新');});", "action('connectCodex',()=>connectProvider('codex',true));")
a=t.index("for(const id of ['stage','unstage'])");b=t.index("action('connectGithub'",a)
t=t[:a]+"""action('commit',async()=>{const paths=[...gitSelection];if(!paths.length)throw new Error('请勾选要提交的文件');const message=$('commitMessage').value.trim();if(!message)throw new Error('请填写提交说明');gitWriting=true;updateControls();try{const r=await api('git/action',{action:'commit-selected',paths,message});for(const p of paths)gitSelection.delete(p);$('commitMessage').value='';toast(r.result||'所选文件已提交');await refreshGit();}finally{gitWriting=false;renderGit();updateControls();}});
"""+t[b:]
a=t.index("action('gitHistory'");b=t.index("action('push'",a);t=t[:a]+t[b:]
t=t.replace("englishDomain:$('newEnglishDomain').value,englishDomain:$('newEnglishDomain').value", "englishDomain:$('newEnglishDomain').value")
t=t.replace("renderDirectory();}catch(e){toast(e.message);}setInterval(poll,1000)", "renderDirectory();autoConnect();}catch(e){toast(e.message);}window.addEventListener('focus',()=>{if(page==='git')refreshGit().catch(e=>toast(e.message));autoConnect();});setInterval(poll,1000)")
p.write_text(t,encoding='utf-8')
p=ROOT/'Tools/Karolina/Karolina.Desktop/Web/index.html';t=p.read_text(encoding='utf-8');a=t.index('<div class="window-actions">');b=t.index('</header>',a);t=t[:a]+t[b:]
t=t.replace('<button id="gitHistory">提交历史</button>','')
a=t.index('<div class="git-selection-tools">');b=t.index('<div class="git-layout">',a)
t=t[:a]+'''<div class="git-selection-tools"><span id="selectionCount">已选择 0 个文件</span><button id="clearGitSelection">清空选择</button><label><input id="showOtherChanges" type="checkbox"> 显示二进制与结构变更</label></div>'''+t[b:]
t=t.replace('Commit · 提交到本地','Commit').replace('加入本次提交前后可分别审查。','勾选文件后填写说明即可提交，左侧可浏览提交历史。')
p.write_text(t,encoding='utf-8')
print('已改为直接提交、自动刷新、左侧历史和自动连接')
