using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

public class SplashScreenUI : MonoBehaviour
{
    [SerializeField] private string nextScene = "MainMenu";
    private int shownFrame;
    private bool loading;

    private void OnEnable()
    {
        shownFrame = Time.frameCount;
        loading = false;
    }

    private void Update()
    {
        if (loading || Time.frameCount <= shownFrame || !Application.isFocused) return;
        foreach (InputDevice device in InputSystem.devices)
        {
            foreach (InputControl control in device.allControls)
            {
                if (!(control is ButtonControl button) || !button.wasPressedThisFrame) continue;
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
