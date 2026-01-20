using Mirror;
using UnityEngine;

public class PhaseGate : NetworkBehaviour
{
    RoomBuildClient roomBuild;
    TrapPlaceClient trapPlace;
    PlayerMovement move;
    PlayerRoleNet role;

    void Awake()
    {
        roomBuild = GetComponent<RoomBuildClient>();
        trapPlace = GetComponent<TrapPlaceClient>();
        move = GetComponent<PlayerMovement>();
        role = GetComponent<PlayerRoleNet>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (role == null) return;

        var gm = GamePhaseNet.Instance;
        if (gm == null) return;

        bool isBuilder = role.IsBuilder;

        bool allowRoomBuild = isBuilder && gm.phase == GamePhase.BuildRooms;
        bool allowTrapPlace = isBuilder && gm.phase == GamePhase.PlaceTraps;

        // ruch w Play (możesz zrobić tylko attacker, jeśli chcesz)
        bool allowMove = gm.phase == GamePhase.Play && (role.IsBuilder || gm.baseRevealed);


        if (roomBuild) roomBuild.enabled = allowRoomBuild;
        if (trapPlace) trapPlace.enabled = allowTrapPlace;
        if (move) move.enabled = allowMove;
    }
}
