using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class AreaDamageStageDefAuthoring : StageDefAuthoring
    {
        [Min(0f)]
        [SerializeField] private float radius = 3f;
        [Min(0f)]
        [SerializeField] private float baseDamage = 50f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private UnitTargetFilter targetFilter = UnitTargetFilter.Default;
        [Min(0)]
        [SerializeField] private int vfxDefId;
        [Min(0)]
        [SerializeField] private int groundProjectileDefId;

        public float Radius => radius;
        public float BaseDamage => baseDamage;
        public DamageType DamageType => damageType;
        public UnitTargetFilter TargetFilter => targetFilter;
        public int VfxDefId => vfxDefId;
        public int GroundProjectileDefId =>
            groundProjectileDefId;

        public override StageDef Bake()
        {
            return new AreaDamageStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                Radius = (fp)radius,
                BaseDamage = (fp)baseDamage,
                DamageType = damageType,
                TargetFilter = targetFilter,
                VfxDefId = vfxDefId,
                GroundProjectileDefId =
                    groundProjectileDefId,
            };
        }
    }
}
