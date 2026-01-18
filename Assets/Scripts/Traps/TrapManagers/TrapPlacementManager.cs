using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class TrapPlacementManager : MonoBehaviour
{
    [Serializable]
    public class TrapDef
    {
        public string id;
        public GameObject prefab;
        public int limit = 3;
        [HideInInspector] public int used;
        public TrapAttach attach = TrapAttach.Any;
    }

    public enum TrapAttach
    {
        Any,
        FloorOnly,
        CeilingOnly,
        WallOnly,
        FloorCeiling,
        FloorWall,
        CeilingWall
    }

    public event Action OnTrapPlacementFinished;

    [Header("Trap list + limity")]
    public List<TrapDef> traps = new();

    [Header("Placement")]
    [Tooltip("USTAW: tylko warstwy ścian/podłogi/sufitu (np. Wall + Ground).")]
    public LayerMask surfaceMask;

    public float maxSnapDistance = 4f;
    public KeyCode finishKey = KeyCode.Return;

    [Header("Ghost")]
    public int ghostSortingOrder = 999;
    public Color ghostOk = new(0, 1, 0, 0.35f);
    public Color ghostBad = new(1, 0, 0, 0.35f);

    private int index = 0;
    private GameObject ghost;
    private SpriteRenderer[] ghostSrs;
    private TrapPlaceable ghostPlaceable;

    private bool canPlace;
    private Vector3 placePos;
    private Quaternion placeRot;

    private Camera cam;

    static readonly Vector2[] RayDirs =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    void Awake()
    {
        cam = Camera.main;
    }

    void OnEnable()
    {
        // KLUCZ: w edytorze (bez Play) NIC nie tworzymy
        if (!Application.isPlaying) return;

        if (traps == null || traps.Count == 0)
        {
            Debug.LogWarning("[TrapPlacement] No traps configured!");
            return;
        }

        index = Mathf.Clamp(index, 0, traps.Count - 1);
        CreateGhostForCurrent();
        Debug.Log("[TrapPlacement] ENABLED");
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;
        DestroyGhost();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (traps == null || traps.Count == 0) return;

        if (Input.GetKeyDown(finishKey))
        {
            Debug.Log("[TrapPlacement] FINISHED via Enter");
            OnTrapPlacementFinished?.Invoke();
            return;
        }

        if (Input.GetMouseButtonDown(1))
            NextTrap();

        UpdateGhost();

        if (Input.GetMouseButtonDown(0) && canPlace)
            PlaceTrap();
    }

    void NextTrap()
    {
        index = (index + 1) % traps.Count;
        CreateGhostForCurrent();
        Debug.Log($"[TrapPlacement] Selected trap: {Current().id} ({Current().used}/{Current().limit})");
    }

    TrapDef Current() => traps[index];

    // =========================
    // GHOST
    // =========================

    void CreateGhostForCurrent()
    {
        DestroyGhost();

        var def = Current();
        if (!def.prefab)
        {
            Debug.LogError("[TrapPlacement] Trap prefab is NULL");
            return;
        }

        ghost = Instantiate(def.prefab);
        ghost.name = $"GhostTrap_{def.id}";

        // ✅ super ważne: ghost nie ma prawa zapisać się do sceny
        ghost.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        // ✅ i jeszcze: usuń NetworkIdentity z ghosta (żeby Mirror go nie traktował jak network/scene object)
        foreach (var ni in ghost.GetComponentsInChildren<NetworkIdentity>(true))
            Destroy(ni);

        // wyłącz collidery w ghost
        foreach (var c in ghost.GetComponentsInChildren<Collider2D>(true))
            c.enabled = false;

        // wyłącz rigidbody w ghost
        var rb = ghost.GetComponentInChildren<Rigidbody2D>(true);
        if (rb) rb.simulated = false;

        // wyłącz WSZYSTKIE skrypty (żeby ghost nie odpalał logiki), zostaw TrapPlaceable
        foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb is TrapPlaceable) continue;
            mb.enabled = false;
        }

        ghostPlaceable = ghost.GetComponentInChildren<TrapPlaceable>(true);

        ghostSrs = ghost.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in ghostSrs)
            sr.sortingOrder = ghostSortingOrder;
    }

    void DestroyGhost()
    {
        if (ghost) Destroy(ghost);
        ghost = null;
        ghostSrs = null;
        ghostPlaceable = null;
    }

    void UpdateGhost()
    {
        if (!ghost) return;

        bool limitOk = Current().used < Current().limit;

        Vector3 mouse = GetMouseWorld();
        mouse.z = 0;

        if (ghostPlaceable != null && ghostPlaceable.mode == TrapPlacementMode.Free)
        {
            placePos = mouse;
            placeRot = Quaternion.identity;

            canPlace = limitOk;
            ghost.transform.SetPositionAndRotation(placePos, placeRot);
            ApplyGhostColor(canPlace);
            return;
        }

        if (!TryPickSurface(mouse, out Vector2 hitPoint, out Vector2 hitNormal))
        {
            canPlace = false;
            ghost.transform.SetPositionAndRotation(mouse, Quaternion.identity);
            ApplyGhostColor(false);
            return;
        }

        bool attachOk = AttachRuleOk(Current().attach, hitNormal);

        bool placeableOk = true;
        float offset = 0f;

        if (ghostPlaceable != null)
        {
            offset = ghostPlaceable.surfaceOffset;

            TrapSurfaceMask s = SurfaceMaskFromNormal(hitNormal);
            placeableOk = (ghostPlaceable.allowedSurfaces & s) != 0;
        }

        canPlace = limitOk && attachOk && placeableOk;

        placePos = hitPoint + hitNormal.normalized * offset;

        if (ghostPlaceable != null)
        {
            Vector2 from = ghostPlaceable.FacingAxisVector();
            Vector2 to = hitNormal.normalized;
            float ang = Vector2.SignedAngle(from, to);
            placeRot = Quaternion.Euler(0, 0, ang);
        }
        else
        {
            placeRot = RotationFromNormal(hitNormal);
        }

        ghost.transform.SetPositionAndRotation(placePos, placeRot);
        ApplyGhostColor(canPlace);
    }

    Vector3 GetMouseWorld()
    {
        if (!cam) cam = Camera.main;
        Vector3 m = cam.ScreenToWorldPoint(Input.mousePosition);
        m.z = 0f;
        return m;
    }

    void ApplyGhostColor(bool ok)
    {
        if (ghostSrs == null) return;
        var col = ok ? ghostOk : ghostBad;
        foreach (var sr in ghostSrs) sr.color = col;
    }

    // =========================
    // SURFACE PICK
    // =========================

    bool TryPickSurface(Vector2 origin, out Vector2 point, out Vector2 normal)
    {
        point = origin;
        normal = Vector2.up;

        RaycastHit2D best = default;
        float bestDist = float.PositiveInfinity;

        foreach (var d in RayDirs)
        {
            var h = Physics2D.Raycast(origin, d, maxSnapDistance, surfaceMask);
            if (!h.collider) continue;

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
            }
        }

        if (!best.collider)
            return false;

        point = best.point;
        normal = best.normal;
        return true;
    }

    TrapSurfaceMask SurfaceMaskFromNormal(Vector2 n)
    {
        if (n.y > 0.5f) return TrapSurfaceMask.Floor;
        if (n.y < -0.5f) return TrapSurfaceMask.Ceiling;
        if (n.x > 0.5f) return TrapSurfaceMask.LeftWall;
        if (n.x < -0.5f) return TrapSurfaceMask.RightWall;
        return TrapSurfaceMask.None;
    }

    Quaternion RotationFromNormal(Vector2 n)
    {
        float angle = Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0, 0, angle - 90f);
    }

    bool AttachRuleOk(TrapAttach rule, Vector2 n)
    {
        bool floor = n.y > 0.5f;
        bool ceiling = n.y < -0.5f;
        bool wall = Mathf.Abs(n.x) > 0.5f;

        return rule switch
        {
            TrapAttach.Any => true,
            TrapAttach.FloorOnly => floor,
            TrapAttach.CeilingOnly => ceiling,
            TrapAttach.WallOnly => wall,
            TrapAttach.FloorCeiling => floor || ceiling,
            TrapAttach.FloorWall => floor || wall,
            TrapAttach.CeilingWall => ceiling || wall,
            _ => true
        };
    }

    // =========================
    // PLACE
    // =========================

    void PlaceTrap()
    {
        var def = Current();
        if (def.used >= def.limit) return;

        if (!def.prefab)
        {
            Debug.LogError("[TrapPlacement] Cannot place: prefab null");
            return;
        }

        Instantiate(def.prefab, placePos, placeRot);
        def.used++;

        Debug.Log($"[TrapPlacement] Placed {def.id} used={def.used}/{def.limit}");
    }
}
