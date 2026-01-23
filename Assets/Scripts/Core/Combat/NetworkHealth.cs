using System.Collections;
using Mirror;
using UnityEngine;

public class NetworkHealth : NetworkBehaviour
{
    [SyncVar] public int maxHp = 100;
    [SyncVar] public int currentHp;
    [SyncVar] public bool isDead;

    public float respawnDelay = 1.5f;

    public override void OnStartServer()
    {
        currentHp = maxHp;
        isDead = false;
    }

    [Server]
    public void ServerSetMaxHp(int value, bool refill = true)
    {
        maxHp = Mathf.Max(1, value);
        if (refill) currentHp = maxHp;
    }

    [Server]
    public void ServerTakeDamage(int amount)
    {
        if (isDead) return;

        amount = Mathf.Max(0, amount);
        if (amount == 0) return;

        currentHp = Mathf.Max(0, currentHp - amount);

        if (currentHp <= 0)
            ServerDie();
    }

    [Server]
    void ServerDie()
    {
        if (isDead) return;

        isDead = true;
        StartCoroutine(ServerRespawnRoutine());
    }

    [Server]
    IEnumerator ServerRespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        currentHp = maxHp;
        isDead = false;

        var resp = GetComponent<PlayerSpawnState>();
        if (resp != null)
            resp.ServerRespawnNow();
    }
}
