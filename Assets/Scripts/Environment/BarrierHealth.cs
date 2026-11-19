using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BarrierHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 50;
    [SerializeField] private int currentHealth;

    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int CurrentHealth => currentHealth;
    public bool IsBroken => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = MaxHealth;
    }

    public bool Restore()
    {
        bool neededRepair = currentHealth < MaxHealth || !gameObject.activeSelf;
        currentHealth = MaxHealth;
        gameObject.SetActive(true);
        return neededRepair;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsBroken)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Debug.Log($"{name} took {damage} damage. Barrier health: {currentHealth}/{MaxHealth}.", this);

        if (IsBroken)
        {
            gameObject.SetActive(false);
        }
    }
}
