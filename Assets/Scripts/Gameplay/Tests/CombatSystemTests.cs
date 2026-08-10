using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class CombatSystemTests
    {
        private UnitWorld _world;
        private UnitPrototype _prototype;
        private CombatSystem _combat;
        private SimulationTickContextController _controller;
        private UnitDisposePolicyTable _disposePolicies;

        [SetUp]
        public void SetUp()
        {
            _controller = new SimulationTickContextController();
            _world = new UnitWorld
            {
                StatDefinitionTable = CreateCombatStatTable()
            };
            _disposePolicies =
                UnityEngine.ScriptableObject
                    .CreateInstance<UnitDisposePolicyTable>();
            _disposePolicies.Entries.Add(
                new UnitDisposePolicyEntry
                {
                    Id = 0,
                    Kind = UnitDisposePolicyKind.KeepAlive,
                });
            _disposePolicies.Entries.Add(
                new UnitDisposePolicyEntry
                {
                    Id = 2,
                    Kind = UnitDisposePolicyKind.Pool,
                    DeathPresentationTicks = 2,
                });
            _world.DisposePolicyTable = _disposePolicies;
            _world.RespawnTimer = new RespawnTimer(_world);
            _prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "TestUnit",
                RuntimeEntityPrefabId = 100,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = CreateCombatPreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
            };
            _combat = new CombatSystem(_world, 300, 60);
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller.IsTickActive)
                _controller.EndTick();
            UnityEngine.Object.DestroyImmediate(
                _disposePolicies);
        }

        private void BeginTick(int tick)
        {
            _controller.BeginTick(tick, ExecutionMode.ServerAuthority);
        }

        [Test]
        public void SubmitDamage_ValidRequest_ReducesHealth()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)100));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp finalHealth = target.StatHandler.CurrentHealth;
            Assert.Less(finalHealth, initialHealth);
            Assert.Greater(finalHealth, fp.zero);
        }

        [Test]
        public void NaturalRegen_AppliesPerInterval_ForHealthAndCastResource()
        {
            var world = new UnitWorld
            {
                TickRate = 30,
                StatDefinitionTable = CreateRegenStatTable(),
            };
            world.DisposePolicyTable = _disposePolicies;
            world.RespawnTimer = new RespawnTimer(world);
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = 99,
                Name = "RegenUnit",
                RuntimeEntityPrefabId = 99,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = CreateRegenPreset(),
            };
            var combat = new CombatSystem(world, 300, 60);
            combat.NaturalRegenIntervalSeconds = (fp)5;
            Unit unit = world.SpawnUnit(
                prototype,
                TeamId.Neutral,
                1,
                0m,
                0m);

            fp maxHealth =
                unit.StatHandler.GetStat(StatId.MaxHealth);
            fp maxResource =
                unit.StatHandler.GetStat(
                    StatId.MaxCastResource);
            unit.StatHandler.SetCurrentHealth(
                maxHealth - (fp)50);
            unit.StatHandler.SetCurrentCastResource(
                maxResource - (fp)50);

            for (int tick = 1;
                 tick <= 30;
                 tick++)
            {
                _controller.BeginTick(
                    tick,
                    ExecutionMode.ServerAuthority);
                combat.BeginTick();
                combat.SettleActiveRequests();
                combat.EndTick();
                _controller.EndTick();
            }

            // HealthRegeneration / CastResourceRegeneration = 5 per 5s ->
            // +1 per second at 30 ticks/s.
            Assert.That(
                (double)(unit.StatHandler.CurrentHealth -
                         (maxHealth - (fp)50)),
                Is.InRange(0.9, 1.1));
            Assert.That(
                (double)(unit.StatHandler
                         .CurrentCastResource -
                         (maxResource - (fp)50)),
                Is.InRange(0.9, 1.1));
        }

        [Test]
        public void DamageFormula_ArmorReducesDamage()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            // Target has 30 armor → reduction = 100/(100+30) ≈ 0.769
            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)100));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp damageTaken = initialHealth - target.StatHandler.CurrentHealth;

            // With 30 armor, damage should be ~77
            Assert.Greater(damageTaken, 50m);
            Assert.Less(damageTaken, 100m);
        }

        [Test]
        public void ZeroArmor_FullDamageApplied()
        {
            BeginTick(1);
            var proto = CreateZeroArmorProto();
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(proto, TeamId.Neutral, 1, 0m, 0m);

            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)100));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp damageTaken = initialHealth - target.StatHandler.CurrentHealth;
            Assert.AreEqual((fp)100m, damageTaken);
        }

        [Test]
        public void CombatModifiers_ApplyOutgoingAndIncomingFinalPatches()
        {
            BeginTick(1);
            Unit attacker = _world.SpawnUnit(
                _prototype,
                TeamId.Neutral,
                1,
                0m,
                0m);
            Unit target = _world.SpawnUnit(
                CreateZeroArmorProto(),
                TeamId.Neutral,
                1,
                0m,
                0m);
            var match = new CombatModifierMatch(
                SourceTypeMask.Attack,
                CombatBuiltinSourceId.BasicAttack,
                CombatBuiltinRecipeId.BasicAttackDamage,
                DamageTypeMask.True);
            attacker.CombatModifiers.Attach(
                new CombatModifierRecord
                {
                    Id = CombatModifierId.Create(1, "Outgoing"),
                    Domain = CombatDomain.Damage,
                    Scope = CombatModifierScope.Outgoing,
                    Match = match,
                    ValuePatches = new[]
                    {
                        new CombatFormulaPatch(
                            CombatFormulaSlot.FinalValue,
                            CombatModifierOperation.Multiply,
                            new CombatOperand((fp)2)),
                    },
                });
            target.CombatModifiers.Attach(
                new CombatModifierRecord
                {
                    Id = CombatModifierId.Create(1, "Incoming"),
                    Domain = CombatDomain.Damage,
                    Scope = CombatModifierScope.Incoming,
                    Match = match,
                    ValuePatches = new[]
                    {
                        new CombatFormulaPatch(
                            CombatFormulaSlot.FinalValue,
                            CombatModifierOperation.Add,
                            new CombatOperand((fp)10)),
                    },
                });
            fp initialHealth =
                target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(
                UnitTestFactory.CreateDamageRequest(
                    attacker.UnitUid,
                    target.UnitUid,
                    (fp)100,
                    DamageType.True,
                    CombatSourceType.Attack,
                    CombatBuiltinSourceId.BasicAttack,
                    CombatBuiltinRecipeId.BasicAttackDamage));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.AreEqual(
                (fp)220,
                initialHealth -
                target.StatHandler.CurrentHealth);
        }

        [Test]
        public void FatalDamage_CompletesFormalDeathSettlement()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.AreEqual(LifeState.Alive, target.LifeState);

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)5000));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.AreEqual(LifeState.Dead, target.LifeState);
        }

        [Test]
        public void FatalDamage_NonHeroIsDisposedAfterCombatConsumersFinish()
        {
            BeginTick(1);
            Unit attacker = _world.SpawnUnit(
                _prototype,
                TeamId.Neutral,
                1,
                0m,
                0m);
            var minionPrototype = new UnitPrototype
            {
                UnitPrototypeId = 2,
                Name = "TestMinion",
                RuntimeEntityPrefabId = 101,
                UnitKind = UnitKind.Minion,
                UnitDisposePolicyId = 2,
                PoolConfig = UnitPoolConfig.Default,
                BaseStats = CreateCombatPreset(),
            };
            Unit target = _world.SpawnUnit(
                minionPrototype,
                TeamId.Neutral,
                1,
                0m,
                0m);
            UnitUid targetUid = target.UnitUid;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid,
                targetUid,
                (fp)5000));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.IsTrue(_world.TryGetUnit(targetUid, out Unit dead));
            Assert.AreEqual(LifeState.Dead, dead.LifeState);

            _world.ProcessPostCombatDeathDisposals(
                _combat.DeathResults);

            RespawnTimerSnapshot captured = default;
            _world.RespawnTimer.Capture(ref captured);
            Assert.AreEqual(1, captured.DisposalEntries.Count);
            Assert.AreEqual(targetUid, captured.DisposalEntries[0].UnitUid);
            Assert.AreEqual(1, captured.DisposalEntries[0].DeathLogicTick);
            Assert.AreEqual(3, captured.DisposalEntries[0].DisposeLogicTick);

            var restoredTimer = new RespawnTimer(_world);
            restoredTimer.Restore(in captured);
            RollbackContext rollbackContext = default;
            restoredTimer.Resolve(in rollbackContext);
            restoredTimer.Rebuild(in rollbackContext);
            RespawnTimerSnapshot roundTrip = default;
            restoredTimer.Capture(ref roundTrip);
            Assert.AreEqual(
                captured.DisposalEntries[0].UnitUid,
                roundTrip.DisposalEntries[0].UnitUid);
            Assert.AreEqual(
                captured.DisposalEntries[0].DisposeLogicTick,
                roundTrip.DisposalEntries[0].DisposeLogicTick);
            _world.RespawnTimer = restoredTimer;

            _world.RespawnTimer.Tick(2);
            Assert.IsTrue(_world.TryGetUnit(targetUid, out _));

            _world.RespawnTimer.Tick(3);
            Assert.IsFalse(_world.TryGetUnit(targetUid, out _));
            Assert.AreEqual(
                1,
                _world.PoolRegistry.GetAvailableCount(
                    minionPrototype.RuntimeEntityPrefabId));

            var secondPrototype = new UnitPrototype
            {
                UnitPrototypeId = 3,
                Name = "SamePrefabMinion",
                RuntimeEntityPrefabId =
                    minionPrototype.RuntimeEntityPrefabId,
                UnitKind = UnitKind.Minion,
                UnitDisposePolicyId = 2,
                PoolConfig = UnitPoolConfig.Default,
                BaseStats = CreateCombatPreset(),
            };
            Unit reused = _world.SpawnUnit(
                secondPrototype,
                TeamId.Neutral,
                3,
                0m,
                0m);
            Assert.AreSame(dead, reused);
            Assert.AreEqual(3, reused.UnitPrototypeId);
            Assert.AreEqual(
                0,
                _world.PoolRegistry.GetAvailableCount(
                    minionPrototype.RuntimeEntityPrefabId));
        }

        [Test]
        public void FormalDeathResult_UsesFatalSourceAndUidSortedAssistants()
        {
            BeginTick(1);
            TeamId attackers = new TeamId(1);
            TeamId defenders = new TeamId(2);
            Unit smallerAssistant = _world.SpawnUnit(
                _prototype, attackers, 1, 0m, 0m);
            Unit largerAssistant = _world.SpawnUnit(
                _prototype, attackers, 1, 0m, 0m);
            Unit killer = _world.SpawnUnit(
                _prototype, attackers, 1, 0m, 0m);
            Unit victim = _world.SpawnUnit(
                _prototype, defenders, 1, 0m, 0m);

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                largerAssistant.UnitUid,
                victim.UnitUid,
                (fp)50,
                DamageType.True));
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                smallerAssistant.UnitUid,
                victim.UnitUid,
                (fp)350,
                DamageType.True));
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                killer.UnitUid,
                victim.UnitUid,
                (fp)100,
                DamageType.True));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.AreEqual(1, _combat.DeathResults.Count);
            DeathResult result = _combat.DeathResults[0];
            Assert.AreEqual(killer.UnitUid, result.KillerHeroUid);
            CollectionAssert.AreEqual(
                new[] { smallerAssistant.UnitUid, largerAssistant.UnitUid },
                result.AssistantHeroUids);
        }

        [Test]
        public void DeadUnit_DoesNotTakeDamage()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            // Transition target to Dead via proper lifecycle
            _world.RequestEnterDying(target);
            _world.ConfirmUnitDeath(target);
            Assert.AreEqual(LifeState.Dead, target.LifeState);

            fp healthBefore = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)100));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.AreEqual(healthBefore, target.StatHandler.CurrentHealth);
        }

        [Test]
        public void InvalidRequest_IsIgnored()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            _combat.BeginTick();
            _combat.SubmitDamage(DamageRequest.None);
            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.Pass();
        }

        [Test]
        public void CalculatePhysicalDamage_NegativeArmor_ClampedToZero()
        {
            fp damage = CombatSystem.CalculateResistedDamage(100m, -10m);
            Assert.AreEqual((fp)100m, damage);
        }

        [Test]
        public void CalculatePhysicalDamage_HighArmor_ReducedHeavily()
        {
            fp damage = CombatSystem.CalculateResistedDamage(100m, 200m);
            // 100 * (100 / 300) ≈ 33.3
            Assert.Greater(damage, 10m);
            Assert.Less(damage, 50m);
        }

        [Test]
        public void BeginTick_ClearsActiveQueue()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)100));

            // BeginTick again clears without settling
            _combat.BeginTick();
            fp healthBefore = target.StatHandler.CurrentHealth;

            _combat.SettleActiveRequests();
            _combat.EndTick();

            Assert.AreEqual(healthBefore, target.StatHandler.CurrentHealth);
        }

        private static StatDefinitionTable CreateCombatStatTable()
        {
            var table = new StatDefinitionTable();
            table.Add(new StatDefinition
            {
                Id = StatId.MaxHealth,
                DebugName = "HP",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.AttackDamage,
                DebugName = "AD",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.Armor,
                DebugName = "Armor",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = false,
                HasMinValue = true,
                MinValue = 0m,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.AttackSpeed,
                DebugName = "AS",
                DefaultBaseValue = 0.625m,
                SupportsLevelGrowth = false,
            });
            return table;
        }

        private static StatPreset CreateCombatPreset()
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = 500m,
                GrowthValue = 50m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = 100m,
                GrowthValue = 5m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.Armor,
                BaseValue = 30m,
                GrowthValue = 0m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackSpeed,
                BaseValue = 0.625m,
                GrowthValue = 0m,
            });
            return preset;
        }

        private static StatDefinitionTable CreateRegenStatTable()
        {
            var table = CreateCombatStatTable();
            table.Add(new StatDefinition
            {
                Id = StatId.HealthRegeneration,
                DebugName = "HP5",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.MaxCastResource,
                DebugName = "MP",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            table.Add(new StatDefinition
            {
                Id = StatId.CastResourceRegeneration,
                DebugName = "MP5",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            return table;
        }

        private static StatPreset CreateRegenPreset()
        {
            var preset = CreateCombatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.HealthRegeneration,
                BaseValue = 5m,
                GrowthValue = 0m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxCastResource,
                BaseValue = 200m,
                GrowthValue = 0m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.CastResourceRegeneration,
                BaseValue = 5m,
                GrowthValue = 0m,
            });
            return preset;
        }

        private UnitPrototype CreateZeroArmorProto()
        {
            var proto = new UnitPrototype
            {
                UnitPrototypeId = 2,
                Name = "ZeroArmor",
                RuntimeEntityPrefabId = 101,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = new StatPreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
            };
            proto.BaseStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = 500m,
                GrowthValue = 50m,
            });
            proto.BaseStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.Armor,
                BaseValue = 0m,
                GrowthValue = 0m,
            });
            proto.BaseStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = 100m,
                GrowthValue = 5m,
            });
            proto.BaseStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackSpeed,
                BaseValue = 0.625m,
                GrowthValue = 0m,
            });
            return proto;
        }
    }
}
