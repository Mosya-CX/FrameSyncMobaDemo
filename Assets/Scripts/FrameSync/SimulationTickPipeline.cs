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
        private readonly List<InitialSpawnEntry> _initialSpawnRequests =
            new List<InitialSpawnEntry>();

        // RVO system instance (created once, reused per Tick)
        private DeterministicRVOSystem _rvoSystem;
        private readonly RvoOrchestrator
            _rvoOrchestrator =
                new RvoOrchestrator();

        public CombatSystem CombatSystem { get; set; }
        public GoldIncomeRuntime GoldIncome { get; set; }
        public ProjectileWorld ProjectileWorld { get; set; }
        public EquipmentShopRuntime EquipmentShop { get; set; }
        public NaturalGoldIncomeSystem NaturalGoldIncome { get; set; }
        public NonHeroRestoreHelper NonHeroHelper { get; set; }
        public ProjectileHitResolver ProjectileHitResolver { get; set; }
        public DeterministicRandomService RandomService { get; set; }
        public MatchRuleRuntime MatchRule { get; set; }
        public FrameSyncMoba.Unit.MatchEventTracker MatchEventTracker { get; set; }
        public CommandCollector CommandCollector => _collector;
        internal int AuthorityReplayTick { get; set; } = -1;
        public int MaxFutureCommandTicks { get; set; } = 12;

        public int LocalSimulationTick { get; private set; }
        public uint LastChecksum { get; private set; }
        public event Action<int, IReadOnlyList<GameplayCommand>, uint> TickCompleted;
        public Action RestoreStaticBindings { get; set; }

        public bool HasPredictedMatchEndCandidate()
        {
            if (MatchRule == null ||
                MatchRule.CurrentPhase != MatchPhase.Running ||
                !MatchRule.BlueBaseUnitUid.IsValid() ||
                !MatchRule.RedBaseUnitUid.IsValid())
                return false;
            return IsFormallyDead(MatchRule.BlueBaseUnitUid) ||
                IsFormallyDead(MatchRule.RedBaseUnitUid);
        }

        public SimulationTickPipeline(UnitWorld unitWorld, PhysicsWorld physicsWorld = null)
        {
            _unitWorld = unitWorld;
            _physicsWorld = physicsWorld;
            _collector = new CommandCollector();
            _checksumWriter = new CanonicalByteWriter(new byte[262144]);
            LocalSimulationTick = 0;
            _rvoSystem = new DeterministicRVOSystem(RVOConfig.Default);
        }

        public void SubmitCommand(GameplayCommand command)
        {
            int latestAllowedTick = checked(
                LocalSimulationTick + MaxFutureCommandTicks);
            if (command.TargetTick < LocalSimulationTick ||
                command.TargetTick > latestAllowedTick)
                throw new DeterministicSimulationException(
                    $"Command Tick {command.TargetTick} is outside legal range [{LocalSimulationTick}, {latestAllowedTick}].");
            _collector.Collect(command);
        }

        public void ReplaceCommandsForNextTick(IReadOnlyList<GameplayCommand> commands)
        {
            _collector.BeginTick(LocalSimulationTick);
            if (commands == null) return;
            for (int i = 0; i < commands.Count; i++) SubmitCommand(commands[i]);
        }

        public void QueueInitialSpawn(in UnitSpawnRequest request)
        {
            QueueInitialSpawn(
                request,
                MatchTopologyRole.None);
        }

        public void QueueInitialSpawn(
            in UnitSpawnRequest request,
            MatchTopologyRole topologyRole)
        {
            if (LocalSimulationTick != 0)
                throw new InvalidOperationException(
                    "Initial Unit spawns must be queued before Tick 0.");
            if (topologyRole <
                    MatchTopologyRole.None ||
                topologyRole >
                    MatchTopologyRole.RedBase)
                throw new ArgumentOutOfRangeException(
                    nameof(topologyRole));
            _initialSpawnRequests.Add(
                new InitialSpawnEntry(
                    request,
                    topologyRole));
        }

        public UnitUid[] MaterializeInitialSpawnsForBootstrap(
            SimulationTickContextController controller,
            int startTick)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));
            if (startTick < 0)
                throw new ArgumentOutOfRangeException(nameof(startTick));
            if (LocalSimulationTick != 0)
                throw new InvalidOperationException(
                    "Bootstrap initial state can only be materialized before simulation starts.");

            controller.BeginTick(
                startTick,
                ExecutionMode.ServerAuthority);
            try
            {
                UnitUid[] spawned = MaterializeInitialSpawns();
                LocalSimulationTick = startTick;
                return spawned;
            }
            finally
            {
                controller.EndTick();
            }
        }

        public void ExecuteTick(
            SimulationTickContextController controller,
            ExecutionMode executionMode = ExecutionMode.ServerAuthority)
        {
            int tick = LocalSimulationTick;
            controller.BeginTick(tick, executionMode);
            GoldIncome?.BeginTick(tick);
            NaturalGoldIncome?.Tick(tick);
            try
            {
                VisualEventOutput.Clear();
                CombatSystem?.BeginTick();
                MaterializeInitialSpawns();
                var commands = _collector.ConsumeCanonicalCommands(tick);
                foreach (var cmd in commands)
                    DispatchCommand(cmd);

                var units = _unitWorld.GetAllUnits();

                // Control system advances before behavior planning so the
                // Planner/Arbiter read this Tick's final StateView, and the
                // coarse CapabilityState is refreshed from it (Unit Framework
                // v27.3 8.4).
                for (int i = 0; i < units.Count; i++)
                {
                    UnitType unit = units[i];
                    if (unit == null) continue;
                    unit.CrowdControl?.Advance();
                    unit.RefreshCapabilityState();
                }

                // Phase 0: BehaviorPlanner + ActionArbiter (Unit Framework v27.3 §3)
                foreach (var unit in units)
                {
                    if (unit?.Planner == null || unit.Arbiter == null) continue;
                    unit.Planner.Tick(out ActionRequest request);
                    if (request == null) continue;
                    var result = unit.Arbiter.Evaluate(request);
                    if (result == ArbitrationResult.Rejected) continue;
                    ExecuteActionRequest(unit, request, result == ArbitrationResult.Interrupt);
                }

                // Phase 1: Locomotion evaluation
                _locomotionBuffer.Clear();
                foreach (var unit in units)
                {
                    if (unit == null) continue;
                    var locomotion = unit.Locomotion?.Evaluate()
                        ?? LocomotionResult.Idle(unit.UnitUid);
                    _locomotionBuffer.Add(locomotion);
                }

                // Phase 1.5: RVO avoidance step (Pathfinding Design v13.1 section 10.6)
                _physicsWorld?.BuildRvoGrid();
                _rvoOrchestrator.Step(
                    _rvoSystem,
                    _physicsWorld,
                    units,
                    _locomotionBuffer);

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
                    unit.TickTags();
                    unit.BuffHandler?.Advance();
                    unit.EquipmentHandler?.AdvanceEffects();
                    unit.HitReaction.TickUpdate();
                    unit.AbilityHandler?.TickUpdate();
                    unit.MovementHandler?.TickUpdate();
                    unit.AttackHandler?.TickUpdate();
                }

                // Fixed-phase check: interrupt runtimes whose action is now
                // blocked by the latest control state (Unit Framework v27.3
                // 3.4 EvaluateCurrentRuntimes).
                for (int i = 0; i < units.Count; i++)
                {
                    if (units[i] == null) continue;
                    units[i].Arbiter?.EvaluateCurrentRuntimes();
                }

                // Phase 3.5: Wall penetration detection and correction
                // Runs after all movement has been applied for this Tick.
                // (Pathfinding Design v13.1 section 12.2)
                if (_unitWorld.PathGrid != null)
                {
                    foreach (var unit in units)
                    {
                        if (unit?.MovementHandler == null) continue;
                        var correction = WallPenetrationResolver.Detect(
                            unit.UnitUid,
                            unit.PhysicsEntity.Transform2D.Position,
                            unit.PhysicsEntity.Shape.Radius,
                            _unitWorld.PathGrid);

                        if (correction.HasValue)
                        {
                            unit.MovementHandler.ApplyCorrection(
                                correction.Value);
                        }
                    }
                }

                ProjectileWorld?.CommitSpawns();
                ProjectileWorld?.AdvanceMotion();
                ProjectileWorld?.UpdateLifecycle();
                _physicsWorld?.BuildUnitFinalGrid();
                _physicsWorld?.DetectUnitCollisionEvents();
                ProjectileHitResolver?.ResolveAllHits(ProjectileWorld);
                ProjectileHitResolver?.EmitEffects(ProjectileWorld);
                ProjectileWorld?.FlushDestroy();
                CombatSystem?.SettleActiveRequests();
                CombatSystem?.EndTick();

                if (MatchRule != null)
                {
                    MatchRule.Statistics.Consume(CombatSystem?.DeathResults, _unitWorld);
                    MatchRule.AdvanceTick(tick);

                    // Process kill streaks and multikills (per-design kill feedback)
                    var deathResults = CombatSystem?.DeathResults;
                    if (deathResults != null && MatchEventTracker != null)
                    {
                        for (int di = 0; di < deathResults.Count; di++)
                        {
                            var dr = deathResults[di];
                            if (!dr.KillerHeroUid.IsValid()) continue;
                            if (!_unitWorld.TryGetUnit(dr.KillerHeroUid, out var killer)) continue;
                            if (!_unitWorld.TryGetUnit(dr.VictimUid, out var victim)) continue;
                            int killerSlot = killer.ControlledByPlayerSlot;
                            int victimSlot = victim.ControlledByPlayerSlot;
                            if (killerSlot < 0 || victimSlot < 0) continue;
                            MatchEventTracker.RecordKill(killerSlot, victimSlot, tick);
                        }
                    }
                    if (executionMode == ExecutionMode.ServerAuthority ||
                        (executionMode == ExecutionMode.ClientReplay && AuthorityReplayTick == tick))
                        MatchRule.EvaluateAuthorityConfirmedTick(tick, _unitWorld);
                }

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

                _unitWorld.ProcessPostCombatDeathDisposals(
                    CombatSystem?.DeathResults);

                // Drop attack targets that died this Tick so Tick-end
                // snapshots never hold stale unit references.
                var attackCleanup =
                    _unitWorld.GetAllUnits();
                for (int ci = 0;
                     ci < attackCleanup.Count;
                     ci++)
                {
                    attackCleanup[ci]?.AttackHandler
                        ?.ClearTargetIfMissing();
                }

                TickNonHeroSystems(tick);
                // Unit v27.3 5.5.1: recompute every still-Dirty stat at tick
                // end so the next tick's previous-value baseline (and the
                // checksum) is identical regardless of spawn vs restore path.
                var finalizeUnits = _unitWorld.GetAllUnits();
                for (int i = 0;
                     i < finalizeUnits.Count;
                     i++)
                {
                    finalizeUnits[i]
                        .StatHandler?.FinalizeTick();
                }
                GameplaySnapshot checksumState = CaptureAggregateSnapshot();
                GoldIncomeBatchDigest goldDigest = GoldIncome?.GetBatchDigest(tick)
                    ?? new GoldIncomeBatchDigest(0);
                LastChecksum = SharedGameplayChecksum.Compute(
                    checksumState, goldDigest, _checksumWriter);
                TickCompleted?.Invoke(tick, commands, LastChecksum);
            }
            finally
            {
                controller.EndTick();
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
                unit.ValidateActionRuntimeSnapshotBoundary();
                var us = new UnitSnapshot
                {
                    UnitUid = unit.UnitUid,
                    OwnerUid = unit.OwnerUid,
                    UnitKind = unit.UnitKind,
                    UnitSubKindId = unit.UnitSubKindId,
                    TeamId = unit.TeamId,
                    UnitPrototypeId = unit.UnitPrototypeId,
                    RespawnPosition = unit.RespawnPosition,
                    LifeState = unit.LifeState,
                    CapabilityState = unit.CapabilityState,
                    HitReactionState = unit.HitReaction,
                    IntentState = unit.Intent,
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
                us.Tags = unit.CaptureTags();
                _unitStateBuffer.Add(us);
            }
            snapshot.UnitWorldState.Units = _unitStateBuffer.ToArray();
            snapshot.UnitWorldState.RuntimeRevision = _unitWorld.RuntimeRevision;
            if (RandomService != null) snapshot.RandomState = RandomService.Capture();
            MatchRule?.Capture(ref snapshot.MatchRuleState);
            if (CombatSystem != null) { var cs = CombatSnapshot.Default; CombatSystem.Capture(ref cs); snapshot.CombatState = cs; }
            if (EquipmentShop != null) { var es = EquipmentShopRuntimeSnapshot.Empty; EquipmentShop.Capture(ref es); snapshot.EquipmentShopState = es; }
            if (ProjectileWorld != null) { var ps = ProjectileWorldSnapshot.Empty; ProjectileWorld.Capture(ref ps); snapshot.ProjectileState = ps; }
            _physicsWorld?.Capture(ref snapshot.PhysicsState);
            _unitWorld.MinionSystem?.Capture(
                ref snapshot.UnitWorldState.MinionSystemState);
            _unitWorld.RespawnTimer?.Capture(
                ref snapshot.UnitWorldState.PendingUnitLifecycleState);
            _campStateBuffer.Clear();
            var camps = _unitWorld.JungleCamps;
            for (int campIndex = 0;
                 campIndex < camps.Count;
                 campIndex++)
            {
                JungleCampSnapshot campState = default;
                camps[campIndex].Capture(
                    ref campState);
                _campStateBuffer.Add(campState);
            }
            snapshot.UnitWorldState.JungleCampStates = _campStateBuffer.ToArray();
            _aiStateBuffer.Clear();
            foreach (var ai in _unitWorld.AIControllers) { var s = new UnitAIControllerSnapshot(); ai.Capture(ref s); _aiStateBuffer.Add(s); }
            snapshot.UnitWorldState.AIControllerStates = _aiStateBuffer.ToArray();
            return snapshot;
        }

        public void RestoreFromSnapshot(
            GameplaySnapshot snapshot,
            int snapshotTick = -1,
            ExecutionMode executionMode = ExecutionMode.ServerAuthority)
        {
            if (snapshot.SchemaVersion !=
                GameplaySnapshot.CurrentSchemaVersion)
            {
                throw new DeterministicSimulationException(
                    $"Unsupported GameplaySnapshot schema {snapshot.SchemaVersion}; expected {GameplaySnapshot.CurrentSchemaVersion}.");
            }

            RestorePhase(snapshot);
            int targetTick = snapshotTick >= 0 ? snapshotTick : LocalSimulationTick;
            var context = new RollbackContext(targetTick, executionMode);
            ResolvePhase(context);
            RebuildPhase(context);
            if (snapshotTick >= 0) LocalSimulationTick = snapshotTick;
        }

        internal void DiscardPendingInitialSpawnsForAuthoritativeRestore()
        {
            _initialSpawnRequests.Clear();
        }

        private void RestorePhase(in GameplaySnapshot snapshot)
        {
            UnitSnapshot[] states = snapshot.UnitWorldState.Units;
            if (states == null)
            {
                throw new DeterministicSimulationException(
                    "UnitWorldSnapshot is missing its Unit list.");
            }

            UnitUid previousUid = default;
            for (int i = 0; i < states.Length; i++)
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
            _unitWorld.ResetSpawnSequenceForRollbackRestore();

            for (int i = 0; i < states.Length; i++)
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
                    us.HitReactionState,
                    us.RespawnPosition);
                unit.RestoreBehaviorState(us.IntentState);
                unit.StatHandler?.Restore(us.StatState);
                unit.CombatModifiers?.Restore(us.CombatModifierState);
                unit.AttackHandler?.Restore(us.AttackState);
                unit.MovementHandler?.Restore(us.MovementState);
                unit.AbilityHandler?.Restore(us.AbilityState);
                unit.BuffHandler?.Restore(us.BuffState);
                unit.CrowdControl?.Restore(us.CCState);
                unit.Locomotion?.Restore(us.LocomotionState);
                unit.EquipmentHandler?.Restore(us.EquipmentState);
                unit.RestoreTags(us.Tags);
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
            RestoreStaticBindings?.Invoke();
        }

        private UnitUid[] MaterializeInitialSpawns()
        {
            if (_initialSpawnRequests.Count == 0)
                return Array.Empty<UnitUid>();

            var spawnedUids =
                new UnitUid[_initialSpawnRequests.Count];
            UnitUid blueBase = default;
            UnitUid redBase = default;
            TeamId blueBaseTeam = default;
            TeamId redBaseTeam = default;
            for (int spawnIndex = 0;
                 spawnIndex < _initialSpawnRequests.Count;
                 spawnIndex++)
            {
                InitialSpawnEntry entry =
                    _initialSpawnRequests[spawnIndex];
                UnitUid spawnedUid =
                    _unitWorld.SpawnUnit(
                        entry.Request);
                spawnedUids[spawnIndex] = spawnedUid;
                if (entry.TopologyRole ==
                    MatchTopologyRole.None)
                    continue;
                if (!_unitWorld.TryGetUnit(
                        spawnedUid,
                        out UnitType spawned) ||
                    spawned.UnitKind !=
                        UnitKind.Structure ||
                    spawned.TeamId ==
                        TeamId.Neutral)
                    throw new DeterministicSimulationException(
                        "TeamBase topology requires a non-neutral Structure Unit.");
                if (entry.TopologyRole ==
                    MatchTopologyRole.BlueBase)
                {
                    if (blueBase.IsValid())
                        throw new DeterministicSimulationException(
                            "Multiple BlueBase initial spawns are configured.");
                    blueBase = spawnedUid;
                    blueBaseTeam =
                        spawned.TeamId;
                }
                else
                {
                    if (redBase.IsValid())
                        throw new DeterministicSimulationException(
                            "Multiple RedBase initial spawns are configured.");
                    redBase = spawnedUid;
                    redBaseTeam =
                        spawned.TeamId;
                }
            }
            _initialSpawnRequests.Clear();
            if (blueBase.IsValid() ||
                redBase.IsValid())
            {
                if (!blueBase.IsValid() ||
                    !redBase.IsValid() ||
                    MatchRule == null ||
                    blueBaseTeam ==
                        redBaseTeam)
                    throw new DeterministicSimulationException(
                        "TeamBase topology requires exactly one BlueBase and one RedBase on distinct teams plus MatchRuleRuntime.");
                MatchRule.RegisterBases(
                    blueBase,
                    redBase);
            }
            return spawnedUids;
        }

        private void ReconcileUnitTopology(UnitSnapshot[] states)
        {
            var runtimeUnits = new List<UnitType>(_unitWorld.GetAllUnits());
            int runtimeIndex = 0;
            int snapshotIndex = 0;
            while (runtimeIndex < runtimeUnits.Count || snapshotIndex < states.Length)
            {
                if (runtimeIndex >= runtimeUnits.Count)
                {
                    CreateUnitForRestore(states[snapshotIndex++]);
                    continue;
                }
                if (snapshotIndex >= states.Length)
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
                unit.ResolveBehaviorState();
                unit.StatHandler?.Resolve(context);
                unit.CombatModifiers?.Resolve(context);
                unit.AttackHandler?.Resolve(context);
                unit.MovementHandler?.Resolve(context);
                unit.AbilityHandler?.Resolve(context);
                unit.BuffHandler?.Resolve(context);
                unit.CrowdControl?.Resolve(context);
                unit.Locomotion?.Resolve(context);
                unit.EquipmentHandler?.Resolve(context);
                var tags = unit.Tags;
                for (int t = 0;
                     t < tags.Count;
                     t++)
                {
                    UnitTag tag = tags[t];
                    if (tag.Uid.SourceUnit.IsValid() &&
                        !_unitWorld.TryGetUnit(
                            tag.Uid.SourceUnit,
                            out _))
                    {
                        throw new DeterministicSimulationException(
                            $"Unit {unit.UnitUid} tag '{tag.Key}' " +
                            $"references missing source " +
                            $"{tag.Uid.SourceUnit}.");
                    }
                }
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
                unit.RefreshCapabilityState();
            }
            CombatSystem?.Rebuild(context);
            EquipmentShop?.Rebuild(context);
            ProjectileWorld?.Rebuild(context);
            NonHeroHelper?.RebuildNonHero(context);
            _physicsWorld?.Rebuild();
        }

        private void TickNonHeroSystems(int tick)
        {
            _unitWorld.MinionSystem?.TickLogic();
            _unitWorld.TickJungleCamps();
            _unitWorld.TickAIControllers();
            _unitWorld.RespawnTimer?.Tick(tick);
        }

        private void DispatchCommand(GameplayCommand command)
        {
            if (!_unitWorld.TryGetUnit(command.UnitUid, out UnitType unit)) return;

            if (command.Kind ==
                GameplayCommandKind.AllocateAbilitySkillPoint)
            {
                unit.AbilityHandler?.TryAllocateSkillPoint(
                    command.AbilitySlot);
                return;
            }
            if (command.Kind ==
                GameplayCommandKind.Debug)
            {
                DispatchDebugCommand(
                    command,
                    unit);
                return;
            }
            if (command.Kind ==
                GameplayCommandKind.EquipmentShop)
            {
                DispatchEquipmentShopCommand(command, unit);
                return;
            }
            if (command.Kind ==
                GameplayCommandKind.SwapEquipmentSlot)
            {
                unit.EquipmentHandler?.SwapSlots(
                    command.SourceSlot,
                    command.TargetSlot);
                return;
            }
            if (command.Kind == GameplayCommandKind.UseItem)
            {
                if (unit.EquipmentHandler != null &&
                    unit.EquipmentHandler.Use(
                        command.SourceSlot,
                        command.Aim))
                    EquipmentShop?.InvalidateUndoByEquipmentUse(
                        command.PlayerSlot,
                        command.SourceSlot);
                return;
            }

            // Units with a Planner: set Intent and let the behavior chain handle routing.
            // Units without: use the legacy direct-handler path.
            if (unit.Planner != null)
            {
                if (command.Kind == GameplayCommandKind.Move)
                {
                    unit.Planner.ReplaceIntent(new UnitIntent
                    {
                        Kind = IntentKind.MoveToPosition,
                        TargetPosition = command.MoveTargetPoint,
                        AllowChase = false,
                        AllowReplan = true,
                    });
                }
                else if (command.Kind == GameplayCommandKind.Attack)
                {
                    unit.Planner.ReplaceIntent(new UnitIntent
                    {
                        Kind = IntentKind.AttackTarget,
                        TargetUnit = command.AttackTargetUid,
                        AllowChase = true,
                        AllowReplan = false,
                    });
                }
                else if (command.Kind == GameplayCommandKind.CastAbility)
                {
                    unit.Planner.ReplaceIntent(new UnitIntent
                    {
                        Kind = IntentKind.CastAbility,
                        AbilityId = command.AbilitySlot,
                        AbilityVerb = command.AbilityVerb,
                        AbilityAim = command.Aim,
                        TargetPosition = command.Aim.TargetPoint,
                        TargetUnit = command.Aim.TargetUnitUid,
                        AllowChase = true,
                        AllowReplan = false,
                    });
                }
                else if (command.Kind == GameplayCommandKind.CancelAbility)
                {
                    unit.AbilityHandler?.HandleSignal(new AbilitySignal { Slot = command.AbilitySlot, Verb = AbilitySignalVerb.Cancel, Aim = AimSnapshot.None });
                }
                return;
            }

            // Legacy path for units without Planner
            if (command.Kind == GameplayCommandKind.Move)
            {
                CancelWindupForNewOrder(unit);
                if (unit.Locomotion != null)
                {
                    var request = RouteMoveRequest.ToPosition(command.MoveTargetPoint);
                    request.AllowRVO = true;
                    unit.Locomotion.AcceptRouteRequest(request);
                }
                else
                {
                    fp2 currentPosition = unit.MovementHandler?.Position ?? fp2.zero;
                    unit.MovementHandler?.ApplyMoveInput(
                        new MoveIntent(command.MoveTargetPoint - currentPosition));
                }
            }
            else if (command.Kind == GameplayCommandKind.Attack)
            {
                CancelWindupForNewOrder(
                    unit,
                    command.AttackTargetUid,
                    isAttack: true);
                unit.AttackHandler?.ApplyAttackInput(command.AttackTargetUid);
            }
            else if (command.Kind == GameplayCommandKind.CastAbility)
            {
                CancelWindupForNewOrder(unit);
                unit.AbilityHandler?.HandleSignal(new AbilitySignal { Slot = command.AbilitySlot, Verb = command.AbilityVerb, Aim = command.Aim });
            }
            else if (command.Kind == GameplayCommandKind.CancelAbility) unit.AbilityHandler?.HandleSignal(new AbilitySignal { Slot = command.AbilitySlot, Verb = AbilitySignalVerb.Cancel, Aim = AimSnapshot.None });
        }

        /// <summary>
        /// A new Order/Command replaces the previous behavior (Unit Framework
        /// v27.3 4.x): terminate an uncommitted attack windup so it does not
        /// keep committing after the goal changed. Same-target attack
        /// repeats keep the in-flight windup (no restart).
        /// </summary>
        private static void CancelWindupForNewOrder(
            UnitType unit,
            UnitUid newAttackTarget = default,
            bool isAttack = false)
        {
            AttackHandler attack =
                unit?.AttackHandler;
            if (attack == null ||
                !attack.IsAttackCycleActive ||
                attack.ImpactCommitted)
            {
                return;
            }
            if (isAttack &&
                attack.CurrentTargetUid ==
                    newAttackTarget)
            {
                return;
            }
            attack.CancelBeforeCommit();
        }

        private void DispatchDebugCommand(
            GameplayCommand command,
            UnitType unit)
        {
            switch ((DebugCommandOp)command.DebugOp)
            {
                case DebugCommandOp.Heal:
                    fp maxHp =
                        unit.StatHandler
                            ?.GetStat(
                                StatId.MaxHealth) ??
                        fp.one;
                    unit.StatHandler
                        ?.SetCurrentHealth(maxHp);
                    break;
                case DebugCommandOp.RestoreMana:
                    fp maxResource =
                        unit.StatHandler
                            ?.GetStat(
                                StatId
                                    .MaxCastResource) ??
                        fp.one;
                    unit.StatHandler
                        ?.SetCurrentCastResource(
                            maxResource);
                    break;
                case DebugCommandOp.Revive:
                    _unitWorld.ForceRevive(unit);
                    break;
                case DebugCommandOp.LevelUp:
                    int required =
                        unit.StatHandler
                            ?.ExperienceRequiredForNextLevel
                            ?? 0;
                    if (required > 0)
                    {
                        unit.StatHandler
                            ?.AddExperience(required);
                    }
                    break;
                case DebugCommandOp.AddGold:
                    GoldIncome?.RequestGoldIncome(
                        command.PlayerSlot,
                        command.DebugValue,
                        GoldIncomeReason.UnitKill);
                    break;
                case DebugCommandOp.Kill:
                    if (unit.UnitKind == UnitKind.Structure)
                    {
                        break;
                    }
                    fp killBase =
                        unit.StatHandler
                            ?.GetStat(
                                StatId.MaxHealth) ??
                        fp.zero;
                    fp killDamage =
                        killBase > fp.zero
                            ? killBase * (fp)100
                            : (fp)99999;
                    var killSource =
                        new SourceDescriptor
                        {
                            SourceType =
                                CombatSourceType.Attack,
                            SourceId =
                                CombatBuiltinSourceId
                                    .BasicAttack,
                            OwnerUnitUid =
                                unit.UnitUid,
                            EmitterUnitUid =
                                unit.UnitUid,
                        };
                    CombatSystem?.SubmitDamage(
                        new DamageRequest
                        {
                            Header =
                                new CombatRequestHeader
                                {
                                    SourceUnitUid =
                                        unit.UnitUid,
                                    TargetUnitUid =
                                        unit.UnitUid,
                                    SourceDescriptor =
                                        killSource,
                                    RecipeId =
                                        CombatBuiltinRecipeId
                                            .BasicAttackDamage,
                                },
                            DamageType =
                                DamageType.True,
                            BaseDamage =
                                killDamage,
                        });
                    break;
            }
        }

        private void DispatchEquipmentShopCommand(
            GameplayCommand command,
            UnitType unit)
        {
            if (EquipmentShop == null ||
                unit.EquipmentHandler == null ||
                GoldIncome == null)
                return;
            EquipmentShop.GetOrCreateTrader(
                command.PlayerSlot,
                command.ControlledUnitUid);
            int currentGold = checked(
                GoldIncome.GetConfirmedEarnedGoldTotal(
                    command.PlayerSlot) +
                EquipmentShop.ComputeEffectiveShopGoldDelta(
                    command.PlayerSlot));
            switch (command.ShopOperationType)
            {
                case EquipmentShopCommandOperationType.Purchase:
                    if (EquipmentShop.TryBuildPurchasePlan(
                            command.PlayerSlot,
                            command.EquipmentId,
                            currentGold,
                            unit.EquipmentHandler,
                            out EquipmentPurchasePlan plan,
                            out _))
                        EquipmentShop.ProcessPurchase(
                            command.PlayerSlot,
                            plan,
                            unit.EquipmentHandler,
                            out _);
                    break;
                case EquipmentShopCommandOperationType.Sell:
                    if (EquipmentShop.TrySell(
                            command.PlayerSlot,
                            command.SourceSlot,
                            unit.EquipmentHandler,
                            out int sellValue,
                            out _))
                        EquipmentShop.ProcessSell(
                            command.PlayerSlot,
                            command.SourceSlot,
                            unit.EquipmentHandler,
                            sellValue,
                            out _);
                    break;
                case EquipmentShopCommandOperationType.Undo:
                    if (EquipmentShop.CanUndo(
                            command.PlayerSlot,
                            currentGold,
                            out _))
                        EquipmentShop.ProcessUndo(
                            command.PlayerSlot,
                            currentGold,
                            unit.EquipmentHandler,
                            out _);
                    break;
                default:
                    throw new DeterministicSimulationException(
                        $"Unsupported EquipmentShop operation {command.ShopOperationType}.");
            }
        }

        private bool IsFormallyDead(UnitUid uid)
        {
            if (!_unitWorld.TryGetUnit(uid, out UnitType unit))
                throw new DeterministicSimulationException(
                    $"Registered base {uid} is missing.");
            return unit.LifeState == LifeState.Dead;
        }

        private void ExecuteActionRequest(UnitType unit, ActionRequest request, bool interrupt)
        {
            unit.CrowdControl?.OnOwnerActionStarted();
            if (interrupt && unit.ActionRuntimes != null)
            {
                // Cancel lower-priority actions before starting this one
                unit.ActionRuntimes.CancelByKind(
                    request.Kind == ActionKind.Cast ? ActionKind.Attack :
                    request.Kind == ActionKind.Attack ? ActionKind.Move :
                    ActionKind.None);
            }

            switch (request)
            {
                case MoveActionRequest moveReq:
                    if (unit.Locomotion != null)
                    {
                        var routeReq = moveReq.ChaseTarget.IsValid()
                            ? RouteMoveRequest.FollowUnit(
                                moveReq.ChaseTarget,
                                moveReq.StopRange,
                                moveReq.Purpose)
                            : RouteMoveRequest.ToPosition(moveReq.TargetPosition, moveReq.StopRange);
                        routeReq.Purpose =
                            moveReq.Purpose;
                        routeReq.AllowRVO = true;
                        unit.Locomotion.AcceptRouteRequest(routeReq);

                        // Attack-move cancel: a new movement request during
                        // the attack recovery window ends the recovery
                        // immediately so the next attack can start sooner
                        // (MoveCancelRecovery, attack design v6.2).
                        if (unit.AttackHandler != null &&
                            unit.AttackHandler
                                .IsAttackCycleActive &&
                            unit.AttackHandler
                                .ImpactCommitted)
                        {
                            unit.AttackHandler
                                .ResetAttackTimer(
                                    AttackTimerResetReason
                                        .MoveCancelRecovery);
                        }
                    }
                    else if (unit.MovementHandler != null)
                    {
                        fp2 currentPos = unit.MovementHandler.Position;
                        unit.MovementHandler.ApplyMoveInput(
                            new MoveIntent(moveReq.TargetPosition - currentPos));
                    }
                    break;

                case AttackActionRequest attackReq:
                    unit.AttackHandler?.ApplyAttackInput(attackReq.TargetUnit);
                    break;

                case CastActionRequest castReq:
                    unit.AbilityHandler?.HandleSignal(new AbilitySignal
                    {
                        Slot = (byte)castReq.AbilityId,
                        Verb = castReq.Verb,
                        Aim = castReq.Aim,
                    });
                    break;
            }
        }

        private readonly struct InitialSpawnEntry
        {
            public readonly UnitSpawnRequest Request;
            public readonly MatchTopologyRole TopologyRole;

            public InitialSpawnEntry(
                in UnitSpawnRequest request,
                MatchTopologyRole topologyRole)
            {
                Request = request;
                TopologyRole = topologyRole;
            }
        }

    }
}
