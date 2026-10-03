"use strict";
const $ = id => document.getElementById(id), session = document.querySelector('meta[name="karolina-session"]').content;
let page = 'chat', docs = [], state = {}, selectedDocs = new Set(), threadId = null, eventCursor = 0, currentDoc = null, lastMessage = new Map(), toolMessages = new Map(), modelSignature = '', updating = false, approvalQueue = new Map(), docDirty = false, documentRequest = 0, documentCache = new Map(), diffRequest = 0, editGeneration = 0;
let lastEditorDirty = null, editorSyncing = null;
const connectionRequests = {codex:null,unity:null}, connectionAttempts = {codex:0,unity:0}, connectionErrors = {codex:'',unity:''};
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function toast(text) { $('toast').textContent = text; $('toast').hidden = false; clearTimeout(toast.timer); toast.timer = setTimeout(() => $('toast').hidden = true, 6500); }
async function api(path, body) { const r = await fetch('/api/' + path, { method: body === undefined ? 'GET':'POST', headers: { 'X-Karolina-Session':session, 'Content-Type':'application/json' }, body: body === undefined ? undefined:JSON.stringify(body) }); let data; try { data = await r.json(); } catch { throw new Error(`服务返回 ${r.status}`); } if (!r.ok) throw new Error(data.error || `操作失败 ${r.status}`); return data; }
function action(id, fn) { $(id).addEventListener('click', async () => { const b = $(id); b.disabled = true; try { await fn(); } catch(e) { toast(e.message); } finally { b.disabled = false; updateControls(); } }); }
function inline(raw) {
  const tokens = []; const hold = html => { tokens.push(html); return `\u0001${tokens.length-1}\u0001`; };
  let s = esc(raw).replace(/`([^`]+)`/g, (_, code) => hold(`<code>${code}</code>`));
  s = s.replace(/\[([^\]]+)\]\(([^)\n]+)\)/g, (_, title, href) => {
    let decoded = href.trim().replace(/^&lt;|&gt;$/g,'').replace(/&amp;/g,'&');try{decoded=decodeURIComponent(decoded);}catch{}
    if (/^https?:\/\//i.test(decoded)) return hold(`<a href="${esc(decoded)}" target="_blank" rel="noopener noreferrer">${title}</a>`);
    if (decoded.startsWith('#')) return hold(`<a href="${esc(decoded)}">${title}</a>`);
    let entry = docs.find(d => d.path === decoded || d.path.endsWith('/'+decoded.replace(/^(?:\.\.\/)+/,'')) || d.title === title);
    return entry ? hold(`<a href="#document:${encodeURIComponent(entry.id)}" data-document="${esc(entry.id)}">${title}</a>`) : `${title} <code>${esc(decoded)}</code>`;
  });
  s = s.replace(/\*\*([^*]+)\*\*/g,'<strong>$1</strong>').replace(/~~([^~]+)~~/g,'<del>$1</del>').replace(/(?<!\*)\*([^*]+)\*(?!\*)/g,'<em>$1</em>');
  return s.replace(/\u0001(\d+)\u0001/g, (_, n) => tokens[Number(n)]);
}
function markdown(text) {
  const lines = String(text).replace(/\r\n/g,'\n').split('\n'), out = []; let i = 0, heading = 0;
  const cells = l => l.trim().replace(/^\|/,'').replace(/\|$/,'').split(/(?<!\\)\|/).map(x=>x.trim());
  const block = l => /^(#{1,6}\s|\s*[-*+]\s|\s*\d+[.)]\s|>|```|~~~|\$\$|\s*$|---+$)/.test(l);
  while (i < lines.length) {
    let l = lines[i]; if (!l.trim()) { i++; continue; }
    const fence = l.match(/^(```+|~~~+)(.*)$/);
    if (fence) { const code=[]; i++; while (i<lines.length && !lines[i].startsWith(fence[1])) code.push(lines[i++]); i++; out.push(`<pre><code>${esc(code.join('\n'))}</code></pre>`); continue; }
    if (l.trim()==='$$') { const math=[]; i++; while(i<lines.length && lines[i].trim()!=='$$') math.push(lines[i++]); i++; out.push(`<div class="math">${esc(math.join('\n'))}</div>`); continue; }
    let h = l.match(/^(#{1,6})\s+(.+?)\s*#*$/); if(h) { let n=h[1].length; out.push(`<h${n} id="heading-${heading++}">${inline(h[2])}</h${n}>`); i++; continue; }
    if (/^\s*(---+|\*\*\*+)\s*$/.test(l)) { out.push('<hr>'); i++; continue; }
    if (i+1<lines.length && l.includes('|') && /^\s*\|?\s*:?-{3,}/.test(lines[i+1])) {
      const headers=cells(l); out.push('<table><thead><tr>'+headers.map(c=>`<th>${inline(c)}</th>`).join('')+'</tr></thead><tbody>'); i+=2;
      while(i<lines.length && lines[i].includes('|') && lines[i].trim()) { out.push('<tr>'+cells(lines[i++]).map(c=>`<td>${inline(c)}</td>`).join('')+'</tr>'); } out.push('</tbody></table>'); continue;
    }
    if (/^>/.test(l)) { const quote=[]; while(i<lines.length && /^>/.test(lines[i])) quote.push(lines[i++].replace(/^>\s?/,'')); out.push('<blockquote>'+markdown(quote.join('\n'))+'</blockquote>'); continue; }
    if (/^\s*([-*+]\s|\d+[.)]\s)/.test(l)) {
      const ordered=/^\s*\d/.test(l), tag=ordered?'ol':'ul'; out.push('<'+tag+'>');
      while(i<lines.length && /^\s*([-*+]\s|\d+[.)]\s)/.test(lines[i])) { let content=lines[i++].replace(/^\s*([-*+]\s|\d+[.)]\s)/,''); let check=content.match(/^\[([ xX])\]\s/); if(check) content=content.slice(4); out.push('<li>'+(check?`<input type="checkbox" disabled ${check[1]!==' '?'checked':''}> `:'')+inline(content)+'</li>'); }
      out.push('</'+tag+'>'); continue;
    }
    const paragraph=[l]; i++; while(i<lines.length && !block(lines[i]) && !(i+1<lines.length && lines[i].includes('|') && /^\s*\|?\s*:?-{3,}/.test(lines[i+1]))) paragraph.push(lines[i++]); out.push('<p>'+paragraph.map(inline).join('<br>')+'</p>');
  }
  return out.join('\n');
}
window.KarolinaMarkdown = markdown; // 供 DOM 行为验收调用；不接受原始 HTML。
function renderDirectory() {
  const query=$('search').value.toLowerCase(); $('directory').replaceChildren();
  if(page==='chat') { for(const c of state.chats||[]) if(c.title.toLowerCase().includes(query)) { const b=document.createElement('button'); b.textContent=c.title; b.classList.toggle('active',c.id===threadId); b.onclick=()=>loadChat(c.id); $('directory').append(b); } return; }
  if(page==='review') {renderReviewDirectory(query);return;}
  if(page==='rule'){renderRuleDirectory(query);return;}
  if(page==='tools'){renderToolDirectory(query);return;}
  let group=''; for(const d of docs.filter(d=>(d.type===page || page==='rule' && ['engineering','resource','guide'].includes(d.type)) && d.title.toLowerCase().includes(query))) { if(page!=='plan' && d.domain!==group) { group=d.domain; const h=document.createElement('div'); h.className='group'; h.textContent=group; $('directory').append(h); } const b=document.createElement('button'); b.innerHTML=`<span class="title">${esc(d.title)}</span>${d.status?`<span class="status">${esc(d.status)}</span>`:''}`; b.classList.toggle('active',currentDoc?.document.id===d.id); b.onclick=()=>loadDocument(d.id); $('directory').append(b); }
}
function navigate(next) {
  if(reviewSaving){toast('正在保存审批，请稍候');return false;}
  if(next!==page && currentDoc && docDirty){if(!confirm('正文尚未保存，放弃当前编辑？'))return false;$('documentSource').value=currentDoc.markdown;docDirty=false;}
  // 切换页面保留审批草稿及脏标志；换任务时才明确确认放弃并重新载入。
  page=next; const names={chat:'AI 对话',requirement:'需求案',plan:'计划案',rule:'规则案',review:'任务审批',tools:'拓展工具'};
  $('sectionTitle').textContent=names[next];$('search').placeholder='搜索标题…';$('sidebarNote').textContent=next==='review'?'本次任务开始前为基线；驳回意见返回执行，不自动回滚。':'参考资料可不选，也可多选。';$('pageEyebrow').textContent=names[next];
  $('pageTitle').textContent=next==='review'&&currentReview?currentReview.title:next==='tools'&&selectedTool?selectedTool.definition.title:['requirement','plan','rule'].includes(next)&&currentDoc?currentDoc.document.title:names[next];
  $('chatActions').hidden=next!=='chat';$('newChat').hidden=next!=='chat'; $('newDocument').hidden=!['requirement','plan','rule'].includes(next);
  $('chatPage').hidden=next!=='chat';$('documentPage').hidden=!['requirement','plan','rule'].includes(next);$('reviewPage').hidden=next!=='review';$('toolsPage').hidden=next!=='tools';rememberWorkspace();
  document.querySelectorAll('[data-page]').forEach(b=>b.classList.toggle('active',b.dataset.page===next));$('search').value='';renderDirectory();
  if(next==='tools')refreshTools().catch(e=>toast(e.message));
  if(next==='review')refreshReviews().then(()=>{if(!currentReview&&reviewTasks.length)openReview(reviewTasks[0].id);}).catch(e=>toast(e.message)); return true;
}
function paintDocument(data) {
  currentDoc=data;docDirty=false;const d=data.document;
  navigate(['requirement','plan','rule'].includes(d.type)?d.type:'rule');$('pageTitle').textContent=d.title;
  $('documentStatus').textContent=d.status||'';$('documentStatus').hidden=!d.status;$('metadataButton').disabled=false;$('historyButton').hidden=d.type!=='requirement';
  $('sourceMode').disabled=d.type==='plan'&&['关闭'].includes(d.status);$('sourceMode').title=$('sourceMode').disabled?'计划已结束，后续工作请新建计划':'';
  $('markdown').innerHTML=data.rendered||(data.rendered=markdown(data.markdown));$('markdown').scrollTop=0;$('documentSource').value=data.markdown;
  $('markdown').hidden=false;$('documentSource').hidden=true;$('saveDocument').hidden=true;$('changeSummaryRow').hidden=true;$('changeSummary').value='';
  $('readMode').classList.add('selected');$('sourceMode').classList.remove('selected');
  rememberWorkspace();$('documentToc').innerHTML=[...$('markdown').querySelectorAll('h2,h3')].map(h=>`<a href="#${h.id}">${esc(h.textContent)}</a>`).join('');renderDirectory();
}
async function loadDocument(id) {
  const request=++documentRequest;
  try {
    const guideTool=extensionTools.find(t=>t.definition.guideId===id);if(guideTool){navigate('tools');await openTool(guideTool);return;}
    if(currentDoc&&id!==currentDoc.document.id&&docDirty){if(!confirm('正文尚未保存，放弃当前编辑？'))return;docDirty=false;$('documentSource').value=currentDoc.markdown;}
    const cached=documentCache.get(id);if(cached)paintDocument(cached);
    const fresh=await api('document/'+encodeURIComponent(id));documentCache.set(id,fresh);
    if(documentCache.size>32)documentCache.delete(documentCache.keys().next().value);
    if(request!==documentRequest||docDirty)return;
    if(!cached||fresh.hash!==cached.hash||JSON.stringify(fresh.document)!==JSON.stringify(cached.document))paintDocument(fresh);else currentDoc={...fresh,rendered:cached.rendered};
  }catch(e){toast(e.message);}
}
async function documentMetadata() {if(!currentDoc)return null;const id=currentDoc.document.id,data=await api('document/'+encodeURIComponent(id)+'/metadata');return currentDoc?.document.id===id?data:null;}
function renderReferences() { const q=$('referenceSearch').value.toLowerCase(); $('referenceList').replaceChildren(); for(const d of docs.filter(d=>['requirement','plan'].includes(d.type)&&d.title.toLowerCase().includes(q))) { const l=document.createElement('label'), c=document.createElement('input'), text=document.createElement('span'); c.type='checkbox'; c.checked=selectedDocs.has(d.id); c.onchange=()=>{c.checked?selectedDocs.add(d.id):selectedDocs.delete(d.id); $('referenceCount').textContent=selectedDocs.size?`${selectedDocs.size} 份`:'未选择';}; text.innerHTML=`${esc(d.title)} <small>· ${esc(d.status)}</small>`; l.append(c,text); $('referenceList').append(l); } $('referenceCount').textContent=selectedDocs.size?`${selectedDocs.size} 份`:'未选择';renderReviewPlans(); }
function addMessage(role,text,id) { $('welcome')?.remove(); const m=document.createElement('div'); m.className='message '+role; const b=document.createElement('div'); b.className='bubble markdown'; b.style.padding='0'; b.style.overflow='visible'; if(role==='user') {b.classList.remove('markdown');b.textContent=text;} else b.innerHTML=markdown(text); m.append(b); $('messages').append(m); if(id) lastMessage.set(id,{text,node:b}); $('messages').scrollTop=$('messages').scrollHeight; return b; }
async function loadChat(id) {
  try {
    if(state.busy&&id!==state.activeThread)throw new Error('请等当前轮次结束后切换对话');
    if(!navigate('chat'))return false;
    const data=await api('chat/'+encodeURIComponent(id));
    if(taskForChat&&currentReview?.threadId!==id)taskForChat=null;
    threadId=id;$('messages').replaceChildren();lastMessage.clear();toolMessages.clear();
    for(const turn of data.thread.turns||[])for(const item of turn.items||[]){if(item.type==='userMessage')addMessage('user',(item.content||[]).filter(c=>c.type==='text').map(c=>c.text).join('\n'));if(item.type==='agentMessage')addMessage('agent',item.text||'',item.id);}
    renderDirectory();updateReviewControls();return true;
  }catch(e){toast(e.message);return false;}
}
function updateControls() {
  $('send').disabled=!state.codex?.connected||!state.models?.length||state.busy; $('stop').hidden=!state.busy||!!state.toolBusy;
  ['model','effort','access','newChat'].forEach(id=>$(id).disabled=!!state.busy || (['model','effort'].includes(id)&&!state.models?.length));
  updateReviewControls();
  document.querySelectorAll('[data-workflow-mode]').forEach(b=>b.disabled=!!state.busy);
  $('toolRun').disabled=!!state.busy||toolRunning||!selectedTool||selectedTool.definition.kind==='manual';
  $('toolStop').disabled=!toolRunning;
  $('registerTool').disabled=!!state.busy||toolRunning;
  ['toolArguments','toolPlan','toolTask'].forEach(id=>$(id).disabled=!!state.busy||toolRunning);
  $('connectCodex').disabled=!!connectionRequests.codex;
}
function syncEditorDirty() {if(editorSyncing||lastEditorDirty===(docDirty||reviewSummaryDirty||reviewNoteDirty||reviewFeedbackDirty))return;editorSyncing=(async()=>{while(lastEditorDirty!==(docDirty||reviewSummaryDirty||reviewNoteDirty||reviewFeedbackDirty)){const dirty=docDirty||reviewSummaryDirty||reviewNoteDirty||reviewFeedbackDirty;await api('window/editor',{dirty});lastEditorDirty=dirty;}})().catch(()=>{lastEditorDirty=null;}).finally(()=>{editorSyncing=null;});}
function connectProvider(name,manual=false) {
  if(connectionRequests[name])return connectionRequests[name];connectionAttempts[name]=Date.now();connectionErrors[name]='';
  connectionRequests[name]=(async()=>{try{await api(name==='codex'?'codex/connect':'unity',name==='codex'?{}:{action:'connect'});if(manual)toast('已连接 Codex，模型和账号已刷新');}catch(e){connectionErrors[name]=e.message;if(manual)toast(e.message);}finally{connectionRequests[name]=null;await refreshState().catch(()=>{});}})();
  refreshState().catch(()=>{});return connectionRequests[name];
}
function autoConnect() {if(state.busy)return;for(const name of ['codex','unity']){const ready=name==='codex'?state.codex?.connected&&state.models?.length:state.unity?.state==='已连接';if(!ready&&!connectionRequests[name]&&Date.now()-connectionAttempts[name]>15000)connectProvider(name);}}

function efforts() { const m=state.models?.find(m=>m.model===$('model').value); $('effort').replaceChildren(); for(const e of m?.supportedReasoningEfforts||[]) { const o=new Option(({none:'无',minimal:'极低',low:'低',medium:'中',high:'高',xhigh:'极高',max:'最高',ultra:'超高'})[e.reasoningEffort]||e.reasoningEffort,e.reasoningEffort); o.title=e.description; $('effort').add(o); } if(m) $('effort').value=m.defaultReasoningEffort; }
async function refreshState() { state=await api('state'); $('project').textContent=state.project; $('projectPath').textContent=state.root; $('projectPath').title=state.root; $('codexStatus').innerHTML=`<i class="dot ${state.codex.connected?'online':''}"></i>Codex ${connectionRequests.codex?'连接中…':state.codex.connected?'已连接':connectionErrors.codex?'连接失败':'未连接'}`; $('unityStatus').innerHTML=`<i class="dot ${state.unity.state==='已连接'?'online':''}"></i>Unity ${connectionRequests.unity?'连接中…':esc(state.unity.state)}${state.unity.busy?' · 操作中':state.unity.lastResult?' · '+esc(state.unity.lastResult):''}`; const acc=state.codex.account?.account; $('accountStatus').textContent=state.codex.connected?(acc?`${acc.type==='chatgpt'?'ChatGPT':'API'} · ${acc.planType||'已登录'}`:'未登录'):(state.codex.error?'连接已断开':''); $('taskStatus').textContent=state.toolBusy?'拓展工具正在执行…':state.busy?(state.activeTurn?'Codex 正在执行…':'等待轮次回执…'):'就绪'; $('codexStatus').title=connectionErrors.codex||state.codex.error||'';$('unityStatus').title=connectionErrors.unity||state.unity.error||'';$('connectCodex').textContent=state.codex.connected?'Codex · 刷新模型与账号':'Codex · 连接';$('connectUnity').textContent='Unity · '+state.unity.state; const signature=JSON.stringify(state.models); if(signature!==modelSignature) { const prior=$('model').value; modelSignature=signature; $('model').replaceChildren(); for(const m of state.models) $('model').add(new Option(m.displayName,m.model)); if(state.models.some(m=>m.model===prior)) $('model').value=prior; else if(state.models.length) $('model').value=(state.models.find(m=>m.isDefault)||state.models[0]).model; efforts(); } updateControls(); if(page==='chat') renderDirectory(); }
function renderApprovals() {
  $('approval').replaceChildren();$('approval').hidden=approvalQueue.size===0;
  for(const [id,request] of approvalQueue) {const card=document.createElement('div'),label=document.createElement('strong'),pre=document.createElement('pre');label.textContent='Codex 请求你的批准';pre.textContent=JSON.stringify(request.params,null,2);card.append(label,pre);for(const [text,accept] of [['允许一次',true],['拒绝',false]]){const b=document.createElement('button');b.textContent=text;b.onclick=async()=>{b.disabled=true;try{await api('approval',{id,accept});approvalQueue.delete(id);renderApprovals();}catch(e){toast(e.message);b.disabled=false;}};card.append(b);} $('approval').append(card);}
}
function receive(e) {
  const method=e.method,p=e.params||{}; if(p.threadId && threadId && p.threadId!==threadId) return;
  if(method==='item/agentMessage/delta') { let m=lastMessage.get(p.itemId); if(!m) {addMessage('agent','',p.itemId); m=lastMessage.get(p.itemId);} m.text+=p.delta; m.node.innerHTML=markdown(m.text); }
  if(method==='item/completed'&&p.item?.type==='agentMessage') { const m=lastMessage.get(p.item.id); if(m) {m.text=p.item.text;m.node.innerHTML=markdown(m.text);} else addMessage('agent',p.item.text,p.item.id); }
  if(method==='item/started'&&['commandExecution','fileChange','mcpToolCall'].includes(p.item?.type)) { const d=document.createElement('details'); d.className='tool-event'; const summary=document.createElement('summary'); summary.textContent=p.item.type==='commandExecution'?`运行命令：${p.item.command}`:p.item.type==='mcpToolCall'?`调用工具：${p.item.tool}`:'修改文件'; const pre=document.createElement('pre'); pre.textContent=JSON.stringify(p.item,null,2); d.append(summary,pre); $('messages').append(d);toolMessages.set(p.item.id,{node:d,summary,pre}); }
  if(method==='item/completed' && toolMessages.has(p.item?.id)){const t=toolMessages.get(p.item.id),item=p.item;t.summary.textContent=(item.type==='commandExecution'?'命令':item.type==='fileChange'?'文件修改':'工具')+' · '+({completed:'已完成',failed:'失败',declined:'已拒绝',interrupted:'已中断'})[item.status]+' '+(item.command||item.tool||'');t.pre.textContent=item.aggregatedOutput||item.output||JSON.stringify(item.changes||item.result||item,null,2);}
  if(method==='karolina/approval') {approvalQueue.set(JSON.stringify(p.id),p);renderApprovals();}
  if(method==='turn/completed') { approvalQueue.clear();renderApprovals(); const status=p.turn?.status; toast(status==='completed'?'本轮已完成，请审查结果':status==='interrupted'?'本轮已停止':`本轮状态：${status} ${p.turn?.error?.message||''}`); }
  if(method==='karolina/review-ready'){refreshReviews().catch(e=>toast(e.message));toast('任务差异已生成，可前往任务审批。');}
  if(method==='karolina/disconnected'){approvalQueue.clear();renderApprovals();}
  if(method==='error'||method==='karolina/error'||method==='karolina/disconnected') toast(p.error?.message||p.message||'Codex 返回错误');
}
async function poll() {if(updating)return;updating=true;try{const data=await api('events?after='+eventCursor);if(data.truncated)toast('实时记录已超出缓存；可从对话历史重新载入完整结果。');data.events.forEach(e=>receive(e.value));eventCursor=data.cursor;await refreshState();syncEditorDirty();autoConnect();if(page==='review'&&Date.now()-lastReviewSync>5000)refreshReviews().catch(e=>toast(e.message));}catch(e){$('taskStatus').textContent='服务连接失败';}finally{updating=false;}}
function drawer(title,html) {$('drawerTitle').textContent=title;$('drawerBody').innerHTML=html;$('drawer').showModal();}
function unityDrawer() {drawer('Unity 连接与验证',`<p>状态：${esc(state.unity?.state)} · 当前工程 ${esc(state.project)}</p><p>操作调用当前工程的 Unity MCP，结果保留真实回执。不会自动清空 Console。</p><button data-unity="connect">连接并核对工程</button><button data-unity="state">Editor 状态</button><button data-unity="console">Console 错误</button><button data-unity="compile">刷新脚本</button><label>测试程序集<input id="unityAssembly" value="FrameSyncMoba.FrameSync.Tests"></label><label>测试类<input id="unityClass" value="CommandTargetTickResolverAdaptiveTests"></label><label>测试模式<select id="unityMode"><option>EditMode</option><option>PlayMode</option></select></label><button data-unity="tests">运行聚焦测试</button><pre id="unityResult">${esc(state.unity?.error||'尚未执行操作')}</pre>`); document.querySelectorAll('[data-unity]').forEach(b=>b.onclick=async()=>{b.disabled=true;try{const r=await api('unity',{action:b.dataset.unity,assembly:$('unityAssembly').value,class:$('unityClass').value,mode:$('unityMode').value});$('unityResult').textContent=JSON.stringify(r,null,2);await refreshState();}catch(e){$('unityResult').textContent=e.message;}finally{b.disabled=false;}}); }
document.querySelectorAll('[data-page]').forEach(b=>b.onclick=()=>{if(navigate(b.dataset.page)){++documentRequest;++diffRequest;}});
document.querySelectorAll('[data-prompt]').forEach(b=>b.onclick=()=>{$('prompt').value=b.dataset.prompt;$('prompt').focus();});
$('documentSource').oninput=()=>{docDirty=true;++editGeneration;syncEditorDirty();};$('search').oninput=renderDirectory;$('referenceSearch').oninput=renderReferences;$('clearReferences').onclick=()=>{selectedDocs.clear();renderReferences();};$('model').onchange=efforts;
$('newChat').onclick=()=>{taskForChat=null;threadId=null;updateReviewControls();lastMessage.clear();toolMessages.clear();$('messages').innerHTML='<div class="empty">新对话 · 输入问题或指令开始</div>';renderDirectory();};
$('theme').onclick=()=>{document.body.classList.toggle('dark');localStorage.setItem('karolina-theme',document.body.classList.contains('dark')?'dark':'light');};if(localStorage.getItem('karolina-theme')==='dark')document.body.classList.add('dark');
$('closeDrawer').onclick=()=>$('drawer').close();$('unityStatus').onclick=unityDrawer;$('connectUnity').onclick=unityDrawer;$('codexStatus').onclick=()=>drawer('Codex 连接',`<p>状态：${state.codex?.connected?'已连接':'未连接'}</p><p>程序：${esc(state.codex?.executable)}</p><p>账号：${esc($('accountStatus').textContent)}</p><p>未登录时请在终端运行 <code>codex login</code>，然后点击顶部刷新模型与账号。</p><pre>${esc(state.codex?.error||'')}</pre>`);
action('connectCodex',()=>connectProvider('codex',true));
action('send',async()=>{const text=$('prompt').value.trim();if(!text)throw new Error('请输入指令');const data=await api('chat/send',{text,model:$('model').value,effort:$('effort').value,access:$('access').value,documents:[...selectedDocs],threadId,taskId:taskForChat,planId:workflowMode==='execute-task'?$('executionPlan').value||null:null,mode:workflowMode});threadId=data.threadId;addMessage('user',text);$('prompt').value='';await refreshState();});
$('prompt').onkeydown=e=>{if(e.key==='Enter'&&!e.shiftKey&&!e.isComposing){e.preventDefault();if(!$('send').disabled)$('send').click();}};
action('stop',async()=>{await api('chat/stop',{});toast('已发送停止请求，等待 Codex 确认');});
$('readMode').onclick=()=>{if(!currentDoc)return;$('changeSummaryRow').hidden=true;$('markdown').innerHTML=markdown($('documentSource').value);$('markdown').hidden=false;$('documentSource').hidden=true;$('saveDocument').hidden=true;$('readMode').classList.add('selected');$('sourceMode').classList.remove('selected');};$('sourceMode').onclick=()=>{if(!currentDoc||$('sourceMode').disabled)return;$('changeSummaryRow').hidden=currentDoc.document.type!=='requirement';$('markdown').hidden=true;$('documentSource').hidden=false;$('saveDocument').hidden=false;$('readMode').classList.remove('selected');$('sourceMode').classList.add('selected');};
action('saveDocument',async()=>{
  if(!currentDoc)return;
  const submitted=currentDoc,id=submitted.document.id,text=$('documentSource').value,hash=submitted.hash,summary=$('changeSummary').value,generation=editGeneration,editorPage=page,editorRequest=documentRequest;
  await api('document/save',{id,markdown:text,expectedHash:hash,changeSummary:summary});
  const fresh=await api('document/'+encodeURIComponent(id));documentCache.set(id,fresh);
  if(currentDoc?.document.id===id&&page===editorPage&&documentRequest===editorRequest){
    if(editGeneration===generation&&$('documentSource').value===text){++documentRequest;paintDocument(fresh);}
    else {currentDoc={...fresh};docDirty=true;}
  }
  toast('正文已保存'+(submitted.document.type==='requirement'&&summary?'，需求变更已记入演进':''));
});
const metaLabels={id:'编号',code:'编码',title:'标题',type:'类别',status:'状态',domain:'模块',version:'版本',path:'正文位置',created:'创建日期',createdAt:'创建日期',updatedAt:'更新日期',completed:'结束日期',risk:'风险',implementationState:'实现核查'};
function metaValue(value) {return esc(typeof value==='object'?JSON.stringify(value):value);}
$('metadataButton').onclick=async()=>{try{const result=await documentMetadata();if(!result)return;const m=result.metadata;
  const basic=Object.entries(metaLabels).filter(([k])=>m[k]!=null).map(([k,label])=>`<dt>${label}</dt><dd>${metaValue(m[k])}</dd>`).join('');
  const links=[...(m.requirementRefs||m.requirements||[]),...(m.related||[])].map(r=>{const id=typeof r==='string'?r:r.id,d=docs.find(d=>d.id===id);return `<div class="relation-card">${d?`<a href="#document:${encodeURIComponent(id)}" data-document="${esc(id)}">${esc(d.title)}</a>`:esc(typeof r==='string'?r:r.title)}${typeof r==='object'?`<small>引用版本 ${esc(r.version)} · ${esc((r.sections||[]).join('、'))}${d?.status?' · 当前 '+esc(d.status):''}</small>`:''}</div>`;}).join('');
  const editable=!docDirty&&['requirement','plan'].includes(m.type)&&!['关闭'].includes(m.status);const transitions=m.type==='requirement'?['激活','废弃']:({准备:['准备','执行'],执行:['执行','测试'],测试:['测试','校正','验收'],校正:['校正','执行','测试'],验收:['验收','校正','关闭']})[m.status]||[];const technical=Object.fromEntries(Object.entries(m).filter(([k])=>!['history','evolution'].includes(k)));
  drawer('文档信息与关联',`<div class="meta-tabs"><button class="selected" data-meta-tab="basic">元数据</button><button data-meta-tab="relations">关联资料</button><button data-meta-tab="evidence">来源与证据</button></div><section data-meta-panel="basic"><dl class="metadata-grid">${basic}</dl>${editable?`<label>生命周期<select id="documentLife">${transitions.map(x=>`<option ${x===m.status?'selected':''}>${esc(x)}</option>`).join('')}</select></label>${m.type==='plan'?`<label>验收/取消结论<input id="closeConclusion" placeholder="结束计划时填写真实验收或取消结论"></label>`:''}<button id="saveDocumentDetails">保存状态与引用</button>`:''}<details><summary>查看机器元数据</summary><pre>${esc(JSON.stringify(technical,null,2))}</pre></details></section><section data-meta-panel="relations" hidden>${links||'<p>此文档没有关联资料。</p>'}${editable&&m.type==='plan'?`<details><summary>修改参考需求</summary><div class="metadata-refs">${docs.filter(d=>d.type==='requirement').map(d=>`<label><input type="checkbox" data-req-id="${esc(d.id)}" ${(m.requirements||[]).includes(d.id)?'checked':''}> ${esc(d.title)}</label>`).join('')}</div></details>`:''}</section><section data-meta-panel="evidence" hidden><h3>来源</h3><pre>${esc(JSON.stringify(m.sources||[],null,2))}</pre><h3>工程证据</h3><pre>${esc(JSON.stringify(m.evidence||[],null,2))}</pre>${m.assessment?`<h3>核查结论</h3><pre>${esc(JSON.stringify(m.assessment,null,2))}</pre>`:''}</section>`);
  if(editable)$('saveDocumentDetails').onclick=async()=>{const b=$('saveDocumentDetails');b.disabled=true;try{await api('document/details',{id:m.id,expectedMetadataHash:result.hash,status:$('documentLife').value,requirements:m.type==='plan'&&JSON.stringify([...document.querySelectorAll('[data-req-id]:checked')].map(x=>x.dataset.reqId).sort())!==JSON.stringify([...(m.requirements||[])].sort())?[...document.querySelectorAll('[data-req-id]:checked')].map(x=>x.dataset.reqId):null,conclusion:$('closeConclusion')?.value||null});$('drawer').close();docs=await api('documents');renderReferences();documentCache.delete(m.id);await loadDocument(m.id);toast('状态与引用已保存');}catch(e){toast(e.message);b.disabled=false;}};
  document.querySelectorAll('[data-meta-tab]').forEach(b=>b.onclick=()=>{document.querySelectorAll('[data-meta-tab]').forEach(x=>x.classList.toggle('selected',x===b));document.querySelectorAll('[data-meta-panel]').forEach(x=>x.hidden=x.dataset.metaPanel!==b.dataset.metaTab);});
}catch(e){toast(e.message);}};
$('historyButton').onclick=()=>{if(!currentDoc||currentDoc.document.type!=='requirement')return;if(docDirty){toast('演进属于正文，请保存或在编辑器定位「需求演进」段落。');return;}$('readMode').click();const h=[...$('markdown').querySelectorAll('h2')].find(h=>h.textContent==='需求演进');if(h)h.scrollIntoView({behavior:'smooth',block:'start'});else toast('正文尚未记录需求演进；下一次语义变更时补充。');};
action('diagnosticsButton',async()=>{const d=await api('diagnostics');drawer('运行详情',`<p>它用来查明 Codex 何时开始、是否完成、为什么失败、用了哪些权限，以及 Unity 的真实回执。普通讨论不需要手动管理它。</p><p>本地记录：<code>${esc(d.location)}</code></p><pre>${esc(JSON.stringify(d.run,null,2))}</pre>`);});
$('newDocument').onclick=()=>{
  const type=page;
  drawer('新建'+({requirement:'需求案',plan:'计划案',rule:'规则案'}[type]),`<label>标题<input id="newTitle" placeholder="用功能名称命名"></label><label>英文文件标题<input id="newEnglishTitle" placeholder="例如 document-lifecycle（编号自动生成）"></label><label>英文模块目录<input id="newEnglishDomain" placeholder="例如 combat；留空使用 custom"></label><label>模块显示名<input id="newDomain" placeholder="例如：战斗、Karolina"></label>${type==='rule'?`<label>规则板块<select id="newRuleSection">${ruleSections.map(([id,title])=>`<option value="${id}">${title}</option>`).join('')}</select></label>`:''}${type==='plan'?`<details open><summary>参考需求（准备可稍后关联，执行前必须填写）</summary><div class="metadata-refs">${docs.filter(d=>d.type==='requirement').map(d=>`<label><input type="checkbox" data-new-req="${esc(d.id)}"> ${esc(d.title)}</label>`).join('')}</div></details>`:''}<button id="createDocument" class="primary">创建${type==='plan'?'准备':''}</button>`);
  $('createDocument').onclick=async()=>{const b=$('createDocument');b.disabled=true;try{const r=await api('document/create',{type,title:$('newTitle').value,domain:$('newDomain').value,englishTitle:$('newEnglishTitle').value,englishDomain:$('newEnglishDomain').value,ruleSection:type==='rule'?$('newRuleSection').value:null,requirements:type==='plan'?[...document.querySelectorAll('[data-new-req]:checked')].map(x=>x.dataset.newReq):[]});$('drawer').close();docs=await api('documents');renderReferences();await loadDocument(r.id);$('sourceMode').click();}catch(e){toast(e.message);b.disabled=false;}};
};
document.addEventListener('click',e=>{const link=e.target.closest('a[data-document]');if(link){e.preventDefault();if($('drawer').open)$('drawer').close();loadDocument(link.dataset.document);}});
document.addEventListener("DOMContentLoaded",()=>{(async()=>{try{docs=await api('documents');renderReferences();await refreshState();await restoreWorkspace();const diagnostic=await api('diagnostics');for(const request of diagnostic.approvals||[])approvalQueue.set(JSON.stringify(request.id),request);renderApprovals();renderDirectory();autoConnect();}catch(e){toast(e.message);}window.addEventListener('focus',()=>{if(page==='review')refreshReviews().catch(e=>toast(e.message));autoConnect();});setInterval(poll,1000);})();});
