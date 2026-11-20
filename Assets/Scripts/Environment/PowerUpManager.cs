using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public enum PowerUpType { None, Fire, Ice, Earth, Wind }

public class PowerUpManager : MonoBehaviour
{
    [SerializeField] private GameBall gameBall;
    [SerializeField] private PlayerPaddle player;
    [SerializeField] private EnemyPaddle enemy;
    [SerializeField, Min(0.1f)] private float enemyUseInterval = 2f;
    private float enemyUseTimer;
    [SerializeField] private EarthRestoreEffect earthRestoreEffect;
    [Header("Power-up Effects")]
    [SerializeField, Min(0.01f)] private float iceDuration = 1.5f;
    [Tooltip("Total vertical velocity change spread over the Wind duration.")]
    [SerializeField, Min(0.1f)] private float windVerticalImpulse = 4f;
    [SerializeField, Min(0.01f)] private float windDuration = 0.75f;
    [SerializeField] private BarrierHealth[] playerBarriers;
    [SerializeField] private BarrierHealth[] enemyBarriers;
    [SerializeField, Min(0.1f)] private float spawnInterval = 3f;
    [SerializeField] private BoxCollider2D[] pickupSpots;
    [SerializeField] private Image playerSlot;
    [SerializeField] private Image enemySlot;
    [SerializeField] private Color fireColor = new Color(1f, 0.3f, 0.1f);
    [SerializeField] private Color iceColor = new Color(0.3f, 0.8f, 1f);
    [SerializeField] private Color earthColor = new Color(0.55f, 0.35f, 0.15f);
    [SerializeField] private Color windColor = new Color(0.6f, 1f, 0.65f);

    [SerializeField] private PowerUpType playerInventory;
    [SerializeField] private PowerUpType enemyInventory;
    [SerializeField] private AudioClip pickupSound;
    [Header("Power-up Artwork")]
    [SerializeField] private Sprite fireSprite;
    [SerializeField] private Sprite iceSprite;
    [SerializeField] private Sprite earthSprite;
    [SerializeField] private Sprite windSprite;
    private Sprite[] placeholderSprites;
    private Vector2[] placeholderSizes;
    private SpriteDrawMode[] placeholderDrawModes;
    private PowerUpType[] fieldTypes;
    private SpriteRenderer[] pickupRenderers;
    private float spawnTimer;

    private void Awake()
    {
        if (gameBall == null || player == null || enemy == null || pickupSpots == null || pickupSpots.Length != 3 ||
            playerSlot == null || enemySlot == null)
        {
            Debug.LogError("Assign the ball, three pickup spots, and one UI slot per fighter.", this);
            enabled = false;
            return;
        }

        fieldTypes = new PowerUpType[pickupSpots.Length];
        pickupRenderers = new SpriteRenderer[pickupSpots.Length];
        placeholderSprites = new Sprite[pickupSpots.Length];
        placeholderSizes = new Vector2[pickupSpots.Length];
        placeholderDrawModes = new SpriteDrawMode[pickupSpots.Length];
        for (int i = 0; i < pickupSpots.Length; i++)
        {
            if (pickupSpots[i] == null || (pickupRenderers[i] = pickupSpots[i].GetComponent<SpriteRenderer>()) == null)
            {
                Debug.LogError("Each pickup spot needs a collider and SpriteRenderer.", this);
                enabled = false;
                return;
            }
        }
        for (int i = 0; i < pickupSpots.Length; i++)
        {
            placeholderSprites[i] = pickupRenderers[i].sprite;
            placeholderSizes[i] = pickupRenderers[i].size;
            placeholderDrawModes[i] = pickupRenderers[i].drawMode;
        }
        playerInventory = PowerUpType.None;
        enemyInventory = PowerUpType.None;
        ClearField();
        RefreshInventory();
    }

    private void OnEnable()
    {
        if (gameBall != null) gameBall.LifeLost += ClearField;
    }

    private void OnDisable()
    {
        if (gameBall != null) gameBall.LifeLost -= ClearField;
    }

    private void LateUpdate()
    {
        if (gameBall.IsBattleOver)
        {
            player.CancelDash();
            enemy.CancelDash();
            return;
        }
        if (Time.timeScale <= 0f) return;

        // Read after PlayerPaddle.Update so direction includes this frame's input.
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && !gameBall.IsCaughtByPlayer)
        {
            TryUse(true);
        }

