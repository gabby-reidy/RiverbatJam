using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CreditsUI : MonoBehaviour
{
    [SerializeField] private AudioClip music;
    private bool leaving;
    private int shownFrame;
    private System.Collections.IEnumerator Start()
    {
        if (AudioManager.instance != null && music != null) AudioManager.instance.PlayMusic(music);
        while (SceneLoader.Instance != null && SceneLoader.Instance.IsTransitioning) yield return null;
        shownFrame = Time.frameCount;
        leaving = false;
    }
    private void Update()
    {
        if ((SceneLoader.Instance != null && SceneLoader.Instance.IsTransitioning) || leaving || Time.frameCount <= shownFrame || Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame) return;
        leaving = true;
        if (SceneLoader.Instance != null) SceneLoader.Instance.LoadMainMenu();
        else SceneManager.LoadSceneAsync("MainMenu");
    }
}
