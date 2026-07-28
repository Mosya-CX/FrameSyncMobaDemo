using FrameSyncMoba.Unit;
using NUnit.Framework;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class EquipmentShopViewTests
    {
        [Test]
        public void CurrentAvailableGold_UsesConfirmedIncomeAndEffectiveShopDelta()
        {
            var income = new GoldIncomeRuntime();
            income.Initialize(1, 1000);
            var shop = new EquipmentShopRuntime();
            shop.Initialize(
                1,
                new EquipmentDatabase(),
                Unity.Mathematics.FixedPoint.fp.one,
                new UnitWorld());
            ShopTraderRuntime trader =
                shop.GetOrCreateTrader(
                    0,
                    new UnitUid(1, 1, 1));
            trader.OperationLog.Add(
                new ShopOperationRecord
                {
                    GoldDelta = -200,
                    Reverted = false,
                });
            trader.OperationLog.Add(
                new ShopOperationRecord
                {
                    GoldDelta = 50,
                    Reverted = true,
                });
            IEquipmentShopView view =
                new EquipmentShopView(
                    shop,
                    income,
                    0);

            Assert.That(
                view.GetCurrentAvailableGold(),
                Is.EqualTo(800));
            Assert.That(
                income
                    .GetConfirmedEarnedGoldTotal(0),
                Is.EqualTo(1000));

            income.BeginTick(0);
            income.RequestGoldIncome(
                0,
                100,
                GoldIncomeReason.NaturalIncome);
            income.SealTick(0);
            Assert.That(
                view.GetCurrentAvailableGold(),
                Is.EqualTo(800),
                "Unconfirmed Gameplay income is not spendable.");

            income.ConfirmAcceptedTick(0);
            Assert.That(
                view.GetCurrentAvailableGold(),
                Is.EqualTo(900));
        }
    }
}
