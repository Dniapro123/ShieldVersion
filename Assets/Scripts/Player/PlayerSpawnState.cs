using Mirror;
using UnityEngine;

public class PlayerSpawnState : NetworkBehaviour
{
    [SyncVar] public Vector3 respawnWorldPos;

    [Header("Safety")]
    [Tooltip("Dodatkowy offset w górę, żeby nie respawnować w podłodze.")]
    public float extraUpOffset = 0.02f;

    Rigidbody2D rb;
    Collider2D col;
    NetworkTransformBase nt;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        nt = GetComponent<NetworkTransformBase>();
    }

    public override void OnStartServer()
    {
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
        Vector3 pos = GetSafeRespawnPos(respawnWorldPos);

        ApplyTeleportServer(pos);

        // właściciel (client) też musi natychmiast dostać teleport,
        // żeby NetworkTransform nie cofnął / nie nadpisał.
        if (connectionToClient != null)
            TargetRespawn(connectionToClient, pos);
    }

    [Server]
    Vector3 GetSafeRespawnPos(Vector3 basePos)
    {
        // Jeśli spawn point jest "na ziemi", to player (pivot w środku) wchodzi w podłogę.
        // Dodajemy pół wysokości collidera + mały margines.
        float up = extraUpOffset;

        if (col != null)
            up += col.bounds.extents.y;

        return basePos + Vector3.up * up;
    }

    [Server]
    void ApplyTeleportServer(Vector3 pos)
    {
        transform.position = pos;

        if (rb) rb.linearVelocity = Vector2.zero;
        if (nt) nt.ResetState();
    }

    [TargetRpc]
    void TargetRespawn(NetworkConnectionToClient conn, Vector3 pos)
    {
        transform.position = pos;

        if (rb) rb.linearVelocity = Vector2.zero;
        if (nt) nt.ResetState();
    }
}
