using Mirror;
using UnityEngine;

public class PlayerCombatNet : NetworkBehaviour
{
    [Header("Refs")]
    public Transform firePoint;
    public Transform gunPivot; // opcjonalnie: obróć broń
    public GameObject projectilePrefab;

    [Header("Fire settings")]
    public float fireRate = 8f;          // strzałów na sekundę
    public int burstSize = 10;           // ile strzałów zanim przerwa
    public float burstCooldown = 1.0f;   // przerwa po serii
    public int damage = 10;

    float nextFireTime;
    int shotsLeftInBurst;
    float burstBlockUntil;

    private void Awake()
    {
        // auto-find jeśli ktoś nie podpiął w Inspectorze
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
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (Time.timeScale <= 0) return;

        // aim
        Vector2 aimDir = GetAimDir();
        if (gunPivot != null)
        {
            float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
            gunPivot.rotation = Quaternion.Euler(0, 0, angle);
        }

        if (Input.GetMouseButton(0))
            TryShoot(aimDir);
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
        // lokalnie możesz też odpalić animację triggerem (lub przez NetworkAnimator)
    }

   private Vector2 GetAimDir()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector2.right; // awaryjnie

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
    if (firePoint == null || projectilePrefab == null) return;

    if (aimDir.sqrMagnitude < 0.5f) aimDir = Vector2.right;
    aimDir.Normalize();

    // offset, żeby nie spawnować w colliderze (PlaceArea/Wall/Player)
    

    Vector3 spawnPos = firePoint.position + (Vector3)(aimDir.normalized * 0.8f);
    GameObject go = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

    var p = go.GetComponent<ProjectileNet>();
    if (p != null)
        p.ServerInit(connectionToClient, aimDir, damage);

    NetworkServer.Spawn(go);
}

}
