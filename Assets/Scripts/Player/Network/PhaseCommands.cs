using Mirror;
using UnityEngine;

public class PhaseCommands : NetworkBehaviour
{
    public KeyCode nextPhaseKey = KeyCode.Return; // ENTER

    void Update()
    {
        if (!isLocalPlayer) return;
        if (!Input.GetKeyDown(nextPhaseKey)) return;

        var role = GetComponent<PlayerRoleNet>();
        if (role == null || !role.IsBuilder) return;

        CmdNextPhase();
    }

    [Command]
    void CmdNextPhase()
    {
        var role = GetComponent<PlayerRoleNet>();
        if (role == null || !role.IsBuilder) return;

        var gm = GamePhaseNet.Instance;
        if (gm == null) { Debug.LogError("[SERVER] No GamePhaseNet in scene!"); return; }

        if (gm.phase == GamePhase.BuildRooms) gm.ServerSetPhase(GamePhase.PlaceTraps);
        else if (gm.phase == GamePhase.PlaceTraps) gm.ServerSetPhase(GamePhase.Play);
    }
}
