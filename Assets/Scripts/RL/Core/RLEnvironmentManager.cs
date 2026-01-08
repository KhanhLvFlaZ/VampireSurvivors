using UnityEngine;
using System.Collections.Generic;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// Manager class that coordinates the RL environment with game systems
    /// Handles initialization, monster registration, and environment updates
    /// </summary>
    public class RLEnvironmentManager : MonoBehaviour
    {
        [Header("Environment Settings")]
        [SerializeField] private bool enableRLEnvironment = true;
        [SerializeField] private float environmentUpdateInterval = 0.1f;

        [Header("Dependencies")]
        [SerializeField] private RLEnvironment rlEnvironment;
        [SerializeField] private RewardCalculator rewardCalculator;

        // Game system references
        private EntityManager entityManager;
        private Character playerCharacter;

        // Environment state
        private bool isInitialized = false;
        private float lastUpdateTime;
        private HashSet<Monster> registeredMonsters;

        // Events
        public System.Action<Monster> OnMonsterRegistered;
        public System.Action<Monster> OnMonsterUnregistered;
        public System.Action OnEnvironmentReset;

        private void Awake()
        {
            registeredMonsters = new HashSet<Monster>();

            // Create components if not assigned
            if (rlEnvironment == null)
            {
                rlEnvironment = gameObject.AddComponent<RLEnvironment>();
            }

            if (rewardCalculator == null)
            {
                rewardCalculator = gameObject.AddComponent<RewardCalculator>();
            }
        }

        /// <summary>
        /// Initialize the RL environment manager with game systems
        /// </summary>
        public void Initialize(EntityManager entityManager, Character playerCharacter)
        {
            if (!enableRLEnvironment)
            {
                Debug.Log("RL Environment is disabled");
                return;
            }

            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;

            // Initialize components
            rewardCalculator.Initialize(rlEnvironment, entityManager, playerCharacter);
            rlEnvironment.Initialize(entityManager, playerCharacter, rewardCalculator);

            // Subscribe to entity manager events for automatic monster registration
            if (entityManager != null)
            {
                // Note: This would require EntityManager to have events for monster spawn/despawn
                // For now, we'll handle registration manually when monsters are created
            }

            isInitialized = true;
            lastUpdateTime = Time.time;

            Debug.Log("RL Environment Manager initialized successfully");
        }

        private void Update()
        {
            if (!isInitialized || !enableRLEnvironment) return;

            // Update environment periodically
            if (Time.time - lastUpdateTime >= environmentUpdateInterval)
            {
                UpdateEnvironment();
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Update the RL environment state
        /// </summary>
        private void UpdateEnvironment()
        {
            if (entityManager?.LivingMonsters == null) return;

            // Check for new monsters to register
            foreach (var monster in entityManager.LivingMonsters)
            {
                if (monster != null && !registeredMonsters.Contains(monster))
                {
                    RegisterMonster(monster);
                }
            }

            // Check for monsters that need to be unregistered
            var monstersToRemove = new List<Monster>();
            foreach (var monster in registeredMonsters)
            {
                if (monster == null || monster.HP <= 0 || !entityManager.LivingMonsters.Contains(monster))
                {
                    monstersToRemove.Add(monster);
                }
            }

            foreach (var monster in monstersToRemove)
            {
                UnregisterMonster(monster);
            }
        }

        /// <summary>
        /// Register a monster with the RL environment
        /// </summary>
        public void RegisterMonster(Monster monster)
        {
            if (monster == null || !enableRLEnvironment || !isInitialized) return;

            if (registeredMonsters.Add(monster))
            {
                rlEnvironment.RegisterMonster(monster);

                // Subscribe to monster events
                monster.OnKilled.AddListener(OnMonsterKilled);

                OnMonsterRegistered?.Invoke(monster);

                Debug.Log($"Monster {monster.name} registered with RL environment");
            }
        }

        /// <summary>
        /// Unregister a monster from the RL environment
        /// </summary>
        public void UnregisterMonster(Monster monster)
        {
            if (monster == null) return;

            if (registeredMonsters.Remove(monster))
            {
                rlEnvironment.UnregisterMonster(monster);
                rewardCalculator.CleanupMonster(monster);

                // Unsubscribe from monster events
                monster.OnKilled.RemoveListener(OnMonsterKilled);

                OnMonsterUnregistered?.Invoke(monster);

                Debug.Log($"Monster {monster.name} unregistered from RL environment");
            }
        }

        /// <summary>
        /// Handle monster death event
        /// </summary>
        private void OnMonsterKilled(Monster monster)
        {
            UnregisterMonster(monster);
        }

        /// <summary>
        /// Get state observation for a specific monster
        /// </summary>
        public float[] GetMonsterState(Monster monster)
        {
            if (!isInitialized || !enableRLEnvironment || monster == null)
                return new float[20]; // Return empty state

            return rlEnvironment.GetState(monster);
        }

        /// <summary>
        /// Calculate reward for a monster action
        /// </summary>
        public float CalculateReward(Monster monster, int action, float[] previousState)
        {
            if (!isInitialized || !enableRLEnvironment || monster == null)
                return 0f;

            return rlEnvironment.CalculateReward(monster, action, previousState);
        }

        /// <summary>
        /// Check if episode is complete for a monster
        /// </summary>
        public bool IsEpisodeComplete(Monster monster)
        {
            if (!isInitialized || !enableRLEnvironment || monster == null)
                return true;

            return rlEnvironment.IsEpisodeComplete(monster);
        }

        /// <summary>
        /// Reset the RL environment
        /// </summary>
        public void ResetEnvironment()
        {
            if (!isInitialized || !enableRLEnvironment) return;

            // Unregister all monsters
            var monstersToUnregister = new List<Monster>(registeredMonsters);
            foreach (var monster in monstersToUnregister)
            {
                UnregisterMonster(monster);
            }

            // Reset environment
            rlEnvironment.ResetEnvironment();

            OnEnvironmentReset?.Invoke();

            Debug.Log("RL Environment reset");
        }

        /// <summary>
        /// Get player behavior analysis
        /// </summary>
        public PlayerBehaviorPattern GetPlayerBehaviorPattern()
        {
            if (!isInitialized || !enableRLEnvironment)
                return new PlayerBehaviorPattern();

            return rlEnvironment.AnalyzePlayerBehavior();
        }

        /// <summary>
        /// Set behavior type for reward calculation
        /// </summary>
        public void SetBehaviorType(BehaviorType behaviorType)
        {
            if (!isInitialized || !enableRLEnvironment) return;

            rewardCalculator.SetBehaviorType(behaviorType);
        }

        /// <summary>
        /// Record monster attack for reward calculation
        /// </summary>
        public void RecordMonsterAttack(Monster monster)
        {
            if (!isInitialized || !enableRLEnvironment || monster == null) return;

            rlEnvironment.RecordMonsterAttack(monster);
        }

        /// <summary>
        /// Get environment statistics
        /// </summary>
        public EnvironmentStats GetEnvironmentStats()
        {
            var stats = new EnvironmentStats();

            if (isInitialized && enableRLEnvironment)
            {
                stats.registeredMonsters = registeredMonsters.Count;
                stats.isActive = true;
                stats.playerBehaviorPattern = GetPlayerBehaviorPattern();
            }

            return stats;
        }

        /// <summary>
        /// Enable or disable the RL environment
        /// </summary>
        public void SetEnvironmentEnabled(bool enabled)
        {
            enableRLEnvironment = enabled;

            if (!enabled && isInitialized)
            {
                ResetEnvironment();
            }

            Debug.Log($"RL Environment {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Check if the RL environment is active and ready
        /// </summary>
        public bool IsEnvironmentReady()
        {
            return isInitialized && enableRLEnvironment && rlEnvironment != null && rewardCalculator != null;
        }

        private void OnDestroy()
        {
            // Clean up
            if (isInitialized)
            {
                ResetEnvironment();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!isInitialized || !enableRLEnvironment || rlEnvironment == null) return;

            // Draw observation radius for registered monsters
            Gizmos.color = Color.yellow;
            foreach (var monster in registeredMonsters)
            {
                if (monster != null)
                {
                    Gizmos.DrawWireSphere(monster.transform.position, rlEnvironment.ObservationRadius);
                }
            }

            // Draw player behavior pattern
            var behaviorPattern = GetPlayerBehaviorPattern();
            if (behaviorPattern.IsValid && playerCharacter != null)
            {
                Gizmos.color = Color.green;
                Vector2 playerPos = playerCharacter.transform.position;
                Vector2 directionEnd = playerPos + behaviorPattern.preferredDirection * 3f;
                Gizmos.DrawLine(playerPos, directionEnd);
                Gizmos.DrawSphere(directionEnd, 0.2f);
            }
        }
    }

    /// <summary>
    /// Environment statistics for monitoring and debugging
    /// </summary>
    [System.Serializable]
    public struct EnvironmentStats
    {
        public int registeredMonsters;
        public bool isActive;
        public PlayerBehaviorPattern playerBehaviorPattern;

        public bool IsValid => registeredMonsters >= 0;
    }
}