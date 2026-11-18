using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[DefaultExecutionOrder(100)]
public class GameBall : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
    [SerializeField, Min(0f)] private float serveDelay = 1f;

    [Header("Paddle Momentum")]
    [SerializeField, Min(0f)] private float horizontalSpeedInfluence = 1f;
    [SerializeField, Range(0f, 1f)] private float stationarySpeedRecovery = 0.5f;
    [SerializeField, Min(0.1f)] private float minBallSpeed = 2f;
    [SerializeField, Min(0.1f)] private float maxBallSpeed = 24f;
    [SerializeField] private float currentSpeed;

    public float CurrentSpeed => currentSpeed;

    [Header("Paddles")]
    [SerializeField] private Collider2D playerPaddle;
    [SerializeField] private Collider2D enemyPaddle;
    [SerializeField, Range(10f, 70f)] private float maxBounceAngle = 60f;

    [Header("Catching")]
    [SerializeField] private BoxCollider2D playerBackCollider;
    [SerializeField] private BoxCollider2D enemyBackCollider;
    [SerializeField, Min(0.01f)] private float heldBallGap = 0.08f;
    [SerializeField, Min(0f)] private float enemyLaunchDelay = 0.75f;
    [SerializeField, Range(0f, 70f)] private float maxLaunchAngle = 45f;
    [SerializeField, Min(0.1f)] private float paddleSpeedForMaxLaunchAngle = 6f;

    public bool IsCaught => heldPaddle != null;

    private Collider2D heldPaddle;
    private float heldDirection;
    private float catchTimer;
    private float ballRadius;
    private int caughtFrame;
    private bool launchRequested;

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
        CircleCollider2D ballCollider = GetComponent<CircleCollider2D>();
        ballRadius = ballCollider != null
            ? ballCollider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y))
            : 0.15f;

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

        FindBackCollider(ref playerBackCollider, playerHealth);
        FindBackCollider(ref enemyBackCollider, enemyHealth);
        ResetBall();
    }

    private void FindBackCollider(ref BoxCollider2D backCollider, Component paddle)
    {
        if (backCollider != null)
        {
            return;
        }

        BoxCollider2D[] colliders = paddle.GetComponentsInChildren<BoxCollider2D>();
        if (colliders.Length == 1)
        {
            backCollider = colliders[0];
        }
        else
        {
            Debug.LogWarning($"Assign the back BoxCollider2D for {paddle.name} on GameBall to enable catching.", this);
        }
    }

    private void Update()
    {
        if (!IsBattleOver && heldPaddle == playerPaddle && IsCaught
            && Time.frameCount > caughtFrame && Keyboard.current != null
            && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            launchRequested = true;
        }
    }

    private void LateUpdate()
    {
        if (IsCaught && !IsBattleOver)
        {
            PositionHeldBall();
        }
    }

    private void FixedUpdate()
    {
        if (IsBattleOver)
        {
            return;
        }

        if (IsCaught)
        {
            PositionHeldBall();
            if (heldPaddle == enemyPaddle)
            {
                catchTimer -= Time.fixedDeltaTime;
                launchRequested = catchTimer <= 0f;
            }

            if (launchRequested)
            {
                LaunchCaughtBall();
            }
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

        rb.linearVelocity = moveDirection * currentSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsBattleOver || IsCaught || serveTimer > 0f)
        {
            return;
        }

        Collider2D hitCollider = collision.collider;

        if (hitCollider == playerBackCollider)
        {
            CatchBall(playerPaddle, 1f);
            return;
        }
        if (hitCollider == enemyBackCollider)
        {
            CatchBall(enemyPaddle, -1f);
            return;
        }

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

                // Barriers absorb damage, but only paddles increase it.
                BarrierHealth barrier = hitCollider.GetComponentInParent<BarrierHealth>();
                if (barrier != null)
                {
                    barrier.TakeDamage(currentDamage);
                }
            }
        }

        rb.linearVelocity = moveDirection * currentSpeed;
    }

    private void CatchBall(Collider2D paddle, float horizontalDirection)
    {
        heldPaddle = paddle;
        heldDirection = horizontalDirection;
        currentDamage = baseDamage;
        reflectionCount = 0;
        ResetSpeed();
        serveTimer = 0f;
        catchTimer = enemyLaunchDelay;
        caughtFrame = Time.frameCount;
        launchRequested = false;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;
        PositionHeldBall();
        Debug.Log($"Ball caught. Damage reset to {currentDamage}.", this);
    }

    private void PositionHeldBall()
    {
        Bounds bounds = heldPaddle.bounds;
        float frontX = heldDirection > 0f ? bounds.max.x : bounds.min.x;
        Vector2 position = new Vector2(frontX + heldDirection * (ballRadius + heldBallGap), bounds.center.y);
        rb.position = position;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private void LaunchCaughtBall()
    {
        PositionHeldBall();
        // Paddle FixedUpdates run first, so this includes this step's input and limits.
        float verticalSpeed = heldPaddle == playerPaddle
            ? playerHealth.VerticalMoveSpeed
            : enemyHealth.VerticalMoveSpeed;
        float movementInfluence = Mathf.Clamp(
            verticalSpeed / Mathf.Max(0.1f, paddleSpeedForMaxLaunchAngle),
            -1f,
            1f
        );
        float angle = movementInfluence * Mathf.Clamp(maxLaunchAngle, 0f, 70f) * Mathf.Deg2Rad;
        moveDirection = new Vector2(Mathf.Cos(angle) * heldDirection, Mathf.Sin(angle));
        heldPaddle = null;
        launchRequested = false;
        rb.simulated = true;
        rb.linearVelocity = moveDirection * currentSpeed;
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

        float paddleHorizontalSpeed = paddle == playerPaddle
            ? playerHealth.HorizontalMoveSpeed
            : enemyHealth.HorizontalMoveSpeed;
        // Positive toward the opponent on either side; negative when retreating.
        float speedChange = paddleHorizontalSpeed * horizontalDirection * Mathf.Max(0f, horizontalSpeedInfluence);
        if (Mathf.Abs(paddleHorizontalSpeed) <= 0.01f)
        {
            currentSpeed = ClampBallSpeed(Mathf.Lerp(
                currentSpeed,
                ClampBallSpeed(moveSpeed),
                stationarySpeedRecovery
            ));
        }
        else
        {
            currentSpeed = ClampBallSpeed(currentSpeed + speedChange);
        }

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

    private float ClampBallSpeed(float speed)
    {
        float minimum = Mathf.Max(0.1f, minBallSpeed);
        float maximum = Mathf.Max(minimum, maxBallSpeed);
        return Mathf.Clamp(speed, minimum, maximum);
    }

    private void ResetSpeed()
    {
        currentSpeed = ClampBallSpeed(moveSpeed);
    }

    private void StopBattle()
    {
        IsBattleOver = true;
        heldPaddle = null;
        launchRequested = false;
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

        heldPaddle = null;
        launchRequested = false;
        rb.simulated = true;
        currentDamage = baseDamage;
        reflectionCount = 0;
        ResetSpeed();
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
