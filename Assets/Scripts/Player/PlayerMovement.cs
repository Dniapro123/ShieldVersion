using UnityEngine;
using Mirror;

public class PlayerMovement : NetworkBehaviour
{
    public float speed = 10f;
    public float jumpForce = 25f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private BoxCollider2D col;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
    }

    public override void OnStartClient()
    {
        // Na zdalnych graczach wyłącz fizykę, żeby nie spadali/nie glitchowali.
        // (Transform będzie i tak aktualizowany przez NetworkTransform)
        if (!isLocalPlayer && rb != null)
            rb.simulated = false;
    }

    public override void OnStartLocalPlayer()
    {
        // Lokalny gracz ma mieć fizykę
        if (rb != null)
            rb.simulated = true;
    }

    void Update()
    {
        // Sterowanie tylko dla lokalnego gracza
        if (!isLocalPlayer) return;

        float input = Input.GetAxisRaw("Horizontal");

        // ruch
        rb.linearVelocity = new Vector2(input * speed, rb.linearVelocity.y);

        // skok
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    bool IsGrounded()
    {
        if (col == null) return false;

        Vector2 size = new Vector2(col.bounds.size.x * 0.9f, col.bounds.size.y);
        return Physics2D.BoxCast(col.bounds.center, size, 0f, Vector2.down, 0.1f, groundLayer);
    }
}
