using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class LocalController : MonoBehaviour
{
    public static LocalController Local;

    private GamePlayer playerHandle;
    public GamePlayer PlayerHandle => playerHandle;

    public UnitUID ControlledUnitUID => heroInputHandler.owner.UnitID;
    private HeroInputHandler heroInputHandler;

    private Dictionary<KeyCode, int> keyAbilityMap = new()
    {
        { KeyCode.Q, 1 },
        { KeyCode.W, 2 },
        { KeyCode.E, 3 },
        { KeyCode.R, 4 }
    };

    public Vector3? MousePosition {  get; private set; }
    public UnitCore SelectedUnit { get; private set; }

    public fp3? MousePositionFixedPoint => MousePosition.HasValue ? new fp3((fp)MousePosition.Value.x, (fp)MousePosition.Value.y, (fp)MousePosition.Value.z) : null;

    private void Awake()
    {
        if (Local != null && Local != this)
        {
            DestroyImmediate(gameObject);
            return;
        }
        Local = this;
    }

    public void Init(GamePlayer playerHandle, HeroUnit controlledHero)
    {
        this.playerHandle = playerHandle;
        heroInputHandler = controlledHero.GetComponent<HeroInputHandler>();
        heroInputHandler.Init();
    }

    private void Update()
    {
        
    }
}

public struct InputInfo
{
    public fp3? mousePosition;
    public UnitCore selectedUnit;
}