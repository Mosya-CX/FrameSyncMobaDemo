using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public sealed class UosBuildMenuTests
    {
        private const string MenuTypeName =
            "FrameSyncMoba.EditorTools.LocalNgoBuildMenu";
        private const string ReleaseWindowTypeName =
            "FrameSyncMoba.EditorTools.ReleaseClientBuildWindow";
        private const string GuardKey =
            "FrameSyncMoba.LocalNgoBuildMenu.LastBuildCompletedUtcTicks." +
            "both-uos";
        private const string ReleaseGuardKey =
            "FrameSyncMoba.LocalNgoBuildMenu.LastBuildCompletedUtcTicks." +
            "client-windows-release";

        [Test]
        public void CombinedUosBuild_HasOneClickMenuEntry()
        {
            Type menuType = FindMenuType();
            MethodInfo method = menuType.GetMethod(
                "BuildUosClientAndServerOnce",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            object[] attributes = method.GetCustomAttributes(
                typeof(MenuItem),
                false);
            Assert.That(attributes, Has.Length.EqualTo(1));
            Assert.That(
                ((MenuItem)attributes[0]).menuItem,
                Is.EqualTo(
                    "FrameSyncMoba/Build Local NGO/" +
                    "Build Client + Server (UOS, Once)"));
        }

        [Test]
        public void ClearBuildGuard_ClearsCombinedUosGuard()
        {
            Type menuType = FindMenuType();
            MethodInfo clearMethod = menuType.GetMethod(
                "ClearBuildBothRetryGuard",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(clearMethod, Is.Not.Null);

            SessionState.SetString(GuardKey, "test-value");
            SessionState.SetString(ReleaseGuardKey, "test-value");
            clearMethod.Invoke(null, null);

            Assert.That(
                SessionState.GetString(GuardKey, string.Empty),
                Is.Empty);
            Assert.That(
                SessionState.GetString(ReleaseGuardKey, string.Empty),
                Is.Empty);
        }

        [Test]
        public void ReleaseClientBuild_HasOptionalWindowEntry()
        {
            Type windowType = FindType(ReleaseWindowTypeName);
            MethodInfo openMethod = windowType.GetMethod(
                "Open",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(openMethod, Is.Not.Null);
            object[] attributes = openMethod.GetCustomAttributes(
                typeof(MenuItem),
                false);
            Assert.That(attributes, Has.Length.EqualTo(1));
            Assert.That(
                ((MenuItem)attributes[0]).menuItem,
                Is.EqualTo(
                    "FrameSyncMoba/Build Local NGO/" +
                    "Build Release Client (Optional CDN Package)..."));
            FieldInfo defaultOption = windowType.GetField(
                "DefaultBuildCdnPackage",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(defaultOption, Is.Not.Null);
            Assert.That(defaultOption.GetRawConstantValue(), Is.False);
        }

        [Test]
        public void ReleaseClientBuild_UsesIsolatedAaalolOutput()
        {
            Type menuType = FindMenuType();
            string uosRoot = ReadPublicConstant(
                menuType,
                "UosClientBuildRoot");
            string releaseRoot = ReadPublicConstant(
                menuType,
                "ReleaseClientBuildRoot");
            string executableName = ReadPublicConstant(
                menuType,
                "ReleaseClientExecutableName");

            Assert.That(uosRoot, Is.EqualTo("Builds/UosClient"));
            Assert.That(releaseRoot, Is.EqualTo("Builds/Demo/Game"));
            Assert.That(releaseRoot, Is.Not.EqualTo(uosRoot));
            Assert.That(executableName, Is.EqualTo("AAALOL.exe"));

            MethodInfo pathMethod = menuType.GetMethod(
                "GetReleaseClientExecutablePath",
                BindingFlags.Public | BindingFlags.Static);
            string path = (string)pathMethod.Invoke(null, null);
            Assert.That(
                path.Replace('\\', '/'),
                Does.EndWith("/Builds/Demo/Game/AAALOL.exe"));
            Assert.That(
                path.Replace('\\', '/'),
                Does.Not.Contain("/Builds/UosClient/"));
        }

        [Test]
        public void ReleaseClientBuild_ComposesValidatedCdnPackagerInvocation()
        {
            Type menuType = FindMenuType();
            MethodInfo normalize = menuType.GetMethod(
                "NormalizeReleaseClientVersion",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo compose = menuType.GetMethod(
                "ComposeCdnPackagerArguments",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(
                normalize.Invoke(null, new object[] { " 2.3.4 " }),
                Is.EqualTo("2.3.4"));
            Assert.Throws<TargetInvocationException>(
                () => normalize.Invoke(null, new object[] { "../../escape" }));

            string arguments = (string)compose.Invoke(
                null,
                new object[]
                {
                    "E:/Project/Tools/UosGameLauncher/Launcher.csproj",
                    "E:/Project/Builds/Demo/Game",
                    "E:/Project/Builds/CdnUpload/2.3.4",
                    "2.3.4",
                });
            Assert.That(arguments, Does.Contain("--build-cdn-package"));
            Assert.That(arguments, Does.Contain("--version 2.3.4"));
            Assert.That(arguments, Does.Contain(
                "--source \"E:/Project/Builds/Demo/Game\""));
            Assert.That(arguments, Does.Contain(
                "--output \"E:/Project/Builds/CdnUpload/2.3.4\""));
        }

        [Test]
        public void ReleaseClientBuild_RefusesToCleanAnyOtherDirectory()
        {
            Type menuType = FindMenuType();
            MethodInfo prepare = menuType.GetMethod(
                "PrepareReleaseClientOutput",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(prepare, Is.Not.Null);

            var exception = Assert.Throws<TargetInvocationException>(
                () => prepare.Invoke(
                    null,
                    new object[]
                    {
                        "E:/Project/Builds/UosClient/" +
                        "FrameSyncMobaClient.exe",
                    }));
            Assert.That(
                exception.InnerException,
                Is.TypeOf<InvalidOperationException>());
        }

        private static Type FindMenuType()
        {
            return FindType(MenuTypeName);
        }

        private static Type FindType(string typeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(typeName, false);
                if (type != null)
                    return type;
            }

            Assert.Fail("Unable to find " + typeName + ".");
            return null;
        }

        private static string ReadPublicConstant(
            Type type,
            string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null);
            return (string)field.GetRawConstantValue();
        }
    }
}
