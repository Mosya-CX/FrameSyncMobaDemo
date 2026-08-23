using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public enum DiagnosticMinionWaveSize
    {
        OneMelee = 1,
        TwoMelee = 2,
        FullWave = 3,
    }

    /// <summary>
    /// Editor-only diagnostic driver for MinionTowerLongRunTest. It owns the
    /// test clock so one deterministic Gameplay Tick is logged at a time.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class MinionTowerDiagnosticDriver : MonoBehaviour
    {
        private const int TickRate = 30;
        private const int IsolatedWaveIntervalTicks = TickRate * 60 * 10;
        private const float KeyPointRadius = 0.75f;

        [SerializeField] private DiagnosticMinionWaveSize waveSize =
            DiagnosticMinionWaveSize.OneMelee;
        [SerializeField] private bool repeatWaves;
        [SerializeField] private bool logEverySimulationTick = true;

        private readonly Dictionary<UnitUid, UnitTrace> traces =
            new Dictionary<UnitUid, UnitTrace>();
        private readonly List<Transform> keyPoints = new List<Transform>(9);
        private readonly List<UnitUid> staleUids = new List<UnitUid>(32);

        private GameBootstrap bootstrap;
        private MinionWaveConfig runtimeWaveConfig;
        private float previousFixedDeltaTime;

        private sealed class UnitTrace
        {
            public fp2 PreviousPosition;
            public fp2 PreviousStep;
            public string KeyPointName;
            public RouteKind RouteKind;
            public int PathCursor = -1;
            public bool NeedRepath;
            public int LastSeenTick;
            public int StuckTicks;
            public bool StuckLogged;
        }

        private void Awake()
        {
            bootstrap = GetComponent<GameBootstrap>();
            if (bootstrap == null)
                throw new InvalidOperationException(
                    "MinionTowerDiagnosticDriver requires GameBootstrap on the same GameObject.");

            ConfigureBootstrapForDiagnosticClock();
            ConfigureDiagnosticWave();
            CacheKeyPoints();

            previousFixedDeltaTime = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 1f / TickRate;
        }

        private void FixedUpdate()
        {
            if (bootstrap == null || !bootstrap.IsInitialized)
                return;

            LogPreTick(bootstrap.Runtime.CurrentTick);
            bootstrap.Runtime.ExecuteAuthorityTick();
            LogCompletedTick(bootstrap.Runtime.CurrentTick - 1);
        }

        private void LogPreTick(int tick)
        {
            IReadOnlyList<UnitType> units = bootstrap.UnitWorld.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitType unit = units[i];
                if (unit == null || unit.UnitKind != UnitKind.Minion ||
                    unit.Intent.Kind != IntentKind.AttackTarget)
                    continue;

                UnitUid targetUid = unit.Intent.TargetUnit;
                AttackPlanStatus planStatus = unit.AttackHandler != null
                    ? unit.AttackHandler.GetAttackPlanStatus(targetUid)
                    : AttackPlanStatus.TargetInvalid;
                MovementTask task = unit.Locomotion != null
                    ? unit.Locomotion.CurrentTask
                    : MovementTask.None;
                string attackDetail =
                    BuildAttackStateDetail(
                        unit.AttackHandler);
                Debug.Log(
                    $"[MinionDiag][PreTick={tick}] Uid={FormatUid(unit.UnitUid)} " +
                    $"Plan={planStatus} IntentTarget={FormatUid(targetUid)} " +
                    $"Task={task.Purpose}/{task.State} Stop={task.StopDistance} " +
                    $"ActionRuntimes={unit.ActionRuntimes?.Count ?? 0} " +
                    $"Atk[{attackDetail}]");
            }
        }

        private static string BuildAttackStateDetail(
            AttackHandler attack)
        {
            if (attack == null)
            {
                return "no-handler";
            }
            FieldInfo stateField =
                typeof(AttackHandler).GetField(
                    "_state",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            if (stateField == null)
            {
                return "no-state";
            }
            object state = stateField.GetValue(attack);
            return
                $"Start={state.GetType().GetField("AttackStartLogicTick")?.GetValue(state)} " +
                $"Impact={state.GetType().GetField("ImpactLogicTick")?.GetValue(state)} " +
                $"Ready={state.GetType().GetField("NextAttackReadyLogicTick")?.GetValue(state)} " +
                $"Dur={state.GetType().GetField("ResolvedAttackDurationTicks")?.GetValue(state)} " +
                $"Windup={state.GetType().GetField("ResolvedWindupTicks")?.GetValue(state)} " +
                $"Committed={state.GetType().GetField("ImpactCommitted")?.GetValue(state)} " +
                $"LastHit={state.GetType().GetField("LastSuccessfulAttackLogicTick")?.GetValue(state)} " +
                $"Cycle={attack.IsAttackCycleActive} " +
                $"ReadyNow={attack.IsAttackReady()}";
        }

        private void OnDestroy()
        {
            Time.fixedDeltaTime = previousFixedDeltaTime;
            if (runtimeWaveConfig != null)
                Destroy(runtimeWaveConfig);
        }

        private void ConfigureBootstrapForDiagnosticClock()
        {
            FieldInfo driveField = typeof(GameBootstrap).GetField(
                "driveSimulationFromUnityUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (driveField == null)
                throw new MissingFieldException(
                    typeof(GameBootstrap).FullName,
                    "driveSimulationFromUnityUpdate");
            driveField.SetValue(bootstrap, false);
        }

        private void ConfigureDiagnosticWave()
        {
            FieldInfo configField = typeof(GameBootstrap).GetField(
                "minionWaveConfig",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (configField == null)
                throw new MissingFieldException(
                    typeof(GameBootstrap).FullName,
                    "minionWaveConfig");

            var source = configField.GetValue(bootstrap) as MinionWaveConfig;
            if (source == null)
                throw new InvalidOperationException(
                    "The long-run test scene requires a MinionWaveConfig.");

            runtimeWaveConfig = Instantiate(source);
            runtimeWaveConfig.name = source.name + "_DiagnosticRuntime";
            runtimeWaveConfig.hideFlags = HideFlags.DontSave;

            MinionWavePhase[] phases = ClonePhases(source.Phases);
            if (waveSize != DiagnosticMinionWaveSize.FullWave)
                RestrictComposition(phases, (int)waveSize);

            SetConfigField("firstWaveTick", 1);
            SetConfigField(
                "waveIntervalTicks",
                repeatWaves ? source.WaveIntervalTicks : IsolatedWaveIntervalTicks);
            SetConfigField("phases", phases);
            configField.SetValue(bootstrap, runtimeWaveConfig);
        }

        private void SetConfigField(string fieldName, object value)
        {
            FieldInfo field = typeof(MinionWaveConfig).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(
                    typeof(MinionWaveConfig).FullName,
                    fieldName);
            field.SetValue(runtimeWaveConfig, value);
        }

        private static void RestrictComposition(
            MinionWavePhase[] phases,
            int meleeCount)
        {
            for (int phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
            {
                MinionWaveComposition[] cycle =
                    phases[phaseIndex].CompositionCycle;
                for (int compositionIndex = 0;
                     compositionIndex < cycle.Length;
                     compositionIndex++)
                {
                    MinionWaveMember[] members =
                        cycle[compositionIndex].Members;
                    if (members == null || members.Length == 0)
                        throw new InvalidOperationException(
                            "Diagnostic wave requires a melee member.");

                    MinionWaveMember melee = members[0];
                    melee.Count = meleeCount;
                    melee.FirstSpawnOffsetTicks = 0;
                    melee.SpawnStepTicks = 24;
                    cycle[compositionIndex].Members =
                        new[] { melee };
                }
            }
        }

        private static MinionWavePhase[] ClonePhases(
            MinionWavePhase[] source)
        {
            var result = new MinionWavePhase[source.Length];
            for (int phaseIndex = 0; phaseIndex < source.Length; phaseIndex++)
            {
                MinionWaveComposition[] sourceCycle =
                    source[phaseIndex].CompositionCycle ??
                    Array.Empty<MinionWaveComposition>();
                var cycle = new MinionWaveComposition[sourceCycle.Length];
                for (int compositionIndex = 0;
                     compositionIndex < sourceCycle.Length;
                     compositionIndex++)
                {
                    MinionWaveMember[] sourceMembers =
                        sourceCycle[compositionIndex].Members ??
                        Array.Empty<MinionWaveMember>();
                    var members = new MinionWaveMember[sourceMembers.Length];
                    for (int memberIndex = 0;
                         memberIndex < sourceMembers.Length;
                         memberIndex++)
                    {
                        members[memberIndex] = sourceMembers[memberIndex];
                        MinionTeamPrototypeOverride[] overrides =
                            sourceMembers[memberIndex].TeamPrototypeOverrides;
                        members[memberIndex].TeamPrototypeOverrides =
                            overrides == null
                                ? Array.Empty<MinionTeamPrototypeOverride>()
                                : (MinionTeamPrototypeOverride[])overrides.Clone();
                    }
                    cycle[compositionIndex].Members = members;
                }
                result[phaseIndex] = new MinionWavePhase
                {
                    StartWaveIndex = source[phaseIndex].StartWaveIndex,
                    CompositionCycle = cycle,
                };
            }
            return result;
        }

        private void CacheKeyPoints()
        {
            string[] paths =
            {
                "Map/LandPath/BlueTeamFundation",
                "Map/LandPath/TopLandPointB",
                "Map/LandPath/TopLandPointM",
                "Map/LandPath/TopLandPointR",
                "Map/LandPath/MiddleLandPoint",
                "Map/LandPath/ButtomLandPointB",
                "Map/LandPath/ButtomLandPointM",
                "Map/LandPath/ButtomLandPointR",
                "Map/LandPath/RedTeamFundation",
            };

            // D-048 moved the map out of the scene into an Addressable client
            // view, so the scene no longer owns a "Map" root. The formal lane
            // key points still live on the deterministic logic map prefab,
            // whose transform hierarchy is unchanged.
            Transform mapRoot = null;
            GameObject sceneMap = GameObject.Find("Map");
            if (sceneMap != null)
            {
                mapRoot = sceneMap.transform;
            }
            else
            {
                GameObject mapPrefab =
                    UnityEditor.AssetDatabase
                        .LoadAssetAtPath<GameObject>(
                            "Assets/Config/Formal/Prefabs/Logic/Map/Map.prefab");
                if (mapPrefab != null)
                {
                    mapRoot = mapPrefab.transform;
                }
            }
            if (mapRoot == null)
            {
                throw new InvalidOperationException(
                    "Diagnostic key points require the scene Map root or the logic Map prefab.");
            }

            for (int i = 0; i < paths.Length; i++)
            {
                // The authored paths carry a scene-root "Map/" prefix that
                // Transform.Find cannot use when mapRoot is already the map
                // root (scene object or logic prefab). Strip it.
                string relativePath =
                    paths[i].StartsWith("Map/",
                        StringComparison.Ordinal)
                        ? paths[i].Substring(4)
                        : paths[i];
                Transform point =
                    mapRoot.Find(relativePath);
                if (point == null)
                    throw new InvalidOperationException(
                        $"Diagnostic key point '{paths[i]}' was not found.");
                keyPoints.Add(point);
            }
        }

        private void LogCompletedTick(int completedTick)
        {
            IReadOnlyList<UnitType> minions =
                bootstrap.UnitWorld.GetUnitsByKind(UnitKind.Minion);
            if (logEverySimulationTick)
            {
                Debug.Log(
                    $"[MinionDiag][Frame={Time.frameCount}][Tick={completedTick}] " +
                    $"Wave={waveSize}; ActiveMinions={minions.Count}");
            }

            for (int i = 0; i < minions.Count; i++)
                LogUnit(completedTick, minions[i]);

            staleUids.Clear();
            foreach (KeyValuePair<UnitUid, UnitTrace> pair in traces)
            {
                if (pair.Value.LastSeenTick != completedTick)
                    staleUids.Add(pair.Key);
            }
            staleUids.Sort();
            for (int i = 0; i < staleUids.Count; i++)
            {
                UnitUid uid = staleUids[i];
                Debug.Log(
                    $"[MinionDiag][Despawn][Tick={completedTick}] Uid={FormatUid(uid)}");
                traces.Remove(uid);
            }
        }

        private void LogUnit(int completedTick, UnitType unit)
        {
            if (unit == null || unit.PhysicsEntity == null)
                return;

            fp2 position = unit.PhysicsEntity.Transform2D.Position;
            if (!traces.TryGetValue(unit.UnitUid, out UnitTrace trace))
            {
                trace = new UnitTrace
                {
                    PreviousPosition = position,
                    RouteKind = RouteKind.None,
                    LastSeenTick = completedTick,
                };
                traces.Add(unit.UnitUid, trace);
                Debug.Log(
                    $"[MinionDiag][Spawn][Tick={completedTick}] " +
                    $"Uid={FormatUid(unit.UnitUid)} Team={unit.TeamId.Value} " +
                    $"Prototype={unit.UnitPrototypeId} Pos={Format(position)}");
            }

            fp2 step = position - trace.PreviousPosition;
            bool reversed =
                fpmath.lengthsq(step) > fp.zero &&
                fpmath.lengthsq(trace.PreviousStep) > fp.zero &&
                fpmath.dot(step, trace.PreviousStep) < fp.zero;

            LocomotionAgentSnapshot locomotion = default;
            unit.Locomotion?.Capture(ref locomotion);
            PathFollowerState follower = locomotion.FollowerState;
            MovementHandler movement = unit.MovementHandler;
            fp2 desiredDirection = movement?.TargetDirection ?? fp2.zero;
            fp2 actualVelocity = movement?.Velocity ?? fp2.zero;
            string nearestPoint = GetNearestKeyPoint(position, out fp nearestDistance);

            // Stuck detection: an alive minion that neither moves nor holds
            // an active movement task for a long window. Emits one detailed
            // diagnostic line per stuck unit (attack cycle, range, target
            // distance, capability/CC state) to localize the stall.
            if (unit.LifeState == LifeState.Alive &&
                fpmath.lengthsq(step) <=
                    (fp)0.1m)
            {
                trace.StuckTicks++;
            }
            else
            {
                trace.StuckTicks = 0;
                trace.StuckLogged = false;
            }
            if (trace.StuckTicks >= 15 &&
                !trace.StuckLogged)
            {
                trace.StuckLogged = true;
                LogStuckDiagnostic(
                    completedTick,
                    unit,
                    position);
            }

            if (logEverySimulationTick)
            {
                Debug.Log(
                    $"[MinionDiag][Unit][Tick={completedTick}] " +
                    $"Uid={FormatUid(unit.UnitUid)} Team={unit.TeamId.Value} " +
                    $"Life={unit.LifeState} Pos={Format(position)} Step={Format(step)} " +
                    $"Desired={Format(desiredDirection)} Velocity={Format(actualVelocity)} " +
                    $"Task={locomotion.Task.Purpose}/{locomotion.Task.State} " +
                    $"Route={locomotion.Route.Kind} Cursor={follower.PathCursor}/" +
                    $"{(follower.PathCellIndices?.Length ?? 0)} " +
                    $"Repath={locomotion.Route.NeedRepath} Reverse={reversed} " +
                    $"Nearest={nearestPoint}:{nearestDistance}");
            }

            string currentPoint = nearestDistance <= (fp)KeyPointRadius
                ? nearestPoint
                : null;
            if (currentPoint != trace.KeyPointName)
            {
                if (trace.KeyPointName != null)
                {
                    Debug.Log(
                        $"[MinionDiag][KeyPointExit][Tick={completedTick}] " +
                        $"Uid={FormatUid(unit.UnitUid)} Point={trace.KeyPointName} " +
                        $"{BuildPathDetails(unit, locomotion)}");
                }
                if (currentPoint != null)
                {
                    Debug.Log(
                        $"[MinionDiag][KeyPointEnter][Tick={completedTick}] " +
                        $"Uid={FormatUid(unit.UnitUid)} Point={currentPoint} " +
                        $"Distance={nearestDistance} {BuildPathDetails(unit, locomotion)}");
                }
                trace.KeyPointName = currentPoint;
            }

            if (reversed ||
                trace.RouteKind != locomotion.Route.Kind ||
                trace.PathCursor != follower.PathCursor ||
                trace.NeedRepath != locomotion.Route.NeedRepath)
            {
                Debug.Log(
                    $"[MinionDiag][RouteChange][Tick={completedTick}] " +
                    $"Uid={FormatUid(unit.UnitUid)} Reverse={reversed} " +
                    $"Route={trace.RouteKind}-\u003e{locomotion.Route.Kind} " +
                    $"Cursor={trace.PathCursor}-\u003e{follower.PathCursor} " +
                    $"Repath={trace.NeedRepath}-\u003e{locomotion.Route.NeedRepath} " +
                    $"{BuildPathDetails(unit, locomotion)}");
            }

            trace.PreviousPosition = position;
            if (fpmath.lengthsq(step) > fp.zero)
                trace.PreviousStep = step;
            trace.RouteKind = locomotion.Route.Kind;
            trace.PathCursor = follower.PathCursor;
            trace.NeedRepath = locomotion.Route.NeedRepath;
            trace.LastSeenTick = completedTick;
        }

        private void LogStuckDiagnostic(
            int tick,
            UnitType unit,
            fp2 position)
        {
            AttackHandler attack = unit.AttackHandler;
            string targetDistance = "none";
            if (unit.Intent.TargetUnit.IsValid() &&
                bootstrap.UnitWorld.TryGetUnit(
                    unit.Intent.TargetUnit,
                    out UnitType target) &&
                target?.PhysicsEntity != null)
            {
                targetDistance = Format(
                    fpmath.length(
                        position -
                        target.PhysicsEntity
                            .Transform2D.Position));
            }
            string stateDetail = "no-attack-handler";
            if (attack != null)
            {
                FieldInfo stateField =
                    typeof(AttackHandler).GetField(
                        "_state",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                if (stateField != null)
                {
                    object state =
                        stateField.GetValue(attack);
                    stateDetail =
                        $"Start={state.GetType().GetField("AttackStartLogicTick")?.GetValue(state)} " +
                        $"Impact={state.GetType().GetField("ImpactLogicTick")?.GetValue(state)} " +
                        $"Ready={state.GetType().GetField("NextAttackReadyLogicTick")?.GetValue(state)} " +
                        $"Dur={state.GetType().GetField("ResolvedAttackDurationTicks")?.GetValue(state)} " +
                        $"Windup={state.GetType().GetField("ResolvedWindupTicks")?.GetValue(state)} " +
                        $"Committed={state.GetType().GetField("ImpactCommitted")?.GetValue(state)} " +
                        $"LastHit={state.GetType().GetField("LastSuccessfulAttackLogicTick")?.GetValue(state)}";
                }
            }
            string message =
                $"[MinionDiag][Stuck] Tick={tick} " +
                $"Uid={FormatUid(unit.UnitUid)} " +
                $"Team={unit.TeamId.Value} " +
                $"Pos={Format(position)} " +
                $"Intent={unit.Intent.Kind} " +
                $"IntentTarget={FormatUid(unit.Intent.TargetUnit)} " +
                $"AttackCycle={attack?.IsAttackCycleActive} " +
                $"AttackTarget={FormatUid(attack?.CurrentTargetUid ?? default)} " +
                $"AttackReady={attack?.IsAttackReady()} " +
                $"AtkSpeed={unit.StatHandler?.GetStat(StatId.AttackSpeed)} " +
                $"Range={attack?.CurrentAttackRange} " +
                $"TargetDist={targetDistance} " +
                $"CanMove={unit.CapabilityState.CanMove} " +
                $"CanAttack={unit.CapabilityState.CanAttack} " +
                $"CCMove={unit.CrowdControl?.IsBlocked(UnitActionBlockMask.VoluntaryMove)} " +
                $"CCAttack={unit.CrowdControl?.IsBlocked(UnitActionBlockMask.VoluntaryAttack)} " +
                $"ActionRuntimes={unit.ActionRuntimes?.Count} " +
                $"State[{stateDetail}]";
            Debug.LogWarning(message);
            try
            {
                string dir =
                    System.IO.Path.Combine(
                        Application.dataPath,
                        "..",
                        "Logs");
                System.IO.Directory.CreateDirectory(
                    dir);
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(
                        dir,
                        "minion_stuck.log"),
                    message + "\n");
            }
            catch (System.Exception)
            {
                // File logging is diagnostic-only.
            }
        }

        private string BuildPathDetails(
            UnitType unit,
            in LocomotionAgentSnapshot locomotion)
        {
            var result = new StringBuilder(160);
            PathFollowerState follower = locomotion.FollowerState;
            result.Append("Path=[");
            int[] cells = follower.PathCellIndices;
            if (cells != null)
            {
                int start = Math.Max(0, follower.PathCursor - 1);
                int end = Math.Min(cells.Length - 1, follower.PathCursor + 1);
                for (int i = start; i <= end; i++)
                {
                    if (i > start) result.Append(',');
                    int cell = cells[i];
                    int x = cell % unit.Locomotion.Grid.Width;
                    int y = cell / unit.Locomotion.Grid.Width;
                    result.Append(i == follower.PathCursor ? '*' : ' ');
                    result.Append(cell);
                    result.Append(':');
                    result.Append(Format(unit.Locomotion.Grid.CellToWorld(x, y)));
                }
            }
            result.Append("] Target=");
            result.Append(
                locomotion.Task.Target.Position.HasValue
                    ? Format(locomotion.Task.Target.Position.Value)
                    : locomotion.Task.Target.TargetUid.HasValue
                        ? FormatUid(locomotion.Task.Target.TargetUid.Value)
                        : "None");
            result.Append(" AllowRVO=");
            result.Append(locomotion.Task.AllowRVO);
            return result.ToString();
        }

        private string GetNearestKeyPoint(
            fp2 position,
            out fp nearestDistance)
        {
            string nearestName = "None";
            fp nearestDistanceSq = (fp)int.MaxValue;
            for (int i = 0; i < keyPoints.Count; i++)
            {
                Vector3 world = keyPoints[i].position;
                fp2 keyPosition = new fp2((fp)world.x, (fp)world.z);
                fp distanceSq = fpmath.lengthsq(position - keyPosition);
                if (distanceSq >= nearestDistanceSq)
                    continue;
                nearestDistanceSq = distanceSq;
                nearestName = keyPoints[i].name;
            }

            nearestDistance = fpmath.sqrt(nearestDistanceSq);
            return nearestName;
        }

        private static string Format(fp2 value) =>
            $"({value.x},{value.y})";

        private static string FormatUid(UnitUid uid) =>
            $"{uid.SpawnLogicTick}/{uid.RuntimeEntityPrefabId}/" +
            uid.SpawnSequenceInTick;
    }
}
