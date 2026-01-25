using Mirror;
using UnityEngine;

/// <summary>
/// Ruchoma pułapka (np. Saw) – ruch liczy serwer.
/// Dmg: tylko attacker, tylko Play.
/// </summary>
public class Enemy_Sideways : NetworkBehaviour
{
    [SerializeField] private float movementDistance = 2f;
    [SerializeField] private float speed = 2f;
    [SerializeField] private float damage = 10f;

    [Header("Rules")]
    [SerializeField] private bool requirePlayPhase = true;

    private bool movingLeft;
    private float leftEdge;
    private float rightEdge;

    private void Awake()
    {
        leftEdge = transform.position.x - movementDistance;
        rightEdge = transform.position.x + movementDistance;
    }

    [Server]
    private bool CanDamageNow()
    {
        if (!NetworkServer.active) return false;
        var gp = GamePhaseNet.Instance;
        if (gp == null) return true;
        if (requirePlayPhase && gp.phase != GamePhase.Play) return false;
        return true;
    }

    [ServerCallback]
    private void Update()
    {
        if (!NetworkServer.active) return;

        if (requirePlayPhase)
        {
            var gp = GamePhaseNet.Instance;
            if (gp != null && gp.phase != GamePhase.Play) return;
        }

        if (movingLeft)
        {
            if (transform.position.x > leftEdge)
                transform.position = new Vector3(transform.position.x - speed * Time.deltaTime, transform.position.y, transform.position.z);
            else
                movingLeft = false;
        }
        else
        {
            if (transform.position.x < rightEdge)
                transform.position = new Vector3(transform.position.x + speed * Time.deltaTime, transform.position.y, transform.position.z);
            else
                movingLeft = true;
        }
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!CanDamageNow()) return;

        var role = collision.GetComponentInParent<PlayerRoleNet>();
        if (role == null || !role.IsAttacker) return;

        var hp = collision.GetComponentInParent<NetworkHealth>();
        if (hp == null || hp.isDead) return;

        int dmg = Mathf.Max(1, Mathf.CeilToInt(damage));
        hp.ServerTakeDamage(dmg);
    }
}
