using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror; 

public class RoomBuildingManager : MonoBehaviour
{
    [Header("Prefaby pokoi (PPM przełącza w pętli)")]
    public List<GameObject> roomPrefabs = new();
    [Tooltip("Index w roomPrefabs, który jest 'MainRoom z reaktorem' (dostępny tylko raz)")]
    public int mainRoomPrefabIndex = 3;

    [Header("Start (siatka (0,0) w centrum)")]
    public GameObject startRoomInScene;
    public bool spawnStartRoom = true;
    public GameObject startRoomPrefab;

    [Header("Referencje")]
    public PlayerMovement player;

    [Header("Ustawienia siatki / limit")]
    public int maxRooms = 8;
    public int roomW = 26;
    public int roomH = 12;

    [Header("Ograniczenia budowy")]
    public int minGridY = 0;

    [Header("Sterowanie")]
    public KeyCode endBuildKey = KeyCode.Return;

    public event Action OnBuildFinished;

    // --- runtime ---
    private readonly Dictionary<Vector2Int, GameObject> rooms = new();
    private GameObject ghost;
    private SpriteRenderer[] ghostSprites;

    private Vector2Int gridPos;
    private bool canPlace;

    private int selectedIndex = 0;
    private bool mainRoomPlaced = false;
    private bool buildMode = true;
    private bool buildFinished = false;

    private Vector3 originWorld;

    void Start()
    {
        if (roomPrefabs == null || roomPrefabs.Count == 0)
        {
            Debug.LogError("[RBM Start] roomPrefabs is empty! Assign prefabs in inspector.");
            enabled = false;
            return;
        }

        originWorld = (startRoomInScene != null) ? startRoomInScene.transform.position : transform.position;
        originWorld.z = 0f;

        if (startRoomInScene != null)
        {
            rooms[Vector2Int.zero] = startRoomInScene;
            CleanupRoomInstance(startRoomInScene);
        }
        else if (spawnStartRoom)
        {
            var prefab = (startRoomPrefab != null) ? startRoomPrefab : roomPrefabs[0];
            var r = Instantiate(prefab, originWorld, Quaternion.identity);
            r.name = "StartRoom";
            rooms[Vector2Int.zero] = r;
            CleanupRoomInstance(r);
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, roomPrefabs.Count - 1);
        RecreateGhostForSelection();
    }

    void Update()
    {
        if (!buildMode) return;
        if (ghost == null) return;

        if (Input.GetMouseButtonDown(1))
            CycleRoomPrefab();

        UpdateGhost();

        if (Input.GetMouseButtonDown(0))
        {
            if (canPlace)
                PlaceRoom();
        }

        if (Input.GetKeyDown(endBuildKey))
        {
            EndBuild();
        }
    }

    GameObject GetSelectedPrefab()
    {
        selectedIndex = Mathf.Clamp(selectedIndex, 0, roomPrefabs.Count - 1);
        return roomPrefabs[selectedIndex];
    }

    bool IsSelectedMainRoom() => selectedIndex == mainRoomPrefabIndex;

    void CycleRoomPrefab()
    {
        int safety = 0;
        do
        {
            selectedIndex = (selectedIndex + 1) % roomPrefabs.Count;
            safety++;
            if (safety > roomPrefabs.Count + 2) break;
        }
        while (mainRoomPlaced && IsSelectedMainRoom());

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
    }

