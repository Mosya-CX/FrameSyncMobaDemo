using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class GameSceneFirstWavePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ResetPersistentSession();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Destroying the listening NGO server during cleanup logs expected
            // transport shutdown errors; swallow them so they never leak.
            LogAssert.ignoreFailingMessages = true;
            ResetPersistentSession();
            yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator GameScene_FirstWaveUsesFlowFieldsAndMoves()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSessionContext.ServerBootstrapSceneName,
                LoadSceneMode.Single);
            yield return WaitForScene(
                GameSessionContext.LobbySceneName);
            yield return SceneManager.LoadSceneAsync(
                GameSessionContext.GameSceneName,
                LoadSceneMode.Single);
            yield return WaitForScene(
                GameSessionContext.GameSceneName);

            GameBootstrap bootstrap =
                Object.FindObjectOfType<GameBootstrap>();
            Assert.NotNull(
                bootstrap,
                "GameScene must contain GameBootstrap.");
            Assert.IsTrue(
                bootstrap.IsInitialized,
                "The production bootstrap must initialize from its serialized assets.");

            SetPrivateBoolean(
                bootstrap,
                "driveSimulationFromUnityUpdate",
                false);
            LocalNgoEndpointDriver endpoint =
                Object.FindObjectOfType<
                    LocalNgoEndpointDriver>();
            if (endpoint != null)
                endpoint.enabled = false;

            GameStartConfig fixtureConfig =
                InvokeFixtureConfig(bootstrap);
            GameBootstrapPayload payload =
                bootstrap.BuildAuthoritativeBootstrapPayload(
                    fixtureConfig);
            bootstrap.ApplyGameBootstrapPayload(payload);

            Assert.IsTrue(
                bootstrap.IsMatchReady,
                "The production bootstrap payload must start the fixture match.");
            Assert.NotNull(bootstrap.UnitWorld.FlowFieldRegistry);
            Assert.That(
                bootstrap.UnitWorld.FlowFieldRegistry.Count,
                Is.EqualTo(6),
                "Both teams need Small, Medium and Large flow fields.");

            ExecuteUntilTick(bootstrap, 973);
            yield return null;

            MinionSystem minionSystem =
                bootstrap.UnitWorld.MinionSystem;
            Assert.NotNull(minionSystem);
            Assert.That(
                minionSystem.ManagedMinionUids.Count,
                Is.EqualTo(18),
                "Three lanes and two teams must spawn 2 melee + 1 ranged minion.");
            AssertWaveTicketsUseMapMinionSpawns(
                minionSystem);

            var positions =
                new Dictionary<UnitUid, fp2>();
            int flowFieldRouteCount = 0;
            int validCombatDecisionCount = 0;
            int validReturnDecisionCount = 0;
            int meleeCount = 0;
            int rangedCount = 0;
            for (int i = 0;
                 i < minionSystem.ManagedMinionUids.Count;
                 i++)
            {
                UnitUid uid =
                    minionSystem.ManagedMinionUids[i];
                Assert.IsTrue(
                    bootstrap.UnitWorld.TryGetUnit(
                        uid,
                        out UnitType unit));
                Assert.That(
                    unit.MovementHandler.Entity,
                    Is.SameAs(unit.PhysicsEntity),
                    $"Minion {uid} MovementHandler must own the movement write path to its PhysicsEntity2D.");
                if (unit.Locomotion.Route.Kind ==
                    RouteKind.FlowField)
                {
                    flowFieldRouteCount++;
                }
                if (unit.Intent.Kind == IntentKind.LaneAdvance)
                {
                    Assert.That(
                        unit.Locomotion.Route.Kind,
                        Is.EqualTo(RouteKind.FlowField),
                        $"Lane-advancing minion {uid} must use its baked flow field.");
                }
                else if (unit.Intent.Kind == IntentKind.AttackTarget)
                {
                    Assert.That(
                        bootstrap.UnitWorld.TryGetUnit(
                            unit.Intent.TargetUnit,
                            out UnitType target),
                        Is.True,
                        $"Minion {uid} must resolve its detected target.");
                    Assert.That(target.TeamId, Is.Not.EqualTo(unit.TeamId));
                    Assert.That(target.TeamId, Is.Not.EqualTo(TeamId.Neutral));
                    Assert.That(target.LifeState, Is.EqualTo(LifeState.Alive));
                    Assert.That(target.CapabilityState.IsTargetable, Is.True);
                    validCombatDecisionCount++;
                }
                else if (unit.Intent.Kind == IntentKind.MoveToPosition)
                {
                    Assert.That(
                        bootstrap.UnitWorld.TryGetAIController(
                            uid,
                            out UnitAIController aiController),
                        Is.True);
                    Assert.That(aiController, Is.TypeOf<MinionAIController>());
                    Assert.That(
                        ((MinionAIController)aiController).AIState,
                        Is.EqualTo(MinionAIState.ReturnToLane));
                    Assert.That(
                        unit.Locomotion.CurrentTask.State,
                        Is.EqualTo(MovementTaskState.Active));
                    validReturnDecisionCount++;
                }
                else
                {
                    Assert.Fail(
                        $"Minion [{uid.SpawnLogicTick}:{uid.RuntimeEntityPrefabId}:{uid.SpawnSequenceInTick}] " +
                        $"has Intent={unit.Intent.Kind}, Route={unit.Locomotion.Route.Kind}, " +
                        $"Task={unit.Locomotion.CurrentTask.State}, Position={unit.PhysicsEntity.Transform2D.Position}.");
                }
                if (unit.UnitSubKindId ==
                    NonHeroUnitSubKindId.MeleeMinion)
                    meleeCount++;
                else if (unit.UnitSubKindId ==
                         NonHeroUnitSubKindId.RangedMinion)
                    rangedCount++;
                Assert.That(
                    unit.StatHandler.GetStat(StatId.MoveSpeed),
                    Is.EqualTo((fp)325),
                    $"Minion {uid} must retain the authored raw MoveSpeed stat.");
                Assert.That(
                    unit.MovementHandler.LogicMoveSpeed,
                    Is.EqualTo(
                        unit.StatHandler.GetStat(StatId.MoveSpeed) *
                        bootstrap.UnitWorld.MoveSpeedToLogicVelocityScale),
                    $"Minion {uid} must apply the global 0.01 scale only at movement use.");
                positions.Add(
                    uid,
                    unit.PhysicsEntity.Transform2D.Position);
            }
            Assert.That(meleeCount, Is.EqualTo(12));
            Assert.That(rangedCount, Is.EqualTo(6));
            Assert.That(
                flowFieldRouteCount,
                Is.GreaterThan(0),
                "At least one non-engaged first-wave minion must still be advancing through a flow field.");
            Assert.That(
                flowFieldRouteCount + validCombatDecisionCount +
                validReturnDecisionCount,
                Is.EqualTo(18),
                "Every first-wave minion must have a valid lane-advance or enemy-engagement decision.");

            ExecuteUntilTick(bootstrap, 1003);
            yield return null;

            int movedCount = 0;
            int engagedWhileStationaryCount = 0;
            foreach (KeyValuePair<UnitUid, fp2> entry
                     in positions)
            {
                Assert.IsTrue(
                    bootstrap.UnitWorld.TryGetUnit(
                        entry.Key,
                        out UnitType unit));
                fp distanceSq = fpmath.lengthsq(
                    unit.PhysicsEntity.Transform2D.Position -
                    entry.Value);
                if (distanceSq > (fp)0.000001m)
                {
                    movedCount++;
                }
                else if (unit.Intent.Kind ==
                         IntentKind.AttackTarget &&
                         bootstrap.UnitWorld.TryGetUnit(
                             unit.Intent.TargetUnit,
                             out UnitType target) &&
                         target.TeamId != unit.TeamId &&
                         target.TeamId != TeamId.Neutral &&
                         target.LifeState == LifeState.Alive &&
                         target.CapabilityState.IsTargetable)
                {
                    engagedWhileStationaryCount++;
                }
                Assert.That(
                    distanceSq,
                    Is.LessThanOrEqualTo((fp)12.25m),
                    $"Minion [{entry.Key.SpawnLogicTick}:{entry.Key.RuntimeEntityPrefabId}:" +
                    $"{entry.Key.SpawnSequenceInTick}] moved from {entry.Value} to " +
                    $"{unit.PhysicsEntity.Transform2D.Position} farther than 3.5 units in one " +
                    $"simulated second; Intent={unit.Intent.Kind}, Route={unit.Locomotion.Route.Kind}, " +
                    $"Velocity={unit.MovementHandler.Velocity}.");
            }

            Assert.That(
                movedCount + engagedWhileStationaryCount,
                Is.EqualTo(18),
                "Every first-wave minion must either make logical progress or remain stationary only for a legal enemy engagement.");

            VerifyTowerCombatAndMatchClosure(
                bootstrap,
                minionSystem);
        }

        private static void AssertWaveTicketsUseMapMinionSpawns(
            MinionSystem minionSystem)
        {
            var counts = new Dictionary<int, int>();
            for (int i = 0;
                 i < minionSystem.PendingTickets.Count;
                 i++)
            {
                MinionTicket ticket =
                    minionSystem.PendingTickets[i];
                Assert.That(
                    minionSystem.TryGetLane(
                        ticket.LaneId,
                        out LaneRuntimeData lane),
                    Is.True);
                bool foundSpawn = false;
                for (int j = 0;
                     j < lane.TeamSpawns.Length;
                     j++)
                {
                    LaneTeamSpawnData spawn =
                        lane.TeamSpawns[j];
                    if (spawn.TeamId != ticket.TeamId)
                        continue;
                    Assert.That(
                        ticket.SpawnPosition,
                        Is.EqualTo(spawn.Position),
                        "Every ticket must use its current map-authored MinionSpawn position.");
                    Assert.That(
                        ticket.SpawnForward,
                        Is.EqualTo(spawn.Forward),
                        "Every ticket must use its current map-authored MinionSpawn forward.");
                    foundSpawn = true;
                    break;
                }
                Assert.That(
                    foundSpawn,
                    Is.True,
                    $"Lane {ticket.LaneId} has no spawn for Team {ticket.TeamId}.");
                int key = ticket.LaneId * 10 +
                    ticket.TeamId.Value;
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
            }
            for (int team = 1; team <= 2; team++)
            for (int lane = 1; lane <= 3; lane++)
            {
                int key = lane * 10 + team;
                counts.TryGetValue(key, out int count);
                Assert.That(
                    count,
                    Is.EqualTo(3),
                    $"Lane {lane}, Team {team} must receive exactly one 2+1 wave.");
            }

            for (int team = 1; team <= 2; team++)
            {
                for (int lane = 1; lane <= 3; lane++)
                {
                    int firstTick = int.MaxValue;
                    int secondTick = int.MaxValue;
                    int thirdTick = int.MaxValue;
                    int firstPrototype = 0;
                    int secondPrototype = 0;
                    int thirdPrototype = 0;
                    for (int i = 0;
                         i < minionSystem.PendingTickets.Count;
                         i++)
                    {
                        MinionTicket ticket =
                            minionSystem.PendingTickets[i];
                        if (ticket.TeamId != new TeamId((byte)team) ||
                            ticket.LaneId != lane)
                            continue;
                        if (ticket.SpawnLogicTick < firstTick)
                        {
                            thirdTick = secondTick;
                            thirdPrototype = secondPrototype;
                            secondTick = firstTick;
                            secondPrototype = firstPrototype;
                            firstTick = ticket.SpawnLogicTick;
                            firstPrototype = ticket.UnitPrototypeId;
                        }
                        else if (ticket.SpawnLogicTick < secondTick)
                        {
                            thirdTick = secondTick;
                            thirdPrototype = secondPrototype;
                            secondTick = ticket.SpawnLogicTick;
                            secondPrototype = ticket.UnitPrototypeId;
                        }
                        else if (ticket.SpawnLogicTick < thirdTick)
                        {
                            thirdTick = ticket.SpawnLogicTick;
                            thirdPrototype = ticket.UnitPrototypeId;
                        }
                    }
                    Assert.That(secondTick - firstTick, Is.EqualTo(24));
                    Assert.That(thirdTick - secondTick, Is.EqualTo(24));
                    Assert.That(
                        secondPrototype,
                        Is.EqualTo(firstPrototype),
                        "The first two sequential spawns must use the melee prototype.");
                    Assert.That(
                        thirdPrototype,
                        Is.Not.EqualTo(firstPrototype),
                        "The third sequential spawn must use the ranged prototype.");
                }
            }
        }

        private static void VerifyTowerCombatAndMatchClosure(
            GameBootstrap bootstrap,
            MinionSystem minionSystem)
        {
            UnitType blueTower = FindUnitByPrototype(
                bootstrap.UnitWorld,
                3001);
            UnitType redMinion = null;
            for (int i = 0;
                 i < minionSystem.ManagedMinionUids.Count;
                 i++)
            {
                UnitUid uid = minionSystem.ManagedMinionUids[i];
                if (uid.IsValid() &&
                    bootstrap.UnitWorld.TryGetUnit(
                        uid,
                        out UnitType candidate) &&
                    candidate.TeamId == new TeamId(2))
                {
                    redMinion = candidate;
                    break;
                }
            }
            Assert.NotNull(redMinion);

            fp2 towerPosition = blueTower.PhysicsEntity
                .Transform2D.Position;
            redMinion.PhysicsEntity.SetLogicPose(
                towerPosition + new fp2(fp.one, fp.zero),
                new fp2(-fp.one, fp.zero));
            redMinion.MovementHandler.SetMoveSpeed(fp.zero);
            fp healthBefore = redMinion.StatHandler.CurrentHealth;

            ExecuteUntilTick(
                bootstrap,
                bootstrap.Runtime.CurrentTick + 2);
            Assert.That(
                blueTower.Intent.Kind,
                Is.EqualTo(IntentKind.AttackTarget));
            Assert.That(
                blueTower.Intent.TargetUnit,
                Is.EqualTo(redMinion.UnitUid));
            Assert.That(
                blueTower.Intent.AllowChase,
                Is.False,
                "Tower AttackOrder must never create a chase route.");

            int deathDeadline =
                bootstrap.Runtime.CurrentTick + 300;
            while (redMinion.LifeState != LifeState.Dead &&
                   bootstrap.Runtime.CurrentTick < deathDeadline)
                bootstrap.Runtime.ExecuteAuthorityTick();

            Assert.That(
                redMinion.StatHandler.CurrentHealth,
                Is.LessThan(healthBefore),
                "Tower attacks must settle through CombatSystem.");
            Assert.That(
                redMinion.LifeState,
                Is.EqualTo(LifeState.Dead),
                "The actual tower must complete formal minion death.");
            Assert.That(
                bootstrap.UnitWorld.TryGetAIController(
                    redMinion.UnitUid,
                    out _),
                Is.False,
                "Formal minion death must unregister its AI controller.");
            Assert.That(
                blueTower.PhysicsEntity.Transform2D.Position,
                Is.EqualTo(towerPosition),
                "A tower must not move while attacking.");

            UnitUid redBaseUid =
                bootstrap.Runtime.MatchRule.RedBaseUnitUid;
            Assert.That(redBaseUid.IsValid(), Is.True);
            Assert.That(
                bootstrap.UnitWorld.TryGetUnit(
                    redBaseUid,
                    out UnitType redBase),
                Is.True);
            redBase.StatHandler.SetCurrentHealth(fp.one);

            int settlementTick = bootstrap.Runtime.CurrentTick;
            var tickController =
                new SimulationTickContextController();
            tickController.BeginTick(
                settlementTick,
                ExecutionMode.ServerAuthority);
            try
            {
                CombatSystem combat = bootstrap.UnitWorld.CombatSystem;
                combat.BeginTick();
                Assert.That(
                    combat.SubmitDamage(
                        new DamageRequest
                        {
                            Header = CombatRequestHeader.Create(
                                blueTower.UnitUid,
                                redBaseUid,
                                CombatSourceType.Attack,
                                CombatBuiltinSourceId.BasicAttack,
                                CombatBuiltinRecipeId.BasicAttackDamage),
                            DamageType = DamageType.Physical,
                            BaseDamage = (fp)100,
                        }),
                    Is.True);
                combat.SettleActiveRequests();
                combat.EndTick();
                Assert.That(redBase.LifeState, Is.EqualTo(LifeState.Dead));
                Assert.That(
                    bootstrap.Runtime.MatchRule
                        .EvaluateAuthorityConfirmedTick(
                            settlementTick,
                            bootstrap.UnitWorld),
                    Is.True);
            }
            finally
            {
                tickController.EndTick();
            }
            Assert.That(
                bootstrap.Runtime.MatchRule.WinningTeamId,
                Is.EqualTo(new TeamId(1)));
            Assert.That(
                bootstrap.Runtime.MatchRule.EndReason,
                Is.EqualTo(MatchEndReason.BaseDestroyed));
        }

        private static UnitType FindUnitByPrototype(
            UnitWorld world,
            int prototypeId)
        {
            IReadOnlyList<UnitType> units = world.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
                if (units[i].UnitPrototypeId == prototypeId)
                    return units[i];
            Assert.Fail($"Unit prototype {prototypeId} is missing.");
            return null;
        }

        private static void ExecuteUntilTick(
            GameBootstrap bootstrap,
            int exclusiveTick)
        {
            while (bootstrap.Runtime.CurrentTick <
                   exclusiveTick)
            {
                bootstrap.Runtime.ExecuteAuthorityTick();
            }
        }

        private static GameStartConfig
            InvokeFixtureConfig(
                GameBootstrap bootstrap)
        {
            MethodInfo method =
                typeof(GameBootstrap).GetMethod(
                    "CreateFixtureGameStartConfig",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (GameStartConfig)method.Invoke(
                bootstrap,
                null);
        }

        private static void SetPrivateBoolean(
            GameBootstrap bootstrap,
            string fieldName,
            bool value)
        {
            FieldInfo field =
                typeof(GameBootstrap).GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(bootstrap, value);
        }

        private static IEnumerator WaitForScene(
            string sceneName,
            int maxFrames = 600)
        {
            int guard = 0;
            while (SceneManager.GetActiveScene().name !=
                   sceneName &&
                   guard++ < maxFrames)
                yield return null;
        }

        private static void ResetPersistentSession()
        {
            NetworkManager[] managers =
                Object.FindObjectsOfType<NetworkManager>(true);
            for (int i = 0;
                 i < managers.Length;
                 i++)
            {
                NetworkManager manager = managers[i];
                if (manager.IsListening)
                    manager.Shutdown();
                Object.Destroy(manager.gameObject);
            }
            var uiManagers =
                Object.FindObjectsOfType<UIManager>(true);
            for (int i = 0;
                 i < uiManagers.Length;
                 i++)
                Object.Destroy(uiManagers[i].gameObject);
            var eventSystems =
                Object.FindObjectsOfType<
                    UnityEngine.EventSystems.EventSystem>(true);
            for (int i = 0;
                 i < eventSystems.Length;
                 i++)
                Object.Destroy(eventSystems[i].gameObject);
            GameSessionContext.ResetSession();
        }
    }
}
