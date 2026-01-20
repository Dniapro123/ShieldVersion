using Mirror;
using UnityEngine;

public class AttackerPickSpawnRoom : NetworkBehaviour
{
    public LayerMask placeAreaMask;

    Camera cam;
    PlayerRoleNet role;
    PlayerSpawnState spawnState;

    void Awake()
    {
        cam = Camera.main;
        role = GetComponent<PlayerRoleNet>();
        spawnState = GetComponent<PlayerSpawnState>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (role == null || !role.IsAttacker) return;

        var gm = GamePhaseNet.Instance;
        if (gm == null) return;

        // wybór tylko gdy już Play, ale baza jeszcze nie odkryta
        if (gm.phase != GamePhase.Play) return;
        if (gm.baseRevealed) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (!cam) cam = Camera.main;
            Vector3 m = cam.ScreenToWorldPoint(Input.mousePosition);
            m.z = 0f;

            Collider2D col = Physics2D.OverlapPoint(m, placeAreaMask);
            if (!col) return;

            RoomNet room = col.GetComponentInParent<RoomNet>();
            if (!room) return;

            NetworkIdentity roomId = room.GetComponent<NetworkIdentity>();
            if (!roomId) return;

            CmdChooseRoom(roomId);
        }
    }

    [Command]
    void CmdChooseRoom(NetworkIdentity roomId)
    {
        var gm = GamePhaseNet.Instance;
        if (gm == null) return;
        if (gm.phase != GamePhase.Play) return;
        if (gm.baseRevealed) return;

        if (roomId == null) return;

        Vector3 spawnPos = roomId.transform.position;

        // preferuj Spawn_Attacker, jeśli istnieje
        Transform t = roomId.transform.Find("Spawn_Attacker");
        if (t) spawnPos = t.position;

        if (spawnState) spawnState.ServerSetRespawn(spawnPos);

        transform.position = spawnPos;
        var rb = GetComponent<Rigidbody2D>();
        if (rb) rb.linearVelocity = Vector2.zero;

        gm.ServerRevealBase(); // <- to wyłączy FrontCover u attackera
    }
}
