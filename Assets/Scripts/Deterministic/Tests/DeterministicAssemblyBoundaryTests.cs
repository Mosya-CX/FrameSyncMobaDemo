using System;
using System.Linq;
using NUnit.Framework;

namespace FrameSyncMoba.Deterministic.Tests
{
    public sealed class DeterministicAssemblyBoundaryTests
    {
        private static readonly string[] ForbiddenDirectReferences =
        {
            "UnityEngine",
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
            string[] references = typeof(DeterministicRandomService)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            foreach (string forbiddenPrefix in ForbiddenDirectReferences)
            {
                Assert.That(
                    references.Any(reference => reference.StartsWith(forbiddenPrefix, StringComparison.Ordinal)),
                    Is.False,
                    $"FrameSyncMoba.Deterministic directly references forbidden assembly prefix '{forbiddenPrefix}'.");
            }
        }
    }
}
