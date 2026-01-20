using Mirror;
using UnityEngine;

public class TrapCommands : NetworkBehaviour
{
    TrapConfig cfg;
    PlayerRoleNet roleNet;

    // globalne liczniki (na typ pułapki)
    public class SyncCounts : SyncDictionary<int, int> { }
    public SyncCounts placedGlobal = new SyncCounts();

    // per-room liczniki: key = roomHash XOR (trapIndex * prime)
    public class SyncRoomCounts : SyncDictionary<int, int> { }
    public SyncRoomCounts placedPerRoom = new SyncRoomCounts();

    const int TrapPrime = 83492791;

    void Awake()
    {
        roleNet = GetComponent<PlayerRoleNet>();
    }

    public override void OnStartServer()
    {
        cfg = TrapConfig.Instance != null ? TrapConfig.Instance : FindAnyObjectByType<TrapConfig>();
        if (cfg == null) Debug.LogError("[SERVER] No TrapConfig in scene!");
    }

    int GetCount(SyncDictionary<int, int> dict, int key) => dict.TryGetValue(key, out int c) ? c : 0;
    void Inc(SyncDictionary<int, int> dict, int key) => dict[key] = GetCount(dict, key) + 1;

    static int RoomTrapKey(int roomHash, int trapIndex) => unchecked(roomHash ^ (trapIndex * TrapPrime));

    // używane przez klienta do ghost-limitów
    public int GetGlobalCount(int trapIndex) => GetCount(placedGlobal, trapIndex);
    public int GetPerRoomCount(int roomHash, int trapIndex) => GetCount(placedPerRoom, RoomTrapKey(roomHash, trapIndex));

    [Command]
    public void CmdPlaceTrap(int trapIndex, Vector3 desiredPos, Vector2 clientNormal, int rotSteps90)
    {
        if (roleNet == null || roleNet.role != PlayerRole.Builder) return;

        if (cfg == null) cfg = TrapConfig.Instance != null ? TrapConfig.Instance : FindAnyObjectByType<TrapConfig>();
        if (cfg == null) return;

        var def = cfg.Get(trapIndex);
        if (def == null || def.prefab == null) return;

        desiredPos.z = 0f;

        // 1) MUSI być w PlaceArea => wtedy wiemy “jaki pokój”
        Collider2D placeCol = Physics2D.OverlapPoint(desiredPos, cfg.placeAreaMask);
        if (!placeCol) return;

        RoomNet room = placeCol.GetComponentInParent<RoomNet>();
        if (!room) return;

        int roomHash = room.RoomHash;
        int roomKey = RoomTrapKey(roomHash, trapIndex);

        // 2) limity
        if (def.limitGlobal > 0 && GetGlobalCount(trapIndex) >= def.limitGlobal) return;
        if (def.maxPerRoom > 0 && GetCount(placedPerRoom, roomKey) >= def.maxPerRoom) return;

        // 3) walidacja attach + finalna pozycja (serwer liczy finalPos)
        Vector3 finalPos = desiredPos;
        Vector2 normal = clientNormal;
        if (normal.sqrMagnitude < 0.0001f) normal = Vector2.up;

        if (def.attach == TrapConfig.TrapAttach.Interior)
        {
            // “w powietrzu” – ale nadal tylko w PlaceArea
            finalPos = placeCol.ClosestPoint(desiredPos);
            finalPos.z = 0f;
        }
        else
        {
            // musi być przy powierzchni (floor/wall)
            Vector2 probe = (Vector2)desiredPos - normal.normalized * cfg.placeOffset;
            Collider2D surface = Physics2D.OverlapCircle(probe, cfg.validateRadius, cfg.SurfaceMask);
            if (!surface) return;

            // powierzchnia MUSI należeć do tego samego pokoju
            var surfRoom = surface.GetComponentInParent<RoomNet>();
            if (surfRoom != room) return;

            if (!cfg.AttachAllows(def.attach, surface)) return;

            Vector2 closest = surface.ClosestPoint(desiredPos);
            Vector2 n = (Vector2)desiredPos - closest;
            if (n.sqrMagnitude < 0.0001f) n = normal;
            normal = n.normalized;

            finalPos = (Vector3)closest + (Vector3)(normal * cfg.placeOffset);
            finalPos.z = 0f;
        }

        // snap pozycji (np. boty do “stałej siatki”)
        finalPos = cfg.SnapPos(finalPos, def.positionSnapStep);

        // 4) rotacja – clamp + opcjonalne wymuszenie
        rotSteps90 = cfg.ClampRotStep(def, rotSteps90);

        float angle;
        if (def.forceFixedAngle)
        {
            angle = def.fixedAngleDeg + def.angleOffsetDeg;
        }
        else
        {
            float baseAngle = (def.attach == TrapConfig.TrapAttach.Interior) ? 0f : cfg.BaseAngleFromNormal(normal);
            angle = baseAngle + def.angleOffsetDeg + rotSteps90 * 90f;
        }

        // 5) spawn
        var go = Instantiate(def.prefab, finalPos, Quaternion.Euler(0, 0, angle));
        NetworkServer.Spawn(go);

        Inc(placedGlobal, trapIndex);
        Inc(placedPerRoom, roomKey);
    }
}
