using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ProjectileNet : NetworkBehaviour
{
    [Header("Tuning")]
    public float speed = 12f;
    public float lifeTime = 2.5f;

    Rigidbody2D rb;
    Collider2D col;

    Vector2 dir;
    int damage;

    NetworkIdentity shooter;
    PlayerRole shooterRole = PlayerRole.Builder;
    bool initialized;

    Vector2 lastPos;
    float castRadius;
    int hitMask;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.None;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        hitMask = ~LayerMask.GetMask("UI", "PlaceArea", "FrontCover", "Projectile");
    }

    public override void OnStartClient()
    {
        if (!isServer && rb != null)
            rb.simulated = false;
    }

    [Server]
    public void ServerInit(NetworkIdentity shooterIdentity, Vector2 direction, int dmg)
    {
        shooter = shooterIdentity;

        var role = shooter != null ? shooter.GetComponent<PlayerRoleNet>() : null;
        if (role != null) shooterRole = role.role;

        dir = direction.sqrMagnitude < 0.001f ? Vector2.right : direction.normalized;
        damage = dmg;

        if (rb != null)
        {
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearVelocity = dir * speed;
        }

        castRadius = 0.05f;
        if (col != null)
            castRadius = Mathf.Max(0.02f, Mathf.Min(col.bounds.extents.x, col.bounds.extents.y));

        if (shooter != null && col != null)
        {
            var shooterCol = shooter.GetComponent<Collider2D>();
            if (shooterCol != null)
                Physics2D.IgnoreCollision(col, shooterCol, true);
        }

        initialized = true;
        lastPos = rb != null ? rb.position : (Vector2)transform.position;

        CancelInvoke();
        Invoke(nameof(ServerDie), Mathf.Max(0.05f, lifeTime));
    }

    [ServerCallback]
    void FixedUpdate()
    {
        if (!initialized) return;

        Vector2 curPos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 delta = curPos - lastPos;
        float dist = delta.magnitude;

        if (dist > 0.0001f)
        {
            RaycastHit2D hit = Physics2D.CircleCast(lastPos, castRadius, delta.normalized, dist, hitMask);
            if (hit.collider != null)
            {
                if (ServerProcessHit(hit.collider))
                    return;
            }
        }

        lastPos = curPos;
    }

    [ServerCallback]
    void OnTriggerEnter2D(Collider2D other)
    {
        ServerProcessHit(other);
    }

    [Server]
    bool ServerProcessHit(Collider2D colHit)
    {
        if (!initialized) return false;

        var gm = GamePhaseNet.Instance;
        if (gm == null || gm.phase != GamePhase.Play)
        {
            ServerDie();
            return true;
        }

        string lname = LayerMask.LayerToName(colHit.gameObject.layer);
        if (lname == "PlaceArea" || lname == "FrontCover" || lname == "UI" || lname == "Projectile")
            return false;

        if (shooter != null)
        {
            var hitNi = colHit.GetComponentInParent<NetworkIdentity>();
            if (hitNi != null && hitNi == shooter)
                return false;
        }

        // Reactor: tylko Attacker
        var reactor = colHit.GetComponentInParent<ReactorHP>();
        if (reactor != null)
        {
            if (shooterRole == PlayerRole.Attacker)
            {
                reactor.ServerTakeDamage(damage);
                ServerDie();
                return true;
            }
            return false;
        }

        // Player: bez friendly-fire
        var hp = colHit.GetComponentInParent<NetworkHealth>();
        if (hp != null)
        {
            var targetRole = hp.GetComponent<PlayerRoleNet>();
            if (targetRole != null && targetRole.role == shooterRole)
                return false;

            hp.ServerTakeDamage(damage);
            ServerDie();
            return true;
        }

        // reszta świata
        ServerDie();
        return true;
    }

    [Server]
    void ServerDie()
    {
        if (isServer && gameObject != null)
            NetworkServer.Destroy(gameObject);
    }
}
