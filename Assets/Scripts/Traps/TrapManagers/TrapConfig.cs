using System;
using System.Collections.Generic;
using UnityEngine;

public class TrapConfig : MonoBehaviour
{
    public static TrapConfig Instance { get; private set; }

    public enum TrapAttach
    {
        FloorOnly,
        WallOnly,
        SurfaceAny,   // wall OR floor
        Interior      // dowolny punkt WEWNĄTRZ PlaceArea (czyli “w powietrzu”)
    }

    [Serializable]
    public class TrapDef
    {
        public GameObject prefab;

        [Header("Limits")]
        public int limitGlobal = 3;  // max łącznie (dla buildera)
        public int maxPerRoom = 1;   // max w jednym pokoju (1 = “tylko jedna”)

        [Header("Attach")]
        public TrapAttach attach = TrapAttach.SurfaceAny;

        [Header("Rotation")]
        public float angleOffsetDeg = 0f;   // korekta prefabów (jeśli “patrzą” inaczej)
        public bool allowRotate = true;     // czy R działa
        public int[] allowedRotSteps90 = { 0, 1, 2, 3 }; // dozwolone 0/90/180/270
        public bool forceFixedAngle = false; // np. boty
        public float fixedAngleDeg = 0f;     // używane gdy forceFixedAngle=true

        [Header("Position Snap (optional)")]
        public float positionSnapStep = 0f; // 0 = brak, np. 0.5f lub 1f = “stała siatka”
    }

    [Header("Masks (USTAW W INSPECTORZE)")]
    public LayerMask floorMask;       // Ground
    public LayerMask wallMask;        // Wall
    public LayerMask placeAreaMask;   // PlaceArea (trigger collider)

    [Header("Placement settings")]
    public float maxSnapDistance = 4f;
    public float placeOffset = 0.06f;
    public float validateRadius = 0.2f; // serwerowa walidacja “czy blisko surface”

    [Header("Traps")]
    public List<TrapDef> traps = new();

    void Awake() => Instance = this;

    public int Count => traps?.Count ?? 0;

    public TrapDef Get(int idx)
    {
        if (traps == null) return null;
        if (idx < 0 || idx >= traps.Count) return null;
        return traps[idx];
    }

    public LayerMask SurfaceMask => floorMask | wallMask;

    public bool IsFloorLayer(int layer) => ((1 << layer) & floorMask.value) != 0;
    public bool IsWallLayer(int layer)  => ((1 << layer) & wallMask.value) != 0;

    public bool AttachAllows(TrapAttach a, Collider2D surface)
    {
        if (!surface) return false;

        bool isFloor = IsFloorLayer(surface.gameObject.layer);
        bool isWall  = IsWallLayer(surface.gameObject.layer);

        return a switch
        {
            TrapAttach.FloorOnly => isFloor,
            TrapAttach.WallOnly => isWall,
            TrapAttach.SurfaceAny => isFloor || isWall,
            _ => false
        };
    }

    public float BaseAngleFromNormal(Vector2 n)
    {
        if (n.sqrMagnitude < 0.0001f) n = Vector2.up;
        return Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg;
    }

    public int ClampRotStep(TrapDef def, int step)
    {
        step = ((step % 4) + 4) % 4;

        if (def == null) return 0;
        if (!def.allowRotate) return 0;

        if (def.allowedRotSteps90 == null || def.allowedRotSteps90.Length == 0)
            return 0;

        foreach (var s in def.allowedRotSteps90)
        {
            int ss = ((s % 4) + 4) % 4;
            if (ss == step) return step;
        }

        // fallback: pierwszy dozwolony
        int first = ((def.allowedRotSteps90[0] % 4) + 4) % 4;
        return first;
    }

    public Vector3 SnapPos(Vector3 p, float step)
    {
        if (step <= 0f) return p;
        p.x = Mathf.Round(p.x / step) * step;
        p.y = Mathf.Round(p.y / step) * step;
        p.z = 0f;
        return p;
    }
}
