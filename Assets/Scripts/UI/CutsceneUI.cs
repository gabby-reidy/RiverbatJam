using TMPro;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum CutsceneShot { KeepCurrent = -1, PlayerBehindBossAhead, PlayerCloseUp, BossCloseUp, BossBehindPlayerAhead, PlayerFallingCloseUp }
public enum CutsceneCharacter { Player, Boss }

[Serializable]
public class CutsceneExpression
{
    public CutsceneCharacter character;
    [Min(0), Tooltip("Apply after this many letters appear. Zero applies at the start.")]
    public int atCharacter;
    public Sprite sprite;
}

[Serializable]
public class CutsceneDialogueStep
{
    public string speakerName;
    [TextArea(3, 8)] public string dialogue;
    [Min(0), Tooltip("Letters per second. Zero reveals the whole line immediately.")]
    public float textSpeed = 40f;
    [Min(0), Tooltip("Seconds after the final letter before hiding the dialogue box.")]
    public float dialogueHoldDuration = 2f;
    [Min(0), Tooltip("Seconds after the final letter before switching shots. Independent of dialogue hold.")]
    public float shotSwitchDelay = 0.5f;
    public CutsceneShot shotAfterDialogue = CutsceneShot.KeepCurrent;
    public Sprite backgroundOverride;
    public bool revealMemory;
    public bool combatImpact;
    public bool transformAfterDialogue;
    public AudioClip music;
    [Tooltip("After both timers finish, show the Space prompt and wait before advancing.")]
    public bool waitForSpace;
    public CutsceneExpression[] expressionChanges = new CutsceneExpression[0];
}

[Serializable]
public class CutsceneShotLayout
{
    public CutsceneShot shot;
    public GameObject root;
    public Image background;
    public Image player;
    public Image boss;
    [NonSerialized] public Sprite originalBackground;
    [NonSerialized] public Sprite originalPlayer;
    [NonSerialized] public Sprite originalBoss;
}

public class CutsceneUI : MonoBehaviour
{
    [SerializeField] private string fightScene = "FightScreen";
    [Header("Scene UI")]
    [SerializeField] private GameObject dialogueBox;
    [SerializeField] private TMP_Text speakerLabel;
    [SerializeField] private TMP_Text dialogueLabel;
    [SerializeField] private GameObject continuePrompt;
    [SerializeField] private AudioClip continueSound;
    [Header("Shots")]
    [SerializeField] private CutsceneShot initialShot = CutsceneShot.PlayerBehindBossAhead;
    [SerializeField] private CutsceneShotLayout[] shotLayouts;
    [Header("Dialogue Sequence")]
    [SerializeField] private CutsceneDialogueStep[] dialogueSteps;

    [Header("Campaign Transitions")]
    [SerializeField] private Image fadeOverlay;
    [SerializeField] private Image memoryImage;
    [SerializeField] private Sprite memorySprite;
    [SerializeField, Min(0f)] private float entryFadeDuration = 1.5f;
    [SerializeField, Min(0.01f)] private float memoryFadeDuration = 1f;
    [SerializeField] private AudioClip sceneMusic;
    [Header("Combat Impact")]
    [SerializeField] private Image impactFlash;
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioClip fallingMusic;
    [SerializeField, Min(0.01f)] private float impactDuration = 0.4f;
    [SerializeField, Min(0.01f)] private float flashDuration = 0.15f;
    [SerializeField, Min(0f)] private float shakePixels = 18f;
    private RectTransform shakenShot;
    private Vector2 originalShotPosition;
    [Header("Final Flight")]
    [SerializeField] private GameObject[] fallingVisuals;
    [SerializeField] private RectTransform platformPlayer;
    [SerializeField] private Image transformationEffect;
    [SerializeField] private Sprite[] transformationFrames;
    [SerializeField, Min(1f)] private float transformationFPS = 18f;
    [SerializeField, Min(0)] private int transformationSwapFrame = 4;
    [SerializeField] private bool flyOnFinalContinue;
    [SerializeField, Min(0.1f)] private float flightDuration = 1.5f;
    [SerializeField, Min(0f)] private float flightHeight = 1500f;
    [SerializeField, Min(0f)] private float flightFadeDelay = 0.25f;
    private Vector2 platformStartPosition;
    private bool transformationPlayed;
    private bool transitioning;
    private bool memoryRevealed;

