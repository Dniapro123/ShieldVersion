using Mirror;
using UnityEngine;

public enum GamePhase { BuildRooms, PlaceTraps, Play }

public class GamePhaseNet : NetworkBehaviour
{
    public static GamePhaseNet Instance { get; private set; }

    [SyncVar] public GamePhase phase = GamePhase.BuildRooms;

    void Awake() => Instance = this;

    public override void OnStartServer()
    {
        phase = GamePhase.BuildRooms; // start gry
    }

    [Server]
    public void ServerSetPhase(GamePhase p)
    {
        phase = p;
        Debug.Log($"[PHASE][SERVER] => {phase}");
    }
}
