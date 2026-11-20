using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CreditsUI : MonoBehaviour
{
    [SerializeField] private AudioClip music;
    private bool leaving;
    private int shownFrame;
    private void OnEnable()
    {
        shownFrame = Time.frameCount;
        leaving = false;
        if (AudioManager.instance != null && music != null) AudioManager.instance.PlayMusic(music);
    }
    private void Update()
    {
        if (leaving || Time.frameCount <= shownFrame || Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame) return;
        leaving = true;
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadMainMenu();
        else SceneManager.LoadSceneAsync("MainMenu");
    }
}
