using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "CrowdControlData", menuName = "控制系统/新建控制配置")]
public class CrowdControlData : ScriptableObject
{
    [SerializeField]
    public int Id;
    [LabelText("名字")]
    public string Name;
    [LabelText("优先级")]
    public short Priority;

    [LabelText("是否僵直")]
    public bool IsSiffness;
    [LabelText("禁用行为")]
    public UnitCapability DisableCapability = UnitCapability.None;
    [LabelText("回调")]
    public CrowdControlBaseMoudle OnTakeEffect;
    public CrowdControlBaseMoudle OnTick;
    public CrowdControlBaseMoudle OnWearOff;
}
