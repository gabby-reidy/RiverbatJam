using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-300)]
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private SettingsMenuUI settingsPanel;
    [SerializeField] private GameBall ball;
    [SerializeField] private AudioClip buttonSound;
    private bool paused;
    private float previousTimeScale = 1f;

    private void OnEnable()
    {
        pauseRoot.SetActive(false);
    }

    private void Update()
    {
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) return;
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (paused)
        {
            if (settingsPanel.gameObject.activeSelf) settingsPanel.Close();
            else Resume();
        }
        else if (ball != null && !ball.IsBattleOver)
        {
            previousTimeScale = Time.timeScale;
            paused = true;
            Time.timeScale = 0f;
            pauseRoot.SetActive(true);
            menuPanel.SetActive(true);
            settingsPanel.gameObject.SetActive(false);
            PlaySound();
        }
    }

    private void PlaySound()
    {
        if (AudioManager.instance == null) return;
        AudioManager.instance.EnableAudio();
        AudioManager.instance.PlaySFX(buttonSound);
    }

    public void Resume()
    {
        if (!paused) return;
        PlaySound();
        paused = false;
        Time.timeScale = previousTimeScale;
        if (ball != null) ball.InputBlockedThroughFrame = Time.frameCount;
        pauseRoot.SetActive(false);
    }

    public void OpenSettings()
    {
        if (!paused) return;
        PlaySound();
        menuPanel.SetActive(false);
        settingsPanel.gameObject.SetActive(true);
    }

    public void Retry()
    {
        if (!paused) return;
        Resume();
        if (SceneLoader.Instance != null) SceneLoader.Instance.RestartBattle(gameObject.scene.name);
        else SceneManager.LoadSceneAsync(gameObject.scene.name);
    }

    public void Quit()
    {
        if (!paused) return;
        Resume();
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadMainMenu();
        else SceneManager.LoadSceneAsync("MainMenu");
    }

    private void OnDisable()
    {
        if (paused) Time.timeScale = previousTimeScale;
        paused = false;
    }
}
