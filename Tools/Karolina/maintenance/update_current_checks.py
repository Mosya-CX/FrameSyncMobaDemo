from revise_current import ROOT
p=ROOT/'Tools/Karolina/Karolina.Tests/Program.cs';t=p.read_text(encoding='utf-8')
t=t.replace('await g.Action("push",[],null);var refs=', 'await Git(f.Root,"remote","set-url","origin","https://github.com/karolina-fixture/local.git");await Git(f.Root,"config","url."+remote.Replace("\\\\","/")+".insteadOf","https://github.com/karolina-fixture/local.git");await g.Action("push",[],null);Check((await g.History()).Length==2);Check((await g.CommitDiff((await g.History())[0].Hash)).Contains("two"));await g.Action("pull",[],null);var refs=')
t=t.replace('"金币与资源修订合入中文功能的演进元数据"','"金币与资源修订保存在中文需求正文演进"').replace('s.List().Select(d=>s.Metadata(d.Id).GetRawText())','s.List().Select(d=>s.Read(d.Id))')
# 临时仓库重现旧空锁以及恢复后的真实 Git 索引操作。
anchor='        await Case("scoped amendment retains base;'
test='''        await Case("Git 残留索引锁诊断、拒绝非空锁和恢复后实际提交", async () => {
            using var f=new Fixture();await Git(f.Root,"init");await Git(f.Root,"config","user.name","Fixture");await Git(f.Root,"config","user.email","fixture@local.invalid");var g=new GitService(f.Root);string file="中文 [空格].txt";File.WriteAllText(Path.Combine(f.Root,file),"one");string locked=Path.Combine(f.Root,".git","index.lock");File.WriteAllText(locked,"");File.SetLastWriteTimeUtc(locked,DateTime.UtcNow.AddMinutes(-5));bool failed=false;try{await g.Action("stage",[file],null);}catch(IOException e){failed=e.Message.Contains("index.lock");}Check(failed && (await g.IndexLock()).Exists);await g.RecoverIndexLock();Check(!File.Exists(locked));await g.Action("stage",[file],null);await g.Action("commit",[],"真实提交");Check((await g.History())[0].Title=="真实提交");File.WriteAllText(locked,"nonempty");File.SetLastWriteTimeUtc(locked,DateTime.UtcNow.AddMinutes(-5));bool refused=false;try{await g.RecoverIndexLock();}catch(InvalidOperationException){refused=true;}Check(refused && File.ReadAllText(locked)=="nonempty");
        });
'''
assert anchor in t;t=t.replace(anchor,test+anchor);p.write_text(t,encoding='utf-8')
p=ROOT/'Tools/Karolina/maintenance/verify_lifecycle.py';t=p.read_text(encoding='utf-8').replace('artifacts/lifecycle/','artifacts/current/').replace("'执行中'","'执行'").replace("'待验收'","'测试'").replace("'已关闭'","'关闭'").replace("'草案'","'准备'")
# 不继续使用前一轮固定数量或状态作为当前工程事实。
t=t.replace('==131','==132').replace('== 131','== 132').replace("'0168'","'0169'")
p.write_text(t,encoding='utf-8')
print('检查夹具已按新合同调整')
