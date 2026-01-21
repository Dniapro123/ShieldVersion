using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundUI : MonoBehaviour
{
    [Header("Timer")]
    public TMP_Text timerText;

    [Header("Result")]
    public GameObject resultPanel;
    public TMP_Text resultText;
    public Button backToMenuButton;

    bool shown;

    void Awake()
    {
        if (resultPanel) resultPanel.SetActive(false);
        shown = false;

        if (backToMenuButton)
            backToMenuButton.onClick.AddListener(BackToMenu);
    }

    void Update()
    {
        var round = RoundManagerNet.Instance;
        if (round == null) return;

        if (timerText)
        {
            timerText.text = round.IsRunning ? FormatTime(round.GetRemainingSeconds()) : "";
        }

        if (!shown && round.IsEnded)
        {
            ShowResult(round.winner);
            shown = true;
        }

        // jeśli kiedyś zrobisz restart rundy, UI samo się schowa
        if (shown && !round.IsEnded)
        {
            if (resultPanel) resultPanel.SetActive(false);
            shown = false;
        }
    }

    void ShowResult(PlayerRole winner)
    {
        PlayerRole myRole = PlayerRole.Builder;

        var lp = NetworkClient.localPlayer;
        if (lp)
        {
            var role = lp.GetComponent<PlayerRoleNet>();
            if (role) myRole = role.role;
        }

        bool iWin = (myRole == winner);
        if (resultText) resultText.text = iWin ? "YOU WIN" : "YOU LOSE";
        if (resultPanel) resultPanel.SetActive(true);
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        int m = s / 60;
        int r = s % 60;
        return $"{m:0}:{r:00}";
    }

    public void BackToMenu()
    {
        // Najczyściej: ustaw w NetworkManager -> Offline Scene = Menu
        var nm = NetworkManager.singleton;
        if (nm == null) return;

        if (NetworkServer.active && NetworkClient.isConnected) nm.StopHost();
        else if (NetworkClient.isConnected) nm.StopClient();
        else nm.StopServer();
    }
}
