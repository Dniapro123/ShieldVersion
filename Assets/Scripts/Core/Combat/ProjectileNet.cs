using Mirror;
using UnityEngine;

public class ProjectileNet : NetworkBehaviour
{
    [Header("Projectile")]
    public float speed = 15f;
    public float lifeTime = 2f;

    [SyncVar] uint ownerNetId;
    [SyncVar] PlayerRole shooterRole;
    [SyncVar] Vector2 dir;
    [SyncVar] int damage;

    Rigidbody2D rb;
    Vector2 lastPos;
    bool hasHit;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    [Server]
    public void ServerInit(uint owner, PlayerRole role, Vector2 direction, int dmg)
    {
        ownerNetId = owner;
        shooterRole = role;
        dir = direction.sqrMagnitude < 0.0001f ? Vector2.right : direction.normalized;
        damage = Mathf.Max(0, dmg);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0;
            rb.angularVelocity = 0;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.linearVelocity = dir * speed;
            lastPos = rb.position;
        }
        else
        {
            lastPos = transform.position;
        }

        Invoke(nameof(ServerDie), lifeTime);
    }

    [Server]
    void ServerDie()
    {
        if (NetworkServer.active && gameObject != null)
            NetworkServer.Destroy(gameObject);
    }

    // --- Fallback przeciwko "przelatywaniu" ---
    [ServerCallback]
    void FixedUpdate()
    {
        if (hasHit) return;

        // jeśli runda nie działa -> kasuj pocisk
        if (RoundManagerNet.Instance != null && !RoundManagerNet.Instance.IsRunning)
        {
            NetworkServer.Destroy(gameObject);
            return;
        }

        Vector2 currentPos = rb ? rb.position : (Vector2)transform.position;
        Vector2 delta = currentPos - lastPos;
        float dist = delta.magnitude;

        if (dist > 0.0001f)
        {
            RaycastHit2D hit = Physics2D.Raycast(lastPos, delta.normalized, dist);
            if (hit.collider != null)
            {
                ServerTryHit(hit.collider);
            }
        }

        lastPos = currentPos;
    }

    [ServerCallback]
    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        ServerTryHit(other);
    }

    [Server]
    void ServerTryHit(Collider2D other)
    {
        if (hasHit) return;
        if (other == null) return;

        int layer = other.gameObject.layer;

        int placeArea = LayerMask.NameToLayer("PlaceArea");
        int frontCover = LayerMask.NameToLayer("FrontCover");
        int ui = LayerMask.NameToLayer("UI");
        int projectile = LayerMask.NameToLayer("Projectile");

        if (placeArea != -1 && layer == placeArea) return;
        if (frontCover != -1 && layer == frontCover) return;
        if (ui != -1 && layer == ui) return;
        if (projectile != -1 && layer == projectile) return;

        // self-hit off
        var targetNi = other.GetComponentInParent<NetworkIdentity>();
        if (targetNi != null && targetNi.netId == ownerNetId)
            return;

        // damage graczy (bez friendly fire)
        var health = other.GetComponentInParent<NetworkHealth>();
        if (health != null)
        {
            var targetRoleNet = health.GetComponent<PlayerRoleNet>();
            if (targetRoleNet != null && targetRoleNet.role == shooterRole)
                return;

            hasHit = true;
            health.ServerTakeDamage(damage);
            NetworkServer.Destroy(gameObject);
            return;
        }

        // damage reactora tylko od Attacker
        var reactor = other.GetComponentInParent<ReactorHP>();
        if (reactor != null)
        {
            hasHit = true;
            if (shooterRole == PlayerRole.Attacker)
                reactor.ServerTakeDamage(damage);

            NetworkServer.Destroy(gameObject);
            return;
        }

        // ściany / podłoga / cokolwiek innego blokuje
        hasHit = true;
        NetworkServer.Destroy(gameObject);
    }
}
