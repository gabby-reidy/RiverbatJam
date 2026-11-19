using UnityEngine;
using UnityEngine.UI;

public class BattleHUD : MonoBehaviour
{
    [SerializeField] private PlayerPaddle player;
    [SerializeField] private EnemyPaddle enemy;
    [SerializeField] private GameBall gameBall;

    [Tooltip("Assign life icons from the outside edge toward the center.")]
    [SerializeField] private GameObject[] playerLifeIcons;
    [Tooltip("Assign life icons from the outside edge toward the center.")]
    [SerializeField] private GameObject[] enemyLifeIcons;
    [SerializeField] private Text launchPrompt;
    [SerializeField] private Text resultLabel;
    [SerializeField] private RectTransform playerFill;
    [SerializeField] private RectTransform enemyFill;
    [SerializeField] private GameObject hudCanvas;

    private void Start()
    {
        if (player == null || enemy == null || gameBall == null || hudCanvas == null ||
            launchPrompt == null || resultLabel == null ||
            playerFill == null || enemyFill == null)
        {
            Debug.LogError("Assign the gameplay and scene UI references on BattleHUD.", this);
            enabled = false;
            return;
        }

        RefreshHUD();
    }

    private void LateUpdate()
    {
        if (hudCanvas != null && player != null && enemy != null && gameBall != null)
        {
            RefreshHUD();
        }
    }

    private void RefreshHUD()
    {
        UpdateLifeIcons(playerLifeIcons, player.CurrentLives);
        UpdateLifeIcons(enemyLifeIcons, enemy.CurrentLives);
        playerFill.anchorMax = new Vector2(Mathf.Clamp01((float)player.CurrentHealth / Mathf.Max(1, player.MaxHealth)), 1);
        enemyFill.anchorMax = new Vector2(Mathf.Clamp01((float)enemy.CurrentHealth / Mathf.Max(1, enemy.MaxHealth)), 1);
        launchPrompt.gameObject.SetActive(gameBall.IsCaughtByPlayer && !gameBall.IsBattleOver);
        bool hasResult = gameBall.IsBattleOver && (player.IsDefeated || enemy.IsDefeated);
        resultLabel.gameObject.SetActive(hasResult);
        if (hasResult)
        {
            resultLabel.text = player.IsDefeated ? "DEFEAT" : "VICTORY";
        }
    }

    private void UpdateLifeIcons(GameObject[] icons, int lives)
    {
        if (icons == null) return;

        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] != null)
            {
                icons[i].SetActive(i < lives);
            }
        }
    }

    private void OnDisable()
    {
        if (hudCanvas != null) hudCanvas.SetActive(false);
    }

    private void OnEnable()
    {
        if (hudCanvas != null) hudCanvas.SetActive(true);
    }
}
