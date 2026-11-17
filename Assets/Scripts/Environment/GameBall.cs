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
        ResetBall();
    }

    private void FixedUpdate()
    {
        if (rb.position.x < leftGoalX ||
            rb.position.x > rightGoalX)
        {
            ResetBall();
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
        if (serveTimer > 0f)
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
        float halfHeight = Mathf.Max(paddle.bounds.extents.y, 0.01f);

        float hitOffset =
            (rb.position.y - paddle.bounds.center.y) / halfHeight;

        hitOffset = Mathf.Clamp(hitOffset, -1f, 1f);

        float angle = hitOffset * maxBounceAngle * Mathf.Deg2Rad;

        moveDirection = new Vector2(
            Mathf.Cos(angle) * horizontalDirection,
            Mathf.Sin(angle)
        );
    }

    public void ResetBall()
    {
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