    private enum Playback { Revealing, Holding, WaitingForSpace, Finished, Leaving }
    private Playback playback;
    private int stepIndex;
    private int promptFrame;
    private float elapsed;
    private int visibleCharacters;
    private bool shotSwitched;
    private bool configured;
    private bool[] expressionApplied;
    private CutsceneShotLayout currentShot;

    private void Awake()
    {
        if (dialogueBox == null || speakerLabel == null || dialogueLabel == null || continuePrompt == null ||
            shotLayouts == null || shotLayouts.Length == 0)
        {
            Debug.LogError("Assign the dialogue UI, Space prompt, and shot layouts on CutsceneUI.", this);
            enabled = false;
            return;
        }
        foreach (CutsceneShotLayout layout in shotLayouts)
        {
            if (layout == null || layout.root == null || layout.background == null)
            {
                Debug.LogError("Every cutscene shot needs its root and background.", this);
                enabled = false;
                return;
            }
            layout.originalBackground = layout.background.sprite;
            if (layout.player != null) layout.originalPlayer = layout.player.sprite;
            if (layout.boss != null) layout.originalBoss = layout.boss.sprite;
        }
        if (platformPlayer != null) platformStartPosition = platformPlayer.anchoredPosition;
        dialogueLabel.richText = false;
        configured = true;
    }

    private void OnEnable()
    {
        if (!configured) return;
        foreach (CutsceneShotLayout layout in shotLayouts)
        {
            layout.background.sprite = layout.originalBackground;
            if (layout.player != null) layout.player.sprite = layout.originalPlayer;
            if (layout.boss != null) layout.boss.sprite = layout.originalBoss;
            layout.root.SetActive(false);
        }
        transitioning = false;
        memoryRevealed = false;
        transformationPlayed = false;
        if (platformPlayer != null)
        {
            platformPlayer.anchoredPosition = platformStartPosition;
            platformPlayer.gameObject.SetActive(false);
            if (fallingVisuals != null) foreach (GameObject visual in fallingVisuals)
                if (visual != null) visual.SetActive(true);
        }
        if (transformationEffect != null) transformationEffect.gameObject.SetActive(false);
        if (memoryImage != null) memoryImage.gameObject.SetActive(false);
        currentShot = null;
        SwitchShot(initialShot, null);
        stepIndex = 0;
        StartCoroutine(FadeIn());
    }

    private void BeginStep()
    {
        continuePrompt.SetActive(false);
        if (dialogueSteps == null || stepIndex >= dialogueSteps.Length)
        {
            dialogueBox.SetActive(false);
            playback = Playback.Finished;
            return;
        }
        CutsceneDialogueStep step = dialogueSteps[stepIndex];
        if (step == null)
        {
            Debug.LogError("Remove the empty dialogue step at index " + stepIndex, this);
            enabled = false;
            return;
        }
        if (step.music != null && AudioManager.instance != null) AudioManager.instance.PlayMusic(step.music);
        speakerLabel.text = step.speakerName ?? "";
        dialogueLabel.text = "";
        dialogueBox.SetActive(true);
        elapsed = 0f;
        visibleCharacters = 0;
        shotSwitched = false;
        expressionApplied = new bool[step.expressionChanges == null ? 0 : step.expressionChanges.Length];
        playback = Playback.Revealing;
        ApplyExpressions(step);
    }

