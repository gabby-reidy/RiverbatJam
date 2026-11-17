using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GameBall : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
    [SerializeField, Min(0f)] private float serveDelay = 1f;

    [Header("Paddles")]
    [SerializeField] private Collider2D playerPaddle;
    [SerializeField] private Collider2D enemyPaddle;
    [SerializeField, Range(10f, 70f)] private float maxBounceAngle = 60f;

    [Header("Goal Boundaries")]
    [SerializeField] private float leftGoalX = -9f;
    [SerializeField] private float rightGoalX = 9f;

    [Header("Damage")]
    [SerializeField, Min(1)] private int baseDamage = 10;
    [SerializeField, Min(0)] private int damagePerReflection = 5;

    [SerializeField] private int currentDamage;
    [SerializeField] private int reflectionCount;

    public int CurrentDamage => currentDamage;
    public int ReflectionCount => reflectionCount;

    public bool IsBattleOver { get; private set; }

    private PlayerPaddle playerHealth;
    private EnemyPaddle enemyHealth;
    private Rigidbody2D rb;
    private Vector2 spawnPosition;
    private Vector2 moveDirection;
    private float serveTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spawnPosition = rb.position;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Start()
    {
        playerHealth = playerPaddle != null ? playerPaddle.GetComponentInParent<PlayerPaddle>() : null;
        enemyHealth = enemyPaddle != null ? enemyPaddle.GetComponentInParent<EnemyPaddle>() : null;

        if (playerHealth == null || enemyHealth == null || enemyHealth.MaxHealth <= 0)
        {
            Debug.LogError("Assign both paddle colliders, their paddle scripts, and the enemy Opponent Data before starting the battle.", this);
            StopBattle();
            return;
        }

        ResetBall();
    }

    private void FixedUpdate()
    {
        if (IsBattleOver)
        {
            return;
        }

        if (rb.position.x < leftGoalX ||
            rb.position.x > rightGoalX)
        {
            // Apply this rally's damage before ResetBall clears it.
            if (rb.position.x < leftGoalX)
            {
                playerHealth.TakeDamage(currentDamage);
            }
            else
            {
                enemyHealth.TakeDamage(currentDamage);
            }

            if (playerHealth.IsDefeated || enemyHealth.IsDefeated)
            {
                Debug.Log(playerHealth.IsDefeated ? "Player defeated. Enemy wins!" : "Enemy defeated. Player wins!", this);
                StopBattle();
            }
            else
            {
                ResetBall();
            }
            return;
        }

        if (serveTimer > 0f)
        {
            serveTimer -= Time.fixedDeltaTime;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = moveDirection * moveSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsBattleOver || serveTimer > 0f)
        {
            return;
        }

        Collider2D hitCollider = collision.collider;

        if (hitCollider == playerPaddle)
        {
            BounceOffPaddle(playerPaddle, 1f);
        }
        else if (hitCollider == enemyPaddle)
        {
            BounceOffPaddle(enemyPaddle, -1f);
        }
        else
        {
            Vector2 normal = collision.GetContact(0).normal;

            // Only reflect if we are moving into the surface.
            if (Vector2.Dot(moveDirection, normal) < 0f)
            {
                moveDirection =
                    Vector2.Reflect(moveDirection, normal).normalized;
            }
        }

        rb.linearVelocity = moveDirection * moveSpeed;
    }

    private void BounceOffPaddle(Collider2D paddle, float horizontalDirection)
    {
        // Ignore another contact while already travelling away from this paddle.
        if (moveDirection.x * horizontalDirection >= 0f)
        {
            return;
        }

        float halfHeight = Mathf.Max(paddle.bounds.extents.y, 0.01f);

        float hitOffset =
            (rb.position.y - paddle.bounds.center.y) / halfHeight;

        hitOffset = Mathf.Clamp(hitOffset, -1f, 1f);

        float angle = hitOffset * maxBounceAngle * Mathf.Deg2Rad;

        moveDirection = new Vector2(
            Mathf.Cos(angle) * horizontalDirection,
            Mathf.Sin(angle)
        );

        IncreaseDamage();
    }

    private void IncreaseDamage()
    {
        reflectionCount++;
        currentDamage += damagePerReflection;

        Debug.Log($"Reflection {reflectionCount}: ball damage is now {currentDamage}.", this);
    }

    private void StopBattle()
    {
        IsBattleOver = true;
        moveDirection = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;
    }

    public void ResetBall()
    {
        if (IsBattleOver)
        {
            return;
        }

        currentDamage = baseDamage;
        reflectionCount = 0;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = spawnPosition;

        float horizontalDirection = Random.value < 0.5f ? -1f : 1f;
        float verticalDirection = Random.Range(-0.5f, 0.5f);

        moveDirection =
            new Vector2(horizontalDirection, verticalDirection).normalized;

        serveTimer = serveDelay;
    }
}
