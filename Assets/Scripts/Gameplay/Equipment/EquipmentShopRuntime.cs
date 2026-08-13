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
        private IConfirmedGoldIncomeView _confirmedGoldView;
        private IEquipmentShopCommandSubmitter _submitter;

        public fp SellRate { get; private set; }

        public void ConfigureIncomeView(
            IConfirmedGoldIncomeView confirmedGoldView)
        {
            _confirmedGoldView =
                confirmedGoldView ??
                throw new ArgumentNullException(
                    nameof(confirmedGoldView));
        }

        public void SetCommandSubmitter(
            IEquipmentShopCommandSubmitter submitter)
        {
            _submitter =
                submitter ??
                throw new ArgumentNullException(
                    nameof(submitter));
        }

        /// <summary>
        /// CurrentAvailableGold = ConfirmedEarnedGoldTotal +
        /// EffectiveShopGoldDelta (UI design v9.1 13.2).
        /// </summary>
        public int GetCurrentAvailableGold(
            int playerSlot)
        {
            int confirmed =
                _confirmedGoldView != null
                    ? _confirmedGoldView
                        .GetConfirmedEarnedGoldTotal(
                            playerSlot)
                    : 0;
            return confirmed +
                ComputeEffectiveShopGoldDelta(
                    playerSlot);
        }

        // ---- UI Request entry points (design v9.1 12.1) ----

        public EquipmentShopRequestCheck RequestPurchase(
            int playerSlot,
            int targetEquipmentId)
        {
            if (!TryResolveHandler(
                    playerSlot,
                    out EquipmentHandler handler,
                    out EquipmentShopFailureReason failure))
                return EquipmentShopRequestCheck.Reject(
                    failure);
            if (_confirmedGoldView == null)
                throw new InvalidOperationException(
                    "EquipmentShopRuntime requires an income view before RequestPurchase.");
            if (_submitter == null)
                throw new InvalidOperationException(
                    "EquipmentShopRuntime requires a command submitter before RequestPurchase.");

            int gold =
                GetCurrentAvailableGold(playerSlot);
            if (!TryBuildPurchasePlan(
                    playerSlot,
                    targetEquipmentId,
                    gold,
                    handler,
                    out _,
                    out failure))
                return EquipmentShopRequestCheck.Reject(
                    failure);

            _submitter.SubmitPurchase(
                playerSlot,
                targetEquipmentId);
            return EquipmentShopRequestCheck.Allow();
        }

        public EquipmentShopRequestCheck RequestSell(
            int playerSlot,
            int sourceSlot)
        {
            if (!TryResolveHandler(
                    playerSlot,
                    out EquipmentHandler handler,
                    out EquipmentShopFailureReason failure))
                return EquipmentShopRequestCheck.Reject(
                    failure);
            if (_submitter == null)
                throw new InvalidOperationException(
                    "EquipmentShopRuntime requires a command submitter before RequestSell.");

            if (!TrySell(
                    playerSlot,
                    sourceSlot,
                    handler,
                    out _,
                    out failure))
                return EquipmentShopRequestCheck.Reject(
                    failure);

            _submitter.SubmitSell(
                playerSlot,
                sourceSlot);
            return EquipmentShopRequestCheck.Allow();
        }

        public EquipmentShopRequestCheck RequestUndo(
            int playerSlot)
        {
            if (_submitter == null)
                throw new InvalidOperationException(
                    "EquipmentShopRuntime requires a command submitter before RequestUndo.");

            int gold =
                GetCurrentAvailableGold(playerSlot);
            if (!CanUndo(
                    playerSlot,
                    gold,
                    out EquipmentShopFailureReason failure))
                return EquipmentShopRequestCheck.Reject(
                    failure);

            _submitter.SubmitUndo(playerSlot);
            return EquipmentShopRequestCheck.Allow();
        }

        private bool TryResolveHandler(
            int playerSlot,
            out EquipmentHandler handler,
            out EquipmentShopFailureReason failure)
        {
            handler = null;
            failure =
                EquipmentShopFailureReason.None;
            if (!TryResolveControlledUnit(
                    playerSlot,
                    out Unit unit) ||
                unit.EquipmentHandler == null)
            {
                failure =
                    EquipmentShopFailureReason
                        .ControlledUnitNotFound;
                return false;
            }
            handler = unit.EquipmentHandler;
            return true;
        }

        private bool TryResolveControlledUnit(
            int playerSlot,
            out Unit controlledUnit)
        {
            controlledUnit = null;
            ShopTraderRuntime trader =
                GetTrader(playerSlot);
            if (trader != null)
            {
                return _unitWorld.TryGetUnit(
                    trader.ControlledUnitUid,
                    out controlledUnit);
            }

            IReadOnlyList<Unit> units =
                _unitWorld.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                Unit candidate = units[i];
                if (candidate == null ||
                    candidate.ControlledByPlayerSlot !=
                        playerSlot)
                {
                    continue;
                }
                if (controlledUnit != null)
                {
                    throw new DeterministicSimulationException(
                        $"PlayerSlot {playerSlot} controls multiple Units.");
                }
                controlledUnit = candidate;
            }
            return controlledUnit != null;
        }

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
            // Equipment/Gold v12 5.22: any combat participation invalidates
            // the trader's undoable-operation stack. CombatSystem raises this
            // deterministically on every endpoint so the invalidation stays
            // in sync across server and clients.
            CombatEvents.OnCombatParticipationUnit +=
                OnCombatParticipation;
        }

        private void OnCombatParticipation(
            UnitUid sourceUnitUid,
            UnitUid targetUnitUid,
            CombatParticipationFlags flags)
        {
            if (flags == CombatParticipationFlags.None)
                return;
            InvalidateParticipantUndo(sourceUnitUid, flags);
            InvalidateParticipantUndo(targetUnitUid, flags);
        }

        private void InvalidateParticipantUndo(
            UnitUid unitUid,
            CombatParticipationFlags flags)
        {
            if (!unitUid.IsValid() ||
                _unitWorld == null ||
                !_unitWorld.TryGetUnit(unitUid, out Unit unit))
                return;
            int playerSlot = unit.ControlledByPlayerSlot;
            if (playerSlot < 0)
                return;
            InvalidateUndoByCombat(playerSlot, flags);
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
            else if (trader.ControlledUnitUid != controlledUnitUid)
            {
                throw new DeterministicSimulationException(
                    $"PlayerSlot {playerSlot} is already bound to {trader.ControlledUnitUid}, not {controlledUnitUid}.");
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

        public int CalculatePurchasePrice(
            int playerSlot,
            int targetEquipmentId)
        {
            if (!TryResolveControlledUnit(
                    playerSlot,
                    out Unit unit) ||
                unit.EquipmentHandler == null)
                return 0;
            EquipmentDefinition targetDef =
                _database?.GetDefinition(targetEquipmentId);
            if (targetDef == null)
                return 0;
            return SelectRecipeComponents(
                targetDef,
                unit.EquipmentHandler,
                out _);
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

            if (handler == null)
            {
                failure = EquipmentShopFailureReason.ControlledUnitNotFound;
                return false;
            }
            int purchaseCost;
            int[] consumedSlots;
            try
            {
                purchaseCost = SelectRecipeComponents(
                    targetDef,
                    handler,
                    out consumedSlots);
            }
            catch (DeterministicSimulationException)
            {
                failure = EquipmentShopFailureReason.InvalidRecipe;
                return false;
            }

            var before =
                new EquipmentTransactionSlotState[EquipmentHandler.SlotCount];
            var after =
                new EquipmentTransactionSlotState[EquipmentHandler.SlotCount];
            for (int slot = 0; slot < EquipmentHandler.SlotCount; slot++)
            {
                before[slot] = handler.CaptureTransactionSlot(slot);
                after[slot] = CloneSlotState(before[slot]);
            }
            for (int i = 0; i < consumedSlots.Length; i++)
                after[consumedSlots[i]] =
                    EquipmentTransactionSlotState.Empty;

            int destSlot;
            bool mergeIntoExisting = false;
            destSlot = FindStackableSlot(after, targetDef);
            if (destSlot >= 0)
            {
                mergeIntoExisting = true;
                after[destSlot].StackCount++;
            }
            else
            {
                destSlot = -1;
                for (int s = 0; s < EquipmentHandler.SlotCount; s++)
                {
                    if (!after[s].Occupied)
                    { destSlot = s; break; }
                }
                if (destSlot < 0) { failure = EquipmentShopFailureReason.InventoryFull; return false; }
                after[destSlot] =
                    EquipmentHandler.CreateInitialTransactionSlot(
                        targetDef);
            }

            if (!ValidatePostPurchaseState(
                    after,
                    targetDef,
                    out failure))
                return false;
            if (currentAvailableGold < purchaseCost)
            { failure = EquipmentShopFailureReason.InsufficientGold; return false; }

            var slotChanges = new List<EquipmentSlotChange>();
            for (int slot = 0; slot < EquipmentHandler.SlotCount; slot++)
            {
                if (before[slot].Equals(after[slot]))
                    continue;
                slotChanges.Add(new EquipmentSlotChange
                {
                    Slot = slot,
                    Before = CloneSlotState(before[slot]),
                    After = CloneSlotState(after[slot]),
                });
            }
            plan = new EquipmentPurchasePlan
            {
                TargetEquipmentId = targetEquipmentId,
                PurchaseCost = purchaseCost,
                ConsumedComponentSlots = consumedSlots,
                MergeIntoExistingStack = mergeIntoExisting,
                DestinationSlot = destSlot,
                SlotChanges = slotChanges.ToArray(),
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

            if (!ValidatePlanBeforeState(handler, plan.SlotChanges))
                return false;
            int revisionBefore = handler.RuntimeRevision;

            // Remove consumed components
            if (plan.ConsumedComponentSlots != null)
            {
                for (int i = 0; i < plan.ConsumedComponentSlots.Length; i++)
                    if (!handler.Remove(plan.ConsumedComponentSlots[i]))
                        throw new DeterministicSimulationException(
                            $"Purchase failed to consume Equipment slot {plan.ConsumedComponentSlots[i]}.");
            }

            // Add or merge target
            if (plan.MergeIntoExistingStack)
                handler.MergeIntoStack(plan.DestinationSlot, 1);
            else if (!handler.Add(targetDef, plan.DestinationSlot))
                throw new DeterministicSimulationException(
                    $"Purchase failed to add Equipment {targetDef.Id} to slot {plan.DestinationSlot}.");
            if (!ValidatePlanAfterState(handler, plan.SlotChanges))
                throw new DeterministicSimulationException(
                    "Purchase result does not match the deterministic purchase plan.");

            int seq = trader.NextOperationSequence++;
            record = new ShopOperationRecord
            {
                OperationSequence = seq,
                OperationType = EquipmentShopOperationType.Purchase,
                Player = playerSlot,
                ControlledUnitUid = trader.ControlledUnitUid,
                LogicTick = SimulationTickContext.Current.Tick,
                GoldDelta = -plan.PurchaseCost,
                SlotChanges = CloneSlotChanges(plan.SlotChanges),
                Reverted = false,
                EquipmentRevisionBefore = revisionBefore,
                EquipmentRevisionAfter = handler.RuntimeRevision,
            };

            trader.OperationLog.Add(record);
            trader.UndoableOperationStack.Add(seq);
            FrameSyncDiagnostics.Log(
                $"[Shop] Purchase p={playerSlot} " +
                $"item={plan.TargetEquipmentId} " +
                $"slot={plan.DestinationSlot} " +
                $"tick={SimulationTickContext.Current.Tick} " +
                $"rev={handler.RuntimeRevision} " +
                $"slotDef={handler.GetSlotDef(plan.DestinationSlot)?.Id ?? 0} " +
                $"merge={plan.MergeIntoExistingStack} " +
                $"mode={SimulationTickContext.Current.ExecutionMode}");
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

            sellValue =
                (int)fpmath.round((fp)def.Value * SellRate);
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

            EquipmentTransactionSlotState before =
                handler.CaptureTransactionSlot(slot);
            int revisionBefore = handler.RuntimeRevision;
            if (!handler.Remove(slot))
                return false;
            EquipmentTransactionSlotState after =
                handler.CaptureTransactionSlot(slot);

            int seq = trader.NextOperationSequence++;
            record = new ShopOperationRecord
            {
                OperationSequence = seq,
                OperationType = EquipmentShopOperationType.Sell,
                Player = playerSlot,
                ControlledUnitUid = trader.ControlledUnitUid,
                LogicTick = SimulationTickContext.Current.Tick,
                GoldDelta = sellValue,
                SlotChanges = new[]
                {
                    new EquipmentSlotChange
                    {
                        Slot = slot,
                        Before = before,
                        After = after,
                    },
                },
                Reverted = false,
                EquipmentRevisionBefore = revisionBefore,
                EquipmentRevisionAfter = handler.RuntimeRevision,
            };

            trader.OperationLog.Add(record);
            trader.UndoableOperationStack.Add(seq);
            return true;
        }

        // ---- Undo ----

        /// <summary>Checks if undo is possible (local RequestCheck).</summary>
        public bool CanUndo(
            int playerSlot,
            int currentAvailableGold,
            out EquipmentShopFailureReason failure)
        {
            failure = EquipmentShopFailureReason.None;
            var trader = GetTrader(playerSlot);
            if (trader == null) { failure = EquipmentShopFailureReason.ControlledUnitNotFound; return false; }
            if (trader.UndoableOperationStack.Count == 0) { failure = EquipmentShopFailureReason.NoUndoableTransaction; return false; }
            if (!_unitWorld.TryGetUnit(
                    trader.ControlledUnitUid,
                    out Unit unit) ||
                unit.EquipmentHandler == null)
            {
                failure = EquipmentShopFailureReason.ControlledUnitNotFound;
                return false;
            }
            if (!TryGetUndoRecord(
                    trader,
                    out _,
                    out ShopOperationRecord original))
            {
                failure = EquipmentShopFailureReason.NoUndoableTransaction;
                return false;
            }
            if (!ValidatePlanAfterState(
                    unit.EquipmentHandler,
                    original.SlotChanges))
            {
                failure = EquipmentShopFailureReason.TransactionStateChanged;
                return false;
            }
            if (original.OperationType ==
                    EquipmentShopOperationType.Sell &&
                currentAvailableGold < original.GoldDelta)
            {
                failure = EquipmentShopFailureReason.InsufficientGold;
                return false;
            }
            return true;
        }

        /// <summary>Executes undo (ProcessCommand).</summary>
        public bool ProcessUndo(
            int playerSlot,
            int currentAvailableGold,
            EquipmentHandler handler,
            out ShopOperationRecord record)
        {
            record = default;
            var trader = GetTrader(playerSlot);
            if (trader == null) return false;
            if (trader.UndoableOperationStack.Count == 0) return false;
            if (!TryGetUndoRecord(
                    trader,
                    out int recordIdx,
                    out ShopOperationRecord original))
                return false;
            if (!ValidatePlanAfterState(handler, original.SlotChanges))
                return false;
            if (original.OperationType ==
                    EquipmentShopOperationType.Sell &&
                currentAvailableGold < original.GoldDelta)
                return false;

            EquipmentSlotChange[] changes =
                original.SlotChanges ?? Array.Empty<EquipmentSlotChange>();
            for (int i = 0; i < changes.Length; i++)
                handler.RestoreTransactionSlot(
                    changes[i].Slot,
                    changes[i].Before);
            original.Reverted = true;
            original.RevertedLogicTick = SimulationTickContext.Current.Tick;
            trader.OperationLog[recordIdx] = original;
            trader.UndoableOperationStack.RemoveAt(trader.UndoableOperationStack.Count - 1);
            record = CloneRecord(original);
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

        private int SelectRecipeComponents(
            EquipmentDefinition targetDef,
            EquipmentHandler handler,
            out int[] consumedSlots)
        {
            int purchaseCost = targetDef.Value;
            var selected =
                new bool[EquipmentHandler.SlotCount];
            var slots = new List<int>();
            EquipmentRecipePart[] parts =
                targetDef.Recipe?.Components ??
                Array.Empty<EquipmentRecipePart>();
            for (int partIndex = 0;
                 partIndex < parts.Length;
                 partIndex++)
            {
                EquipmentRecipePart part = parts[partIndex];
                if (part.Item == null ||
                    part.Count <= 0 ||
                    part.Item.Value < 0)
                    throw new DeterministicSimulationException(
                        $"Equipment {targetDef.Id} has an invalid recipe part at index {partIndex}.");
                int needed = part.Count;
                for (int slot = 0;
                     slot < EquipmentHandler.SlotCount &&
                     needed > 0;
                     slot++)
                {
                    if (selected[slot] ||
                        handler.GetSlotDef(slot)?.Id !=
                        part.Item.Id)
                        continue;
                    selected[slot] = true;
                    slots.Add(slot);
                    purchaseCost = checked(
                        purchaseCost - part.Item.Value);
                    needed--;
                }
            }
            if (purchaseCost < 0)
                throw new DeterministicSimulationException(
                    $"Equipment {targetDef.Id} recipe discount exceeds its value.");
            slots.Sort();
            consumedSlots = slots.ToArray();
            return purchaseCost;
        }

        private static int FindStackableSlot(
            EquipmentTransactionSlotState[] slots,
            EquipmentDefinition targetDef)
        {
            if (targetDef.Tier != EquipmentTier.Consumable ||
                targetDef.MaxStack <= 1)
                return -1;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                EquipmentTransactionSlotState state = slots[slot];
                if (state.Occupied &&
                    state.EquipmentId == targetDef.Id &&
                    state.StackCount < targetDef.MaxStack)
                    return slot;
            }
            return -1;
        }

        private bool ValidatePostPurchaseState(
            EquipmentTransactionSlotState[] slots,
            EquipmentDefinition targetDef,
            out EquipmentShopFailureReason failure)
        {
            failure = EquipmentShopFailureReason.None;
            int targetCount = 0;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                EquipmentTransactionSlotState state = slots[slot];
                if (!state.Occupied)
                    continue;
                EquipmentDefinition definition =
                    _database.GetDefinition(state.EquipmentId);
                if (definition == null ||
                    state.StackCount < 1 ||
                    state.StackCount > definition.MaxStack)
                {
                    failure = EquipmentShopFailureReason.InvalidRecipe;
                    return false;
                }
                if (state.EquipmentId == targetDef.Id)
                    targetCount++;
            }
            if (targetDef.Tier == EquipmentTier.Finished &&
                targetCount > 1)
            {
                failure =
                    EquipmentShopFailureReason.DuplicateFinishedItem;
                return false;
            }
            EquipmentTagDefinition[] targetTags =
                targetDef.Tags ??
                Array.Empty<EquipmentTagDefinition>();
            UniqueEquipmentTagTable uniqueTable =
                _database.UniqueTagTable;
            for (int tagIndex = 0;
                 tagIndex < targetTags.Length;
                 tagIndex++)
            {
                EquipmentTagDefinition tag =
                    targetTags[tagIndex];
                if (tag == null ||
                    !tag.Uid.IsValid ||
                    uniqueTable == null ||
                    !uniqueTable.IsUnique(tag))
                    continue;
                int matches = 0;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    if (!slots[slot].Occupied)
                        continue;
                    EquipmentDefinition definition =
                        _database.GetDefinition(
                            slots[slot].EquipmentId);
                    EquipmentTagDefinition[] tags =
                        definition?.Tags ??
                        Array.Empty<EquipmentTagDefinition>();
                    for (int i = 0; i < tags.Length; i++)
                    {
                        EquipmentTagDefinition candidate =
                            tags[i];
                        if (candidate == null ||
                            candidate.Uid != tag.Uid)
                            continue;
                        matches++;
                        break;
                    }
                }
                if (matches > 1)
                {
                    failure =
                        EquipmentShopFailureReason.UniqueTagConflict;
                    return false;
                }
            }
            return true;
        }

        private static bool ValidatePlanBeforeState(
            EquipmentHandler handler,
            EquipmentSlotChange[] changes)
        {
            return ValidatePlanState(
                handler,
                changes,
                useAfter: false);
        }

        private static bool ValidatePlanAfterState(
            EquipmentHandler handler,
            EquipmentSlotChange[] changes)
        {
            return ValidatePlanState(
                handler,
                changes,
                useAfter: true);
        }

        private static bool ValidatePlanState(
            EquipmentHandler handler,
            EquipmentSlotChange[] changes,
            bool useAfter)
        {
            if (handler == null ||
                changes == null ||
                changes.Length == 0)
                return false;
            int previousSlot = -1;
            for (int i = 0; i < changes.Length; i++)
            {
                EquipmentSlotChange change = changes[i];
                if ((uint)change.Slot >= EquipmentHandler.SlotCount ||
                    change.Slot <= previousSlot ||
                    !handler.MatchesTransactionSlot(
                        change.Slot,
                        useAfter ? change.After : change.Before))
                    return false;
                previousSlot = change.Slot;
            }
            return true;
        }

        private static bool TryGetUndoRecord(
            ShopTraderRuntime trader,
            out int recordIndex,
            out ShopOperationRecord record)
        {
            recordIndex = -1;
            record = default;
            if (trader == null ||
                trader.UndoableOperationStack.Count == 0)
                return false;
            int sequence =
                trader.UndoableOperationStack[
                    trader.UndoableOperationStack.Count - 1];
            for (int i = trader.OperationLog.Count - 1; i >= 0; i--)
            {
                ShopOperationRecord candidate =
                    trader.OperationLog[i];
                if (candidate.OperationSequence != sequence ||
                    candidate.Reverted)
                    continue;
                recordIndex = i;
                record = candidate;
                return true;
            }
            return false;
        }

        private static EquipmentTransactionSlotState CloneSlotState(
            in EquipmentTransactionSlotState source)
        {
            if (!source.Occupied)
                return EquipmentTransactionSlotState.Empty;
            var effectStates =
                new List<EquipmentEffectRuntimeSnapshot>();
            List<EquipmentEffectRuntimeSnapshot> sourceEffects =
                source.EffectStates ??
                new List<EquipmentEffectRuntimeSnapshot>();
            for (int i = 0; i < sourceEffects.Count; i++)
            {
                List<EquipmentEffectModuleRuntimeState> modules =
                    sourceEffects[i].ModuleStates ??
                    new List<EquipmentEffectModuleRuntimeState>();
                effectStates.Add(
                    new EquipmentEffectRuntimeSnapshot
                    {
                        ModuleStates =
                            new List<EquipmentEffectModuleRuntimeState>(
                                modules),
                    });
            }
            return new EquipmentTransactionSlotState
            {
                Occupied = true,
                EquipmentId = source.EquipmentId,
                StackCount = source.StackCount,
                ChargeCount = source.ChargeCount,
                ReadyTick = source.ReadyTick,
                EffectStates = effectStates,
            };
        }

        private static EquipmentSlotChange[] CloneSlotChanges(
            EquipmentSlotChange[] changes)
        {
            if (changes == null || changes.Length == 0)
                return Array.Empty<EquipmentSlotChange>();
            var clone = new EquipmentSlotChange[changes.Length];
            for (int i = 0; i < changes.Length; i++)
            {
                clone[i] = new EquipmentSlotChange
                {
                    Slot = changes[i].Slot,
                    Before = CloneSlotState(changes[i].Before),
                    After = CloneSlotState(changes[i].After),
                };
            }
            return clone;
        }

        private static ShopOperationRecord CloneRecord(
            in ShopOperationRecord source)
        {
            ShopOperationRecord clone = source;
            clone.SlotChanges =
                CloneSlotChanges(source.SlotChanges);
            return clone;
        }

        // ---- IRollback ----

        public void Capture(ref EquipmentShopRuntimeSnapshot state)
        {
            // Always allocate a fresh list: the snapshot must own its list so
            // later captures (which share the static Empty instance) cannot
            // mutate the content of previously captured snapshots. Reusing
            // the passed-in list corrupted rollback anchors' shop state.
            state.CreatedTraders =
                new System.Collections.Generic.List<ShopTraderRuntimeSnapshot>();
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
                    ts.OperationLog = new System.Collections.Generic.List<ShopOperationRecord>(trader.OperationLog.Count);
                    for (int j = 0; j < trader.OperationLog.Count; j++)
                        ts.OperationLog.Add(
                            CloneRecord(trader.OperationLog[j]));
                }

                if (trader.UndoableOperationStack.Count > 0)
                {
                    ts.UndoableOperationStack = new System.Collections.Generic.List<int>(trader.UndoableOperationStack.Count);
                    for (int j = 0; j < trader.UndoableOperationStack.Count; j++)
                        ts.UndoableOperationStack.Add(trader.UndoableOperationStack[j]);
                }

                state.CreatedTraders.Add(ts);
            }
        }

        public void Restore(in EquipmentShopRuntimeSnapshot state)
        {
            int snapshotOps = 0;
            if (state.CreatedTraders != null)
            {
                for (int s = 0; s < state.CreatedTraders.Count; s++)
                    snapshotOps +=
                        state.CreatedTraders[s].OperationLog?.Count ?? 0;
            }
            FrameSyncDiagnostics.Log(
                $"[Shop] RestoreIn snapshotOps={snapshotOps} " +
                $"tick={SimulationTickContext.Current.Tick} " +
                $"mode={SimulationTickContext.Current.ExecutionMode}");
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
                    for (int j = 0; j < ts.OperationLog.Count; j++)
                    {
                        ShopOperationRecord record = ts.OperationLog[j];
                        if (record.OperationSequence <= previousOperationSequence ||
                            record.OperationSequence >= ts.NextOperationSequence ||
                            record.Player != ts.Player ||
                            record.ControlledUnitUid != ts.ControlledUnitUid ||
                            record.LogicTick < 0 ||
                            !Enum.IsDefined(
                                typeof(EquipmentShopOperationType),
                                record.OperationType) ||
                            !ValidateSlotChangeShape(
                                record.SlotChanges))
                            throw new DeterministicSimulationException(
                                $"Equipment shop operation log for Player {ts.Player} is invalid or non-canonical.");
                        previousOperationSequence = record.OperationSequence;
                        trader.OperationLog.Add(CloneRecord(record));
                    }

                if (ts.UndoableOperationStack != null)
                {
                    int previousUndoSequence = -1;
                    for (int j = 0; j < ts.UndoableOperationStack.Count; j++)
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
            for (int i = 0; i < _tradersByPlayerSlot.Length; i++)
            {
                ShopTraderRuntime trader = _tradersByPlayerSlot[i];
                if (trader == null)
                    continue;
                FrameSyncDiagnostics.Log(
                    $"[Shop] Restore p={trader.Player} " +
                    $"ops={trader.OperationLog.Count} " +
                    $"undo={trader.UndoableOperationStack.Count} " +
                    $"tick={SimulationTickContext.Current.Tick} " +
                    $"mode={SimulationTickContext.Current.ExecutionMode}");
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
                    return !record.Reverted;
            }
            return false;
        }

        private static bool ValidateSlotChangeShape(
            EquipmentSlotChange[] changes)
        {
            if (changes == null || changes.Length == 0)
                return false;
            int previousSlot = -1;
            for (int i = 0; i < changes.Length; i++)
            {
                if ((uint)changes[i].Slot >=
                        EquipmentHandler.SlotCount ||
                    changes[i].Slot <= previousSlot ||
                    changes[i].Before.Equals(
                        changes[i].After))
                    return false;
                previousSlot = changes[i].Slot;
            }
            return true;
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
        public EquipmentSlotChange[] SlotChanges;
        public bool Reverted;
        public int RevertedLogicTick;
        public int EquipmentRevisionBefore;
        public int EquipmentRevisionAfter;
    }

    public enum EquipmentShopOperationType : byte
    {
        Purchase = 0,
        Sell = 1,
    }

    public enum EquipmentShopFailureReason : byte
    {
        None = 0, InvalidLocalPlayer, ControlledUnitNotFound, NotInShopRange,
        ItemNotFound, InsufficientGold, InventoryFull, InvalidRecipe,
        DuplicateFinishedItem, UniqueTagConflict, InvalidSlot, EmptySlot,
        NoUndoableTransaction, UndoInvalidatedByLeavingShop, UndoInvalidatedByCombat,
        UndoInvalidatedByEquipmentUse,
        TransactionStateChanged,
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
        public EquipmentSlotChange[] SlotChanges;
    }

    public struct EquipmentSlotChange
    {
        public int Slot;
        public EquipmentTransactionSlotState Before;
        public EquipmentTransactionSlotState After;
    }

    public struct ShopTraderRuntimeSnapshot
    {
        public int Player;
        public UnitUid ControlledUnitUid;
        public int NextOperationSequence;
        public System.Collections.Generic.List<ShopOperationRecord> OperationLog;
        public System.Collections.Generic.List<int> UndoableOperationStack;
    }

    public struct EquipmentShopRuntimeSnapshot
    {
        public System.Collections.Generic.List<ShopTraderRuntimeSnapshot> CreatedTraders;
        public static readonly EquipmentShopRuntimeSnapshot Empty = new EquipmentShopRuntimeSnapshot
        { CreatedTraders = new System.Collections.Generic.List<ShopTraderRuntimeSnapshot>() };
    }
}