        enemyUseTimer -= Time.deltaTime;
        if (enemyUseTimer <= 0f && ShouldEnemyUse())
        {
            if (TryUse(false))
            {
                enemyUseTimer = Mathf.Max(0.1f, enemyUseInterval);
            }
        }
    }

    public bool TryUse(bool usePlayerInventory)
    {
        if (!isActiveAndEnabled || gameBall.IsBattleOver || Time.timeScale <= 0f)
            return false;

        PowerUpType inventory = usePlayerInventory ? playerInventory : enemyInventory;
        bool activated = false;
        switch (inventory)
        {
            case PowerUpType.Fire:
                activated = usePlayerInventory ? player.TryDash() : enemy.TryDash();
                break;
            case PowerUpType.Ice:
                activated = gameBall.TryFreeze(iceDuration);
                break;
            case PowerUpType.Earth:
                activated = RestoreBarriers(usePlayerInventory ? playerBarriers : enemyBarriers);
                break;
            case PowerUpType.Wind:
                activated = gameBall.TryWind(windVerticalImpulse, windDuration);
                break;
        }
        // Failed attempts keep the held power-up.
        if (!activated) return false;
        if (usePlayerInventory) playerInventory = PowerUpType.None;
        else enemyInventory = PowerUpType.None;
        RefreshInventory();
        return true;
    }

    public PowerUpType EnemyInventory => enemyInventory;

    public void GetEnemyAimTargets(WizardOpponentData data, System.Collections.Generic.List<Vector2> targets,
        float bottom, float top)
    {
        targets.Clear();
        if (data.SeekPowerUps && (enemyInventory == PowerUpType.None || data.ReplaceHeldPowerUp))
        {
            for (int i = 0; i < fieldTypes.Length; i++)
                if (fieldTypes[i] != PowerUpType.None) targets.Add(pickupSpots[i].bounds.center);
        }
        if (data.TargetWeakBarriers && playerBarriers != null)
            foreach (BarrierHealth barrier in playerBarriers)
                if (barrier != null) targets.Add(barrier.transform.position);
        if (data.AimAwayFromPlayer)
        {
            targets.Add(new Vector2(player.transform.position.x, bottom + 0.3f));
            targets.Add(new Vector2(player.transform.position.x, top - 0.3f));
        }
    }

    public float ScoreEnemyReturn(WizardOpponentData data, Vector2 origin, Vector2 direction,
        float bottom, float top, float radius)
    {
        float score = 0f;
        bool protectHeld = enemyInventory != PowerUpType.None && !data.ReplaceHeldPowerUp;
        for (int i = 0; i < fieldTypes.Length; i++)
        {
            if (fieldTypes[i] == PowerUpType.None || pickupSpots[i].bounds.center.x >= origin.x) continue;
            Bounds bounds = pickupSpots[i].bounds;
            float y = ProjectReturnY(origin, direction, bounds.center.x, bottom, top);
            if (Mathf.Abs(y - bounds.center.y) > bounds.extents.y + radius) continue;
            if (protectHeld) score -= 20f;
            else if (data.SeekPowerUps)
                score += data.PreferredPowerUp != PowerUpType.None && fieldTypes[i] == data.PreferredPowerUp ? 8f : 4f;
        }
        if (data.TargetWeakBarriers && playerBarriers != null)
        {
            float bestWeakness = 0f;
            foreach (BarrierHealth barrier in playerBarriers)
            {
                if (barrier == null || barrier.transform.position.x >= origin.x) continue;
                float y = ProjectReturnY(origin, direction, barrier.transform.position.x, bottom, top);
                // Transform size remains available after a broken block is disabled.
                float halfHeight = Mathf.Abs(barrier.transform.lossyScale.y) * 0.5f;
                if (Mathf.Abs(y - barrier.transform.position.y) <= halfHeight + radius)
                    bestWeakness = Mathf.Max(bestWeakness, 1f - (float)barrier.CurrentHealth / barrier.MaxHealth);
            }
            score += bestWeakness * 2f;
        }
        if (data.AimAwayFromPlayer && player.transform.position.x < origin.x)
        {
            float y = ProjectReturnY(origin, direction, player.transform.position.x, bottom, top);
            score += Mathf.Abs(y - player.transform.position.y) / Mathf.Max(0.1f, top - bottom);
        }
        return score;
    }

    private float ProjectReturnY(Vector2 origin, Vector2 direction, float x, float bottom, float top)
    {
        float projected = origin.y + (x - origin.x) * direction.y / direction.x;
        return bottom + Mathf.PingPong(projected - bottom, Mathf.Max(0.01f, top - bottom));
    }

    private bool ShouldEnemyUse()
    {
        switch (enemyInventory)
        {
            case PowerUpType.Earth:
                if (enemyBarriers == null) return false;
                foreach (BarrierHealth barrier in enemyBarriers)
                {
                    if (barrier != null && (barrier.CurrentHealth < barrier.MaxHealth || !barrier.gameObject.activeSelf))
                        return true;
                }
                return false;
            case PowerUpType.Fire:
                return enemy.ShouldUseFire();
            case PowerUpType.Ice:
                return enemy.NeedsDefensivePowerUp(true);
            case PowerUpType.Wind:
                return gameBall.IsInPlay && gameBall.LastHitSide == 1 && gameBall.TravelVelocity.x < 0f;
            default:
                return false;
        }
    }

    private bool RestoreBarriers(BarrierHealth[] barriers)
    {
        if (barriers == null) return false;
        bool repaired = false;
        foreach (BarrierHealth barrier in barriers)
        {
            if (barrier == null) continue;
            bool wasBroken = barrier.IsBroken || !barrier.gameObject.activeSelf;
            if (!barrier.Restore()) continue;
            repaired = true;
            if (earthRestoreEffect != null) earthRestoreEffect.Play(barrier, wasBroken);
        }
        return repaired;
    }

    private void Update()
    {
        if (!gameBall.IsInPlay)
        {
            spawnTimer = spawnInterval;
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        spawnTimer = Mathf.Max(0.1f, spawnInterval);

        // Reservoir sampling gives each empty spot the same chance.
        int chosen = -1;
        int emptyCount = 0;
        for (int i = 0; i < fieldTypes.Length; i++)
        {
            if (fieldTypes[i] == PowerUpType.None && Random.Range(0, ++emptyCount) == 0) chosen = i;
        }
        if (chosen < 0) return;

        fieldTypes[chosen] = (PowerUpType)Random.Range(1, 5);
        SetPickupArtwork(chosen);
        pickupRenderers[chosen].enabled = true;
        pickupSpots[chosen].enabled = true;
    }

    public void Collect(Collider2D hit)
    {
        if (!enabled || !gameBall.IsInPlay) return;
        for (int i = 0; i < pickupSpots.Length; i++)
        {
            if (pickupSpots[i] != hit || fieldTypes[i] == PowerUpType.None) continue;
            PowerUpType type = fieldTypes[i];
            if (AudioManager.instance != null) AudioManager.instance.PlaySFX(pickupSound);
            fieldTypes[i] = PowerUpType.None;
            pickupRenderers[i].enabled = false;
            pickupSpots[i].enabled = false;

            // A neutral serve consumes the pickup without awarding it.
            if (gameBall.LastHitSide >= 0)
            {
                if (gameBall.LastHitSide == 0) playerInventory = type;
                else
                {
                    enemyInventory = type;
                    enemyUseTimer = Mathf.Max(enemyUseTimer, 0.25f);
                }
                RefreshInventory();
            }
            return;
        }
    }

    private void ClearField()
    {
        player.CancelDash();
        enemy.CancelDash();
        enemyUseTimer = Mathf.Max(0.1f, enemyUseInterval);
        spawnTimer = Mathf.Max(0.1f, spawnInterval);
        for (int i = 0; i < fieldTypes.Length; i++)
        {
            fieldTypes[i] = PowerUpType.None;
            pickupRenderers[i].enabled = false;
            pickupSpots[i].enabled = false;
        }
    }

    private void RefreshInventory()
    {
        SetSlotArtwork(playerSlot, playerInventory);
        SetSlotArtwork(enemySlot, enemyInventory);
    }

    private Sprite GetSprite(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Fire: return fireSprite;
            case PowerUpType.Ice: return iceSprite;
            case PowerUpType.Earth: return earthSprite;
            case PowerUpType.Wind: return windSprite;
            default: return null;
        }
    }

    private void SetSlotArtwork(Image slot, PowerUpType type)
    {
        Sprite sprite = GetSprite(type);
        slot.enabled = type != PowerUpType.None;
        slot.sprite = sprite;
        slot.preserveAspect = true;
        slot.color = sprite != null ? Color.white : GetColor(type);
    }

    private void SetPickupArtwork(int index)
    {
        Sprite sprite = GetSprite(fieldTypes[index]);
        SpriteRenderer renderer = pickupRenderers[index];
        renderer.sprite = sprite != null ? sprite : placeholderSprites[index];
        renderer.color = sprite != null ? Color.white : GetColor(fieldTypes[index]);
        if (sprite == null)
        {
            renderer.drawMode = placeholderDrawModes[index];
            renderer.size = placeholderSizes[index];
            return;
        }
        // Spellbooks import at one unit tall; use the existing pickup transform scale.
        renderer.drawMode = SpriteDrawMode.Simple;
    }

    private Color GetColor(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Fire: return fireColor;
            case PowerUpType.Ice: return iceColor;
            case PowerUpType.Earth: return earthColor;
            case PowerUpType.Wind: return windColor;
            default: return Color.white;
        }
    }
}
