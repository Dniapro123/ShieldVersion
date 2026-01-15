using System.Collections.Generic;
using UnityEngine;

public class RoomBuildingManager : MonoBehaviour
{
    [Header("Prefaby pokoi (PPM przełącza w pętli)")]
    public List<GameObject> roomPrefabs = new();     // np. 4 elementy
    [Tooltip("Index w roomPrefabs, który jest 'MainRoom z reaktorem' (dostępny tylko raz)")]
    public int mainRoomPrefabIndex = 3;

    [Header("Start (siatka (0,0) w centrum)")]
    [Tooltip("Jeśli podasz obiekt ze sceny, to użyjemy go jako startowego pokoju w grid(0,0).")]
    public GameObject startRoomInScene;

    [Tooltip("Jeśli startRoomInScene jest NULL i to TRUE, to zespawnimy startRoomPrefab.")]
    public bool spawnStartRoom = true;

    [Tooltip("Prefab startowego pokoju. Jeśli NULL -> roomPrefabs[0].")]
    public GameObject startRoomPrefab;

    [Header("Referencje")]
    public PlayerMovement player;

    [Header("Ustawienia siatki / limit")]
    [Tooltip("Ile pokoi można jeszcze postawić (NIE licząc pokoju startowego w (0,0)).")]
    public int maxRooms = 8;
    public int roomW = 26;
    public int roomH = 12;

    [Header("Ograniczenia budowy")]
    [Tooltip("Najniższy dozwolony poziom siatki. 0 blokuje budowę pod ziemią.")]
    public int minGridY = 0;

    [Header("Sterowanie")]
    public KeyCode endBuildKey = KeyCode.Return;

    // --- runtime ---
    private readonly Dictionary<Vector2Int, GameObject> rooms = new();
    private GameObject ghost;
    private SpriteRenderer[] ghostSprites;

    private Vector2Int gridPos;
    private bool canPlace;

    private int selectedIndex = 0;
    private bool mainRoomPlaced = false;
    private bool buildMode = true;

    private Vector3 originWorld; // światowy punkt (0,0) siatki

    // freeze gracza w build mode
    private Rigidbody2D playerRb;
    private RigidbodyType2D prevBodyType;
    private float prevGravity;
    private RigidbodyConstraints2D prevConstraints;

    void Start()
    {
        if (roomPrefabs == null || roomPrefabs.Count == 0)
        {
            Debug.LogError("[RBM Start] roomPrefabs is empty! Assign prefabs in inspector.");
            enabled = false;
            return;
        }

        // Origin siatki: pozycja startRoomInScene albo tego GameObjectu (na którym jest RBM)
        originWorld = (startRoomInScene != null) ? startRoomInScene.transform.position : transform.position;
        originWorld.z = 0f;

        Debug.Log($"[RBM Start] originWorld={originWorld}");

        // Pokój startowy w (0,0) — opcjonalnie
        if (startRoomInScene != null)
        {
            rooms[Vector2Int.zero] = startRoomInScene;
            Debug.Log($"[RBM Start] using startRoomInScene='{startRoomInScene.name}' at grid(0,0)");
            CleanupRoomInstance(startRoomInScene);
        }
        else if (spawnStartRoom)
        {
            var prefab = (startRoomPrefab != null) ? startRoomPrefab : roomPrefabs[0];
            var r = Instantiate(prefab, originWorld, Quaternion.identity);
            r.name = "StartRoom";
            rooms[Vector2Int.zero] = r;

            Debug.Log($"[RBM Start] spawned start room from prefab='{prefab.name}' at grid(0,0)");
            CleanupRoomInstance(r);
        }
        else
        {
            Debug.Log("[RBM Start] No start room. First placed room can become first anchor.");
        }

        Debug.Log($"[RBM Start] rooms.Count={rooms.Count}");

        EnterBuildMode();

        selectedIndex = Mathf.Clamp(selectedIndex, 0, roomPrefabs.Count - 1);
        RecreateGhostForSelection();
    }

    void Update()
    {
        if (!buildMode) return;
        if (ghost == null) return;

        // PPM -> zmiana prefabów
        if (Input.GetMouseButtonDown(1))
            CycleRoomPrefab();

        UpdateGhost();

        // LPM -> place
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[RBM Click] canPlace={canPlace} gridPos={gridPos} selected='{GetSelectedPrefab().name}'");
            if (canPlace)
                PlaceRoom();
        }

