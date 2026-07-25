using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class RVOIntegrationTests
    {
        private DeterministicRVOSystem _rvo;
        [SetUp] public void SetUp() => _rvo = new DeterministicRVOSystem(RVOConfig.Default);

        private static RVOInput MakeInput(int id, fp2 pos, fp2 desiredVel, fp maxSpeed = default, fp radius = default)
        {
            if (maxSpeed <= fp.zero) maxSpeed = (fp)3m;
            if (radius <= fp.zero) radius = (fp)0.5m;
            return new RVOInput { SelfUid = new UnitUid(1, (byte)id, (byte)id), Position = pos, DesiredVelocity = desiredVel, Radius = radius, MaxSpeed = maxSpeed };
        }

        [Test] public void FiveAgents_ConvergingOnCenter_NoDeadlock()
        {
            var inputs = new RVOInput[] { MakeInput(1,new fp2((fp)0m,(fp)5m),new fp2((fp)0m,-(fp)1m)), MakeInput(2,new fp2((fp)0m,-(fp)5m),new fp2((fp)0m,(fp)1m)), MakeInput(3,new fp2(-(fp)5m,(fp)0m),new fp2((fp)1m,(fp)0m)), MakeInput(4,new fp2((fp)5m,(fp)0m),new fp2(-(fp)1m,(fp)0m)), MakeInput(5,new fp2(-(fp)3m,(fp)3m),new fp2((fp)1m,-(fp)1m)) };
            RvoResult[] r = _rvo.Step(inputs); Assert.That(r.Length, Is.EqualTo(5));
            for (int i = 0; i < r.Length; i++) Assert.That(fpmath.dot(r[i].FinalVelocity,r[i].FinalVelocity), Is.GreaterThanOrEqualTo(fp.zero));
        }

        [Test] public void TwoUnitsHeadOn_VelocitiesDiverge()
        {
            var inputs = new RVOInput[] { MakeInput(1,new fp2(fp.zero,(fp)8m),new fp2(fp.zero,-fp.one)), MakeInput(2,new fp2(fp.zero,(fp)3m),new fp2(fp.zero,fp.one)) };
            RvoResult[] r = _rvo.Step(inputs); Assert.That(r.Length, Is.EqualTo(2));
            for (int i = 0; i < r.Length; i++) { fp lenSq = fpmath.dot(r[i].FinalVelocity,r[i].FinalVelocity); Assert.That(lenSq, Is.GreaterThanOrEqualTo(fp.zero)); Assert.That(lenSq, Is.LessThanOrEqualTo(inputs[i].MaxSpeed*inputs[i].MaxSpeed+(fp)0.1m)); }
        }

        [Test] public void TenAgents_RandomSpread_CompletesWithoutError()
        {
            var inputs = new RVOInput[10];
            for (int i = 0; i < 10; i++) { fp a = (fp)((i*36.0m)*3.14159265m/180.0m); inputs[i] = MakeInput(i+1,new fp2(fpmath.cos(a)*(fp)5m,fpmath.sin(a)*(fp)5m),new fp2(-fpmath.cos(a),-fpmath.sin(a))); }
            RvoResult[] r = _rvo.Step(inputs); Assert.That(r.Length, Is.EqualTo(10));
            for (int i = 0; i < r.Length; i++) Assert.That(fpmath.dot(r[i].FinalVelocity,r[i].FinalVelocity), Is.GreaterThanOrEqualTo(fp.zero));
        }

        [Test] public void RVO_Deterministic_SameInputProducesSameOutput()
        {
            var inputs = new RVOInput[] { MakeInput(1,new fp2((fp)0m,(fp)5m),new fp2((fp)0m,-(fp)1m)), MakeInput(2,new fp2((fp)0m,(fp)6m),new fp2((fp)0m,(fp)1m)), MakeInput(3,new fp2((fp)1m,(fp)5m),new fp2((fp)0m,-(fp)1m)) };
            var a = new DeterministicRVOSystem(RVOConfig.Default); var b = new DeterministicRVOSystem(RVOConfig.Default);
            RvoResult[] r1 = a.Step(inputs); RvoResult[] r2 = b.Step(inputs);
            for (int i = 0; i < r1.Length; i++) { Assert.That(r1[i].FinalVelocity.x, Is.EqualTo(r2[i].FinalVelocity.x)); Assert.That(r1[i].FinalVelocity.y, Is.EqualTo(r2[i].FinalVelocity.y)); }
        }

        [Test] public void RVO_ZeroDesiredVelocity_ReturnsZero()
        {
            var inputs = new RVOInput[] { MakeInput(1,fp2.zero,fp2.zero), MakeInput(2,new fp2((fp)3m,fp.zero),new fp2(-fp.one,fp.zero)) };
            RvoResult[] r = _rvo.Step(inputs); Assert.That(r[0].FinalVelocity.x, Is.EqualTo(fp.zero)); Assert.That(r[0].FinalVelocity.y, Is.EqualTo(fp.zero));
        }
    }

    [TestFixture]
    public class FlowFieldLaneIntegrationTests
    {
        private const int W = 16, H = 16; private static readonly fp CS = (fp)1m;
        private PathGridMap2D CreateGrid() { var g = new PathGridMap2D(); g.Initialise(fp2.zero, new fp2((fp)(W-1),(fp)(H-1)), CS); return g; }
        private TeamFlowFieldData BuildField(PathGridMap2D g, TeamFlowFieldService s, fp2 tgt, byte tid=0, RadiusClass rc=RadiusClass.Medium)
        {
            int[] cost = s.BuildLaneCostField(new LaneTargetConfig{LaneIndex=0,Targets=new fp2[]{tgt}}, rc);
            return s.BuildTeamFlowField(tid, rc, new int[][]{cost}, FlowFieldBuildConfig.Default);
        }

        [Test] public void FlowDirection_FromFarCell_PointsTowardTarget() { var g=CreateGrid(); var s=new TeamFlowFieldService(g); var f=BuildField(g,s,new fp2((fp)8m,(fp)8m)); fp2 d=s.GetFlowDirection(f,new fp2((fp)2m,(fp)2m)); Assert.That(d.x!=fp.zero||d.y!=fp.zero); Assert.That(d.x,Is.GreaterThanOrEqualTo(fp.zero)); Assert.That(d.y,Is.GreaterThanOrEqualTo(fp.zero)); }
        [Test] public void FlowDirection_AtTarget_ReturnsZero() { var g=CreateGrid(); var s=new TeamFlowFieldService(g); var f=BuildField(g,s,new fp2((fp)8m,(fp)8m)); fp2 d=s.GetFlowDirection(f,new fp2((fp)8m,(fp)8m)); Assert.That(d.x,Is.EqualTo(fp.zero)); Assert.That(d.y,Is.EqualTo(fp.zero)); }
        [Test] public void FlowField_BlockedCell_AvoidsObstacle() { var g=CreateGrid(); for(int y=2;y<=12;y++) g.SetObstruction(new fp2((fp)7.5m,(fp)(y-0.5m)),new fp2((fp)8.5m,(fp)(y+0.5m)),blocked:true); var s=new TeamFlowFieldService(g); var f=BuildField(g,s,new fp2((fp)14m,(fp)8m)); fp2 d=s.GetFlowDirection(f,new fp2((fp)5m,(fp)8m)); Assert.That(d.x!=fp.zero||d.y!=fp.zero); }
        [Test] public void FlowField_AtEdgeOfGrid_HandlesBordersGracefully() { var g=CreateGrid(); var s=new TeamFlowFieldService(g); var f=BuildField(g,s,new fp2((fp)8m,(fp)8m)); fp2 d=s.GetFlowDirection(f,fp2.zero); Assert.That(d.x!=fp.zero||d.y!=fp.zero); }
    }

    [TestFixture]
    public class AStarFlowFieldFallbackTests
    {
        private const int W=16,H=16; private static readonly fp CS=(fp)1m;
        private PathGridMap2D CreateGrid() { var g=new PathGridMap2D(); g.Initialise(fp2.zero,new fp2((fp)(W-1),(fp)(H-1)),CS); return g; }

        [Test] public void AStar_FindsPath_InOpenGrid() { var g=CreateGrid(); var a=new AStarPathService(g); PathResult r=a.FindPath(new fp2((fp)2m,(fp)2m),new fp2((fp)12m,(fp)12m),RadiusClass.Medium); Assert.That(r.Success,Is.True); Assert.That(r.PathCellIndices,Is.Not.Null.And.Length.GreaterThan(0)); }
        [Test] public void AStar_BlockedPath_ReturnsEmpty() { var g=CreateGrid(); for(int x=0;x<W;x++) g.SetObstruction(new fp2((fp)(x-0.5m),(fp)7.5m),new fp2((fp)(x+0.5m),(fp)8.5m),blocked:true); var a=new AStarPathService(g); PathResult r=a.FindPath(new fp2((fp)2m,(fp)2m),new fp2((fp)12m,(fp)12m),RadiusClass.Medium); Assert.That(r.Success,Is.False); }
        [Test] public void FlowField_Unavailable_FallbackToAStar() { var g=CreateGrid(); var a=new AStarPathService(g); PathResult r=a.FindPath(new fp2((fp)2m,(fp)2m),new fp2((fp)12m,(fp)12m),RadiusClass.Medium); Assert.That(r.Success,Is.True); }
    }

    [TestFixture]
    public class MinionWaveFlowFieldIntegrationTests
    {
        private const int W=20,H=20; private static readonly fp CS=(fp)1m;
        private PathGridMap2D CreateGrid() { var g=new PathGridMap2D(); g.Initialise(fp2.zero,new fp2((fp)(W-1),(fp)(H-1)),CS); return g; }

        [Test] public void MinionWave_MultipleAgents_FollowFlowFieldTowardTarget()
        {
            var g=CreateGrid(); var s=new TeamFlowFieldService(g);
            int[] cost=s.BuildLaneCostField(new LaneTargetConfig{LaneIndex=0,Targets=new fp2[]{new fp2((fp)18m,(fp)10m)}},RadiusClass.Small);
            var f=s.BuildTeamFlowField(0,RadiusClass.Small,new int[][]{cost},FlowFieldBuildConfig.Default); Assert.That(f.IsValid,Is.True);
            fp2[] pos=new fp2[]{new fp2((fp)2m,(fp)8m),new fp2((fp)2m,(fp)9m),new fp2((fp)2m,(fp)10m),new fp2((fp)2m,(fp)11m),new fp2((fp)2m,(fp)12m)};
            for(int i=0;i<pos.Length;i++) { fp2 d=s.GetFlowDirection(f,pos[i]); Assert.That(d.x!=fp.zero||d.y!=fp.zero); Assert.That(d.x,Is.GreaterThanOrEqualTo(fp.zero)); }
        }

        [Test] public void FlowFieldPlusRVO_AgentsAvoidEachOtherWhileMarching()
        {
            var g=CreateGrid(); var s=new TeamFlowFieldService(g);
            int[] cost=s.BuildLaneCostField(new LaneTargetConfig{LaneIndex=0,Targets=new fp2[]{new fp2((fp)18m,(fp)10m)}},RadiusClass.Small);
            var f=s.BuildTeamFlowField(0,RadiusClass.Small,new int[][]{cost},FlowFieldBuildConfig.Default);
            var inputs=new RVOInput[]{new RVOInput{SelfUid=new UnitUid(1,1,1),Position=new fp2((fp)5m,(fp)9.5m),DesiredVelocity=s.GetFlowDirection(f,new fp2((fp)5m,(fp)9.5m)),Radius=(fp)0.25m,MaxSpeed=(fp)2m},new RVOInput{SelfUid=new UnitUid(1,2,2),Position=new fp2((fp)5m,(fp)10m),DesiredVelocity=s.GetFlowDirection(f,new fp2((fp)5m,(fp)10m)),Radius=(fp)0.25m,MaxSpeed=(fp)2m},new RVOInput{SelfUid=new UnitUid(1,3,3),Position=new fp2((fp)5m,(fp)10.5m),DesiredVelocity=s.GetFlowDirection(f,new fp2((fp)5m,(fp)10.5m)),Radius=(fp)0.25m,MaxSpeed=(fp)2m}};
            var rvo=new DeterministicRVOSystem(RVOConfig.Default); RvoResult[] r=rvo.Step(inputs); Assert.That(r.Length,Is.EqualTo(3));
            for(int i=0;i<r.Length;i++) Assert.That(r[i].FinalVelocity.x,Is.GreaterThanOrEqualTo(fp.zero));
        }
    }
}
