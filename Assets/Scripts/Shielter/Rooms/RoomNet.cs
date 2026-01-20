using Mirror;
using UnityEngine;

public class RoomNet : NetworkBehaviour
{
    [SyncVar] public int gridX;
    [SyncVar] public int gridY;

    // bity wg (int)WallType
    [SyncVar(hook = nameof(OnMaskChanged))]
    public int openMask;

    // Stabilny hash pokoju na podstawie gridów (bez nowych SyncVar)
    public int RoomHash => unchecked((gridX * 73856093) ^ (gridY * 19349663));

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

            int bit = 1 << (int)w.wallType;

            if ((mask & bit) != 0)
                w.MakeInterior();
        }
    }
}
