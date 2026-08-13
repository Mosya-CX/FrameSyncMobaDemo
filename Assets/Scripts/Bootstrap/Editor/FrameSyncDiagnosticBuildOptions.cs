using System.Collections.Generic;
using FrameSyncMoba.Unit;
using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.EditorTools
{
    /// <summary>
    /// Per-build diagnostic compilation switch. It never mutates global
    /// PlayerSettings symbols, so a disabled package cannot accidentally reuse
    /// an enabled define outside the selected BuildPlayerOptions invocation.
    /// </summary>
    public static class FrameSyncDiagnosticBuildOptions
    {
        public const string DiagnosticsBuildDefine =
            FrameSyncDiagnostics.BuildDefine;
        public const string MenuPath =
            "FrameSyncMoba/Build Diagnostics/Include Async Diagnostics";
        public const string EditorPreferenceKey =
            "FrameSyncMoba.Build.IncludeAsyncDiagnostics";

        public static bool IncludeAsyncDiagnostics
        {
            get => EditorPrefs.GetBool(
                EditorPreferenceKey,
                true);
            set
            {
                EditorPrefs.SetBool(
                    EditorPreferenceKey,
                    value);
                Menu.SetChecked(MenuPath, value);
            }
        }

        [MenuItem(MenuPath)]
        public static void ToggleAsyncDiagnostics()
        {
            IncludeAsyncDiagnostics =
                !IncludeAsyncDiagnostics;
            Debug.Log(
                "[Build] Async diagnostics will be " +
                (IncludeAsyncDiagnostics
                    ? "included in subsequent packages."
                    : "fully compiled out of subsequent packages."));
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggleAsyncDiagnostics()
        {
            Menu.SetChecked(
                MenuPath,
                IncludeAsyncDiagnostics);
            return !BuildPipeline.isBuildingPlayer;
        }

        public static string[] ComposeScriptingDefines(
            bool uosOnline,
            string uosOnlineDefine)
        {
            var defines = new List<string>(2);
            if (uosOnline &&
                !string.IsNullOrWhiteSpace(uosOnlineDefine))
            {
                defines.Add(uosOnlineDefine);
            }
            if (IncludeAsyncDiagnostics)
                defines.Add(DiagnosticsBuildDefine);
            return defines.ToArray();
        }
    }
}
