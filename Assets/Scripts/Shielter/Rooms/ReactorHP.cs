using Mirror;
using UnityEngine;

public class ReactorHP : NetworkBehaviour
{
    [Header("HP")]
    [Min(1)] public int maxHp = 200;

    [SyncVar(hook = nameof(OnHpChanged))]
    public int hp;

    [SyncVar]
    public bool destroyed;

    [Header("Optional visuals")]
    public GameObject aliveVisual;     // np. sprite reaktora
    public GameObject destroyedVisual; // np. rozwalony sprite (może być null)
    public Collider2D hitCollider;     // collider do trafień (może być null)

    public bool IsDestroyed => destroyed || hp <= 0;

    public override void OnStartServer()
    {
        ServerReset();
    }

    [Server]
    public void ServerReset()
    {
        hp = Mathf.Clamp(maxHp, 1, 999999);
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

    void OnHpChanged(int oldHp, int newHp)
    {
        // Na razie nic — w przyszłości tu podepniesz UI hp.
        // Debug.Log($"Reactor HP: {newHp}/{maxHp}");
    }

    [ClientRpc]
    void RpcApplyVisual(bool isDestroyed)
    {
        if (aliveVisual) aliveVisual.SetActive(!isDestroyed);
        if (destroyedVisual) destroyedVisual.SetActive(isDestroyed);

        if (hitCollider) hitCollider.enabled = !isDestroyed;
    }

    [ServerCallback]
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
            ServerTakeDamage(999999);
    }

}
