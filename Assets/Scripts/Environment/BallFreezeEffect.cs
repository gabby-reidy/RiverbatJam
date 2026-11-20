using UnityEngine;

public class BallFreezeEffect : MonoBehaviour
{
    [SerializeField] private GameBall ball;
    [SerializeField] private SpriteRenderer effectRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 18f;
    private bool wasFrozen;
    private bool playing;
    private float framePosition;

    private void Awake()
    {
        if (ball == null || effectRenderer == null || frames == null || frames.Length < 3)
        {
            Debug.LogError("Assign the ball, renderer, and at least three freeze frames.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        bool frozen = ball.IsFrozen;
        int holdFrame = frames.Length - 3;
        if (frozen && !wasFrozen)
        {
            framePosition = 0f;
            playing = true;
        }
        else if (playing)
        {
            if (!frozen && !ball.IsInPlay)
            {
                // Catching, resetting, or ending the fight cancels the effect.
                playing = false;
            }
            else if (wasFrozen && !frozen)
                framePosition = holdFrame + 1;
            else
                framePosition += Time.deltaTime * Mathf.Max(1f, framesPerSecond);
        }
        if (frozen) framePosition = Mathf.Min(framePosition, holdFrame);
        if (framePosition >= frames.Length) playing = false;
        effectRenderer.enabled = playing;
        if (playing) effectRenderer.sprite = frames[Mathf.FloorToInt(framePosition)];
        wasFrozen = frozen;
    }

    private void OnDisable()
    {
        wasFrozen = false;
        playing = false;
        framePosition = 0f;
        if (effectRenderer != null) effectRenderer.enabled = false;
    }
}
