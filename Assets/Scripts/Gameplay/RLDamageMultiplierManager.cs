using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// Global damage multiplier manager for tuning monster damage (e.g., RL training integration).
    /// Lives in the base Vampire assembly to avoid cross-assembly cycles.
    /// </summary>
    public class RLDamageMultiplierManager : MonoBehaviour
    {
        public static RLDamageMultiplierManager Instance { get; private set; }

        [SerializeField] private float damageMultiplier = 0.5f; // 0.5 = 50% damage
        [SerializeField] private bool enableDebugLog = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (enableDebugLog)
            {
                Debug.Log($"[RLDamageMultiplierManager] Initialized - damage multiplier: {damageMultiplier}x");
            }
        }

        public float GetDamageMultiplier() => damageMultiplier;

        public void SetDamageMultiplier(float newMultiplier)
        {
            damageMultiplier = Mathf.Clamp01(newMultiplier);
            if (enableDebugLog)
            {
                Debug.Log($"[RLDamageMultiplierManager] Damage multiplier changed to {damageMultiplier}x");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
