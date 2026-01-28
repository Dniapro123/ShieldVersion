using Mirror;
using UnityEngine;
using System;
using System.Collections;

public class NetworkHealth : NetworkBehaviour
{
    [Header("HP per role")]
    [Min(1)] public int builderMaxHp = 100;
    [Min(1)] public int attackerMaxHp = 150;

    [SyncVar(hook = nameof(OnMaxHpChanged))] public int maxHp;
    [SyncVar(hook = nameof(OnHpChanged))] public int hp;

    [Header("Death/Respawn")]
    public float respawnDelay = 1.5f;
    public bool respawnOnDeath = true;
    public float destroyOnDeathDelay = 0.05f;

    [SyncVar] public bool isDead;

    public event Action<int, int> ClientOnHealthChanged;

    static bool HasAnimParam(Animator a, string name, AnimatorControllerParameterType type)
    {
        if (a == null) return false;
        foreach (var p in a.parameters)
            if (p.name == name && p.type == type) return true;
        return false;
    }

    public override void OnStartServer()
    {
        StartCoroutine(ServerInitAfterRoleAssigned());
    }

    public override void OnStartClient()
    {
        ClientOnHealthChanged?.Invoke(hp, maxHp);
    }

    [Server]
    IEnumerator ServerInitAfterRoleAssigned()
    {
        yield return null;
        ServerApplyRoleMaxHP();
        hp = maxHp;
        isDead = false;
        RpcSetVisible(true);
    }

    [Server]
    public void ServerApplyRoleMaxHP()
    {
        int target = attackerMaxHp;
        var role = GetComponent<PlayerRoleNet>();
        if (role != null)
        {
            if (role.IsBuilder) target = builderMaxHp;
            else if (role.IsAttacker) target = attackerMaxHp;
        }

        maxHp = Mathf.Max(1, target);
        hp = Mathf.Clamp(hp, 0, maxHp);
    }

    [Server]
    public void ServerResetHP()
    {
        ServerApplyRoleMaxHP();
        hp = maxHp;
        isDead = false;
        RpcSetVisible(true);
    }

    [ClientRpc]
    void RpcPlayHurt()
    {
        var a = GetComponentInChildren<Animator>();
        if (a != null && HasAnimParam(a, "Hurt", AnimatorControllerParameterType.Trigger))
            a.SetTrigger("Hurt");
    }

    [Server]
    public void ServerTakeDamage(int dmg)
    {
        if (dmg <= 0) return;
        if (isDead) return;

        hp = Mathf.Max(0, hp - dmg);

        if (hp > 0)
            RpcPlayHurt();

        if (hp == 0)
        {
            isDead = true;
            RpcSetVisible(false);

            if (respawnOnDeath)
                StartCoroutine(ServerRespawnAfterDelay());
            else
                StartCoroutine(ServerDestroyAfterDelay());
        }
    }

    [Server]
    IEnumerator ServerRespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        var spawnState = GetComponent<PlayerSpawnState>();
        if (spawnState != null)
            spawnState.ServerRespawnNow();

        ServerResetHP();
    }

    [Server]
    IEnumerator ServerDestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyOnDeathDelay);

        if (isServer && gameObject != null)
            NetworkServer.Destroy(gameObject);
    }

    void OnHpChanged(int oldValue, int newValue) => ClientOnHealthChanged?.Invoke(newValue, maxHp);
    void OnMaxHpChanged(int oldValue, int newValue) => ClientOnHealthChanged?.Invoke(hp, newValue);

    [ClientRpc]
    void RpcSetVisible(bool visible)
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = visible;
    }
}
