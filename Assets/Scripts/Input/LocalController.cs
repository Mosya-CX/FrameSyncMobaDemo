using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 纯本地输入桥接器：
/// - 采集本地键鼠输入
/// - 维护鼠标指向地面点 / 当前悬停单位
/// - 转交 HeroInputHandler
/// 
/// 不参与网络同步，不参与帧逻辑，只负责把本地玩家输入转换成命令。
/// </summary>
public class LocalController : MonoBehaviour
{
    public static LocalController Instance { get; private set; }
    public static bool HasInstance => Instance != null;
    public static LocalController Local => Instance;

    [Header("射线设置")]
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private LayerMask unitMask = ~0;
    [SerializeField] private float rayDistance = 500f;

    [Header("按键映射")]
    [SerializeField] private KeyCode moveOrAttackKey = KeyCode.Mouse1;
    [SerializeField] private KeyCode cancelCastKey = KeyCode.Escape;

    [SerializeField]
    private List<AbilityKeyBinding> abilityBindings = new()
    {
        new AbilityKeyBinding(KeyCode.Q, 1),
        new AbilityKeyBinding(KeyCode.W, 2),
        new AbilityKeyBinding(KeyCode.E, 3),
        new AbilityKeyBinding(KeyCode.R, 4),
        new AbilityKeyBinding(KeyCode.D, 5),
        new AbilityKeyBinding(KeyCode.F, 6),
    };

    private readonly Dictionary<KeyCode, int> keyAbilityMap = new();

    private GamePlayer playerHandle;
    public GamePlayer PlayerHandle => playerHandle;

    private HeroUnit controlledHero;
    public HeroUnit ControlledHero => controlledHero;

    private HeroInputHandler heroInputHandler;
    public HeroInputHandler HeroInputHandler => heroInputHandler;

    public bool HasControlledHero => controlledHero != null && heroInputHandler != null;

    public UnitUID ControlledUnitUID => HasControlledHero ? controlledHero.UnitID : default;

    public Vector3? MousePosition { get; private set; }
    public UnitCore SelectedUnit { get; private set; }

    public fp3? MousePositionFixedPoint =>
        MousePosition.HasValue
            ? new fp3((fp)MousePosition.Value.x, (fp)MousePosition.Value.y, (fp)MousePosition.Value.z)
            : null;

    public static LocalController GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("LocalController");
        DontDestroyOnLoad(go);
        return go.AddComponent<LocalController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        RebuildAbilityMap();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void BindPlayer(GamePlayer player)
    {
        if (player == null)
            return;

        playerHandle = player;
    }

    public void BindHero(HeroUnit hero)
    {
        if (hero == null)
            return;

        controlledHero = hero;
        heroInputHandler = hero.GetComponent<HeroInputHandler>();

        if (heroInputHandler == null)
        {
            Debug.LogError($"[{nameof(LocalController)}] 绑定失败，{hero.name} 上缺少 HeroInputHandler。");
            return;
        }

        heroInputHandler.Init();

        Debug.Log($"[{nameof(LocalController)}] 已绑定英雄: {hero.name}");
    }

    public void UnbindHero(HeroUnit hero)
    {
        if (hero == null)
            return;

        if (controlledHero != hero)
            return;

        if (heroInputHandler != null)
            heroInputHandler.CancelCurrentIndicator();

        controlledHero = null;
        heroInputHandler = null;
        SelectedUnit = null;
        MousePosition = null;
    }

    public void ClearBindings()
    {
        if (heroInputHandler != null)
            heroInputHandler.CancelCurrentIndicator();

        playerHandle = null;
        controlledHero = null;
        heroInputHandler = null;
        SelectedUnit = null;
        MousePosition = null;
    }

    private void Update()
    {
        if (!HasControlledHero)
            return;

        if (inputCamera == null)
            inputCamera = Camera.main;

        UpdatePointerState();

        // 鼠标右键：移动/攻击
        if (Input.GetKeyDown(moveOrAttackKey))
        {
            var info = BuildInputInfo();
            heroInputHandler.HandleRightMouseInput(info);
        }

        // 技能按键 down / up
        foreach (var pair in keyAbilityMap)
        {
            if (Input.GetKeyDown(pair.Key))
            {
                var info = BuildInputInfo();
                heroInputHandler.HandlePressAbilityButton(pair.Value, info);
            }

            if (Input.GetKeyUp(pair.Key))
            {
                var info = BuildInputInfo();
                heroInputHandler.HandleReleaseAbilityButton(pair.Value, info);
            }
        }

        // 取消预览
        if (Input.GetKeyDown(cancelCastKey))
            heroInputHandler.CancelCurrentIndicator();
    }

    private void UpdatePointerState()
    {
        MousePosition = null;
        SelectedUnit = null;

        if (inputCamera == null)
            return;

        // UI 上方不处理世界点击
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        var ray = inputCamera.ScreenPointToRay(Input.mousePosition);

        // 先找单位
        if (Physics.Raycast(ray, out var unitHit, rayDistance, unitMask, QueryTriggerInteraction.Ignore))
        {
            var unit = unitHit.collider.GetComponentInParent<UnitCore>();
            if (unit != null)
                SelectedUnit = unit;
        }

        // 再找地面
        if (Physics.Raycast(ray, out var groundHit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
            MousePosition = groundHit.point;
    }

    private InputInfo BuildInputInfo()
    {
        return new InputInfo
        {
            mousePosition = MousePositionFixedPoint,
            selectedUnit = SelectedUnit,
        };
    }

    private void RebuildAbilityMap()
    {
        keyAbilityMap.Clear();

        for (int i = 0; i < abilityBindings.Count; i++)
        {
            var binding = abilityBindings[i];
            if (!keyAbilityMap.ContainsKey(binding.Key))
                keyAbilityMap.Add(binding.Key, binding.AbilityId);
        }
    }

    [System.Serializable]
    public struct AbilityKeyBinding
    {
        public KeyCode Key;
        public int AbilityId;

        public AbilityKeyBinding(KeyCode key, int abilityId)
        {
            Key = key;
            AbilityId = abilityId;
        }
    }

    public InputInfo BuildCurrentInputInfo()
    {
        return new InputInfo
        {
            mousePosition = MousePositionFixedPoint,
            selectedUnit = SelectedUnit,
        };
    }
}

/// <summary>
/// 输入快照
/// </summary>
public struct InputInfo
{
    public fp3? mousePosition;
    public UnitCore selectedUnit;
}