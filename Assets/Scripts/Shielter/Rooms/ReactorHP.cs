using Mirror;
using UnityEngine;
using System;

public class ReactorHP : NetworkBehaviour
{
    [Header("HP")]
    [Min(1)]
    [SyncVar(hook = nameof(OnMaxHpChanged))]
    public int maxHp = 200;

    [SyncVar(hook = nameof(OnHpChanged))]
    public int hp;

    [SyncVar]
    public bool destroyed;

    [Header("Optional visuals")]
    public GameObject aliveVisual;     // np. sprite reaktora
    public GameObject destroyedVisual; // np. rozwalony sprite (może być null)
    public Collider2D hitCollider;     // collider do trafień (może być null)

    // ✅ Event dla HUD
    public event Action<int, int> ClientOnReactorHpChanged;

    public bool IsDestroyed => destroyed || hp <= 0;

    public override void OnStartServer()
    {
        ServerReset();
    }

    public override void OnStartClient()
    {
        // ✅ od razu wypchnij wartości do UI po stronie klienta
        ClientOnReactorHpChanged?.Invoke(hp, maxHp);
    }

    [Server]
    public void ServerReset()
    {
        maxHp = Mathf.Clamp(maxHp, 1, 999999);
        hp = maxHp;
        destroyed = false;

        RpcApplyVisual(false);
    }

    /// <summary>
    /// Serwerowe obrażenia (wywołuj z pocisków / hitów na serwerze).
    /// </summary>
    [Server]
    public void ServerTakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (IsDestroyed) return;

        hp = Mathf.Max(0, hp - amount);

        if (hp <= 0)
        {
            destroyed = true;
            RpcApplyVisual(true);

            // Zwycięstwo attackera (tylko jeśli runda istnieje)
            if (RoundManagerNet.Instance != null)
                RoundManagerNet.Instance.ServerEndRound(PlayerRole.Attacker);
        }
    }

    // ✅ Hooki SyncVar -> UI
    void OnHpChanged(int oldHp, int newHp)
    {
        ClientOnReactorHpChanged?.Invoke(newHp, maxHp);
    }

    void OnMaxHpChanged(int oldMax, int newMax)
    {
        ClientOnReactorHpChanged?.Invoke(hp, newMax);
    }

    [ClientRpc]
    void RpcApplyVisual(bool isDestroyed)
    {
        if (aliveVisual) aliveVisual.SetActive(!isDestroyed);
        if (destroyedVisual) destroyedVisual.SetActive(isDestroyed);

        if (hitCollider) hitCollider.enabled = !isDestroyed;
    }
}
