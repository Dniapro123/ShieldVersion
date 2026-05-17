using Mirror;
using TMPro;
using UnityEngine;

public class InGameHintsUI : MonoBehaviour
{
    [SerializeField] TMP_Text lineA;
    [SerializeField] TMP_Text lineB;
    [SerializeField] TMP_Text lineC;

    PlayerRoleNet localRole;

    void Update()
    {
        // znajdź local playera
        if (localRole == null)
        {
            if (NetworkClient.localPlayer != null)
                localRole = NetworkClient.localPlayer.GetComponent<PlayerRoleNet>();
        }

        var gm = GamePhaseNet.Instance;
        if (localRole == null || gm == null)
        {
            SetLines("", "", "");
            return;
        }

        // Builder hints
        if (localRole.IsBuilder)
        {
            if (gm.phase == GamePhase.BuildRooms)
            {
                SetLines(
                    "Press ENTER to go to next phase",
                    "Press LMB to place room",
                    "Press RMB to change object"
                );
            }
            else if (gm.phase == GamePhase.PlaceTraps)
            {
                SetLines(
                    "Press ENTER to start attack",
                    "Press LMB to place trap",
                    "Press RMB to change object"
                );
            }
            else // Play
            {
                SetLines(
                    "Defend the reactor for 2:00",
                    "",
                    ""
                );
            }

            return;
        }

        // Attacker hints
        if (localRole.IsAttacker)
        {
            // pokazujemy instrukcję wyboru spawnu tylko zanim baza odkryta
            if (gm.phase == GamePhase.PlaceTraps && !gm.baseRevealed)
                SetLines("Press LMB to choose spawn room", "", "");
            else
                SetLines("", "", "");
        }
    }

    void SetLines(string a, string b, string c)
    {
        if (lineA) lineA.text = a;
        if (lineB) lineB.text = b;
        if (lineC) lineC.text = c;
    }
}
