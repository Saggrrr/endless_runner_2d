using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Endless runner player controller:
/// - Tap/click/Space to jump (double jump supported)
/// - Grounded check using a downward raycast
/// - Physics-based via Rigidbody2D
/// - On obstacle collision: notifies GameManager (lives/death)
/// - On death: freezes player so it stays visible
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerController : MonoBehaviour
{
    [Header("Jump")]
    [SerializeField] private float jumpImpulse = 12f;
    [SerializeField] private int maxJumps = 2;

    [Header("Ground Check")]
    [SerializeField] private float groundRayExtraDistance = 0.08f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Fail Conditions")]
    [SerializeField] private float fallFailY = -10f;

    private Rigidbody2D _rb;
    private Collider2D _col;

    private int _jumpsRemaining;
    private bool _runStarted;
    private bool _frozen;

    private void Awake()
    {
        // Rigidbody2D is required for jumping/physics.
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();

        _rb.gravityScale = Mathf.Max(_rb.gravityScale, 3f);
        _rb.freezeRotation = true;

        // Ensure the player has a real (non-trigger) collider so collisions work.
        _col = GetComponent<Collider2D>();
        if (_col == null) _col = gameObject.AddComponent<BoxCollider2D>();
        _col.isTrigger = false;

        _jumpsRemaining = maxJumps;
    }

    private void Update()
    {
        if (!_runStarted || _frozen)
            return;

        bool tapped = IsTapPressedThisFrame();
        bool space = IsSpacePressedThisFrame();

        if ((tapped || space) && _jumpsRemaining > 0)
            DoJump();

        // Falling below a threshold ends the run.
        var gm = GameManager.Instance;
        if (gm != null && gm.State == GameManager.GameState.Playing && transform.position.y < fallFailY)
            gm.KillPlayer();
    }

    private void FixedUpdate()
    {
        if (_frozen)
            return;

        if (IsGrounded())
            _jumpsRemaining = maxJumps;
    }

    private void DoJump()
    {
        _jumpsRemaining--;

        // Make jump responsive even while falling.
        var v = _rb.linearVelocity;
        if (v.y < 0f) v.y = 0f;
        _rb.linearVelocity = v;

        _rb.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
    }

    private bool IsGrounded()
    {
        Vector2 origin;
        float rayDistance;

        if (_col != null)
        {
            Bounds b = _col.bounds;
            origin = new Vector2(b.center.x, b.min.y + 0.01f);
            rayDistance = groundRayExtraDistance + 0.02f;
        }
        else
        {
            origin = transform.position;
            rayDistance = 0.25f + groundRayExtraDistance;
        }

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, rayDistance, groundMask);
        Debug.DrawRay(origin, Vector2.down * rayDistance, hit.collider ? Color.green : Color.red);
        return hit.collider != null;
    }

    public void SetRunStarted(bool started)
    {
        _runStarted = started;
    }

    public void FreezeOnDeath()
    {
        _frozen = true;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.simulated = false; // fully stop physics so player stays put on death
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameManager.GameState.Playing)
            return;

        // Detect obstacle by tag (set in code when spawned).
        if (collision.collider != null && collision.collider.CompareTag("Obstacle"))
        {
            Debug.Log("HIT OBSTACLE");
            gm.RegisterObstacleHit(collision.collider.gameObject);
        }
    }

    private static bool IsTapPressedThisFrame()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        return false;
    }

    private static bool IsSpacePressedThisFrame()
    {
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }
}

