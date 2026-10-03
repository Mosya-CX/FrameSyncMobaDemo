from revise_current import ROOT,write
import json
def doc(id):
 c=json.loads((ROOT/'Docs/catalog.json').read_text(encoding='utf-8'));return next(ROOT/e['path'] for e in c['entries'] if e['id']==id)
def rule(id,text):write(doc(id),text)
rule('RULE-005','''# 文档维护与演进

## 分类与职责

需求正文记录长期目标、选定技术、边界、附录和“需求演进”。需求只有激活/废弃。演进描述变动前后、缘由、技术选择、兼容及边界影响、关联计划；旧材料迁移如实保留已有简述，不编造缺失历史。元数据只存编号、标题、状态、关系、版本、来源和工程证据，不存 history/evolution。

计划是一轮明确结束的执行：准备、执行、测试、校正、验收、关闭。测试强调机器检查和 Agent 审查；验收强调用户人工操作。测试/人工验收发现问题进入校正，再执行/测试；关闭不可重启，后续通常新建需求与计划。历史取消以关闭及 outcome=取消保存，历史暂缓转准备并保留实际进度说明。计划不建演进历史。

规则无状态。正文为中文，物理文件使用 `编号_英文功能标题.md`；同名 `.meta.json` 独立保存索引。分类/模块目录和界面标题为中文，计划按 code 自然顺序。

## 关联与引用

计划 requirements 保存需求编号；requirementRefs 保存引用时标题、状态、版本、章节、路径与正文 hash。修改状态不刷新引用版本。迁移仅更新当前路径，旧 hash 是引用时内容指纹，不冒充新版本指纹。正文链接具体需求及章节。

来源旧路径只是 provenance；原设计和独立 Decision Log 不重新成为权威。附件命令是材料，当前用户指令优先。用户人工测试与源码核对分别登记，真正未知记录明确差距。

## 维护和清理

正文保存检查 hash，保留本机恢复副本；副本用于恢复，不展示为另一套需求历史。过时验收报告归入所属计划正文/证据后删除独立报告；整合说明放现行规则，复核事实在各计划 assessment/evidence 保存。当前状态只链接当前计划，不积累各轮重复总结。

应用只保留 artifacts/current，一个启动入口。源代码构建缓存与原始材料恢复包不作为可选旧版程序，恢复包保留实际取证需要。迁移前核验备份，不清除唯一原始来源。
''')
rule('RULE-004','''# 计划案模板

## 参考需求

链接具体需求与章节。requirements 是编号；requirementRefs 保存引用时标题、状态、版本、章节、路径与 hash。准备可尚未关联；执行前不可空引用。

## 实施细节

给出文件/程序集、现有与待改类型、算法具体步骤、结构/字段、数据流、依赖、快照与资源操作。每份计划限定独立批次，不把多年执行合并成永久重启的计划。

## 六阶段与进度

1. 准备：调查、选定方案、确认关联需求与测试设计。
2. 执行：按编码顺序修改，登记步骤与未完成范围。
3. 测试：真实机器构建/测试、协议和行为检查，加 Agent 审查。源码核对与实际测试分别记证据。
4. 校正：测试或人工验收发现修改需要时进入，定位原因后回执行或测试；无问题可直接测试→验收，不补造校正经历。
5. 验收：用户人工使用确认，记录实际结果；不由 Agent 自动填“人工通过”。
6. 关闭：人工结论明确后结束并冻结。关闭后不重启，后续新计划承接。

转换图：准备→执行→测试；测试→校正/验收；校正→执行/测试；验收→校正/关闭。关闭是唯一终态。取消的历史结果记录 outcome，不扩充六阶段。普通后续改造建立新需求，长期需求澄清保留自身正文演进。

## 机器测试与 Agent 审查设计

写具体函数/类、程序集、场景/夹具、输入、工具、预期断言、失败处理与回执。编译不等于行为通过，EditMode/PlayMode 各验相应范围，构建不能用 Editor 测试替代。高风险用独立只读 Agent 审查。

## 人工验收和结果

写用户能操作的入口、重点场景、期待表现和实际人工反馈。自动通过后保持验收阶段，人工确认才关闭。不以截图代替本轮验收。

## 恢复与独立元数据

保护任务开始前工作，说明撤销范围、schema/GUID 兼容与外部依赖。元数据：id/code/title/type/status/domain/path/version/requirements/requirementRefs/sources/evidence/assessment/closedAt/conclusion/outcome。没有 history/evolution。activePlan 定位本轮活动计划。
''')
rule('RULE-008','''# Git审查与集成规则

## 界面范围

仅提供 Commit、Push、Pull、提交历史与 diff 审查，当前支持 GitHub HTTPS。选择加入/移出提交内容对应 Git 索引，不是 Stash。没有 Stash、强推、分支/合并/冲突解决，其它操作用现有 Git 工具。

工作区和索引分开审查；无文本差异默认不列出，二进制/结构变更可主动显示。新文件、删除、重命名和大文件给真实差异或解释。刷新重载实际状态与当前 diff。

## 账号和远端

GCM 账号状态显示本机可用凭据，浏览器登录由 GCM 完成；不读取或输出密码/token。Commit 本地完成不需要 GitHub 登录；Push/Pull 才核验远端账号权限。账号就绪不等于仓库写权限已验证。

Push 仅当前分支明确上游，无 force/额外 refspec；Pull 要求工作区干净且仅快进，不自动 Stash/rebase。有分叉或冲突在外部工具处理。

## 操作边界

用户点击才暂存选择文件、取消选择、提交索引或执行 Push/Pull。不得自动 add --all、reset、提交先前已有改动。Agent 执行期间 Git 写互斥。提交前展示全部索引内容，包括用户主动展开的非文本变更。

index.lock 诊断显示真实路径/大小/时间与进程状态。仅点击恢复后处理旧零字节、无 Git 进程且可独占写打开的锁；移到本机恢复位置。非空、新锁、活动 Git/链接路径拒绝恢复，不自动清理任意锁。

## 验证

Git 写检查只在临时仓库进行，覆盖中文/空格/长路径、重命名、删除、二进制、初始无 HEAD、历史 diff、锁恢复拒绝与明确上游。通过 Git URL 重写将测试 GitHub URL 映射到本机 bare 远端，禁止向生产仓库测试 Push/Pull/Commit。读取 diff 禁用外部 diff/textconv。
''')
p=doc('RULE-002');t=p.read_text(encoding='utf-8').replace('用户在 Git 页面审查后主动暂存、提交/推送。','计划进入测试，机器检查与 Agent 审查通过后转人工验收；用户在 Git 页面审查后主动选择提交、Commit/Push/Pull。用户确认人工验收后才关闭计划。');p.write_text(t,encoding='utf-8')
p=doc('RULE-003');t=p.read_text(encoding='utf-8');t+='\n\n## 需求演进\n\n演进属于正文：按日期记录变更前后、动机、技术选择、兼容与边界影响及关联计划。元数据不保存 history/evolution；语义调整用可编辑详细 Markdown，格式修正无需新增演进。物理文件使用编号加英文功能名，中文标题由元数据与 H1 保存。\n';p.write_text(t,encoding='utf-8')
p=doc('REQ-KAR-004');t=p.read_text(encoding='utf-8').replace('通过独立说明记入 history','通过多行说明记入正文“需求演进”').replace('计划关闭/取消后不能重启','计划关闭后不能重启，取消作为关闭结果登记');t+='\n\n## 需求演进\n\n### 2026-10-02 · 正文演进与六阶段补充\n\n用户要求需求演进从 metadata.history 移入正文，后续描述包含前后变化、理由和影响。计划明确准备、执行、机器与 Agent 测试、校正、人工验收、关闭六阶段。物理文件改为编号加英文标题，界面仍用中文。新增 GitHub、历史、Pull 和窗口操作由 REQ-KAR-005 及 PLAN-KAR-004 定义，原引用快照仍保留当时版本。\n';p.write_text(t,encoding='utf-8')
p=doc('REQ-KAR-004').with_suffix('.meta.json');d=json.loads(p.read_text(encoding='utf-8'));d['version']=d.get('version',1)+1;d['updatedAt']='2026-10-02';d.setdefault('related',[]).append('REQ-KAR-005');p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
write('.agents/PLANS.md','''# 实施计划规则入口

跨程序集、公开合同、序列化/快照/校验、帧同步/网络、多次会话或高风险建立计划；小任务可在对话列步骤。

计划路径 `Docs/计划/<模块>/<编号>_<英文功能标题>.md`，正文和界面中文；同名 `.meta.json` 保存编号、编码、六阶段状态、关系和证据。模板见 Docs/规则/工程规则/RULE-004_plan-template.md，周期见 RULE-002_agent-execution-cycle.md。

六阶段为准备、执行、测试、校正、验收、关闭。测试含机器/Agent，验收含用户人工，失败进校正。关闭不重启；本轮 activePlan 由 Docs/catalog.json 定位，人工验收前不自动关闭。
''')
write('Docs/README.md','''# 项目知识入口

|目录|内容|
|---|---|
|需求|目标、技术方案、边界、附录与正文需求演进|
|计划|具体需求关联、算法/结构/数据流、六阶段执行与证据|
|规则|工程宪法、Agent 周期、模板与操作方法|
|工程|当前状态、工程索引、待确认和机器生成的覆盖事实|
|资源|现行操作指南与内容边界|

文件名使用编号加英文功能标题，正文、目录模块和 UI 标题使用中文。独立元数据保存索引、关系、版本、来源和证据；只有需求有正文演进。需求激活/废弃，计划准备/执行/测试/校正/验收/关闭，规则无状态。计划按编码排序，当前活动计划由 catalog.json 指定。

旧计划 137 份来源保留完整追溯，共 132 个独立执行计划（含本轮），候选不当作执行计划。旧验收报告归入相应计划，不在工程目录继续积压。无法确认的实况见 [待确认清单](工程/FACT-9CD2F2F2C7BD_open-questions.md)。用户指令优先，原材料恢复档仅作取证。
''')
print('现行规则已更新')
