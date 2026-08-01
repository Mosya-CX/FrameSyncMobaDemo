using NUnit.Framework;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class ClientUiActionRouterTests
    {
        [Test]
        public void UiActions_AreForwardedToApplicationOwner()
        {
            var root = new GameObject(
                "ClientUiActionRouterTest");
            try
            {
                ClientUiActionRouter router =
                    root.AddComponent<ClientUiActionRouter>();
                int selected = 0;
                int locked = 0;
                bool ready = false;
                bool returned = false;
                router.Bind(
                    value => selected = value,
                    value => locked = value,
                    value => ready = value,
                    () => returned = true);

                router.SelectHero(1001);
                router.LockHero(1001);
                router.SetReady(true);
                router.ReturnToMainMenu();

                Assert.That(selected, Is.EqualTo(1001));
                Assert.That(locked, Is.EqualTo(1001));
                Assert.That(ready, Is.True);
                Assert.That(returned, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnboundAction_FailsVisibly()
        {
            var root = new GameObject(
                "UnboundClientUiActionRouterTest");
            try
            {
                ClientUiActionRouter router =
                    root.AddComponent<ClientUiActionRouter>();

                Assert.Throws<System.InvalidOperationException>(
                    () => router.SetReady(true));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
