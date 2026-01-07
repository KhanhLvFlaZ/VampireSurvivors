using UnityEngine;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// Converts spawned Monsters into RLMonsters and registers them with the RLSystem.
    /// Works via spawn-event hook when available, and falls back to periodic scene scans.
    /// </summary>
    public class MonsterRLConverter : MonoBehaviour
    {
        [Header("Conversion Options")]
        [SerializeField] private bool subscribeToSpawnEvent = true;
        [SerializeField] private bool pollExistingMonsters = true;
        [SerializeField] private float pollIntervalSeconds = 2f;

        private RLSystem rlSystem;

        private void Awake()
        {
            rlSystem = FindFirstObjectByType<RLSystem>();
            if (rlSystem == null)
            {
                Debug.LogError("[MonsterRLConverter] RLSystem not found in scene. Run 'Vampire RL → Setup Custom RL Training'.");
            }
        }

        private void OnEnable()
        {
            if (subscribeToSpawnEvent)
            {
                // If your game has a global spawn event, hook here.
                // Uncomment and point to the real event:
                // MonsterManager.OnMonsterSpawned += OnMonsterSpawned;
            }

            if (pollExistingMonsters)
            {
                InvokeRepeating(nameof(ConvertExistingMonsters), 1f, Mathf.Max(0.25f, pollIntervalSeconds));
            }
        }

        private void OnDisable()
        {
            if (subscribeToSpawnEvent)
            {
                // MonsterManager.OnMonsterSpawned -= OnMonsterSpawned;
            }
            CancelInvoke();
        }

        // Example event handler if a spawn event exists
        private void OnMonsterSpawned(Monster monster)
        {
            TryConvert(monster);
        }

        // Fallback: scan the scene periodically
        private void ConvertExistingMonsters()
        {
            var monsters = FindObjectsOfType<Monster>();
            foreach (var monster in monsters)
            {
                TryConvert(monster);
            }
        }

        private void TryConvert(Monster monster)
        {
            if (monster == null) return;

            // Already RL-enabled
            if (monster.GetComponent<RLMonsterAgent>() != null)
                return;

            // Add RLMonsterAgent component
            var rlAgent = monster.gameObject.AddComponent<RLMonsterAgent>();

            // Register with RLSystem for training
            if (rlSystem != null)
            {
                Debug.Log($"[MonsterRLConverter] Converted to RLMonsterAgent: {monster.name}");
            }
            else
            {
                Debug.LogWarning($"[MonsterRLConverter] RLSystem missing, converted {monster.name}.");
            }
        }
    }
}
