using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Navigation")]
    [SerializeField] private GameObject returnPanel;
    [SerializeField] private AudioClip buttonSound;
    [SerializeField] private UnityEngine.Events.UnityEvent onClosed = new UnityEngine.Events.UnityEvent();

    public void Close()
    {
        if(AudioManager.instance != null)
        {
            AudioManager.instance.EnableAudio();
            AudioManager.instance.PlaySFX(buttonSound);
        }

        gameObject.SetActive(false);
        if(returnPanel != null) returnPanel.SetActive(true);
        onClosed.Invoke();
    }

    private SettingsManager settingsManager;
    private bool slidersReady;

    private void OnEnable()
    {
        slidersReady = masterVolumeSlider != null && musicVolumeSlider != null && sfxVolumeSlider != null
            && masterVolumeSlider != musicVolumeSlider && masterVolumeSlider != sfxVolumeSlider
            && musicVolumeSlider != sfxVolumeSlider;

        if(!slidersReady)
        {
            Debug.LogError("Assign three separate volume sliders on SettingsMenuUI.", this);
            return;
        }

        ConfigureSlider(masterVolumeSlider);
        ConfigureSlider(musicVolumeSlider);
        ConfigureSlider(sfxVolumeSlider);

        // Callbacks are registered here; leave Inspector On Value Changed lists empty.
        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        BindSettings();
    }

    private void ConfigureSlider(Slider slider)
    {
        slider.wholeNumbers = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
    }

    private void Update()
    {
        if(slidersReady && settingsManager != SettingsManager.Instance)
        {
            BindSettings();
        }
    }

    private void BindSettings()
    {
        if(settingsManager != null)
        {
            settingsManager.AudioSettingsChanged -= RefreshSliders;
        }

        settingsManager = SettingsManager.Instance;
        bool settingsAvailable = settingsManager != null;
        masterVolumeSlider.interactable = settingsAvailable;
        musicVolumeSlider.interactable = settingsAvailable;
        sfxVolumeSlider.interactable = settingsAvailable;

        if(settingsAvailable)
        {
            settingsManager.AudioSettingsChanged += RefreshSliders;
            RefreshSliders();
        }
    }

    private void RefreshSliders()
    {
        if(settingsManager == null)
        {
            return;
        }

        masterVolumeSlider.SetValueWithoutNotify(settingsManager.MasterVolume);
        musicVolumeSlider.SetValueWithoutNotify(settingsManager.MusicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(settingsManager.SFXVolume);
    }

    public void SetMasterVolume(float volume)
    {
        if(settingsManager != null)
        {
            settingsManager.SetMasterVolume(volume);
        }
    }

    public void SetMusicVolume(float volume)
    {
        if(settingsManager != null)
        {
            settingsManager.SetMusicVolume(volume);
        }
    }

    public void SetSFXVolume(float volume)
    {
        if(settingsManager != null)
        {
            settingsManager.SetSFXVolume(volume);
        }
    }

    private void OnDisable()
    {
        if(slidersReady)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            musicVolumeSlider.onValueChanged.RemoveListener(SetMusicVolume);
            sfxVolumeSlider.onValueChanged.RemoveListener(SetSFXVolume);
        }

        if(settingsManager != null)
        {
            settingsManager.AudioSettingsChanged -= RefreshSliders;
        }

        settingsManager = null;
        slidersReady = false;
    }
}
