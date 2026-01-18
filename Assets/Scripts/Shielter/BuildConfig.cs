using UnityEngine;

public class BuildConfig : MonoBehaviour
{
    public static BuildConfig Instance { get; private set; }

    [Header("Grid")]
    public int roomW = 26;
    public int roomH = 12;
    public int maxRooms = 8;
    public int minGridY = 0;

    [Header("Origin in world")]
    public Vector3 originWorld = Vector3.zero;

    private void Awake()
    {
        Instance = this;
        originWorld.z = 0f;
    }
}
