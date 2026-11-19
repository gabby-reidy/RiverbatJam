using UnityEngine;

public class PreloadedScene : MonoBehaviour
{
    [SerializeField] private GameObject sceneContent;

    public bool IsConfigured => sceneContent != null && sceneContent != gameObject;

    private void Awake()
    {
        if (!IsConfigured)
        {
            Debug.LogError("Assign the inactive Scene Content child on PreloadedScene.", this);
            return;
        }

        // Opening this scene directly in Play Mode should still work normally.
        if (SceneLoader.Instance == null || !SceneLoader.Instance.IsPreloading)
        {
            SetContentActive(true);
        }
    }

    public void SetContentActive(bool active)
    {
        if (IsConfigured) sceneContent.SetActive(active);
    }
}
