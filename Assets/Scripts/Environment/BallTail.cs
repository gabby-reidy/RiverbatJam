using UnityEngine;

public class BallTail : MonoBehaviour
{
    [SerializeField] private GameBall ball;
    [SerializeField] private SpriteRenderer tailRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 18f;
    [SerializeField] private float artworkAngleOffset = 180f;
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
    }

    private void LateUpdate()
    {
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
        animationTime = Mathf.Repeat(animationTime + Time.deltaTime, frames.Length / framesPerSecond);
        int index = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(animationTime * framesPerSecond));
        tailRenderer.sprite = frames[index];
    }

    private void OnDisable()
    {
        animationTime = 0f;
        if (tailRenderer != null) tailRenderer.enabled = false;
    }
}
