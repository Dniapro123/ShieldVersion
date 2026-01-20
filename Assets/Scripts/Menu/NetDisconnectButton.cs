using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetDisconnectButton : MonoBehaviour
{
    public void DisconnectToMenu()
    {
        var nm = NetworkManager.singleton;
        if (nm == null)
        {
            SceneManager.LoadScene("Menu");
            return;
        }

        // Bezpieczne zatrzymanie w zależności od trybu
        if (NetworkServer.active && NetworkClient.isConnected)
            nm.StopHost();
        else if (NetworkClient.isConnected)
            nm.StopClient();
        else if (NetworkServer.active)
            nm.StopServer();

        // Mirror zwykle sam ładuje offlineScene, ale to jest “pas bezpieczeństwa”
        if (!string.IsNullOrWhiteSpace(nm.offlineScene))
            SceneManager.LoadScene(nm.offlineScene);
    }
}
