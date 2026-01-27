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

    [Tooltip("Jeśli false (np. bot), obiekt po śmierci zostanie zniszczony (bez respawnu).")]
    public bool respawnOnDeath = true;

    [Tooltip("Opóźnienie przed zniszczeniem obiektu, gdy respawnOnDeath=false.")]
    public float destroyOnDeathDelay = 0.05f;

    [SyncVar] public bool isDead;

    // UI/event (lokalne, nie sieciowe) — HUD może się tu podpinać
    public event Action<int, int> ClientOnHealthChanged;

    public override void OnStartServer()
    {
        // Rola zwykle jest ustawiana chwilę po spawn,
        // więc inicjalizujemy po 1 klatce.
        StartCoroutine(ServerInitAfterRoleAssigned());
    }

    public override void OnStartClient()
    {
        // wypchnij wartości od razu, żeby UI miało startowy stan
        ClientOnHealthChanged?.Invoke(hp, maxHp);
    }

    [Server]
    IEnumerator ServerInitAfterRoleAssigned()
    {
        yield return null;

        ServerApplyRoleMaxHP();
        hp = maxHp;
        isDead = false;
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

        // Gracze mają PlayerSpawnState (boty zwykle nie)
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

    // Hooks (client) -> UI
    void OnHpChanged(int oldValue, int newValue) => ClientOnHealthChanged?.Invoke(newValue, maxHp);
    void OnMaxHpChanged(int oldValue, int newValue) => ClientOnHealthChanged?.Invoke(hp, newValue);

    [ClientRpc]
void RpcSetVisible(bool visible)
{
    // UWAGA: w trybie HOST (server+client w jednym procesie) ClientRpc wykona się też na instancji serwera.
    // Wyłączanie Collider2D tutaj potrafi psuć serwerową fizykę (trafienia, bounds do respawnu, itp.).
    // Dlatego sterujemy TYLKO wizualami.
    foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        sr.enabled = visible;
}
}
