using Mirror;
using UnityEngine;

public class PlayerSpawnState : NetworkBehaviour
{
    [SyncVar] public Vector3 respawnWorldPos;

    [Header("Safety")]
    [Tooltip("Dodatkowy offset w górę, żeby nie respawnować w podłodze.")]
    public float extraUpOffset = 0.02f;

    Rigidbody2D rb;
    BoxCollider2D box;
    NetworkTransformBase nt;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();
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

    /// <summary>
    /// Server-authoritative respawn: teleportuje NA SERWERZE i natychmiast wysyła teleport do WSZYSTKICH klientów.
    /// (W projekcie masz NetworkTransformReliable ustawiony jako Client->Server, więc sam serwer nie zawsze „wymusi”
    /// teleporta na innych klientach bez dodatkowego RPC).
    /// </summary>
    [Server]
    public void ServerRespawnNow()
    {
        ServerTeleportAll(respawnWorldPos);
    }

    /// <summary>
    /// Uniwersalny teleport używany też przy wyborze spawnu attackera.
    /// </summary>
    [Server]
    public void ServerTeleportAll(Vector3 basePos)
    {
        Vector3 pos = GetSafeRespawnPos(basePos);

        ApplyTeleportServer(pos);
        RpcTeleport(pos);
    }

    [Server]
    Vector3 GetSafeRespawnPos(Vector3 basePos)
    {
        // Nie polegamy na col.bounds (może być zero/stale gdy collider jest chwilowo wyłączony).
        // Player ma BoxCollider2D (RequireComponent w PlayerMovement), więc liczymy pół-wysokość z size * scale.
        float halfH = 0f;
        if (box != null)
            halfH = 0.5f * box.size.y * Mathf.Abs(transform.lossyScale.y);

        float up = extraUpOffset + halfH;
        return basePos + Vector3.up * up;
    }

    [Server]
    void ApplyTeleportServer(Vector3 pos)
    {
        transform.position = pos;

        if (rb)
            rb.linearVelocity = Vector2.zero;

        if (nt)
            nt.ResetState();
    }

    [ClientRpc]
    void RpcTeleport(Vector3 pos)
    {
        transform.position = pos;

        if (rb)
            rb.linearVelocity = Vector2.zero;

        if (nt)
            nt.ResetState();
    }
}
