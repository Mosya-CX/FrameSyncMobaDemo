using Unity.Mathematics.FixedPoint;

public sealed class SkillRuntime
{
    public readonly SkillDef Def;

    public int Level { get; private set; }
    public fp CooldownRemaining { get; private set; }
    public SkillBlackboard State { get; private set; }

    public int NextRepeatStepIndex { get; private set; }
    public fp RepeatRemainingTime { get; private set; }

    public bool IsLearned => Level > 0;
    public bool IsCoolingDown => CooldownRemaining > 0;
    public bool HasRepeatWindow => RepeatRemainingTime > 0;

    public SkillRuntime(SkillDef def)
    {
        Def = def;
        Level = 1;
        State = new SkillBlackboard();
        NextRepeatStepIndex = 0;
        RepeatRemainingTime = fp.zero;
    }

    public void SetLevel(int level)
    {
        Level = level < 0 ? 0 : level;
    }

    public void Tick(fp dt)
    {
        if (CooldownRemaining > fp.zero)
        {
            CooldownRemaining -= dt;
            if (CooldownRemaining < fp.zero)
                CooldownRemaining = fp.zero;
        }

        if (RepeatRemainingTime > fp.zero)
        {
            RepeatRemainingTime -= dt;
            if (RepeatRemainingTime < fp.zero)
                RepeatRemainingTime = fp.zero;
        }
    }

    public int ResolveCastStepIndex()
    {
        if (!HasRepeatWindow)
            NextRepeatStepIndex = 0;

        return NextRepeatStepIndex;
    }

    public void BeginRepeatWindow(int nextStepIndex, fp duration)
    {
        NextRepeatStepIndex = nextStepIndex < 0 ? 0 : nextStepIndex;
        RepeatRemainingTime = duration <= fp.zero ? fp.zero : duration;
    }

    public void ClearRepeatWindow()
    {
        NextRepeatStepIndex = 0;
        RepeatRemainingTime = fp.zero;
    }

    public void StartCooldown()
    {
        if (Def == null || Def.Cooldown <= 0f)
        {
            CooldownRemaining = fp.zero;
            return;
        }

        CooldownRemaining = (fp)Def.Cooldown;
    }

    public void StartCooldown(fp duration)
    {
        CooldownRemaining = duration <= fp.zero ? fp.zero : duration;
    }

    public void ModifyCooldown(fp delta)
    {
        CooldownRemaining += delta;
        if (CooldownRemaining < fp.zero)
            CooldownRemaining = fp.zero;
    }

    public void ResetCooldown()
    {
        CooldownRemaining = fp.zero;
    }

    public SkillRuntimeSnapshot CaptureSnapshot()
    {
        return new SkillRuntimeSnapshot
        {
            SkillId = Def != null ? Def.Id : 0,
            Level = Level,
            CooldownRemaining = CooldownRemaining,
            NextRepeatStepIndex = NextRepeatStepIndex,
            RepeatRemainingTime = RepeatRemainingTime,
            State = State != null ? State.CaptureSnapshot() : default,
        };
    }

    public void RestoreSnapshot(SkillRuntimeSnapshot snapshot)
    {
        Level = snapshot.Level;
        CooldownRemaining = snapshot.CooldownRemaining;
        NextRepeatStepIndex = snapshot.NextRepeatStepIndex;
        RepeatRemainingTime = snapshot.RepeatRemainingTime;

        State ??= new SkillBlackboard();
        State.RestoreSnapshot(snapshot.State);
    }
}

public struct SkillRuntimeSnapshot
{
    public int SkillId;
    public int Level;
    public fp CooldownRemaining;
    public int NextRepeatStepIndex;
    public fp RepeatRemainingTime;
    public SkillBlackboardSnapshot State;
}
