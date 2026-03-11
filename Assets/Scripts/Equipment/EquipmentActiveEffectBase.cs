using UnityEngine;

public abstract class EquipmentActiveEffectBase : ScriptableObject
{
    public abstract bool TryApply(EquipmentActiveRuntime runtime, EquipmentUseContext useContext);
}