using UnityEngine;

public class BallTail : MonoBehaviour
{
    [SerializeField] private GameBall ball;
    [SerializeField] private SpriteRenderer tailRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 18f;
    [SerializeField] private float artworkAngleOffset = 180f;
    [SerializeField] private Sprite[] fireFrames;
    [SerializeField] private SpriteRenderer ballRenderer;
    [SerializeField] private Color fireColor = new Color(1f, 32f / 255f, 0f, 1f);
    [SerializeField] private Color frozenColor = new Color(105f / 255f, 207f / 255f, 1f, 1f);
    private Color normalColor;
    private bool showingFire;
    private float animationTime;
    private Rigidbody2D ballBody;

    private void Awake()
    {
        if (ball == null || tailRenderer == null || frames == null || frames.Length == 0)
        {
            Debug.LogError("Assign the ball, tail renderer, and animation frames on BallTail.", this);
            enabled = false;
            return;
        }
        ballBody = ball.GetComponent<Rigidbody2D>();
        if (ballRenderer == null) ballRenderer = ball.GetComponent<SpriteRenderer>();
        if (ballRenderer != null) normalColor = ballRenderer.color;
    }

    private void LateUpdate()
    {
        bool fire = ball.IsFireCharged;
        if (fire != showingFire) animationTime = 0f;
        showingFire = fire;
        if (ballRenderer != null) ballRenderer.color = ball.IsFrozen ? frozenColor : (fire ? fireColor : normalColor);
        Sprite[] activeFrames = fire && fireFrames != null && fireFrames.Length > 0 ? fireFrames : frames;
        bool moving = ball.IsInPlay && ballBody != null && ballBody.linearVelocity.sqrMagnitude > 0.001f;
        tailRenderer.enabled = moving;
        if (!moving)
        {
            animationTime = 0f;
            return;
        }
        Vector2 velocity = ball.TravelVelocity;
        transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + artworkAngleOffset);
        animationTime = Mathf.Repeat(animationTime + Time.deltaTime, activeFrames.Length / framesPerSecond);
        int index = Mathf.Min(activeFrames.Length - 1, Mathf.FloorToInt(animationTime * framesPerSecond));
        tailRenderer.sprite = activeFrames[index];
    }

    private void OnDisable()
    {
        animationTime = 0f;
        showingFire = false;
        if (ballRenderer != null) ballRenderer.color = normalColor;
        if (tailRenderer != null) tailRenderer.enabled = false;
    }
}
