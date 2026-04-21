using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

public enum SkillQueryShapeType : byte
{
    Rect = 0,
    Ladder = 1,
    Round = 2,
}

[CreateAssetMenu(fileName = "QueryShapeTargetsToBlackboardNode", menuName = "SkillSystem/Effects/Common/Query Shape Targets To Blackboard")]
public sealed class QueryShapeTargetsToBlackboardNode : SkillEffectNode
{
    [TitleGroup("主范围"), LabelText("形状")]
    public SkillQueryShapeType Shape = SkillQueryShapeType.Rect;

    [TitleGroup("主范围"), LabelText("仅敌方")]
    public bool EnemyOnly = true;

    [TitleGroup("主范围"), LabelText("启用实体类型过滤")]
    public bool UseEntityTypeFilter = false;

    [TitleGroup("主范围"), LabelText("实体类型"), ShowIf(nameof(UseEntityTypeFilter))]
    public SimulationEntityType EntityTypeFilter = SimulationEntityType.Hero;

    [TitleGroup("主范围"), LabelText("结果数量键")]
    public string CountKey = "Targets.Count";

    [TitleGroup("主范围"), LabelText("结果前缀")]
    public string TargetPrefix = "Targets";

    [TitleGroup("主范围/矩形"), LabelText("长度"), ShowIf("@Shape == SkillQueryShapeType.Rect")]
    public float RectLength = 1f;
    [TitleGroup("主范围/矩形"), LabelText("宽度"), ShowIf("@Shape == SkillQueryShapeType.Rect")]
    public float RectWidth = 1f;

    [TitleGroup("主范围/梯形"), LabelText("底宽"), ShowIf("@Shape == SkillQueryShapeType.Ladder")]
    public float LadderBottom = 1f;
    [TitleGroup("主范围/梯形"), LabelText("顶宽"), ShowIf("@Shape == SkillQueryShapeType.Ladder")]
    public float LadderTop = 1f;
    [TitleGroup("主范围/梯形"), LabelText("高度"), ShowIf("@Shape == SkillQueryShapeType.Ladder")]
    public float LadderHeight = 1f;

    [TitleGroup("主范围/圆形"), LabelText("半径"), ShowIf("@Shape == SkillQueryShapeType.Round")]
    public float RoundRadius = 1f;

    [TitleGroup("甜区"), LabelText("启用甜区查询")]
    public bool UseSweetQuery = false;

    [TitleGroup("甜区"), LabelText("甜区数量键"), ShowIf(nameof(UseSweetQuery))]
    public string SweetCountKey = "Sweet.Count";

    [TitleGroup("甜区"), LabelText("甜区前缀"), ShowIf(nameof(UseSweetQuery))]
    public string SweetTargetPrefix = "Sweet";

    [TitleGroup("甜区/矩形"), LabelText("长度"), ShowIf("@UseSweetQuery && Shape == SkillQueryShapeType.Rect")]
    public float SweetRectLength = 1f;
    [TitleGroup("甜区/矩形"), LabelText("宽度"), ShowIf("@UseSweetQuery && Shape == SkillQueryShapeType.Rect")]
    public float SweetRectWidth = 1f;
    [TitleGroup("甜区/矩形"), LabelText("前向偏移"), ShowIf("@UseSweetQuery && Shape == SkillQueryShapeType.Rect")]
    public float SweetRectForwardOffset = 0f;

    [TitleGroup("甜区/梯形"), LabelText("底宽"), ShowIf("@UseSweetQuery && Shape == SkillQueryShapeType.Ladder")]
    public float SweetLadderBottom = 1f;
    [TitleGroup("甜区/梯形"), LabelText("顶宽"), ShowIf("@UseSweetQuery && Shape == SkillQueryShapeType.Ladder")]
    public float SweetLadderTop = 1f;
    [TitleGroup("甜区/梯形"), LabelText("高度"), ShowIf("@UseSweetQuery && Shape == SkillQueryShapeType.Ladder")]
    public float SweetLadderHeight = 1f;
    [TitleGroup("甜区/梯形"), LabelText("前向偏移"), ShowIf("@UseSweetQuery && Shape == SkillQueryShapeType.Ladder")]
    public float SweetLadderForwardOffset = 0f;

