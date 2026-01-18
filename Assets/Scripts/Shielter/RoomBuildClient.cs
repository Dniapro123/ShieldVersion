using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RoomBuildClient : NetworkBehaviour
{
    [Header("Same list/order as BuildCommands")]
    public List<GameObject> roomPrefabs = new();

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

    void Awake()
    {
        cam = Camera.main;
        build = GetComponent<BuildCommands>();
        roleNet = GetComponent<PlayerRoleNet>();
    }

    public override void OnStartLocalPlayer()
    {
        // tylko local player ma UI/ghost
        if (roleNet != null)
        {
            roleNet.OnRoleChanged += _ => RefreshEnabled();
        }
        RefreshEnabled();
    }

    void OnDestroy()
    {
        if (roleNet != null)
            roleNet.OnRoleChanged -= _ => RefreshEnabled();
    }

    void RefreshEnabled()
    {
        bool canBuild = (roleNet != null && roleNet.IsBuilder);
        enabled = canBuild;

        if (!canBuild) DestroyGhost();
        else CreateGhost();
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (roleNet == null || !roleNet.IsBuilder) return;
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

    void CreateGhost()
    {
        DestroyGhost();
        if (roomPrefabs == null || roomPrefabs.Count == 0) return;

        var prefab = roomPrefabs[index];
        if (!prefab) return;

        ghost = Instantiate(prefab);
        ghost.name = "GhostRoom";
        ghost.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        // usuń NetworkIdentity z ghosta (żeby nie mieszać Mirror)
        foreach (var ni in ghost.GetComponentsInChildren<NetworkIdentity>(true))
            Destroy(ni);

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

        // tu możesz później dodać predykcję "czy wolno" (na razie zawsze OK)
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
