using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance {get; private set;}

    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string startingScene = "LoadScreen";
    [SerializeField] private string firstGameScene = "Cutscene";
    [SerializeField] private string[] preloadScenes = { "Cutscene", "FightScreen" };
    private readonly Dictionary<string, PreloadedScene> preloaded = new Dictionary<string, PreloadedScene>();
    public bool IsPreloading { get; private set; }
    public bool IsGameReady { get; private set; }

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
        Time.timeScale = 1f;
        LoadScene(startingScene);
    }

    public void RestartBattle(string sceneName)
    {
        LoadScene(sceneName);
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

        IsGameReady = false;
        preloaded.Clear();
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

    public IEnumerator PreloadGame()
    {
        if (IsLoading || IsPreloading) yield break;
        IsGameReady = false;
        if (preloadScenes == null || preloadScenes.Length == 0 || Array.IndexOf(preloadScenes, firstGameScene) < 0)
        {
            Debug.LogError("The preload list must include the first game scene.", this);
            yield break;
        }

        HashSet<string> names = new HashSet<string>();
        foreach (string sceneName in preloadScenes)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || sceneName == mainMenuScene || sceneName == startingScene ||
                !names.Add(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("Invalid, duplicate, or unlisted preload scene: " + sceneName, this);
                yield break;
            }
        }

        IsLoading = true;
        IsPreloading = true;
        bool succeeded = true;
        try
        {
            foreach (string sceneName in preloadScenes)
            {
                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.isLoaded)
                {
                    AsyncOperation operation = BeginPreload(sceneName);
                    if (operation == null) { succeeded = false; break; }
                    yield return operation;
                    scene = SceneManager.GetSceneByName(sceneName);
                }

                PreloadedScene entry = null;
                if (scene.isLoaded)
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        entry = root.GetComponent<PreloadedScene>();
                        if (entry != null) break;
                    }
                }
                if (entry == null || !entry.IsConfigured)
                {
                    Debug.LogError("Preload scene needs a PreloadedScene root with inactive content: " + sceneName, this);
                    succeeded = false;
                    break;
                }
                entry.SetContentActive(false);
                preloaded[sceneName] = entry;
            }
        }
        finally
        {
            IsPreloading = false;
            IsLoading = false;
        }
        IsGameReady = succeeded && preloaded.Count == preloadScenes.Length;
    }

    private AsyncOperation BeginPreload(string sceneName)
    {
        try
        {
            return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return null;
        }
    }

    public bool ContinueFromLoadScreen()
    {
        if (!IsGameReady || IsLoading || !ActivateGameScene(firstGameScene)) return false;
        Scene loadingScene = SceneManager.GetSceneByName(startingScene);
        if (loadingScene.isLoaded)
        {
            foreach (GameObject root in loadingScene.GetRootGameObjects()) root.SetActive(false);
            IsLoading = true;
            StartCoroutine(WaitForScene(SceneManager.UnloadSceneAsync(loadingScene)));
        }
        return true;
    }

    public bool ActivateGameScene(string sceneName)
    {
        if (IsLoading || !IsGameReady || !preloaded.TryGetValue(sceneName, out PreloadedScene next) || next == null)
            return false;
        if (!SceneManager.SetActiveScene(next.gameObject.scene)) return false;
        foreach (PreloadedScene entry in preloaded.Values)
            if (entry != null && entry != next) entry.SetContentActive(false);
        next.SetContentActive(true);
        return true;
    }

    public bool EnterFight(string sceneName)
    {
        if (IsLoading) return false;
        if (IsGameReady) return ActivateGameScene(sceneName);
        LoadScene(sceneName);
        return IsLoading;
    }

    public void ReturnAfterVictory(string fightScene)
    {
        if (IsLoading) return;
        if (!IsGameReady)
        {
            LoadScene(firstGameScene);
            return;
        }
        if (ActivateGameScene(firstGameScene)) StartCoroutine(RefreshFight(fightScene));
    }

    private IEnumerator RefreshFight(string sceneName)
    {
        // Temporary loop: prepare a fresh fight while the placeholder cutscene is shown.
        IsLoading = true;
        IsPreloading = true;
        preloaded.Remove(sceneName);
        try
        {
            Scene oldScene = SceneManager.GetSceneByName(sceneName);
            if (oldScene.isLoaded) yield return SceneManager.UnloadSceneAsync(oldScene);
            AsyncOperation operation = BeginPreload(sceneName);
            if (operation != null) yield return operation;
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    PreloadedScene entry = root.GetComponent<PreloadedScene>();
                    if (entry == null || !entry.IsConfigured) continue;
                    entry.SetContentActive(false);
                    preloaded[sceneName] = entry;
                    break;
                }
            }
            if (!preloaded.ContainsKey(sceneName))
            {
                IsGameReady = false;
                Debug.LogError("Could not prepare a fresh fight: " + sceneName, this);
            }
        }
        finally
        {
            IsPreloading = false;
            IsLoading = false;
        }
    }

    private void OnDestroy()
    {
        if(Instance == this)
        {
            Instance = null;
        }
    }
}
