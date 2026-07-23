using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Equipment shop runtime: full buy/sell/undo with two-layer RequestCheck/ProcessCommand.
    /// Implements IRollback&lt;EquipmentShopRuntimeSnapshot&gt; (Equipment/Gold v12 §5).
    /// </summary>
    public sealed class EquipmentShopRuntime : IRollback<EquipmentShopRuntimeSnapshot>
    {
        private ShopTraderRuntime[] _tradersByPlayerSlot = Array.Empty<ShopTraderRuntime>();
        private EquipmentDatabase _database;
        private UnitWorld _unitWorld;
        private int _maxPlayers;

        public fp SellRate { get; private set; }

        public void Initialize(
            int maxPlayers,
            EquipmentDatabase database,
            fp sellRate,
            UnitWorld unitWorld)
        {
            if (maxPlayers < 0)
                throw new ArgumentOutOfRangeException(nameof(maxPlayers));
            if (sellRate < fp.zero || sellRate > fp.one)
                throw new ArgumentOutOfRangeException(nameof(sellRate));
            _maxPlayers = maxPlayers;
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _unitWorld = unitWorld ?? throw new ArgumentNullException(nameof(unitWorld));
            SellRate = sellRate;
            _tradersByPlayerSlot = new ShopTraderRuntime[maxPlayers];
        }

        public ShopTraderRuntime GetOrCreateTrader(int playerSlot, UnitUid controlledUnitUid)
        {
            if ((uint)playerSlot >= (uint)_tradersByPlayerSlot.Length) return null;
            var trader = _tradersByPlayerSlot[playerSlot];
            if (trader == null)
            {
                trader = new ShopTraderRuntime { Player = playerSlot, ControlledUnitUid = controlledUnitUid };
                _tradersByPlayerSlot[playerSlot] = trader;
            }
            return trader;
        }

        public ShopTraderRuntime GetTrader(int playerSlot)
        {
            if ((uint)playerSlot >= (uint)_tradersByPlayerSlot.Length) return null;
            return _tradersByPlayerSlot[playerSlot];
        }

        /// <summary>
        /// Computes the effective shop gold delta for a player by summing
        /// all non-reverted GoldDelta values in their OperationLog.
        /// (Equipment/Gold v12 §5.17)
        /// </summary>
        public int ComputeEffectiveShopGoldDelta(int playerSlot)
        {
            var trader = GetTrader(playerSlot);
            if (trader == null) return 0;

            int delta = 0;
            for (int i = 0; i < trader.OperationLog.Count; i++)
            {
                var record = trader.OperationLog[i];
                if (!record.Reverted)
                    delta += record.GoldDelta;
            }
            return delta;
        }

        // ---- Purchase ----

        /// <summary>Builds a purchase plan without side effects (local RequestCheck).</summary>
        public bool TryBuildPurchasePlan(
            int playerSlot, int targetEquipmentId, int currentAvailableGold,
            EquipmentHandler handler, out EquipmentPurchasePlan plan, out EquipmentShopFailureReason failure)
        {
            plan = default;
            failure = EquipmentShopFailureReason.None;

            if (_database == null) { failure = EquipmentShopFailureReason.ItemNotFound; return false; }
            var targetDef = _database.GetDefinition(targetEquipmentId);
            if (targetDef == null) { failure = EquipmentShopFailureReason.ItemNotFound; return false; }

            // Check duplicate finished item
            if (targetDef.Tier == EquipmentTier.Finished && handler.HasDefinition(targetDef))
            { failure = EquipmentShopFailureReason.DuplicateFinishedItem; return false; }

            // Check unique tag conflicts
            if (targetDef.Tags != null)
            {
                for (int i = 0; i < targetDef.Tags.Length; i++)
                {
                    if (handler.HasTag(targetDef.Tags[i]))
                    { failure = EquipmentShopFailureReason.UniqueTagConflict; return false; }
                }
            }

            // Calculate cost and component consumption
            int purchaseCost = targetDef.Value;
            var consumedSlots = new List<int>();

            if (targetDef.Recipe?.Components != null)
            {
                for (int i = 0; i < targetDef.Recipe.Components.Length; i++)
                {
                    var part = targetDef.Recipe.Components[i];
                    int needed = part.Count;
                    for (int s = 0; s < EquipmentHandler.SlotCount && needed > 0; s++)
                    {
                        if (handler.GetSlotDef(s) == part.Item)
                        {
                            consumedSlots.Add(s);
                            purchaseCost -= part.Item.Value;
                            needed--;
                        }
                    }
                    if (needed > 0) { failure = EquipmentShopFailureReason.InvalidRecipe; return false; }
                }
            }

            // Determine destination
            int destSlot;
            bool mergeIntoExisting = false;
            int stackableSlot = handler.FindStackableSlot(targetDef);
            if (stackableSlot >= 0)
            {
                destSlot = stackableSlot;
                mergeIntoExisting = true;
            }
            else
            {
                // Simulate removal of consumed components
                var mockSlots = new bool[EquipmentHandler.SlotCount];
                for (int i = 0; i < consumedSlots.Count; i++)
                    mockSlots[consumedSlots[i]] = true;

                destSlot = -1;
                for (int s = 0; s < EquipmentHandler.SlotCount; s++)
                {
                    if (!mockSlots[s] && handler.GetSlotDef(s) == null)
                    { destSlot = s; break; }
                }
                if (destSlot < 0) { failure = EquipmentShopFailureReason.InventoryFull; return false; }
            }

            if (currentAvailableGold < purchaseCost)
            { failure = EquipmentShopFailureReason.InsufficientGold; return false; }

            plan = new EquipmentPurchasePlan
            {
                TargetEquipmentId = targetEquipmentId,
                PurchaseCost = purchaseCost,
                ConsumedComponentSlots = consumedSlots.ToArray(),
                MergeIntoExistingStack = mergeIntoExisting,
                DestinationSlot = destSlot,
            };
            return true;
        }

        /// <summary>Executes purchase (ProcessCommand, deterministic, all endpoints).</summary>
        public bool ProcessPurchase(
            int playerSlot, EquipmentPurchasePlan plan, EquipmentHandler handler,
            out ShopOperationRecord record)
        {
            record = default;
            var trader = GetTrader(playerSlot);
            if (trader == null) return false;

            var targetDef = _database.GetDefinition(plan.TargetEquipmentId);
            if (targetDef == null) return false;

            int seq = trader.NextOperationSequence++;

            // Remove consumed components
            if (plan.ConsumedComponentSlots != null)
            {
                for (int i = 0; i < plan.ConsumedComponentSlots.Length; i++)
                    handler.Remove(plan.ConsumedComponentSlots[i]);
            }

            // Add or merge target
            if (plan.MergeIntoExistingStack)
                handler.MergeIntoStack(plan.DestinationSlot, 1);
            else
                handler.Add(targetDef, plan.DestinationSlot);

            record = new ShopOperationRecord
            {
                OperationSequence = seq,
                OperationType = EquipmentShopOperationType.Purchase,
                Player = playerSlot,
                ControlledUnitUid = trader.ControlledUnitUid,
                LogicTick = SimulationTickContext.Current.Tick,
                GoldDelta = -plan.PurchaseCost,
                Reverted = false,
                EquipmentRevisionBefore = 0,
                EquipmentRevisionAfter = 0,
            };

            trader.OperationLog.Add(record);
            trader.UndoableOperationStack.Add(seq);
            return true;
        }

        // ---- Sell ----

        /// <summary>Validates a sell request (local RequestCheck).</summary>
        public bool TrySell(
            int playerSlot, int slot, EquipmentHandler handler,
            out int sellValue, out EquipmentShopFailureReason failure)
        {
            sellValue = 0;
            failure = EquipmentShopFailureReason.None;

            var def = handler.GetSlotDef(slot);
            if (def == null) { failure = EquipmentShopFailureReason.EmptySlot; return false; }

            sellValue = (int)((fp)def.Value * SellRate);
            if (sellValue <= 0) sellValue = 1;
            return true;
        }

        /// <summary>Executes sell (ProcessCommand).</summary>
        public bool ProcessSell(
            int playerSlot, int slot, EquipmentHandler handler, int sellValue,
            out ShopOperationRecord record)
        {
            record = default;
            var trader = GetTrader(playerSlot);
            if (trader == null) return false;

            int seq = trader.NextOperationSequence++;
            handler.Remove(slot);

            record = new ShopOperationRecord
            {
                OperationSequence = seq,
                OperationType = EquipmentShopOperationType.Sell,
                Player = playerSlot,
                ControlledUnitUid = trader.ControlledUnitUid,
                LogicTick = SimulationTickContext.Current.Tick,
                GoldDelta = sellValue,
                Reverted = false,
                EquipmentRevisionBefore = 0,
                EquipmentRevisionAfter = 0,
            };

            trader.OperationLog.Add(record);
            trader.UndoableOperationStack.Add(seq);
            return true;
        }

        // ---- Undo ----

        /// <summary>Checks if undo is possible (local RequestCheck).</summary>
        public bool CanUndo(int playerSlot, out EquipmentShopFailureReason failure)
        {
            failure = EquipmentShopFailureReason.None;
            var trader = GetTrader(playerSlot);
            if (trader == null) { failure = EquipmentShopFailureReason.ControlledUnitNotFound; return false; }
            if (trader.UndoableOperationStack.Count == 0) { failure = EquipmentShopFailureReason.NoUndoableTransaction; return false; }
            return true;
        }

        /// <summary>Executes undo (ProcessCommand).</summary>
        public bool ProcessUndo(int playerSlot, EquipmentHandler handler, out ShopOperationRecord record)
        {
            record = default;
            var trader = GetTrader(playerSlot);
            if (trader == null) return false;
            if (trader.UndoableOperationStack.Count == 0) return false;

            int topSeq = trader.UndoableOperationStack[trader.UndoableOperationStack.Count - 1];

            // Find the original record
            int recordIdx = -1;
            for (int i = trader.OperationLog.Count - 1; i >= 0; i--)
            {
                if (trader.OperationLog[i].OperationSequence == topSeq && !trader.OperationLog[i].Reverted)
                { recordIdx = i; break; }
            }
            if (recordIdx < 0) return false;

            var original = trader.OperationLog[recordIdx];
            int newSeq = trader.NextOperationSequence++;

            // Mark original reverted
            original.Reverted = true;
            original.RevertedLogicTick = SimulationTickContext.Current.Tick;
            trader.OperationLog[recordIdx] = original;

            // Create the undo record
            record = new ShopOperationRecord
            {
                OperationSequence = newSeq,
                OperationType = EquipmentShopOperationType.Undo,
                Player = playerSlot,
                ControlledUnitUid = trader.ControlledUnitUid,
                LogicTick = SimulationTickContext.Current.Tick,
                GoldDelta = -original.GoldDelta,
                Reverted = false,
                EquipmentRevisionBefore = 0,
                EquipmentRevisionAfter = 0,
            };

            trader.OperationLog.Add(record);
            trader.UndoableOperationStack.RemoveAt(trader.UndoableOperationStack.Count - 1);
            return true;
        }

        // ---- Undo invalidation ----

        /// <summary>Invalidates undo when leaving shop range.</summary>
        public void InvalidateUndoByLeavingShop(int playerSlot)
        {
            var trader = GetTrader(playerSlot);
            trader?.UndoableOperationStack.Clear();
        }

        /// <summary>Invalidates undo by combat participation (Equipment/Gold v12 §5.22).</summary>
        public void InvalidateUndoByCombat(int playerSlot, CombatParticipationFlags flags)
        {
            if (flags == CombatParticipationFlags.None) return;
            var trader = GetTrader(playerSlot);
            trader?.UndoableOperationStack.Clear();
        }

        /// <summary>Invalidates undo by equipment use.</summary>
        public void InvalidateUndoByEquipmentUse(int playerSlot, int slot)
        {
            var trader = GetTrader(playerSlot);
            trader?.UndoableOperationStack.Clear();
        }

        // ---- IRollback ----

        public void Capture(ref EquipmentShopRuntimeSnapshot state)
        {
            if (state.CreatedTraders == null)
                state.CreatedTraders = new List<ShopTraderRuntimeSnapshot>();
            else
                state.CreatedTraders.Clear();

            for (int i = 0; i < _tradersByPlayerSlot.Length; i++)
            {
                var trader = _tradersByPlayerSlot[i];
                if (trader == null) continue;

                var ts = new ShopTraderRuntimeSnapshot
                {
                    Player = trader.Player,
                    ControlledUnitUid = trader.ControlledUnitUid,
                    NextOperationSequence = trader.NextOperationSequence,
                };

                if (trader.OperationLog.Count > 0)
                {
                    ts.OperationLog = new ShopOperationRecord[trader.OperationLog.Count];
                    for (int j = 0; j < trader.OperationLog.Count; j++)
                        ts.OperationLog[j] = trader.OperationLog[j];
                }

                if (trader.UndoableOperationStack.Count > 0)
                {
                    ts.UndoableOperationStack = new int[trader.UndoableOperationStack.Count];
                    for (int j = 0; j < trader.UndoableOperationStack.Count; j++)
                        ts.UndoableOperationStack[j] = trader.UndoableOperationStack[j];
                }

                state.CreatedTraders.Add(ts);
            }
        }

        public void Restore(in EquipmentShopRuntimeSnapshot state)
        {
            _tradersByPlayerSlot = new ShopTraderRuntime[_maxPlayers];
            if (state.CreatedTraders == null) return;

            int previousPlayer = -1;
            for (int i = 0; i < state.CreatedTraders.Count; i++)
            {
                var ts = state.CreatedTraders[i];
                if ((uint)ts.Player >= (uint)_tradersByPlayerSlot.Length ||
                    ts.Player <= previousPlayer ||
                    !ts.ControlledUnitUid.IsValid() ||
                    ts.NextOperationSequence < 0)
                    throw new DeterministicSimulationException(
                        "Equipment shop traders are invalid or not in canonical Player order.");
                previousPlayer = ts.Player;
                var trader = new ShopTraderRuntime
                {
                    Player = ts.Player,
                    ControlledUnitUid = ts.ControlledUnitUid,
                    NextOperationSequence = ts.NextOperationSequence,
                };

                int previousOperationSequence = -1;
                if (ts.OperationLog != null)
                    for (int j = 0; j < ts.OperationLog.Length; j++)
                    {
                        ShopOperationRecord record = ts.OperationLog[j];
                        if (record.OperationSequence <= previousOperationSequence ||
                            record.OperationSequence >= ts.NextOperationSequence ||
                            record.Player != ts.Player ||
                            record.ControlledUnitUid != ts.ControlledUnitUid ||
                            record.LogicTick < 0 ||
                            !Enum.IsDefined(
                                typeof(EquipmentShopOperationType),
                                record.OperationType))
                            throw new DeterministicSimulationException(
                                $"Equipment shop operation log for Player {ts.Player} is invalid or non-canonical.");
                        previousOperationSequence = record.OperationSequence;
                        trader.OperationLog.Add(record);
                    }

                if (ts.UndoableOperationStack != null)
                {
                    int previousUndoSequence = -1;
                    for (int j = 0; j < ts.UndoableOperationStack.Length; j++)
                    {
                        int sequence = ts.UndoableOperationStack[j];
                        if (sequence <= previousUndoSequence ||
                            !HasActiveOperation(trader.OperationLog, sequence))
                            throw new DeterministicSimulationException(
                                $"Equipment shop undo stack for Player {ts.Player} is invalid or non-canonical.");
                        previousUndoSequence = sequence;
                        trader.UndoableOperationStack.Add(sequence);
                    }
                }

                _tradersByPlayerSlot[trader.Player] = trader;
            }
        }

        public void Resolve(in RollbackContext context)
        {
            for (int player = 0; player < _tradersByPlayerSlot.Length; player++)
            {
                ShopTraderRuntime trader = _tradersByPlayerSlot[player];
                if (trader != null &&
                    !_unitWorld.TryGetUnit(trader.ControlledUnitUid, out _))
                    throw new DeterministicSimulationException(
                        $"Equipment shop Player {player} references missing Unit {trader.ControlledUnitUid}.");
            }
        }

        public void Rebuild(in RollbackContext context)
        {
            // Player-slot indexing is rebuilt directly by Restore.
        }

        private static bool HasActiveOperation(
            List<ShopOperationRecord> operationLog,
            int sequence)
        {
            for (int i = 0; i < operationLog.Count; i++)
            {
                ShopOperationRecord record = operationLog[i];
                if (record.OperationSequence == sequence)
                    return !record.Reverted &&
                           record.OperationType != EquipmentShopOperationType.Undo;
            }
            return false;
        }
    }

    public sealed class ShopTraderRuntime
    {
        public int Player;
        public UnitUid ControlledUnitUid;
        public int NextOperationSequence;
        public readonly List<ShopOperationRecord> OperationLog = new List<ShopOperationRecord>();
        public readonly List<int> UndoableOperationStack = new List<int>();
    }

    public struct ShopOperationRecord
    {
        public int OperationSequence;
        public EquipmentShopOperationType OperationType;
        public int Player;
        public UnitUid ControlledUnitUid;
        public int LogicTick;
        public int GoldDelta;
        public bool Reverted;
        public int RevertedLogicTick;
        public int EquipmentRevisionBefore;
        public int EquipmentRevisionAfter;
    }

    public enum EquipmentShopOperationType : byte { Purchase = 0, Sell = 1, Undo = 2 }

    public enum EquipmentShopFailureReason : byte
    {
        None = 0, InvalidLocalPlayer, ControlledUnitNotFound, NotInShopRange,
        ItemNotFound, InsufficientGold, InventoryFull, InvalidRecipe,
        DuplicateFinishedItem, UniqueTagConflict, InvalidSlot, EmptySlot,
        NoUndoableTransaction, UndoInvalidatedByLeavingShop, UndoInvalidatedByCombat,
        UndoInvalidatedByEquipmentUse,
    }

    [Flags]
    public enum CombatParticipationFlags : byte
    {
        None = 0, DamageDealt = 1 << 0, DamageTaken = 1 << 1,
        HealDealt = 1 << 2, HealTaken = 1 << 3,
        ShieldGranted = 1 << 4, ShieldReceived = 1 << 5,
    }

    public struct EquipmentPurchasePlan
    {
        public int TargetEquipmentId;
        public int PurchaseCost;
        public int[] ConsumedComponentSlots;
        public bool MergeIntoExistingStack;
        public int DestinationSlot;
    }

    public struct ShopTraderRuntimeSnapshot
    {
        public int Player;
        public UnitUid ControlledUnitUid;
        public int NextOperationSequence;
        public ShopOperationRecord[] OperationLog;
        public int[] UndoableOperationStack;
    }

    public struct EquipmentShopRuntimeSnapshot
    {
        public List<ShopTraderRuntimeSnapshot> CreatedTraders;
        public static readonly EquipmentShopRuntimeSnapshot Empty = new EquipmentShopRuntimeSnapshot
        { CreatedTraders = new List<ShopTraderRuntimeSnapshot>() };
    }
}
