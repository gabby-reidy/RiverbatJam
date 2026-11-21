using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[DefaultExecutionOrder(100)]
public class GameBall : MonoBehaviour
{
    [Header("Sound Effects")]
    [SerializeField] private AudioClip catchSound;
    [SerializeField] private AudioClip reflectSound;
    [SerializeField] private AudioClip wallHitSound;

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
    public bool IsCaughtByPlayer => IsCaught && heldPaddle == playerPaddle;

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
    public bool GameplayLocked { get; set; }
    public int InputBlockedThroughFrame { get; set; } = -1;
    public bool CampaignControlled { get; set; }
    public float OpeningServeMultiplier { get; set; } = 1f;
    public event System.Action<bool, bool, bool> RallyEnded;
    private float nextServeMultiplier = 1f;

    public void ServeTo(bool servePlayer, float multiplier = 1f)
    {
        if (IsBattleOver) return;
        CatchBall(servePlayer ? playerPaddle : enemyPaddle, servePlayer ? 1f : -1f);
        LastHitSide = servePlayer ? 0 : 1;
        nextServeMultiplier = multiplier;
    }

    public float FreezeTimeRemaining => freezeTimer;
    public float MaxBounceAngle => maxBounceAngle;
    public BoxCollider2D EnemyBackCollider => enemyBackCollider;
    public Collider2D EnemyFrontCollider => enemyPaddle;
    public PowerUpManager PowerUps => powerUps;
    public bool IsFireCharged { get; private set; }

    public bool IsFrozen => freezeTimer > 0f;
    public bool IsInPlay => !GameplayLocked && !IsBattleOver && !IsCaught && !IsFrozen && serveTimer <= 0f;
    public Vector2 TravelVelocity => moveDirection * currentSpeed;
    private float freezeTimer;
    private RigidbodyConstraints2D constraintsBeforeFreeze;

