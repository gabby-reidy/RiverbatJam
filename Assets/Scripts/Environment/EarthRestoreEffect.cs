using System.Collections.Generic;
using UnityEngine;

public class EarthRestoreEffect : MonoBehaviour
{
    [SerializeField] private Sprite[] rebuildFrames;
    [SerializeField, Min(1f)] private float framesPerSecond = 18f;
    [SerializeField, Min(0.01f)] private float burstSize = 1.4f;
    [SerializeField] private SpriteRenderer healPrefab;
    private class Repair
    {
        public BarrierHealth barrier;
        public SpriteRenderer source;
        public SpriteRenderer effect;
        public SpriteRenderer growingBrick;
        public Color healColor;
        public bool sourceEnabled;
        public float elapsed;
    }
    private readonly List<Repair> repairs = new List<Repair>();

    public void Play(BarrierHealth barrier, bool wasBroken)
    {
        if (rebuildFrames == null || rebuildFrames.Length == 0 || barrier == null) return;
        SpriteRenderer source = barrier.GetComponent<SpriteRenderer>();
        if (source == null || (!wasBroken && healPrefab == null)) return;
        for (int i = repairs.Count - 1; i >= 0; i--)
            if (repairs[i].barrier == barrier) Finish(i);
        Repair repair = new Repair { barrier = barrier, source = source, sourceEnabled = source.enabled };
        if (wasBroken)
        {
            GameObject visual = new GameObject("Growing Brick");
            visual.transform.SetParent(barrier.transform, false);
            visual.transform.localScale = Vector3.one * 0.03f;
            repair.growingBrick = visual.AddComponent<SpriteRenderer>();
            repair.growingBrick.sharedMaterial = source.sharedMaterial;
            repair.growingBrick.sprite = source.sprite;
            repair.growingBrick.color = source.color;
            repair.growingBrick.drawMode = source.drawMode;
            repair.growingBrick.size = source.size;
            repair.growingBrick.sortingLayerID = source.sortingLayerID;
            repair.growingBrick.sortingOrder = source.sortingOrder;
            source.enabled = false;
            GameObject burst = new GameObject("Earth Rebuild Burst");
            burst.transform.SetParent(barrier.transform.parent, false);
            burst.transform.position = barrier.transform.position;
            burst.transform.localScale = Vector3.one * burstSize;
            repair.effect = burst.AddComponent<SpriteRenderer>();
            repair.effect.sharedMaterial = source.sharedMaterial;
            repair.effect.sprite = rebuildFrames[0];
            repair.effect.sortingLayerID = source.sortingLayerID;
            repair.effect.sortingOrder = source.sortingOrder + 1;
        }
        else
        {
            repair.effect = Instantiate(healPrefab, barrier.transform.position,
                healPrefab.transform.rotation, barrier.transform.parent);
            repair.effect.gameObject.SetActive(true);
            repair.effect.sortingLayerID = source.sortingLayerID;
            repair.effect.sortingOrder = source.sortingOrder - 1;
            repair.healColor = repair.effect.color;
            Color color = repair.healColor;
            color.a = 0f;
            repair.effect.color = color;
        }
        repairs.Add(repair);
    }

    private void LateUpdate()
    {
        float duration = rebuildFrames.Length / Mathf.Max(1f, framesPerSecond);
        for (int i = repairs.Count - 1; i >= 0; i--)
        {
            Repair repair = repairs[i];
            repair.elapsed += Time.deltaTime;
            if (repair.barrier == null || !repair.barrier.gameObject.activeInHierarchy || repair.elapsed >= duration)
            {
                Finish(i);
                continue;
            }
            float progress = repair.elapsed / duration;
            if (repair.growingBrick != null)
            {
                repair.growingBrick.sprite = repair.source.sprite;
                repair.growingBrick.transform.localScale = Vector3.one * Mathf.Lerp(0.03f, 1f, Mathf.SmoothStep(0f, 1f, progress));
                repair.effect.sprite = rebuildFrames[Mathf.Min(rebuildFrames.Length - 1,
                    Mathf.FloorToInt(repair.elapsed * Mathf.Max(1f, framesPerSecond)))];
            }
            else
            {
                Color color = repair.healColor;
                color.a *= Mathf.Sin(progress * Mathf.PI);
                repair.effect.color = color;
            }
        }
    }

    private void Finish(int index)
    {
        Repair repair = repairs[index];
        if (repair.source != null) repair.source.enabled = repair.sourceEnabled;
        if (repair.growingBrick != null) Destroy(repair.growingBrick.gameObject);
        if (repair.effect != null) Destroy(repair.effect.gameObject);
        repairs.RemoveAt(index);
    }

    private void OnDisable()
    {
        for (int i = repairs.Count - 1; i >= 0; i--) Finish(i);
    }
}
