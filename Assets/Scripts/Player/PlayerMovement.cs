using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float jumpForce = 12f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private BoxCollider2D col;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
    }

    void Update()
    {
         Debug.Log("Grounded = " + IsGrounded());
        float input = Input.GetAxisRaw("Horizontal");

        // ДВИЖЕНИЕ
        rb.linearVelocity = new Vector2(input * speed, rb.linearVelocity.y);

        // ПРЫЖОК
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

bool IsGrounded()
{
    Vector2 size = new Vector2(col.bounds.size.x * 0.9f, col.bounds.size.y);
    return Physics2D.BoxCast(col.bounds.center, size, 0, Vector2.down, 0.1f, groundLayer);
}


}
