using UnityEngine;
using Mirror;

public class ArrowTrap : NetworkBehaviour
{
    [Header("Attack")]
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject arrowProjectilePrefab; // prefab pocisku (z EnemyProjectile)

    [Header("SFX")]
    [SerializeField] private AudioClip arrowSound;

    private float cooldownTimer;

    void Awake()
    {
        // auto-find jeśli zapomnisz przypiąć
        if (firePoint == null)
        {
            var fp = transform.Find("FirePoint");
            if (fp != null) firePoint = fp;
        }
    }

    void Update()
    {
        // ✅ TYLKO SERWER wykonuje logikę pułapek
        if (!isServer) return;

        cooldownTimer += Time.deltaTime;
        if (cooldownTimer >= attackCooldown)
        {
            cooldownTimer = 0f;
            ServerAttack();
        }
    }

    [Server]
    private void ServerAttack()
    {
        if (firePoint == null || arrowProjectilePrefab == null)
        {
            Debug.LogWarning($"[ArrowTrap] Missing refs on {name} (firePoint or arrowProjectilePrefab).");
            return;
        }

        var proj = Instantiate(arrowProjectilePrefab, firePoint.position, firePoint.rotation);

        // Jeśli chcesz, żeby pocisk był widoczny u wszystkich:
        NetworkServer.Spawn(proj);

        RpcPlaySfx();
    }

    [ClientRpc]
    private void RpcPlaySfx()
    {
        if (SoundManager.instance != null && arrowSound != null)
            SoundManager.instance.PlaySound(arrowSound);
    }
}
