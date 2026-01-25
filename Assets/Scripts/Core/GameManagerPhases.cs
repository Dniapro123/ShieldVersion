using UnityEngine;
using Mirror;

public class GameManagerPhases : MonoBehaviour
{
    public enum Phase { BuildRooms, PlaceTraps, Play }

    [Header("References (scene managers)")]
    public RoomBuildingManager roomBuilder;
    public TrapPlacementManager trapPlacer;

    [Header("Player (auto: local player)")]
    public PlayerMovement player;
    public Rigidbody2D playerRb;

    [Header("Ground snap (optional)")]
    public LayerMask groundMask;
    public float snapDownDistance = 80f;
    public float groundClearance = 0.05f;

    [Header("Debug")]
    public Phase phase = Phase.BuildRooms;

    void Awake()
    {
        if (!roomBuilder) roomBuilder = FindAnyObjectByType<RoomBuildingManager>();
        if (!trapPlacer)  trapPlacer  = FindAnyObjectByType<TrapPlacementManager>();
    }

    void Start()
    {
        // fazy są wspólne, ale kontrola dotyczy TYLKO local playera
        if (roomBuilder) roomBuilder.OnBuildFinished += HandleBuildFinished;
        if (trapPlacer)  trapPlacer.OnTrapPlacementFinished += HandleTrapFinished;

        // NIE ustawiaj fazy od razu, poczekaj aż local player się pojawi
        InvokeRepeating(nameof(TryBindLocalPlayer), 0f, 0.2f);
    }

    void OnDestroy()
    {
        if (roomBuilder) roomBuilder.OnBuildFinished -= HandleBuildFinished;
        if (trapPlacer)  trapPlacer.OnTrapPlacementFinished -= HandleTrapFinished;
    }

    void TryBindLocalPlayer()
    {
        if (!NetworkClient.active) return;
        if (NetworkClient.localPlayer == null) return;

        player = NetworkClient.localPlayer.GetComponent<PlayerMovement>();
        playerRb = NetworkClient.localPlayer.GetComponent<Rigidbody2D>();

        if (player != null && playerRb != null)
        {
            CancelInvoke(nameof(TryBindLocalPlayer));
            Debug.Log("[GM] Bound local player OK");
            SetPhase(Phase.BuildRooms);
        }
    }

    void HandleBuildFinished()
    {
        SetPhase(Phase.PlaceTraps);
    }

    void HandleTrapFinished()
    {
        SetPhase(Phase.Play);
    }

    public void SetPhase(Phase newPhase)
    {
        phase = newPhase;

        // managerów faz nie ograniczamy per-player (to scena),
        // ale jeśli chcesz: docelowo tylko Builder ma je mieć aktywne
        if (roomBuilder) roomBuilder.enabled = (phase == Phase.BuildRooms);
        if (trapPlacer)  trapPlacer.enabled  = (phase == Phase.PlaceTraps);

        bool play = (phase == Phase.Play);

        // sterowanie tylko w Play
        if (player) player.enabled = play;

        // HARD RESET fizyki na local playerze
        if (playerRb)
        {
            if (!play)
            {
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                playerRb.simulated = true; // local ma być true
                playerRb.bodyType = RigidbodyType2D.Kinematic;
                playerRb.gravityScale = 0f;
                playerRb.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            else
            {
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                playerRb.simulated = true;
                playerRb.bodyType = RigidbodyType2D.Dynamic;
                playerRb.gravityScale = 2f;
                playerRb.constraints = RigidbodyConstraints2D.FreezeRotation;
                playerRb.WakeUp();

                SnapPlayerToGroundIfPossible();
            }
        }

        Debug.Log($"[GameManagerPhases] Phase = {phase} (local player only)");
    }

    void SnapPlayerToGroundIfPossible()
    {
        if (!player || groundMask.value == 0) return;

        var col = player.GetComponent<Collider2D>();
        float halfH = 0.5f;
        if (col) halfH = col.bounds.extents.y;

        Vector2 origin = player.transform.position;
        var hit = Physics2D.Raycast(origin, Vector2.down, snapDownDistance, groundMask);
        if (hit.collider)
        {
            Vector3 p = player.transform.position;
            p.y = hit.point.y + halfH + groundClearance;
            player.transform.position = p;
        }
    }
}