    void UpdateGhost()
    {
        Vector3 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouse.z = 0;

        int gx = Mathf.RoundToInt((mouse.x - originWorld.x) / roomW);
        int gy = Mathf.RoundToInt((mouse.y - originWorld.y) / roomH);

        gridPos = new Vector2Int(gx, gy);
        ghost.transform.position = originWorld + new Vector3(gx * roomW, gy * roomH, 0);

        bool withinY = gridPos.y >= minGridY;
        bool empty = !rooms.ContainsKey(gridPos);

        int placedExtra = rooms.ContainsKey(Vector2Int.zero) ? (rooms.Count - 1) : rooms.Count;
        bool limitOK = placedExtra < maxRooms;

        bool mainRoomOK = !(IsSelectedMainRoom() && mainRoomPlaced);

        bool neighbor =
            rooms.ContainsKey(gridPos + Vector2Int.left) ||
            rooms.ContainsKey(gridPos + Vector2Int.right) ||
            rooms.ContainsKey(gridPos + Vector2Int.up) ||
            rooms.ContainsKey(gridPos + Vector2Int.down);

        bool placementRule = (gridPos.y == 0) || neighbor;

        bool notStartCell = !(rooms.ContainsKey(Vector2Int.zero) && gridPos == Vector2Int.zero);

        canPlace = withinY && empty && limitOK && mainRoomOK && placementRule && notStartCell;

        Color c = canPlace ? new Color(0, 1, 0, 0.35f) : new Color(1, 0, 0, 0.35f);
        foreach (var s in ghostSprites) s.color = c;
    }

    void PlaceRoom()
    {
        var prefab = GetSelectedPrefab();

        GameObject r = Instantiate(prefab, ghost.transform.position, Quaternion.identity);
        r.name = $"Room_{gridPos.x}_{gridPos.y}";

        rooms[gridPos] = r;
        CleanupRoomInstance(r);

        ConnectWithNeighbors(r, gridPos);

        if (IsSelectedMainRoom())
            mainRoomPlaced = true;

        int placedExtra = rooms.ContainsKey(Vector2Int.zero) ? (rooms.Count - 1) : rooms.Count;
        if (placedExtra >= maxRooms)
            EndBuild();
    }

    void CleanupRoomInstance(GameObject room)
    {
        foreach (var wall in room.GetComponentsInChildren<Wall>(true))
        {
            wall.state = WallState.Exterior;

            var cols = wall.GetComponents<BoxCollider2D>();
            if (cols.Length > 0)
            {
                cols[0].enabled = true;
                for (int i = cols.Length - 1; i >= 1; i--)
                    Destroy(cols[i]);
            }
        }
    }

    void ConnectWithNeighbors(GameObject room, Vector2Int pos)
    {
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var dir in dirs)
        {
            Vector2Int nPos = pos + dir;
            if (rooms.TryGetValue(nPos, out GameObject neighbor))
                ConnectTwoRooms(room, neighbor, dir);
        }
    }

    void ConnectTwoRooms(GameObject a, GameObject b, Vector2Int dirFromAtoB)
    {
        Transform wallsA = FindWalls(a.transform);
        Transform wallsB = FindWalls(b.transform);
        if (wallsA == null || wallsB == null) return;

        string aWallName = null;
        string bWallName = null;

        if (dirFromAtoB == Vector2Int.right) { aWallName = "Wall_Right"; bWallName = "Wall_Left"; }
        else if (dirFromAtoB == Vector2Int.left) { aWallName = "Wall_Left"; bWallName = "Wall_Right"; }
        else if (dirFromAtoB == Vector2Int.up) { aWallName = "Wall_Top"; bWallName = "Wall_Bottom"; }
        else if (dirFromAtoB == Vector2Int.down) { aWallName = "Wall_Bottom"; bWallName = "Wall_Top"; }

        Transform aWallT = wallsA.Find(aWallName);
        Transform bWallT = wallsB.Find(bWallName);
        if (!aWallT || !bWallT) return;

        Wall aWall = aWallT.GetComponent<Wall>();
        Wall bWall = bWallT.GetComponent<Wall>();
        if (!aWall || !bWall) return;

        aWall.MakeInterior();
        bWall.MakeInterior();
    }

    Transform FindWalls(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == "Walls") return t;
        return null;
    }

    void EndBuild()
    {
        if (buildFinished) return;
        buildFinished = true;

        buildMode = false;

        if (ghost != null) Destroy(ghost);

        Debug.Log("[RBM EndBuild] build finished -> invoking OnBuildFinished");
        OnBuildFinished?.Invoke();
    }
}
