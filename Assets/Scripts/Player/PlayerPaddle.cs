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

    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int CurrentHealth => currentHealth;
    public bool IsDefeated => currentHealth <= 0;
    public float VerticalMoveSpeed { get; private set; }
    public float HorizontalMoveSpeed { get; private set; }

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = MaxHealth;

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
            moveInput * moveSpeed * Time.fixedDeltaTime;

        nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);

        VerticalMoveSpeed = (nextPosition.y - rb.position.y) / Time.fixedDeltaTime;
        HorizontalMoveSpeed = (nextPosition.x - rb.position.x) / Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDefeated)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Debug.Log($"{name} took {damage} damage. Health: {currentHealth}/{MaxHealth}.", this);
    }

    private void OnDisable()
    {
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
