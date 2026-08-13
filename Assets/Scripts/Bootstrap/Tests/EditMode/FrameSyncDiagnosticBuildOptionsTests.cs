using NUnit.Framework;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public sealed class FrameSyncDiagnosticBuildOptionsTests
    {
        [Test]
        public void ComposeScriptingDefines_CompilesDiagnosticsOnlyWhenEnabled()
        {
            bool original = EditorTools
                .FrameSyncDiagnosticBuildOptions
                .IncludeAsyncDiagnostics;
            try
            {
                EditorTools.FrameSyncDiagnosticBuildOptions
                    .IncludeAsyncDiagnostics = false;
                CollectionAssert.AreEqual(
                    new[] { "FRAME_SYNC_MOBA_UOS_ONLINE" },
                    EditorTools.FrameSyncDiagnosticBuildOptions
                        .ComposeScriptingDefines(
                            true,
                            "FRAME_SYNC_MOBA_UOS_ONLINE"));
                CollectionAssert.IsEmpty(
                    EditorTools.FrameSyncDiagnosticBuildOptions
                        .ComposeScriptingDefines(
                            false,
                            "FRAME_SYNC_MOBA_UOS_ONLINE"));

                EditorTools.FrameSyncDiagnosticBuildOptions
                    .IncludeAsyncDiagnostics = true;
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "FRAME_SYNC_MOBA_UOS_ONLINE",
                        Unit.FrameSyncDiagnostics.BuildDefine,
                    },
                    EditorTools.FrameSyncDiagnosticBuildOptions
                        .ComposeScriptingDefines(
                            true,
                            "FRAME_SYNC_MOBA_UOS_ONLINE"));
                CollectionAssert.AreEqual(
                    new[]
                    {
                        Unit.FrameSyncDiagnostics.BuildDefine,
                    },
                    EditorTools.FrameSyncDiagnosticBuildOptions
                        .ComposeScriptingDefines(
                            false,
                            "FRAME_SYNC_MOBA_UOS_ONLINE"));
            }
            finally
            {
                EditorTools.FrameSyncDiagnosticBuildOptions
                    .IncludeAsyncDiagnostics = original;
            }
        }
    }
}