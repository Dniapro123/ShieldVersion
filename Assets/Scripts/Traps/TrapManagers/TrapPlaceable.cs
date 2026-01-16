using UnityEngine;

public enum TrapPlacementMode
{
    Free,       // może stać w powietrzu / wszędzie (byle nie pod ziemią)
    Surface     // musi być przyczepiona do ściany/podłogi/sufitu
}

[System.Flags]
public enum TrapSurfaceMask
{
    None   = 0,
    Floor  = 1 << 0,   // normal = (0, 1)
    Ceiling= 1 << 1,   // normal = (0,-1)
    LeftWall  = 1 << 2,// normal = ( 1,0) -> ściana po lewej, trap „patrzy” w prawo
    RightWall = 1 << 3 // normal = (-1,0)
}

public enum TrapFacingAxis
{
    Up,
    Right,
    Down,
    Left
}

public class TrapPlaceable : MonoBehaviour
{
    [Header("Placement rules")]
    public TrapPlacementMode mode = TrapPlacementMode.Surface;
    public TrapSurfaceMask allowedSurfaces =
        TrapSurfaceMask.Floor | TrapSurfaceMask.Ceiling | TrapSurfaceMask.LeftWall | TrapSurfaceMask.RightWall;

    [Header("Orientation")]
    [Tooltip("Jaka oś w prefabie jest 'kierunkiem działania' (np. strzał leci w prawo => Right).")]
    public TrapFacingAxis facingAxis = TrapFacingAxis.Right;

    [Tooltip("Mały odsuw od ściany żeby nie wchodziło w collider (world units).")]
    public float surfaceOffset = 0.02f;

    public Vector2 FacingAxisVector()
    {
        return facingAxis switch
        {
            TrapFacingAxis.Up => Vector2.up,
            TrapFacingAxis.Right => Vector2.right,
            TrapFacingAxis.Down => Vector2.down,
            TrapFacingAxis.Left => Vector2.left,
            _ => Vector2.right
        };
    }
}
