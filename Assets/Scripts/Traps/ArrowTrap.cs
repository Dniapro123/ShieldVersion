using Mirror;
using UnityEngine;

public class ArrowTrap : NetworkBehaviour
{
    [Header("Refs")]
    public Transform firePoint;
    public GameObject projectilePrefab;

    [Header("Fire")]
    public float fireInterval = 1.25f;          // co ile strzela
    public float firstShotDelay = 0.15f;        // opóźnienie pierwszego strzału po wejściu w trigger
    public bool aimAtTarget = false;            // jeśli true -> strzela w kierunku gracza, jeśli false -> strzela w kierunku firePoint.right
    public int damage = 10;

    [Header("Projectile tuning (optional)")]
    public bool overrideProjectileTuning = false;
    public float projectileSpeed = 8f;
    public float projectileLifeTime = 3f;

    [Header("Rules")]
    public bool requirePlayPhase = true;
    public bool requireBaseRevealed = false;

    [Header("FX (optional)")]
    public Animator animator;
    public string shootTriggerName = "shoot";

    double nextShootTime;
    NetworkIdentity target; // attacker w triggerze

    void Awake()
    {
        if (firePoint == null)
        {
            var fp = transform.Find("FirePoint");
            if (fp != null) firePoint = fp;
        }
        if (animator == null) animator = GetComponent<Animator>();
    }

    [Server]
    bool CanActNow()
    {
        var gp = GamePhaseNet.Instance;
        if (gp == null) return true; // gdy testujesz bez phase managera

        if (requirePlayPhase && gp.phase != GamePhase.Play) return false;
        if (requireBaseRevealed && !gp.baseRevealed) return false;

        return true;
    }

    [ServerCallback]
    void Update()
    {
        if (!NetworkServer.active) return;
        if (!CanActNow()) return;
        if (projectilePrefab == null || firePoint == null) return;

        if (target == null) return;
        if (NetworkTime.time < nextShootTime) return;

        Vector2 dir;
        if (aimAtTarget && target != null)
        {
            dir = ((Vector2)target.transform.position - (Vector2)firePoint.position);
            if (dir.sqrMagnitude < 0.001f) dir = firePoint.right;
            dir.Normalize();
        }
        else
        {
            dir = firePoint.right.normalized;
        }

        ServerShoot(dir);

        nextShootTime = NetworkTime.time + fireInterval;
    }

    [Server]
    void ServerShoot(Vector2 dir)
    {
        // FX (animacja) — tylko wizualna
        RpcShootFX();

        // Spawn pocisku na serwerze
        Vector3 spawnPos = firePoint.position + (Vector3)(dir * 0.25f);
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0, 0, ang);

        GameObject go = Instantiate(projectilePrefab, spawnPos, rot);

        // Jeśli używasz ProjectileNet (ten “graczowy”) — zainicjalizuj go
        var pNet = go.GetComponent<ProjectileNet>();
        if (pNet != null)
        {
            if (overrideProjectileTuning)
            {
                pNet.speed = projectileSpeed;
                pNet.lifeTime = projectileLifeTime;
            }

            // shooter = ten trap (żeby ignorować self-hit nie ma znaczenia)
            pNet.ServerInit(netIdentity, dir, damage);
        }

        // Jeśli używasz EnemyProjectile (trapowy) — on sam zada dmg przez NetworkHealth
        // (parametry speed/delay ustawiasz w prefabie pocisku; tu nic nie musimy robić)

        NetworkServer.Spawn(go);
    }

    [ClientRpc]
    void RpcShootFX()
    {
        if (animator != null && !string.IsNullOrWhiteSpace(shootTriggerName))
            animator.SetTrigger(shootTriggerName);
    }

    // Wykrywanie attackera w triggerze (tylko serwer)
    [ServerCallback]
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanActNow()) return;

        var role = other.GetComponentInParent<PlayerRoleNet>();
        if (role == null || !role.IsAttacker) return;

        var hp = other.GetComponentInParent<NetworkHealth>();
        if (hp == null || hp.isDead) return;

        target = other.GetComponentInParent<NetworkIdentity>();
        nextShootTime = NetworkTime.time + firstShotDelay;
    }

    [ServerCallback]
    void OnTriggerExit2D(Collider2D other)
    {
        var ni = other.GetComponentInParent<NetworkIdentity>();
        if (ni != null && ni == target)
            target = null;
    }
}
