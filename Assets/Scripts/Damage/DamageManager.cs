using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class DamageManager : MonoSingleton<DamageManager>, IStateful
{
    private Queue<DamageInfo> damageRequestQueue = new();

    public void Begin()
    {

    }

    public void Clean()
    {
        damageRequestQueue.Clear();
    }

    public IEnumerator Init()
    {
        yield break;
    }

    public void Tick(uint currentTick)
    {
        while (damageRequestQueue.Count > 0)
            damageRequestQueue.Peek().Target.GetDamage(damageRequestQueue.Dequeue());
    }

    #region 伤害信息请求
    public void CreateAttackDamageRequest(UnitCore source, UnitCore target)
    {
        var damageInfo = new DamageInfo
        {
            sourceUid = source.UnitID,
            targetUid = target.UnitID,
            basicPhysicalDamage = source.AttackDamage,
            physicalDamageMultiplier = target.Stats.PhysicalDamageReduction,
            magicalDamageMultiplier = target.Stats.MagicDamageReduction,
        };

        #region Tags处理
        damageInfo.tags.Add(DamageTagConst.FromAttack);

        AutoAddTagsBySourceInfoAndTargetInfo(ref damageInfo);
        #endregion

        source.DamageModifier?.Invoke(ref damageInfo);
        target.DamageModifier?.Invoke(ref damageInfo);

        damageRequestQueue.Enqueue(damageInfo);
    }

    public void CreateAbilityDamageRequest(UnitCore source, UnitCore target, fp basePhysicsDamage, fp baseMagicDamage, string[] additionalTags)
    {
        var damageInfo = new DamageInfo
        {
            sourceUid = source.UnitID,
            targetUid = target.UnitID,
            basicPhysicalDamage = basePhysicsDamage,
            basicMagicalDamage = baseMagicDamage,
            physicalDamageMultiplier = target.Stats.PhysicalDamageReduction,
            magicalDamageMultiplier = target.Stats.MagicDamageReduction,
        };

        #region Tag处理
        if (additionalTags != null)
            for (int i = 0; i < additionalTags.Length; i++)
                damageInfo.tags.Add(additionalTags[i]);

        damageInfo.tags.Add(DamageTagConst.FromAbility);

        AutoAddTagsBySourceInfoAndTargetInfo(ref damageInfo);
        #endregion

        source.DamageModifier?.Invoke(ref damageInfo);
        target.DamageModifier?.Invoke(ref damageInfo);

        damageRequestQueue.Enqueue(damageInfo);
    }

    public void CreateBuffDamageRequest(UnitCore source, UnitCore target, fp basePhysicsDamage, fp baseMagicDamage, string[] additionalTags)
    {
        var damageInfo = new DamageInfo
        {
            sourceUid = source.UnitID,
            targetUid = target.UnitID,
            basicPhysicalDamage = basePhysicsDamage,
            basicMagicalDamage = baseMagicDamage,
            physicalDamageMultiplier = target.Stats.PhysicalDamageReduction,
            magicalDamageMultiplier = target.Stats.MagicDamageReduction,
        };

        #region Tag处理
        if (additionalTags != null)
            for (int i = 0; i < additionalTags.Length; i++)
                damageInfo.tags.Add(additionalTags[i]);

        damageInfo.tags.Add(DamageTagConst.FromBuff);

        AutoAddTagsBySourceInfoAndTargetInfo(ref damageInfo);
        #endregion

        source.DamageModifier?.Invoke(ref damageInfo);
        target.DamageModifier?.Invoke(ref damageInfo);

        damageRequestQueue.Enqueue(damageInfo);
    }

    public void CreateEquipmentDamageRequest(UnitCore source, UnitCore target, fp basePhysicsDamage, fp baseMagicDamage, string[] additionalTags)
    {
        var damageInfo = new DamageInfo
        {
            sourceUid = source.UnitID,
            targetUid = target.UnitID,
            basicPhysicalDamage = basePhysicsDamage,
            basicMagicalDamage = baseMagicDamage,
            physicalDamageMultiplier = target.Stats.PhysicalDamageReduction,
            magicalDamageMultiplier = target.Stats.MagicDamageReduction,
        };

        #region Tag处理
        if (additionalTags != null)
            for (int i = 0; i < additionalTags.Length; i++)
                damageInfo.tags.Add(additionalTags[i]);

        damageInfo.tags.Add(DamageTagConst.FromEquipment);

        AutoAddTagsBySourceInfoAndTargetInfo(ref damageInfo);
        #endregion

        source.DamageModifier?.Invoke(ref damageInfo);
        target.DamageModifier?.Invoke(ref damageInfo);

        damageRequestQueue.Enqueue(damageInfo);
    }
    
    private void AutoAddTagsBySourceInfoAndTargetInfo(ref DamageInfo damageInfo)
    {
        if (damageInfo.Source.CompareTag("Hero"))
            damageInfo.tags.Add(DamageTagConst.FromHero);
        else if (damageInfo.Source.CompareTag("Mob"))
            damageInfo.tags.Add(DamageTagConst.FromMob);
        else if (damageInfo.Source.CompareTag("Monster"))
            damageInfo.tags.Add(DamageTagConst.FromMonster);

        if (damageInfo.Target.CompareTag("Hero"))
            damageInfo.tags.Add(DamageTagConst.ToHero);
        else if (damageInfo.Target.CompareTag("Mob"))
            damageInfo.tags.Add(DamageTagConst.ToMob);
        else if (damageInfo.Target.CompareTag("Monster"))
            damageInfo.tags.Add(DamageTagConst.ToMonster);
    }
    
    #endregion

    #region 快照和回滚
    public object CaptureState()
    {
        throw new System.NotImplementedException();
    }

    public void RestoreState(object state)
    {
        throw new System.NotImplementedException();
    }
    #endregion
}
