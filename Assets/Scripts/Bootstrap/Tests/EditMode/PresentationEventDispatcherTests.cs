using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public sealed class PresentationEventDispatcherTests
    {
        private GameObject root;
        private PresentationEventDispatcher dispatcher;
        private CountingVfxHandler vfx;
        private CountingSfxHandler sfx;

        [SetUp]
        public void SetUp()
        {
            VisualEventOutput.Clear();
            root = new GameObject(
                "PresentationDispatcherTest");
            dispatcher = root.AddComponent<
                PresentationEventDispatcher>();
            vfx = new CountingVfxHandler();
            sfx = new CountingSfxHandler();
            dispatcher.RegisterVfxHandler(vfx);
            dispatcher.RegisterSfxHandler(sfx);
        }

        [TearDown]
        public void TearDown()
        {
            VisualEventOutput.Clear();
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void Replay_DoesNotDispatchCompletedEventAgain()
        {
            PresentationEventId id =
                CreateId(10, 1, 20);

            SubmitVfx(id);
            dispatcher.DispatchCurrentFrame();
            SubmitVfx(id);
            dispatcher.DispatchCurrentFrame();

            Assert.That(vfx.Count, Is.EqualTo(1));
        }

        [Test]
        public void CompleteIdentity_KeepsDistinctEvents()
        {
            SubmitVfx(CreateId(10, 1, 20));
            SubmitVfx(CreateId(10, 2, 20));
            SubmitVfx(CreateId(10, 2, 21));

            dispatcher.DispatchCurrentFrame();

            Assert.That(vfx.Count, Is.EqualTo(3));
        }

        [Test]
        public void VfxAndSfx_UseIndependentHistories()
        {
            PresentationEventId id =
                CreateId(12, 3, 30);
            SubmitVfx(id);
            VisualEventOutput.SubmitSfx(
                new SfxEvent
                {
                    Id = id,
                    SfxDefId = 30,
                });

            dispatcher.DispatchCurrentFrame();

            Assert.That(vfx.Count, Is.EqualTo(1));
            Assert.That(sfx.Count, Is.EqualTo(1));
        }

        private static PresentationEventId CreateId(
            int tick,
            int sequence,
            int key)
        {
            return new PresentationEventId
            {
                SourceLogicTick = tick,
                SourceKind =
                    PresentationSourceKind.Unit,
                SourceRuntimeUid =
                    new UnitUid(1, 2, 3),
                EventSequence = sequence,
                EventKey = key,
            };
        }

        private static void SubmitVfx(
            in PresentationEventId id)
        {
            VisualEventOutput.SubmitVfx(
                new VfxEvent
                {
                    Id = id,
                    VfxDefId = id.EventKey,
                });
        }

        private sealed class CountingVfxHandler :
            IVfxHandler
        {
            public int Count { get; private set; }

            public void OnVfxEvent(
                in VfxEvent evt)
            {
                Count++;
            }
        }

        private sealed class CountingSfxHandler :
            ISfxHandler
        {
            public int Count { get; private set; }

            public void OnSfxEvent(
                in SfxEvent evt)
            {
                Count++;
            }
        }
    }
}
