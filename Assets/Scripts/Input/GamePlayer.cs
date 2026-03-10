using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 网络玩家句柄：
/// 1. 标识哪个 NetworkObject 代表一个玩家
/// 2. 存储该玩家当前选择的英雄 prefab / 队伍
/// 3. 在本地拥有权到手后，负责与 LocalController 对接
/// </summary>
public class GamePlayer : NetworkBehaviour
{
    public static GamePlayer Local { get; private set; }

    /// <summary>
    /// -1 代表还未选择英雄
    /// </summary>
    public readonly NetworkVariable<int> selectedHeroPrefabId =
        new(-1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

    /// <summary>
    /// 0=未分配, 1=中立, 2=蓝方, 3=红方
    /// </summary>
    public readonly NetworkVariable<byte> teamID =
        new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    /// <summary>
    /// 当前这个玩家在本地控制的英雄。
    /// 不是网络同步数据，只是本地缓存。
    /// </summary>
    public HeroUnit ControlledHero { get; private set; }

    public bool HasSelectedHero => selectedHeroPrefabId.Value >= 0;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        gameObject.name = $"GamePlayer[{OwnerClientId}]";
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        Local = this;

        // 如果场景里已经有 LocalController，就绑定自己
        var localController = LocalController.GetOrCreate();
        localController.BindPlayer(this);

        // 如果英雄已经先生成了，再尝试重绑一次
        if (ControlledHero != null)
            localController.BindHero(ControlledHero);
    }

    public override void OnLostOwnership()
    {
        base.OnLostOwnership();

        if (Local == this)
        {
            Local.UnbindControlledHero();
            Local = null;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this)
        {
            Local.UnbindControlledHero();
            Local = null;
        }

        base.OnNetworkDespawn();
    }

    /// <summary>
    /// 本地 UI 选英雄时可调用。
    /// </summary>
    public void SelectHero(int heroPrefabId)
    {
        if (!IsOwner)
        {
            Debug.LogWarning("只有拥有者可以选择英雄。");
            return;
        }

        selectedHeroPrefabId.Value = heroPrefabId;
    }

    /// <summary>
    /// 单位生成完成后，由 HeroUnit / UnitManager 调用。
    /// 只有匹配当前玩家选择的 prefab + team 时，才会绑定为本地控制英雄。
    /// </summary>
    public bool TryRegisterControlledHero(HeroUnit registrant)
    {
        if (registrant == null)
            return false;

        // 没有拥有权，不是本地玩家，不做本地绑定
        if (!IsOwner)
            return false;

        // 还没选英雄
        if (!HasSelectedHero)
            return false;

        // prefab 不匹配
        if (registrant.PrefabId != selectedHeroPrefabId.Value)
            return false;

        // 队伍不匹配
        if (registrant.TeamID != teamID.Value)
            return false;

        ControlledHero = registrant;

        var localController = LocalController.GetOrCreate();
        localController.BindPlayer(this);
        localController.BindHero(registrant);

        Debug.Log($"GamePlayer[{OwnerClientId}] 绑定本地控制英雄: {registrant.name}");
        return true;
    }

    /// <summary>
    /// 英雄销毁/死亡重生替换时可调用。
    /// </summary>
    public void UnregisterControlledHero(HeroUnit registrant)
    {
        if (registrant == null)
            return;

        if (ControlledHero != registrant)
            return;

        ControlledHero = null;

        if (Local == this && LocalController.HasInstance)
            LocalController.Instance.UnbindHero(registrant);
    }

    public void UnbindControlledHero()
    {
        if (LocalController.HasInstance)
            LocalController.Instance.ClearBindings();

        ControlledHero = null;
    }
}