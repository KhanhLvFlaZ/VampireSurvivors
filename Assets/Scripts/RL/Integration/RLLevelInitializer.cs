using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// Initializer component for setting up RL integration with level blueprint
    /// Requirement: 1.1 - Consistent RL initialization, 1.5 - Multi-agent coordination
    /// Place on the EntityManager GameObject to enable RL for a level
    /// </summary>
    [RequireComponent(typeof(EntityManager))]
    public class RLLevelInitializer : MonoBehaviour
    {
        [SerializeField] private RLLevelConfiguration rlLevelConfiguration;
        [SerializeField] private bool autoInitializeOnStart = true;

        private EntityManager entityManager;
        private RLEntityIntegration rlIntegration;
        private bool initialized = false;

        public bool Initialized => initialized;
        public RLLevelConfiguration RLConfiguration => rlLevelConfiguration;

        private void Awake()
        {
            entityManager = GetComponent<EntityManager>();

            // Ensure RLEntityIntegration component exists
            if (!TryGetComponent<RLEntityIntegration>(out rlIntegration))
            {
                rlIntegration = gameObject.AddComponent<RLEntityIntegration>();
            }
        }

        private void Start()
        {
            if (autoInitializeOnStart && rlLevelConfiguration != null)
            {
                Initialize(rlLevelConfiguration);
            }
        }

        /// <summary>
        /// Initialize RL system for this level
        /// Requirement: 1.1
        /// </summary>
        public bool Initialize(RLLevelConfiguration config)
        {
            if (initialized)
            {
                Debug.LogWarning("RL Level already initialized");
                return false;
            }

            if (config == null)
            {
                Debug.LogError("RLLevelConfiguration is null");
                return false;
            }

            if (!config.EnableRLForLevel)
            {
                Debug.Log("RL is disabled for this level");
                return false;
            }

            // Validate configuration
            if (!config.Validate(out string errorMsg))
            {
                Debug.LogError($"RLLevelConfiguration validation failed: {errorMsg}");
                return false;
            }

            try
            {
                // Initialize RL integration
                rlIntegration.InitializeRL(config);

                // Register event handlers if available
                RegisterEventHandlers();

                initialized = true;
                Debug.Log($"RL Level initialized successfully with {config.MaxConcurrentRLAgents} max concurrent agents");

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to initialize RL Level: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Register event handlers for RL events
        /// </summary>
        private void RegisterEventHandlers()
        {
            if (rlIntegration != null)
            {
                rlIntegration.OnRLMonsterSpawned += HandleRLMonsterSpawned;
                rlIntegration.OnRLMonsterDespawned += HandleRLMonsterDespawned;
                rlIntegration.OnRLAgentCountChanged += HandleRLAgentCountChanged;
            }
        }

        /// <summary>
        /// Handle RL monster spawned event
        /// </summary>
        private void HandleRLMonsterSpawned(RLMonsterAgent monster)
        {
            if (monster == null)
                return;

            // Log spawn for debugging
            if (rlLevelConfiguration.RecordPerformanceMetrics)
            {
                Debug.Log($"RL Monster spawned: {monster.name} at {monster.transform.position}");
            }
        }

        /// <summary>
        /// Handle RL monster despawned event
        /// </summary>
        private void HandleRLMonsterDespawned(RLMonsterAgent monster)
        {
            if (monster == null)
                return;

            // Log despawn for debugging
            if (rlLevelConfiguration.RecordPerformanceMetrics)
            {
                Debug.Log($"RL Monster despawned: {monster.name}");
            }
        }

        /// <summary>
        /// Handle RL agent count changed event
        /// </summary>
        private void HandleRLAgentCountChanged(int newCount)
        {
            if (rlLevelConfiguration.RecordPerformanceMetrics)
            {
                Debug.Log($"Active RL agents: {newCount}/{rlLevelConfiguration.MaxConcurrentRLAgents}");
            }
        }

        /// <summary>
        /// Unregister event handlers
        /// </summary>
        private void OnDestroy()
        {
            if (rlIntegration != null)
            {
                rlIntegration.OnRLMonsterSpawned -= HandleRLMonsterSpawned;
                rlIntegration.OnRLMonsterDespawned -= HandleRLMonsterDespawned;
                rlIntegration.OnRLAgentCountChanged -= HandleRLAgentCountChanged;
            }
        }

        /// <summary>
        /// Spawn an RL-enabled monster
        /// </summary>
        public RLMonsterAgent SpawnRLMonster(RLMonsterBlueprint blueprint, Vector2 position)
        {
            if (!initialized || rlIntegration == null)
            {
                Debug.LogError("RL system not initialized");
                return null;
            }

            return rlIntegration.SpawnRLMonster(blueprint, position);
        }

        /// <summary>
        /// Spawn RL monster at random position
        /// </summary>
        public RLMonsterAgent SpawnRLMonsterRandomPosition(RLMonsterBlueprint blueprint)
        {
            if (!initialized || rlIntegration == null)
            {
                Debug.LogError("RL system not initialized");
                return null;
            }

            return rlIntegration.SpawnRLMonsterRandomPosition(blueprint);
        }

        /// <summary>
        /// Apply difficulty scaling to all RL agents
        /// </summary>
        public void ApplyDifficultyScaling(DifficultyLevel difficulty)
        {
            if (!initialized || rlIntegration == null)
                return;

            rlIntegration.ApplyDifficultyScaling(difficulty);
        }

        /// <summary>
        /// Get current RL agent count
        /// </summary>
        public int GetRLAgentCount()
        {
            if (!initialized || rlIntegration == null)
                return 0;

            return rlIntegration.CurrentRLAgentCount;
        }

        /// <summary>
        /// Get all active RL monsters
        /// </summary>
        public RLMonsterAgent[] GetActiveRLMonsters()
        {
            if (!initialized || rlIntegration == null)
                return new RLMonsterAgent[0];

            return rlIntegration.GetActiveRLMonsters();
        }
    }
}
