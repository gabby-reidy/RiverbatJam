using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BarrierHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 50;
    [SerializeField] private int currentHealth;

    [SerializeField] private Sprite intactSprite;
    [SerializeField] private Sprite damagedSprite;
    private SpriteRenderer spriteRenderer;

    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int CurrentHealth => currentHealth;
    public bool IsBroken => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = MaxHealth;
        RefreshSprite();
    }

    public bool Restore()
    {
        bool neededRepair = currentHealth < MaxHealth || !gameObject.activeSelf;
        currentHealth = MaxHealth;
        RefreshSprite();
        gameObject.SetActive(true);
        return neededRepair;
    }

    private void RefreshSprite()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;
        Sprite sprite = currentHealth < MaxHealth && currentHealth > 0 ? damagedSprite : intactSprite;
        if (sprite != null) spriteRenderer.sprite = sprite;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsBroken)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        RefreshSprite();
        Debug.Log($"{name} took {damage} damage. Barrier health: {currentHealth}/{MaxHealth}.", this);

        if (IsBroken)
        {
            gameObject.SetActive(false);
        }
    }
}
