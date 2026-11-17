using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPaddle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WizardOpponentData opponentData;
    [SerializeField] private GameBall gameBall;

    [Header("Paddle Center Limits")]
    [SerializeField] private float minY = -3.5f;
    [SerializeField] private float maxY = 3.5f;

    [Header("Tracking")]
    [SerializeField, Min(0f)] private float trackingDeadZone = 0.1f;

    [Header("Runtime Health")]
    [SerializeField] private int currentHealth;

    public int MaxHealth => opponentData != null ? opponentData.MaxHealth : 0;
    public int CurrentHealth => currentHealth;
    public bool IsDefeated => currentHealth <= 0;

    private Rigidbody2D rb;
    private float fixedX;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        fixedX = rb.position.x;
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
        }
    }

    private void FixedUpdate()
    {
        if (opponentData == null || gameBall == null)
        {
            return;
        }

        float targetY = Mathf.Clamp(
            gameBall.transform.position.y,
            minY,
            maxY
        );

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

        rb.MovePosition(new Vector2(fixedX, nextY));
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

    private void OnValidate()
    {
        maxY = Mathf.Max(minY, maxY);
    }
}
