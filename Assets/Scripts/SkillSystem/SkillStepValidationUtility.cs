public static class SkillStepValidationUtility
{
    public static bool Validate(SkillDef def, out string reason)
    {
        reason = null;

        if (def == null)
        {
            reason = "SkillDef is null.";
            return false;
        }

        if (def.Steps == null || def.Steps.Length == 0)
        {
            reason = $"Skill '{def.SkillName}' 缺少 Steps。";
            return false;
        }

        SkillStepDef previous = null;
        for (int i = 0; i < def.Steps.Length; i++)
        {
            var current = def.Steps[i];
            if (current == null)
            {
                reason = $"Skill '{def.SkillName}' 的 Step[{i}] 为 null。";
                return false;
            }

            if (!current.CanFollow(previous, out reason))
            {
                reason = $"Skill '{def.SkillName}' 的 Step[{i}] 非法：{reason}";
                return false;
            }

            previous = current;
        }

        return true;
    }
}
