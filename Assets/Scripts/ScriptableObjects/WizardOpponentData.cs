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

    [Header("AI Defense")]
    [SerializeField, Min(0.02f)] private float reactionInterval = 0.2f;
    [SerializeField, Min(0f)] private float predictionError = 0.25f;

    [Header("AI Tactics")]
    [SerializeField, Range(0f, 1f)] private float aggression = 0.65f;
    [SerializeField] private float preferredX = 6.5f;
    [SerializeField, Min(0.1f)] private float retreatSpeedThreshold = 12f;
    [SerializeField, Min(1)] private int retreatDamageThreshold = 30;

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
    public float ReactionInterval => Mathf.Max(0.02f, reactionInterval);
    public float PredictionError => Mathf.Max(0f, predictionError);
    public float Aggression => Mathf.Clamp01(aggression);
    public float PreferredX => preferredX;
    public float RetreatSpeedThreshold => Mathf.Max(0.1f, retreatSpeedThreshold);
    public int RetreatDamageThreshold => Mathf.Max(1, retreatDamageThreshold);
    public AudioClip CastSound => castSound;
    public AudioClip ReflectSound => reflectSound;
    public AudioClip HurtSound => hurtSound;
    public AudioClip DefeatSound => defeatSound;
}
