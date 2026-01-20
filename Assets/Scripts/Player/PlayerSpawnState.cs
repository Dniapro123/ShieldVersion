using Mirror;
using UnityEngine;

public class PlayerSpawnState : NetworkBehaviour
{
    [SyncVar] private Vector3 respawnPos;

    public override void OnStartServer()
    {
        respawnPos = transform.position;
    }

    [Server]
    public void ServerSetRespawn(Vector3 pos)
    {
        respawnPos = pos;
    }

    [Server]
    public void ServerRespawnNow()
    {
        transform.position = respawnPos;
        var rb = GetComponent<Rigidbody2D>();
        if (rb) rb.linearVelocity = Vector2.zero;
    }
}
