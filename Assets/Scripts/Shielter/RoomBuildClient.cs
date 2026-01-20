using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RoomBuildClient : NetworkBehaviour
{
    [Header("Same list/order as BuildCommands")]
    public List<GameObject> roomPrefabs = new();

    [Header("MainRoom rules (client preview)")]
    public int mainRoomPrefabIndex = 0;

    [Header("Optional: phase gating (recommended)")]
    public GameManagerPhases phases;

    [Header("Input")]
    public KeyCode cycleKey = KeyCode.Mouse1;   // PPM
    public KeyCode placeKey = KeyCode.Mouse0;   // LPM

    [Header("Ghost")]
    public int ghostSortingOrder = 999;
    public Color ghostOk = new(0, 1, 0, 0.35f);
    public Color ghostBad = new(1, 0, 0, 0.35f);

    int index;
    GameObject ghost;
    SpriteRenderer[] ghostSrs;

    BuildCommands build;
    PlayerRoleNet roleNet;
    Camera cam;

    bool lastCanBuild;

    void Awake()
    {
        cam = Camera.main;
        build = GetComponent<BuildCommands>();
        roleNet = GetComponent<PlayerRoleNet>();

        if (!phases) phases = FindAnyObjectByType<GameManagerPhases>();
    }

    public override void OnStartLocalPlayer()
    {
        lastCanBuild = false;
        RefreshGhostState(force: true);

        if (roleNet != null)
            roleNet.RoleChanged += OnRoleChanged;
    }

    public override void OnStopLocalPlayer()
    {
        if (roleNet != null)
            roleNet.RoleChanged -= OnRoleChanged;

        DestroyGhost();
    }

    void OnDisable() => DestroyGhost();
    void OnDestroy() => DestroyGhost();

    void OnRoleChanged(PlayerRole oldRole, PlayerRole newRole)
    {
        RefreshGhostState(force: true);
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        RefreshGhostState(force: false);
        if (!lastCanBuild) return;

        if (roomPrefabs == null || roomPrefabs.Count == 0) return;
        if (build == null) return;
        if (BuildConfig.Instance == null) return;

        if (Input.GetKeyDown(cycleKey))
        {
            index = (index + 1) % roomPrefabs.Count;
            CreateGhost();
        }

        UpdateGhost();

        if (Input.GetKeyDown(placeKey))
        {
            if (!TryGetGridFromMouse(out int gx, out int gy))
                return;

            if (!IsPlacementAllowed(gx, gy, index))
                return;

            build.CmdPlaceRoom(index, gx, gy);
        }
    }

    void RefreshGhostState(bool force)
    {
        bool canBuildNow = CanBuildNow();

        if (!force && canBuildNow == lastCanBuild) return;
        lastCanBuild = canBuildNow;

        if (!canBuildNow) DestroyGhost();
        else CreateGhost();
    }

    bool CanBuildNow()
    {
        if (roleNet == null || !roleNet.IsBuilder) return false;

        if (phases != null && phases.phase != GameManagerPhases.Phase.BuildRooms)
            return false;

        return true;
    }

    void CreateGhost()
    {
        DestroyGhost();
        if (roomPrefabs == null || roomPrefabs.Count == 0) return;

        var prefab = roomPrefabs[Mathf.Clamp(index, 0, roomPrefabs.Count - 1)];
        if (!prefab) return;

        ghost = Instantiate(prefab);
        ghost.name = "GhostRoom";
        ghost.transform.SetParent(transform, true);

        foreach (var ni in ghost.GetComponentsInChildren<NetworkIdentity>(true))
            DestroyImmediate(ni);
        foreach (var nb in ghost.GetComponentsInChildren<NetworkBehaviour>(true))
            DestroyImmediate(nb);

        foreach (var c in ghost.GetComponentsInChildren<Collider2D>(true))
            c.enabled = false;

        foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
            mb.enabled = false;

        ghostSrs = ghost.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in ghostSrs)
        {
            sr.sortingOrder = ghostSortingOrder;
            sr.color = ghostOk;
        }
    }

    void DestroyGhost()
    {
        if (ghost) Destroy(ghost);
        ghost = null;
        ghostSrs = null;
    }

    void UpdateGhost()
    {
        if (!ghost) return;

        if (!TryGetGridFromMouse(out int gx, out int gy))
            return;

        var cfg = BuildConfig.Instance;
        Vector3 pos = cfg.originWorld + new Vector3(gx * cfg.roomW, gy * cfg.roomH, 0f);
        ghost.transform.position = pos;

        bool ok = IsPlacementAllowed(gx, gy, index);
        ApplyGhostColor(ok);
    }

    void ApplyGhostColor(bool ok)
    {
        if (ghostSrs == null) return;
        var col = ok ? ghostOk : ghostBad;
        foreach (var sr in ghostSrs) sr.color = col;
    }

    bool IsPlacementAllowed(int gx, int gy, int prefabIndex)
    {
        var cfg = BuildConfig.Instance;
        if (!cfg) return false;

        if (prefabIndex < 0 || prefabIndex >= roomPrefabs.Count) return false;
        if (gy < cfg.minGridY) return false;

        // local occupancy check (żeby nie stawiać na zajętym polu)
        var allRooms = FindObjectsByType<RoomNet>(FindObjectsSortMode.None);
        if (IsOccupied(allRooms, gx, gy)) return false;

        // UWAGA: dla reguł "ostatni slot" używamy serwerowych SyncVar (stabilne)
        int placedServer = (build != null) ? build.placedRoomsServer : 0;
        bool mainPlacedServer = (build != null) && build.mainRoomPlaced;

        // fallback, jeśli SyncVar jeszcze nie doszło (np. w edytorze): policz lokalnie
        int placedLocal = allRooms != null ? allRooms.Length : 0;
        int placed = placedServer > 0 ? placedServer : placedLocal;

        if (placed >= cfg.maxRooms) return false;

        bool hasNeighbor =
            IsOccupied(allRooms, gx - 1, gy) ||
            IsOccupied(allRooms, gx + 1, gy) ||
            IsOccupied(allRooms, gx, gy - 1) ||
            IsOccupied(allRooms, gx, gy + 1);

        bool placementRule = (placed == 0) || (gy == 0) || hasNeighbor;
        if (!placementRule) return false;

        // ===== MainRoom rules (zgodnie z wymaganiem) =====
        bool isMain = prefabIndex == mainRoomPrefabIndex;

        if (isMain)
        {
            // MainRoom w dowolnym momencie, ale tylko raz
            if (mainPlacedServer) return false;
        }
        else
        {
            // jeśli to ostatni slot i MainRoom jeszcze nie ma -> blokuj zwykłe pokoje
            if (!mainPlacedServer && placed == cfg.maxRooms - 1) return false;
        }
        // ================================================

        return true;
    }

    bool IsOccupied(RoomNet[] rooms, int gx, int gy)
    {
        if (rooms == null) return false;
        for (int i = 0; i < rooms.Length; i++)
        {
            var r = rooms[i];
            if (!r) continue;
            if (r.gridX == gx && r.gridY == gy) return true;
        }
        return false;
    }

    bool TryGetGridFromMouse(out int gx, out int gy)
    {
        gx = 0; gy = 0;

        if (!cam) cam = Camera.main;
        var cfg = BuildConfig.Instance;
        if (!cfg) return false;

        Vector3 m = cam.ScreenToWorldPoint(Input.mousePosition);
        m.z = 0f;

        gx = Mathf.RoundToInt((m.x - cfg.originWorld.x) / cfg.roomW);
        gy = Mathf.RoundToInt((m.y - cfg.originWorld.y) / cfg.roomH);
        return true;
    }
}