    [TitleGroup("甜区/圆形"), LabelText("半径"), ShowIf("@UseSweetQuery && Shape == SkillQueryShapeType.Round")]
    public float SweetRoundRadius = 1f;
    [TitleGroup("甜区/圆形"), LabelText("中心前移"), ShowIf("@UseSweetQuery && Shape == SkillQueryShapeType.Round")]
    public float SweetRoundForwardOffset = 0f;

    public override void Execute(SkillExecution execution, SkillEffectContext context)
    {
        if (context.Caster == null || context.Blackboard == null)
            return;

        var origin = context.Caster.LogicPosition;
        var toward = ResolveToward(context);

        var units = Query(origin, toward, false);
        WriteTargets(context.Blackboard, CountKey, TargetPrefix, units, context.Caster.TeamID);
        Release(units);

        if (!UseSweetQuery)
            return;

        var sweetUnits = Query(origin, toward, true);
        WriteTargets(context.Blackboard, SweetCountKey, SweetTargetPrefix, sweetUnits, context.Caster.TeamID);
        Release(sweetUnits);
    }

    private IReadOnlyList<UnitCore> Query(fp3 origin, fp3 toward, bool sweet)
    {
        return Shape switch
        {
            SkillQueryShapeType.Rect => SpatialQueryUtility.SearchRectRangeUnits(
                UnitManager.Instance.Spawns.Values,
                origin + toward * (fp)(sweet ? SweetRectForwardOffset : 0f),
                toward,
                (fp)(sweet ? SweetRectLength : RectLength),
                (fp)(sweet ? SweetRectWidth : RectWidth),
                default),
            SkillQueryShapeType.Ladder => SpatialQueryUtility.SearchLadderRangeUnits(
                UnitManager.Instance.Spawns.Values,
                origin + toward * (fp)(sweet ? SweetLadderForwardOffset : 0f),
                toward,
                (fp)(sweet ? SweetLadderBottom : LadderBottom),
                (fp)(sweet ? SweetLadderTop : LadderTop),
                (fp)(sweet ? SweetLadderHeight : LadderHeight),
                default),
            SkillQueryShapeType.Round => SpatialQueryUtility.SearchRoundRangeUnits(
                UnitManager.Instance.Spawns.Values,
                origin + toward * (fp)(sweet ? SweetRoundForwardOffset : 0f),
                (fp)(sweet ? SweetRoundRadius : RoundRadius),
                default),
            _ => null
        };
    }

    private void WriteTargets(SkillBlackboard board, string countKey, string prefix, IReadOnlyList<UnitCore> units, int casterTeamId)
    {
        int count = 0;
        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || unit.IsDead)
                    continue;

                if (EnemyOnly && unit.TeamID == casterTeamId)
                    continue;

                if (UseEntityTypeFilter && unit.SimulationEntityType != EntityTypeFilter)
                    continue;

                board.Set($"{prefix}_{count}", unit.UnitID);
                count++;
            }
        }

        board.Set(countKey, count);
    }

    private static void Release(IReadOnlyList<UnitCore> units)
    {
        if (units is List<UnitCore> list)
            ListPool<UnitCore>.Release(list);
    }

    private static fp3 ResolveToward(SkillEffectContext context)
    {
        if (context.AimDirection.HasValue && fpmath.lengthsq(context.AimDirection.Value) > fp.zero)
            return fpmath.normalize(context.AimDirection.Value);

        if (context.TargetPoint.HasValue)
        {
            var delta = context.TargetPoint.Value - context.Caster.LogicPosition;
            if (fpmath.lengthsq(delta) > fp.zero)
                return fpmath.normalize(delta);
        }

        return context.Caster.Direction;
    }
}
