using Mirror;
using TMPro;
using UnityEngine;

public class NetMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TMP_InputField addressInput;
    [SerializeField] TMP_Text statusText;

    void Start()
    {
        if (addressInput != null && string.IsNullOrWhiteSpace(addressInput.text))
            addressInput.text = "localhost";

        RefreshStatus();
    }

    void Update() => RefreshStatus();

    void RefreshStatus()
    {
        if (statusText == null) return;

        if (NetworkServer.active && NetworkClient.isConnected)
            statusText.text = "Status: HOST (server+client)";
        else if (NetworkServer.active)
            statusText.text = "Status: SERVER";
        else if (NetworkClient.isConnected)
            statusText.text = "Status: CLIENT";
        else
            statusText.text = "Status: OFFLINE";
    }

    public void Host()
    {
        var nm = NetworkManager.singleton;
        if (nm == null) return;

        nm.networkAddress = GetAddress();
        nm.StartHost();
    }

    public void Join()
    {
        var nm = NetworkManager.singleton;
        if (nm == null) return;

        nm.networkAddress = GetAddress();
        nm.StartClient();
    }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    string GetAddress()
    {
        return addressInput != null && !string.IsNullOrWhiteSpace(addressInput.text)
            ? addressInput.text.Trim()
            : "localhost";
    }
}
