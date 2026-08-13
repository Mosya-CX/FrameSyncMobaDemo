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
        private const string GuardKey =
            "FrameSyncMoba.LocalNgoBuildMenu.LastBuildCompletedUtcTicks." +
            "both-uos";

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
            clearMethod.Invoke(null, null);

            Assert.That(
                SessionState.GetString(GuardKey, string.Empty),
                Is.Empty);
        }

        private static Type FindMenuType()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(MenuTypeName, false);
                if (type != null)
                    return type;
            }

            Assert.Fail("Unable to find " + MenuTypeName + ".");
            return null;
        }
    }
}
