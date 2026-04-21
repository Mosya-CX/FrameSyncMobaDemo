using UnityEngine;

public readonly struct SkillRuntimeLifecycleContext
{
    public readonly UnitCore Owner;
    public readonly SkillBook SkillBook;
    public readonly SkillDef SkillDef;
    public readonly SkillRuntime Runtime;

    public SkillRuntimeLifecycleContext(UnitCore owner, SkillBook skillBook, SkillDef skillDef, SkillRuntime runtime)
    {
        Owner = owner;
        SkillBook = skillBook;
        SkillDef = skillDef;
        Runtime = runtime;
    }
}
