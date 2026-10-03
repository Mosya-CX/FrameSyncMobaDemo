"use strict";
let workflowMode='discuss-requirement',restoringWorkspace=true,viewSaving=null,nextWorkspaceView=null;
const ruleSections=[['facts','工程事实'],['execution','执行细则'],['index','项目索引'],['templates','模板']];
function renderRuleDirectory(query){
  for(const [section,title] of ruleSections){
    const header=document.createElement('div');header.className='group';header.textContent=title;$('directory').append(header);
    for(const d of docs.filter(d=>d.type==='rule'&&d.section===section&&d.title.toLowerCase().includes(query))){const b=document.createElement('button');b.textContent=d.title;b.classList.toggle('active',currentDoc?.document.id===d.id);b.onclick=()=>loadDocument(d.id);$('directory').append(b);}
  }
}
function setWorkflowMode(mode){
  if(!['discuss-requirement','formulate-plan','execute-task'].includes(mode))return;
  if(state.busy)return;
  workflowMode=mode;if(mode!=='execute-task')taskForChat=null;
  document.querySelectorAll('[data-workflow-mode]').forEach(b=>b.classList.toggle('selected',b.dataset.workflowMode===mode));
  $('executionPlanRow').hidden=mode!=='execute-task';
  $('access').querySelector('[value=workspace-write]').textContent=mode==='execute-task'?'工程写入':'文档写入';
  const full=$('access').querySelector('[value=danger-full-access]');full.disabled=mode!=='execute-task';if(full.disabled&&$('access').value==='danger-full-access')$('access').value='read-only';
  $('workflowHint').textContent=mode==='discuss-requirement'?'讨论目标与方案，输出或维护需求案；写入范围为 Docs。':mode==='formulate-plan'?'引用需求制定计划与验收流程；不会开始实施。':'选择具体计划，执行任务后审查本次 Unity 文件变化。';
  updateReviewControls();rememberWorkspace();
}
document.querySelectorAll('[data-workflow-mode]').forEach(b=>b.onclick=()=>setWorkflowMode(b.dataset.workflowMode));
function rememberWorkspace(){
  if(restoringWorkspace)return;
  nextWorkspaceView={page,reviewId:currentReview?.id||null,documentId:currentDoc?.document.id||null,mode:workflowMode,toolId:selectedTool?.definition.id||null,showMeta:$('showReviewMeta').checked};
  if(viewSaving)return;
  viewSaving=(async()=>{while(nextWorkspaceView){const value=nextWorkspaceView;nextWorkspaceView=null;await api('workspace/view',value);}})().catch(e=>toast('页面位置未保存：'+e.message)).finally(()=>{viewSaving=null;});
}
async function restoreWorkspace(){
  const saved=await api('workspace/view');await refreshReviews();try{await refreshTools();}catch(e){toast('拓展工具目录加载失败：'+e.message);}
  $('showReviewMeta').checked=!!saved.showMeta;
  setWorkflowMode(saved.mode||'discuss-requirement');
  if(saved.page==='review'){
    navigate('review');const task=reviewTasks.find(t=>t.id===saved.reviewId)||reviewTasks.find(t=>t.state!=='已通过')||reviewTasks[0];if(task)await openReview(task.id,true);
  }else if(['requirement','plan','rule'].includes(saved.page)&&docs.some(d=>d.id===saved.documentId)){await loadDocument(saved.documentId);}
  else if(saved.page==='tools'){navigate('tools');const item=extensionTools.find(t=>t.definition.id===saved.toolId)||extensionTools[0];if(item)await openTool(item);}
  else navigate('chat');
  restoringWorkspace=false;rememberWorkspace();
}
