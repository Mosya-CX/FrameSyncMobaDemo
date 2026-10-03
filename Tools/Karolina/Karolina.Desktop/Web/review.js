"use strict";
let reviewTasks=[], currentReview=null, reviewFile=null, reviewRequest=0, reviewReading=null, lastReviewSync=0;
let taskForChat=null, noteAnchor=null, reviewSummaryDirty=false, reviewNoteDirty=false, reviewFeedbackDirty=false;
let reviewSaving=false, reviewDraftGeneration=0;
async function reviewMutation(fn) {
  if(reviewSaving)throw new Error('正在保存审批，请稍候');
  reviewSaving=true;updateReviewControls();
  try{return await fn();}finally{reviewSaving=false;updateReviewControls();}
}
function renderReviewPlans() {
  const select=$('executionPlan'), previous=select.value;
  select.replaceChildren(new Option('不选 · 普通只读对话',''));
  for(const plan of docs.filter(d=>d.type==='plan'&&d.status!=='关闭'))select.add(new Option(plan.title+' · '+plan.status,plan.id));
  if([...select.options].some(o=>o.value===previous))select.value=previous;
}
function renderReviewDirectory(query) {
  for(const task of reviewTasks.filter(t=>t.title.toLowerCase().includes(query))){
    const button=document.createElement('button');button.className='review-task';button.classList.toggle('active',currentReview?.id===task.id);
    const visible=task.files.filter(f=>$('showReviewMeta').checked||!f.path.toLowerCase().endsWith('.meta')).length;
    button.innerHTML=`<span class="title">${esc(task.title)}</span><small>${esc(task.state)} · 第 ${task.round} 轮 · ${visible} 个显示文件</small>`;
    button.onclick=()=>{if(!reviewSaving)openReview(task.id).catch(e=>toast(e.message));};$('directory').append(button);
  }
  if(!reviewTasks.length)$('directory').innerHTML='<div class="empty">还没有任务审批<br>在 AI 对话中选择执行计划，或在外部执行前开始记录。</div>';
}
async function refreshReviews() {
  if(reviewReading)return reviewReading;lastReviewSync=Date.now();
  reviewReading=(async()=>{reviewTasks=await api('reviews');if(page==='review'){renderDirectory();const fresh=reviewTasks.find(t=>t.id===currentReview?.id);if(fresh&&fresh.revision!==currentReview.revision&&!reviewSaving&&!reviewSummaryDirty&&!reviewNoteDirty&&!reviewFeedbackDirty)await openReview(fresh.id);}})();
  try{await reviewReading;}finally{reviewReading=null;}
}
async function openReview(id,discard=false) {
  if(!discard&&(reviewSummaryDirty||reviewNoteDirty||reviewFeedbackDirty)&&!confirm('有未保存的摘要或审批意见，放弃后切换？'))return;
  const request=++reviewRequest,generation=reviewDraftGeneration,data=await api('review/'+id);
  if(request!==reviewRequest||generation!==reviewDraftGeneration&&!discard)return;
  currentReview=data;reviewFile=null;reviewSummaryDirty=false;reviewNoteDirty=false;reviewFeedbackDirty=false;noteAnchor=null;
  $('reviewEmpty').hidden=true;$('reviewContent').hidden=false;$('pageTitle').textContent=data.title;
  $('reviewState').textContent=data.state+' · 第 '+data.round+' 轮';$('reviewRun').textContent='执行结果：'+data.runState;
  $('reviewProvenance').textContent='对照方式：'+data.baselineKind+'\n'+data.baselineDescription;
  $('reviewSummary').value=data.summary;$('reviewFeedback').value=data.feedback;
  $('reviewPlan').textContent=docs.find(d=>d.id===data.planId)?.title||data.title;
  $('reviewSummary').readOnly=data.state==='已通过'||data.state==='待再执行';
  $('reviewFeedback').readOnly=data.state!=='待审批';
  $('reviewNotes').replaceChildren();$('reviewNote').value='';$('noteLocation').textContent='文件整体意见（也可点击差异行定位）';
  $('reviewDiffTitle').textContent='选择文件查看此次任务差异';$('reviewDiff').innerHTML='<div class="empty">绿色为新增，红色为删除。批准针对本次任务的具体内容版本。</div>';
  $('reviewAttempts').innerHTML=data.attempts.length?`<summary>之前的退回意见（${data.attempts.length} 轮）</summary>`+data.attempts.map(a=>`<section class="review-attempt"><strong>第 ${a.round} 轮</strong><p>${esc(a.feedback)}</p>${a.files.filter(f=>f.decision==='不通过').map(f=>`<p><code>${esc(f.path)}</code><br>${f.notes.map(n=>esc(n.text)).join('<br>')}</p>`).join('')}</section>`).join(''):'';
  renderReviewFiles();renderDirectory();updateReviewControls();syncEditorDirty();
  rememberWorkspace();
}
function renderReviewFiles() {
  $('reviewFiles').replaceChildren();const q=$('reviewSearch').value.toLowerCase();
  const showMeta=$('showReviewMeta').checked;
  for(const file of currentReview?.files||[]){
    if(!file.path.toLowerCase().includes(q)||!showMeta&&file.path.toLowerCase().endsWith('.meta'))continue;
    const button=document.createElement('button');button.className='review-file';button.classList.toggle('active',reviewFile?.path===file.path);
    button.innerHTML=`<span>${esc(file.path)}</span><small>${esc(file.kind)} · ${esc(file.decision)}</small>`;button.onclick=()=>{if(!reviewSaving)openReviewFile(file).catch(e=>toast(e.message));};$('reviewFiles').append(button);
  }
  if(!currentReview?.files.length)$('reviewFiles').innerHTML='<div class="empty">尚无提交的 Unity 文件差异。<br>完成修改后填写摘要并提交审批。</div>';
  const files=currentReview?.files||[],hiddenMeta=showMeta?0:files.filter(f=>f.path.toLowerCase().endsWith('.meta')).length;
  const displayed=files.filter(f=>(showMeta||!f.path.toLowerCase().endsWith('.meta'))&&f.path.toLowerCase().includes(q)).length;
  if(files.length&&!displayed)$('reviewFiles').innerHTML='<div class="empty">当前筛选下没有文件。</div>';
  $('reviewCounts').textContent=`显示 ${displayed} / 全部 ${files.length} 个文件${hiddenMeta?' · 隐藏 '+hiddenMeta+' 个 .meta':''} · ${files.filter(f=>f.decision==='通过').length} 通过 · ${files.filter(f=>f.decision==='不通过').length} 不通过`;
  $('approveTask').textContent='批准本次任务全部变更'+(hiddenMeta?'（含 '+hiddenMeta+' 个隐藏 .meta）':'');
}
async function openReviewFile(file) {
  if(reviewNoteDirty&&!confirm('文件意见还未保存，放弃后切换？'))return;
  const request=++reviewRequest,id=currentReview.id,generation=reviewDraftGeneration,data=await api('review/'+id+'/diff?path='+encodeURIComponent(file.path));
  if(request!==reviewRequest||id!==currentReview?.id||generation!==reviewDraftGeneration)return;
  reviewFile=data.file;noteAnchor=null;reviewNoteDirty=false;$('reviewNote').value='';$('reviewDiffTitle').textContent=file.path;
  $('noteLocation').textContent='文件整体意见（也可点击差异行定位）';renderReviewFiles();paintReviewDiff(data);paintReviewNotes();updateReviewControls();
}
function paintReviewDiff(data) {
  const target=$('reviewDiff');target.replaceChildren();
  const metadata=document.createElement('div');metadata.className='review-file-facts';
  metadata.textContent=`${data.file.originalPath?data.file.originalPath+' → '+data.file.path+'\n':''}${data.file.kind} · 变更前 ${data.file.before?.bytes??'不存在'} 字节 → 变更后 ${data.file.after?.bytes??'不存在'} 字节\nSHA-256：${data.file.before?.hash??'不存在'} → ${data.file.after?.hash??'不存在'}`;target.append(metadata);
  if(currentReview.selectionEvidence?.[data.file.path]){const evidence=document.createElement('p');evidence.textContent='收录依据：'+currentReview.selectionEvidence[data.file.path];target.append(evidence);}
  if(!data.text){const message=document.createElement('p');message.className='review-binary';message.textContent=data.message;target.append(message);return;}
  const fragment=document.createDocumentFragment();
  for(const line of data.lines){
    const row=document.createElement('div');row.className='diff-line '+line.kind;
    for(const [side,number] of [['before',line.before],['after',line.after]]){const cell=document.createElement('button');cell.className='number';cell.textContent=number??'';cell.disabled=number==null;cell.title=number==null?'':(side==='before'?'变更前':'变更后')+'第 '+number+' 行，添加意见';cell.onclick=()=>{noteAnchor={side,line:number};$('noteLocation').textContent=(side==='before'?'变更前':'变更后')+'第 '+number+' 行';$('reviewNote').focus();};row.append(cell);}
    const text=document.createElement('span');text.className='text';text.textContent=line.text;row.append(text);fragment.append(row);
  }
  target.append(fragment);
}
function paintReviewNotes() {
  $('reviewNotes').innerHTML=(reviewFile?.notes||[]).map((n,i)=>`<div class="review-note"><small>${n.line?(n.side==='before'?'变更前':'变更后')+'第 '+n.line+' 行':'文件整体'}</small><p>${esc(n.text)}</p>${currentReview.state==='待审批'?`<button data-remove-note="${i}">移除意见</button>`:''}</div>`).join('');
  document.querySelectorAll('[data-remove-note]').forEach(b=>b.onclick=()=>{if(reviewSaving)return;reviewFile.notes.splice(Number(b.dataset.removeNote),1);reviewNoteDirty=true;reviewDraftGeneration++;paintReviewNotes();syncEditorDirty();});
}
function updateReviewControls() {
  const task=currentReview, idle=!state.busy&&!reviewSaving;
  $('reviewSummary').readOnly=reviewSaving||!task||['已通过','待再执行'].includes(task.state);
  $('reviewFeedback').readOnly=reviewSaving||task?.state!=='待审批';$('reviewNote').readOnly=reviewSaving||task?.state!=='待审批';
  $('refreshReviews').disabled=reviewSaving;$('startExternalReview').disabled=reviewSaving||!!state.busy;
  $('showReviewMeta').disabled=reviewSaving;
  $('submitReview').disabled=!idle||!task||!['执行','待审批'].includes(task.state)||!$('reviewSummary').value.trim();
  for(const id of ['approveTask','returnTask'])$(id).disabled=!idle||!task||task.state!=='待审批';
  for(const id of ['approveFile','rejectFile','saveFileNotes'])$(id).disabled=!idle||!reviewFile||task?.state!=='待审批';
  $('resumeReview').disabled=!idle||task?.state!=='待再执行';
  $('executionPlan').disabled=!!state.busy||!!taskForChat;
  $('executionHint').textContent=taskForChat?'继续审批退回的任务（原开始基线保留）':'项目写入前请选择计划；参考资料仍可不选或多选。';
}
function draftNotes() {
  const notes=[...(reviewFile?.notes||[])], text=$('reviewNote').value.trim();if(text)notes.push({text,...(noteAnchor||{})});return notes;
}
async function saveFileDecision(accept) {
  if(!currentReview||!reviewFile)return;
  const id=currentReview.id,path=reviewFile.path;
  const summary=$('reviewSummary').value, feedback=$('reviewFeedback').value, summaryDirty=reviewSummaryDirty, feedbackDirty=reviewFeedbackDirty;
  let saved=false;
  try{
    await api('review/file',{id,revision:currentReview.revision,path,fingerprint:reviewFile.fingerprint,accept,notes:draftNotes()});saved=true;
    await openReview(id,true);await openReviewFile(currentReview.files.find(f=>f.path===path));await refreshReviews();
  }catch(e){if(saved)throw new Error('文件意见已保存，但重新读取失败，请刷新任务：'+e.message);throw e;}
  finally{$('reviewSummary').value=summary;$('reviewFeedback').value=feedback;reviewSummaryDirty=summaryDirty;reviewFeedbackDirty=feedbackDirty;syncEditorDirty();}
}
action('startExternalReview',async()=>{
  drawer('开始记录任务',`<p>必须在任务执行前记录基线。只统计此后 Assets、Packages、ProjectSettings 的变化；已有改动保留。外部 IDE/Unity 同期改动无法自动证明作者，也会进入差异。</p><label>执行计划<select id="externalPlan">${docs.filter(d=>d.type==='plan'&&d.status!=='关闭').map(d=>`<option value="${esc(d.id)}">${esc(d.title)} · ${esc(d.status)}</option>`).join('')}</select></label><button id="beginExternalReview" class="primary">记录开始基线</button>`);
  $('beginExternalReview').onclick=async()=>{const b=$('beginExternalReview');b.disabled=true;try{const task=await api('review/start',{planId:$('externalPlan').value});$('drawer').close();await refreshReviews();await openReview(task.id,true);}catch(e){toast(e.message);b.disabled=false;}};
});
action('refreshReviews',async()=>{await refreshReviews();if(currentReview)await openReview(currentReview.id);else if(reviewTasks.length)await openReview(reviewTasks[0].id);});
action('submitReview',()=>reviewMutation(async()=>{if(reviewNoteDirty)throw new Error('请先保存当前文件意见');const feedback=$('reviewFeedback').value,feedbackDirty=reviewFeedbackDirty;const task=await api('review/submit',{id:currentReview.id,revision:currentReview.revision,summary:$('reviewSummary').value});await openReview(task.id,true);$('reviewFeedback').value=feedback;reviewFeedbackDirty=feedbackDirty;await refreshReviews();syncEditorDirty();toast('最新任务差异已提交；变动的文件需要重新审批。');}));
action('approveFile',()=>reviewMutation(()=>saveFileDecision(true)));action('rejectFile',()=>reviewMutation(()=>saveFileDecision(false)));
action('saveFileNotes',()=>reviewMutation(()=>saveFileDecision(null)));
action('approveTask',()=>reviewMutation(async()=>{if(reviewNoteDirty||reviewSummaryDirty)throw new Error('请先保存意见或提交最新摘要');const hidden=$('showReviewMeta').checked?0:currentReview.files.filter(f=>f.path.toLowerCase().endsWith('.meta')).length;if(!confirm('批准此次任务的全部文件变化'+(hidden?'（包含 '+hidden+' 个当前隐藏的 .meta 文件）':'')+'？已有不通过的文件必须先处理。本操作不提交 Git，也不自动关闭计划。'))return;const task=await api('review/decision',{id:currentReview.id,revision:currentReview.revision,accept:true,feedback:$('reviewFeedback').value});await openReview(task.id,true);await refreshReviews();toast('此次任务整体批准通过。');}));
action('returnTask',()=>reviewMutation(async()=>{if(reviewNoteDirty||reviewSummaryDirty)throw new Error('请先保存文件意见或提交最新摘要');const task=await api('review/decision',{id:currentReview.id,revision:currentReview.revision,accept:false,feedback:$('reviewFeedback').value});await openReview(task.id,true);await refreshReviews();toast('任务已退回；点击「返回再执行」将意见带回 AI 对话。');}));
action('resumeReview',async()=>{
  const task=currentReview;
  if(task.threadId){if(!await loadChat(task.threadId))return;}else{if(!navigate('chat'))return;threadId=null;lastMessage.clear();toolMessages.clear();$('messages').innerHTML='<div class="empty">外部任务返回执行 · 将建立新的 Codex 对话</div>';}
  taskForChat=task.id;
  setWorkflowMode('execute-task');
  $('executionPlan').value=task.planId;$('access').value='workspace-write';$('prompt').value=task.feedbackPrompt+'\n\n请根据以上意见修正，保持已通过且内容未变的文件，完成后总结变化及真实验证结果。';$('prompt').focus();updateReviewControls();
});
$('reviewSummary').oninput=()=>{reviewSummaryDirty=true;reviewDraftGeneration++;updateReviewControls();syncEditorDirty();};
$('reviewNote').oninput=()=>{reviewNoteDirty=true;reviewDraftGeneration++;syncEditorDirty();};
$('reviewFeedback').oninput=()=>{reviewFeedbackDirty=true;reviewDraftGeneration++;syncEditorDirty();};
$('reviewSearch').oninput=renderReviewFiles;
$('showReviewMeta').onchange=()=>{
  if(!$('showReviewMeta').checked&&reviewFile?.path.toLowerCase().endsWith('.meta')){
    if(reviewNoteDirty){toast('请先保存当前 .meta 文件意见，再隐藏');$('showReviewMeta').checked=true;return;}
    reviewFile=null;noteAnchor=null;$('reviewNote').value='';$('reviewNotes').replaceChildren();$('reviewDiffTitle').textContent='选择文件查看此次任务差异';$('reviewDiff').replaceChildren();
  }
  ++reviewRequest;
  renderReviewFiles();renderDirectory();updateReviewControls();rememberWorkspace();
};
$('executionPlan').onchange=()=>{taskForChat=null;updateReviewControls();};
