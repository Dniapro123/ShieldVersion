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

    [Header("Facing")]
    [Tooltip("Zaznacz jeśli sprite DOMYŚLNIE patrzy w PRAWO. Jeśli Twoja postać domyślnie patrzy w LEWO (i w prefabie masz FlipX=1), ustaw FALSE.")]
    public bool spriteFacesRight = false;
    public bool flipByMoveDirection = true;

    Rigidbody2D rb;
    BoxCollider2D col;
    SpriteRenderer sr;
    Animator anim;

    float moveInput;
    bool jumpPressed;

    float defaultGravity;

    PhysicsMaterial2D noFrictionMat;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();

        defaultGravity = rb != null ? rb.gravityScale : 1f;

        if (rb != null) rb.freezeRotation = true;
        EnsureNoFrictionMaterial();
    }

    static bool HasAnimParam(Animator a, string name, AnimatorControllerParameterType type)
    {
        if (a == null) return false;
        foreach (var p in a.parameters)
            if (p.name == name && p.type == type) return true;
        return false;
    }

    public override void OnStartServer()
    {
        // SERWER: NIE wyłączamy grawitacji.
        // Musi być simulated=true (trafienia), ale nie psujemy fizyki.
        if (rb != null)
        {
            rb.simulated = true;
            rb.freezeRotation = true;
        }
    }

    public override void OnStartClient()
    {
        // Remote na zwykłym kliencie może mieć wyłączoną fizykę (pozycja idzie z NetworkTransform).
        // Na hoście NIE wyłączamy, bo to ta sama instancja co serwer.
        if (!isLocalPlayer && rb != null && !isServer)
            rb.simulated = false;
    }

    public override void OnStartLocalPlayer()
    {
        if (rb != null)
        {
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = defaultGravity; // <<< kluczowe (naprawia "wiszenie" po host spawnie)
            rb.freezeRotation = true;
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        moveInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.Space))
            jumpPressed = true;

        // Animator (u Ciebie w controllerze są: Speed, Grounded, Attack, Hurt)
        if (anim != null)
        {
            if (HasAnimParam(anim, "Speed", AnimatorControllerParameterType.Float))
                anim.SetFloat("Speed", Mathf.Abs(moveInput));

            if (HasAnimParam(anim, "Grounded", AnimatorControllerParameterType.Bool))
                anim.SetBool("Grounded", IsGrounded());
        }

        // Facing / Flip
        if (flipByMoveDirection && sr != null)
        {
            if (moveInput > 0.01f)
                sr.flipX = spriteFacesRight ? false : true;   // idziesz w prawo
            else if (moveInput < -0.01f)
                sr.flipX = spriteFacesRight ? true : false;   // idziesz w lewo
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
