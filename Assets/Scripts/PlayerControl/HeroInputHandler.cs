using Sirenix.OdinInspector;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

// 玩家控制器和英雄单位之间的中间件
// 负责根据玩家控制器的输入数据处理成对应的命令并将命令传递给命令发送队列
// 负责定义英雄施法方案
// 技能指示器控制
public class HeroInputHandler : MonoBehaviour
{
    [HideInInspector]
    public HeroUnit owner;

    private void Awake()
    {
        enabled = false;
    }

    public void Init()
    {
        if (enabled)
            return;

        enabled = true;
        owner = GetComponent<HeroUnit>();

        for (int i = 0; i < abilityTriggerSchemes.Count; i++)
        {
            List<AbilityIndicatorRuntime> indicatorRuntimeList = new();
            for (int j = 0; j < abilityTriggerSchemes[i].phaseSchemes.Count; j++)
                indicatorRuntimeList.Add(new AbilityIndicatorRuntime(abilityTriggerSchemes[i].phaseSchemes[j]));
            indicatorRuntimeDict.Add(abilityTriggerSchemes[i].abilityId, indicatorRuntimeList);
        }
    }

    #region 输入处理
    public void HandleRightMouseInput(in InputInfo info)
    {
        if (owner.CurrentActionState == UnitActionState.Siffness)
            return;

        if (TryGetAbilityPhaseScheme(currentActiveIndicatorKey.Value.abilityId, currentActiveIndicatorKey.Value.phaseIndex, out var scheme))
            if (scheme.triggerMode != AbilityTriggerrMode.Charge)
                CancelCurrentIndicator();

        // 验证可行性
        if (info.selectedUnit != null && owner.capability.HasFlag(UnitCapability.Attack) && 
            info.selectedUnit.TeamID != owner.TeamID && info.selectedUnit.CurrentActionState != UnitActionState.Dead)
            SendAttackCommand(info.selectedUnit.UnitID);
        else if (info.mousePosition.HasValue && owner.capability.HasFlag(UnitCapability.Move))
            SendMoveCommand(info.mousePosition.Value);
    }

    public void SendAttackCommand(UnitUID targetUid)
    {
        var command = new AttackCommand
        {
            ReceiverUnitId = owner.UnitID,
            TargetUnitId = targetUid,
        };
        FrameSyncCoreSystem.Instance.AddPendingCommand(command);
    }

    public void SendMoveCommand(fp3 targetPosition)
    {
        var command = new MoveCommand
        {
            ReceiverUnitId = owner.UnitID,
            TargetPosition = targetPosition,
        };
        FrameSyncCoreSystem.Instance.AddPendingCommand(command);
    }

    public void HandlePressAbilityButton(in int abilityId, in InputInfo inputInfo)
    {
        if (owner.CurrentActionState == UnitActionState.Siffness)
            return;

        if (owner.AbilityHandler.abilities.TryGetValue(abilityId, out var abilityInfo))
        {
            if (abilityInfo.CanTrigger(inputInfo))
            {
                var currentPhaseIndex = abilityInfo.currentPhaseIndex;
                if (TryGetIndicatorRuntime((abilityId, currentPhaseIndex), out var indicatorRuntime))
                {
                    switch (indicatorRuntime.phaseIndicatorData.triggerMode)
                    {
                        case AbilityTriggerrMode.PressTrigger:
                            SendAbilityCommand(abilityId, inputInfo.mousePosition, inputInfo.selectedUnit.UnitID);
                            break;
                        case AbilityTriggerrMode.ReleaseTrigger:
                            SwitchAbilityIndicator((abilityId, currentPhaseIndex), true);
                            break;
                        case AbilityTriggerrMode.Charge:
                            SendAbilityCommand(abilityId, inputInfo.mousePosition, inputInfo.selectedUnit.UnitID);
                            SwitchAbilityIndicator((abilityId, currentPhaseIndex), true);
                            break;  
                    }
                }
            }
            else
            {
                Debug.Log("当前技能不可用");
            }
        }
    }

    public void HandleReleaseAbilityButton(int abilityId, in InputInfo inputInfo)
    {
        if (owner.CurrentActionState == UnitActionState.Siffness)
            return;

        if (owner.AbilityHandler.abilities.TryGetValue(abilityId, out var abilityInfo))
        {
            if (abilityInfo.CanTrigger(inputInfo))
            {
                var currentPhaseIndex = abilityInfo.currentPhaseIndex;
                if (TryGetIndicatorRuntime((abilityId, currentPhaseIndex), out var indicatorRuntime))
                {
                    SendAbilityCommand(abilityId, inputInfo.mousePosition, inputInfo.selectedUnit.UnitID);
                    switch (indicatorRuntime.phaseIndicatorData.triggerMode)
                    {
                        case AbilityTriggerrMode.ReleaseTrigger:
                            SwitchAbilityIndicator((abilityId, currentPhaseIndex), false);
                            break;
                        case AbilityTriggerrMode.Charge:
                            SwitchAbilityIndicator((abilityId, currentPhaseIndex), false);
                            break;
                    }
                }
            }
            else
            {
                Debug.Log("当前技能不可用");
            }
        }
    }

