using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BotDefenderNet : NetworkBehaviour
{
    [Header("Refs")]
    public Transform firePoint;
    public GameObject projectilePrefab;          // Prefab z ProjectileNet
    public SpriteRenderer spriteRenderer;

    [Header("Phase gating")]
    public bool requirePlayPhase = true;
    public bool requireBaseRevealed = true;      // bot aktywny dopiero po wyborze spawna przez attackera

    [Header("Patrol")]
    public float patrolSpeed = 1.6f;
    public float patrolDistance = -1f;           // jeśli <0 -> auto: 2x szerokość collidera
    public float patrolBodyWidths = 2f;

    [Header("Detection")]
    public float detectionRange = 7f;
    public LayerMask detectionMask = ~0;         // możesz ustawić np. tylko Default
    public bool requireLineOfSight = true;
    public LayerMask losMask = ~0;               // warstwy, które blokują “wzrok”

    [Header("Shooting")]
    public float shootCooldown = 1.0f;           // pojedyncze, wolne strzały
    public int damage = 10;
    public float projectileSpeed = 6f;
    public float projectileLifeTime = 3f;
    public float projectileSpawnForward = 0.35f;

    [SyncVar(hook = nameof(OnFacingChanged))]
    private bool facingLeft;

    private Rigidbody2D rb;
    private Collider2D bodyCol;

    private float leftX, rightX;
    private int patrolDir = 1;

    private NetworkIdentity target;
    private float nextShotTime;

    private readonly Collider2D[] hits = new Collider2D[24];

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCol = GetComponent<Collider2D>();

        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (!firePoint)
        {
            var fp = transform.Find("FirePoint");
            if (fp) firePoint = fp;
        }
    }

    public override void OnStartServer()
    {
        // Bot jest po stronie Buildera (żeby friendly-fire działał jak u graczy)
        var role = GetComponent<PlayerRoleNet>();
        if (role) role.role = PlayerRole.Builder;

        SetupPatrolBounds();
        nextShotTime = Time.time + Random.Range(0f, 0.5f);
    }

    public override void OnStartClient()
    {
        // Klienci nie symulują fizyki botów — pozycja idzie z serwera (NetworkTransform)
        if (!isServer && rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        if (spriteRenderer) spriteRenderer.flipX = facingLeft;
}


    [ServerCallback]
    void FixedUpdate()
    {
        if (!CanActNow())
        {
            // “zamrożenie” bez niszczenia grawitacji
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            target = null;
            return;
        }

        // 1) Szukaj celu (attacker)
        target = FindAttackerTarget();

        // 2) Jeśli jest cel -> stań i strzelaj
        if (target != null)
        {
            Vector2 origin = firePoint ? (Vector2)firePoint.position : (Vector2)transform.position;
            Vector2 toTarget = (Vector2)target.transform.position - origin;

            if (toTarget.sqrMagnitude <= 0.001f)
                return;

            SetFacing(toTarget.x < 0);

            // stop patrolu podczas celowania
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (Time.time >= nextShotTime)
            {
                if (!requireLineOfSight || HasLineOfSight(origin, target.transform))
                {
                    ServerShoot(toTarget.normalized);
                    nextShotTime = Time.time + shootCooldown;
                }
                else
                {
                    // jeśli LOS zablokowany, sprawdzaj częściej
                    nextShotTime = Time.time + 0.15f;
                }
            }

            return;
        }

        // 3) Brak celu -> patrol
        Patrol();
    }

    [Server]
    bool CanActNow()
    {
        var gm = GamePhaseNet.Instance;
        if (!gm) return true; // jak testujesz scenę bez phase managera

        if (requirePlayPhase && gm.phase != GamePhase.Play) return false;
        if (requireBaseRevealed && !gm.baseRevealed) return false;

        return true;
    }

    [Server]
    void SetupPatrolBounds()
    {
        float dist = patrolDistance;

        if (dist < 0f)
        {
            float w = 1f;
            if (bodyCol) w = bodyCol.bounds.size.x;
            dist = Mathf.Max(0.5f, w * patrolBodyWidths);
        }

        float x = transform.position.x;
        leftX = x - dist;
        rightX = x + dist;

        patrolDir = Random.value < 0.5f ? -1 : 1;
        SetFacing(patrolDir < 0);
    }

    [Server]
    void Patrol()
    {
        float x = transform.position.x;

        if (patrolDir > 0 && x >= rightX) patrolDir = -1;
        if (patrolDir < 0 && x <= leftX) patrolDir = 1;

        SetFacing(patrolDir < 0);
        rb.linearVelocity = new Vector2(patrolDir * patrolSpeed, rb.linearVelocity.y);
    }

    [Server]
    NetworkIdentity FindAttackerTarget()
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, detectionRange, hits, detectionMask);

        NetworkIdentity best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var c = hits[i];
            if (!c) continue;

            var role = c.GetComponentInParent<PlayerRoleNet>();
            if (!role || !role.IsAttacker) continue;

            var hp = c.GetComponentInParent<NetworkHealth>();
            if (hp && hp.isDead) continue;

            var ni = c.GetComponentInParent<NetworkIdentity>();
            if (!ni) continue;

            float d = (ni.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist)
            {
                // LOS opcjonalnie, żeby bot nie strzelał przez ściany
                if (requireLineOfSight)
                {
                    Vector2 origin = firePoint ? (Vector2)firePoint.position : (Vector2)transform.position;
                    if (!HasLineOfSight(origin, ni.transform)) continue;
                }

                bestDist = d;
                best = ni;
            }
        }

        return best;
    }

    [Server]
    bool HasLineOfSight(Vector2 origin, Transform targetTf)
    {
        Vector2 to = (Vector2)targetTf.position - origin;
        float dist = to.magnitude;
        if (dist < 0.05f) return true;

        Vector2 dir = to / dist;

        var hit = Physics2D.Raycast(origin, dir, dist, losMask);
        if (!hit.collider) return true;

        // OK jeśli trafiliśmy target
        return hit.collider.transform.IsChildOf(targetTf) || hit.collider.transform == targetTf;
    }

    [Server]
    void ServerShoot(Vector2 dir)
    {
        if (!projectilePrefab) return;

        Vector2 origin = firePoint ? (Vector2)firePoint.position : (Vector2)transform.position;
        Vector3 spawnPos = origin + dir * projectileSpawnForward;

        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0, 0, ang);

        GameObject go = Instantiate(projectilePrefab, spawnPos, rot);

        var p = go.GetComponent<ProjectileNet>();
        if (p != null)
        {
            p.speed = projectileSpeed;
            p.lifeTime = projectileLifeTime;
            p.ServerInit(netIdentity, dir, damage);
        }

        NetworkServer.Spawn(go);
    }

    [Server]
    void SetFacing(bool left)
    {
        if (facingLeft == left) return;
        facingLeft = left;
    }

    void OnFacingChanged(bool oldV, bool newV)
    {
        if (spriteRenderer) spriteRenderer.flipX = newV;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
#endif
}