        // Enter -> end build
        if (Input.GetKeyDown(endBuildKey))
        {
            Debug.Log("[RBM] EndBuild requested");
            EndBuild();
        }
    }

    // =========================
    // BUILD MODE (player freeze)
    // =========================

    void EnterBuildMode()
    {
        buildMode = true;

        if (player != null)
        {
            player.enabled = false;
            Debug.Log("[RBM] player script disabled (build mode)");

            playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                prevBodyType = playerRb.bodyType;
                prevGravity = playerRb.gravityScale;
                prevConstraints = playerRb.constraints;

                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                playerRb.gravityScale = 0f;
                playerRb.bodyType = RigidbodyType2D.Kinematic;
                playerRb.constraints = RigidbodyConstraints2D.FreezeAll;

                Debug.Log("[RBM] player Rigidbody2D frozen (no falling)");
            }
        }
    }

    void ExitBuildMode()
    {
        buildMode = false;

        if (player != null)
        {
            player.enabled = true;

            if (playerRb != null)
            {
                playerRb.bodyType = prevBodyType;
                playerRb.gravityScale = prevGravity;
                playerRb.constraints = prevConstraints;
                playerRb.linearVelocity = Vector2.zero;

                Debug.Log("[RBM] player Rigidbody2D restored");
            }

            Debug.Log("[RBM] player script enabled (game mode)");
        }
    }

    // =========================
    // SELECTION / GHOST
    // =========================

    GameObject GetSelectedPrefab()
    {
        selectedIndex = Mathf.Clamp(selectedIndex, 0, roomPrefabs.Count - 1);
        return roomPrefabs[selectedIndex];
    }

    bool IsSelectedMainRoom()
        => selectedIndex == mainRoomPrefabIndex;

    void CycleRoomPrefab()
    {
        // jeśli mainRoom już postawiony, to przy przełączaniu pomijamy go
        int safety = 0;
        do
        {
            selectedIndex = (selectedIndex + 1) % roomPrefabs.Count;
            safety++;
            if (safety > roomPrefabs.Count + 2) break;
        }
        while (mainRoomPlaced && IsSelectedMainRoom());

        Debug.Log($"[RBM Select] selectedIndex={selectedIndex} prefab='{GetSelectedPrefab().name}' mainRoom={(IsSelectedMainRoom() ? "YES" : "NO")} placed={(mainRoomPlaced ? "YES" : "NO")}");
        RecreateGhostForSelection();
    }

    void RecreateGhostForSelection()
    {
        if (ghost != null) Destroy(ghost);

        var prefab = GetSelectedPrefab();
        ghost = Instantiate(prefab);
        ghost.name = "GhostRoom";

        foreach (var c in ghost.GetComponentsInChildren<Collider2D>(true))
            c.enabled = false;

        foreach (var w in ghost.GetComponentsInChildren<Wall>(true))
            w.enabled = false;

        ghostSprites = ghost.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var s in ghostSprites)
            s.sortingOrder = 999;

        Debug.Log($"[RBM Ghost] created ghost from '{prefab.name}'");
    }

    void UpdateGhost()
    {
        Vector3 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouse.z = 0;

        int gx = Mathf.RoundToInt((mouse.x - originWorld.x) / roomW);
        int gy = Mathf.RoundToInt((mouse.y - originWorld.y) / roomH);

        gridPos = new Vector2Int(gx, gy);
        ghost.transform.position = originWorld + new Vector3(gx * roomW, gy * roomH, 0);

        // 1) nie budujemy pod ziemią
        bool withinY = gridPos.y >= minGridY;

        // 2) puste pole
        bool empty = !rooms.ContainsKey(gridPos);

        // 3) limit pokoi (maxRooms nie liczy pokoju startowego w (0,0))
        int placedExtra = rooms.ContainsKey(Vector2Int.zero) ? (rooms.Count - 1) : rooms.Count;
        bool limitOK = placedExtra < maxRooms;

        // 4) mainroom tylko raz
        bool mainRoomOK = !(IsSelectedMainRoom() && mainRoomPlaced);

        // 5) reguła połączeń: można stawiać jeśli (y==0) LUB ma sąsiada
        bool neighbor =
            rooms.ContainsKey(gridPos + Vector2Int.left) ||
            rooms.ContainsKey(gridPos + Vector2Int.right) ||
            rooms.ContainsKey(gridPos + Vector2Int.up) ||
            rooms.ContainsKey(gridPos + Vector2Int.down);

        bool placementRule = (gridPos.y == 0) || neighbor;

        // 6) nie nadpisuj startowej komórki (0,0) jeśli start istnieje
        bool notStartCell = !(rooms.ContainsKey(Vector2Int.zero) && gridPos == Vector2Int.zero);

        canPlace = withinY && empty && limitOK && mainRoomOK && placementRule && notStartCell;

        Color c = canPlace ? new Color(0, 1, 0, 0.35f) : new Color(1, 0, 0, 0.35f);
        foreach (var s in ghostSprites) s.color = c;
    }

    // =========================
    // PLACE
    // =========================

    void PlaceRoom()
    {
        var prefab = GetSelectedPrefab();

        Debug.Log($"[RBM PlaceRoom] placing '{prefab.name}' at gridPos={gridPos}, worldPos={ghost.transform.position}");

        GameObject r = Instantiate(prefab, ghost.transform.position, Quaternion.identity);
        r.name = $"Room_{gridPos.x}_{gridPos.y}";

        rooms[gridPos] = r;
        CleanupRoomInstance(r);

        Debug.Log($"[RBM PlaceRoom] instantiated '{r.name}'. rooms.Count={rooms.Count}");

        ConnectWithNeighbors(r, gridPos);

        if (IsSelectedMainRoom())
        {
            mainRoomPlaced = true;
            Debug.Log("[RBM] MainRoom placed (only once). Build continues until Enter or limit.");
        }

        // auto-koniec jeśli skończyły się sloty
        int placedExtra = rooms.ContainsKey(Vector2Int.zero) ? (rooms.Count - 1) : rooms.Count;
        if (placedExtra >= maxRooms)
        {
            Debug.Log("[RBM] maxRooms reached -> ending build mode");
            EndBuild();
        }
    }

    // czyści śmieciowe collidery (jeśli prefab się kiedyś “pobrudził”)
    void CleanupRoomInstance(GameObject room)
    {
        foreach (var wall in room.GetComponentsInChildren<Wall>(true))
        {
            wall.state = WallState.Exterior;

            // usuń dodatkowe BoxCollidery, zostaw 1 (główny)
            var cols = wall.GetComponents<BoxCollider2D>();
            if (cols.Length > 0)
            {
                cols[0].enabled = true;
                for (int i = cols.Length - 1; i >= 1; i--)
                    Destroy(cols[i]);
            }
        }
    }

    // =========================
    // CONNECT
    // =========================

    void ConnectWithNeighbors(GameObject room, Vector2Int pos)
    {
        Debug.Log($"[RBM ConnectWithNeighbors] room='{room.name}' pos={pos}");

        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

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
        if (wallsA == null || wallsB == null) return;

        string aWallName = null;
        string bWallName = null;

        if (dirFromAtoB == Vector2Int.right) { aWallName = "Wall_Right"; bWallName = "Wall_Left"; }
        else if (dirFromAtoB == Vector2Int.left) { aWallName = "Wall_Left"; bWallName = "Wall_Right"; }
        else if (dirFromAtoB == Vector2Int.up) { aWallName = "Wall_Top"; bWallName = "Wall_Bottom"; }
        else if (dirFromAtoB == Vector2Int.down) { aWallName = "Wall_Bottom"; bWallName = "Wall_Top"; }

        Transform aWallT = wallsA.Find(aWallName);
        Transform bWallT = wallsB.Find(bWallName);

        if (!aWallT || !bWallT)
        {
            Debug.LogError($"[RBM] Missing wall transform! A.{aWallName} or B.{bWallName}");
            return;
        }

        Wall aWall = aWallT.GetComponent<Wall>();
        Wall bWall = bWallT.GetComponent<Wall>();
        if (!aWall || !bWall)
        {
            Debug.LogError("[RBM] Missing Wall component on one of the walls!");
            return;
        }

        int aColsBefore = aWallT.GetComponents<BoxCollider2D>().Length;
        int bColsBefore = bWallT.GetComponents<BoxCollider2D>().Length;

        Debug.Log($"[RBM BEFORE] A={a.name}/{aWallName} state={aWall.state} cols={aColsBefore} | " +
                  $"B={b.name}/{bWallName} state={bWall.state} cols={bColsBefore}");

        aWall.MakeInterior();
        bWall.MakeInterior();

        int aColsAfter = aWallT.GetComponents<BoxCollider2D>().Length;
        int bColsAfter = bWallT.GetComponents<BoxCollider2D>().Length;

        Debug.Log($"[RBM AFTER]  A={a.name}/{aWallName} state={aWall.state} cols={aColsAfter} | " +
                  $"B={b.name}/{bWallName} state={bWall.state} cols={bColsAfter}");
    }

    Transform FindWalls(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == "Walls") return t;
        return null;
    }

    // =========================
    // END BUILD
    // =========================

    void EndBuild()
    {
        ExitBuildMode();

        if (ghost != null) Destroy(ghost);

        Debug.Log("[RBM EndBuild] build done, ghost destroyed");
    }
}
