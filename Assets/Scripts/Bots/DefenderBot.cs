using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class DefenderBot : MonoBehaviour
{
    [Header("Basic")]
    public bool freezeInBuildPhases = true;

    Rigidbody2D rb;
    float prevGravity;
    RigidbodyType2D prevType;
    RigidbodyConstraints2D prevConstraints;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        CacheRb();
    }

    void CacheRb()
    {
        prevGravity = rb.gravityScale;
        prevType = rb.bodyType;
        prevConstraints = rb.constraints;
    }

    // wywołaj z GameManagera kiedy zaczyna się PLAY
    public void SetActiveGameplay(bool active)
    {
        if (!freezeInBuildPhases) return;

        if (active)
        {
            rb.bodyType = prevType;
            rb.gravityScale = prevGravity;
            rb.constraints = prevConstraints;
            rb.WakeUp();
        }
        else
        {
            CacheRb();
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }
}
