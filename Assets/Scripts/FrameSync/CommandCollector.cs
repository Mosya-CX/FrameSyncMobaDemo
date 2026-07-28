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
        private readonly Dictionary<UseItemMergeKey, GameplayCommand> useItemCommands =
            new Dictionary<UseItemMergeKey, GameplayCommand>();
        private readonly List<GameplayCommand> nonMergedCommands = new List<GameplayCommand>();

        public int CommandCount =>
            moveCommands.Count + attackCommands.Count +
            useItemCommands.Count + nonMergedCommands.Count;

        public void BeginTick(int targetTick)
        {
            moveCommands.Clear();
            attackCommands.Clear();
            useItemCommands.Clear();
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

                case GameplayCommandKind.UseItem:
                    CollectLastBySequence(
                        useItemCommands,
                        new UseItemMergeKey(
                            command.PlayerSlot,
                            command.ControlledUnitUid,
                            command.TargetTick,
                            command.SourceSlot),
                        command);
                    break;

                default:
                    nonMergedCommands.Add(command);
                    break;
            }
        }

        public List<GameplayCommand> GetCanonicalCommands()
        {
            int total = moveCommands.Count + attackCommands.Count +
                useItemCommands.Count + nonMergedCommands.Count;
            var result = new List<GameplayCommand>(total);
            result.AddRange(moveCommands.Values);
            result.AddRange(attackCommands.Values);
            result.AddRange(useItemCommands.Values);
            result.AddRange(nonMergedCommands);
            result.Sort(GameplayCommandCanonicalComparer.Instance);
            return result;
        }

        public List<GameplayCommand> ConsumeCanonicalCommands(int targetTick)
        {
            List<GameplayCommand> allCommands = GetCanonicalCommands();
            var result = new List<GameplayCommand>();
            for (int i = 0; i < allCommands.Count; i++)
            {
                GameplayCommand command = allCommands[i];
                if (command.TargetTick != targetTick) continue;
                result.Add(command);
                var key = new CommandMergeKey(
                    command.PlayerSlot,
                    command.ControlledUnitUid,
                    command.TargetTick);
                if (command.Kind == GameplayCommandKind.Move)
                    moveCommands.Remove(key);
                else if (command.Kind == GameplayCommandKind.Attack)
                    attackCommands.Remove(key);
                else if (command.Kind == GameplayCommandKind.UseItem)
                    useItemCommands.Remove(
                        new UseItemMergeKey(
                            command.PlayerSlot,
                            command.ControlledUnitUid,
                            command.TargetTick,
                            command.SourceSlot));
            }

            for (int i = nonMergedCommands.Count - 1; i >= 0; i--)
            {
                if (nonMergedCommands[i].TargetTick == targetTick)
                    nonMergedCommands.RemoveAt(i);
            }
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

        private static void CollectLastBySequence(
            Dictionary<UseItemMergeKey, GameplayCommand> commands,
            UseItemMergeKey key,
            GameplayCommand command)
        {
            if (!commands.TryGetValue(
                    key,
                    out GameplayCommand existing) ||
                command.CommandSeq >= existing.CommandSeq)
                commands[key] = command;
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

        private readonly struct UseItemMergeKey :
            IEquatable<UseItemMergeKey>
        {
            private readonly int playerSlot;
            private readonly UnitUid controlledUnitUid;
            private readonly int targetTick;
            private readonly byte sourceSlot;

            public UseItemMergeKey(
                int playerSlot,
                UnitUid controlledUnitUid,
                int targetTick,
                byte sourceSlot)
            {
                this.playerSlot = playerSlot;
                this.controlledUnitUid = controlledUnitUid;
                this.targetTick = targetTick;
                this.sourceSlot = sourceSlot;
            }

            public bool Equals(UseItemMergeKey other) =>
                playerSlot == other.playerSlot &&
                controlledUnitUid ==
                    other.controlledUnitUid &&
                targetTick == other.targetTick &&
                sourceSlot == other.sourceSlot;

            public override bool Equals(object obj) =>
                obj is UseItemMergeKey other &&
                Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = playerSlot;
                    hash = (hash * 397) ^
                        controlledUnitUid.GetHashCode();
                    hash = (hash * 397) ^ targetTick;
                    hash = (hash * 397) ^ sourceSlot;
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
