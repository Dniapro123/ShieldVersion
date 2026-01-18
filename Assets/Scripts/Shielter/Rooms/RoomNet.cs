using Mirror;
using UnityEngine;

public class RoomNet : NetworkBehaviour
{
    [SyncVar] public int gridX;
    [SyncVar] public int gridY;

    [SyncVar(hook = nameof(OnMaskChanged))]
    public int openMask; // bity wg (int)WallType

    [Server]
    public void InitGrid(int gx, int gy)
    {
        gridX = gx;
        gridY = gy;
    }

    [Server]
    public void Open(WallType wall)
    {
        int bit = 1 << (int)wall;
        if ((openMask & bit) != 0) return;

        openMask |= bit;

        // żeby serwer też od razu miał "dziurę" lokalnie
        ApplyMask(openMask);
    }

    void OnMaskChanged(int oldMask, int newMask)
    {
        ApplyMask(newMask);
    }

    void ApplyMask(int mask)
    {
        var walls = GetComponentsInChildren<Wall>(true);
        foreach (var w in walls)
        {
            if (w == null) continue;

            // UWAGA: Wall musi mieć pole wallType typu WallType
            // i ustawione w prefabie (Left/Right/Top/Bottom)
            int bit = 1 << (int)w.wallType;

            if ((mask & bit) != 0)
                w.MakeInterior();
        }
    }
}
