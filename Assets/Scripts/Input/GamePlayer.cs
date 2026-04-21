using Unity.Netcode;
using UnityEngine;

public class GamePlayer : NetworkBehaviour
{
    public static GamePlayer Local { get; private set; }

    public readonly NetworkVariable<int> selectedHeroPrefabId =
        new(-1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

    public readonly NetworkVariable<bool> isHeroLocked =
        new(false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

    public readonly NetworkVariable<byte> teamID =
        new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public HeroUnit ControlledHero { get; private set; }

    public bool HasSelectedHero => selectedHeroPrefabId.Value >= 0;
    public bool IsHeroLocked => isHeroLocked.Value;
    public int SelectedHeroPrefabId => selectedHeroPrefabId.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        gameObject.name = $"GamePlayer[{OwnerClientId}]";
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();

        Local = this;
        LocalController.TryCreate();

        if (ControlledHero != null)
            LocalController.Local.BindHero(ControlledHero);
    }

    public override void OnLostOwnership()
    {
        base.OnLostOwnership();

        if (Local == this)
        {
            UnbindControlledHero();
            Local = null;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this)
        {
            UnbindControlledHero();
            Local = null;
        }

        base.OnNetworkDespawn();
    }

    public void SelectHero(int heroPrefabId)
    {
        if (!IsOwner)
        {
            Debug.LogWarning("只有拥有者可以选择英雄。");
            return;
        }

        selectedHeroPrefabId.Value = heroPrefabId;
        isHeroLocked.Value = false;
    }

    public void LockHeroSelection()
    {
        if (!IsOwner)
            return;

        if (selectedHeroPrefabId.Value < 0)
            return;

        isHeroLocked.Value = true;
    }

    public void UnlockHeroSelection()
    {
        if (!IsOwner)
            return;

        isHeroLocked.Value = false;
    }

    public bool TryRegisterControlledHero(HeroUnit registrant)
    {
        if (registrant == null)
            return false;

        if (!IsOwner)
            return false;

        if (!HasSelectedHero)
            return false;

        if (registrant.PrefabId != selectedHeroPrefabId.Value)
            return false;

        if (registrant.TeamID != teamID.Value)
            return false;

        ControlledHero = registrant;

        LocalController.TryCreate();
        LocalController.Local.BindHero(registrant);

        Debug.Log($"GamePlayer[{OwnerClientId}] 绑定本地控制英雄: {registrant.name}");
        return true;
    }

    public void UnregisterControlledHero(HeroUnit registrant)
    {
        if (registrant == null)
            return;

        if (ControlledHero != registrant)
            return;

        ControlledHero = null;

        if (Local == this)
            LocalController.Local?.UnbindHero(registrant);
    }

    public void UnbindControlledHero()
    {
        LocalController.Local?.ClearBindings();
        ControlledHero = null;
    }
}