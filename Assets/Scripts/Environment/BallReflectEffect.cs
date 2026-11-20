using System.Collections.Generic;
using UnityEngine;

public class BallReflectEffect : MonoBehaviour
{
    [SerializeField] private GameBall ball;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1)] private int framesPerRow = 10;
    [SerializeField, Range(1, 9)] private int colorRow = 2;
    [SerializeField, Min(1f)] private float framesPerSecond = 30f;
    [SerializeField, Min(0.01f)] private float effectSize = 0.9f;
    [SerializeField] private Material effectMaterial;
    [SerializeField] private int sortingOrder = 3;
    private class Burst
    {
        public SpriteRenderer renderer;
        public float time;
        public int firstFrame;
    }
    private readonly List<Burst> bursts = new List<Burst>();

    private void OnEnable()
    {
        if (ball == null || frames == null || frames.Length < framesPerRow * 9)
        {
            Debug.LogError("Assign the ball and all nine rows of reflection frames.", this);
            enabled = false;
            return;
        }
        ball.ShieldReflected += Play;
    }

    private void Play(Vector2 point, Vector2 direction, Transform paddle)
    {
        GameObject effect = new GameObject("Shield Reflection Burst");
        effect.transform.position = new Vector3(point.x, point.y, ball.transform.position.z);
        effect.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f);
        effect.transform.localScale = Vector3.one * effectSize;
        // Preserve world size and outgoing direction while following the struck paddle.
        effect.transform.SetParent(paddle, true);
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        if (effectMaterial != null) renderer.sharedMaterial = effectMaterial;
        renderer.sortingOrder = sortingOrder;
        int first = (Mathf.Clamp(colorRow, 1, 9) - 1) * framesPerRow;
        renderer.sprite = frames[first];
        bursts.Add(new Burst { renderer = renderer, firstFrame = first });
    }

    private void LateUpdate()
    {
        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            Burst burst = bursts[i];
            burst.time += Time.deltaTime;
            int frame = Mathf.FloorToInt(burst.time * Mathf.Max(1f, framesPerSecond));
            if (burst.renderer == null || frame >= framesPerRow)
            {
                if (burst.renderer != null) Destroy(burst.renderer.gameObject);
                bursts.RemoveAt(i);
            }
            else burst.renderer.sprite = frames[burst.firstFrame + frame];
        }
    }

    private void OnDisable()
    {
        if (ball != null) ball.ShieldReflected -= Play;
        foreach (Burst burst in bursts)
            if (burst.renderer != null) Destroy(burst.renderer.gameObject);
        bursts.Clear();
    }
}
