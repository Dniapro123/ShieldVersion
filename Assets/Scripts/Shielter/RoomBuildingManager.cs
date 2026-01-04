using System.Collections.Generic;
using UnityEngine;

public class RoomBuildingManager : MonoBehaviour
{
    [Header("Prefaby / referencje")]
    public GameObject roomPrefab;
    public GameObject mainRoom;        // Object above first room
    public PlayerMovement player;

    [Header("Ustawienia")]
    public int maxRooms = 6;
    public int roomW = 20;             // size +1
    public int roomH = 12;             // Size + 0

    private Dictionary<Vector2Int, GameObject> rooms = new();
    private GameObject ghost;
    private SpriteRenderer[] ghostSprites;
    private Vector2Int gridPos;
    private bool canPlace;

    void Start()
    {
        rooms[Vector2Int.zero] = mainRoom;

        if (player != null)
            player.enabled = false;

        CreateGhost();
    }
        //Obsługa mysza  
        //mouse
    void Update()
    {
        if (ghost == null) return;

        UpdateGhost();

        if (Input.GetMouseButtonDown(0) && canPlace)
            PlaceRoom();

        if (Input.GetKeyDown(KeyCode.Return))
            EndBuild();
    }

    void CreateGhost()
    {
        ghost = Instantiate(roomPrefab);

        // colliderów nie kasuje – tylko wyłącza
        foreach (var c in ghost.GetComponentsInChildren<Collider2D>())
            c.enabled = false;

        // GHOST nie może wykonywać logiki ścian
        foreach (var w in ghost.GetComponentsInChildren<Wall>())
            w.enabled = false; 

        ghostSprites = ghost.GetComponentsInChildren<SpriteRenderer>();
        foreach (var s in ghostSprites)
            s.sortingOrder = 999;
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

    void PlaceRoom()
    {
        GameObject r = Instantiate(roomPrefab, ghost.transform.position, Quaternion.identity);

        // upewniamy się, że w prawdziwym pokoju Wall.cs jest włączony
        ResetWallScripts(r); //

        rooms[gridPos] = r;
        ConnectWithNeighbors(r, gridPos);
        ConnectWithNeighbors(mainRoom, Vector2Int.zero);

    }

    // Przywraca działanie skryptów Wall w nowym pokoju
    private void ResetWallScripts(GameObject room)
    {
        foreach (var w in room.GetComponentsInChildren<Wall>(true))
            w.enabled = true;
    }

    // --- ŁĄCZENIE POKOJÓW ---

    void ConnectWithNeighbors(GameObject room, Vector2Int pos)
    {
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
                ConnectTwoRooms(room, neighbor, dir);
            }
        }
    }

    void ConnectTwoRooms(GameObject a, GameObject b, Vector2Int dirFromAtoB)
    {
        Transform wallsA = FindWalls(a.transform);
        Transform wallsB = FindWalls(b.transform);

        if (wallsA == null || wallsB == null)
        {
            Debug.LogWarning("Nie znaleziono 'Walls' w którymś pokoju");
            return;
        }

        if (dirFromAtoB == Vector2Int.right)
        {
            wallsA.Find("Wall_Right").GetComponent<Wall>().MakeInterior();
            wallsB.Find("Wall_Left").GetComponent<Wall>().MakeInterior();
        }
        else if (dirFromAtoB == Vector2Int.left)
        {
            wallsA.Find("Wall_Left").GetComponent<Wall>().MakeInterior();
            wallsB.Find("Wall_Right").GetComponent<Wall>().MakeInterior();
        }
        else if (dirFromAtoB == Vector2Int.up)
        {
            wallsA.Find("Wall_Top").GetComponent<Wall>().MakeInterior();
            wallsB.Find("Wall_Bottom").GetComponent<Wall>().MakeInterior();
        }
        else if (dirFromAtoB == Vector2Int.down)
        {
            wallsA.Find("Wall_Bottom").GetComponent<Wall>().MakeInterior();
            wallsB.Find("Wall_Top").GetComponent<Wall>().MakeInterior();
        }
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

    void EndBuild()
    {
        if (player != null) player.enabled = true;
        if (ghost != null) Destroy(ghost);

        Debug.Log("Build done.");
    }
}
