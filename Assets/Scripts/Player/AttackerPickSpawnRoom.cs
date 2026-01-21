using Mirror;
using UnityEngine;

public class AttackerPickSpawnRoom : NetworkBehaviour
{
    public LayerMask placeAreaMask;

    Camera cam;
    PlayerRoleNet role;
    PlayerSpawnState spawnState;
    Rigidbody2D rb;
    NetworkTransformBase nt;

    void Awake()
    {
        cam = Camera.main;
        role = GetComponent<PlayerRoleNet>();
        spawnState = GetComponent<PlayerSpawnState>();
        rb = GetComponent<Rigidbody2D>();
        nt = GetComponent<NetworkTransformBase>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (role == null || !role.IsAttacker) return;

        var gm = GamePhaseNet.Instance;
        if (gm == null) return;

        // wybór tylko w Play i dopóki baza nieodkryta
        if (gm.phase != GamePhase.Play) return;
        if (gm.baseRevealed) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (!cam) cam = Camera.main;
            Vector3 m = cam.ScreenToWorldPoint(Input.mousePosition);
            m.z = 0f;

            Collider2D hit = Physics2D.OverlapPoint(m, placeAreaMask);
            if (!hit) return;

            RoomNet room = hit.GetComponentInParent<RoomNet>();
            if (!room) return;

            NetworkIdentity roomId = room.GetComponent<NetworkIdentity>();
            if (!roomId) return;

            CmdChooseSpawnRoom(roomId);
        }
    }

    [Command]
    void CmdChooseSpawnRoom(NetworkIdentity roomId)
    {
        var roleServer = GetComponent<PlayerRoleNet>();
        if (roleServer == null || roleServer.role != PlayerRole.Attacker) return;

        var gm = GamePhaseNet.Instance;
        if (gm == null) return;
        if (gm.phase != GamePhase.Play) return;
        if (gm.baseRevealed) return;

        if (roomId == null) return;
        RoomNet room = roomId.GetComponent<RoomNet>();
        if (room == null) return;

        Vector3 spawnPos = room.transform.position;

        // preferuj Spawn_Attacker
        Transform t = room.transform.Find("Spawn_Attacker");
        if (t != null) spawnPos = t.position;

        // zapisz respawn
        if (spawnState) spawnState.ServerSetRespawn(spawnPos);

        // TELEPORT (server)
        ServerTeleport(spawnPos);

        // TELEPORT (client-owner) -> żeby NetworkTransform nie cofnął
        if (connectionToClient != null)
            TargetTeleport(connectionToClient, spawnPos);

        // reveal bazy -> FrontCover OFF
        gm.ServerRevealBase();
    }

    [Server]
    void ServerTeleport(Vector3 pos)
    {
        transform.position = pos;
        if (rb) rb.linearVelocity = Vector2.zero;
        if (nt) nt.ResetState();
    }

    [TargetRpc]
    void TargetTeleport(NetworkConnectionToClient conn, Vector3 pos)
    {
        transform.position = pos;
        if (rb) rb.linearVelocity = Vector2.zero;
        if (nt) nt.ResetState();
    }
}
