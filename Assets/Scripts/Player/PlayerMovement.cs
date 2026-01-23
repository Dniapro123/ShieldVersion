using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Move")]
    public float speed = 10f;

    [Header("Jump")]
    public float jumpForce = 25f;
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.12f;
    [Range(0.5f, 1f)] public float groundCheckWidthScale = 0.9f;

    [Header("Optional visuals")]
    public bool flipByMoveDirection = true;

    Rigidbody2D rb;
    BoxCollider2D col;
    SpriteRenderer sr;
    Animator anim;

    float moveInput;
    bool jumpPressed;

    PhysicsMaterial2D noFrictionMat;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();

        rb.freezeRotation = true;
        EnsureNoFrictionMaterial();
    }

    public override void OnStartServer()
    {
        // Serwer MUSI mieć collider gracza w symulacji, inaczej pociski (server-authoritative)
        // nie wykryją trafienia. Jednocześnie nie chcemy, żeby serwer "dublował" fizykę ruchu klienta,
        // więc robimy Rigidbody2D kinematic.
        if (rb != null)
        {
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    public override void OnStartClient()
    {
        // Na zwykłych klientach wyłącz fizykę na zdalnych graczach.
        // UWAGA: nie na hoście (isServer == true), bo wtedy wyłączysz collider również po stronie serwera.
        if (!isServer && !isLocalPlayer && rb != null)
            rb.simulated = false;
    }

    public override void OnStartLocalPlayer()
    {
        if (rb != null)
            rb.simulated = true;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        moveInput = Input.GetAxisRaw("Horizontal");
        if (Input.GetKeyDown(KeyCode.Space))
            jumpPressed = true;

        if (flipByMoveDirection && sr != null)
        {
            if (moveInput > 0.01f) sr.flipX = false;
            else if (moveInput < -0.01f) sr.flipX = true;
        }

        if (anim != null && rb != null)
        {
            anim.SetBool("run", Mathf.Abs(moveInput) > 0.01f);
            anim.SetBool("grounded", IsGrounded());
            anim.SetFloat("yVel", rb.linearVelocity.y);
        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;
        if (rb == null) return;

        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);

        if (jumpPressed)
        {
            jumpPressed = false;
            if (IsGrounded())
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    bool IsGrounded()
    {
        if (col == null) return false;

        Vector2 size = new Vector2(col.bounds.size.x * groundCheckWidthScale, col.bounds.size.y);
        RaycastHit2D hit = Physics2D.BoxCast(col.bounds.center, size, 0f, Vector2.down, groundCheckDistance, groundLayer);
        return hit.collider != null;
    }

    void EnsureNoFrictionMaterial()
    {
        noFrictionMat = new PhysicsMaterial2D("NoFriction_Runtime")
        {
            friction = 0f,
            bounciness = 0f
        };

        if (col != null)
            col.sharedMaterial = noFrictionMat;
    }
}