    public void SendAbilityCommand(in int abilityId, in fp3? targetPosition, in UnitUID? targetUid)
    {
        var abilityCommand = new AbilityCommand
        {
            ReceiverUnitId = owner.UnitID,
            AbilityId = abilityId,
            context = new AbilityTriggerContext
            {
                TargetPosition = targetPosition,
                TargetUID = targetUid,
            }
        };
        FrameSyncCoreSystem.Instance.AddPendingCommand(abilityCommand);
    }
    #endregion

    #region 技能
    private (int abilityId, int phaseIndex)? currentActiveIndicatorKey;
    
    private readonly Dictionary<int, List<AbilityIndicatorRuntime>> indicatorRuntimeDict = new();

    [SerializeField]
    [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true, DraggableItems = false)]
    public List<AbilityTriggerScheme> abilityTriggerSchemes;

    [System.Serializable]
    public class AbilityTriggerScheme
    {
        [ReadOnly]
        public int abilityId;
        [ReadOnly]
        public string abilityName;

        [SerializeField]
        [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true, DraggableItems = false)]
        public List<PhaseTriggerScheme> phaseSchemes = new();

        [System.Serializable]
        public class PhaseTriggerScheme
        {
            public AbilityTriggerrMode triggerMode;
            [ShowIf("@triggerMode == AbilityTriggerrMode.ReleaseTrigger || triggerMode == AbilityTriggerrMode.Charge")]
            public AbilityIndicatorBase indicator;
        } 
    }

    public enum AbilityTriggerrMode
    {
        None,
        PressTrigger,
        ReleaseTrigger,
        Charge,
    }

    public void SwitchAbilityIndicator(in (int abilityId, int phaseIndex) indicatorKey, in bool active)
    {
        if (!TryGetIndicatorRuntime(indicatorKey, out var indicatorRuntime) || indicatorRuntime.phaseIndicatorData.indicator == null)
            return;

        CancelCurrentIndicator();

        if (active)
            indicatorRuntime.ActiveIndicator();
        else
            indicatorRuntime.InactiveIndicator();
    }

    private bool TryGetIndicatorRuntime(in (int abilityId, int phaseIndex) indicatorKey, out AbilityIndicatorRuntime runtime)
    {
        runtime = null;
        if (indicatorRuntimeDict.TryGetValue(indicatorKey.abilityId, out var runtimeList))
        {
            if (indicatorKey.phaseIndex >= 0 && indicatorKey.phaseIndex < runtimeList.Count)
            {
                runtime = runtimeList[indicatorKey.phaseIndex];
                return true;
            }
        }
        return false;
    }

    public void CancelCurrentIndicator()
    {
        if (currentActiveIndicatorKey != null)
        {
            if (TryGetIndicatorRuntime(currentActiveIndicatorKey.Value, out var indicatorRuntime))
                indicatorRuntime?.InactiveIndicator();
            currentActiveIndicatorKey = null;
        }
    }

    #endregion

    #region 装备和道具

    #endregion

#if UNITY_EDITOR
    [SerializeField, HideInInspector]
    private int LastHashCode;

    private void OnValidate()
    {
        owner = GetComponent<HeroUnit>();

        var abilityList = owner.definitionConfig.abilityList;

        if (abilityList == null)
        {
            LastHashCode = 0;
            abilityTriggerSchemes.Clear();
            return;
        }

        int currentHashCode = abilityList.GetHashCode();
        if (currentHashCode != LastHashCode)
        {
            var newSchemes = new List<AbilityTriggerScheme>();
            for (int i = 0; i < abilityList.Length; i++)
            {
                var newScheme = new AbilityTriggerScheme
                {
                    abilityId = abilityList[i].Id,
                    abilityName = abilityList[i].Name,
                };
                for (int j = 0; j < abilityList[i].Phases.Length; j++)
                    newScheme.phaseSchemes.Add(new());

                newSchemes.Add(newScheme);
            }

            for (int i = 0; i < newSchemes.Count; i++)
            {
                for (int j = 0; j < newSchemes[i].phaseSchemes.Count; j++)
                {
                    if (TryGetAbilityPhaseScheme(newSchemes[i].abilityId, j, out var scheme))
                    {
                        newSchemes[i].phaseSchemes[j].triggerMode = scheme.triggerMode;
                        newSchemes[i].phaseSchemes[j].indicator = scheme.indicator;
                    }
                }
            }
            LastHashCode = currentHashCode;
        }
    }

    private bool TryGetAbilityPhaseScheme(int abilityId, int phaseIndex, out AbilityTriggerScheme.PhaseTriggerScheme phaseTriggerScheme)
    {
        phaseTriggerScheme = null;
        if (abilityTriggerSchemes == null)
            return false;

        for (int i = 0; i < abilityTriggerSchemes.Count; i++)
        {
            if (abilityTriggerSchemes[i].abilityId == abilityId)
            {
                if (abilityTriggerSchemes[i].phaseSchemes == null)
                    return false;

                if (phaseIndex < 0 || phaseIndex >= abilityTriggerSchemes[i].phaseSchemes.Count)
                    return false;

                phaseTriggerScheme = abilityTriggerSchemes[i].phaseSchemes[phaseIndex];
                return true;
            }
        }
        return false;
    }

#endif
}
