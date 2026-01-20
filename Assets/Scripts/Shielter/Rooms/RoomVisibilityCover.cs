using Mirror;
using UnityEngine;

public class RoomVisibilityCover : MonoBehaviour
{
    [Header("Assign FrontCover sprite renderer (child)")]
    public SpriteRenderer frontCover;

    PlayerRoleNet localRole;

    void Awake()
    {
        if (!frontCover)
        {
            var t = transform.Find("FrontCover");
            if (t) frontCover = t.GetComponent<SpriteRenderer>();
        }
    }

    void OnEnable()
    {
        InvokeRepeating(nameof(TryBindLocalPlayer), 0f, 0.25f);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(TryBindLocalPlayer));
    }

    void TryBindLocalPlayer()
    {
        if (localRole != null) { CancelInvoke(nameof(TryBindLocalPlayer)); return; }
        if (!NetworkClient.active) return;
        if (NetworkClient.localPlayer == null) return;

        localRole = NetworkClient.localPlayer.GetComponent<PlayerRoleNet>();
        if (localRole != null) CancelInvoke(nameof(TryBindLocalPlayer));
    }

    void LateUpdate()
    {
        if (!frontCover) return;

        // offline / w edytorze: nie zasłaniaj
        if (!NetworkClient.active)
        {
            frontCover.enabled = false;
            return;
        }

        var gm = GamePhaseNet.Instance;
        if (gm == null)
        {
            frontCover.enabled = false;
            return;
        }

        // Jak jeszcze nie mamy roli — bezpiecznie zasłaniamy
        if (localRole == null)
        {
            frontCover.enabled = true;
            return;
        }

        // Builder zawsze widzi wnętrze
        if (localRole.IsBuilder)
        {
            frontCover.enabled = false;
            return;
        }

        // Attacker: zasłaniaj dopóki:
        // - faza nie jest Play
        // LUB
        // - baza nie została jeszcze "reveal" po przebiciu ściany
        bool shouldCover = (gm.phase != GamePhase.Play) || (!gm.baseRevealed);
        frontCover.enabled = shouldCover;
    }
}
