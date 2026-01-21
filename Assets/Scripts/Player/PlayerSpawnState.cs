using Mirror;
using UnityEngine;

public class PlayerSpawnState : NetworkBehaviour
{
    // SyncVar tylko dla debug / wglądu; respawn i tak robi serwer.
    [SyncVar] public Vector3 respawnWorldPos;

    public override void OnStartServer()
    {
        // domyślnie: gdzie gracz się pojawił na starcie
        respawnWorldPos = transform.position;
    }

    [Server]
    public void ServerSetRespawn(Vector3 pos)
    {
        respawnWorldPos = pos;
    }

    [Server]
    public void ServerRespawnNow()
    {
        transform.position = respawnWorldPos;

        var rb = GetComponent<Rigidbody2D>();
        if (rb) rb.linearVelocity = Vector2.zero;
    }
}
