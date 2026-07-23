using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    public sealed class SimulationTickPipeline
    {
        private readonly UnitWorld _unitWorld;
        private readonly PhysicsWorld _physicsWorld;
        private readonly CommandCollector _collector;
        private readonly CanonicalByteWriter _checksumWriter;
        private readonly List<UnitSnapshot> _unitStateBuffer = new List<UnitSnapshot>();
        private readonly List<UnitAIControllerSnapshot> _aiStateBuffer = new List<UnitAIControllerSnapshot>();
        private readonly List<JungleCampSnapshot> _campStateBuffer = new List<JungleCampSnapshot>();
        private readonly List<LocomotionResult> _locomotionBuffer = new List<LocomotionResult>();

        public CombatSystem CombatSystem { get; set; }
        public GoldIncomeRuntime GoldIncome { get; set; }
        public ProjectileWorld ProjectileWorld { get; set; }
        public EquipmentShopRuntime EquipmentShop { get; set; }
        public MinionSystem MinionSystem { get; set; }
        public JungleCampSystem JungleCampSystem { get; set; }
        public NonHeroRestoreHelper NonHeroHelper { get; set; }
        public ProjectileHitResolver ProjectileHitResolver { get; set; }
        public PresentationSyncManager PresentationSync { get; set; }
        public DeterministicRandomService RandomService { get; set; }
        public MatchRuleRuntime MatchRule { get; set; }
        public CommandCollector CommandCollector => _collector;
        internal int AuthorityReplayTick { get; set; } = -1;

        public int LocalSimulationTick { get; private set; }
        public uint LastChecksum { get; private set; }
        public event Action<int, IReadOnlyList<GameplayCommand>, uint> TickCompleted;

        public SimulationTickPipeline(UnitWorld unitWorld, PhysicsWorld physicsWorld = null)
        {
            _unitWorld = unitWorld;
            _physicsWorld = physicsWorld;
            _collector = new CommandCollector();
            _checksumWriter = new CanonicalByteWriter(new byte[262144]);
            LocalSimulationTick = 0;
        }

        public void SubmitCommand(GameplayCommand command)
        {
            if (command.TargetTick != LocalSimulationTick)
                throw new DeterministicSimulationException(
                    $"Command Tick {command.TargetTick} does not match next simulation Tick {LocalSimulationTick}.");
            _collector.Collect(command);
        }

        public void ReplaceCommandsForNextTick(IReadOnlyList<GameplayCommand> commands)
        {
            _collector.BeginTick(LocalSimulationTick);
            if (commands == null) return;
            for (int i = 0; i < commands.Count; i++) SubmitCommand(commands[i]);
        }

        public void ExecuteTick(
            SimulationTickContextController controller,
            ExecutionMode executionMode = ExecutionMode.ServerAuthority)
        {
            int tick = LocalSimulationTick;
                controller.BeginTick(tick, executionMode);
                GoldIncome?.BeginTick(tick);
            try
            {
                VisualEventOutput.Clear();
                CombatSystem?.BeginTick();
                var commands = _collector.GetCanonicalCommands();
                foreach (var cmd in commands)
                    DispatchCommand(cmd);

                var units = _unitWorld.GetAllUnits();

                // Phase 1: Locomotion evaluation
                _locomotionBuffer.Clear();
                foreach (var unit in units)
                {
                    if (unit == null) continue;
                    var locomotion = unit.Locomotion?.Evaluate()
                        ?? LocomotionResult.Idle(unit.UnitUid);
                    _locomotionBuffer.Add(locomotion);
                }

                // Phase 2: Apply route movement
                for (int i = 0; i < units.Count; i++)
                {
                    var unit = units[i];
                    if (unit == null) continue;
                    var loco = (i < _locomotionBuffer.Count)
                        ? _locomotionBuffer[i]
                        : LocomotionResult.Idle(unit.UnitUid);
                    unit.MovementHandler?.ApplyRouteMovement(loco);
                }

                // Phase 3: Handler Tick
                foreach (var unit in units)
                {
                    if (unit == null) continue;
                    unit.BuffHandler?.Advance();
                    unit.EquipmentHandler?.AdvanceEffects();
                    unit.CrowdControl?.TickUpdate();
                    unit.HitReaction.TickUpdate();
                    unit.AbilityHandler?.TickUpdate();
                    unit.MovementHandler?.TickUpdate(fp.one);
                    var damageRequest = unit.AttackHandler?.TickUpdate();
                    if (damageRequest.HasValue && CombatSystem != null)
                        CombatSystem.SubmitDamage(damageRequest.Value);
                }

                ProjectileWorld?.CommitSpawns();
                ProjectileWorld?.TickAll();
                ProjectileHitResolver?.ProcessAllHits(ProjectileWorld);
                CombatSystem?.SettleActiveRequests();
                SyncMovementToPhysics();
                _physicsWorld?.BuildUnitFinalGrid();
                _physicsWorld?.DetectUnitCollisionEvents();
                CombatSystem?.EndTick();

                if (MatchRule != null)
                {
                    MatchRule.Statistics.Consume(CombatSystem?.DeathResults, _unitWorld);
                    MatchRule.AdvanceTick(tick);
                    if (executionMode == ExecutionMode.ServerAuthority ||
                        (executionMode == ExecutionMode.ClientReplay && AuthorityReplayTick == tick))
                        MatchRule.EvaluateAuthorityConfirmedTick(tick, _unitWorld);
                }

                // Wire gold allocations to GoldIncomeRuntime
                // Wire gold allocations from CombatSystem to GoldIncomeRuntime
                var combatGoldAllocs = MatchRule?.Statistics.GoldAllocations;
                if (combatGoldAllocs != null && GoldIncome != null)
                {
                    foreach (var alloc in combatGoldAllocs)
                    {
                        if (!alloc.ReceiverHeroUid.IsValid()) continue;
                        if (!_unitWorld.TryGetUnit(alloc.ReceiverHeroUid, out var receiver)) continue;
                        int playerSlot = receiver.ControlledByPlayerSlot;
                        if (playerSlot >= 0)
                        {
                            int goldAmount = (int)alloc.GoldAmount;
                            if (goldAmount > 0)
                                GoldIncome.RequestGoldIncome(
                                    playerSlot, goldAmount, GoldIncomeReason.UnitKill);
                        }
                    }
                }
                GoldIncome?.SealTick(tick);
                TickNonHeroSystems(tick);
                GameplaySnapshot checksumState = CaptureAggregateSnapshot();
                GoldIncomeBatchDigest goldDigest = GoldIncome?.GetBatchDigest(tick)
                    ?? new GoldIncomeBatchDigest(0);
                LastChecksum = SharedGameplayChecksum.Compute(
                    checksumState, goldDigest, _checksumWriter);
                TickCompleted?.Invoke(tick, commands, LastChecksum);
                PresentationSync?.ConsumeAllEvents();
            }
            finally
            {
                controller.EndTick();
                _collector.BeginTick(tick + 1);
                LocalSimulationTick = tick + 1;
            }
        }

        public GameplaySnapshot CaptureAggregateSnapshot()
        {
            var snapshot = GameplaySnapshot.CreateEmpty();
            var units = _unitWorld.GetAllUnits();
            _unitStateBuffer.Clear();
            foreach (var unit in units)
            {
                var us = new UnitSnapshot
                {
                    UnitUid = unit.UnitUid,
                    OwnerUid = unit.OwnerUid,
                    UnitKind = unit.UnitKind,
                    UnitSubKindId = unit.UnitSubKindId,
                    TeamId = unit.TeamId,
                    UnitPrototypeId = unit.UnitPrototypeId,
                    LifeState = unit.LifeState,
                    CapabilityState = unit.CapabilityState,
                    HitReactionState = unit.HitReaction,
                    PhysicsTransform = unit.PhysicsEntity.Transform2D,
                    PhysicsShape = unit.PhysicsEntity.Shape,
                };
                unit.StatHandler?.Capture(ref us.StatState);
                unit.CombatModifiers?.Capture(ref us.CombatModifierState);
                unit.AttackHandler?.Capture(ref us.AttackState);
                unit.MovementHandler?.Capture(ref us.MovementState);
                unit.AbilityHandler?.Capture(ref us.AbilityState);
                unit.BuffHandler?.Capture(ref us.BuffState);
                unit.CrowdControl?.Capture(ref us.CCState);
                unit.Locomotion?.Capture(ref us.LocomotionState);
                unit.EquipmentHandler?.Capture(ref us.EquipmentState);
                _unitStateBuffer.Add(us);
            }
            snapshot.UnitWorldState.Units.AddRange(_unitStateBuffer);
            snapshot.UnitWorldState.RuntimeRevision = _unitWorld.RuntimeRevision;
            if (RandomService != null) snapshot.RandomState = RandomService.Capture();
            MatchRule?.Capture(ref snapshot.MatchRuleState);
            if (CombatSystem != null) { var cs = CombatSnapshot.Default; CombatSystem.Capture(ref cs); snapshot.CombatState = cs; }
            if (EquipmentShop != null) { var es = EquipmentShopRuntimeSnapshot.Empty; EquipmentShop.Capture(ref es); snapshot.EquipmentShopState = es; }
            if (ProjectileWorld != null) { var ps = ProjectileWorldSnapshot.Empty; ProjectileWorld.Capture(ref ps); snapshot.ProjectileState = ps; }
            _physicsWorld?.Capture(ref snapshot.PhysicsState);
            MinionSystem?.Capture(ref snapshot.UnitWorldState.MinionSystemState);
            _unitWorld.RespawnTimer?.Capture(
                ref snapshot.UnitWorldState.PendingUnitLifecycleState);
            JungleCampSystem?.Capture(_campStateBuffer);
            snapshot.UnitWorldState.JungleCampStates.AddRange(_campStateBuffer);
            _aiStateBuffer.Clear();
            foreach (var ai in _unitWorld.AIControllers) { var s = new UnitAIControllerSnapshot(); ai.Capture(ref s); _aiStateBuffer.Add(s); }
            snapshot.UnitWorldState.AIControllerStates.AddRange(_aiStateBuffer);
            return snapshot;
        }

        public void RestoreFromSnapshot(
            GameplaySnapshot snapshot,
            int snapshotTick = -1,
            ExecutionMode executionMode = ExecutionMode.ServerAuthority)
        {
            if (snapshot.SchemaVersion != 4)
            {
                throw new DeterministicSimulationException(
                    $"Unsupported GameplaySnapshot schema {snapshot.SchemaVersion}; expected 4.");
            }

            RestorePhase(snapshot);
            int targetTick = snapshotTick >= 0 ? snapshotTick : LocalSimulationTick;
            var context = new RollbackContext(targetTick, executionMode);
            ResolvePhase(context);
            RebuildPhase(context);
            if (snapshotTick >= 0) LocalSimulationTick = snapshotTick;
        }

        private void RestorePhase(in GameplaySnapshot snapshot)
        {
            List<UnitSnapshot> states = snapshot.UnitWorldState.Units;
            if (states == null)
            {
                throw new DeterministicSimulationException(
                    "UnitWorldSnapshot is missing its Unit list.");
            }

            UnitUid previousUid = default;
            for (int i = 0; i < states.Count; i++)
            {
                UnitSnapshot us = states[i];
                if (!us.UnitUid.IsValid() || (i > 0 && previousUid.CompareTo(us.UnitUid) >= 0))
                {
                    throw new DeterministicSimulationException(
                        "Unit snapshots must contain unique, strictly increasing UnitUid values.");
                }

                previousUid = us.UnitUid;
            }

            ReconcileUnitTopology(states);
            _unitWorld.RestoreRuntimeRevision(snapshot.UnitWorldState.RuntimeRevision);

            for (int i = 0; i < states.Count; i++)
            {
                UnitSnapshot us = states[i];
                if (!_unitWorld.TryGetUnit(us.UnitUid, out UnitType unit))
                    throw new DeterministicSimulationException(
                        $"Unit topology reconciliation did not create {us.UnitUid}.");
                unit.RestoreCoreState(
                    us.UnitUid,
                    us.OwnerUid,
                    us.UnitKind,
                    us.UnitSubKindId,
                    us.TeamId,
                    us.UnitPrototypeId,
                    us.LifeState,
                    us.CapabilityState,
                    us.HitReactionState);
                unit.StatHandler?.Restore(us.StatState);
                unit.CombatModifiers?.Restore(us.CombatModifierState);
                unit.AttackHandler?.Restore(us.AttackState);
                unit.MovementHandler?.Restore(us.MovementState);
                unit.AbilityHandler?.Restore(us.AbilityState);
                unit.BuffHandler?.Restore(us.BuffState);
                unit.CrowdControl?.Restore(us.CCState);
                unit.Locomotion?.Restore(us.LocomotionState);
                unit.EquipmentHandler?.Restore(us.EquipmentState);
                unit.PhysicsEntity.RestoreLogicSpatialState(us.PhysicsTransform, us.PhysicsShape);
            }
            _unitWorld.RespawnTimer?.Restore(
                snapshot.UnitWorldState.PendingUnitLifecycleState);
            CombatSystem?.Restore(snapshot.CombatState);
            MatchRule?.Restore(snapshot.MatchRuleState);
            EquipmentShop?.Restore(snapshot.EquipmentShopState);
            ProjectileWorld?.Restore(snapshot.ProjectileState);
            _physicsWorld?.Restore(snapshot.PhysicsState);
            if (RandomService != null)
            {
                if (snapshot.RandomState.State == 0u)
                {
                    throw new DeterministicSimulationException(
                        "GameplaySnapshot is missing deterministic random state.");
                }
                RandomService.Restore(snapshot.RandomState);
            }

            var nonHeroState = new NonHeroWorldSnapshot
            {
                MinionSystemState = snapshot.UnitWorldState.MinionSystemState,
                JungleCampStates = snapshot.UnitWorldState.JungleCampStates,
                AIControllerStates = snapshot.UnitWorldState.AIControllerStates,
            };
            NonHeroHelper?.RestoreNonHero(nonHeroState);
        }

        private void ReconcileUnitTopology(List<UnitSnapshot> states)
        {
            var runtimeUnits = new List<UnitType>(_unitWorld.GetAllUnits());
            int runtimeIndex = 0;
            int snapshotIndex = 0;
            while (runtimeIndex < runtimeUnits.Count || snapshotIndex < states.Count)
            {
                if (runtimeIndex >= runtimeUnits.Count)
                {
                    CreateUnitForRestore(states[snapshotIndex++]);
                    continue;
                }
                if (snapshotIndex >= states.Count)
                {
                    _unitWorld.RemoveUnitForRollbackRestore(runtimeUnits[runtimeIndex++]);
                    continue;
                }

                int comparison = runtimeUnits[runtimeIndex].UnitUid.CompareTo(
                    states[snapshotIndex].UnitUid);
                if (comparison < 0)
                    _unitWorld.RemoveUnitForRollbackRestore(runtimeUnits[runtimeIndex++]);
                else if (comparison > 0)
                    CreateUnitForRestore(states[snapshotIndex++]);
                else
                {
                    runtimeIndex++;
                    snapshotIndex++;
                }
            }
        }

        private void CreateUnitForRestore(in UnitSnapshot state)
        {
            _unitWorld.CreateUnitForRollbackRestore(
                state.UnitUid,
                state.OwnerUid,
                state.UnitPrototypeId,
                state.TeamId,
                state.PhysicsTransform.Position,
                state.PhysicsTransform.Forward);
        }

        private void ResolvePhase(in RollbackContext context)
        {
            var units = _unitWorld.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitType unit = units[i];
                unit.StatHandler?.Resolve(context);
                unit.CombatModifiers?.Resolve(context);
                unit.AttackHandler?.Resolve(context);
                unit.MovementHandler?.Resolve(context);
                unit.AbilityHandler?.Resolve(context);
                unit.BuffHandler?.Resolve(context);
                unit.CrowdControl?.Resolve(context);
                unit.Locomotion?.Resolve(context);
                unit.EquipmentHandler?.Resolve(context);
            }
            CombatSystem?.Resolve(context);
            MatchRule?.Resolve(_unitWorld);
            _unitWorld.RespawnTimer?.Resolve(context);
            ProjectileWorld?.Resolve(_unitWorld);
            EquipmentShop?.Resolve(context);
            NonHeroHelper?.ResolveNonHero(context);
            _physicsWorld?.Resolve();
        }

        private void RebuildPhase(in RollbackContext context)
        {
            var units = _unitWorld.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitType unit = units[i];
                unit.StatHandler?.Rebuild(context);
                unit.CombatModifiers?.Rebuild(context);
                unit.AttackHandler?.Rebuild(context);
                unit.MovementHandler?.Rebuild(context);
                unit.AbilityHandler?.Rebuild(context);
                unit.BuffHandler?.Rebuild(context);
                unit.CrowdControl?.Rebuild(context);
                unit.Locomotion?.Rebuild(context);
                unit.EquipmentHandler?.Rebuild(context);
            }
            CombatSystem?.Rebuild(context);
            EquipmentShop?.Rebuild(context);
            ProjectileWorld?.Rebuild(context);
            NonHeroHelper?.RebuildNonHero(context);
            _physicsWorld?.Rebuild();
        }

        private void TickNonHeroSystems(int tick)
        {
            foreach (var c in _unitWorld.AIControllers) c.AIThink();
            MinionSystem?.ProcessWave(tick);
            JungleCampSystem?.Tick(tick);
            _unitWorld.RespawnTimer?.Tick(tick);
        }

        private void DispatchCommand(GameplayCommand command)
        {
            if (!_unitWorld.TryGetUnit(command.UnitUid, out UnitType unit)) return;
            if (command.Kind == GameplayCommandKind.Move)
            {
                fp2 currentPosition = unit.MovementHandler?.Snapshot.Position ?? fp2.zero;
                unit.MovementHandler?.ApplyMoveInput(
                    new MoveIntent(command.MoveTargetPoint - currentPosition));
            }
            else if (command.Kind == GameplayCommandKind.Attack) unit.AttackHandler?.ApplyAttackInput(command.AttackTargetUid);
            else if (command.Kind == GameplayCommandKind.CastAbility) unit.AbilityHandler?.HandleSignal(new AbilitySignal { Slot = command.AbilitySlot, Verb = command.AbilityVerb, Aim = command.Aim });
            else if (command.Kind == GameplayCommandKind.CancelAbility) unit.AbilityHandler?.HandleSignal(new AbilitySignal { Slot = command.AbilitySlot, Verb = AbilitySignalVerb.Cancel, Aim = AimSnapshot.None });
        }

        private void SyncMovementToPhysics()
        {
            if (_physicsWorld == null) return;
            foreach (var entity in _physicsWorld.UnitEntities)
                if (entity.QueryInfo.Owner is UnitType unit && unit.MovementHandler != null)
                { ref readonly var snap = ref unit.MovementHandler.Snapshot; entity.SetLogicPose(snap.Position, snap.Facing); }
        }

    }

    internal struct RvoResult
    {
        public UnitUid UnitUid;
        public fp2 FinalVelocity;
        public static readonly RvoResult Zero = default;
    }
}
