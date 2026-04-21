using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Sirenix.OdinInspector;

public sealed class LobbyFlowNetwork : NetworkBehaviour
{
    [SerializeField, LabelText("开始游戏至少需要玩家数")]
    private int minPlayersToStart = 2;

    [SerializeField, LabelText("蓝方ID")]
    private byte blueTeamId = 2;

    [SerializeField, LabelText("红方ID")]
    private byte redTeamId = 3;

    [SerializeField, LabelText("切场景前等待秒数")]
    private float startDelay = 1f;

    private bool gameStarting;
    private float startTimer;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
            Debug.Log("[LobbyFlowNetwork] Lobby 流程已启动");
    }

    private void Update()
    {
        if (!IsServer)
            return;

        AssignTeamsIfNeeded();

        if (gameStarting)
        {
            startTimer -= Time.deltaTime;
            if (startTimer <= 0f)
            {
                gameStarting = false;
                GameManager.Instance.StartGameSceneFromLobby();
            }

            return;
        }

        if (CanStartGame())
        {
            gameStarting = true;
            startTimer = startDelay;
            Debug.Log("[LobbyFlowNetwork] 所有玩家已确认英雄，准备进入 GameScene");
        }
    }

    private void AssignTeamsIfNeeded()
    {
        var players = FindObjectsByType<GamePlayer>(FindObjectsSortMode.None);
        int blueCount = 0;
        int redCount = 0;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].teamID.Value == blueTeamId) blueCount++;
            if (players[i].teamID.Value == redTeamId) redCount++;
        }

        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player.teamID.Value != 0)
                continue;

            if (blueCount <= redCount)
            {
                player.teamID.Value = blueTeamId;
                blueCount++;
            }
            else
            {
                player.teamID.Value = redTeamId;
                redCount++;
            }

            Debug.Log($"[LobbyFlowNetwork] 为玩家 {player.OwnerClientId} 分配队伍 {player.teamID.Value}");
        }
    }

    private bool CanStartGame()
    {
        var players = FindObjectsByType<GamePlayer>(FindObjectsSortMode.None);
        if (players.Length < minPlayersToStart)
            return false;

        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];

            if (player.teamID.Value == 0)
                return false;

            if (!player.HasSelectedHero)
                return false;

            if (!player.IsHeroLocked)
                return false;
        }

        return true;
    }
}