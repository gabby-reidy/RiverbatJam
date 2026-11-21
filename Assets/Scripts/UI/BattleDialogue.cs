using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[Serializable]
public class BattleLine
{
    public string speaker;
    [TextArea(2, 8)] public string text;
}

[DefaultExecutionOrder(-200)]
public class BattleDialogue : MonoBehaviour
{
    [SerializeField] private GameBall ball;
    [SerializeField] private PlayerPaddle player;
    [SerializeField] private EnemyPaddle enemy;
    [SerializeField] private GameObject dialogueBox;
    [SerializeField] private TMP_Text speakerLabel;
    [SerializeField] private TMP_Text dialogueLabel;
    [SerializeField] private GameObject continuePrompt;
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private TMP_Text restartLabel;
    [SerializeField] private TMP_Text menuLabel;
    [SerializeField] private string nextScene;
    [SerializeField] private AudioClip battleMusic;
    [SerializeField] private BattleLine[] introduction;
    [SerializeField] private BattleLine[] playerLostLifeQuips;
    [SerializeField] private BattleLine[] enemyLostLifeQuips;
    [SerializeField] private BattleLine[] victory;
    [SerializeField] private BattleLine[] defeat;
    [SerializeField, Min(1f)] private float lettersPerSecond = 45f;
    [SerializeField, Min(0f)] private float lineHold = 1.5f;
    [SerializeField, Range(1f, 2f)] private float openingServeMultiplier = 1.3f;
    private int playerQuip;
    private int enemyQuip;
    private bool choices;
    private bool leaving;

    private void OnEnable()
    {
        ball.CampaignControlled = true;
        ball.RallyEnded += OnRallyEnded;
        Lock(true);
        ball.gameObject.SetActive(false);
        continuePrompt.SetActive(false);
        defeatPanel.SetActive(false);
        dialogueBox.SetActive(false);
    }

    private IEnumerator Start()
    {
        if (AudioManager.instance != null)
        {
            if (battleMusic != null) AudioManager.instance.PlayMusic(battleMusic);
            else AudioManager.instance.StopMusic();
        }
        while (SceneLoader.Instance != null && SceneLoader.Instance.IsTransitioning) yield return null;
        yield return Speak(introduction);
        yield return WaitForSpace();
        ball.OpeningServeMultiplier = openingServeMultiplier;
        ball.gameObject.SetActive(true);
        // Let GameBall.Start initialize its held opening serve before releasing controls.
        yield return null;

        Lock(false);
    }

    private void Lock(bool locked)
    {
        ball.GameplayLocked = locked;
        if (!locked) ball.InputBlockedThroughFrame = Time.frameCount;
        player.ControlsLocked = locked;
        enemy.ControlsLocked = locked;
        if (locked) { player.CancelDash(); enemy.CancelDash(); }
    }

    private void OnRallyEnded(bool playerDamaged, bool lifeLost, bool ended)
    {
        if (ended) StartCoroutine(EndBattle());
        else if (lifeLost)
        {
            BattleLine[] quips = playerDamaged ? playerLostLifeQuips : enemyLostLifeQuips;
            int index = playerDamaged ? playerQuip++ : enemyQuip++;
            StartCoroutine(LifeDialogue(quips, index));
        }
    }

    private IEnumerator LifeDialogue(BattleLine[] lines, int index)
    {
        Lock(true);
        if (lines != null && lines.Length > 0) yield return Speak(new[] { lines[index % lines.Length] });
        yield return WaitForSpace();
        Lock(false);
    }

    private IEnumerator EndBattle()
    {
        Lock(true);
        yield return Speak(player.IsDefeated ? defeat : victory);
        if (player.IsDefeated)
        {
            defeatPanel.SetActive(true);
            choices = true;
        }
        else
        {
            yield return WaitForSpace();
            while (!GoTo(nextScene)) yield return null;
        }
    }

    private IEnumerator Speak(BattleLine[] lines)
    {
        continuePrompt.SetActive(false);
        if (lines != null) foreach (BattleLine line in lines)
        {
            speakerLabel.text = line.speaker;
            dialogueLabel.text = line.text ?? "";
            dialogueLabel.maxVisibleCharacters = 0;
            dialogueBox.SetActive(true);
            float elapsed = 0f;
            while (dialogueLabel.maxVisibleCharacters < dialogueLabel.text.Length)
            {
                elapsed += Time.deltaTime;
                dialogueLabel.maxVisibleCharacters = Mathf.FloorToInt(elapsed * lettersPerSecond);
                yield return null;
            }
            dialogueLabel.maxVisibleCharacters = int.MaxValue;
            yield return new WaitForSeconds(lineHold);
        }
        dialogueBox.SetActive(false);
    }

    private IEnumerator WaitForSpace()
    {
        continuePrompt.SetActive(true);
        yield return null;
        while (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame) yield return null;
        if (AudioManager.instance != null) AudioManager.instance.EnableAudio();
        continuePrompt.SetActive(false);
    }

    private void Update()
    {
        if (!choices || leaving || (SceneLoader.Instance != null && SceneLoader.Instance.IsTransitioning)) return;
        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        Vector2 pointer = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        if ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
            (clicked && RectTransformUtility.RectangleContainsScreenPoint(restartLabel.rectTransform, pointer)))
        {
            leaving = true;
            if (SceneLoader.Instance != null) SceneLoader.Instance.RestartBattle(gameObject.scene.name);
            else SceneManager.LoadSceneAsync(gameObject.scene.name);
        }
        else if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (clicked && RectTransformUtility.RectangleContainsScreenPoint(menuLabel.rectTransform, pointer)))
        {
            leaving = true;
            if (SceneLoader.Instance != null) SceneLoader.Instance.LoadMainMenu();
            else SceneManager.LoadSceneAsync("MainMenu");
        }
    }

    private bool GoTo(string scene)
    {
        if (SceneLoader.Instance != null) return SceneLoader.Instance.EnterFight(scene);
        SceneManager.LoadSceneAsync(scene);
        return true;
    }

    private void OnDisable()
    {
        if (ball != null) ball.RallyEnded -= OnRallyEnded;
        StopAllCoroutines();
    }
}