    private void Update()
    {
        if (transitioning || playback == Playback.Leaving || (SceneLoader.Instance != null && SceneLoader.Instance.IsTransitioning)) return;
        if (playback == Playback.Finished) { EnterFight(); return; }
        if (playback == Playback.WaitingForSpace)
        {
            if (Time.frameCount > promptFrame && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (AudioManager.instance != null)
                {
                    AudioManager.instance.EnableAudio();
                    AudioManager.instance.PlaySFX(continueSound);
                }
                if (flyOnFinalContinue && stepIndex == dialogueSteps.Length - 1 && platformPlayer != null)
                {
                    StartCoroutine(FlyIntoFight());
                    return;
                }
                stepIndex++;
                BeginStep();
            }
            return;
        }
        CutsceneDialogueStep step = dialogueSteps[stepIndex];
        elapsed += Time.deltaTime;
        if (playback == Playback.Revealing)
        {
            string line = step.dialogue ?? "";
            int count = step.textSpeed <= 0f ? line.Length : Mathf.Min(line.Length, Mathf.FloorToInt(elapsed * step.textSpeed));
            if (count != visibleCharacters)
            {
                visibleCharacters = count;
                dialogueLabel.text = line.Substring(0, count);
                ApplyExpressions(step);
            }
            if (visibleCharacters < line.Length) return;
            elapsed = 0f;
            playback = Playback.Holding;
        }
        // Both timers start at the final letter, not one after another.
        if (!shotSwitched && elapsed >= Mathf.Max(0f, step.shotSwitchDelay))
        {
            shotSwitched = true;
            if (step.combatImpact)
            {
                StartCoroutine(CombatImpact(step));
                return;
            }
            SwitchShot(step.shotAfterDialogue, step.backgroundOverride);
        }
        bool holdComplete = elapsed >= Mathf.Max(0f, step.dialogueHoldDuration);
        if (holdComplete) dialogueBox.SetActive(false);
        if (!holdComplete || !shotSwitched) return;
        if (step.transformAfterDialogue && !transformationPlayed && platformPlayer != null)
        {
            StartCoroutine(TransformToPlatform());
            return;
        }
        if (step.revealMemory && !memoryRevealed && memorySprite != null)
        {
            StartCoroutine(RevealMemory());
            return;
        }
        if (step.waitForSpace)
        {
            continuePrompt.SetActive(true);
            promptFrame = Time.frameCount;
            playback = Playback.WaitingForSpace;
        }
        else { stepIndex++; BeginStep(); }
    }

    private IEnumerator TransformToPlatform()
    {
        transitioning = true;
        transformationPlayed = true;
        if (transformationEffect != null && transformationFrames != null && transformationFrames.Length > 0)
        {
            transformationEffect.gameObject.SetActive(true);
            bool swapped = false;
            for (int i = 0; i < transformationFrames.Length; i++)
            {
                transformationEffect.sprite = transformationFrames[i];
                if (!swapped && i >= Mathf.Clamp(transformationSwapFrame, 0, transformationFrames.Length - 1))
                {
                    ShowPlatform();
                    swapped = true;
                }
                yield return new WaitForSeconds(1f / Mathf.Max(1f, transformationFPS));
            }
            transformationEffect.gameObject.SetActive(false);
        }
        ShowPlatform();
        transitioning = false;
    }

    private void ShowPlatform()
    {
        if (fallingVisuals != null) foreach (GameObject visual in fallingVisuals)
            if (visual != null) visual.SetActive(false);
        platformPlayer.gameObject.SetActive(true);
    }

    private IEnumerator FlyIntoFight()
    {
        transitioning = true;
        continuePrompt.SetActive(false);
        dialogueBox.SetActive(false);
        Vector2 start = platformPlayer.anchoredPosition;
        float time = 0f;
        bool requested = false;
        while (time < flightDuration)
        {
            float t = Mathf.Clamp01(time / Mathf.Max(0.1f, flightDuration));
            platformPlayer.anchoredPosition = start + Vector2.up * flightHeight * t * t;
            if (!requested && time >= flightFadeDelay && SceneLoader.Instance != null)
                requested = SceneLoader.Instance.EnterFight(fightScene);
            yield return null;
            // The scene transition pauses gameplay, but the flight must keep moving.
            time += Time.unscaledDeltaTime;
        }
        platformPlayer.anchoredPosition = start + Vector2.up * flightHeight;
        while (!requested)
        {
            if (SceneLoader.Instance != null) requested = SceneLoader.Instance.EnterFight(fightScene);
            else if (Application.CanStreamedLevelBeLoaded(fightScene))
            {
                SceneManager.LoadSceneAsync(fightScene);
                requested = true;
            }
            yield return null;
        }
        playback = Playback.Leaving;
    }

