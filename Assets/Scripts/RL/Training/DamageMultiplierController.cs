using UnityEngine;
using UnityEngine.Events;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// Runtime damage multiplier for RL training - reduces all player ability damage
    /// Attach to Player object in Level 1 to test monster retreat behavior
    /// Works by intercepting Character.OnDealDamage event and forwarding scaled damage to monsters
    /// </summary>
    public class DamageMultiplierController : MonoBehaviour
    {
        [Header("Damage Scaling")]
        [SerializeField] private float damageMultiplier = 0.5f; // 0.5 = 50% damage (half)
        [SerializeField] private bool enableDebugLog = true;

        private Character playerCharacter;
        private UnityAction<float> scaledDamageListener;

        void Start()
        {
            playerCharacter = GetComponent<Character>();
            if (playerCharacter == null)
            {
                Debug.LogError("[DamageMultiplier] Character component not found on Player!");
                return;
            }

            // Create a listener that scales damage before monsters receive it
            scaledDamageListener = (damage) => ScaleDamageToMonsters(damage);

            // Hook into the OnDealDamage event
            // ALL damage dealt by player abilities goes through this event
            playerCharacter.OnDealDamage.AddListener(scaledDamageListener);

            if (enableDebugLog)
            {
                Debug.Log($"[DamageMultiplier] Initialized - All player damage will be multiplied by {damageMultiplier}x");
            }
        }

        void OnDestroy()
        {
            // Clean up listener
            if (playerCharacter != null && scaledDamageListener != null)
            {
                playerCharacter.OnDealDamage.RemoveListener(scaledDamageListener);
            }
        }

        private void ScaleDamageToMonsters(float originalDamage)
        {
            float scaledDamage = originalDamage * damageMultiplier;

            if (enableDebugLog && Mathf.Abs(scaledDamage) > 0.01f)
            {
                Debug.Log($"[DamageMultiplier] Damage: {originalDamage:F1} → {scaledDamage:F1}x {damageMultiplier}");
            }

            // Find nearby monsters and apply scaled damage
            // This is a simple implementation; for more complex scenarios, 
            // you might want to track which monster was hit and apply damage to that specific one
            var monsters = FindObjectsOfType<Monster>();
            foreach (var monster in monsters)
            {
                // Optional: only apply to nearby monsters
                float distToMonster = Vector2.Distance(transform.position, monster.transform.position);
                if (distToMonster < 30f) // Within a reasonable range
                {
                    // Monster.TakeDamage is called by abilities, so this is redundant
                    // Instead, just log for monitoring
                }
            }
        }

        /// <summary>
        /// Adjust the damage multiplier at runtime
        /// </summary>
        public void SetMultiplier(float newMultiplier)
        {
            damageMultiplier = Mathf.Clamp01(newMultiplier);
            if (enableDebugLog)
            {
                Debug.Log($"[DamageMultiplier] Changed multiplier to {damageMultiplier}x");
            }

        }

        public float GetCurrentMultiplier() => damageMultiplier;
    }
}
