using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class LoadScreenUI : MonoBehaviour
{
    [SerializeField] private GameObject continuePrompt;
    private bool ready;
    private int readyFrame;

    private IEnumerator Start()
    {
        if (continuePrompt == null || SceneLoader.Instance == null)
        {
            Debug.LogError("LoadScreen requires its prompt and a SceneLoader.", this);
            yield break;
        }

        continuePrompt.SetActive(false);
        while (SceneLoader.Instance.IsLoading) yield return null;
        yield return SceneLoader.Instance.PreloadGame();
        if (!SceneLoader.Instance.IsGameReady) yield break;

        readyFrame = Time.frameCount;
        ready = true;
        continuePrompt.SetActive(true);
    }

    private void Update()
    {
        if (!ready || Time.frameCount <= readyFrame || Keyboard.current == null ||
            !Keyboard.current.spaceKey.wasPressedThisFrame) return;

        if (SceneLoader.Instance.ContinueFromLoadScreen())
        {
            ready = false;
            continuePrompt.SetActive(false);
        }
    }
}
