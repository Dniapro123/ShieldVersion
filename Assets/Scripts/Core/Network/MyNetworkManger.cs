using Mirror;
using UnityEngine;

public class MyNetworkManger : NetworkManager
{
    [Header("Role HP")]
    public int builderHp = 140;
    public int attackerHp = 100;

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);

        GameObject player = conn.identity.gameObject;

        var roleNet = player.GetComponent<PlayerRoleNet>();
        if (roleNet != null)
        {
            // po base.OnServerAddPlayer numPlayers zawiera już nowego gracza
            roleNet.role = (numPlayers == 1) ? PlayerRole.Builder : PlayerRole.Attacker;
        }

        var health = player.GetComponent<NetworkHealth>();
        if (health != null && roleNet != null)
        {
            int hp = (roleNet.role == PlayerRole.Builder) ? builderHp : attackerHp;
            health.ServerSetMaxHp(hp, refill: true);
        }
    }
}
