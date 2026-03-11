using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class EquipmentPassiveEffectBase : ScriptableObject
{
    public abstract void OnEquip(EquipmentPassiveRuntime runtime);
    public abstract void OnUnequip(EquipmentPassiveRuntime runtime);
    public abstract void OnTick(EquipmentPassiveRuntime runtime, fp dt, uint currentTick);
    public abstract bool TryApply(EquipmentPassiveRuntime runtime);
}