using TMPro;
using System;
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
        currentShot = null;
        SwitchShot(initialShot, null);
        stepIndex = 0;
        BeginStep();
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
        if (playback == Playback.Leaving) return;
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
            SwitchShot(step.shotAfterDialogue, step.backgroundOverride);
            shotSwitched = true;
        }
        bool holdComplete = elapsed >= Mathf.Max(0f, step.dialogueHoldDuration);
        if (holdComplete) dialogueBox.SetActive(false);
        if (!holdComplete || !shotSwitched) return;
        if (step.waitForSpace)
        {
            continuePrompt.SetActive(true);
            promptFrame = Time.frameCount;
            playback = Playback.WaitingForSpace;
        }
        else { stepIndex++; BeginStep(); }
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