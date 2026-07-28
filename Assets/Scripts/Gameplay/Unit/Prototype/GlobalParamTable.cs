using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [CreateAssetMenu(menuName = "MOBA/Global Param Table")]
    public sealed class GlobalParamTable : ScriptableObject
    {
        [Header("Stat Growth")]
        public fp StatGrowthC = (fp)1;
        public fp StatGrowthD = (fp)1;
        [Header("Armor / Magic Resist")]
        public fp ArmorConstant = (fp)100;
        public fp MagicResistConstant = (fp)100;
        [Header("Movement")]
        public fp MoveSpeedToLogicVelocityScale = (fp)0.01;
        public fp ArriveDistanceNormal = (fp)0.1;
        public fp ArriveDistanceAttack = (fp)0.5;
        [Header("Attack")]
        [Min(0)] public int AttackSequenceResetIntervalTicks = 300;

        public static GlobalParamTable CreateDefault()
        {
            var table = CreateInstance<GlobalParamTable>();
            table.StatGrowthC = (fp)1;
            table.StatGrowthD = (fp)1;
            table.ArmorConstant = (fp)100;
            table.MagicResistConstant = (fp)100;
            table.MoveSpeedToLogicVelocityScale = (fp)0.01;
            table.ArriveDistanceNormal = (fp)0.1;
            table.ArriveDistanceAttack = (fp)0.5;
            table.AttackSequenceResetIntervalTicks = 300;
            return table;
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (ArmorConstant <= fp.zero) Debug.LogError("ArmorConstant must be > 0.");
            if (MagicResistConstant <= fp.zero) Debug.LogError("MagicResistConstant must be > 0.");
            if (AttackSequenceResetIntervalTicks <= 0) Debug.LogError("AttackSequenceResetIntervalTicks must be > 0.");
        }
#endif
    }
}
