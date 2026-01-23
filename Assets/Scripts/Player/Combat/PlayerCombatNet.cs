using Mirror;
using UnityEngine;

public class PlayerCombatNet : NetworkBehaviour
{
    [Header("Refs")]
    public Transform firePoint;
    public Transform gunPivot;
    public GameObject projectilePrefab;

    [Header("Fire settings")]
    public float fireRate = 8f;
    public int burstSize = 10;
    public float burstCooldown = 1.0f;
    public int damage = 10;

    float nextFireTime;
    int shotsLeftInBurst;
    float burstBlockUntil;

    // server-side anti-spam
    double serverNextFireTime;
    int serverShotsLeft;
    double serverBurstBlockUntil;

    PlayerRoleNet role;

    private void Awake()
    {
        role = GetComponent<PlayerRoleNet>();

        if (firePoint == null)
        {
            var fp = transform.Find("FirePoint");
            if (fp != null) firePoint = fp;
        }

        if (gunPivot == null)
        {
            var gp = transform.Find("GunPivot");
            if (gp != null) gunPivot = gp;
        }
    }

    void Start()
    {
        shotsLeftInBurst = burstSize;
        serverShotsLeft = burstSize;
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (Time.timeScale <= 0) return;

        if (!CanShootLocal()) return;

        Vector2 aimDir = GetAimDir();
        if (gunPivot != null)
        {
            float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
            gunPivot.rotation = Quaternion.Euler(0, 0, angle);
        }

        if (Input.GetMouseButton(0))
            TryShoot(aimDir);
    }

    bool CanShootLocal()
    {
        var gm = GamePhaseNet.Instance;
        if (gm == null) return false;
        if (gm.phase != GamePhase.Play) return false;

        if (role != null && role.IsAttacker && !gm.baseRevealed) return false;

        return true;
    }

    void TryShoot(Vector2 aimDir)
    {
        if (Time.time < burstBlockUntil) return;
        if (Time.time < nextFireTime) return;

        if (shotsLeftInBurst <= 0)
        {
            shotsLeftInBurst = burstSize;
            burstBlockUntil = Time.time + burstCooldown;
            return;
        }

        nextFireTime = Time.time + 1f / fireRate;
        shotsLeftInBurst--;

        CmdShoot(aimDir);
    }

    private Vector2 GetAimDir()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector2.right;

        Vector3 mouse = Input.mousePosition;
        Vector3 world = cam.ScreenToWorldPoint(mouse);
        world.z = 0;

        Vector3 origin = (gunPivot != null ? gunPivot.position : transform.position);
        Vector2 dir = (world - origin);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        return dir.normalized;
    }

    [Command]
    void CmdShoot(Vector2 aimDir)
    {
        var gm = GamePhaseNet.Instance;
        if (gm == null || gm.phase != GamePhase.Play) return;

        var r = GetComponent<PlayerRoleNet>();
        if (r != null && r.IsAttacker && !gm.baseRevealed) return;

        // rate limiting na serwerze
        double now = NetworkTime.time;
        if (now < serverBurstBlockUntil) return;
        if (now < serverNextFireTime) return;

        if (serverShotsLeft <= 0)
        {
            serverShotsLeft = burstSize;
            serverBurstBlockUntil = now + burstCooldown;
            return;
        }

        serverNextFireTime = now + 1.0 / Mathf.Max(0.1f, fireRate);
        serverShotsLeft--;

        if (firePoint == null || projectilePrefab == null) return;

        if (aimDir.sqrMagnitude < 0.5f) aimDir = Vector2.right;
        aimDir.Normalize();

        Vector3 spawnPos = firePoint.position + (Vector3)(aimDir * 0.8f);
        Quaternion rot = Quaternion.Euler(0, 0, Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg);

        GameObject go = Instantiate(projectilePrefab, spawnPos, rot);

        var p = go.GetComponent<ProjectileNet>();
        if (p != null)
            p.ServerInit(netIdentity, aimDir, damage);

        NetworkServer.Spawn(go);
    }
}
