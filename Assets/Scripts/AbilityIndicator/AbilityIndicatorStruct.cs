using UnityEngine;
using static HeroInputHandler.AbilityTriggerScheme;

public class AbilityIndicatorRuntime
{
    public PhaseTriggerScheme phaseIndicatorData;

    public AbilityIndicatorRuntime(PhaseTriggerScheme phaseIndicatorData)
    {
        this.phaseIndicatorData = phaseIndicatorData;
        phaseIndicatorData.indicator?.OnCreate(this);
    }

    public float updateTimer = 0;

    #region ¿ì½Ý·ÃÎÊ
    public Vector3? mousePosition => LocalController.Local.MousePosition;
    public UnitCore selectedUnit => LocalController.Local.SelectedUnit;
    #endregion

    public void ActiveIndicator()
    {
        updateTimer = 0;
        phaseIndicatorData.indicator.ActiveIndicator(this);
    }

    public void UpdateIndicator(float deltaTime)
    {
        updateTimer += deltaTime;
        phaseIndicatorData.indicator.UpdateIndicator(this);
    }

    public void InactiveIndicator()
    {
        phaseIndicatorData.indicator.ActiveIndicator(this);
        updateTimer = 0;
    }
}
