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

    [Header("Lives")]
    [SerializeField, Min(1)] private int startingLives = 3;
    [SerializeField] private int currentLives;

    public int MaxHealth => opponentData != null ? opponentData.MaxHealth : 0;
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
    public bool IsFireDashing { get; private set; }

    [Header("Defensive Power-ups")]
    [SerializeField, Min(0.05f)] private float iceThreatWindow = 0.45f;
    [SerializeField, Min(0.05f)] private float dashThreatWindow = 0.65f;

    private Rigidbody2D rb;
    private float targetX;
    private bool trackingIncomingShot;
    private bool attackThisShot;
    private Rigidbody2D ballBody;
    private Collider2D frontCollider;
    private float ballRadius;
    private float targetY;
    private float reactionTimer;
    private bool pursuingCatch;
    private Vector2 catchTarget;
    private float defensiveTargetY;
    private readonly System.Collections.Generic.List<Vector2> aimTargets = new System.Collections.Generic.List<Vector2>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        targetX = rb.position.x;
        targetY = rb.position.y;
        currentHealth = MaxHealth;
        currentLives = Mathf.Max(1, startingLives);

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
        frontCollider = gameBall.EnemyFrontCollider;
        CircleCollider2D ballCollider = gameBall.GetComponent<CircleCollider2D>();
        ballRadius = ballCollider != null
            ? ballCollider.radius * Mathf.Max(Mathf.Abs(gameBall.transform.lossyScale.x), Mathf.Abs(gameBall.transform.lossyScale.y))
            : 0.15f;
    }

    public bool ControlsLocked { get; set; }

    private void FixedUpdate()
    {
        if (ControlsLocked)
        {
            VerticalMoveSpeed = 0f;
            HorizontalMoveSpeed = 0f;
            return;
        }
        VerticalMoveSpeed = 0f;
        HorizontalMoveSpeed = 0f;
        if (opponentData == null || gameBall == null || ballBody == null || gameBall.IsBattleOver)
        {
            return;
        }

        reactionTimer -= Time.fixedDeltaTime;
        if (reactionTimer <= 0f)
        {
            pursuingCatch = TryChooseCatchTarget(out catchTarget);
            targetY = pursuingCatch ? catchTarget.y : ChooseTargetY();
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
        nextPosition += GetDashStep();
        nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);
        VerticalMoveSpeed = (nextPosition.y - rb.position.y) / Time.fixedDeltaTime;
        HorizontalMoveSpeed = (nextPosition.x - rb.position.x) / Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    private float ChooseTargetX()
    {
        if (pursuingCatch) return catchTarget.x;
        // Hold the interception lane while the ball is frozen.
        if (gameBall.IsFrozen) return rb.position.x;
        if (CanFollowUp())
        {
            trackingIncomingShot = false;
            return Mathf.Abs(targetY - rb.position.y) <= verticalAlignmentTolerance ? minX : rb.position.x;
        }

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

        bool offensiveFire = gameBall.PowerUps != null && gameBall.PowerUps.EnemyInventory == PowerUpType.Fire &&
            opponentData.FireUsage != FireUsage.Defensive;
        return attackThisShot || offensiveFire ? minX : rb.position.x;
    }

    private float ChooseTargetY()
    {
        Vector2 velocity = gameBall.IsFrozen ? gameBall.TravelVelocity : ballBody.linearVelocity;
        float interceptX = frontCollider != null ? frontCollider.bounds.min.x - ballRadius : rb.position.x;

        if (CanFollowUp())
        {
            float closingSpeed = opponentData.PaddleMoveSpeed - Mathf.Abs(velocity.x);
            float catchTime = (interceptX - ballBody.position.x) / closingSpeed;
            float chaseHeight = Mathf.Max(0.01f, topBounceY - bottomBounceY);
            float projected = ballBody.position.y + velocity.y * catchTime;
            return Mathf.Clamp(bottomBounceY + Mathf.PingPong(projected - bottomBounceY, chaseHeight), minY, maxY);
        }

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
        defensiveTargetY = Mathf.Clamp(interceptY + error, minY, maxY);
        if (gameBall.IsFrozen) return defensiveTargetY;
        return ChooseReturnAim(interceptX, interceptY, travelTime, error);
    }

    private bool CanFollowUp()
    {
        float interceptX = frontCollider != null ? frontCollider.bounds.min.x - ballRadius : rb.position.x;
        float distance = interceptX - ballBody.position.x;
        float velocityX = ballBody.linearVelocity.x;
        return !gameBall.IsCaught && !gameBall.IsBattleOver
            && gameBall.CurrentSpeed < opponentData.FollowUpSpeedThreshold
            && velocityX < -0.01f
            && opponentData.PaddleMoveSpeed > Mathf.Abs(velocityX) + 0.1f
            && distance > 0f && distance <= opponentData.FollowUpMaxDistance
            && ballBody.position.x > minX + ballRadius;
    }

    private float ChooseReturnAim(float interceptX, float interceptY, float travelTime, float error)
    {
        PowerUpManager manager = gameBall.PowerUps;
        if (manager == null || frontCollider == null) return defensiveTargetY;
        bool protectHeld = manager.EnemyInventory != PowerUpType.None && !opponentData.ReplaceHeldPowerUp;
        if (!opponentData.SeekPowerUps && !opponentData.TargetWeakBarriers &&
            !opponentData.AimAwayFromPlayer && !protectHeld) return defensiveTargetY;

        Vector2 origin = new Vector2(interceptX, interceptY);
        float bestY = defensiveTargetY;
        float bestScore = float.NegativeInfinity;
        // Sample legal contact offsets, plus exact angles toward visible targets.
        for (int i = -12; i <= 12; i++)
            EvaluateAim(i / 12f * 0.9f, origin, travelTime, error, ref bestY, ref bestScore);
        manager.GetEnemyAimTargets(opponentData, aimTargets, bottomBounceY, topBounceY);
        foreach (Vector2 target in aimTargets)
        {
            if (target.x >= origin.x) continue;
            float angle = Mathf.Atan2(target.y - origin.y, origin.x - target.x) * Mathf.Rad2Deg;
            if (Mathf.Abs(angle) > gameBall.MaxBounceAngle * 0.9f) continue;
            EvaluateAim(angle / gameBall.MaxBounceAngle, origin, travelTime, error, ref bestY, ref bestScore);
        }
        return bestY;
    }

    private void EvaluateAim(float offset, Vector2 origin, float travelTime, float error,
        ref float bestY, ref float bestScore)
    {
        float centerOffset = frontCollider.bounds.center.y - rb.position.y;
        float y = origin.y - offset * frontCollider.bounds.extents.y - centerOffset + error;
        if (y < minY || y > maxY || Mathf.Abs(y - rb.position.y) > opponentData.PaddleMoveSpeed * travelTime)
            return;
        float angle = offset * gameBall.MaxBounceAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(-Mathf.Cos(angle), Mathf.Sin(angle));
        float score = gameBall.PowerUps.ScoreEnemyReturn(opponentData, origin, direction,
            bottomBounceY, topBounceY, ballRadius);
        score -= Mathf.Abs(y - rb.position.y) * 0.05f + Mathf.Abs(offset) * 0.01f;
        if (score <= bestScore) return;
        bestScore = score;
        bestY = y;
    }

    private bool TryChooseCatchTarget(out Vector2 destination)
    {
        destination = rb.position;
        BoxCollider2D back = gameBall.EnemyBackCollider;
        if (back == null || gameBall.IsCaught || gameBall.IsBattleOver || frontCollider == null) return false;
        Vector2 ballPosition = ballBody.position;
        float availableTime;
        if (gameBall.IsFrozen)
        {
            if (!opponentData.PressureFrozenBall) return false;
            availableTime = gameBall.FreezeTimeRemaining - opponentData.ReactionInterval;
        }
        else
        {
            if (!opponentData.AttemptCatches || !gameBall.IsInPlay ||
                (gameBall.CurrentDamage < opponentData.RetreatDamageThreshold &&
                 gameBall.CurrentSpeed < opponentData.RetreatSpeedThreshold)) return false;
            // Only pursue a rear contact once the ball has passed our normal return surface.
            if (ballPosition.x <= frontCollider.bounds.max.x || gameBall.TravelVelocity.x <= 0f) return false;
            availableTime = 0.15f;
            ballPosition += gameBall.TravelVelocity * availableTime;
        }
        if (availableTime <= 0f) return false;
        Vector2 backOffset = (Vector2)back.bounds.center - rb.position;
        destination = ballPosition - backOffset;
        if (destination.x < minX || destination.x > maxX || destination.y < minY || destination.y > maxY)
            return false;
        return Vector2.Distance(destination, rb.position) <= opponentData.PaddleMoveSpeed * availableTime;
    }

    public bool ShouldUseFire()
    {
        if (opponentData == null) return false;
        if (opponentData.FireUsage != FireUsage.Offensive && NeedsDefensivePowerUp(false)) return true;
        if (opponentData.FireUsage == FireUsage.Defensive || !gameBall.IsInPlay || pursuingCatch ||
            dashTimer > 0f || frontCollider == null || HorizontalMoveSpeed >= -0.1f) return false;

        Vector2 velocity = gameBall.TravelVelocity;
        float distance = frontCollider.bounds.min.x - ballRadius - ballBody.position.x;
        if (velocity.x <= 0f || distance <= 0f) return false;
        Vector2 motion = new Vector2(HorizontalMoveSpeed, VerticalMoveSpeed);
        float closingSpeed = velocity.x - motion.x - motion.normalized.x * dashSpeedBoost;
        float contactTime = distance / Mathf.Max(0.1f, closingSpeed);
        if (contactTime > dashDuration || contactTime < Time.fixedDeltaTime) return false;
        float projected = ballBody.position.y + velocity.y * contactTime;
        float ballY = bottomBounceY + Mathf.PingPong(projected - bottomBounceY, topBounceY - bottomBounceY);
        float paddleY = frontCollider.bounds.center.y +
            (motion.y + motion.normalized.y * dashSpeedBoost) * contactTime;
        return rb.position.x > minX + 0.1f &&
            Mathf.Abs(ballY - paddleY) < frontCollider.bounds.extents.y * 0.8f;
    }

    public bool NeedsDefensivePowerUp(bool useIce)
    {
        if (!isActiveAndEnabled || opponentData == null || gameBall == null || ballBody == null ||
            !gameBall.IsInPlay || frontCollider == null || dashTimer > 0f)
            return false;

        Vector2 velocity = gameBall.TravelVelocity;
        float interceptX = frontCollider.bounds.min.x - ballRadius;
        if (velocity.x <= 0.01f || ballBody.position.x >= interceptX) return false;

        float contactTime = (interceptX - ballBody.position.x) / velocity.x;
        float window = useIce ? iceThreatWindow : dashThreatWindow;
        if (contactTime < 0.05f || contactTime > window) return false;

        // Use the last reacted-to target rather than gaining perfect aim from an item.
        float offset = defensiveTargetY - rb.position.y;
        float safeReach = Mathf.Max(0.1f, frontCollider.bounds.extents.y * 0.8f);
        float normalReach = opponentData.PaddleMoveSpeed * contactTime;
        if (Mathf.Abs(offset) <= safeReach + normalReach) return false;

        if (useIce) return true;

        Vector2 movement = new Vector2(HorizontalMoveSpeed, VerticalMoveSpeed);
        if (movement.sqrMagnitude < 0.001f || movement.y * offset <= 0f) return false;
        float normalY = Mathf.Clamp(rb.position.y + movement.y * contactTime, minY, maxY);
        float dashY = Mathf.Clamp(normalY + movement.normalized.y * dashSpeedBoost *
            Mathf.Min(dashDuration, contactTime), minY, maxY);
        // Spend Fire only if this direction improves the intercept, without a worse overshoot.
        return Mathf.Abs(defensiveTargetY - dashY) + 0.15f < Mathf.Abs(defensiveTargetY - normalY);
    }

    public bool TryDash()
    {
        if (!isActiveAndEnabled || IsDefeated || dashTimer > 0f || dashSpeedBoost <= 0f) return false;
        Vector2 direction = new Vector2(HorizontalMoveSpeed, VerticalMoveSpeed);
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
        IsFireDashing = false;
        dashDirection = Vector2.zero;
    }

    private Vector2 GetDashStep()
    {
        IsFireDashing = dashTimer > 0f;
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
