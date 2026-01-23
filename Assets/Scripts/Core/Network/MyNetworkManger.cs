using Mirror;
using UnityEngine;

public class MyNetworkManager : NetworkManager
{
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);

        var playerObj = conn.identity.gameObject;

        var roleNet = playerObj.GetComponent<PlayerRoleNet>();
        if (roleNet == null)
        {
            Debug.LogError("[NET] Player prefab missing PlayerRoleNet!");
            return;
        }

        // pierwszy gracz = Builder, drugi = Attacker (Host to zazwyczaj pierwszy)
        roleNet.role = (numPlayers == 1) ? PlayerRole.Builder : PlayerRole.Attacker;

        // Zamiast starego ServerSetMaxHp(...)
        var health = playerObj.GetComponent<NetworkHealth>();
        if (health != null)
            health.ServerResetHP(); // ustawia maxHp wg roli + hp=max

        Debug.Log($"[NET] Added player {conn.connectionId} role={roleNet.role}");
    }
}
