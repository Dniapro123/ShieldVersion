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
    public float burstCooldown = 1f;
    public int damage = 10;

    float nextShotTime;
    int burstRemaining;
    float burstCooldownUntil;

    void Awake()
    {
        burstRemaining = burstSize;
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (Time.timeScale <= 0) return;

        Vector2 aimDir = GetAimDir();
        RotateGun(aimDir);

        if (Input.GetMouseButton(0))
            TryShoot(aimDir);
    }

    void RotateGun(Vector2 dir)
    {
        if (gunPivot == null) return;
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        gunPivot.rotation = Quaternion.Euler(0, 0, angle);
    }

    void TryShoot(Vector2 dir)
    {
        if (!CanShootNowClient()) return;

        if (Time.time < burstCooldownUntil) return;
        if (Time.time < nextShotTime) return;

        nextShotTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);

        if (burstSize > 0)
        {
            if (burstRemaining <= 0)
            {
                burstRemaining = burstSize;
                burstCooldownUntil = Time.time + burstCooldown;
                return;
            }
            burstRemaining--;
        }

        CmdShoot(dir);
    }

    bool CanShootNowClient()
    {
        // Najpewniej: tylko gdy runda RUNNING (czyli Play + baseRevealed + timer idzie)
        if (RoundManagerNet.Instance != null)
            return RoundManagerNet.Instance.IsRunning;

        // Fallback gdyby kiedyś RoundManagera nie było
        if (GamePhaseNet.Instance == null) return false;
        return GamePhaseNet.Instance.phase == GamePhase.Play && GamePhaseNet.Instance.baseRevealed;
    }

    bool CanShootNowServer()
    {
        if (RoundManagerNet.Instance != null)
            return RoundManagerNet.Instance.IsRunning;

        if (GamePhaseNet.Instance == null) return false;
        return GamePhaseNet.Instance.phase == GamePhase.Play && GamePhaseNet.Instance.baseRevealed;
    }

    [Command]
    void CmdShoot(Vector2 dir)
    {
        if (!CanShootNowServer()) return;
        if (projectilePrefab == null) return;

        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        Vector3 basePos = firePoint != null ? firePoint.position : transform.position;
        Vector3 spawnPos = basePos + (Vector3)dir * 0.25f;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0, 0, angle);

        GameObject go = Instantiate(projectilePrefab, spawnPos, rot);

        var proj = go.GetComponent<ProjectileNet>();
        if (proj != null)
        {
            var roleNet = GetComponent<PlayerRoleNet>();
            PlayerRole shooterRole = roleNet != null ? roleNet.role : PlayerRole.Builder;

            // owner = netId gracza, żeby zawsze dało się ominąć self-hit
            proj.ServerInit(netId, shooterRole, dir, damage);
        }

        NetworkServer.Spawn(go);
    }

    Vector2 GetAimDir()
    {
        if (firePoint == null)
            return transform.localScale.x >= 0 ? Vector2.right : Vector2.left;

        Camera cam = Camera.main;
        if (cam == null)
            return transform.localScale.x >= 0 ? Vector2.right : Vector2.left;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        Vector2 dir = (Vector2)(mouseWorld - firePoint.position);

        if (dir.sqrMagnitude < 0.0004f)
            dir = transform.localScale.x >= 0 ? Vector2.right : Vector2.left;

        return dir.normalized;
    }
}
