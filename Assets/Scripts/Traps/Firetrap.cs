using Mirror;
using UnityEngine;
using System.Collections;

/// <summary>
/// Firetrap multiplayer:
/// - logika i dmg liczone na serwerze
/// - animacje/kolor/SFX odpalane RPC na klientach
/// - dmg idzie w NetworkHealth (tylko Attacker), tylko w Play
/// </summary>
public class Firetrap : NetworkBehaviour
{
    [SerializeField] private float damage = 10f;

    [Header("Firetrap Timers")]
    [SerializeField] private float activationDelay = 0.35f;
    [SerializeField] private float activeTime = 1.2f;

    [Header("SFX")]
    [SerializeField] private AudioClip firetrapSound;

    private Animator anim;
    private SpriteRenderer spriteRend;

    private bool triggered;
    private bool active;

    private NetworkHealth targetHp;
    private float damageAcc;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        spriteRend = GetComponent<SpriteRenderer>();
    }

    [Server]
    private bool CanDamageNow()
    {
        if (!NetworkServer.active) return false;
        var gp = GamePhaseNet.Instance;
        if (gp == null) return true;
        return gp.phase == GamePhase.Play;
    }

    [ServerCallback]
    private void Update()
    {
        if (!NetworkServer.active) return;

        if (!CanDamageNow())
        {
            targetHp = null;
            damageAcc = 0f;
            return;
        }

        if (active && targetHp != null && !targetHp.isDead)
        {
            // damage traktujemy jako DPS
            damageAcc += damage * Time.deltaTime;
            int d = Mathf.FloorToInt(damageAcc);
            if (d > 0)
            {
                damageAcc -= d;
                targetHp.ServerTakeDamage(d);
            }
        }
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!CanDamageNow()) return;

        var role = collision.GetComponentInParent<PlayerRoleNet>();
        if (role == null || !role.IsAttacker) return;

        targetHp = collision.GetComponentInParent<NetworkHealth>();
        if (targetHp == null) return;

        if (!triggered)
            StartCoroutine(ServerActivateFiretrap());
    }

    [ServerCallback]
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!NetworkServer.active) return;

        var role = collision.GetComponentInParent<PlayerRoleNet>();
        if (role == null || !role.IsAttacker) return;

        targetHp = null;
        damageAcc = 0f;
    }

    [Server]
    private IEnumerator ServerActivateFiretrap()
    {
        triggered = true;

        RpcSetColor(new Color(1f, 0f, 0f, 1f));
        yield return new WaitForSeconds(activationDelay);

        RpcPlaySfx();
        RpcSetColor(Color.white);
        active = true;
        RpcSetActivated(true);

        yield return new WaitForSeconds(activeTime);

        active = false;
        triggered = false;
        RpcSetActivated(false);

        targetHp = null;
        damageAcc = 0f;
    }

    [ClientRpc]
    private void RpcSetActivated(bool on)
    {
        if (anim != null)
            anim.SetBool("activated", on);
    }

    [ClientRpc]
    private void RpcSetColor(Color c)
    {
        if (spriteRend != null)
            spriteRend.color = c;
    }

    [ClientRpc]
    private void RpcPlaySfx()
    {
        if (SoundManager.instance != null && firetrapSound != null)
            SoundManager.instance.PlaySound(firetrapSound);
    }
}
