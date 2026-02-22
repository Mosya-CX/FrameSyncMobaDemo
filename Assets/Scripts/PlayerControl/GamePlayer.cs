using Unity.Netcode;
using UnityEngine;

public class GamePlayer : NetworkBehaviour
{
    public static GamePlayer Local;

    // -1代表还未选择英雄
    public NetworkVariable<int> selectedHeroPrefabId = 
        new(-1, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Owner);

    // 0代表未分配阵营，1代表中立野怪，2代表蓝方，3代表红方
    public NetworkVariable<byte> teamID = 
        new(0, 
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        gameObject.name = $"Player[{OwnerClientId}Handle]";
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        Local = this;
    }

    public void RegisterControlledHero(HeroUnit registrant)
    {
        if (!registrant)
            return;

        if (Local)
            return;

        if (registrant.PrefabId != selectedHeroPrefabId.Value)
            return;

        if (registrant.TeamID != teamID.Value)
            return;

        var local = new GameObject("LocalController").AddComponent<LocalController>();
        local.Init(this, registrant);
    }
}
