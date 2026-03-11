using UnityEngine;

public abstract class EquipmentConditionBase : ScriptableObject
{
    public virtual bool CanTriggerPassive(EquipmentPassiveRuntime runtime) => true;
    public virtual bool CanUseActive(EquipmentActiveRuntime runtime, EquipmentUseContext useContext) => true;
}