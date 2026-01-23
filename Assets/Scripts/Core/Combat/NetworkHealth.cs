using Mirror;
using UnityEngine;
using System.Collections;

public class NetworkHealth : NetworkBehaviour
{
    [Header("HP per role")]
    [Min(1)] public int builderMaxHp = 140;
    [Min(1)] public int attackerMaxHp = 100;

    [SyncVar] public int maxHp;
    [SyncVar] public int hp;

    [Header("Death/Respawn")]
    public float respawnDelay = 1.5f;
    [SyncVar] public bool isDead;

    public override void OnStartServer()
    {
        ServerApplyRoleMaxHP();
        hp = maxHp;
        isDead = false;
    }

    [Server]
    public void ServerApplyRoleMaxHP()
    {
        // domyślnie
        int target = attackerMaxHp;

        var role = GetComponent<PlayerRoleNet>();
        if (role != null)
        {
            if (role.IsBuilder) target = builderMaxHp;
            else if (role.IsAttacker) target = attackerMaxHp;
        }

        maxHp = Mathf.Max(1, target);

        // jeśli ktoś zmienia rolę w trakcie (raczej nie), dopasuj hp w dół
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

    [Server]
    public void ServerTakeDamage(int dmg)
    {
        if (dmg <= 0) return;
        if (isDead) return;

        hp = Mathf.Max(0, hp - dmg);

        if (hp == 0)
        {
            isDead = true;
            RpcSetVisible(false);
            StartCoroutine(ServerRespawnAfterDelay());
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

    [ClientRpc]
    void RpcSetVisible(bool visible)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.enabled = visible;

        var col = GetComponent<Collider2D>();
        if (col) col.enabled = visible;

        // (opcjonalnie) wyłącz ruch/atak przy dead
        // var mv = GetComponent<PlayerMovement>(); if (mv) mv.enabled = visible;
        // var atk = GetComponent<PlayerCombatNet>(); if (atk) atk.enabled = visible;
    }
}
