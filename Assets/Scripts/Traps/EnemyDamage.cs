using Mirror;
using UnityEngine;

/// <summary>
/// Bazowy komponent obrażeń dla pułapek / pocisków pułapek.
/// Serwer-autorytatywnie: tylko serwer zadaje dmg (NetworkHealth), tylko w fazie Play.
/// Domyślnie rani tylko Attackera.
/// </summary>
public class EnemyDamage : NetworkBehaviour
{
    [SerializeField] protected float damage = 10f;

    [Header("Rules")]
    [Tooltip("Jeśli true, pułapka/pocisk rani wyłącznie gracza w roli Attacker.")]
    [SerializeField] protected bool onlyDamageAttacker = true;

    [Tooltip("Jeśli true, obrażenia działają tylko w GamePhase.Play.")]
    [SerializeField] protected bool requirePlayPhase = true;

    [Tooltip("Jeśli true, obrażenia działają dopiero po baseRevealed (opcjonalnie).")]
    [SerializeField] protected bool requireBaseRevealed = false;

    [Tooltip("Jeśli true, po trafieniu serwer niszczy ten obiekt (przydatne dla pocisków).")]
    [SerializeField] protected bool destroySelfOnHit = false;

    protected int DamageInt => Mathf.Max(1, Mathf.CeilToInt(damage));

    [Server]
    protected bool CanDamageNow()
    {
        if (!NetworkServer.active) return false;

        var gp = GamePhaseNet.Instance;
        if (gp == null) return true;

        if (requirePlayPhase && gp.phase != GamePhase.Play) return false;
        if (requireBaseRevealed && !gp.baseRevealed) return false;

        return true;
    }

    [Server]
    protected bool TryGetAttackerHealth(Collider2D collision, out NetworkHealth hp)
    {
        hp = null;
        if (collision == null) return false;

        var role = collision.GetComponentInParent<PlayerRoleNet>();
        if (role == null) return false;

        if (onlyDamageAttacker && !role.IsAttacker) return false;

        hp = collision.GetComponentInParent<NetworkHealth>();
        if (hp == null) return false;

        if (hp.isDead) return false;

        return true;
    }

    /// <summary>
    /// Standardowo pułapki mają Trigger hitbox.
    /// Ten handler działa TYLKO na serwerze.
    /// </summary>
    [ServerCallback]
    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (!CanDamageNow()) return;

        if (TryGetAttackerHealth(collision, out var hp))
        {
            hp.ServerTakeDamage(DamageInt);

            if (destroySelfOnHit)
            {
                if (isServer) NetworkServer.Destroy(gameObject);
                else Destroy(gameObject);
            }
        }
    }
}
