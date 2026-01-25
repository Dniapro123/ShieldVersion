using Mirror;
using UnityEngine;

public class PhaseGate : NetworkBehaviour
{
    RoomBuildClient roomBuild;
    TrapPlaceClient trapPlace;
    PlayerMovement move;
    Rigidbody2D rb;
    PlayerRoleNet role;

    void Awake()
    {
        roomBuild = GetComponent<RoomBuildClient>();
        trapPlace = GetComponent<TrapPlaceClient>();
        move = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody2D>();
        role = GetComponent<PlayerRoleNet>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (role == null) return;

        var gm = GamePhaseNet.Instance;
        if (gm == null) return;

        bool isBuilder = role.IsBuilder;
        bool isAttacker = role.IsAttacker;

        bool allowRoomBuild = isBuilder && gm.phase == GamePhase.BuildRooms;
        bool allowTrapPlace = isBuilder && gm.phase == GamePhase.PlaceTraps;

        // Builder może się ruszać w Play zawsze.
        // Attacker dopiero po baseRevealed (czyli po kliknięciu pokoju).
        bool allowMove =
            (isBuilder && gm.phase == GamePhase.Play) ||
            (isAttacker && gm.phase == GamePhase.Play && gm.baseRevealed);

        if (roomBuild) roomBuild.enabled = allowRoomBuild;
        if (trapPlace) trapPlace.enabled = allowTrapPlace;
        if (move) move.enabled = allowMove;

        // Stabilna fizyka: jak nie wolno się ruszać -> nie spadaj / nie driftuj
        if (rb)
        {
            if (allowMove)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 2f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.simulated = true;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
                rb.simulated = true;
            }
        }
    }
}
