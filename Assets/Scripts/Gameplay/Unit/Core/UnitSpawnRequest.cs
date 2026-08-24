using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public enum UnitSpawnReason : byte
    {
        Unspecified = 0,
    }

    public readonly struct UnitSpawnRequest
    {
        public readonly int UnitPrototypeId;
        public readonly GameplayParticipantId GameplayParticipantId;
        public readonly TeamId TeamId;
        public readonly fp2 Position;
        public readonly fp2 Forward;
        public readonly UnitUid OwnerUid;
        public readonly UnitSpawnReason Reason;

        public UnitSpawnRequest(
            int unitPrototypeId,
            GameplayParticipantId gameplayParticipantId,
            TeamId teamId,
            fp2 position,
            fp2 forward,
            UnitUid ownerUid = default,
            UnitSpawnReason reason = UnitSpawnReason.Unspecified)
        {
            UnitPrototypeId = unitPrototypeId;
            GameplayParticipantId = gameplayParticipantId;
            TeamId = teamId;
            Position = position;
            Forward = forward;
            OwnerUid = ownerUid;
            Reason = reason;
        }
    }
}
