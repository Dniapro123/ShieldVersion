using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class BuildCommands : NetworkBehaviour
{
    [Header("Spawnable Room Prefabs (MUST be registered in NetworkManager)")]
    public List<GameObject> roomPrefabs = new();

    [Header("MainRoom rules")]
    [Tooltip("Index w roomPrefabs który jest MainRoom.")]
    public int mainRoomPrefabIndex = 0;

    [Tooltip("Nazwa do auto-detekcji (opcjonalnie).")]
    public string mainRoomNameHint = "MainRoom";

    [SyncVar] public Vector3 builderMainSpawnWorld;

    // >>> KLUCZ: serwerowa prawda o mainroom
    [SyncVar] public bool mainRoomPlaced;

    // >>> KLUCZ: serwerowa liczba postawionych pokoi (żeby klient nie mylił się przez opóźnienia)
    [SyncVar] public int placedRoomsServer;

    private readonly Dictionary<Vector2Int, NetworkIdentity> rooms = new();

    int roomW, roomH, maxRooms, minGridY;
    Vector3 originWorld;

    bool IsMainIndex(int i) => (mainRoomPrefabIndex >= 0 && i == mainRoomPrefabIndex);

    public override void OnStartServer()
    {
        rooms.Clear();
        mainRoomPlaced = false;
        placedRoomsServer = 0;
        builderMainSpawnWorld = transform.position;

        var cfg = BuildConfig.Instance;
        if (cfg == null)
        {
            Debug.LogError("[SERVER] No BuildConfig in scene!");
            roomW = 26; roomH = 12; maxRooms = 8; minGridY = 0;
            originWorld = Vector3.zero;
        }
        else
        {
            roomW = cfg.roomW;
            roomH = cfg.roomH;
            maxRooms = cfg.maxRooms;
            minGridY = cfg.minGridY;
            originWorld = cfg.originWorld;
            originWorld.z = 0f;

            Debug.Log($"[SERVER] BuildConfig: W={roomW} H={roomH} max={maxRooms} minY={minGridY} origin={originWorld}");
        }
    }

    [Command]
    public void CmdPlaceRoom(int prefabIndex, int gx, int gy)
    {
        // tylko Builder
        var roleNet = GetComponent<PlayerRoleNet>();
        if (roleNet == null || roleNet.role != PlayerRole.Builder) return;

        if (prefabIndex < 0 || prefabIndex >= roomPrefabs.Count) return;
        if (gy < minGridY) return;

        var gridPos = new Vector2Int(gx, gy);

        if (rooms.ContainsKey(gridPos)) return;
        if (rooms.Count >= maxRooms) return;

        // ======= MAINROOM RULES (zgodnie z Twoim wymaganiem) =======
        int placed = rooms.Count; // ile już stoi na serwerze

        if (IsMainIndex(prefabIndex))
        {
            // MainRoom wolno w dowolnym momencie, ale tylko raz
            if (mainRoomPlaced) return;
        }
        else
        {
            // jeśli to byłby ostatni slot i MainRoom jeszcze nie ma -> blokuj zwykłe pokoje
            if (!mainRoomPlaced && placed == maxRooms - 1) return;
        }
        // ===========================================================

        bool neighbor =
            rooms.ContainsKey(gridPos + Vector2Int.left) ||
            rooms.ContainsKey(gridPos + Vector2Int.right) ||
            rooms.ContainsKey(gridPos + Vector2Int.up) ||
            rooms.ContainsKey(gridPos + Vector2Int.down);

        // pierwszy pokój można postawić zawsze
        bool placementRule = (rooms.Count == 0) || (gridPos.y == 0) || neighbor;
        if (!placementRule) return;

        Vector3 worldPos = originWorld + new Vector3(gx * roomW, gy * roomH, 0f);
        var prefab = roomPrefabs[prefabIndex];

        var go = Instantiate(prefab, worldPos, Quaternion.identity);
        go.name = $"Room_{gx}_{gy}";

        var roomNet = go.GetComponent<RoomNet>();
        if (roomNet != null)
            roomNet.InitGrid(gx, gy);
        else
            Debug.LogWarning("[SERVER] Room prefab has no RoomNet! (sync walls/grid won't work)");

        NetworkServer.Spawn(go);

        var ni = go.GetComponent<NetworkIdentity>();
        if (ni != null) rooms[gridPos] = ni;

        placedRoomsServer = rooms.Count; // sync dla klienta

        Debug.Log($"[SERVER] Spawned {go.name} netId={ni.netId} at {gridPos}");

        ConnectWithNeighbors(go, gridPos);

        // Jeśli to MainRoom: oznacz + teleport buildera na Spawn_Builder
        if (IsMainIndex(prefabIndex))
        {
            mainRoomPlaced = true;

            Transform spawnT = go.transform.Find("Spawn_Builder");
            Vector3 spawnPos = spawnT ? spawnT.position : go.transform.position;

            builderMainSpawnWorld = spawnPos;

            transform.position = spawnPos;
            var rb = GetComponent<Rigidbody2D>();
            if (rb) rb.linearVelocity = Vector2.zero;
        }
    }

    void ConnectWithNeighbors(GameObject room, Vector2Int pos)
    {
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        foreach (var dir in dirs)
        {
            var nPos = pos + dir;
            if (rooms.TryGetValue(nPos, out var neighborNi) && neighborNi != null)
            {
                ConnectTwoRooms(room, neighborNi.gameObject, dir);
            }
        }
    }

    void ConnectTwoRooms(GameObject a, GameObject b, Vector2Int dirFromAtoB)
    {
        var aNet = a.GetComponent<RoomNet>();
        var bNet = b.GetComponent<RoomNet>();
        if (!aNet || !bNet) return;

        if (dirFromAtoB == Vector2Int.right) { aNet.Open(WallType.Right);  bNet.Open(WallType.Left); }
        if (dirFromAtoB == Vector2Int.left)  { aNet.Open(WallType.Left);   bNet.Open(WallType.Right); }
        if (dirFromAtoB == Vector2Int.up)    { aNet.Open(WallType.Top);    bNet.Open(WallType.Bottom); }
        if (dirFromAtoB == Vector2Int.down)  { aNet.Open(WallType.Bottom); bNet.Open(WallType.Top); }
    }
}