    private IEnumerator CombatImpact(CutsceneDialogueStep step)
    {
        transitioning = true;
        dialogueBox.SetActive(false);
        continuePrompt.SetActive(false);
        if (AudioManager.instance != null)
        {
            AudioManager.instance.CutToMusic(fallingMusic);
            AudioManager.instance.PlaySFX(impactSound);
        }
        if (impactFlash != null)
        {
            impactFlash.gameObject.SetActive(true);
            impactFlash.color = Color.white;
            impactFlash.transform.SetAsLastSibling();
        }
        SwitchShot(step.shotAfterDialogue, step.backgroundOverride);
        shakenShot = currentShot != null ? currentShot.root.GetComponent<RectTransform>() : null;
        if (shakenShot != null) originalShotPosition = shakenShot.anchoredPosition;
        float time = 0f;
        float duration = Mathf.Max(impactDuration, flashDuration);
        while (time < duration)
        {
            if (shakenShot != null)
                shakenShot.anchoredPosition = originalShotPosition + UnityEngine.Random.insideUnitCircle * shakePixels * (1f - Mathf.Clamp01(time / impactDuration));
            if (impactFlash != null)
                impactFlash.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(time / flashDuration));
            yield return null;
            time += Time.deltaTime;
        }
        ResetImpact();
        transitioning = false;
    }

    private void ResetImpact()
    {
        if (shakenShot != null) shakenShot.anchoredPosition = originalShotPosition;
        shakenShot = null;
        if (impactFlash != null) impactFlash.gameObject.SetActive(false);
    }

    private void ApplyExpressions(CutsceneDialogueStep step)
    {
        // Apply crossed markers in time order even when one frame reveals several letters.
        while (true)
        {
            int next = -1;
            for (int i = 0; i < expressionApplied.Length; i++)
            {
                CutsceneExpression change = step.expressionChanges[i];
                if (expressionApplied[i] || change == null || change.atCharacter > visibleCharacters) continue;
                if (next < 0 || change.atCharacter < step.expressionChanges[next].atCharacter) next = i;
            }
            if (next < 0) return;
            expressionApplied[next] = true;
            CutsceneExpression expression = step.expressionChanges[next];
            if (expression.sprite == null) continue;
            foreach (CutsceneShotLayout layout in shotLayouts)
            {
                Image portrait = expression.character == CutsceneCharacter.Player ? layout.player : layout.boss;
                if (portrait != null) portrait.sprite = expression.sprite;
            }
        }
    }

    private void SwitchShot(CutsceneShot shot, Sprite background)
    {
        if (shot != CutsceneShot.KeepCurrent)
        {
            CutsceneShotLayout next = Array.Find(shotLayouts, layout => layout.shot == shot);
            if (next == null) Debug.LogError("Missing cutscene shot: " + shot, this);
            else
            {
                if (currentShot != null) currentShot.root.SetActive(false);
                currentShot = next;
                currentShot.root.SetActive(true);
            }
        }
        if (background != null && currentShot != null) currentShot.background.sprite = background;
    }

    private IEnumerator FadeIn()
    {
        transitioning = true;
        dialogueBox.SetActive(false);
        continuePrompt.SetActive(false);
        if (AudioManager.instance != null)
        {
            if (sceneMusic != null) AudioManager.instance.PlayMusic(sceneMusic);
            else AudioManager.instance.StopMusic();
        }
        bool sharedFade = SceneLoader.Instance != null && SceneLoader.Instance.IsTransitioning;
        if (sharedFade)
        {
            if (fadeOverlay != null) fadeOverlay.gameObject.SetActive(false);
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsTransitioning) yield return null;
        }
        else if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < entryFadeDuration)
            {
                fadeOverlay.color = new Color(0, 0, 0, 1f - elapsed / Mathf.Max(0.01f, entryFadeDuration));
                elapsed += Time.deltaTime;
                yield return null;
            }
            fadeOverlay.gameObject.SetActive(false);
        }
        transitioning = false;
        BeginStep();
    }

    private IEnumerator RevealMemory()
    {
        transitioning = true;
        memoryImage.sprite = memorySprite;
        memoryImage.color = new Color(1, 1, 1, 0);
        memoryImage.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < memoryFadeDuration)
        {
            memoryImage.color = new Color(1, 1, 1, elapsed / memoryFadeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        memoryImage.color = Color.white;
        memoryRevealed = true;
        transitioning = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ResetImpact();
    }

    private void EnterFight()
    {
        if (SceneLoader.Instance != null)
        {
            if (SceneLoader.Instance.EnterFight(fightScene)) playback = Playback.Leaving;
        }
        else if (Application.CanStreamedLevelBeLoaded(fightScene))
        {
            playback = Playback.Leaving;
            SceneManager.LoadSceneAsync(fightScene);
        }
    }
}