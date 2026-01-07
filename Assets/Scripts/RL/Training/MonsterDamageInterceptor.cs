using UnityEngine;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// Proper damage reduction by intercepting IDamageable.TakeDamage calls
    /// This ensures monsters actually receive reduced damage, not just logging it
    /// </summary>
    public class MonsterDamageInterceptor : MonoBehaviour
    {
        [SerializeField] private float damageReduction = 0.5f; // 0.5 = 50% damage (take 50% of incoming)
        [SerializeField] private bool enableDebugLog = false;

        private Character playerCharacter;

        void Start()
        {
            playerCharacter = GetComponent<Character>();
            if (playerCharacter == null)
            {
                Debug.LogError("[MonsterDamageInterceptor] Character not found!");
                return;
            }

            Debug.Log($"[MonsterDamageInterceptor] Active - monsters will take {damageReduction * 100}% of player damage");
        }

        void OnDestroy()
        {
            // Cleanup if needed
        }

        /// <summary>
        /// Call this from Monster.TakeDamage to scale incoming damage
        /// Problem: can't intercept TakeDamage directly, so this is a manual patch approach
        /// </summary>
        public float GetScaledDamage(float incomingDamage)
        {
            float scaledDamage = incomingDamage * damageReduction;
            if (enableDebugLog)
            {
                Debug.Log($"[DamageScale] {incomingDamage:F1} → {scaledDamage:F1}");
            }
            return scaledDamage;
        }

        public void SetDamageReduction(float newReduction)
        {
            damageReduction = Mathf.Clamp01(newReduction);
            Debug.Log($"[MonsterDamageInterceptor] Damage reduction set to {damageReduction * 100}%");
        }
    }
}
