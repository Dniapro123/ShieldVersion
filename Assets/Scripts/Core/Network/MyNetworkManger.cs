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


// Jeśli attacker dołącza / wraca do gry już po reveal (czyli po wyborze spawnu),
// to od razu ustawiamy go na zapisanym spawnie (reconnect fix).
var gm = GamePhaseNet.Instance;
if (gm != null && roleNet.role == PlayerRole.Attacker &&
    gm.phase == GamePhase.Play && gm.baseRevealed && gm.attackerSpawnSet)
{
    var spawnState = playerObj.GetComponent<PlayerSpawnState>();
    if (spawnState != null)
    {
        spawnState.ServerSetRespawn(gm.attackerSpawnPos);
        spawnState.ServerTeleportAll(gm.attackerSpawnPos);
    }
    else
    {
        playerObj.transform.position = gm.attackerSpawnPos;
    }
}

        Debug.Log($"[NET] Added player {conn.connectionId} role={roleNet.role}");
    }
}
