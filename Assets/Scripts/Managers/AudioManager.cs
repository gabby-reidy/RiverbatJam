using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance {get; private set;}

    private AudioSource musicSource;
    private AudioSource outgoingMusicSource;
    private AudioSource sfxSource;
    [SerializeField, Min(0.01f)] private float musicFadeDuration = 2f;
    private float musicGain;
    private float outgoingMusicGain;
    private float targetMusicGain;
    private SettingsManager settingsManager;
    private AudioClip requestedMusic;
    private bool audioEnabled;

    private float masterVolume = 1f;
    private float musicVolume = 1f;
    private float sfxVolume = 1f;

    private void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = CreateAudioSource("MusicSource");
        musicSource.loop = true;
        outgoingMusicSource = CreateAudioSource("OutgoingMusicSource");
        outgoingMusicSource.loop = true;
        ApplySourceVolumes();
        sfxSource = CreateAudioSource("SFXSource");
    }

    private AudioSource CreateAudioSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void Start()
    {
        BindSettings();
    }

    private void Update()
    {
        float fadeStep = Time.unscaledDeltaTime / Mathf.Max(0.01f, musicFadeDuration);
        musicGain = Mathf.MoveTowards(musicGain, targetMusicGain, fadeStep);
        outgoingMusicGain = Mathf.MoveTowards(outgoingMusicGain, 0f, fadeStep);
        if (musicGain == 0f && targetMusicGain == 0f && musicSource.isPlaying) musicSource.Stop();
        if (outgoingMusicGain == 0f && outgoingMusicSource.isPlaying) outgoingMusicSource.Stop();
        ApplySourceVolumes();

        // Also handles a settings manager created later or replaced between scenes.
        if(settingsManager != SettingsManager.Instance)
        {
            BindSettings();
        }
    }

    private void BindSettings()
    {
        if(settingsManager != null)
        {
            settingsManager.AudioSettingsChanged -= ApplyAudioSettings;
        }

        settingsManager = SettingsManager.Instance;

        if(settingsManager != null)
        {
            settingsManager.AudioSettingsChanged += ApplyAudioSettings;
            ApplyAudioSettings();
        }
    }

    private void ApplyAudioSettings()
    {
        if(settingsManager == null)
        {
            return;
        }

        masterVolume = settingsManager.MasterVolume;
        musicVolume = settingsManager.MusicVolume;
        sfxVolume = settingsManager.SFXVolume;
        ApplySourceVolumes();
    }

    // Call from the menu's click/tap handler. This gate does not bypass browser policy.
    public void EnableAudio()
    {
        BindSettings();
        audioEnabled = true;

        if(requestedMusic != null)
        {
            PlayMusic(requestedMusic);
        }
    }

    public void PlayMusic(AudioClip musicClip)
    {
        if(musicClip == null)
        {
            StopMusic();
            return;
        }

        requestedMusic = musicClip;

        if(!audioEnabled || musicSource == null)
        {
            return;
        }

        if(musicSource.clip == musicClip && musicSource.isPlaying)
        {
            targetMusicGain = 1f;
            return;
        }

        // Reuse the fading-out source, keeping the incoming track's clock independent.
        AudioSource previous = musicSource;
        musicSource = outgoingMusicSource;
        outgoingMusicSource = previous;
        float previousGain = musicGain;
        musicGain = outgoingMusicGain;
        outgoingMusicGain = previousGain;
        if (musicSource.clip != musicClip || !musicSource.isPlaying)
        {
            musicSource.Stop();
            musicGain = 0f;
            musicSource.clip = musicClip;
            ApplySourceVolumes();
            musicSource.Play();
        }
        targetMusicGain = 1f;
    }

    public bool TryGetMusicPlaybackTime(AudioClip clip, out float seconds)
    {
        seconds = 0f;
        if (clip == null || musicSource == null || musicSource.clip != clip || !musicSource.isPlaying)
        {
            return false;
        }

        seconds = musicSource.timeSamples / (float)clip.frequency;
        return true;
    }

    public void StopMusic()
    {
        requestedMusic = null;
        targetMusicGain = 0f;
    }

    public void PlaySFX(AudioClip sfxClip)
    {
        // Discard sounds requested before interaction instead of replaying them later.
        if(!audioEnabled || sfxClip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(sfxClip);
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplySourceVolumes();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplySourceVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplySourceVolumes();
    }

    private void ApplySourceVolumes()
    {
        if(musicSource != null)
        {
            musicSource.volume = masterVolume * musicVolume * musicGain;
        }

        if(outgoingMusicSource != null)
        {
            outgoingMusicSource.volume = masterVolume * musicVolume * outgoingMusicGain;
        }

        if(sfxSource != null)
        {
            sfxSource.volume = masterVolume * sfxVolume;
        }
    }

    private void OnDestroy()
    {
        if(settingsManager != null)
        {
            settingsManager.AudioSettingsChanged -= ApplyAudioSettings;
        }

        if(instance == this)
        {
            instance = null;
        }
    }
}
