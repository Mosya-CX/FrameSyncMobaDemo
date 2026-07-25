using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public class FullGameplayLoopTests
    {
        private SimulationTickContextController _tickController;
        [SetUp] public void SetUp() { _tickController = new SimulationTickContextController(); _tickController.BeginTick(0, ExecutionMode.ServerAuthority); }
        [TearDown] public void TearDown() { _tickController.EndTick(); _tickController = null; }

        [Test] public void TickContext_InitializesWithCorrectTick()
        {
            Assert.That(SimulationTickContext.Current.Tick, Is.EqualTo(0));
            Assert.That(SimulationTickContext.Current.ExecutionMode, Is.EqualTo(ExecutionMode.ServerAuthority));
        }

        [Test] public void TickContext_AdvancesCorrectly()
        {
            _tickController.EndTick(); _tickController.BeginTick(1, ExecutionMode.ServerAuthority);
            Assert.That(SimulationTickContext.Current.Tick, Is.EqualTo(1));
        }

        [Test] public void DeterministicRandom_ProducesSameSequenceForSameSeed()
        {
            var rng1 = new DeterministicRandomService(12345u); var rng2 = new DeterministicRandomService(12345u);
            for (int i = 0; i < 100; i++) Assert.That(rng1.NextInt(), Is.EqualTo(rng2.NextInt()));
        }

        [Test] public void UnitUid_ComparisonAndSorting()
        {
            var a = new UnitUid(1, 5, 10); var b = new UnitUid(1, 3, 20); var c = new UnitUid(2, 1, 5); var d = new UnitUid(1, 5, 10);
            Assert.That(a.CompareTo(b), Is.GreaterThan(0)); Assert.That(b.CompareTo(a), Is.LessThan(0));
            Assert.That(c.CompareTo(a), Is.GreaterThan(0)); Assert.That(a.Equals(d), Is.True);
        }
    }

    [TestFixture]
    public class MinionWaveIntegrationTests
    {
        [Test] public void PathGrid_Initialise_ProducesValidGrid() { var g=new PathGridMap2D(); g.Initialise(fp2.zero,new fp2((fp)9m,(fp)9m),(fp)1m); Assert.That(g.Width,Is.GreaterThan(0)); Assert.That(g.Height,Is.GreaterThan(0)); }
        [Test] public void AStar_FindPath_ReturnsValidPath() { var g=new PathGridMap2D(); g.Initialise(fp2.zero,new fp2((fp)15m,(fp)15m),(fp)1m); var a=new AStarPathService(g); PathResult r=a.FindPath(new fp2((fp)1m,(fp)1m),new fp2((fp)12m,(fp)12m),RadiusClass.Medium); Assert.That(r.Success,Is.True); Assert.That(r.PathCellIndices,Is.Not.Null.And.Length.GreaterThan(0)); }
        [Test] public void FlowField_BuildAndQuery_ReturnsValidDirection() { var g=new PathGridMap2D(); g.Initialise(fp2.zero,new fp2((fp)15m,(fp)15m),(fp)1m); var s=new TeamFlowFieldService(g); int[] cost=s.BuildLaneCostField(new LaneTargetConfig{LaneIndex=0,Targets=new fp2[]{new fp2((fp)12m,(fp)8m)}},RadiusClass.Small); var f=s.BuildTeamFlowField(0,RadiusClass.Small,new int[][]{cost},FlowFieldBuildConfig.Default); Assert.That(f.IsValid,Is.True); fp2 d=s.GetFlowDirection(f,new fp2((fp)2m,(fp)8m)); Assert.That(d.x!=fp.zero||d.y!=fp.zero); }
    }

    [TestFixture]
    public class ShopToCombatIntegrationTests
    {
        [Test] public void EquipmentDefinition_CreateFromConfig_Valid()
        {
            var def = new EquipmentDefinition { Id = 1001, Name = "Test Sword", Value = 500 };
            Assert.That(def.Id, Is.EqualTo(1001)); Assert.That(def.Name, Is.EqualTo("Test Sword")); Assert.That(def.Value, Is.EqualTo(500));
        }

        [Test] public void CombatRequest_CreatesValidDamage()
        {
            var r = new DamageRequest { SourceUnitUid = new UnitUid(1,1,10), TargetUnitUid = new UnitUid(2,2,20), DamageType = DamageType.Physical, BaseDamage = (fp)100m };
            Assert.That(r.IsValid, Is.True); Assert.That(r.BaseDamage, Is.EqualTo((fp)100m));
        }

        [Test] public void GoldIncome_BatchTracking_HasInitialState() { var g = new GoldIncomeRuntime(); Assert.That(g.ConfirmedEarnedGoldTotals, Is.Not.Null); }

        [Test] public void EquipmentSlotSnapshot_EmptyByDefault() { var s = default(EquipmentSlotSnapshot); Assert.That(s.EquipmentId, Is.EqualTo(0)); }
    }
}
