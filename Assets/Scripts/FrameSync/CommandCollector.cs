using System;
using System.Collections.Generic;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    public sealed class CommandCollector
    {
        private readonly Dictionary<CommandMergeKey, GameplayCommand> moveCommands =
            new Dictionary<CommandMergeKey, GameplayCommand>();
        private readonly Dictionary<CommandMergeKey, GameplayCommand> attackCommands =
            new Dictionary<CommandMergeKey, GameplayCommand>();
        private readonly List<GameplayCommand> nonMergedCommands = new List<GameplayCommand>();

        public int CommandCount =>
            moveCommands.Count + attackCommands.Count + nonMergedCommands.Count;

        public void BeginTick(int targetTick)
        {
            moveCommands.Clear();
            attackCommands.Clear();
            nonMergedCommands.Clear();
        }

        public void Collect(GameplayCommand command)
        {
            if (command.IsNone) return;
            ValidateHeader(command);

            var key = new CommandMergeKey(
                command.PlayerSlot,
                command.ControlledUnitUid,
                command.TargetTick);

            switch (command.Kind)
            {
                case GameplayCommandKind.Move:
                    CollectLastBySequence(moveCommands, key, command);
                    break;

                case GameplayCommandKind.Attack:
                    CollectLastBySequence(attackCommands, key, command);
                    break;

                default:
                    nonMergedCommands.Add(command);
                    break;
            }
        }

        public List<GameplayCommand> GetCanonicalCommands()
        {
            int total = moveCommands.Count + attackCommands.Count + nonMergedCommands.Count;
            var result = new List<GameplayCommand>(total);
            result.AddRange(moveCommands.Values);
            result.AddRange(attackCommands.Values);
            result.AddRange(nonMergedCommands);
            result.Sort(GameplayCommandCanonicalComparer.Instance);
            return result;
        }

        public void WriteCanonicalBytes(Deterministic.CanonicalByteWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            List<GameplayCommand> commands = GetCanonicalCommands();
            writer.WriteInt32(commands.Count);
            for (int i = 0; i < commands.Count; i++)
            {
                commands[i].WriteCanonicalBytes(writer);
            }
        }

        private static void CollectLastBySequence(
            Dictionary<CommandMergeKey, GameplayCommand> commands,
            CommandMergeKey key,
            GameplayCommand command)
        {
            if (!commands.TryGetValue(key, out GameplayCommand existing)
                || command.CommandSeq >= existing.CommandSeq)
            {
                commands[key] = command;
            }
        }

        private static void ValidateHeader(GameplayCommand command)
        {
            if (!command.ControlledUnitUid.IsValid())
            {
                throw new ArgumentException(
                    "GameplayCommand requires a valid ControlledUnitUid.", nameof(command));
            }

            if (command.Header.SchemaVersion != CommandHeader.CurrentSchemaVersion)
            {
                throw new ArgumentException(
                    $"Unsupported GameplayCommand schema {command.Header.SchemaVersion}.", nameof(command));
            }
        }

        private readonly struct CommandMergeKey : IEquatable<CommandMergeKey>
        {
            private readonly int playerSlot;
            private readonly UnitUid controlledUnitUid;
            private readonly int targetTick;

            public CommandMergeKey(int playerSlot, UnitUid controlledUnitUid, int targetTick)
            {
                this.playerSlot = playerSlot;
                this.controlledUnitUid = controlledUnitUid;
                this.targetTick = targetTick;
            }

            public bool Equals(CommandMergeKey other)
            {
                return playerSlot == other.playerSlot
                    && controlledUnitUid == other.controlledUnitUid
                    && targetTick == other.targetTick;
            }

            public override bool Equals(object obj) =>
                obj is CommandMergeKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = playerSlot;
                    hash = (hash * 397) ^ controlledUnitUid.GetHashCode();
                    hash = (hash * 397) ^ targetTick;
                    return hash;
                }
            }
        }

        private sealed class GameplayCommandCanonicalComparer : IComparer<GameplayCommand>
        {
            public static readonly GameplayCommandCanonicalComparer Instance =
                new GameplayCommandCanonicalComparer();

            public int Compare(GameplayCommand x, GameplayCommand y)
            {
                int comparison = x.TargetTick.CompareTo(y.TargetTick);
                if (comparison != 0) return comparison;

                comparison = x.PlayerSlot.CompareTo(y.PlayerSlot);
                if (comparison != 0) return comparison;

                comparison = x.ControlledUnitUid.CompareTo(y.ControlledUnitUid);
                if (comparison != 0) return comparison;

                return x.CommandSeq.CompareTo(y.CommandSeq);
            }
        }
    }
}
