using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip buttonSound;

    private void Start()
    {
        if(ValidatePanels())
        {
            settingsPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
        }

        if(AudioManager.instance != null && menuMusic != null)
        {
            AudioManager.instance.PlayMusic(menuMusic);
        }
    }

    public void PlayGame()
    {
        if(SceneLoader.Instance == null)
        {
            Debug.LogError("MainMenuUI requires a SceneLoader to start the game.", this);
            return;
        }

        if(SceneLoader.Instance.IsLoading || !ValidatePanels())
        {
            return;
        }

        PlayButtonAudio();
        SceneLoader.Instance.StartGame();

    }

    public void OpenSettings()
    {
        if(IsLoading() || !ValidatePanels())
        {
            return;
        }

        PlayButtonAudio();
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if(IsLoading() || !ValidatePanels())
        {
            return;
        }

        PlayButtonAudio();
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    private void PlayButtonAudio()
    {
        if(AudioManager.instance == null)
        {
            return;
        }

        if(menuMusic != null)
        {
            AudioManager.instance.PlayMusic(menuMusic);
        }

        AudioManager.instance.EnableAudio();
        AudioManager.instance.PlaySFX(buttonSound);
    }

    private bool IsLoading()
    {
        return SceneLoader.Instance != null && SceneLoader.Instance.IsLoading;
    }

    private bool ValidatePanels()
    {
        if(mainMenuPanel == null || settingsPanel == null || mainMenuPanel == settingsPanel)
        {
            Debug.LogError("Assign separate Main Menu and Settings panels on MainMenuUI.", this);
            return false;
        }

        return true;
    }
}