    // -1 is a neutral serve, 0 is the player, 1 is the enemy.
    public int LastHitSide { get; private set; } = -1;
    public event System.Action LifeLost;
    public event System.Action GoalDamaged;
    [SerializeField] private PowerUpManager powerUps;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (TryCatchFromTrigger(other)) return;
        if (powerUps != null) powerUps.Collect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCatchFromTrigger(other);
    }

    private bool TryCatchFromTrigger(Collider2D other)
    {
        if (GameplayLocked || IsBattleOver || IsCaught || serveTimer > 0f) return false;
        if (other == playerBackCollider && playerPaddle != null)
        {
            CatchBall(playerPaddle, 1f);
            return true;
        }
        if (other == enemyBackCollider && enemyPaddle != null)
        {
            CatchBall(enemyPaddle, -1f);
            return true;
        }
        return false;
    }

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
        // Catch areas detect contact without trapping the ball against the solid shield.
        if (playerBackCollider != null) playerBackCollider.isTrigger = true;
        if (enemyBackCollider != null) enemyBackCollider.isTrigger = true;
        if (CampaignControlled) ServeTo(false, OpeningServeMultiplier);
        else ResetBall();
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
        if (GameplayLocked || Time.frameCount <= InputBlockedThroughFrame) return;
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
        if (GameplayLocked) return;
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

        if (IsFrozen)
        {
            freezeTimer = Mathf.Max(0f, freezeTimer - Time.fixedDeltaTime);
            rb.linearVelocity = Vector2.zero;
            if (IsFrozen) return;
            rb.constraints = constraintsBeforeFreeze;
        }

        if (rb.position.x < leftGoalX ||
            rb.position.x > rightGoalX)
        {
            int playerLivesBefore = playerHealth.CurrentLives;
            int enemyLivesBefore = enemyHealth.CurrentLives;

            // Apply this rally's damage before ResetBall clears it.
            if (rb.position.x < leftGoalX)
            {
                playerHealth.TakeDamage(currentDamage);
            }
            else
            {
                enemyHealth.TakeDamage(currentDamage);
            }

            GoalDamaged?.Invoke();
            bool playerDamaged = rb.position.x < leftGoalX;
            bool lostLife = playerHealth.CurrentLives < playerLivesBefore || enemyHealth.CurrentLives < enemyLivesBefore;

            if (playerHealth.CurrentLives < playerLivesBefore || enemyHealth.CurrentLives < enemyLivesBefore)
            {
                playerHealth.RestoreHealth();
                enemyHealth.RestoreHealth();
                LifeLost?.Invoke();
            }

            if (playerHealth.IsDefeated || enemyHealth.IsDefeated)
            {
                Debug.Log(playerHealth.IsDefeated ? "Player defeated. Enemy wins!" : "Enemy defeated. Player wins!", this);
                StopBattle();
                if (!CampaignControlled && enemyHealth.IsDefeated && !playerHealth.IsDefeated)
                {
                    if (SceneLoader.Instance != null)
                        SceneLoader.Instance.ReturnAfterVictory(gameObject.scene.name);
                    else if (Application.CanStreamedLevelBeLoaded("Cutscene"))
                        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Cutscene");
                }
            }
            else
            {
                ServeTo(playerDamaged);
            }
            RallyEnded?.Invoke(playerDamaged, lostLife, IsBattleOver);
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
        bool wasFireCharged = IsFireCharged;
        if (hitCollider == playerPaddle || hitCollider == enemyPaddle ||
            hitCollider == playerBackCollider || hitCollider == enemyBackCollider ||
            hitCollider.GetComponentInParent<BarrierHealth>() != null)
            IsFireCharged = false;

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

        if (IsFrozen) return;

        if (hitCollider == playerPaddle)
        {
            BounceOffPaddle(playerPaddle, 1f, collision.GetContact(0).point, !wasFireCharged);
        }
        else if (hitCollider == enemyPaddle)
        {
            BounceOffPaddle(enemyPaddle, -1f, collision.GetContact(0).point, !wasFireCharged);
        }
        else
        {
            if (ReflectOffSurface(collision))
            {
                if (AudioManager.instance != null) AudioManager.instance.PlaySFX(wallHitSound);
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

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsBattleOver || IsCaught || serveTimer > 0f)
        {
            return;
        }

        Collider2D hitCollider = collision.collider;
        if (IsFrozen)
        {
            // Catching must also work when the back contact began before Ice.
            if (hitCollider == playerBackCollider) CatchBall(playerPaddle, 1f);
            else if (hitCollider == enemyBackCollider) CatchBall(enemyPaddle, -1f);
            return;
        }
        if (hitCollider == playerPaddle || hitCollider == enemyPaddle)
        {
            float direction = hitCollider == playerPaddle ? 1f : -1f;
            ClearShieldContact(hitCollider, direction);
            // Recover an existing contact without counting another return.
            moveDirection.x = Mathf.Abs(moveDirection.x) * direction;
            rb.linearVelocity = moveDirection * currentSpeed;
            return;
        }
        if (hitCollider == playerBackCollider || hitCollider == enemyBackCollider) return;

        // A paddle can redirect us into a wall whose contact never ended.
        // Correct direction without replaying damage, momentum, or catching.
        if (ReflectOffSurface(collision))
        {
            rb.linearVelocity = moveDirection * currentSpeed;
        }
    }

    private bool ReflectOffSurface(Collision2D collision)
    {
        bool reflected = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Vector2.Dot(moveDirection, normal) < -0.001f)
            {
                moveDirection = Vector2.Reflect(moveDirection, normal).normalized;
                reflected = true;
            }
        }
        return reflected;
    }

    public bool TryFreeze(float duration)
    {
        if (!IsInPlay || duration <= 0f || rb.position.x < leftGoalX || rb.position.x > rightGoalX)
            return false;

        // Keep the saved travel direction and speed; keep collisions enabled for catches.
        constraintsBeforeFreeze = rb.constraints;
        freezeTimer = duration;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        return true;
    }

    private void EndFreeze()
    {
        if (IsFrozen) rb.constraints = constraintsBeforeFreeze;
        freezeTimer = 0f;
    }

    public event System.Action WindActivated;

    public bool TryWind()
    {
        if (!IsInPlay || Mathf.Abs(moveDirection.y) < 0.001f) return false;

        // Reverse vertical travel immediately without changing the ball's speed.
        moveDirection.y = -moveDirection.y;
        rb.linearVelocity = moveDirection * currentSpeed;
        WindActivated?.Invoke();
        return true;
    }

    private void CatchBall(Collider2D paddle, float horizontalDirection)
    {
        IsFireCharged = false;
        nextServeMultiplier = 1f;
        if (AudioManager.instance != null) AudioManager.instance.PlaySFX(catchSound);
        EndFreeze();
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
        LastHitSide = heldPaddle == playerPaddle ? 0 : 1;
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

        float horizontalSpeed = heldPaddle == playerPaddle
            ? playerHealth.HorizontalMoveSpeed
            : enemyHealth.HorizontalMoveSpeed;
        // Forward is +X for the player and -X for the enemy.
        float speedChange = horizontalSpeed * heldDirection * Mathf.Max(0f, horizontalSpeedInfluence);
        currentSpeed = ClampBallSpeed((ClampBallSpeed(moveSpeed) + speedChange) * nextServeMultiplier);
        nextServeMultiplier = 1f;

        heldPaddle = null;
        launchRequested = false;
        rb.simulated = true;
        rb.linearVelocity = moveDirection * currentSpeed;
    }

    public event System.Action<Vector2, Vector2, Transform> ShieldReflected;

    private void BounceOffPaddle(Collider2D paddle, float horizontalDirection, Vector2 contactPoint, bool canIgnite)
    {
        float paddleHorizontalSpeed = paddle == playerPaddle
            ? playerHealth.HorizontalMoveSpeed
            : enemyHealth.HorizontalMoveSpeed;
        // An outgoing ball can be hit again only when the paddle catches up from behind.
        bool outgoing = moveDirection.x * horizontalDirection >= 0f;
        float closingSpeed = (paddleHorizontalSpeed - moveDirection.x * currentSpeed) * horizontalDirection;
        bool inFront = (rb.position.x - paddle.bounds.center.x) * horizontalDirection > 0f;
        if (outgoing && (closingSpeed <= 0.1f || !inFront))
        {
            return;
        }

        float halfHeight = Mathf.Max(paddle.bounds.extents.y, 0.01f);

        float hitOffset =
            (rb.position.y - paddle.bounds.center.y) / halfHeight;

        hitOffset = Mathf.Clamp(hitOffset, -1f, 1f);

        float angle = hitOffset * maxBounceAngle * Mathf.Deg2Rad;

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

        ClearShieldContact(paddle, horizontalDirection);

        LastHitSide = paddle == playerPaddle ? 0 : 1;
        IsFireCharged = canIgnite && (paddle == playerPaddle
            ? playerHealth.IsFireDashing : enemyHealth.IsFireDashing);
        IncreaseDamage();
        ShieldReflected?.Invoke(contactPoint, moveDirection, paddle.transform);
        if (AudioManager.instance != null) AudioManager.instance.PlaySFX(reflectSound);
    }

    private void ClearShieldContact(Collider2D paddle, float horizontalDirection)
    {
        // A curved shield endpoint can keep blocking the newly assigned velocity.
        // Place the entire ball beyond its front edge while preserving hit height.
        Bounds bounds = paddle.bounds;
        float edge = horizontalDirection > 0f ? bounds.max.x : bounds.min.x;
        float clearX = edge + horizontalDirection * (ballRadius + Mathf.Max(0.01f, heldBallGap));
        Vector2 position = rb.position;
        if ((position.x - clearX) * horizontalDirection < 0f)
        {
            position.x = clearX;
            rb.position = position;
        }
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
        EndFreeze();
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
        IsFireCharged = false;
        if (IsBattleOver)
        {
            return;
        }

        EndFreeze();
        heldPaddle = null;
        launchRequested = false;
        rb.simulated = true;
        currentDamage = baseDamage;
        reflectionCount = 0;
        ResetSpeed();
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = spawnPosition;
        LastHitSide = -1;

        float horizontalDirection = Random.value < 0.5f ? -1f : 1f;
        float verticalDirection = Random.Range(-0.5f, 0.5f);

        moveDirection =
            new Vector2(horizontalDirection, verticalDirection).normalized;

        serveTimer = serveDelay;
    }
}
