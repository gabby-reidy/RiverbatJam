using UnityEngine;

public class ScoreCameraShake : MonoBehaviour
{
    [SerializeField] private GameBall ball;
    [SerializeField, Min(0f)] private float strength = 0.08f;
    [SerializeField, Min(0.01f)] private float duration = 0.18f;
    private Vector3 restingPosition;
    private float remaining;

    private void OnEnable()
    {
        restingPosition = transform.localPosition;
        if (ball != null) ball.GoalDamaged += Shake;
    }

    private void Shake()
    {
        remaining = Mathf.Max(0.01f, duration);
    }

    private void LateUpdate()
    {
        if (remaining <= 0f) return;
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        float fade = remaining / Mathf.Max(0.01f, duration);
        Vector2 offset = Random.insideUnitCircle * strength * fade;
        transform.localPosition = restingPosition + new Vector3(offset.x, offset.y, 0f);
    }

    private void OnDisable()
    {
        if (ball != null) ball.GoalDamaged -= Shake;
        remaining = 0f;
        transform.localPosition = restingPosition;
    }
}
