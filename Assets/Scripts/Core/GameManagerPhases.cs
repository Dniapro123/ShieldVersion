using System;
using UnityEngine;

public class GameManagerPhases : MonoBehaviour
{
    public enum Phase
    {
        BuildRooms,
        PlaceTraps,
        Play
    }

    [Header("References")]
    public RoomBuildingManager roomBuilder;
    public TrapPlacementManager trapPlacer;
    public PlayerMovement player;

    [Header("Ground snap (optional)")]
    public LayerMask groundMask;
    public float snapDownDistance = 80f;
    public float groundClearance = 0.05f;

    [Header("Optional: freeze player physics while not playing")]
    public Rigidbody2D playerRb;

    [Header("Debug")]
    public Phase phase = Phase.BuildRooms;

    // zapamiętanie stanu RB żeby po Play wrócił normalnie
    private bool rbStored = false;
    private RigidbodyType2D prevBodyType;
    private float prevGravity;
    private RigidbodyConstraints2D prevConstraints;

    void Awake()
    {
        if (!player) player = FindAnyObjectByType<PlayerMovement>();
        if (player && !playerRb) playerRb = player.GetComponent<Rigidbody2D>();
        if (!roomBuilder) roomBuilder = FindAnyObjectByType<RoomBuildingManager>();
        if (!trapPlacer) trapPlacer = FindAnyObjectByType<TrapPlacementManager>();
    }

    void Start()
    {
        if (roomBuilder) roomBuilder.OnBuildFinished += HandleBuildFinished;
        if (trapPlacer)  trapPlacer.OnTrapPlacementFinished += HandleTrapFinished;

        // start zawsze w budowie
        SetPhase(Phase.BuildRooms);
    }

    void OnDestroy()
    {
        if (roomBuilder) roomBuilder.OnBuildFinished -= HandleBuildFinished;
        if (trapPlacer)  trapPlacer.OnTrapPlacementFinished -= HandleTrapFinished;
    }

    void HandleBuildFinished()
    {
        Debug.Log("[GM] Build finished -> PlaceTraps");
        SetPhase(Phase.PlaceTraps);
    }

    void HandleTrapFinished()
    {
        Debug.Log("[GM] Traps finished -> Play");
        SetPhase(Phase.Play);
    }

    public void SetPhase(Phase newPhase)
    {
        phase = newPhase;

        // aktywacje managerów faz
        if (roomBuilder) roomBuilder.enabled = (phase == Phase.BuildRooms);
        if (trapPlacer)  trapPlacer.enabled  = (phase == Phase.PlaceTraps);

        bool allowPlayerControl = (phase == Phase.Play);

        // gracz – sterowanie tylko w Play
        if (player) player.enabled = allowPlayerControl;

        // fizyka gracza – w Build/Traps zamroź, w Play przywróć
        if (playerRb)
        {
            if (!allowPlayerControl)
            {
                StoreRbStateIfNeeded();

                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                playerRb.gravityScale = 0f;
                playerRb.bodyType = RigidbodyType2D.Kinematic;
                playerRb.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            else
            {
                RestoreRbState();

                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                playerRb.WakeUp();

                SnapPlayerToGroundIfPossible();
            }
        }

        Debug.Log($"[GameManagerPhases] Phase = {phase}");
    }

    void StoreRbStateIfNeeded()
    {
        if (rbStored) return;
        rbStored = true;

        prevBodyType = playerRb.bodyType;
        prevGravity = playerRb.gravityScale;
        prevConstraints = playerRb.constraints;
    }

    void RestoreRbState()
    {
        if (!rbStored) return;

        playerRb.bodyType = prevBodyType;
        playerRb.gravityScale = prevGravity;
        playerRb.constraints = prevConstraints;
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
