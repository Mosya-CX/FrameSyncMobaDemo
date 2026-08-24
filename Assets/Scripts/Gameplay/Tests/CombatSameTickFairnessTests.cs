using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class CombatSameTickFairnessTests
    {
        [TearDown]
        public void TearDown()
        {
            CombatEvents.Clear();
        }

        [Test]
        public void HealAndDamage_AreIndependentOfSubmissionOrder()
        {
            fp damageThenHeal = RunHealDamageScenario(true);
            fp healThenDamage = RunHealDamageScenario(false);

            Assert.AreEqual(healThenDamage, damageThenHeal);
            Assert.AreEqual((fp)20, damageThenHeal);
            Assert.AreEqual((fp)20, healThenDamage);
        }

        [Test]
        public void ShieldAndDamage_AreIndependentOfSubmissionOrder()
        {
            ScenarioResult damageThenShield =
                RunShieldDamageScenario(true);
            ScenarioResult shieldThenDamage =
                RunShieldDamageScenario(false);

            Assert.AreEqual(
                shieldThenDamage.LifeState,
                damageThenShield.LifeState);
            Assert.AreEqual(LifeState.Alive, damageThenShield.LifeState);
            Assert.AreEqual(LifeState.Alive, shieldThenDamage.LifeState);
            Assert.AreEqual((fp)20, damageThenShield.Health);
            Assert.AreEqual((fp)20, shieldThenDamage.Health);
        }

        [Test]
        public void LethalBatch_HighestEffectiveLifeDamageWins_AfterUidAndSubmissionMirror()
        {
            KillerScenarioResult first = RunKillerScenario(
                123u,
                highDamageUsesLowerUid: true,
                reverseSubmission: false,
                highDamage: (fp)70,
                lowDamage: (fp)30);
            KillerScenarioResult mirrored = RunKillerScenario(
                123u,
                highDamageUsesLowerUid: false,
                reverseSubmission: true,
                highDamage: (fp)70,
                lowDamage: (fp)30);

            Assert.IsTrue(first.HighDamageHeroWon);
            Assert.IsTrue(mirrored.HighDamageHeroWon);
            Assert.AreEqual(1, first.AssistantCount);
            Assert.AreEqual(1, mirrored.AssistantCount);
        }

        [Test]
        public void LethalBatch_ExactTie_IsStableForSeedAndIndependentOfSubmissionOrder()
        {
            KillerScenarioResult first = RunKillerScenario(
                777u,
                highDamageUsesLowerUid: true,
                reverseSubmission: false,
                highDamage: (fp)100,
                lowDamage: (fp)100);
            KillerScenarioResult reversed = RunKillerScenario(
                777u,
                highDamageUsesLowerUid: true,
                reverseSubmission: true,
                highDamage: (fp)100,
                lowDamage: (fp)100);

            Assert.AreEqual(first.KillerUid, reversed.KillerUid);
            Assert.AreEqual(1, first.AssistantCount);
            Assert.AreEqual(1, reversed.AssistantCount);
        }

        [Test]
        public void ConfiguredMatchSeedOverridesCompositionFallbackForExactTie()
        {
            for (uint seed = 1u; seed <= 16u; seed++)
            {
                KillerScenarioResult direct = RunKillerScenario(
                    seed,
                    highDamageUsesLowerUid: true,
                    reverseSubmission: false,
                    highDamage: (fp)100,
                    lowDamage: (fp)100);
                KillerScenarioResult configured = RunKillerScenario(
                    seed,
                    highDamageUsesLowerUid: true,
                    reverseSubmission: false,
                    highDamage: (fp)100,
                    lowDamage: (fp)100,
                    constructorSeed: 123u);

                Assert.AreEqual(
                    direct.KillerUid,
                    configured.KillerUid,
                    $"Configured match seed {seed} was not used.");
            }
        }

        [Test]
        public void ConfiguredMatchSeed_IsIdempotentButImmutable()
        {
            UnitWorld world = CreateWorld(out _, out _);
            var combat = new CombatSystem(world, 0, 0, 123u);

            combat.ConfigureInitialMatchSeed(777u);
            Assert.DoesNotThrow(() =>
                combat.ConfigureInitialMatchSeed(777u));
            Assert.Throws<DeterministicSimulationException>(() =>
                combat.ConfigureInitialMatchSeed(778u));
        }

        [Test]
        public void LethalBatch_ExactTie_SeedCorpusDoesNotPermanentlyFavorOneUid()
        {
            int lowerUidWins = 0;
            int higherUidWins = 0;
            for (uint seed = 1; seed <= 64; seed++)
            {
                KillerScenarioResult result = RunKillerScenario(
                    seed,
                    highDamageUsesLowerUid: true,
                    reverseSubmission: (seed & 1u) == 0u,
                    highDamage: (fp)100,
                    lowDamage: (fp)100);
                if (result.KillerUid.SpawnSequenceInTick == 0)
                    lowerUidWins++;
                else
                    higherUidWins++;
            }

            Assert.Greater(lowerUidWins, 0);
            Assert.Greater(higherUidWins, 0);
        }

        [Test]
        public void LethalBatch_ShieldOnlyDamageCannotWin()
        {
            Assert.IsTrue(RunShieldOnlyKillerScenario(false));
            Assert.IsTrue(RunShieldOnlyKillerScenario(true));
        }

        [Test]
        public void ShieldOnlyDamage_DoesNotGenerateDrainHealing()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                UnitWorld world = CreateWorld(out Unit attacker, out Unit target);
                AddFlatStat(attacker, StatId.Omnivamp, fp.one);
                AddFlatStat(
                    attacker,
                    StatId.HealingReceivedRatio,
                    fp.one);
                attacker.StatHandler.FinalizeTick();
                attacker.StatHandler.SetCurrentHealth((fp)50);

                var combat = new CombatSystem(world, 0, 0, 894u);
                combat.BeginTick();
                combat.SubmitShield(new ShieldRequest
                {
                    SourceUnitUid = target.UnitUid,
                    TargetUnitUid = target.UnitUid,
                    BaseValue = (fp)100,
                    ShieldType = ShieldType.Physical,
                });
                combat.SubmitDamage(
                    UnitTestFactory.CreateDamageRequest(
                        attacker.UnitUid,
                        target.UnitUid,
                        (fp)100,
                        DamageType.Physical));
                combat.SettleActiveRequests();
                combat.EndTick();

                Assert.AreEqual(
                    (fp)50,
                    attacker.StatHandler.CurrentHealth);
                Assert.AreEqual(
                    (fp)100,
                    target.StatHandler.CurrentHealth);
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void ProportionalLifeRemainder_NeverExceedsRequestCapacity()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                var world = new UnitWorld
                {
                    TickRate = 30,
                    RandomService = new DeterministicRandomService(912u),
                };
                var attackers = new Unit[3];
                for (int i = 0; i < attackers.Length; i++)
                {
                    attackers[i] = UnitTestFactory.CreateUnit(
                        new UnitUid(1, 1101, (byte)i),
                        UnitKind.Hero,
                        0,
                        new TeamId(1),
                        i + 1);
                    world.RegisterUnit(attackers[i]);
                }
                Unit victim = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1102, 3),
                    UnitKind.Hero,
                    0,
                    new TeamId(2),
                    4);
                world.RegisterUnit(victim);
                fp oneRawUnit = fp.FromRaw(1);
                victim.StatHandler.SetCurrentHealth(fp.FromRaw(2));
                var actualAmounts =
                    new System.Collections.Generic.List<fp>();
                CombatEvents.OnDamageTaken += data =>
                    actualAmounts.Add(data.ActualDamage);

                var combat = new CombatSystem(world, 0, 0, 912u);
                combat.BeginTick();
                for (int i = 0; i < attackers.Length; i++)
                {
                    combat.SubmitDamage(CreateDamage(
                        attackers[i],
                        victim,
                        oneRawUnit));
                }
                combat.SettleActiveRequests();
                combat.EndTick();

                Assert.AreEqual(3, actualAmounts.Count);
                fp totalActual = fp.zero;
                for (int i = 0; i < actualAmounts.Count; i++)
                {
                    Assert.LessOrEqual(actualAmounts[i], oneRawUnit);
                    totalActual += actualAmounts[i];
                }
                Assert.AreEqual(fp.FromRaw(2), totalActual);
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void ProportionalHealRemainder_NeverExceedsRequestCapacity()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                UnitWorld world = CreateWorld(out Unit first, out Unit target);
                Unit second = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1101, 2),
                    UnitKind.Hero,
                    0,
                    new TeamId(1),
                    3);
                Unit third = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1101, 3),
                    UnitKind.Hero,
                    0,
                    new TeamId(1),
                    4);
                world.RegisterUnit(second);
                world.RegisterUnit(third);
                AddFlatStat(
                    target,
                    StatId.HealingReceivedRatio,
                    fp.one);
                target.StatHandler.FinalizeTick();
                fp maximum = target.StatHandler.GetStat(StatId.MaxHealth);
                target.StatHandler.SetCurrentHealth(
                    maximum - fp.FromRaw(2));
                fp oneRawUnit = fp.FromRaw(1);
                var effectiveAmounts =
                    new System.Collections.Generic.List<fp>();
                CombatEvents.OnHealTaken += data =>
                    effectiveAmounts.Add(data.EffectiveHeal);

                var combat = new CombatSystem(world, 0, 0, 913u);
                combat.BeginTick();
                Unit[] sources = { first, second, third };
                for (int i = 0; i < sources.Length; i++)
                {
                    combat.SubmitHeal(new HealRequest
                    {
                        SourceUnitUid = sources[i].UnitUid,
                        TargetUnitUid = target.UnitUid,
                        BaseValue = oneRawUnit,
                    });
                }
                combat.SettleActiveRequests();
                combat.EndTick();

                Assert.AreEqual(3, effectiveAmounts.Count);
                fp totalEffective = fp.zero;
                for (int i = 0; i < effectiveAmounts.Count; i++)
                {
                    Assert.LessOrEqual(
                        effectiveAmounts[i],
                        oneRawUnit);
                    totalEffective += effectiveAmounts[i];
                }
                Assert.AreEqual(fp.FromRaw(2), totalEffective);
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void DamageReactionHeal_EntersNextWaveAndRecoversTarget()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                UnitWorld world = CreateWorld(out Unit attacker, out Unit target);
                var combat = new CombatSystem(world, 0, 0, 99u);
                target.StatHandler.AddModifier(
                    StatId.HealingReceivedRatio,
                    StatModifierOperation.FlatAdd,
                    fp.one);
                target.StatHandler.FinalizeTick();
                bool submitted = false;
                CombatEvents.OnDamageTaken += data =>
                {
                    if (submitted) return;
                    submitted = true;
                    combat.SubmitHeal(new HealRequest
                    {
                        SourceUnitUid = target.UnitUid,
                        TargetUnitUid = target.UnitUid,
                        BaseValue = (fp)50,
                    });
                };

                combat.BeginTick();
                combat.SubmitDamage(CreateDamage(attacker, target, (fp)120));
                combat.SettleActiveRequests();
                combat.EndTick();

                Assert.AreEqual(LifeState.Alive, target.LifeState);
                Assert.AreEqual((fp)50, target.StatHandler.CurrentHealth);
                Assert.AreEqual(0, combat.DeathResults.Count);
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void SameTickMutualLethal_AllSealedAttacksRemainEffective()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                UnitWorld world = CreateWorld(out Unit first, out Unit second);
                var combat = new CombatSystem(world, 0, 0, 321u);
                combat.BeginTick();
                combat.SubmitDamage(CreateDamage(first, second, (fp)100));
                combat.SubmitDamage(CreateDamage(second, first, (fp)100));
                combat.SettleActiveRequests();
                combat.EndTick();

                Assert.AreEqual(LifeState.Dead, first.LifeState);
                Assert.AreEqual(LifeState.Dead, second.LifeState);
                Assert.AreEqual(2, combat.DeathResults.Count);
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void SameTickMutualLethal_DrainReactionsRecoverBothSources()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                UnitWorld world = CreateWorld(out Unit first, out Unit second);
                AddFlatStat(first, StatId.Omnivamp, fp.one);
                AddFlatStat(second, StatId.Omnivamp, fp.one);
                AddFlatStat(first, StatId.HealingReceivedRatio, fp.one);
                AddFlatStat(second, StatId.HealingReceivedRatio, fp.one);
                first.StatHandler.FinalizeTick();
                second.StatHandler.FinalizeTick();

                var combat = new CombatSystem(world, 0, 0, 654u);
                combat.BeginTick();
                combat.SubmitDamage(CreateDamage(first, second, (fp)100));
                combat.SubmitDamage(CreateDamage(second, first, (fp)100));
                combat.SettleActiveRequests();
                combat.EndTick();

                Assert.AreEqual(LifeState.Alive, first.LifeState);
                Assert.AreEqual(LifeState.Alive, second.LifeState);
                Assert.AreEqual((fp)100, first.StatHandler.CurrentHealth);
                Assert.AreEqual((fp)100, second.StatHandler.CurrentHealth);
                Assert.AreEqual(0, combat.DeathResults.Count);
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void MixedDamageAndSpecificShields_AreIndependentOfSubmissionOrder()
        {
            ScenarioResult first = RunMixedShieldScenario(false);
            ScenarioResult reversed = RunMixedShieldScenario(true);

            Assert.AreEqual(LifeState.Alive, first.LifeState);
            Assert.AreEqual(LifeState.Alive, reversed.LifeState);
            Assert.AreEqual((fp)50, first.Health);
            Assert.AreEqual(first.Health, reversed.Health);
        }

        private static fp RunHealDamageScenario(bool damageFirst)
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                UnitWorld world = CreateWorld(out Unit attacker, out Unit target);
                var combat = new CombatSystem(world, 0, 0);
                target.StatHandler.AddModifier(
                    StatId.HealingReceivedRatio,
                    StatModifierOperation.FlatAdd,
                    fp.one);
                target.StatHandler.FinalizeTick();
                target.StatHandler.SetCurrentHealth((fp)50);
                combat.BeginTick();

                DamageRequest damage = CreateDamage(attacker, target, (fp)80);
                var heal = new HealRequest
                {
                    SourceUnitUid = target.UnitUid,
                    TargetUnitUid = target.UnitUid,
                    BaseValue = (fp)50,
                };
                if (damageFirst)
                {
                    combat.SubmitDamage(damage);
                    combat.SubmitHeal(heal);
                }
                else
                {
                    combat.SubmitHeal(heal);
                    combat.SubmitDamage(damage);
                }

                combat.SettleActiveRequests();
                combat.EndTick();
                return target.StatHandler.CurrentHealth;
            }
            finally
            {
                controller.EndTick();
            }
        }

        private static ScenarioResult RunShieldDamageScenario(bool damageFirst)
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                UnitWorld world = CreateWorld(out Unit attacker, out Unit target);
                var combat = new CombatSystem(world, 0, 0);
                target.StatHandler.SetCurrentHealth((fp)50);
                combat.BeginTick();

                DamageRequest damage = CreateDamage(attacker, target, (fp)80);
                var shield = new ShieldRequest
                {
                    SourceUnitUid = target.UnitUid,
                    TargetUnitUid = target.UnitUid,
                    BaseValue = (fp)50,
                    ShieldType = ShieldType.White,
                    DurationTicks = 0,
                };
                if (damageFirst)
                {
                    combat.SubmitDamage(damage);
                    combat.SubmitShield(shield);
                }
                else
                {
                    combat.SubmitShield(shield);
                    combat.SubmitDamage(damage);
                }

                combat.SettleActiveRequests();
                combat.EndTick();
                return new ScenarioResult(
                    target.LifeState,
                    target.StatHandler.CurrentHealth);
            }
            finally
            {
                controller.EndTick();
            }
        }

        private static UnitWorld CreateWorld(
            out Unit attacker,
            out Unit target)
        {
            var world = new UnitWorld
            {
                TickRate = 30,
                RandomService = new DeterministicRandomService(123u),
            };
            attacker = UnitTestFactory.CreateUnit(
                new UnitUid(1, 1101, 0),
                UnitKind.Hero,
                0,
                new TeamId(1),
                1);
            target = UnitTestFactory.CreateUnit(
                new UnitUid(1, 1102, 0),
                UnitKind.Hero,
                0,
                new TeamId(2),
                2);
            world.RegisterUnit(attacker);
            world.RegisterUnit(target);
            return world;
        }

        private static KillerScenarioResult RunKillerScenario(
            uint seed,
            bool highDamageUsesLowerUid,
            bool reverseSubmission,
            fp highDamage,
            fp lowDamage,
            uint constructorSeed = 0u)
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                var world = new UnitWorld
                {
                    TickRate = 30,
                    RandomService = new DeterministicRandomService(seed),
                };
                Unit lowerUidHero = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1101, 0),
                    UnitKind.Hero,
                    0,
                    new TeamId(1),
                    1);
                Unit higherUidHero = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1101, 1),
                    UnitKind.Hero,
                    0,
                    new TeamId(1),
                    2);
                Unit victim = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1102, 0),
                    UnitKind.Hero,
                    0,
                    new TeamId(2),
                    3);
                world.RegisterUnit(lowerUidHero);
                world.RegisterUnit(higherUidHero);
                world.RegisterUnit(victim);
                Unit highHero = highDamageUsesLowerUid
                    ? lowerUidHero
                    : higherUidHero;
                Unit lowHero = highDamageUsesLowerUid
                    ? higherUidHero
                    : lowerUidHero;
                var combat = new CombatSystem(
                    world,
                    0,
                    0,
                    constructorSeed == 0u
                        ? seed
                        : constructorSeed);
                if (constructorSeed != 0u)
                    combat.ConfigureInitialMatchSeed(seed);
                combat.BeginTick();
                DamageRequest highRequest = CreateDamage(
                    highHero,
                    victim,
                    highDamage);
                DamageRequest lowRequest = CreateDamage(
                    lowHero,
                    victim,
                    lowDamage);
                if (reverseSubmission)
                {
                    combat.SubmitDamage(lowRequest);
                    combat.SubmitDamage(highRequest);
                }
                else
                {
                    combat.SubmitDamage(highRequest);
                    combat.SubmitDamage(lowRequest);
                }
                combat.SettleActiveRequests();
                combat.EndTick();

                Assert.AreEqual(1, combat.DeathResults.Count);
                DeathResult death = combat.DeathResults[0];
                return new KillerScenarioResult(
                    death.KillerHeroUid,
                    death.KillerHeroUid == highHero.UnitUid,
                    death.AssistantHeroUids.Length);
            }
            finally
            {
                controller.EndTick();
            }
        }

        private static ScenarioResult RunMixedShieldScenario(bool reverse)
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                var world = new UnitWorld
                {
                    TickRate = 30,
                    RandomService = new DeterministicRandomService(456u),
                };
                Unit physicalSource = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1101, 0),
                    UnitKind.Hero,
                    0,
                    new TeamId(1),
                    1);
                Unit magicSource = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1101, 1),
                    UnitKind.Hero,
                    0,
                    new TeamId(1),
                    2);
                Unit victim = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1102, 0),
                    UnitKind.Hero,
                    0,
                    new TeamId(2),
                    3);
                world.RegisterUnit(physicalSource);
                world.RegisterUnit(magicSource);
                world.RegisterUnit(victim);
                var combat = new CombatSystem(world, 0, 0, 456u);
                var white = new ShieldRequest
                {
                    SourceUnitUid = victim.UnitUid,
                    TargetUnitUid = victim.UnitUid,
                    BaseValue = (fp)50,
                    ShieldType = ShieldType.White,
                };
                var physical = new ShieldRequest
                {
                    SourceUnitUid = victim.UnitUid,
                    TargetUnitUid = victim.UnitUid,
                    BaseValue = (fp)100,
                    ShieldType = ShieldType.Physical,
                };
                var magic = new ShieldRequest
                {
                    SourceUnitUid = victim.UnitUid,
                    TargetUnitUid = victim.UnitUid,
                    BaseValue = (fp)100,
                    ShieldType = ShieldType.Magic,
                };
                DamageRequest physicalDamage = UnitTestFactory.CreateDamageRequest(
                    physicalSource.UnitUid,
                    victim.UnitUid,
                    (fp)150,
                    DamageType.Physical);
                DamageRequest magicDamage = UnitTestFactory.CreateDamageRequest(
                    magicSource.UnitUid,
                    victim.UnitUid,
                    (fp)150,
                    DamageType.Magic);

                combat.BeginTick();
                if (reverse)
                {
                    combat.SubmitDamage(magicDamage);
                    combat.SubmitShield(magic);
                    combat.SubmitDamage(physicalDamage);
                    combat.SubmitShield(physical);
                    combat.SubmitShield(white);
                }
                else
                {
                    combat.SubmitShield(white);
                    combat.SubmitDamage(physicalDamage);
                    combat.SubmitShield(physical);
                    combat.SubmitDamage(magicDamage);
                    combat.SubmitShield(magic);
                }
                combat.SettleActiveRequests();
                combat.EndTick();
                return new ScenarioResult(
                    victim.LifeState,
                    victim.StatHandler.CurrentHealth);
            }
            finally
            {
                controller.EndTick();
            }
        }

        private static bool RunShieldOnlyKillerScenario(
            bool reverseSubmission)
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                var world = new UnitWorld
                {
                    TickRate = 30,
                    RandomService = new DeterministicRandomService(891u),
                };
                Unit shieldAttacker = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1101, 0),
                    UnitKind.Hero,
                    0,
                    new TeamId(1),
                    1);
                Unit lifeAttacker = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1101, 1),
                    UnitKind.Hero,
                    0,
                    new TeamId(1),
                    2);
                Unit victim = UnitTestFactory.CreateUnit(
                    new UnitUid(1, 1102, 0),
                    UnitKind.Hero,
                    0,
                    new TeamId(2),
                    3);
                world.RegisterUnit(shieldAttacker);
                world.RegisterUnit(lifeAttacker);
                world.RegisterUnit(victim);
                victim.StatHandler.SetCurrentHealth((fp)50);

                var combat = new CombatSystem(world, 0, 0, 891u);
                var physicalShield = new ShieldRequest
                {
                    SourceUnitUid = victim.UnitUid,
                    TargetUnitUid = victim.UnitUid,
                    BaseValue = (fp)100,
                    ShieldType = ShieldType.Physical,
                };
                DamageRequest shieldDamage =
                    UnitTestFactory.CreateDamageRequest(
                        shieldAttacker.UnitUid,
                        victim.UnitUid,
                        (fp)100,
                        DamageType.Physical);
                DamageRequest lifeDamage =
                    UnitTestFactory.CreateDamageRequest(
                        lifeAttacker.UnitUid,
                        victim.UnitUid,
                        (fp)50,
                        DamageType.True);

                combat.BeginTick();
                combat.SubmitShield(physicalShield);
                if (reverseSubmission)
                {
                    combat.SubmitDamage(lifeDamage);
                    combat.SubmitDamage(shieldDamage);
                }
                else
                {
                    combat.SubmitDamage(shieldDamage);
                    combat.SubmitDamage(lifeDamage);
                }
                combat.SettleActiveRequests();
                combat.EndTick();

                Assert.AreEqual(1, combat.DeathResults.Count);
                return combat.DeathResults[0].KillerHeroUid ==
                       lifeAttacker.UnitUid;
            }
            finally
            {
                controller.EndTick();
            }
        }

        private static DamageRequest CreateDamage(
            Unit source,
            Unit target,
            fp amount)
        {
            return UnitTestFactory.CreateDamageRequest(
                source.UnitUid,
                target.UnitUid,
                amount,
                DamageType.True,
                CombatSourceType.Attack,
                CombatBuiltinSourceId.BasicAttack,
                CombatBuiltinRecipeId.BasicAttackDamage);
        }

        private static void AddFlatStat(
            Unit unit,
            StatId statId,
            fp value)
        {
            unit.StatHandler.AddModifier(
                statId,
                StatModifierOperation.FlatAdd,
                value);
        }

        private readonly struct ScenarioResult
        {
            public readonly LifeState LifeState;
            public readonly fp Health;

            public ScenarioResult(LifeState lifeState, fp health)
            {
                LifeState = lifeState;
                Health = health;
            }
        }

        private readonly struct KillerScenarioResult
        {
            public readonly UnitUid KillerUid;
            public readonly bool HighDamageHeroWon;
            public readonly int AssistantCount;

            public KillerScenarioResult(
                UnitUid killerUid,
                bool highDamageHeroWon,
                int assistantCount)
            {
                KillerUid = killerUid;
                HighDamageHeroWon = highDamageHeroWon;
                AssistantCount = assistantCount;
            }
        }
    }
}
