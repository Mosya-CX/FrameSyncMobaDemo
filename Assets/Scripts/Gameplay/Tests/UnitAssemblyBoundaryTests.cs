using System;
using System.Linq;
using NUnit.Framework;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class UnitAssemblyBoundaryTests
    {
        private static readonly string[] ForbiddenDirectReferences =
        {
            "UnityEditor",
            "Unity.Netcode",
            "Unity.Networking.Transport",
            "Unity.InputSystem",
            "Unity.UGUI",
            "Unity.TextMeshPro",
            "Unity.UOS",
            "XLua",
        };

        [Test]
        public void RuntimeAssembly_HasNoForbiddenDirectDependency()
        {
            string[] references = typeof(UnitUid)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            foreach (string forbiddenPrefix in ForbiddenDirectReferences)
            {
                Assert.That(
                    references.Any(reference => reference.StartsWith(forbiddenPrefix, StringComparison.Ordinal)),
                    Is.False,
                    $"FrameSyncMoba.Unit directly references forbidden assembly prefix '{forbiddenPrefix}'.");
            }
        }

        [Test]
        public void RuntimeAssembly_HasIntendedDeterministicDependency()
        {
            string[] references = typeof(UnitUid)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(
                references,
                Contains.Item("FrameSyncMoba.Deterministic"),
                "FrameSyncMoba.Unit must depend on FrameSyncMoba.Deterministic so the active-Gameplay gate reads SimulationTickContext.Current.");
        }

        [Test]
        public void RuntimeAssembly_HasRequiredUnityComponentDependency()
        {
            string[] references = typeof(Unit)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(
                references.Any(reference => reference.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.True,
                "FrameSyncMoba.Unit must reference UnityEngine because Unit and UnitHandler are formal MonoBehaviours.");
        }
    }
}
