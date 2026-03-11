using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "CrowdControlData", menuName = "控制系统/新建控制配置")]
public class CrowdControlData : ScriptableObject
{
    [SerializeField] public int Id;
    [LabelText("名字")] public string Name;
    [LabelText("优先级")] public short Priority;

    [LabelText("控制类型")] public ControlType Type = ControlType.None;

    [Title("输入限制")]
    public bool BlockMoveInput;
    public bool BlockAttackInput;
    public bool BlockCastInput;

    [Title("行为限制")]
    public bool BlockMove;
    public bool BlockTrack;
    public bool BlockAttack;
    public bool BlockCast;
    public bool BlockDash;

    [Title("是否强制打断当前技能/攻击/Dash")]
    public bool ForceInterruptCast = true;
    public bool ForceInterruptAttack = true;
    public bool ForceInterruptDash = true;

    [Title("移动倍率")]
    public float MoveSpeedMultiplier = 1f;

    [Title("特殊行为")]
    public ControlBehaviorBase SpecialBehavior;

    [Title("回调")]
    public CrowdControlBaseMoudle OnTakeEffect;
    public CrowdControlBaseMoudle OnTick;
    public CrowdControlBaseMoudle OnWearOff;
}