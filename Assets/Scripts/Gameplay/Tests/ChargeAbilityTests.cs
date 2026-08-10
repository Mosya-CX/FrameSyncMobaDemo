using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Verifies the charge (hold-release) ability slices: ChargeStageDef
    /// writes the deterministic charge ratio and consumes an active Toggle
    /// slot; ChargeProjectileStageDef interpolates damage/range/AD ratio and
    /// spawns a projectile carrying a per-instance on-hit damage override
    /// (Ability v15.2 3.10, 7.2).
    /// </summary>
    [TestFixture]
    public sealed class ChargeAbilityTests
    {
        private SimulationTickContextController controller;
        private UnitWorld world;
        private UnitType caster;
        private ProjectileWorld projectileWorld;
        private int nextTick = 21;

        [SetUp]
        public void SetUp()
        {
            controller = new SimulationTickContextController();
            controller.BeginTick(
                20,
                ExecutionMode.ServerAuthority);
            world = new UnitWorld();
            UnitPrototype prototype = CreatePrototype();
            caster = UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(1),
                20,
                fp.zero,
                fp.zero);
            UnitTestFactory.AddProjectilePrefab(
                world,
                2001);
            projectileWorld = new ProjectileWorld
            {
                UnitWorld = world,
                PhysicsWorld = world.PhysicsWorld,
                PrefabTable = world.GlobalPrefabTable,
                DefRegistry = new ProjectileDefRegistry(),
            };
            projectileWorld.DefRegistry.Register(
                new ProjectileDef
                {
                    DefId = 2001,
                    RuntimeEntityPrefabId = 2001,
                    Speed = (fp)1,
                    MaxLifetimeTicks = 100,
                    HitRadius = (fp)1 / (fp)10,
                    TargetFilter =
                        ProjectileTargetFilter.DefaultEnemy,
                    HitPolicy = new ProjectileHitPolicy
                    {
                        Enabled = false,
                    },
                });
            world.ProjectileWorld = projectileWorld;
        }

        [TearDown]
        public void TearDown()
        {
            controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void ChargeStage_RatioIncreasesWithElapsedTicks()
        {
            InstallQ(InstallChargeModel(
                maxChargeTicks: 45,
                consumeToggleSlot: byte.MaxValue));

            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                FocusSignal()));
            AbilityRuntime q = GetRuntime(0);
            Assert.IsNotNull(q.ActiveSession);
            Assert.AreEqual(
                fp.zero,
                ReadRatio(q));

            for (int i = 0; i < 20; i++)
            {
                AdvanceTick();
            }
            Assert.AreEqual(
                (fp)20 / (fp)45,
                ReadRatio(q));

            for (int i = 0; i < 30; i++)
            {
                AdvanceTick();
            }
            Assert.AreEqual(
                fp.one,
                ReadRatio(q));
        }

        [Test]
        public void ChargeStage_SelfSlowAppliesWhileChargingAndRemovesOnRelease()
        {
            var hold = new ChargeStageDef
            {
                ChargeRatioBlackboardKeyId = 1,
                MaxChargeTicks = 45,
                ConsumeToggleSlot = byte.MaxValue,
                SelfSlowModifierPercent = (fp)0.2m,
                SlowModifierBlackboardKeyId = 3,
            };
            InstallQ(hold);
            fp baseSpeed =
                caster.StatHandler.GetStat(
                    StatId.MoveSpeed);

            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                FocusSignal()));
            fp slowedSpeed =
                caster.StatHandler.GetStat(
                    StatId.MoveSpeed);
            Assert.AreEqual(
                (double)(baseSpeed * (fp)0.8m),
                (double)slowedSpeed,
                0.01,
                "Charging must apply the self slow.");

            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                CommitSignal(
                    AimSnapshot.ForDirection(
                        new fp2(fp.one, fp.zero)),
                    0)));
            Assert.AreEqual(
                (double)baseSpeed,
                (double)caster.StatHandler.GetStat(
                    StatId.MoveSpeed),
                0.01,
                "Releasing must remove the self slow.");
        }

        [Test]
        public void ChargeStage_TimeoutCancelsAndRefundsHalfCost()
        {
            var hold = new ChargeStageDef
            {
                ChargeRatioBlackboardKeyId = 1,
                MaxChargeTicks = 45,
                ConsumeToggleSlot = byte.MaxValue,
            };
            InstallQWithCost(hold);
            AbilityRuntime q = GetRuntime(0);
            fp initialMana =
                caster.StatHandler.CurrentCastResource;

            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                FocusSignal()));
            Assert.AreEqual(
                (fp)initialMana - (fp)50,
                caster.StatHandler.CurrentCastResource);
            int cooldownBefore =
                q.CooldownEndsAtTick;

            // Hold lasts 120 ticks; exceed it to trigger the timeout.
            for (int i = 0; i < 122; i++)
            {
                AdvanceTick();
            }

            Assert.IsNull(q.ActiveSession);
            Assert.AreEqual(
                (double)((fp)initialMana - (fp)25),
                (double)caster.StatHandler.CurrentCastResource,
                0.01,
                "Timeout must refund half of the 50 mana already paid.");
            Assert.AreEqual(
                cooldownBefore,
                q.CooldownEndsAtTick,
                "Timeout cancel must not start cooldown.");
        }

        [Test]
        public void ChargeStage_ConsumesActiveToggleAndStartsCooldown()
        {
            InstallW();
            InstallQ(InstallChargeModel(
                maxChargeTicks: 45,
                consumeToggleSlot: 1,
                consumeToggleCooldownTicks: 1200));

            // Toggle W on.
            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                CommitSignal(AimSnapshot.None, 1)));
            AbilityRuntime w = GetRuntime(1);
            Assert.IsNotNull(w.ActiveSession);

            // Start charging Q; the active W must be consumed.
            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                FocusSignal()));
            Assert.IsNull(w.ActiveSession);
            Assert.AreEqual(
                20 + 1200,
                w.CooldownEndsAtTick);

            AbilityRuntime q = GetRuntime(0);
            Assert.IsTrue(q.ActiveSession.Blackboard.TryGet(
                new AbilityBlackboardKey<fp>(2),
                out fp empowered));
            Assert.AreEqual(fp.one, empowered);
        }

        [Test]
        public void ChargeProjectileStage_InterpolatesDamageRangeAndOverride()
        {
            var model = new HoldReleaseCastModelDef
            {
                Hold = new CastStage
                {
                    StageKey = 1,
                    DurationTicks = 120,
                    Def = new ChargeStageDef
                    {
                        ChargeRatioBlackboardKeyId = 1,
                        MaxChargeTicks = 45,
                        ConsumeToggleSlot = byte.MaxValue,
                    },
                },
                Release = new CastStage
                {
                    StageKey = 2,
                    DurationTicks = 0,
                    Def = new ChargeProjectileStageDef
                    {
                        ProjectileDefId = 2001,
                        SpawnOffsetDistance = (fp)1,
                        ChargeRatioBlackboardKeyId = 1,
                        MinBaseDamageByLevel =
                            new AbilityLevelValue(
                                new[] { (fp)50 }),
                        MaxBaseDamageByLevel =
                            new AbilityLevelValue(
                                new[] { (fp)100 }),
                        MinAttackDamageRatioByLevel =
                            new AbilityLevelValue(
                                new[] { (fp)0.5m }),
                        MaxAttackDamageRatioByLevel =
                            new AbilityLevelValue(
                                new[] { (fp)1m }),
                        MinRange = (fp)5,
                        MaxRange = (fp)10,
                        FalloffPerHitPercent = (fp)0.15m,
                        MinDamageRatio = (fp)0.33m,
                        RecipeId = 20001,
                    },
                },
            };
            InstallQ(model);

            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                FocusSignal()));
            for (int i = 0; i < 20; i++)
            {
                AdvanceTick();
            }

            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                CommitSignal(
                    AimSnapshot.ForDirection(
                        new fp2((fp)2, fp.zero)),
                    0)));
            Assert.AreEqual(1, projectileWorld.PendingCount);
            projectileWorld.CommitSpawns();
            Assert.AreEqual(1, projectileWorld.Count);

            ProjectileRuntime runtime =
                projectileWorld.GetAllOrdered()[0];
            Assert.AreEqual(
                new fp2(fp.one, fp.zero),
                runtime.Position);
            // ratio = 20/45; range = 5 + 5 * 20/45 = 7.222... -> 7 ticks at speed 1.
            Assert.AreEqual(7, runtime.RemainingLifetimeTicks);

            Assert.IsNotNull(runtime.OnHitDamageOverride);
            Assert.AreEqual(1, runtime.OnHitDamageOverride.Length);
            ProjectileOnHitDamage effect =
                runtime.OnHitDamageOverride[0];
            // base = 50 + 50 * 20/45 = 72.222...
            Assert.AreEqual(
                (double)((fp)50 + (fp)50 * (fp)20 / (fp)45),
                (double)effect.Amount,
                0.0001);
            // ad ratio = 0.5 + 0.5 * 20/45 = 0.7222...
            Assert.AreEqual(
                (double)((fp)0.5m + (fp)0.5m * (fp)20 / (fp)45),
                (double)effect.DamageRatio,
                0.0001);
            Assert.AreEqual(
                (fp)0.15m,
                effect.FalloffPerHitPercent);
        }

        [Test]
        public void ChargeProjectile_SnapshotRoundTrip_PreservesOverride()
        {
            InstallQ(InstallChargeModel(
                maxChargeTicks: 45,
                consumeToggleSlot: byte.MaxValue));
            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                FocusSignal()));
            for (int i = 0; i < 10; i++)
            {
                AdvanceTick();
            }
            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                CommitSignal(
                    AimSnapshot.ForDirection(
                        new fp2(fp.one, fp.zero)),
                    0)));
            projectileWorld.CommitSpawns();

            var worldSnapshot =
                new ProjectileWorldSnapshot();
            projectileWorld.Capture(ref worldSnapshot);
            Assert.AreEqual(
                1,
                worldSnapshot.ActiveProjectiles.Length);
            Assert.IsNotNull(
                worldSnapshot.ActiveProjectiles[0]
                    .OnHitDamageOverride);
            fp amount =
                worldSnapshot.ActiveProjectiles[0]
                    .OnHitDamageOverride[0].Amount;

            projectileWorld.Restore(worldSnapshot);
            ProjectileRuntime restored =
                projectileWorld.GetAllOrdered()[0];
            Assert.AreEqual(
                amount,
                restored.OnHitDamageOverride[0].Amount);
        }

        private ChargeStageDef InstallChargeModel(
            int maxChargeTicks,
            byte consumeToggleSlot,
            int consumeToggleCooldownTicks = 0)
        {
            return new ChargeStageDef
            {
                ChargeRatioBlackboardKeyId = 1,
                MaxChargeTicks = maxChargeTicks,
                ConsumeToggleSlot = consumeToggleSlot,
                ConsumeToggleCooldownTicks =
                    consumeToggleCooldownTicks,
                EmpoweredBlackboardKeyId =
                    consumeToggleSlot != byte.MaxValue
                        ? 2
                        : 0,
            };
        }

        private void InstallQ(ChargeStageDef hold)
        {
            var model = new HoldReleaseCastModelDef
            {
                Hold = new CastStage
                {
                    StageKey = 1,
                    DurationTicks = 120,
                    Def = hold,
                },
                Release = new CastStage
                {
                    StageKey = 2,
                    DurationTicks = 0,
                    Def = new ChargeProjectileStageDef
                    {
                        ProjectileDefId = 2001,
                        SpawnOffsetDistance = (fp)1,
                        ChargeRatioBlackboardKeyId = 1,
                        MinBaseDamageByLevel =
                            new AbilityLevelValue(
                                new[] { (fp)50 }),
                        MaxBaseDamageByLevel =
                            new AbilityLevelValue(
                                new[] { (fp)100 }),
                        MinRange = (fp)5,
                        MaxRange = (fp)10,
                        RecipeId = 20001,
                    },
                },
            };
            InstallQ(model);
        }

        private void InstallQ(CastModelDef model)
        {
            Install(
                new AbilityDef
                {
                    AbilityId = 10011,
                    Name = "TestCharge",
                    CastModel = model,
                    AimKind = AimKind.Direction,
                    CastRange = (fp)16,
                    CostPlan = default,
                    CooldownByLevel = default,
                },
                0);
        }

        private void InstallQWithCost(ChargeStageDef hold)
        {
            var model = new HoldReleaseCastModelDef
            {
                Hold = new CastStage
                {
                    StageKey = 1,
                    DurationTicks = 120,
                    Def = hold,
                },
                Release = new CastStage
                {
                    StageKey = 2,
                    DurationTicks = 0,
                    Def = new ChargeProjectileStageDef
                    {
                        ProjectileDefId = 2001,
                        SpawnOffsetDistance = (fp)1,
                        ChargeRatioBlackboardKeyId = 1,
                        MinBaseDamageByLevel =
                            new AbilityLevelValue(
                                new[] { (fp)50 }),
                        MaxBaseDamageByLevel =
                            new AbilityLevelValue(
                                new[] { (fp)100 }),
                        MinRange = (fp)5,
                        MaxRange = (fp)10,
                        RecipeId = 20001,
                    },
                },
                HoldTimeoutPolicy =
                    HoldTimeoutPolicy.Cancel,
                RefundCostPercentOnTimeout =
                    (fp)0.5m,
            };
            Install(
                new AbilityDef
                {
                    AbilityId = 10011,
                    Name = "TestChargeCost",
                    CastModel = model,
                    AimKind = AimKind.Direction,
                    CastRange = (fp)16,
                    CostPlan = new AbilityCostPlan(
                        new AbilityLevelValue(
                            new[] { (fp)50 }),
                        default,
                        AbilityCostTiming.OnSessionStart),
                    CooldownByLevel = default,
                },
                0);
        }

        private void InstallW()
        {
            var model = new ToggleCastModelDef
            {
                Active = new CastStage
                {
                    StageKey = 1,
                    DurationTicks = 360000,
                    Def = new DelayStageDef(),
                },
                ResourcePerTick = fp.zero,
            };
            Install(
                new AbilityDef
                {
                    AbilityId = 10012,
                    Name = "TestToggle",
                    CastModel = model,
                    AimKind = AimKind.None,
                    CostPlan = default,
                    CooldownByLevel = default,
                },
                1);
        }

        private void Install(
            AbilityDef definition,
            byte slot)
        {
            var runtime = new AbilityRuntime
            {
                Definition = definition,
                Level = 1,
            };
            var slotRuntime = new AbilitySlotRuntime
            {
                SlotIndex = slot,
                ActiveAbilityId = definition.AbilityId,
                AllocatedPoints = 1,
            };
            slotRuntime.AddAbility(runtime);
            caster.AbilityHandler.AddSlot(slotRuntime);
        }

        private AbilityRuntime GetRuntime(byte slot) =>
            caster.AbilityHandler.GetActiveRuntime(slot);

        private fp ReadRatio(AbilityRuntime runtime)
        {
            Assert.IsTrue(
                runtime.ActiveSession.Blackboard.TryGet(
                    new AbilityBlackboardKey<fp>(1),
                    out fp ratio));
            return ratio;
        }

        private void AdvanceTick()
        {
            controller.EndTick();
            controller.BeginTick(
                nextTick++,
                ExecutionMode.ServerAuthority);
            caster.AbilityHandler.TickUpdate();
        }

        private static AbilitySignal FocusSignal() =>
            new AbilitySignal
            {
                Slot = 0,
                Verb = AbilitySignalVerb.Focus,
            };

        private static AbilitySignal CommitSignal(
            AimSnapshot aim,
            byte slot) =>
            new AbilitySignal
            {
                Slot = slot,
                Verb = AbilitySignalVerb.Commit,
                Aim = aim,
            };

        private static UnitPrototype CreatePrototype()
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = (fp)1000,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxCastResource,
                BaseValue = (fp)500,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = (fp)100,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MoveSpeed,
                BaseValue = (fp)330,
            });
            return new UnitPrototype
            {
                UnitPrototypeId = 1,
                RuntimeEntityPrefabId = 1001,
                UnitKind = UnitKind.Hero,
                BaseStats = preset,
                Loadout = HandlerLoadout.DefaultHero,
            };
        }
    }
}
