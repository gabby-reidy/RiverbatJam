using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerPaddle : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 6f;

    [Header("Paddle Center Limits")]
    [SerializeField] private float minX = -7f;
    [SerializeField] private float maxX = -0.5f;
    [SerializeField] private float minY = -3.5f;
    [SerializeField] private float maxY = 3.5f;

    [Header("Health")]
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private int currentHealth;

    [Header("Lives")]
    [SerializeField, Min(1)] private int startingLives = 3;
    [SerializeField] private int currentLives;

    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int CurrentHealth => currentHealth;
    public int CurrentLives => currentLives;
    public bool IsDefeated => currentLives <= 0;
    public float VerticalMoveSpeed { get; private set; }
    public float HorizontalMoveSpeed { get; private set; }

    [Header("Fire Dash")]
    [SerializeField, Min(0f)] private float dashSpeedBoost = 12f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.15f;
    private Vector2 dashDirection;
    private float dashTimer;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = MaxHealth;
        currentLives = Mathf.Max(1, startingLives);

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Update()
    {
        moveInput = Vector2.zero;

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.wKey.isPressed ||
            Keyboard.current.upArrowKey.isPressed)
        {
            moveInput.y += 1f;
        }

        if (Keyboard.current.sKey.isPressed ||
            Keyboard.current.downArrowKey.isPressed)
        {
            moveInput.y -= 1f;
        }

        if (Keyboard.current.dKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed)
        {
            moveInput.x += 1f;
        }

        if (Keyboard.current.aKey.isPressed ||
            Keyboard.current.leftArrowKey.isPressed)
        {
            moveInput.x -= 1f;
        }

        // Prevent diagonal movement from being faster.
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    private void FixedUpdate()
    {
        Vector2 nextPosition = rb.position +
            moveInput * moveSpeed * Time.fixedDeltaTime + GetDashStep();

        nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);

        VerticalMoveSpeed = (nextPosition.y - rb.position.y) / Time.fixedDeltaTime;
        HorizontalMoveSpeed = (nextPosition.x - rb.position.x) / Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    public bool TryDash()
    {
        if (!isActiveAndEnabled || IsDefeated || dashTimer > 0f || dashSpeedBoost <= 0f) return false;
        Vector2 direction = moveInput;
        if (direction.sqrMagnitude < 0.001f) return false;
        direction.Normalize();
        Vector2 destination = rb.position + direction * dashSpeedBoost * Time.fixedDeltaTime;
        destination.x = Mathf.Clamp(destination.x, minX, maxX);
        destination.y = Mathf.Clamp(destination.y, minY, maxY);
        if ((destination - rb.position).sqrMagnitude < 0.000001f) return false;

        dashDirection = direction;
        dashTimer = Mathf.Max(0.01f, dashDuration);
        return true;
    }

    public void CancelDash()
    {
        dashTimer = 0f;
        dashDirection = Vector2.zero;
    }

    private Vector2 GetDashStep()
    {
        float step = Mathf.Min(dashTimer, Time.fixedDeltaTime);
        dashTimer = Mathf.Max(0f, dashTimer - Time.fixedDeltaTime);
        return dashDirection * dashSpeedBoost * step;
    }

    public void RestoreHealth()
    {
        if (!IsDefeated)
        {
            currentHealth = MaxHealth;
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDefeated)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (currentHealth == 0)
        {
            // Excess damage cannot take more than one life in a single hit.
            currentLives--;
            if (!IsDefeated)
            {
                currentHealth = MaxHealth;
            }
        }

        Debug.Log($"{name} took {damage} damage. Health: {currentHealth}/{MaxHealth}. Lives: {currentLives}.", this);
    }

    private void OnDisable()
    {
        CancelDash();
        moveInput = Vector2.zero;
        VerticalMoveSpeed = 0f;
        HorizontalMoveSpeed = 0f;
    }

    private void OnValidate()
    {
        maxX = Mathf.Max(minX, maxX);
        maxY = Mathf.Max(minY, maxY);
    }
}
