using System.Collections.Generic;
using UnityEngine;

public class LocalController : MonoBehaviour
{
    public static LocalController Local;

    private GamePlayer playerHandle;
    public GamePlayer PlayerHandle => playerHandle;

    private HeroUnit controlledHero;

    private Dictionary<KeyCode, int> keyAbilityMap = new()
    {
        { KeyCode.Q, 1 },
        { KeyCode.W, 2 },
        { KeyCode.E, 3 },
        { KeyCode.R, 4 }
    };

    private bool canControl;

    private void Awake()
    {
        if (Local != null && Local != this)
        {
            DestroyImmediate(gameObject);
            return;
        }
        Local = this;

        canControl = false;
    }

    public void Init(GamePlayer playerHandle, HeroUnit controlledHero)
    {
        this.playerHandle = playerHandle;
        this.controlledHero = controlledHero;
        canControl = true;
    }

    private void Update()
    {
        if (canControl)
        {
            if (controlledHero == null) return;

            foreach (var pair in keyAbilityMap)
            {
                if (Input.GetKeyDown(pair.Key))
                {
                    OnAbilityPress(pair.Value);
                }

                if (Input.GetKeyUp(pair.Key))
                {
                    OnAbilityRelease(pair.Value);
                }
            }
        }
    }

    private void OnAbilityPress(int abilityId)
    {
        var cmd = new AbilityCommand
        {
            CommandType = CommandType.AbilityPress,
            ControlledUnitId = controlledHero.UnitID,
            AbilityId = abilityId
        };

        FrameSyncCoreSystem.Instance.AddCommand(cmd);
    }

    private void OnAbilityRelease(int abilityId)
    {
        Vector3 mouseWorld = GetMouseWorldPosition();

        var cmd = new AbilityCommand
        {
            CommandType = CommandType.AbilityRelease,
            ControlledUnitId = controlledHero.UnitID,
            AbilityId = abilityId,
            HasTargetPosition = true,
            TargetPosition = mouseWorld
        };

        FrameSyncCoreSystem.Instance.AddCommand(cmd);
    }

    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 1000f))
            return hit.point;

        return Vector3.zero;
    }
}
