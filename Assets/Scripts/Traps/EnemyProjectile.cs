using Mirror;
using UnityEngine;

/// <summary>
/// Pocisk pułapki (np. strzała).
/// Ruch + kolizje liczone na serwerze.
/// Animację eksplozji odpalamy RPC.
/// </summary>
public class EnemyProjectile : EnemyDamage
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float resetTime = 3f;

    private float lifetime;
    private Animator anim;
    private BoxCollider2D coll;
    private bool hit;

    [Header("Explosion")]
    [Tooltip("Ile sekund po eksplozji poczekać zanim serwer zniszczy pocisk.")]
    [SerializeField] private float destroyDelay = 0.25f;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        coll = GetComponent<BoxCollider2D>();

        // dla pocisków: niszcz po trafieniu
        destroySelfOnHit = true;
    }

    public void ActivateProjectile()
    {
        hit = false;
        lifetime = 0f;

        if (coll != null) coll.enabled = true;
        gameObject.SetActive(true);
    }

    [ServerCallback]
    private void Update()
    {
        if (!CanDamageNow())
        {
            if (isServer) NetworkServer.Destroy(gameObject);
            return;
        }

        if (hit) return;

        float movementSpeed = speed * Time.deltaTime;
        transform.Translate(movementSpeed, 0, 0);

        lifetime += Time.deltaTime;
        if (lifetime > resetTime)
        {
            if (isServer) NetworkServer.Destroy(gameObject);
        }
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hit) return;

        hit = true;

        // dmg (tylko attacker)
        base.OnTriggerEnter2D(collision);

        if (coll != null) coll.enabled = false;

        RpcExplode();

        if (isServer)
            Invoke(nameof(ServerDestroy), destroyDelay);
    }

    [Server]
    private void ServerDestroy()
    {
        if (isServer) NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcExplode()
    {
        if (coll != null) coll.enabled = false;

        if (anim != null)
            anim.SetTrigger("explode");
        else
            gameObject.SetActive(false);
    }
}
