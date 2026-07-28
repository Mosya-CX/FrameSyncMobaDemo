using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Presentation
{
    /// <summary>
    /// Presentation Design v13.2 section 1.4 - local rollback cache that
    /// prevents duplicate playback and maintains visual continuity across
    /// prediction rollbacks.
    ///
    /// Stored outside GameplaySnapshot; never enters SharedGameplayChecksum.
    /// Uses PresentationEventId from FrameSyncMoba.Unit namespace.
    /// </summary>
    [Serializable]
    public struct PresentationSnapshot
    {
        public PresentationEventRecord[] ExpectedEvents;
        public PlayingEventRecord[] PlayingEvents;
        public CompletedOneShotRecord[] CompletedOneShots;

        public static readonly PresentationSnapshot Empty = new PresentationSnapshot
        {
            ExpectedEvents = Array.Empty<PresentationEventRecord>(),
            PlayingEvents = Array.Empty<PlayingEventRecord>(),
            CompletedOneShots = Array.Empty<CompletedOneShotRecord>(),
        };

        public bool IsEmpty =>
            (ExpectedEvents == null || ExpectedEvents.Length == 0)
            && (PlayingEvents == null || PlayingEvents.Length == 0)
            && (CompletedOneShots == null || CompletedOneShots.Length == 0);
    }

    [Serializable]
    public struct PresentationEventRecord
    {
        public FrameSyncMoba.Unit.PresentationEventId EventId;
        public int ExpectedLogicTick;
        public PresentationEventKind Kind;
    }

    [Serializable]
    public struct PlayingEventRecord
    {
        public FrameSyncMoba.Unit.PresentationEventId EventId;
        public int InstanceId;
        public int StartedLogicTick;
        public fp RemainingTimeTicks;
    }

    [Serializable]
    public struct CompletedOneShotRecord
    {
        public FrameSyncMoba.Unit.PresentationEventId EventId;
        public int CompletedLogicTick;
    }

    public enum PresentationEventKind : byte
    {
        VfxOneShot = 0,
        VfxLoop = 1,
        SfxOneShot = 2,
        HitReaction = 3,
        DeathAnimation = 4,
        AttackSfx = 5,
    }
}
