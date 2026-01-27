using Mirror;
using UnityEngine;

public enum GamePhase { BuildRooms, PlaceTraps, Play }

public class GamePhaseNet : NetworkBehaviour
{
    public static GamePhaseNet Instance { get; private set; }

    [SyncVar] public GamePhase phase = GamePhase.BuildRooms;

    // Attacker zobaczy wnętrze dopiero po pierwszym przebiciu zewnętrznej ściany
    [SyncVar] public bool baseRevealed = false;

    // Zapamiętany spawn attackera (po kliknięciu pokoju) – używane też przy reconnect.
    [SyncVar] public Vector3 attackerSpawnPos = Vector3.zero;
    [SyncVar] public bool attackerSpawnSet = false;

    void Awake() => Instance = this;

    public override void OnStartServer()
    {
        phase = GamePhase.BuildRooms;
        baseRevealed = false;
        attackerSpawnPos = Vector3.zero;
        attackerSpawnSet = false;
    }

    [Server]
    public void ServerSetPhase(GamePhase p)
    {
        phase = p;

        // Jak wracasz do build/traps (albo restart rundy) — chowamy znowu bazę i czyścimy spawn attackera
        if (p != GamePhase.Play)
        {
            baseRevealed = false;
            attackerSpawnSet = false;
            attackerSpawnPos = Vector3.zero;
        }

        Debug.Log($"[PHASE][SERVER] => {phase}");
    }

    [Server]
    public void ServerSetAttackerSpawn(Vector3 pos)
    {
        attackerSpawnPos = pos;
        attackerSpawnSet = true;
    }

    [Server]
    public void ServerRevealBase()
    {
        if (baseRevealed) return;
        baseRevealed = true;
        Debug.Log("[FOG][SERVER] Base revealed for attacker");
    }
}
