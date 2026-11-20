using UnityEngine;

public class BallWindEffect : MonoBehaviour
{
    [SerializeField] private GameBall ball;
    [SerializeField] private SpriteRenderer effectRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 36f;
    private float animationTime;
    private bool playing;

    private void OnEnable()
    {
        if (ball == null || effectRenderer == null || frames == null || frames.Length == 0)
        {
            Debug.LogError("Assign the ball, renderer, and wind frames.", this);
            enabled = false;
            return;
        }
        ball.WindActivated += Play;
        effectRenderer.enabled = false;
    }

    private void Play()
    {
        animationTime = 0f;
        playing = true;
        effectRenderer.sprite = frames[0];
        effectRenderer.enabled = true;
    }

    private void LateUpdate()
    {
        if (!playing) return;
        animationTime += Time.deltaTime;
        int index = Mathf.FloorToInt(animationTime * Mathf.Max(1f, framesPerSecond));
        if (index >= frames.Length || ball.IsCaught || ball.IsBattleOver)
        {
            playing = false;
            effectRenderer.enabled = false;
            return;
        }
        effectRenderer.sprite = frames[index];
    }

    private void OnDisable()
    {
        if (ball != null) ball.WindActivated -= Play;
        playing = false;
        if (effectRenderer != null) effectRenderer.enabled = false;
    }
}
