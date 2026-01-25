using Mirror;
using TMPro;
using UnityEngine;
using System.Collections;

public class HUDHealthUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text playerHpText;
    public TMP_Text reactorHpText;

    [Header("Formats")]
    public string playerFormat = "HP: {0}/{1}";
    public string reactorFormat = "Reactor: {0}/{1}";

    NetworkHealth localHealth;
    ReactorHP reactor;

    void OnEnable()
    {
        StartCoroutine(BindRoutine());
    }

    IEnumerator BindRoutine()
    {
        // local player health
        while (NetworkClient.localPlayer == null)
            yield return null;

        localHealth = NetworkClient.localPlayer.GetComponent<NetworkHealth>();
        if (localHealth != null)
        {
            localHealth.ClientOnHealthChanged += OnPlayerHpChanged;
            OnPlayerHpChanged(localHealth.hp, localHealth.maxHp);
        }

        // reactor (może pojawić się później)
        while (reactor == null)
        {
            reactor = FindObjectOfType<ReactorHP>();
            yield return null;
        }

        reactor.ClientOnReactorHpChanged += OnReactorHpChanged;
        OnReactorHpChanged(reactor.hp, reactor.maxHp);
    }

    void OnDisable()
    {
        if (localHealth != null) localHealth.ClientOnHealthChanged -= OnPlayerHpChanged;
        if (reactor != null) reactor.ClientOnReactorHpChanged -= OnReactorHpChanged;
    }

    void OnPlayerHpChanged(int hp, int maxHp)
    {
        if (playerHpText != null)
            playerHpText.text = string.Format(playerFormat, hp, maxHp);
    }

    void OnReactorHpChanged(int hp, int maxHp)
    {
        if (reactorHpText != null)
            reactorHpText.text = string.Format(reactorFormat, hp, maxHp);
    }
}
