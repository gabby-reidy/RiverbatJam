using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPaddle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WizardOpponentData opponentData;
    [SerializeField] private GameBall gameBall;

    [Header("Paddle Center Limits")]
    [SerializeField] private float minX = 0.5f;
    [SerializeField] private float maxX = 8f;
    [SerializeField] private float minY = -3.5f;
    [SerializeField] private float maxY = 3.5f;

    [Header("Tracking")]
    [SerializeField, Min(0f)] private float trackingDeadZone = 0.1f;

    [Header("Prediction")]
    [Tooltip("Ball-center limits at wall contact, including the ball radius. Update if arena dimensions change.")]
    [SerializeField] private float bottomBounceY = -4.1566f;
    [SerializeField] private float topBounceY = 4.1483f;
    [SerializeField] private float recoveryY = 0f;

    [Header("Horizontal Tactics")]
    [SerializeField, Min(0.05f)] private float approachWindow = 0.6f;
    [SerializeField, Min(0f)] private float verticalAlignmentTolerance = 1f;

    [Header("Runtime Health")]
    [SerializeField] private int currentHealth;

    public int MaxHealth => opponentData != null ? opponentData.MaxHealth : 0;
    public int CurrentHealth => currentHealth;
    public bool IsDefeated => currentHealth <= 0;
    public float VerticalMoveSpeed { get; private set; }
    public float HorizontalMoveSpeed { get; private set; }

    private Rigidbody2D rb;
    private float targetX;
    private bool trackingIncomingShot;
    private bool attackThisShot;
    private Rigidbody2D ballBody;
    private Collider2D frontCollider;
    private float ballRadius;
    private float targetY;
    private float reactionTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        targetX = rb.position.x;
        targetY = rb.position.y;
        currentHealth = MaxHealth;

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Start()
    {
        if (opponentData == null || gameBall == null)
        {
            Debug.LogError("Assign Opponent Data and Game Ball on EnemyPaddle.", this);

            enabled = false;
            return;
        }

        ballBody = gameBall.GetComponent<Rigidbody2D>();
        frontCollider = GetComponentInChildren<PolygonCollider2D>();
        CircleCollider2D ballCollider = gameBall.GetComponent<CircleCollider2D>();
        ballRadius = ballCollider != null ? ballCollider.bounds.extents.x : 0.15f;
    }

    private void FixedUpdate()
    {
        VerticalMoveSpeed = 0f;
        HorizontalMoveSpeed = 0f;
        if (opponentData == null || gameBall == null || ballBody == null || gameBall.IsBattleOver)
        {
            return;
        }

        reactionTimer -= Time.fixedDeltaTime;
        if (reactionTimer <= 0f)
        {
            targetY = ChooseTargetY();
            targetX = ChooseTargetX();
            reactionTimer = opponentData.ReactionInterval;
        }

        float nextY = rb.position.y;

        if (Mathf.Abs(targetY - nextY) > trackingDeadZone)
        {
            nextY = Mathf.MoveTowards(
                nextY,
                targetY,
                opponentData.PaddleMoveSpeed * Time.fixedDeltaTime
            );
        }

        nextY = Mathf.Clamp(nextY, minY, maxY);

        float nextX = Mathf.MoveTowards(rb.position.x, targetX, opponentData.PaddleMoveSpeed * Time.fixedDeltaTime);
        Vector2 nextPosition = new Vector2(Mathf.Clamp(nextX, minX, maxX), nextY);
        // Share one movement budget between both axes, like the player's diagonal clamp.
        nextPosition = rb.position + Vector2.ClampMagnitude(
            nextPosition - rb.position, opponentData.PaddleMoveSpeed * Time.fixedDeltaTime);
        nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);
        VerticalMoveSpeed = (nextPosition.y - rb.position.y) / Time.fixedDeltaTime;
        HorizontalMoveSpeed = (nextPosition.x - rb.position.x) / Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    private float ChooseTargetX()
    {
        float interceptX = frontCollider != null ? frontCollider.bounds.min.x - ballRadius : rb.position.x;
        bool incoming = !gameBall.IsCaught && ballBody.linearVelocity.x > 0.01f
            && ballBody.position.x < interceptX;

        if (!incoming)
        {
            trackingIncomingShot = false;
            return Mathf.Clamp(opponentData.PreferredX, minX, maxX);
        }

        if (!trackingIncomingShot)
        {
            attackThisShot = Random.value < opponentData.Aggression;
            trackingIncomingShot = true;
        }

        float timeToContact = (interceptX - ballBody.position.x) / ballBody.linearVelocity.x;
        if (Mathf.Abs(targetY - rb.position.y) > verticalAlignmentTolerance || timeToContact > approachWindow)
        {
            return rb.position.x;
        }

        bool dangerous = gameBall.CurrentSpeed >= opponentData.RetreatSpeedThreshold
            || gameBall.CurrentDamage >= opponentData.RetreatDamageThreshold;
        if (dangerous)
        {
            return maxX;
        }

        return attackThisShot ? minX : rb.position.x;
    }

    private float ChooseTargetY()
    {
        Vector2 velocity = ballBody.linearVelocity;
        float interceptX = frontCollider != null ? frontCollider.bounds.min.x - ballRadius : rb.position.x;

        // Recover when the ball is held, travelling away, or already behind us.
        if (gameBall.IsCaught || velocity.x <= 0.01f || ballBody.position.x >= interceptX)
        {
            return Mathf.Clamp(recoveryY, minY, maxY);
        }

        float travelTime = (interceptX - ballBody.position.x) / velocity.x;
        float projectedY = ballBody.position.y + velocity.y * travelTime;
        float height = Mathf.Max(0.01f, topBounceY - bottomBounceY);
        // Fold the projected path into the arena, including multiple wall bounces.
        float interceptY = bottomBounceY + Mathf.PingPong(projectedY - bottomBounceY, height);
        float error = Random.Range(-opponentData.PredictionError, opponentData.PredictionError);
        return Mathf.Clamp(interceptY + error, minY, maxY);
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
        VerticalMoveSpeed = 0f;
        HorizontalMoveSpeed = 0f;
    }

    private void OnValidate()
    {
        minX = Mathf.Max(0.5f, minX);
        maxX = Mathf.Max(minX, maxX);
        maxY = Mathf.Max(minY, maxY);
        topBounceY = Mathf.Max(bottomBounceY + 0.01f, topBounceY);
    }
}
