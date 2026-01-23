using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ProjectileNet : NetworkBehaviour
{
    [Header("Tuning")]
    public float speed = 12f;
    public float lifeTime = 2.5f;

    Rigidbody2D rb;
    Vector2 dir;
    int damage;
    NetworkConnectionToClient owner;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.None;
    }

    public override void OnStartClient()
    {
        // Klient nie symuluje fizyki pocisku — dostaje pozycję z sieci (NetworkTransform).
        if (!isServer && rb != null)
            rb.simulated = false;
    }

    [Server]
    public void ServerInit(NetworkConnectionToClient ownerConn, Vector2 direction, int dmg)
    {
        owner = ownerConn;
        dir = direction.sqrMagnitude < 0.001f ? Vector2.right : direction.normalized;
        damage = dmg;

        rb.simulated = true;
        rb.linearVelocity = dir * speed;

        CancelInvoke();
        Invoke(nameof(ServerDie), lifeTime);
    }

    [ServerCallback]
    void OnTriggerEnter2D(Collider2D col)
    {
        // ignoruj ownera
        var ni = col.GetComponentInParent<NetworkIdentity>();
        if (ni != null && ni.connectionToClient == owner) return;

        // ignoruj warstwy "systemowe"
        string lname = LayerMask.LayerToName(col.gameObject.layer);
        if (lname == "PlaceArea" || lname == "FrontCover" || lname == "UI")
            return;

        // Reactor
        var reactor = col.GetComponentInParent<ReactorHP>();
        if (reactor != null)
        {
            reactor.ServerTakeDamage(damage);
            ServerDie();
            return;
        }

        // Player
        var hp = col.GetComponentInParent<NetworkHealth>();
        if (hp != null)
        {
            hp.ServerTakeDamage(damage);
            ServerDie();
            return;
        }

        // ściany/ground
        ServerDie();
    }

    [Server]
    void ServerDie()
    {
        if (isServer && gameObject != null)
            NetworkServer.Destroy(gameObject);
    }
}
