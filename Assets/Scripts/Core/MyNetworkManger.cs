using Mirror;
using UnityEngine;

public class MyNetworkManager : NetworkManager
{
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);

        var playerObj = conn.identity;
        var roleNet = playerObj.GetComponent<PlayerRoleNet>();
        if (roleNet == null)
        {
            Debug.LogError("[NET] Player prefab missing PlayerRoleNet!");
            return;
        }

        // pierwszy gracz = Builder, drugi = Attacker
        // (Host to zazwyczaj pierwszy)
        roleNet.role = (numPlayers == 1) ? PlayerRole.Builder : PlayerRole.Attacker;

        Debug.Log($"[NET] Added player {conn.connectionId} role={roleNet.role}");
    }
}
