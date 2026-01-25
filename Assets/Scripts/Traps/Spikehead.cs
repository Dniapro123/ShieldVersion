using Mirror;
using UnityEngine;

/// <summary>
/// Spikehead – serwer decyduje o ataku i ruchu.
/// Dmg jest w EnemyDamage (tylko attacker, Play).
/// </summary>
public class Spikehead : EnemyDamage
{
    [Header("SpikeHead Attributes")]
    [SerializeField] private float speed = 6f;
    [SerializeField] private float range = 4f;
    [SerializeField] private float checkDelay = 0.25f;
    [SerializeField] private LayerMask playerLayer;

    private Vector3[] directions = new Vector3[4];
    private Vector3 destination;
    private float checkTimer;
    private bool attacking;

    [Header("SFX")]
    [SerializeField] private AudioClip impactSound;

    private void OnEnable()
    {
        if (NetworkServer.active)
            Stop();
    }

    [ServerCallback]
    private void Update()
    {
        if (!NetworkServer.active) return;
        if (!CanDamageNow()) return;

        if (attacking)
        {
            transform.Translate(destination * Time.deltaTime * speed);
        }
        else
        {
            checkTimer += Time.deltaTime;
            if (checkTimer > checkDelay)
                CheckForPlayer();
        }
    }

    [Server]
    private void CheckForPlayer()
    {
        CalculateDirections();

        for (int i = 0; i < directions.Length; i++)
        {
            Debug.DrawRay(transform.position, directions[i], Color.red);
            RaycastHit2D hit = Physics2D.Raycast(transform.position, directions[i], range, playerLayer);

            if (hit.collider != null && !attacking)
            {
                var role = hit.collider.GetComponentInParent<PlayerRoleNet>();
                if (role == null || !role.IsAttacker) continue;

                attacking = true;
                destination = directions[i];
                checkTimer = 0f;
                return;
            }
        }

        checkTimer = 0f;
    }

    private void CalculateDirections()
    {
        directions[0] = transform.right * range;
        directions[1] = -transform.right * range;
        directions[2] = transform.up * range;
        directions[3] = -transform.up * range;
    }

    [Server]
    private void Stop()
    {
        destination = transform.position;
        attacking = false;
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!CanDamageNow()) return;

        RpcImpactSfx();
        base.OnTriggerEnter2D(collision);
        Stop();
    }

    [ClientRpc]
    private void RpcImpactSfx()
    {
        if (SoundManager.instance != null && impactSound != null)
            SoundManager.instance.PlaySound(impactSound);
    }
}
