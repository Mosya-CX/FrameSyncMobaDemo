using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class DamageManager : MonoSingleton<DamageManager>
{
    private readonly Queue<DamageRequest> damageRequestQueue = new();

    public void Clean()
    {
        damageRequestQueue.Clear();
    }

    public void Tick(uint currentTick)
    {
        while (damageRequestQueue.Count > 0)
        {
            var request = damageRequestQueue.Dequeue();
            ProcessDamageRequest(request, currentTick);
        }
    }

    public void CreateAttackDamageRequest(UnitCore source, UnitCore target)
    {
        var request = new DamageRequest
        {
            Source = source,
            Target = target,
            SourceKind = DamageSourceKind.Attack,
            BasePhysicalDamage = source.AttackDamage,
            BaseMagicalDamage = 0,
            CanCrit = true,
        };

        request.Tags.Add(DamageTagConst.FromAttack);
        AutoAddTagsBySourceAndTarget(request, source, target);

        damageRequestQueue.Enqueue(request);
    }

    public void CreateAbilityDamageRequest(UnitCore source, UnitCore target, fp basePhysicalDamage, fp baseMagicalDamage, params string[] additionalTags)
    {
        var request = new DamageRequest
        {
            Source = source,
            Target = target,
            SourceKind = DamageSourceKind.Ability,
            BasePhysicalDamage = basePhysicalDamage,
            BaseMagicalDamage = baseMagicalDamage,
            CanCrit = false,
        };

        request.Tags.Add(DamageTagConst.FromAbility);
        AddTags(request, additionalTags);
        AutoAddTagsBySourceAndTarget(request, source, target);

        damageRequestQueue.Enqueue(request);
    }

    public void CreateBuffDamageRequest(UnitCore source, UnitCore target, fp basePhysicalDamage, fp baseMagicalDamage, string[] additionalTags)
    {
        var request = new DamageRequest
        {
            Source = source,
            Target = target,
            SourceKind = DamageSourceKind.Buff,
            BasePhysicalDamage = basePhysicalDamage,
            BaseMagicalDamage = baseMagicalDamage,
            CanCrit = false,
        };

        request.Tags.Add(DamageTagConst.FromBuff);
        AddTags(request, additionalTags);
        AutoAddTagsBySourceAndTarget(request, source, target);

        damageRequestQueue.Enqueue(request);
    }

    public void CreateEquipmentDamageRequest(UnitCore source, UnitCore target, fp basePhysicalDamage, fp baseMagicalDamage, string[] additionalTags)
    {
        var request = new DamageRequest
        {
            Source = source,
            Target = target,
            SourceKind = DamageSourceKind.Equipment,
            BasePhysicalDamage = basePhysicalDamage,
            BaseMagicalDamage = baseMagicalDamage,
            CanCrit = false,
        };

        request.Tags.Add(DamageTagConst.FromEquipment);
        AddTags(request, additionalTags);
        AutoAddTagsBySourceAndTarget(request, source, target);

        damageRequestQueue.Enqueue(request);
    }

    private void ProcessDamageRequest(DamageRequest request, uint currentTick)
    {
        if (request.Source == null)
            return;
        if (request.Target == null || request.Target.IsDead)
            return;

        var source = request.Source;
        var target = request.Target;
        var context = new DamageContext
        {
            Source = source,
            Target = target,
            SourceKind = request.SourceKind,
            BasePhysicalDamage = request.BasePhysicalDamage,
            BaseMagicalDamage = request.BaseMagicalDamage,
            Tags = new HashSet<string>(request.Tags),
            Extra = request.Extra,
            PhysicalReductionMultiplier = target.Stats.PhysicalDamageReduction,
            MagicalReductionMultiplier = target.Stats.MagicDamageReduction,
        };

        // 暴击示例：这里只保留最基础逻辑，后续可抽随机服务
        if (request.CanCrit && source.CritChance > 0)
        {
            context.IsCrit = true;
            context.CritMultiplier = source.Stats.Get(UnitStatType.CritMultiplier);
        }

        ApplyOutgoingDamageModifiers(source, context);
        ApplyIncomingDamageModifiers(target, context);

        // 汇总前
        fp physicalBeforeReduction = (context.BasePhysicalDamage + context.BonusPhysicalDamage) * context.PhysicalMultiplier;
        fp magicalBeforeReduction = (context.BaseMagicalDamage + context.BonusMagicalDamage) * context.MagicalMultiplier;

        if (context.IsCrit)
        {
            physicalBeforeReduction = physicalBeforeReduction * context.CritMultiplier + context.CritBonusDamage;
            magicalBeforeReduction = magicalBeforeReduction * context.CritMultiplier + context.CritBonusDamage;
        }

        context.FinalPhysicalBeforeReduction = physicalBeforeReduction;
        context.FinalMagicalBeforeReduction = magicalBeforeReduction;

        fp finalPhysical = physicalBeforeReduction * context.PhysicalReductionMultiplier;
        fp finalMagical = magicalBeforeReduction * context.MagicalReductionMultiplier;

        var result = new DamageResult(
            source,
            target,
            finalPhysical,
            finalMagical,
            context.IsCrit,
            new List<string>(context.Tags));

        target.ApplyDamageResult(result, currentTick);
        source.OnDamageDealt(result);
        target.OnDamageTaken(result);
    }

    private void ApplyOutgoingDamageModifiers(UnitCore source, DamageContext context)
    {
        source.ModifyOutgoingDamage(context);
    }

    private void ApplyIncomingDamageModifiers(UnitCore target, DamageContext context)
    {
        target.ModifyIncomingDamage(context);
    }

    private void AddTags(DamageRequest request, string[] tags)
    {
        if (tags == null)
            return;

        for (int i = 0; i < tags.Length; i++)
            request.Tags.Add(tags[i]);
    }

    private void AutoAddTagsBySourceAndTarget(DamageRequest request, UnitCore source, UnitCore target)
    {
        switch (source.SimulationEntityType)
        {
            case SimulationEntityType.Hero: request.Tags.Add(DamageTagConst.FromHero); break;
            case SimulationEntityType.Minion: request.Tags.Add(DamageTagConst.FromMob); break;
            case SimulationEntityType.Monster: request.Tags.Add(DamageTagConst.FromMonster); break;
        }

        switch (target.SimulationEntityType)
        {
            case SimulationEntityType.Hero: request.Tags.Add(DamageTagConst.ToHero); break;
            case SimulationEntityType.Minion: request.Tags.Add(DamageTagConst.ToMob); break;
            case SimulationEntityType.Monster: request.Tags.Add(DamageTagConst.ToMonster); break;
        }
    }

    [System.Serializable]
    public class DamageManagerSnapshot
    {
        public List<DamageRequestSnapshot> Requests = new();
    }

    [System.Serializable]
    public class DamageRequestSnapshot
    {
        public UnitUID SourceUid;
        public UnitUID TargetUid;
        public DamageSourceKind SourceKind;
        public fp BasePhysicalDamage;
        public fp BaseMagicalDamage;
        public bool CanCrit;
        public List<string> Tags = new();
    }

    public object CaptureState()
    {
        var snap = new DamageManagerSnapshot();

        foreach (var req in damageRequestQueue)
        {
            snap.Requests.Add(new DamageRequestSnapshot
            {
                SourceUid = req.Source.UnitID,
                TargetUid = req.Target.UnitID,
                SourceKind = req.SourceKind,
                BasePhysicalDamage = req.BasePhysicalDamage,
                BaseMagicalDamage = req.BaseMagicalDamage,
                CanCrit = req.CanCrit,
                Tags = new List<string>(req.Tags),
            });
        }

        return snap;
    }

    public void RestoreState(object state)
    {
        damageRequestQueue.Clear();

        if (state is not DamageManagerSnapshot snap)
            return;

        for (int i = 0; i < snap.Requests.Count; i++)
        {
            var item = snap.Requests[i];
            var req = new DamageRequest
            {
                Source = UnitManager.Instance.GetActiveUnit(item.SourceUid),
                Target = UnitManager.Instance.GetActiveUnit(item.TargetUid),
                SourceKind = item.SourceKind,
                BasePhysicalDamage = item.BasePhysicalDamage,
                BaseMagicalDamage = item.BaseMagicalDamage,
                CanCrit = item.CanCrit,
            };

            for (int j = 0; j < item.Tags.Count; j++)
                req.Tags.Add(item.Tags[j]);

            damageRequestQueue.Enqueue(req);
        }
    }
}