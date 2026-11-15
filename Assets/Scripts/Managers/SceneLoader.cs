using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance {get; private set;}

    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string startingScene = "SampleScene";

    public bool IsLoading {get; private set;}

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartGame()
    {
        LoadScene(startingScene);
    }

    public void LoadMainMenu()
    {
        LoadScene(mainMenuScene);
    }

    private void LoadScene(string sceneName)
    {
        if(IsLoading)
        {
            return;
        }

        if(string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Cannot load scene: " + sceneName + ". Check the scene name and enable it in the Build Profiles scene list.", this);
            return;
        }

        if(!isActiveAndEnabled)
        {
            Debug.LogError("SceneLoader must be active and enabled to load a scene.", this);
            return;
        }

        IsLoading = true;
        AsyncOperation operation;

        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch(Exception exception)
        {
            IsLoading = false;
            Debug.LogException(exception, this);
            return;
        }

        if(operation == null)
        {
            IsLoading = false;
            Debug.LogError("Unity could not start loading scene: " + sceneName, this);
            return;
        }

        if(AudioManager.instance != null)
        {
            AudioManager.instance.StopMusic();
        }

        StartCoroutine(WaitForScene(operation));
    }

    private IEnumerator WaitForScene(AsyncOperation operation)
    {
        yield return operation;
        IsLoading = false;
    }

    private void OnDestroy()
    {
        if(Instance == this)
        {
            Instance = null;
        }
    }
}
