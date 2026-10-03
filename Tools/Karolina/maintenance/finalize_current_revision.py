"""记录本轮实际证据，计划停在人工验收；不提交工程。"""
import os,json,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
os.environ['KAROLINA_DESKTOP_EXE']=str(ROOT/'Tools/Karolina/artifacts/current/Karolina.Desktop.exe')
from verify_workbench import Service
local=Path(os.environ['LOCALAPPDATA'])/'Karolina/revision-inventory'
base=json.loads((local/'current-acceptance.json').read_text(encoding='utf-8'));extra=json.loads((local/'current-additions.json').read_text(encoding='utf-8'));docs=json.loads((local/'current-documents.json').read_text(encoding='utf-8'))
assert len(base['checks'])==43 and len(extra['checks'])==15 and not docs['problems'] and not docs['changedProtectedFiles']
s=Service()
try:
 id='PLAN-KAR-004';snapshot=s.api('document/'+id);body=snapshot['markdown'];body=body.replace('- [ ] 执行：','- [x] 执行：').replace('- [ ] 测试：','- [x] 测试：')
 body=body.replace('本计划执行中。机器/Agent 测试证据完成后填入本节；人工未验收前保持“验收”，不声称真实 GitHub Push 或 Unity 行为已通过。',f'''执行与机器/Agent 测试已完成，计划进入人工验收，尚未关闭。

- Release 构建：0 错误、0 警告。
- 核心检查：38/38；真实临时 Git 索引、Commit、明确 Push、快进 Pull、history/diff、长路径和索引锁恢复均覆盖。GitHub 测试 URL 重写到本机 bare 远端，没有向 GitHub 写入测试内容。
- HTTP/DOM 回归：43/43。最新正文读取中位数 {base['metrics']['readMedianMs']:.2f} ms，最大 {base['metrics']['readMaxMs']:.2f} ms，仅是本机测量。
- 本轮专项：15/15；需求多行正文演进、英文文件名、提交历史 diff、挂起 turn/start 完全退出/写锁释放、真实原生最小化及关闭。窗口关闭约 3–4 秒结束服务；没有截图。
- 文档取证：273 个正文/元数据/catalog 条目，123 需求、132 计划；所有 Docs 物理目录/文件 ASCII，真实本地链接和引用路径零问题。
- 原工程保护：2953 个既有 Unity 文件哈希未变。只核验 Codex 模型/账号真实连接，Unity 处于未开启/连接失败，不宣称 Unity 行为已验收。
- 工程 index.lock 复现为旧零字节残留且没有 Git 进程，安全移到本机恢复位置；索引前后 SHA 相同。残留产生者无可靠记录，未断言是某个工具制造；以后遇到残留通过可见诊断恢复，不吞掉错误。
- GitHub 已识别本机 GCM 账号，远端写权限仍在用户实际 Push 时验证。没有替用户暂存、Commit、Push 或 Pull 本工程。
- 独立只读 Agent 两轮审查闭合，无剩余 P1/P2；审查者不宣称独立重跑机器检查。

机器回执在本机 `Karolina/revision-inventory/current-acceptance.json`、`current-additions.json`、`current-documents.json`。旧轮报告已归入所属计划，不新增当前目录中的独立验收报告。

## 人工验收重点

1. 通过唯一启动入口打开当前程序，最小化/恢复以及原生 X 或完全退出，确认不残留运行任务。
2. 需求演进在正文可读可编辑；英文路径不影响中文标题/模块，信息与关联仍正确。计划的测试、校正和人工验收分别明确。
3. Git 页查看实际 GitHub 账号/上游、文本 diff 和 Commit 历史，选择本次提交内容；真实生产 Commit/Push/Pull 由用户主动操作，服务器权限失败应清晰可见。

如人工验收发现问题，进入校正后重新执行/测试；确认满意后填写人工结论并关闭。本轮未获得人工通过，因此保持“验收”。''')
 s.api('document/save',dict(id=id,markdown=body,expectedHash=snapshot['hash'],changeSummary=None))
 for stage in ['测试','验收']:
  m=s.api('document/'+id+'/metadata');s.api('document/details',dict(id=id,expectedMetadataHash=m['hash'],status=stage,requirements=None,conclusion=None))
finally:s.close()
p=ROOT/'Docs/rules/engineering/RULE-003_requirement-template.md';t=p.read_text(encoding='utf-8').replace('历史需求修订作为本功能 history','历史需求修订写入本功能正文“需求演进”').replace('related、history、sources','related、sources');p.write_text(t,encoding='utf-8')
p=ROOT/'Docs/engineering/FACT-82B7CF40B5E6_current-state.md';t=p.read_text(encoding='utf-8');start=t.index('## 当前任务');end=t.index('## 既有未完成工作');t=t[:start]+'''## 当前任务

[当前版本与最小 Git 工作流实施](../plans/karolina/PLAN-KAR-004_current-version-git-implementation.md)已完成编码与机器/Agent 检查，处于人工验收。单一当前程序、全英文物理路径、中文正文、需求正文演进、六阶段计划、GitHub 最小 Git 工作台与窗口生命周期已落地。

旧独立验收报告归入当期计划；历史执行记录的源码/人工确认在各计划 assessment/evidence 保存。

'''+t[end:];latest=t.find('## 最新证据');t=t[:latest]+'''## 最新证据

Release 0错误0警告，核心38/38，HTTP/DOM43/43，本轮专项15/15，文档273项零断链且全部路径ASCII，2953受保护Unity文件未变。真实原生最小化/X/完全退出、挂起RPC退出与工程锁释放均有行为证据；独立只读审查闭合，无剩余P1/P2。无截图验收。

GitHub 本机账号凭据已识别，工程过期空索引锁已移走且原暂存索引哈希未变；没有自动执行工程暂存/Commit/Push/Pull。Unity本轮未开启，不宣称真实Unity编译或行为通过。人工验收重点和局限详见当前计划。
''';p.write_text(t,encoding='utf-8')
print('本轮计划已转人工验收，未关闭')
