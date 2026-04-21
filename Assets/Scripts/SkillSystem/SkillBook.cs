using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using Sirenix.OdinInspector;

public sealed class SkillBook : UnitBaseHandler
{
    [SerializeField, TitleGroup("默认技能"), LabelText("默认技能列表")]
    private SkillDef[] defaultSkills;

    private readonly Dictionary<int, SkillRuntime> runtimeTable = new();

    public IReadOnlyDictionary<int, SkillRuntime> RuntimeTable => runtimeTable;

    protected override void Awake()
    {
        base.Awake();

        runtimeTable.Clear();

        if (defaultSkills == null)
            return;

        for (int i = 0; i < defaultSkills.Length; i++)
        {
            CreateRuntime(defaultSkills[i], 1);
        }
    }

    public override void Tick(fp deltaTime)
    {
        foreach (var runtime in runtimeTable.Values)
        {
            runtime.Tick(deltaTime);

            if (runtime.Def != null &&
                runtime.Def.UseRepeatCast &&
                runtime.RepeatRemainingTime <= fp.zero &&
                runtime.NextRepeatStepIndex > 0)
            {
                if (runtime.Def.StartCooldownOnRepeatTimeout)
                    runtime.StartCooldown();

                runtime.ClearRepeatWindow();
            }
        }
    }

    public bool TryGetRuntime(int skillId, out SkillRuntime runtime) => runtimeTable.TryGetValue(skillId, out runtime);

    public bool TryGetDef(int skillId, out SkillDef def)
    {
        if (runtimeTable.TryGetValue(skillId, out var runtime) && runtime.Def != null)
        {
            def = runtime.Def;
            return true;
        }

        def = null;
        return false;
    }

    public void AddSkill(SkillDef def, int level = 1)
    {
        if (def == null)
            return;

        if (runtimeTable.TryGetValue(def.Id, out var runtime))
        {
            runtime.SetLevel(level);
            return;
        }

        CreateRuntime(def, level);
    }

    public bool RemoveSkill(int skillId)
    {
        return runtimeTable.Remove(skillId);
    }

    private void CreateRuntime(SkillDef def, int level)
    {
        if (def == null || runtimeTable.ContainsKey(def.Id))
            return;

        var runtime = new SkillRuntime(def);
        runtime.SetLevel(level);
        runtimeTable.Add(def.Id, runtime);
    }

    public override object CaptureState()
    {
        var snapshots = new List<SkillRuntimeSnapshot>(runtimeTable.Count);
        foreach (var pair in runtimeTable)
            snapshots.Add(pair.Value.CaptureSnapshot());

        return snapshots.ToArray();
    }

    public override void RestoreState(object state)
    {
        if (state is not SkillRuntimeSnapshot[] snapshots)
            return;

        foreach (var snap in snapshots)
        {
            if (runtimeTable.TryGetValue(snap.SkillId, out var runtime))
                runtime.RestoreSnapshot(snap);
        }
    }
}
