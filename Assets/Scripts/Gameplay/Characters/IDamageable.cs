namespace Vampire.Gameplay.Characters
{
    /// <summary>
    /// Interface for objects that can take damage
    /// Used by networking system to integrate with character/enemy health
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount);
        void Heal(float amount);

        float CurrentHealth { get; }
        bool IsAlive { get; }
    }
}
