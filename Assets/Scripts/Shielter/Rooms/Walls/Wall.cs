using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Wall : MonoBehaviour
{
    public WallState state = WallState.Exterior;
    public WallType wallType;

    public Sprite exteriorSprite;
    public Sprite interiorSprite;

    private BoxCollider2D mainCol;
    private SpriteRenderer sr;

    void Awake()
    {
        mainCol = GetComponent<BoxCollider2D>();
        sr      = GetComponent<SpriteRenderer>();

        if (exteriorSprite != null)
            sr.sprite = exteriorSprite;

        SyncColliderToSprite();
    }

    /// Dopasuj BoxCollider2D do sprite'a (uwzględniając skalę obiektu).
    void SyncColliderToSprite()
    {
        if (sr.sprite == null) return;

        // rozmiar sprita w świecie
        Vector2 worldSize = sr.bounds.size;

        // skala obiektu
        Vector3 lossy = transform.lossyScale;

        // konwersja na local space
        float localW = worldSize.x / Mathf.Max(Mathf.Abs(lossy.x), 0.0001f);
        float localH = worldSize.y / Mathf.Max(Mathf.Abs(lossy.y), 0.0001f);

        mainCol.size   = new Vector2(localW, localH);
        mainCol.offset = Vector2.zero;
    }

    public void MakeInterior()
    {
        if (mainCol == null) mainCol = GetComponent<BoxCollider2D>();

        if (state == WallState.Interior)
            return;

        state = WallState.Interior;

        float doorHeightWorld = GetDoorHeightFromPlayer();

        if (wallType == WallType.Left || wallType == WallType.Right)
        {
            CreateVerticalDoor(doorHeightWorld);
        }
        else
        {
            CreateHorizontalDoor(doorHeightWorld);
        }

        if (interiorSprite != null)
            sr.sprite = interiorSprite;
    }

    float GetDoorHeightFromPlayer()
    {
        var player = FindAnyObjectByType<PlayerMovement>();
        if (player == null) 
            return mainCol.bounds.size.y * 0.4f;

        if (!player.TryGetComponent<BoxCollider2D>(out var pCol))
            return mainCol.bounds.size.y * 0.4f;

        return pCol.bounds.size.y * 2f;   // „2x wysokość gracza”
    }

    // ------- PIONOWE DRZWI (lewa/prawa ściana) -------

void CreateVerticalDoor(float doorHeightWorld)
{
    float full = mainCol.size.y;           // np. 12
    float thick = mainCol.size.x;

    float doorH = doorHeightWorld / transform.localScale.y;

    float topSeg = Mathf.Max(0, full - doorH);

    mainCol.enabled = false;

    // ---- GÓRA ŚCIANY ----
    var top = gameObject.AddComponent<BoxCollider2D>();
    top.size = new Vector2(thick, topSeg);

    // offset liczymy OD DOŁU, nie od środka!
    float bottomY = -full / 2f;  // np. -6

    float topOffset = bottomY + doorH + (topSeg / 2f);

    top.offset = new Vector2(0, topOffset);
}



    // ------- POZIOME DRZWI (górna/dolna ściana) -------

    void CreateHorizontalDoor(float doorHeightWorld)
    {
        Vector3 lossy = transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(lossy.x), 0.0001f);

        float fullLocalWidth = mainCol.size.x;
        float thickLocal     = mainCol.size.y;

        // szerokość drzwi w LOCAL (używamy tej samej „doorHeightWorld” jako szerokości otworu)
        float doorLocal = doorHeightWorld / sx;
        doorLocal = Mathf.Clamp(doorLocal, 0.1f, fullLocalWidth * 0.9f);

        float restLocal = fullLocalWidth - doorLocal;
        float segLocal  = restLocal * 0.5f;

        Vector2 center = mainCol.offset;

        mainCol.enabled = false;

        // lewy segment
        var left = gameObject.AddComponent<BoxCollider2D>();
        left.size   = new Vector2(segLocal, thickLocal);
        left.offset = new Vector2(
            center.x - (doorLocal * 0.5f + segLocal * 0.5f),
            center.y);

        // prawy segment
        var right = gameObject.AddComponent<BoxCollider2D>();
        right.size   = new Vector2(segLocal, thickLocal);
        right.offset = new Vector2(
            center.x + (doorLocal * 0.5f + segLocal * 0.5f),
            center.y);
    }
}
