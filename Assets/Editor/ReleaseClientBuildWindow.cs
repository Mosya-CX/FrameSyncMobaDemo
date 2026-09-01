using System;
using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.EditorTools
{
    public sealed class ReleaseClientBuildWindow : EditorWindow
    {
        public const string MenuPath =
            "FrameSyncMoba/Build Local NGO/" +
            "Build Release Client (Optional CDN Package)...";
        public const bool DefaultBuildCdnPackage = false;

        private string clientVersion = "1.0.0";
        private bool buildCdnPackage =
            DefaultBuildCdnPackage;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<ReleaseClientBuildWindow>(
                true,
                "发布客户端构建",
                true);
            window.minSize = new Vector2(520f, 270f);
            window.Show();
        }

        private void OnEnable()
        {
            try
            {
                clientVersion =
                    LocalNgoBuildMenu.NormalizeReleaseClientVersion(
                        PlayerSettings.bundleVersion);
            }
            catch (ArgumentException)
            {
                clientVersion = "1.0.0";
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "正式客户端",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "输出：Builds/Demo/Game/AAALOL.exe 与 AAALOL_* 配套内容。" +
                "\nBuilds/UosClient 保持为测试包，不会被覆盖。",
                MessageType.Info);

            buildCdnPackage = EditorGUILayout.ToggleLeft(
                "构建成功后生成签名 CDN 分片（可选）",
                buildCdnPackage);
            using (new EditorGUI.DisabledScope(!buildCdnPackage))
            {
                clientVersion = EditorGUILayout.TextField(
                    "客户端版本",
                    clientVersion);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                buildCdnPackage
                    ? "完成 Player 构建后继续生成 Builds/CdnUpload/<版本>/Upload。"
                    : "只构建正式 Player，不生成 CDN 分片。",
                MessageType.None);
            EditorGUILayout.Space(10f);

            using (new EditorGUI.DisabledScope(
                       BuildPipeline.isBuildingPlayer))
            {
                if (GUILayout.Button(
                        "构建正式客户端",
                        GUILayout.Height(38f)))
                {
                    StartBuild();
                }
            }
        }

        private void StartBuild()
        {
            try
            {
                if (buildCdnPackage)
                {
                    clientVersion =
                        LocalNgoBuildMenu.NormalizeReleaseClientVersion(
                            clientVersion);
                }

                bool packageAfterBuild = buildCdnPackage;
                string version = clientVersion;
                Close();
                LocalNgoBuildMenu.BuildReleaseClient(
                    version,
                    packageAfterBuild);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "发布客户端构建失败",
                    exception.Message,
                    "确定");
            }
        }
    }
}