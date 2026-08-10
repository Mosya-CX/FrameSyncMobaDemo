using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Verifies the design v9.1 section 12 UI Request entry points:
    /// RequestCheck runs locally, the canonical Command is submitted only when
    /// the check passes, and the submitter port is never exposed to UI code.
    /// </summary>
    public sealed class EquipmentShopRequestTests
    {
        private SimulationTickContextController tickController;

        [SetUp]
        public void SetUp()
        {
            tickController =
                new SimulationTickContextController();
            tickController.BeginTick(
                10,
                ExecutionMode.ServerAuthority);
        }

        [TearDown]
        public void TearDown()
        {
            tickController.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void RequestPurchase_Allowed_SubmitsCanonicalCommand()
        {
            EquipmentDefinition item =
                Definition(1, 50, EquipmentTier.Basic);
            RequestContext context =
                CreateContext(item, 100);

            EquipmentShopRequestCheck check =
                context.Shop.RequestPurchase(
                    0,
                    item.Id);

            Assert.That(check.Allowed, Is.True);
            Assert.That(
                context.Submitter.Purchases,
                Is.EqualTo(new[] { item.Id }));
            Assert.That(
                context.Submitter.TotalCalls,
                Is.EqualTo(1));
        }

        [Test]
        public void RequestPurchase_InsufficientGold_RejectsWithoutSubmission()
        {
            EquipmentDefinition item =
                Definition(1, 50, EquipmentTier.Basic);
            RequestContext context =
                CreateContext(item, 10);

            EquipmentShopRequestCheck check =
                context.Shop.RequestPurchase(
                    0,
                    item.Id);

            Assert.That(check.Allowed, Is.False);
            Assert.That(
                check.FailureReason,
                Is.EqualTo(
                    EquipmentShopFailureReason
                        .InsufficientGold));
            Assert.That(
                context.Submitter.TotalCalls,
                Is.EqualTo(0));
        }

        [Test]
        public void RequestSell_EmptySlot_RejectsWithoutSubmission()
        {
            EquipmentDefinition item =
                Definition(10, 100, EquipmentTier.Basic);
            RequestContext context =
                CreateContext(item, 100);

            EquipmentShopRequestCheck check =
                context.Shop.RequestSell(0, 2);

            Assert.That(check.Allowed, Is.False);
            Assert.That(
                check.FailureReason,
                Is.EqualTo(
                    EquipmentShopFailureReason
                        .EmptySlot));
            Assert.That(
                context.Submitter.TotalCalls,
                Is.EqualTo(0));
        }

        [Test]
        public void RequestSell_Allowed_SubmitsSlot()
        {
            EquipmentDefinition item =
                Definition(10, 100, EquipmentTier.Basic);
            RequestContext context =
                CreateContext(item, 100);
            Assert.IsTrue(
                context.Handler.Add(item, 2));

            EquipmentShopRequestCheck check =
                context.Shop.RequestSell(0, 2);

            Assert.That(check.Allowed, Is.True);
            Assert.That(
                context.Submitter.Sells,
                Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void RequestUndo_AfterSettledSell_Allowed()
        {
            EquipmentDefinition item =
                Definition(10, 100, EquipmentTier.Basic);
            RequestContext context =
                CreateContext(item, 100);
            Assert.IsTrue(
                context.Handler.Add(item, 2));
            Assert.IsTrue(
                context.Shop.TrySell(
                    0,
                    2,
                    context.Handler,
                    out int sellValue,
                    out _));
            Assert.IsTrue(
                context.Shop.ProcessSell(
                    0,
                    2,
                    context.Handler,
                    sellValue,
                    out _));

            EquipmentShopRequestCheck check =
                context.Shop.RequestUndo(0);

            Assert.That(check.Allowed, Is.True);
            Assert.That(
                context.Submitter.UndoCount,
                Is.EqualTo(1));
        }

        [Test]
        public void RequestUndo_NoTransactions_Rejects()
        {
            EquipmentDefinition item =
                Definition(10, 100, EquipmentTier.Basic);
            RequestContext context =
                CreateContext(item, 100);

            EquipmentShopRequestCheck check =
                context.Shop.RequestUndo(0);

            Assert.That(check.Allowed, Is.False);
            Assert.That(
                check.FailureReason,
                Is.EqualTo(
                    EquipmentShopFailureReason
                        .NoUndoableTransaction));
            Assert.That(
                context.Submitter.TotalCalls,
                Is.EqualTo(0));
        }

        [Test]
        public void RequestPurchase_MissingIncomeView_Throws()
        {
            EquipmentDefinition item =
                Definition(1, 50, EquipmentTier.Basic);
            RequestContext context =
                CreateContext(item, 100, false);

            Assert.Throws<InvalidOperationException>(
                () => context.Shop.RequestPurchase(
                    0,
                    item.Id));
        }

        [Test]
        public void RequestPurchase_MissingSubmitter_Throws()
        {
            EquipmentDefinition item =
                Definition(1, 50, EquipmentTier.Basic);
            RequestContext context =
                CreateContext(item, 100, true, false);

            Assert.Throws<InvalidOperationException>(
                () => context.Shop.RequestPurchase(
                    0,
                    item.Id));
        }

        private static RequestContext CreateContext(
            EquipmentDefinition item,
            int confirmedGold,
            bool withIncomeView = true,
            bool withSubmitter = true)
        {
            var database = new EquipmentDatabase();
            database.Register(item);
            database.Seal();
            var world = new UnitWorld
            {
                EquipmentDatabase = database,
            };
            Unit unit = UnitTestFactory.CreateUnit(
                new UnitUid(0, 2, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            unit.World = world;
            unit.EquipmentHandler.DefinitionDatabase =
                database;
            world.RegisterUnit(unit);

            var shop = new EquipmentShopRuntime();
            shop.Initialize(
                1,
                database,
                (fp)7 / (fp)10,
                world);
            shop.GetOrCreateTrader(0, unit.UnitUid);

            var submitter = new RecordingSubmitter();
            if (withIncomeView)
                shop.ConfigureIncomeView(
                    new FixedIncomeView(confirmedGold));
            if (withSubmitter)
                shop.SetCommandSubmitter(submitter);

            return new RequestContext(
                shop,
                unit.EquipmentHandler,
                submitter);
        }

        private static EquipmentDefinition Definition(
            int id,
            int value,
            EquipmentTier tier)
        {
            var def =
                ScriptableObject
                    .CreateInstance<EquipmentDefinition>();
            def.Id = id;
            def.Name = $"TestEquipment{id}";
            def.Description = "Request test fixture";
            def.Tier = tier;
            def.Value = value;
            def.MaxStack =
                tier == EquipmentTier.Consumable
                    ? 3
                    : 1;
            return def;
        }

        private sealed class FixedIncomeView :
            IConfirmedGoldIncomeView
        {
            private readonly int _confirmed;

            public FixedIncomeView(int confirmed)
            {
                _confirmed = confirmed;
            }

            public int GetConfirmedEarnedGoldTotal(
                int playerSlot)
            {
                return _confirmed;
            }
        }

        private sealed class RecordingSubmitter :
            IEquipmentShopCommandSubmitter
        {
            public readonly List<int> Purchases =
                new List<int>();
            public readonly List<int> Sells =
                new List<int>();
            public int UndoCount;

            public int TotalCalls =>
                Purchases.Count + Sells.Count + UndoCount;

            public void SubmitPurchase(
                int playerSlot,
                int targetEquipmentId)
            {
                Purchases.Add(targetEquipmentId);
            }

            public void SubmitSell(
                int playerSlot,
                int sourceSlot)
            {
                Sells.Add(sourceSlot);
            }

            public void SubmitUndo(int playerSlot)
            {
                UndoCount++;
            }
        }

        private readonly struct RequestContext
        {
            public readonly EquipmentShopRuntime Shop;
            public readonly EquipmentHandler Handler;
            public readonly RecordingSubmitter Submitter;

            public RequestContext(
                EquipmentShopRuntime shop,
                EquipmentHandler handler,
                RecordingSubmitter submitter)
            {
                Shop = shop;
                Handler = handler;
                Submitter = submitter;
            }
        }
    }
}
