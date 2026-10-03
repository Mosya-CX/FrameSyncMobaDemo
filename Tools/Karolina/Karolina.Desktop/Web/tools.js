"use strict";
let extensionTools=[],selectedTool=null,toolRequest=0,toolRunning=false;
async function refreshTools(){extensionTools=await api('tools');if(page==='tools')renderDirectory();}
function renderToolDirectory(query){
  for(const item of extensionTools.filter(t=>t.definition.title.toLowerCase().includes(query))){const tool=item.definition,b=document.createElement('button');b.innerHTML=`<span class="title">${esc(tool.title)}</span><small>${esc(tool.kind==='unity-mcp'?'Unity MCP':tool.kind==='external'?'外部程序':'操作指南')}</small>`;b.classList.toggle('active',selectedTool?.definition.id===tool.id);b.onclick=()=>openTool(item).catch(e=>toast(e.message));$('directory').append(b);}
  if(!extensionTools.length)$('directory').innerHTML='<div class="empty">尚未注册拓展工具。</div>';
}
async function openTool(item){
  if(toolRunning)throw new Error('工具正在执行，请等待或停止');
  const request=++toolRequest;selectedTool=item;
  const tool=item.definition;$('toolEmpty').hidden=true;$('toolContent').hidden=false;$('pageTitle').textContent=tool.title;
  $('toolDescription').textContent=tool.description;$('toolKind').textContent=tool.kind==='unity-mcp'?'Unity MCP':tool.kind==='external'?'外部程序':'操作指南';
  $('toolArguments').value=JSON.stringify(tool.defaultArguments||{},null,2);
  $('toolInvocation').hidden=tool.kind==='manual';$('toolPlanRow').hidden=!tool.requiresTask;
  $('toolPlan').replaceChildren(new Option('选择执行计划',''));for(const p of docs.filter(d=>d.type==='plan'&&d.status!=='关闭'))$('toolPlan').add(new Option(p.title,p.id));
  $('toolTask').replaceChildren(new Option('新任务 / 根据所选计划继续',''));for(const t of reviewTasks.filter(t=>['执行','待再执行'].includes(t.state)))$('toolTask').add(new Option(t.title,t.id));
  $('toolOutput').textContent='尚未调用。';$('toolRun').disabled=tool.kind==='manual'||!!state.busy;
  if(tool.guideId){const guide=await api('document/'+encodeURIComponent(tool.guideId));if(request!==toolRequest)return;$('toolManual').innerHTML=markdown(guide.markdown);}else $('toolManual').textContent='缺少配套说明，请完善注册信息。';
  renderDirectory();rememberWorkspace();
  await renderToolHistory();
}
async function renderToolHistory(){const runs=await api('tools/runs');$('toolHistoryBody').replaceChildren();for(const run of runs){const p=document.createElement('p');p.textContent=(extensionTools.find(t=>t.definition.id===run.toolId)?.definition.title||run.toolId)+' · '+run.state+' · '+new Date(run.started).toLocaleString();$('toolHistoryBody').append(p);}}
action('refreshTools',refreshTools);
action('toolRun',async()=>{
  const item=selectedTool;if(!item)throw new Error('请先选择工具');const argumentsValue=JSON.parse($('toolArguments').value);
  if(item.definition.requiresTask&&!$('toolPlan').value&&!$('toolTask').value)throw new Error('写入工具请选择执行计划或原任务');
  toolRunning=true;$('toolStop').disabled=false;$('toolOutput').textContent='正在调用…';
  try{const result=await api('tools/run',{id:item.definition.id,hash:item.hash,arguments:argumentsValue,planId:$('toolPlan').value||null,taskId:$('toolTask').value||null});$('toolOutput').textContent=result.state+'\n'+result.output+(result.reviewId?'\n已生成任务审批条目。':'');await refreshReviews();}
  catch(e){$('toolOutput').textContent=e.message;throw e;}finally{toolRunning=false;$('toolStop').disabled=true;await renderToolHistory();}
});
action('toolStop',()=>api('tools/stop',{}));
action('buildFinished',async()=>{if(confirm('确认 Unity 构建已经结束？')){await api('tools/build-finished',{});toast('已登记构建结束，Unity操作恢复。');}});
action('registerTool',async()=>{
  drawer('注册拓展工具',`<p>为工具提供明确入口、参数和使用说明。注册不会执行工具，调用以当前 Windows 用户权限运行。</p><label>注册声明（JSON）<textarea id="toolRegistration" rows="13"></textarea></label><label>配套使用说明（Markdown）<textarea id="toolRegistrationManual" rows="6" placeholder="用途、参数、步骤、预期结果、失败处理与写入范围"></textarea></label><button id="saveToolRegistration" class="primary">注册工具</button>`);
  $('toolRegistration').value=JSON.stringify({id:'my-tool',title:'我的工具',description:'说明工具用途',kind:'external',executable:'dotnet',arguments:['--info'],defaultArguments:{},readOnly:true,timeoutSeconds:60},null,2);
  $('saveToolRegistration').onclick=async()=>{const b=$('saveToolRegistration');b.disabled=true;try{const item=await api('tools/register',{definition:JSON.parse($('toolRegistration').value),manual:$('toolRegistrationManual').value});$('drawer').close();docs=await api('documents');await refreshTools();await openTool(item);toast('工具已注册，尚未执行。');}catch(e){toast(e.message);}finally{b.disabled=false;}};
});
