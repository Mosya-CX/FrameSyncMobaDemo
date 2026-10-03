"""本轮工作台修订；迁移后由标记拒绝重复执行。"""
from pathlib import Path
import json,re,os,zipfile,hashlib
ROOT=Path(__file__).resolve().parents[3]
def write(p,text):
 p=ROOT/p;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text,encoding='utf-8')
def dump(p,obj):write(p,json.dumps(obj,ensure_ascii=False,indent=2)+'\n')
def replace(p,old,new):
 f=ROOT/p;t=f.read_text(encoding='utf-8-sig');assert old in t,(p,old[:80]);write(p,t.replace(old,new))

def implementation():
 p='Tools/Karolina/Karolina.Core/DocumentLibrary.cs'
 replace(p,'("草案" or "执行中" or "暂缓" or "待验收" or "已关闭" or "已取消")','("准备" or "执行" or "测试" or "校正" or "验收" or "关闭")')
 replace(p,'string[]? requirements = null)','string[]? requirements = null, string? englishTitle = null)')
 replace(p,'string relative = $"Docs/{category}/{domain}/{title}.md", path = SafePath(relative), metadata = path[..^3] + ".meta.json";\n        if (File.Exists(path) || File.Exists(metadata)) throw new InvalidOperationException("此标题已存在，请使用不同标题");\n        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);','if (List().Any(d => d.Type == type && d.Domain == domain && d.Title == title)) throw new InvalidOperationException("此标题已存在，请使用不同标题");')
 replace(p,'string code = type == "plan" ?', '''string slug = string.IsNullOrWhiteSpace(englishTitle) ? "new-" + type : englishTitle.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$") || slug.Length > 90) throw new ArgumentException("英文文件标题请使用小写英文、数字及连字符");
        string relative = $"Docs/{category}/{domain}/{id}_{slug}.md", path = SafePath(relative), metadata = path[..^3] + ".meta.json";
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        string code = type == "plan" ?''')
 replace(p,'"草案"','"准备"')
 replace(p,'"已关闭" or "已取消"','"关闭"')
 replace(p,'{ "准备" => ["执行中", "已取消"], "执行中" => ["暂缓", "待验收", "已取消"], "暂缓" => ["执行中", "已取消"], "待验收" => ["执行中", "已关闭", "已取消"], _ => [] }','{ "准备" => ["执行"], "执行" => ["测试"], "测试" => ["校正", "验收"], "校正" => ["执行", "测试"], "验收" => ["校正", "关闭"], _ => [] }')
 replace(p,'"执行中"','"执行"')
 replace(p,'请先结束或暂缓该计划','请先完成当前计划')
 replace(p,'if (type == "requirement") node["history"] = new System.Text.Json.Nodes.JsonArray();','// 需求演进写入正文，元数据只保存索引、来源与证据。')
 replace(p,'var history = obj["history"] as System.Text.Json.Nodes.JsonArray ?? new();\n            history.Add(JsonSerializer.SerializeToNode(new { date = DateTimeOffset.Now, summary = changeSummary.Trim(), version = version + 1 })); obj["history"] = history;','markdown = AppendEvolution(markdown, changeSummary.Trim(), version + 1);')
 replace(p,'if (changeSummary.Length > 2000)','if (changeSummary.Length > 12000)')
 replace(p,'obj["version"] = version + 1;', 'obj.Remove("history"); obj.Remove("evolution");\n        obj["version"] = version + 1;')
 replace(p,'private static void AssertIdentity','''private static string AppendEvolution(string markdown, string description, int version)
    {
        string item = $"\\n### {DateTimeOffset.Now:yyyy-MM-dd HH:mm} · 修订 {version}\\n\\n{description}\\n\\n";
        var section = new System.Text.RegularExpressions.Regex(@"^## 需求演进[^\\r\\n]*\\r?\\n.*?(?=^## |\\z)", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Singleline);
        return section.IsMatch(markdown) ? section.Replace(markdown, m => m.Value.TrimEnd() + "\\n" + item, 1) : markdown.TrimEnd() + "\\n\\n## 需求演进\\n" + item;
    }
    private static void AssertIdentity''')
 replace(p,'string bodyPath = SafePath(entry.Path), oldBody = File.ReadAllText(bodyPath), body = oldBody;','''string bodyPath = SafePath(entry.Path), oldBody = File.ReadAllText(bodyPath), body = oldBody;
        if (entry.Type == "requirement" && status != null && status != entry.Status)
            body = AppendEvolution(body, $"需求适用状态由「{entry.Status}」改为「{status}」。\\n\\n原因与影响：{(string.IsNullOrWhiteSpace(conclusion) ? "请在正文补充状态调整依据及承接需求。" : conclusion.Trim())}", obj["version"]!.GetValue<int>());
        obj.Remove("history"); obj.Remove("evolution");''')
 replace(p,'草案尚未执行。','准备阶段，尚未执行。')
 replace(p,'- [ ] 调查现状\\n- [ ] 实现\\n- [ ] 验收','- [ ] 准备：确认需求、方案与测试设计\\n- [ ] 执行：按实施细节落地\\n- [ ] 测试：机器检查与独立 Agent 审查\\n- [ ] 校正：处理机器、Agent 或人工发现的问题后复测\\n- [ ] 验收：用户人工使用确认\\n- [ ] 关闭：登记人工验收结论并冻结本次计划')
 p='Tools/Karolina/Karolina.Desktop/Workbench.cs'
 replace(p,'string[]? Requirements);','string[]? Requirements, string? EnglishTitle = null);')
 replace(p,'input.Domain, input.Requirements)','input.Domain, input.Requirements, input.EnglishTitle)')

if __name__=='__main__':implementation()
