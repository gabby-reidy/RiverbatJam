using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

public class SplashScreenUI : MonoBehaviour
{
    [SerializeField] private string nextScene = "MainMenu";
    [SerializeField] private Image blackOverlay;
    [SerializeField] private GameObject continuePrompt;
    [SerializeField] private AudioClip menuMusic;
    [SerializeField, Min(0f)] private float blackHoldDuration = 6f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float promptDelay = 3f;
    private float elapsed;
    private bool revealStarted;
    private bool ready;
    private int shownFrame;
    private bool loading;

    private void OnEnable()
    {
        elapsed = 0f;
        revealStarted = false;
        ready = false;
        if (continuePrompt != null) continuePrompt.SetActive(false);
        if (blackOverlay != null)
        {
            blackOverlay.gameObject.SetActive(true);
            blackOverlay.color = Color.black;
        }
        shownFrame = Time.frameCount;
        loading = false;
    }

    private void Start()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayMusic(menuMusic);
            AudioManager.instance.EnableAudio();
        }
    }

    private void Update()
    {
        if (!loading)
        {
            if (!revealStarted)
            {
                // Wait for the track itself, rather than counting scene loading frames.
                if (AudioManager.instance != null &&
                    AudioManager.instance.TryGetMusicPlaybackTime(menuMusic, out float musicTime) &&
                    musicTime >= blackHoldDuration)
                {
                    revealStarted = true;
                    elapsed = 0f;
                }
            }
            else
            {
                elapsed += Time.unscaledDeltaTime;
            }
            float fade = revealStarted
                ? Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDuration))
                : 0f;
            if (blackOverlay != null)
            {
                blackOverlay.color = new Color(0f, 0f, 0f, 1f - fade);
                if (fade >= 1f) blackOverlay.gameObject.SetActive(false);
            }
            if (!ready && revealStarted && elapsed >= fadeDuration + promptDelay)
            {
                ready = true;
                shownFrame = Time.frameCount;
                if (continuePrompt != null) continuePrompt.SetActive(true);
            }
        }
        if (loading || Time.frameCount <= shownFrame || !Application.isFocused) return;
        foreach (InputDevice device in InputSystem.devices)
        {
            foreach (InputControl control in device.allControls)
            {
                if (!(control is ButtonControl button) || !button.wasPressedThisFrame) continue;
                // Early interaction can unlock browser audio, but cannot skip the reveal.
                if (AudioManager.instance != null) AudioManager.instance.EnableAudio();
                if (!ready) return;
                if (!Application.CanStreamedLevelBeLoaded(nextScene))
                {
                    Debug.LogError("Add the splash destination to the Build Profiles scene list.", this);
                    enabled = false;
                    return;
                }
                loading = true;
                SceneManager.LoadSceneAsync(nextScene);
                return;
            }
        }
    }
}
