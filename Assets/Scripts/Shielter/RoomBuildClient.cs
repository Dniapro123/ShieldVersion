using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RoomBuildClient : NetworkBehaviour
{
    [Header("Same list/order as BuildCommands")]
    public List<GameObject> roomPrefabs = new();

    [Header("Optional: phase gating (recommended)")]
    public GameManagerPhases phases; // znajdzie się automatycznie

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
        // lokalny gracz kontroluje ghost
        lastCanBuild = false;
        RefreshGhostState(force: true);

        // jeżeli masz RoleChanged (hook), to się podepniemy
        if (roleNet != null)
            roleNet.RoleChanged += OnRoleChanged;
    }

    public override void OnStopLocalPlayer()
    {
        // gdy rozłączasz / stop host / niszczy się player -> sprzątaj
        if (roleNet != null)
            roleNet.RoleChanged -= OnRoleChanged;

        DestroyGhost();
    }

    void OnDisable()
    {
        // jak wyłączysz komponent (np. faza Play) też sprzątaj
        DestroyGhost();
    }

    void OnDestroy()
    {
        DestroyGhost();
    }

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
            if (TryGetGridFromMouse(out int gx, out int gy))
            {
                // wysyłamy do serwera
                build.CmdPlaceRoom(index, gx, gy);
            }
        }
    }

    void RefreshGhostState(bool force)
    {
        bool canBuildNow = CanBuildNow();

        if (!force && canBuildNow == lastCanBuild) return;
        lastCanBuild = canBuildNow;

        if (!canBuildNow)
        {
            DestroyGhost();
        }
        else
        {
            CreateGhost();
        }
    }

    bool CanBuildNow()
    {
        if (roleNet == null || !roleNet.IsBuilder) return false;

        // mega ważne: nie pokazuj ghosta w Play / Traps
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

        // NAJWAŻNIEJSZE: zrób go dzieckiem local playera -> zniknie gdy player znika
        ghost.transform.SetParent(transform, true);

        // NAJWAŻNIEJSZE #2: usuń natychmiast Mirror komponenty, żeby Mirror nie krzyczał w OnValidate
        foreach (var ni in ghost.GetComponentsInChildren<NetworkIdentity>(true))
            DestroyImmediate(ni);

        foreach (var nb in ghost.GetComponentsInChildren<NetworkBehaviour>(true))
            DestroyImmediate(nb);

        // off colliders
        foreach (var c in ghost.GetComponentsInChildren<Collider2D>(true))
            c.enabled = false;

        // off wszystkie skrypty (poza SR)
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

        ApplyGhostColor(true);
    }

    void ApplyGhostColor(bool ok)
    {
        if (ghostSrs == null) return;
        var col = ok ? ghostOk : ghostBad;
        foreach (var sr in ghostSrs) sr.color = col;
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
