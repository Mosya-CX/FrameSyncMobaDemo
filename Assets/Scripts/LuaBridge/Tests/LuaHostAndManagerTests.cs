using NUnit.Framework;

namespace FrameSyncMoba.LuaBridge.Tests
{
    /// <summary>
    /// Proves the Lua UI foundation: LuaInit executes through the
    /// StreamingAssets loader, module.New(refs) creates page/cell instances,
    /// and LuaHost drives the fixed lifecycle delegates.
    /// </summary>
    public sealed class LuaHostAndManagerTests
    {
        private LuaManager _manager;

        [SetUp]
        public void SetUp()
        {
            _manager = LuaManager.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            _manager?.Dispose();
            _manager = null;
        }

        [Test]
        public void CreateDefault_ExecutesLuaInit_AndBecomesReady()
        {
            Assert.That(_manager.IsReady, Is.True);
            Assert.That(
                _manager.ReadGlobalInt("_LuaUiInitialized"),
                Is.EqualTo(1));
        }

        [Test]
        public void PageHost_DrivesLuaLifecycle()
        {
            var refs = new[]
            {
                new UIRef("Probe", null),
            };
            using (LuaHost host =
                _manager.CreatePageHost(
                    "UI.Core.TestPage",
                    refs))
            {
                Assert.That(host.IsBound, Is.True);
                host.Show();
                host.Refresh();
                host.Hide();
            }

            Assert.That(
                _manager.ReadGlobalInt("_TestPageShow"),
                Is.EqualTo(1));
            Assert.That(
                _manager.ReadGlobalInt("_TestPageRefresh"),
                Is.EqualTo(1));
            Assert.That(
                _manager.ReadGlobalInt("_TestPageHide"),
                Is.EqualTo(1));
            Assert.That(
                _manager.ReadGlobalInt("_TestPageDispose"),
                Is.EqualTo(1));
        }

        [Test]
        public void TwoPageInstances_AreIndependent()
        {
            using (LuaHost first =
                _manager.CreatePageHost(
                    "UI.Core.TestPage",
                    null))
            using (LuaHost second =
                _manager.CreatePageHost(
                    "UI.Core.TestPage",
                    null))
            {
                first.Show();
                second.Show();
            }

            Assert.That(
                _manager.ReadGlobalInt("_TestPageShow"),
                Is.EqualTo(2));
            Assert.That(
                _manager.ReadGlobalInt("_TestPageDispose"),
                Is.EqualTo(2));
        }

        [Test]
        public void CellHost_SetIndexAndBind_DriveLua()
        {
            using (LuaHost host =
                _manager.CreateCellHost(
                    "UI.Core.TestCell",
                    null))
            {
                host.SetIndex(3);
                host.Bind(7);
            }

            Assert.That(
                _manager.ReadGlobalInt("_TestCellIndex"),
                Is.EqualTo(3));
            Assert.That(
                _manager.ReadGlobalInt("_TestCellData"),
                Is.EqualTo(7));
            Assert.That(
                _manager.ReadGlobalInt("_TestCellDispose"),
                Is.EqualTo(1));
        }

        [Test]
        public void Dispose_Twice_IsSafe()
        {
            LuaHost host =
                _manager.CreatePageHost(
                    "UI.Core.TestPage",
                    null);
            host.Dispose();
            host.Dispose();
        }

        [Test]
        public void ManagerDispose_ReleasesOutstandingHosts()
        {
            // Hosts are intentionally not disposed by the caller: the scene
            // teardown order may destroy cells after UIManager.OnDestroy, so
            // LuaManager must release every outstanding host before closing
            // the LuaEnv. Otherwise xLua throws
            // "try to dispose a LuaEnv with C# callback".
            LuaHost page =
                _manager.CreatePageHost(
                    "UI.Core.TestPage",
                    null);
            LuaHost cell =
                _manager.CreateCellHost(
                    "UI.Core.TestCell",
                    null);

            Assert.DoesNotThrow(
                () => _manager.Dispose());
            Assert.That(
                page.IsDisposed,
                Is.True,
                "Manager dispose must release outstanding page hosts.");
            Assert.That(
                cell.IsDisposed,
                Is.True,
                "Manager dispose must release outstanding cell hosts.");

            // TearDown must tolerate the already-disposed manager.
            _manager = null;
        }

        [Test]
        public void MissingNew_Throws()
        {
            Assert.That(
                () => _manager.CreatePageHost(
                    "UI.Core.UIFormat",
                    null),
                Throws.InvalidOperationException);
        }
    }
}
