using System.Collections.Generic;
using UnityEngine;

public class RoomBuildingManager : MonoBehaviour
{
    [Header("Prefaby / referencje")]
    public GameObject roomPrefab;
    public GameObject mainRoom;        // pokój startowy na scenie
    public PlayerMovement player;

    [Header("Ustawienia")]
    public int maxRooms = 8;
    public int roomW = 40
    ;
    public int roomH = 12;

    private Dictionary<Vector2Int, GameObject> rooms = new();
    private GameObject ghost;
    private SpriteRenderer[] ghostSprites;
    private Vector2Int gridPos;
    private bool canPlace;

    void Start()
    {
        rooms[Vector2Int.zero] = mainRoom;

        Debug.Log($"[RBM Start] mainRoom='{(mainRoom ? mainRoom.name : "NULL")}' at grid (0,0)");
        Debug.Log($"[RBM Start] rooms.Count={rooms.Count}");

        if (player != null)
        {
            player.enabled = false;
            Debug.Log("[RBM Start] player disabled for build mode");
        }

        CreateGhost();
    }

    void Update()
    {
        if (ghost == null) return;

        UpdateGhost();

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[RBM Click] canPlace={canPlace} gridPos={gridPos}");
            if (canPlace)
                PlaceRoom();
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log("[RBM] EndBuild requested (Enter)");
            EndBuild();
        }
    }

    // =========================
    // GHOST
    // =========================

    void CreateGhost()
    {
        ghost = Instantiate(roomPrefab);
        ghost.name = "GhostRoom";

        foreach (var c in ghost.GetComponentsInChildren<Collider2D>(true))
            c.enabled = false;

        foreach (var w in ghost.GetComponentsInChildren<Wall>(true))
            w.enabled = false;

        ghostSprites = ghost.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var s in ghostSprites)
            s.sortingOrder = 999;

        Debug.Log($"[RBM Ghost] created ghost from prefab '{roomPrefab.name}', disabled colliders & Wall scripts");
    }

    void UpdateGhost()
    {
        Vector3 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouse.z = 0;

        int gx = Mathf.RoundToInt(mouse.x / roomW);
        int gy = Mathf.RoundToInt(mouse.y / roomH);

        gridPos = new Vector2Int(gx, gy);
        ghost.transform.position = new Vector3(gx * roomW, gy * roomH, 0);

        bool empty   = !rooms.ContainsKey(gridPos);
        bool notMain = gridPos != Vector2Int.zero;
        bool limitOK = rooms.Count - 1 < maxRooms;

        bool neighbor =
            rooms.ContainsKey(gridPos + Vector2Int.left)  ||
            rooms.ContainsKey(gridPos + Vector2Int.right) ||
            rooms.ContainsKey(gridPos + Vector2Int.up)    ||
            rooms.ContainsKey(gridPos + Vector2Int.down);

        canPlace = empty && notMain && limitOK && neighbor;

        Color c = canPlace ? new Color(0, 1, 0, 0.35f)
                           : new Color(1, 0, 0, 0.35f);
        foreach (var s in ghostSprites) s.color = c;
    }

    // =========================
    // PLACEMENT
    // =========================

    void PlaceRoom()
    {
        Debug.Log($"[RBM PlaceRoom] placing at gridPos={gridPos}, worldPos={ghost.transform.position}");

        GameObject r = Instantiate(roomPrefab, ghost.transform.position, Quaternion.identity);
        r.name = $"Room_{gridPos.x}_{gridPos.y}";

        rooms[gridPos] = r;

        Debug.Log($"[RBM PlaceRoom] instantiated '{r.name}'. rooms.Count={rooms.Count}");

        ConnectWithNeighbors(r, gridPos);
    }

    // =========================
    // CONNECTING
    // =========================

    void ConnectWithNeighbors(GameObject room, Vector2Int pos)
    {
        Debug.Log($"[RBM ConnectWithNeighbors] room='{room.name}' pos={pos}");

        Vector2Int[] dirs =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        foreach (var dir in dirs)
        {
            Vector2Int nPos = pos + dir;

            if (rooms.TryGetValue(nPos, out GameObject neighbor))
            {
                Debug.Log($"[RBM Neighbor FOUND] '{room.name}' has neighbor '{neighbor.name}' at {nPos} dir={dir}");
                ConnectTwoRooms(room, neighbor, dir);
            }
            else
            {
                Debug.Log($"[RBM Neighbor NONE] '{room.name}' no neighbor at {nPos} dir={dir}");
            }
        }
    }

    void ConnectTwoRooms(GameObject a, GameObject b, Vector2Int dirFromAtoB)
    {
        Debug.Log($"[RBM ConnectTwoRooms] A='{a.name}' B='{b.name}' dir={dirFromAtoB}");

        Transform wallsA = FindWalls(a.transform);
        Transform wallsB = FindWalls(b.transform);

        Debug.Log($"[RBM FindWalls] A walls={(wallsA ? "OK" : "NULL")} | B walls={(wallsB ? "OK" : "NULL")}");

        if (wallsA == null || wallsB == null)
        {
            Debug.LogWarning("[RBM] Nie znaleziono 'Walls' w którymś pokoju");
            return;
        }

        // wybierz nazwy ścian po obu stronach
        string aWallName = null;
        string bWallName = null;

        if (dirFromAtoB == Vector2Int.right) { aWallName = "Wall_Right";  bWallName = "Wall_Left"; }
        if (dirFromAtoB == Vector2Int.left)  { aWallName = "Wall_Left";   bWallName = "Wall_Right"; }
        if (dirFromAtoB == Vector2Int.up)    { aWallName = "Wall_Top";    bWallName = "Wall_Bottom"; }
        if (dirFromAtoB == Vector2Int.down)  { aWallName = "Wall_Bottom"; bWallName = "Wall_Top"; }

        if (aWallName == null || bWallName == null)
        {
            Debug.LogError("[RBM] Unknown direction mapping!");
            return;
        }

        Transform aWallT = wallsA.Find(aWallName);
        Transform bWallT = wallsB.Find(bWallName);

        Debug.Log($"[RBM Walls Find] A.{aWallName}={(aWallT ? "OK" : "NULL")} | B.{bWallName}={(bWallT ? "OK" : "NULL")}");

        if (!aWallT || !bWallT)
        {
            Debug.LogError($"[RBM] Missing wall transform! A.{aWallName} or B.{bWallName}");
            return;
        }

        Wall aWall = aWallT.GetComponent<Wall>();
        Wall bWall = bWallT.GetComponent<Wall>();

        Debug.Log($"[RBM Wall Script] A Wall={(aWall ? "OK" : "NULL")} | B Wall={(bWall ? "OK" : "NULL")}");

        if (!aWall || !bWall)
        {
            Debug.LogError("[RBM] Missing Wall component on one of the walls!");
            return;
        }

        // LOGI STANU + COLLIDERÓW PRZED
        int aColsBefore = aWallT.GetComponents<BoxCollider2D>().Length;
        int bColsBefore = bWallT.GetComponents<BoxCollider2D>().Length;

        Debug.Log($"[RBM BEFORE] A={a.name}/{aWallName} state={aWall.state} cols={aColsBefore} | " +
                  $"B={b.name}/{bWallName} state={bWall.state} cols={bColsBefore}");

        // wywołanie drzwi
        aWall.MakeInterior();
        bWall.MakeInterior();

        // LOGI STANU + COLLIDERÓW PO
        int aColsAfter = aWallT.GetComponents<BoxCollider2D>().Length;
        int bColsAfter = bWallT.GetComponents<BoxCollider2D>().Length;

        Debug.Log($"[RBM AFTER]  A={a.name}/{aWallName} state={aWall.state} cols={aColsAfter} | " +
                  $"B={b.name}/{bWallName} state={bWall.state} cols={bColsAfter}");
    }

    Transform FindWalls(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Walls")
                return t;
        }
        return null;
    }

    // =========================
    // END BUILD
    // =========================

    void EndBuild()
    {
        if (player != null) player.enabled = true;
        if (ghost != null) Destroy(ghost);

        Debug.Log("[RBM EndBuild] build done, player enabled, ghost destroyed");
    }
}
