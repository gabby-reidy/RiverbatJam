using UnityEngine;

[CreateAssetMenu(fileName = "NewWizardOpponent",
    menuName = "Wizard Pong/Opponent")]
public class WizardOpponentData : ScriptableObject
{
    [Header("Identity and Art")]
    [SerializeField] private string wizardName = "New Wizard";
    [SerializeField] private Sprite portrait;
    [SerializeField] private Sprite battleSprite;

    [Header("Battle Settings")]
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0.1f)] private float paddleMoveSpeed = 5f;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip castSound;
    [SerializeField] private AudioClip reflectSound;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip defeatSound;

    public string WizardName => wizardName;
    public Sprite Portrait => portrait;
    public Sprite BattleSprite => battleSprite;
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public float PaddleMoveSpeed => Mathf.Max(0.1f, paddleMoveSpeed);
    public AudioClip CastSound => castSound;
    public AudioClip ReflectSound => reflectSound;
    public AudioClip HurtSound => hurtSound;
    public AudioClip DefeatSound => defeatSound;
}