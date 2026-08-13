using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEditor;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class SunderedSkyEquipmentTests
    {
        private SimulationTickContextController controller;
        private UnitWorld world;
        private UnitPrototype prototype;
        private Unit attacker;
        private Unit targetA;
        private Unit targetB;
        private CombatSystem combat;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.Clear();
            controller = new SimulationTickContextController();
            controller.BeginTick(
                10,
                ExecutionMode.ServerAuthority);
            EquipmentDatabase database =
                LoadDatabase();
            var buffRegistry =
                new BuffDefinitionRegistry();
            buffRegistry.Register(
                LoadOverhealBuff());
            world = new UnitWorld
            {
                StatDefinitionTable =
                    CreateStatTable(),
                AttackSequenceResetIntervalTicks = 3,
                EquipmentDatabase = database,
                BuffDefinitions = buffRegistry,
                RandomService =
                    new DeterministicRandomService(42u),
            };
            prototype = CreatePrototype();
            attacker = world.SpawnUnit(
                prototype,
                new TeamId(1),
                10,
                fp.zero,
                fp.zero);
            targetA = world.SpawnUnit(
                prototype,
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            targetB = world.SpawnUnit(
                prototype,
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            attacker.EquipmentHandler
                .DefinitionDatabase = database;
            attacker.BuffHandler
                .DefinitionRegistry = buffRegistry;
            targetA.BuffHandler
                .DefinitionRegistry = buffRegistry;
            targetB.BuffHandler
                .DefinitionRegistry = buffRegistry;
            combat = new CombatSystem(world, 0, 0);
            world.CombatSystem = combat;
            CombatEvents.TryResolveUnit = uid =>
                world.TryGetUnit(uid, out Unit resolved)
                    ? resolved
                    : null;
            Assert.That(
                database.TryGetDefinition(
                    31011,
                    out EquipmentDefinition sundered),
                Is.True);
            Assert.That(
                attacker.EquipmentHandler.Add(
                    sundered,
                    0),
                Is.True);
            combat.BeginTick();
        }

        [TearDown]
        public void TearDown()
        {
            if (controller.IsTickActive)
            {
                controller.EndTick();
            }
            CombatEvents.Clear();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void
            Catalog_ContainsSunderedSkyTreeWithStatsAndRecipes()
        {
            EquipmentDatabase database =
                LoadDatabase();
            Assert.That(database.Count, Is.EqualTo(11));
            Assert.That(
                database.TryGetDefinition(
                    31006,
                    out EquipmentDefinition sword),
                Is.True);
            Assert.That(
                database.TryGetDefinition(
                    31007,
                    out EquipmentDefinition ruby),
                Is.True);
            Assert.That(
                database.TryGetDefinition(
                    31008,
                    out EquipmentDefinition mote),
                Is.True);
            Assert.That(
                database.TryGetDefinition(
                    31009,
                    out EquipmentDefinition tunneler),
                Is.True);
            Assert.That(
                database.TryGetDefinition(
                    31010,
                    out EquipmentDefinition warhammer),
                Is.True);
            Assert.That(
                database.TryGetDefinition(
                    31011,
                    out EquipmentDefinition sundered),
                Is.True);

            Assert.That(sword.Value, Is.EqualTo(350));
            Assert.That(ruby.Value, Is.EqualTo(400));
            Assert.That(mote.Value, Is.EqualTo(250));
            Assert.That(tunneler.Value, Is.EqualTo(1150));
            Assert.That(warhammer.Value, Is.EqualTo(1050));
            Assert.That(sundered.Value, Is.EqualTo(3100));

            Assert.That(
                tunneler.Recipe.Components.Length,
                Is.EqualTo(2));
            Assert.That(
                tunneler.Recipe.Components[0].Item.Id,
                Is.EqualTo(31006));
            Assert.That(
                tunneler.Recipe.Components[1].Item.Id,
                Is.EqualTo(31007));
            Assert.That(
                warhammer.Recipe.Components.Length,
                Is.EqualTo(2));
            Assert.That(
                warhammer.Recipe.Components[0].Item.Id,
                Is.EqualTo(31006));
            Assert.That(
                warhammer.Recipe.Components[0].Count,
                Is.EqualTo(2));
            Assert.That(
                warhammer.Recipe.Components[1].Item.Id,
                Is.EqualTo(31008));
            Assert.That(
                sundered.Recipe.Components.Length,
                Is.EqualTo(2));
            Assert.That(
                sundered.Recipe.Components[0].Item.Id,
                Is.EqualTo(31009));
            Assert.That(
                sundered.Recipe.Components[1].Item.Id,
                Is.EqualTo(31010));
            Assert.That(
                sundered.Effects.Length,
                Is.EqualTo(1));
            Assert.That(
                sundered.Effects[0].Modules[0],
                Is.TypeOf<
                    LightshieldStrikeEquipmentModule>());
        }

        [Test]
        public void
            EmpoweredStrike_Deals160PercentCritical_AndStartsCooldown()
        {
            bool wasCritical = false;
            CombatEvents.OnDamageDealt += data =>
            {
                if (data.Source.SourceType ==
                        CombatSourceType.Attack &&
                    data.RecipeId ==
                        CombatBuiltinRecipeId
                            .EmpoweredAttackDamage)
                {
                    wasCritical = data.IsCritical;
                }
            };

            fp damage = DriveAttack(targetA);

            // Base AD 100 + Sundered Sky bonus AD 45 = 145 total;
            // empowered strike = 145 * 1.6 * crit(2) = 464 (fp-approximate).
            AssertFpClose(
                damage,
                (fp)464m);
            Assert.That(wasCritical, Is.True);
            Assert.That(
                targetA.HasTag(
                    CooldownKey(attacker)),
                Is.True);
        }

        [Test]
        public void
            PerTargetCooldown_SecondHitOnSameHeroIsNormal()
        {
            fp first = DriveAttack(targetA);
            fp second = DriveAttack(targetA);

            AssertFpClose(first, (fp)464m);
            AssertFpClose(second, (fp)145m);
            Assert.That(
                targetA.HasTag(
                    CooldownKey(attacker)),
                Is.True);
        }

        [Test]
        public void
            DifferentEnemyHero_StillTriggersEmpoweredStrike()
        {
            fp first = DriveAttack(targetA);
            fp second = DriveAttack(targetB);

            AssertFpClose(first, (fp)464m);
            AssertFpClose(second, (fp)464m);
            Assert.That(
                targetB.HasTag(
                    CooldownKey(attacker)),
                Is.True);
            Assert.That(
                targetA.HasTag(
                    CooldownKey(attacker)),
                Is.True);
        }

        [Test]
        public void
            HealRestoresMissingHealth_AndOverhealBecomesTempMaxHealth()
        {
            // The equipped item adds +45 bonus Attack Damage, but the heal
            // must scale with BASE Attack Damage only (100), not the total
            // (145): 100*0.9 + missing*0.04.
            Assert.That(
                attacker.StatHandler.GetBaseStat(
                    StatId.AttackDamage),
                Is.EqualTo((fp)100));
            Assert.That(
                attacker.StatHandler.GetBonusStat(
                    StatId.AttackDamage),
                Is.EqualTo((fp)45));
            Assert.That(
                attacker.StatHandler.GetStat(
                    StatId.MaxHealth),
                Is.EqualTo((fp)950),
                "Base 500 + Sundered Sky bonus 450 Max Health.");

            // MaxHealth is 950: missing 750 health at current 200.
            // heal = 100*0.9 + 750*0.04 = 120, fully applied, no overheal.
            attacker.StatHandler.SetCurrentHealth(
                (fp)200);
            DriveAttack(targetA);
            AssertFpClose(
                attacker.StatHandler.CurrentHealth,
                (fp)320m);
            Assert.That(
                attacker.BuffHandler.TryGetRuntime(
                    new BuffConfigId(31911),
                    out _),
                Is.False);

            // Missing only 50 health at current 900:
            // heal = 100*0.9 + 50*0.04 = 92; 50 is applied (capped at 950)
            // and 42 converts to temporary bonus Max Health.
            attacker.StatHandler.SetCurrentHealth(
                (fp)900);
            DriveAttack(targetB);
            AssertFpClose(
                attacker.StatHandler.CurrentHealth,
                (fp)950m);
            Assert.That(
                attacker.BuffHandler.TryGetRuntime(
                    new BuffConfigId(31911),
                    out _),
                Is.True);

            fp maxBefore = attacker.StatHandler
                .GetStat(StatId.MaxHealth);
            attacker.BuffHandler.Advance();
            fp maxAfter = attacker.StatHandler
                .GetStat(StatId.MaxHealth);
            AssertFpClose(
                maxAfter,
                maxBefore + (fp)42m);
        }

        [Test]
        public void
            RangedStrike_HealsWithRangedBaseAdRatio()
        {
            // Ranged owners heal 45% base AD instead of 90%.
            attacker.StatHandler.AddModifier(
                StatId.AttackRange,
                StatModifierOperation.FlatAdd,
                (fp)375);
            attacker.StatHandler.SetCurrentHealth(
                (fp)200);

            SubmitEmpoweredAttack(
                targetA,
                tick: 11);

            // MaxHealth 950: missing 750.
            // heal = 100*0.45 + 750*0.04 = 45 + 30 = 75 -> current 275.
            AssertFpClose(
                attacker.StatHandler.CurrentHealth,
                (fp)275m);
            Assert.That(
                targetA.HasTag(
                    CooldownKey(attacker)),
                Is.True);
        }

        [Test]
        public void
            MeleeWithProjectile_StillHealsWithMeleeRatio()
        {
            // Aatrox-like melee: AttackRange 200 but AttackHandler carries a
            // projectile id. The heal must still use the melee 90% ratio.
            attacker.AttackHandler.ProjectileDefId =
                101;
            attacker.StatHandler.SetCurrentHealth(
                (fp)200);

            SubmitEmpoweredAttack(
                targetA,
                tick: 11);

            // MaxHealth 950: missing 750.
            // heal = 100*0.9 + 750*0.04 = 120 -> current 320.
            AssertFpClose(
                attacker.StatHandler.CurrentHealth,
                (fp)320m);
        }

        private void SubmitEmpoweredAttack(
            Unit target,
            int tick)
        {
            if (controller.IsTickActive)
            {
                controller.EndTick();
            }
            controller.BeginTick(
                tick,
                ExecutionMode.ServerAuthority);
            combat.BeginTick();
            combat.SubmitDamage(
                UnitTestFactory.CreateDamageRequest(
                    attacker.UnitUid,
                    target.UnitUid,
                    (fp)145,
                    DamageType.Physical,
                    CombatSourceType.Attack,
                    CombatBuiltinSourceId.BasicAttack,
                    CombatBuiltinRecipeId
                        .EmpoweredAttackDamage));
            combat.SettleActiveRequests();
            combat.EndTick();
        }

        private static void AssertFpClose(
            fp actual,
            fp expected) =>
            AssertFpClose(
                actual,
                expected,
                (fp)0.01m);

        private static void AssertFpClose(
            fp actual,
            fp expected,
            fp tolerance)
        {
            Assert.That(
                fpmath.abs(actual - expected) <=
                    tolerance,
                Is.True,
                $"Expected {expected} but was {actual}.");
        }

        [Test]
        public void
            FinishedItem_IsUnique_SecondPurchaseRejected()
        {
            var shop = new EquipmentShopRuntime();
            shop.Initialize(
                1,
                LoadDatabase(),
                (fp)7 / (fp)10,
                world);
            shop.GetOrCreateTrader(
                0,
                attacker.UnitUid);

            // The Sundered Sky is already equipped (SetUp); a second copy
            // must be rejected by the default finished-item uniqueness rule.
            Assert.That(
                shop.TryBuildPurchasePlan(
                    0,
                    31011,
                    100000,
                    attacker.EquipmentHandler,
                    out _,
                    out EquipmentShopFailureReason
                        failure),
                Is.False);
            Assert.That(
                failure,
                Is.EqualTo(
                    EquipmentShopFailureReason
                        .DuplicateFinishedItem));
        }

        [Test]
        public void
            MaxHealthItem_EquipAndUnequip_SyncCurrentHealth()
        {
            Assert.That(
                attacker.StatHandler.GetStat(
                    StatId.MaxHealth),
                Is.EqualTo((fp)950));
            Assert.That(
                attacker.StatHandler.CurrentHealth,
                Is.EqualTo((fp)950),
                "Equipping +450 Max Health must also raise current health.");

            Assert.That(
                attacker.EquipmentHandler.Remove(0),
                Is.True);
            Assert.That(
                attacker.StatHandler.GetStat(
                    StatId.MaxHealth),
                Is.EqualTo((fp)500));
            Assert.That(
                attacker.StatHandler.CurrentHealth,
                Is.EqualTo((fp)500),
                "Unequipping -450 Max Health must also lower current health.");

            // Re-equip, then test the partial-health case: the same delta
            // applies without clamping (700 - 450 = 250).
            Assert.That(
                LoadDatabase().TryGetDefinition(
                    31011,
                    out EquipmentDefinition sundered),
                Is.True);
            Assert.That(
                attacker.EquipmentHandler.Add(
                    sundered,
                    0),
                Is.True);
            Assert.That(
                attacker.StatHandler.GetStat(
                    StatId.MaxHealth),
                Is.EqualTo((fp)950));
            attacker.StatHandler.SetCurrentHealth(
                (fp)700);
            Assert.That(
                attacker.EquipmentHandler.Remove(0),
                Is.True);
            Assert.That(
                attacker.StatHandler.CurrentHealth,
                Is.EqualTo((fp)250));
        }

        private fp DriveAttack(Unit target)
        {
            // Reset the target's health so each hit is measured against the
            // full pool (the first empowered strike may nearly kill it).
            target.StatHandler.SetCurrentHealth(
                target.StatHandler.GetStat(
                    StatId.MaxHealth));
            attacker.AttackHandler.BeginAttack(
                target.UnitUid);
            AttackSnapshot begun =
                Capture(attacker.AttackHandler);
            AdvanceTo(begun.ImpactLogicTick);
            attacker.AttackHandler.TickUpdate();
            Assert.That(
                attacker.AttackHandler
                    .ImpactCommitted,
                Is.True,
                "Attack must commit at impact.");
            fp before =
                target.StatHandler.CurrentHealth;
            combat.SettleActiveRequests();
            return before -
                target.StatHandler.CurrentHealth;
        }

        private void AdvanceTo(int tick)
        {
            controller.EndTick();
            controller.BeginTick(
                tick,
                ExecutionMode.ServerAuthority);
            combat.BeginTick();
        }

        private static AttackSnapshot Capture(
            AttackHandler handler)
        {
            AttackSnapshot state = default;
            handler.Capture(ref state);
            return state;
        }

        private static string CooldownKey(Unit owner) =>
            "SunderedSky.Cooldown." +
            owner.UnitUid;

        private static UnitPrototype CreatePrototype()
        {
            var preset = new StatPreset();
            AddStat(
                preset,
                StatId.AttackDamage,
                (fp)100);
            AddStat(
                preset,
                StatId.MaxHealth,
                (fp)500);
            AddStat(
                preset,
                StatId.AttackSpeed,
                (fp)30);
            AddStat(
                preset,
                StatId.AttackRange,
                (fp)200);
            AddStat(
                preset,
                StatId.Armor,
                fp.zero);
            return new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "SunderedSkyTestHero",
                RuntimeEntityPrefabId = 100,
                UnitKind = UnitKind.Hero,
                BaseStats = preset,
                Loadout = HandlerLoadout.DefaultHero,
            };
        }

        private static void AddStat(
            StatPreset preset,
            StatId id,
            fp value)
        {
            preset.Stats.Add(
                new StatPresetEntry
                {
                    StatId = id,
                    BaseValue = value,
                    GrowthValue = fp.zero,
                });
        }

        private static StatDefinitionTable
            CreateStatTable()
        {
            var table =
                new StatDefinitionTable();
            AddDefinition(
                table,
                StatId.AttackDamage);
            AddDefinition(
                table,
                StatId.MaxHealth);
            AddDefinition(
                table,
                StatId.AttackSpeed);
            AddDefinition(
                table,
                StatId.AttackRange);
            AddDefinition(
                table,
                StatId.Armor);
            AddDefinition(
                table,
                StatId.HealingReceivedRatio,
                (fp)1m);
            return table;
        }

        private static void AddDefinition(
            StatDefinitionTable table,
            StatId id,
            fp defaultBaseValue = default)
        {
            table.Add(
                new StatDefinition
                {
                    Id = id,
                    DebugName = id.ToString(),
                    DefaultBaseValue = defaultBaseValue,
                    SupportsLevelGrowth = true,
                });
        }

        private static EquipmentDatabase
            LoadDatabase()
        {
            EquipmentCatalogAsset catalog =
                AssetDatabase
                    .LoadAssetAtPath<
                        EquipmentCatalogAsset>(
                        "Assets/Config/Formal/Equipment/" +
                        "FormalEquipmentCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            return catalog.BakeOrThrow();
        }

        private static BuffDefinition
            LoadOverhealBuff()
        {
            BuffDefinition buff =
                AssetDatabase
                    .LoadAssetAtPath<BuffDefinition>(
                        "Assets/Config/Formal/Buffs/" +
                        "Buff_SunderedSkyOverheal.asset");
            Assert.That(buff, Is.Not.Null);
            return buff;
        }
    }
}
