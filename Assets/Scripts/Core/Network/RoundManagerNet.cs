using Mirror;
using UnityEngine;

public enum RoundState { WaitingForReveal, Running, Ended }

public class RoundManagerNet : NetworkBehaviour
{
    public static RoundManagerNet Instance { get; private set; }

    [Header("Round")]
    [Min(1f)] public float roundDurationSeconds = 120f;

    [SyncVar] public RoundState state = RoundState.WaitingForReveal;
    [SyncVar] public PlayerRole winner = PlayerRole.Builder;
    [SyncVar] public double roundEndTime; // NetworkTime.time

    ReactorHP reactor;

    void Awake() => Instance = this;

    public override void OnStartServer()
    {
        state = RoundState.WaitingForReveal;
        winner = PlayerRole.Builder;
        roundEndTime = 0;
        reactor = null;
    }

    [ServerCallback]
    void Update()
    {
        var gm = GamePhaseNet.Instance;
        if (gm == null) return;

        // Reactor pojawia się dopiero gdy postawisz MainRoom -> szukamy aż znajdziemy
        if (reactor == null)
            reactor = FindObjectOfType<ReactorHP>();

        // Start rundy dopiero po Play + baseRevealed
        if (state == RoundState.WaitingForReveal)
        {
            if (gm.phase == GamePhase.Play && gm.baseRevealed)
                ServerStartRound();
            return;
        }

        if (state != RoundState.Running) return;

        // Attacker wygrywa jeśli reaktor zniszczony (ReactorHP robi Destroy(gameObject))
        /*    if (reactor == null)
        {
            reactor = FindObjectOfType<ReactorHP>();
            return; // nie kończ rundy tylko dlatego, że jeszcze nie znaleźliśmy reaktora
        }
        
        if (reactor == null)
        {
            ServerEndRound(PlayerRole.Attacker);
            return;
        }*/

        if (reactor != null && reactor.IsDestroyed)
        {
            ServerEndRound(PlayerRole.Attacker);
            return;
        }



        // Builder wygrywa jeśli czas minął
        if (NetworkTime.time >= roundEndTime)
        {
            ServerEndRound(PlayerRole.Builder);
            return;
        }
    }

    [Server]
    void ServerStartRound()
    {
        if (state != RoundState.WaitingForReveal) return;

        roundEndTime = NetworkTime.time + roundDurationSeconds;
        state = RoundState.Running;

        Debug.Log($"[ROUND][SERVER] Started, ends in {roundDurationSeconds}s");
    }

    [Server]
    public void ServerEndRound(PlayerRole win)
    {
        if (state == RoundState.Ended) return;

        winner = win;
        state = RoundState.Ended;
        Debug.Log($"[ROUND][SERVER] Ended. Winner: {winner}");
    }

    public float GetRemainingSeconds()
    {
        if (state != RoundState.Running) return 0f;
        return Mathf.Max(0f, (float)(roundEndTime - NetworkTime.time));
    }

    public bool IsRunning => state == RoundState.Running;
    public bool IsEnded => state == RoundState.Ended;
}
