using UnityEngine;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "DecayStatModifierBySkillLevelBuffModule", menuName = "SkillSystem/Buff/Decay Stat Modifier By Skill Level")]
public sealed class DecayStatModifierBySkillLevelBuffModule : BuffBaseModule
{
    [LabelText("技能ID")]
    public int SkillId;

    [LabelText("属性")]
    public UnitStatType StatType = UnitStatType.MoveSpeed;

    [LabelText("修正类型")]
    public StatModifierType ModifierType = StatModifierType.PercentAdd;

    [LabelText("每级初始数值")]
    public float[] InitialValuesBySkillLevel;

    [LabelText("黑板 HandleId 键")]
    public string HandleKey = "Decay.HandleId";

    public override void Apply(BuffCallbackContext context)
    {
        if (context?.Buff?.target == null)
            return;

        var target = context.Buff.target;
        if (target.Stats == null)
            return;

        if (!TryResolveInitialValue(target, out var startValue))
            return;

        if (!TryGetInt(context.Buff, HandleKey, out int handleId))
        {
            var handle = ModifierHandleGenerator.Create();
            handleId = handle.id;
            context.Buff.blackBoard[HandleKey] = handleId;
            context.Buff.RegisterUndoAction(() => target.Stats.RemoveModifier(StatType, new ModifierHandle(handleId)));
        }

        fp ratio = fp.one;
        if (!context.Buff.buffData.isForever && context.Buff.buffData.Duration > 0f)
        {
            ratio = context.Buff.durationTimer / (fp)context.Buff.buffData.Duration;
            if (ratio < fp.zero) ratio = fp.zero;
            if (ratio > fp.one) ratio = fp.one;
        }

        var currentValue = startValue * ratio;
        var handleRef = new ModifierHandle(handleId);

        target.Stats.RemoveModifier(StatType, handleRef);
        target.Stats.AddModifier(StatType, new StatModifier(handleRef, ModifierType, currentValue));
    }

    private bool TryResolveInitialValue(UnitCore target, out fp value)
    {
        value = fp.zero;
        var skillBook = target != null ? target.GetComponent<SkillBook>() : null;
        if (skillBook == null || !skillBook.TryGetRuntime(SkillId, out var runtime))
            return false;

        int level = Mathf.Clamp(runtime.Level, 1, Mathf.Max(1, InitialValuesBySkillLevel != null ? InitialValuesBySkillLevel.Length : 1)) - 1;
        value = InitialValuesBySkillLevel != null && InitialValuesBySkillLevel.Length > 0
            ? (fp)InitialValuesBySkillLevel[Mathf.Clamp(level, 0, InitialValuesBySkillLevel.Length - 1)]
            : fp.zero;
        return true;
    }

    private static bool TryGetInt(BuffInfo buff, string key, out int value)
    {
        if (buff != null && buff.blackBoard != null && buff.blackBoard.TryGetValue(key, out var obj) && obj is int v)
        {
            value = v;
            return true;
        }

        value = 0;
        return false;
    }
}
