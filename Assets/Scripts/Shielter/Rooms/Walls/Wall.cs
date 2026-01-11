using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Wall : MonoBehaviour
{
    public WallState state = WallState.Exterior;
    public WallType wallType;

    [Header("Tiling fixes")]
    public bool rotateTilesOnVerticalWalls = true;
    public bool snapTilesToPixelGrid = true;


    [Header("Tile visuals (Option 2)")]
    public Sprite wallTileSprite;              // <- sprite pojedynczego kafla ściany
    public bool autoTileSizeFromSprite = false;
    public float tileSizeWorld = 1f;           // <- ustaw na 1 jeśli 1 tile = 1 unit
    public int sortingOrder = 10;
    public Color tileColor = Color.white;

    [Header("Hide base stretched sprite")]
    public bool hideBaseSpriteWhenTiling = true;

    private BoxCollider2D mainCol;
    private Transform visualsRoot;

    void Awake()
    {
        EnsureMainCol("Awake");
        EnsureVisualsRoot();

        if (autoTileSizeFromSprite && wallTileSprite != null)
        {
            // pewniejsze auto: rozmiar sprite w unitach
            float w = wallTileSprite.rect.width / wallTileSprite.pixelsPerUnit;
            float h = wallTileSprite.rect.height / wallTileSprite.pixelsPerUnit;
            tileSizeWorld = Mathf.Max(0.0001f, Mathf.Min(w, h));
        }

        ApplyBaseSpriteVisibility();
        RebuildVisuals();
    }

    void EnsureVisualsRoot()
    {
        var existing = transform.Find("Visuals");
        if (existing != null)
        {
            visualsRoot = existing;
            return;
        }

        visualsRoot = new GameObject("Visuals").transform;
        visualsRoot.SetParent(transform, false);
        visualsRoot.localPosition = Vector3.zero;
    }

    void ApplyBaseSpriteVisibility()
    {
        if (!hideBaseSpriteWhenTiling) return;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // chowamy rozciągnięty sprite, jeśli mamy tile sprite
            sr.enabled = (wallTileSprite == null);
        }
    }

    void EnsureMainCol(string from)
    {
        if (mainCol == null)
            mainCol = GetComponent<BoxCollider2D>();

        if (mainCol == null)
            Debug.LogError($"[Wall EnsureMainCol FAIL] ({from}) {transform.root.name}/{name} - brak BoxCollider2D!");
    }

    bool DoorAlreadyBuilt()
    {
        EnsureMainCol("DoorAlreadyBuilt");
        var cols = GetComponents<BoxCollider2D>();
        return (mainCol != null && !mainCol.enabled) || cols.Length > 1;
    }

    public void MakeInterior()
    {
        EnsureMainCol("MakeInterior");

        int colsBefore = GetComponents<BoxCollider2D>().Length;
        bool mainEnabled = mainCol != null && mainCol.enabled;

        Debug.Log($"[MakeInterior] {transform.root.name}/{name} state={state} colsBefore={colsBefore} mainColEnabled={mainEnabled}");

        if (DoorAlreadyBuilt())
        {
            Debug.Log($"[MakeInterior SKIP] {transform.root.name}/{name} already has door");
            return;
        }

        state = WallState.Interior;

        float doorWorld = GetDoorSizeWorld();
        Debug.Log($"[MakeInterior] {transform.root.name}/{name} building doorWorld={doorWorld}");

        if (wallType == WallType.Left || wallType == WallType.Right)
            CreateVerticalDoor_FromBottom(doorWorld);   // OD DOŁU
        else
            CreateHorizontalDoor_Centered(doorWorld);

        Physics2D.SyncTransforms();

        ApplyBaseSpriteVisibility();
        RebuildVisuals();

        int colsAfter = GetComponents<BoxCollider2D>().Length;
        Debug.Log($"[MakeInterior DONE] {transform.root.name}/{name} colsAfter={colsAfter}");
    }

    float GetDoorSizeWorld()
    {
        var player = FindAnyObjectByType<PlayerMovement>();
        if (player && player.TryGetComponent<BoxCollider2D>(out var pCol))
            return pCol.bounds.size.y * 2f;

        EnsureMainCol("GetDoorSizeWorld");
        return mainCol.bounds.size.y * 0.4f;
    }

    // ======================================================
    // VISUALS – kafelki generowane deterministycznie po colliderach
    // ======================================================

    void RebuildVisuals()
    {
        if (visualsRoot == null) return;

        // usuń stare kafle
        for (int i = visualsRoot.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying) Destroy(visualsRoot.GetChild(i).gameObject);
            else DestroyImmediate(visualsRoot.GetChild(i).gameObject);
        }

        if (wallTileSprite == null || tileSizeWorld <= 0.0001f)
            return;

        var cols = GetComponents<BoxCollider2D>();
        if (cols == null || cols.Length == 0)
            return;

        foreach (var c in cols)
        {
            if (c == null || !c.enabled) continue;
            BuildTilesForCollider(c);
        }
    }

 void BuildTilesForCollider(BoxCollider2D col)
{
    bool vertical = col.size.y >= col.size.x;

    // długość collidera w LOCAL
    float lengthLocal = vertical ? col.size.y : col.size.x;

    // tileSizeWorld -> tileSizeLocal (uwzględnij skalę obiektu)
    float axisScale = vertical ? Mathf.Abs(transform.lossyScale.y) : Mathf.Abs(transform.lossyScale.x);
    axisScale = Mathf.Max(axisScale, 0.0001f);
    float tileSizeLocal = tileSizeWorld / axisScale;

    // zakres pozycji środków kafli tak, żeby kafel NIE wyszedł poza collider
    float min = -lengthLocal * 0.5f + tileSizeLocal * 0.5f;
    float max =  lengthLocal * 0.5f - tileSizeLocal * 0.5f;

    // jeśli collider jest krótszy niż kafel -> 1 kafel
    if (max < min)
    {
        Vector2 localPos1 = vertical
            ? new Vector2(col.offset.x, col.offset.y)
            : new Vector2(col.offset.x, col.offset.y);

        Vector3 w1 = transform.TransformPoint(localPos1);
        CreateTile(w1, (vertical && rotateTilesOnVerticalWalls) ? Quaternion.Euler(0,0,90f) : Quaternion.identity);
        return;
    }

    float span = max - min;

    // count tak, by krok <= tileSizeLocal (żeby NIE było dziur),
    // a końce zawsze były trafione min i max
    int count = Mathf.CeilToInt(span / tileSizeLocal) + 1;
    count = Mathf.Max(2, count);

    float step = span / (count - 1); // <= tileSizeLocal

    Quaternion rot = Quaternion.identity;
    if (vertical && rotateTilesOnVerticalWalls)
        rot = Quaternion.Euler(0, 0, 90f);

    for (int i = 0; i < count; i++)
    {
        float t = min + i * step;

        Vector2 localPos = vertical
            ? new Vector2(col.offset.x, col.offset.y + t)
            : new Vector2(col.offset.x + t, col.offset.y);

        Vector3 worldPos = transform.TransformPoint(localPos);

        if (snapTilesToPixelGrid && wallTileSprite != null)
        {
            float ppu = wallTileSprite.pixelsPerUnit;
            float stepPix = 1f / Mathf.Max(1f, ppu);
            worldPos.x = Mathf.Round(worldPos.x / stepPix) * stepPix;
            worldPos.y = Mathf.Round(worldPos.y / stepPix) * stepPix;
        }

        CreateTile(worldPos, rot);
    }
}


