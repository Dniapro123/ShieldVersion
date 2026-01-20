using Mirror;
using UnityEngine;

public class TrapPlaceClient : NetworkBehaviour
{
    public KeyCode cycleKey = KeyCode.Mouse1;
    public KeyCode placeKey = KeyCode.Mouse0;
    public KeyCode rotateKey = KeyCode.R;

    public int ghostSortingOrder = 999;
    public Color ghostOk = new(0, 1, 0, 0.35f);
    public Color ghostBad = new(1, 0, 0, 0.35f);

    int trapIndex;
    int rotSteps90;

    TrapConfig cfg;
    TrapCommands cmds;
    PlayerRoleNet roleNet;
    Camera cam;

    GameObject ghostRoot;
    SpriteRenderer[] ghostSrs;

    void Awake()
    {
        cam = Camera.main;
        cmds = GetComponent<TrapCommands>();
        roleNet = GetComponent<PlayerRoleNet>();
    }

    public override void OnStartLocalPlayer()
    {
        cfg = TrapConfig.Instance != null ? TrapConfig.Instance : FindAnyObjectByType<TrapConfig>();
        CreateGhost();
    }

    void OnDisable() => DestroyGhost();

    void Update()
    {
        if (!isLocalPlayer) return;
        if (cfg == null || cfg.Count == 0) return;
        if (roleNet == null || roleNet.role != PlayerRole.Builder) return;

        if (Input.GetKeyDown(cycleKey))
        {
            trapIndex = (trapIndex + 1) % cfg.Count;
            rotSteps90 = 0;
            CreateGhost();
        }

        var def = cfg.Get(trapIndex);
        if (def == null) return;

        if (Input.GetKeyDown(rotateKey))
        {
            if (!def.allowRotate) rotSteps90 = 0;
            else
            {
                int next = (rotSteps90 + 1) % 4;
                // skacz do następnego dozwolonego kroku
                for (int i = 0; i < 4; i++)
                {
                    int clamped = cfg.ClampRotStep(def, next);
                    if (clamped == next) { rotSteps90 = next; break; }
                    next = (next + 1) % 4;
                }
                rotSteps90 = cfg.ClampRotStep(def, rotSteps90);
            }
        }

        bool ok = ComputePlacement(def, out Vector3 desiredPos, out Vector2 normal, out RoomNet room);

        if (ghostRoot)
        {
            ghostRoot.transform.position = desiredPos;
            ghostRoot.transform.rotation = Quaternion.Euler(0, 0, ComputeAngle(def, normal, rotSteps90));
            ApplyGhost(ok);
        }

        if (ok && Input.GetKeyDown(placeKey))
        {
            cmds.CmdPlaceTrap(trapIndex, desiredPos, normal, rotSteps90);
        }
    }

    float ComputeAngle(TrapConfig.TrapDef def, Vector2 normal, int rot90)
    {
        rot90 = cfg.ClampRotStep(def, rot90);

        if (def.forceFixedAngle)
            return def.fixedAngleDeg + def.angleOffsetDeg;

        float baseAngle = (def.attach == TrapConfig.TrapAttach.Interior) ? 0f : cfg.BaseAngleFromNormal(normal);
        return baseAngle + def.angleOffsetDeg + rot90 * 90f;
    }

    bool ComputePlacement(TrapConfig.TrapDef def, out Vector3 desiredPos, out Vector2 normal, out RoomNet room)
    {
        desiredPos = Vector3.zero;
        normal = Vector2.up;
        room = null;

        if (!cam) cam = Camera.main;

        Vector3 m = cam.ScreenToWorldPoint(Input.mousePosition);
        m.z = 0f;

        // 1) musimy być w PlaceArea (wtedy wiemy, który pokój)
        Collider2D placeCol = Physics2D.OverlapPoint(m, cfg.placeAreaMask);
        if (!placeCol) return false;

        room = placeCol.GetComponentInParent<RoomNet>();
        if (!room) return false;

        int roomHash = room.RoomHash;

        // 2) limity: global + per-room
        if (cmds != null)
        {
            if (def.limitGlobal > 0 && cmds.GetGlobalCount(trapIndex) >= def.limitGlobal) return false;
            if (def.maxPerRoom > 0 && cmds.GetPerRoomCount(roomHash, trapIndex) >= def.maxPerRoom) return false;
        }

        // 3) pozycja
        if (def.attach == TrapConfig.TrapAttach.Interior)
        {
            desiredPos = m;
            desiredPos = cfg.SnapPos(desiredPos, def.positionSnapStep);
            normal = Vector2.up;
            return true;
        }

        // 4) Surface: znajdź najbliższą ścianę/podłogę w tym samym pokoju
        if (!TryGetBestSurfaceHit(m, room, out RaycastHit2D bestHit))
            return false;

        if (!cfg.AttachAllows(def.attach, bestHit.collider))
            return false;

        normal = bestHit.normal;
        if (normal.sqrMagnitude < 0.0001f) normal = Vector2.up;

        desiredPos = bestHit.point + normal.normalized * cfg.placeOffset;
        desiredPos.z = 0f;

        desiredPos = cfg.SnapPos(desiredPos, def.positionSnapStep);
        return true;
    }

    bool TryGetBestSurfaceHit(Vector2 origin, RoomNet room, out RaycastHit2D best)
    {
        best = default;
        float bestDist = float.MaxValue;
        bool found = false;

        Vector2[] dirs = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };

        foreach (var d in dirs)
        {
            var h = Physics2D.Raycast(origin, d, cfg.maxSnapDistance, cfg.SurfaceMask);
            if (!h.collider) continue;

            // musi być ta sama RoomNet (żeby nie łapało ścian innego pokoju)
            var surfRoom = h.collider.GetComponentInParent<RoomNet>();
            if (surfRoom != room) continue;

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                found = true;
            }
        }

        return found;
    }

    void CreateGhost()
    {
        DestroyGhost();
        if (cfg == null) return;

        var def = cfg.Get(trapIndex);
        if (def == null || def.prefab == null) return;

        ghostRoot = new GameObject("TrapGhost");
        ghostRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        var src = def.prefab.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var s in src)
        {
            var go = new GameObject(s.gameObject.name);
            go.transform.SetParent(ghostRoot.transform, false);
            go.transform.localPosition = s.transform.localPosition;
            go.transform.localRotation = s.transform.localRotation;
            go.transform.localScale = s.transform.localScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s.sprite;
            sr.flipX = s.flipX;
            sr.flipY = s.flipY;
            sr.sortingLayerID = s.sortingLayerID;
            sr.sortingOrder = ghostSortingOrder + s.sortingOrder;
            sr.color = ghostOk;
        }

        ghostSrs = ghostRoot.GetComponentsInChildren<SpriteRenderer>(true);
    }

    void DestroyGhost()
    {
        if (ghostRoot) Destroy(ghostRoot);
        ghostRoot = null;
        ghostSrs = null;
    }

    void ApplyGhost(bool ok)
    {
        if (ghostSrs == null) return;
        var c = ok ? ghostOk : ghostBad;
        foreach (var sr in ghostSrs) sr.color = c;
    }
}
