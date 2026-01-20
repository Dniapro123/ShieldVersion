using Mirror;
using UnityEngine;

public enum GamePhase { BuildRooms, PlaceTraps, Play }

public class GamePhaseNet : NetworkBehaviour
{
    public static GamePhaseNet Instance { get; private set; }

    [SyncVar] public GamePhase phase = GamePhase.BuildRooms;

    // Attacker zobaczy wnętrze dopiero po pierwszym przebiciu zewnętrznej ściany
    [SyncVar] public bool baseRevealed = false;

    void Awake() => Instance = this;

    public override void OnStartServer()
    {
        phase = GamePhase.BuildRooms;
        baseRevealed = false;
    }

    [Server]
    public void ServerSetPhase(GamePhase p)
    {
        phase = p;

        // Jak wracasz do build/traps (albo restart rundy) — chowamy znowu bazę
        if (p != GamePhase.Play)
            baseRevealed = false;

        Debug.Log($"[PHASE][SERVER] => {phase}");
    }

    [Server]
    public void ServerRevealBase()
    {
        if (baseRevealed) return;
        baseRevealed = true;
        Debug.Log("[FOG][SERVER] Base revealed for attacker");
    }
}
