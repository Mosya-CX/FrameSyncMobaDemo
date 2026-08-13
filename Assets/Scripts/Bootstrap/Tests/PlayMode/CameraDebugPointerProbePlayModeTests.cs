using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class CameraDebugPointerProbePlayModeTests
    {
        [UnityTest]
        public IEnumerator LightweightProxySelection_UsesNearestThenStableId()
        {
            var firstObject = new GameObject("ProxyA");
            var secondObject = new GameObject("ProxyB");
            try
            {
                firstObject.transform.position = new Vector3(-1f, 0f, 0f);
                secondObject.transform.position = new Vector3(1f, 0f, 0f);
                CameraDebugSelectableProxy first =
                    firstObject.AddComponent<CameraDebugSelectableProxy>();
                CameraDebugSelectableProxy second =
                    secondObject.AddComponent<CameraDebugSelectableProxy>();
                first.Configure(20, 1, null);
                second.Configure(10, 2, null);
                yield return null;

                CameraDebugSelectableProxy selected =
                    CameraDebugPointerProbe.SelectNearest(
                        Vector3.zero,
                        2f,
                        out int candidates,
                        out float distance);

                Assert.That(candidates, Is.EqualTo(2));
                Assert.That(selected, Is.SameAs(second));
                Assert.That(distance, Is.EqualTo(1f).Within(.001f));
            }
            finally
            {
                Object.Destroy(firstObject);
                Object.Destroy(secondObject);
            }
            yield return null;
        }
    }
}
