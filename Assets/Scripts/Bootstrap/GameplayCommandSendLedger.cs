using System;
using System.Collections.Generic;
using FrameSyncMoba.FrameSync;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Client application-layer send history. NGO ReliableSequenced owns
    /// retransmission; this ledger prevents identical Gameplay input
    /// identities from being wrapped in a new Bundle every Unity Update.
    /// </summary>
    internal sealed class GameplayCommandSendLedger
    {
        private readonly HashSet<GameplayCommandIdentity> sentIdentities =
            new HashSet<GameplayCommandIdentity>();
        private bool hasObservedRevision;
        private ulong observedRevision;

        public void Reset()
        {
            sentIdentities.Clear();
            hasObservedRevision = false;
            observedRevision = 0;
        }

        public bool TryBuildUnsentCommands(
            CommandCollector collector,
            out ulong contentRevision,
            out List<GameplayCommand> unsentCommands)
        {
            if (collector == null)
                throw new ArgumentNullException(nameof(collector));

            contentRevision = collector.ContentRevision;
            unsentCommands = null;
            if (hasObservedRevision &&
                observedRevision == contentRevision)
            {
                return false;
            }

            List<GameplayCommand> canonical =
                collector.GetCanonicalCommands();
            var pendingIdentities =
                new HashSet<GameplayCommandIdentity>();
            var candidates =
                new List<GameplayCommand>(canonical.Count);
            for (int i = 0; i < canonical.Count; i++)
            {
                GameplayCommand command = canonical[i];
                GameplayCommandIdentity identity =
                    GameplayCommandIdentity.From(command);
                if (sentIdentities.Contains(identity) ||
                    !pendingIdentities.Add(identity))
                {
                    continue;
                }
                candidates.Add(command);
            }

            if (candidates.Count == 0)
            {
                MarkObserved(contentRevision);
                return false;
            }

            unsentCommands = candidates;
            return true;
        }

        public void CommitSuccessfulSend(
            ulong contentRevision,
            IReadOnlyList<GameplayCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                throw new ArgumentException(
                    "A successful Bundle requires at least one command.",
                    nameof(commands));

            for (int i = 0; i < commands.Count; i++)
            {
                sentIdentities.Add(
                    GameplayCommandIdentity.From(commands[i]));
            }
            MarkObserved(contentRevision);
        }

        private void MarkObserved(ulong contentRevision)
        {
            observedRevision = contentRevision;
            hasObservedRevision = true;
        }
    }
}