void CreateTile(Vector3 worldPos, Quaternion rot)
{
    var tile = new GameObject("Tile");
    tile.transform.SetParent(visualsRoot, true);
    tile.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
    tile.transform.rotation = rot;
    tile.transform.localScale = Vector3.one;

    var sr = tile.AddComponent<SpriteRenderer>();
    sr.sprite = wallTileSprite;
    sr.sortingOrder = sortingOrder;
    sr.color = tileColor;
}


    // ======================================================
    // COLLIDERS – DRZWI
    // ======================================================

    // LEWA/PRAWA – drzwi OD DOŁU
    void CreateVerticalDoor_FromBottom(float doorHWorld)
    {
        EnsureMainCol("CreateVerticalDoor_FromBottom");

        float sy = Mathf.Max(Mathf.Abs(transform.lossyScale.y), 0.0001f);
        float doorHLocal = doorHWorld / sy;

        float fullLocal = mainCol.size.y;
        float thickLocal = mainCol.size.x;

        doorHLocal = Mathf.Clamp(doorHLocal, 0.1f, fullLocal * 0.95f);
        float topHeight = fullLocal - doorHLocal;

        mainCol.enabled = false;

        var top = gameObject.AddComponent<BoxCollider2D>();
        top.size = new Vector2(thickLocal, topHeight);

        float bottomY = -fullLocal / 2f;
        float topCenterY = bottomY + doorHLocal + topHeight / 2f;
        top.offset = new Vector2(0f, topCenterY);
    }

    // GÓRA/DÓŁ – drzwi na środku
    void CreateHorizontalDoor_Centered(float doorWWorld)
    {
        EnsureMainCol("CreateHorizontalDoor_Centered");

        float sx = Mathf.Max(Mathf.Abs(transform.lossyScale.x), 0.0001f);
        float doorWLocal = doorWWorld / sx;

        float fullLocal = mainCol.size.x;
        float thickLocal = mainCol.size.y;

        doorWLocal = Mathf.Clamp(doorWLocal, 0.1f, fullLocal * 0.9f);

        float rest = fullLocal - doorWLocal;
        float seg = rest * 0.5f;

        mainCol.enabled = false;

        var left = gameObject.AddComponent<BoxCollider2D>();
        left.size = new Vector2(seg, thickLocal);
        left.offset = new Vector2(-(doorWLocal * 0.5f + seg * 0.5f), 0f);

        var right = gameObject.AddComponent<BoxCollider2D>();
        right.size = new Vector2(seg, thickLocal);
        right.offset = new Vector2(doorWLocal * 0.5f + seg * 0.5f, 0f);
    }
}
