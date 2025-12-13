using UnityEngine;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// Integration component that connects the RL system with the existing game architecture
    /// This component should be added to the main game scene to enable RL functionality
    /// </summary>
    public class RLSystemIntegration : MonoBehaviour
    {
        [Header("RL System Configuration")]
        [SerializeField] private bool enableRLSystem = true;
        [SerializeField] private bool autoInitialize = true;

        [Header("RL Components")]
        [SerializeField] private RLEnvironmentManager environmentManager;

        [Header("Game System References")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private Character playerCharacter;

        // System state
        private bool isInitialized = false;

        // Events
        public System.Action OnRLSystemInitialized;
        public System.Action OnRLSystemShutdown;

        private void Awake()
        {
            // Create RL environment manager if not assigned
            if (environmentManager == null)
            {
                GameObject envManagerGO = new GameObject("RL Environment Manager");
                envManagerGO.transform.SetParent(transform);
                environmentManager = envManagerGO.AddComponent<RLEnvironmentManager>();
            }
        }

        private void Start()
        {
            if (autoInitialize && enableRLSystem)
            {
                InitializeRLSystem();
            }
        }

        /// <summary>
        /// Initialize the RL system with game dependencies
        /// </summary>
        public void InitializeRLSystem()
        {
            if (isInitialized)
            {
                Debug.LogWarning("RL System is already initialized");
                return;
            }

            if (!enableRLSystem)
            {
                Debug.Log("RL System is disabled");
                return;
            }

            // Find game systems if not assigned
            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }

            // Validate dependencies
            if (entityManager == null)
            {
                Debug.LogError("EntityManager not found. RL System cannot be initialized.");
                return;
            }

            if (playerCharacter == null)
            {
                Debug.LogError("Player Character not found. RL System cannot be initialized.");
                return;
            }

            if (environmentManager == null)
            {
                Debug.LogError("RL Environment Manager not found. RL System cannot be initialized.");
                return;
            }

            // Initialize the environment manager
            environmentManager.Initialize(entityManager, playerCharacter);

            // Subscribe to game events for automatic RL integration
            SubscribeToGameEvents();

            isInitialized = true;

            OnRLSystemInitialized?.Invoke();

            Debug.Log("RL System initialized successfully");
        }

        /// <summary>
        /// Shutdown the RL system
        /// </summary>
        public void ShutdownRLSystem()
        {
            if (!isInitialized) return;

            // Unsubscribe from game events
            UnsubscribeFromGameEvents();

            // Reset environment
            if (environmentManager != null)
            {
                environmentManager.ResetEnvironment();
                environmentManager.SetEnvironmentEnabled(false);
            }

            isInitialized = false;

            OnRLSystemShutdown?.Invoke();

            Debug.Log("RL System shutdown");
        }

        /// <summary>
        /// Subscribe to game events for automatic RL integration
        /// </summary>
        private void SubscribeToGameEvents()
        {
            // Subscribe to player events if available
            if (playerCharacter != null)
            {
                playerCharacter.OnDeath.AddListener(OnPlayerDeath);
            }

            // Note: Additional event subscriptions would go here
            // For example, monster spawn/death events from EntityManager
        }

        /// <summary>
        /// Unsubscribe from game events
        /// </summary>
        private void UnsubscribeFromGameEvents()
        {
            if (playerCharacter != null)
            {
                playerCharacter.OnDeath.RemoveListener(OnPlayerDeath);
            }
        }

        /// <summary>
        /// Handle player death event
        /// </summary>
        private void OnPlayerDeath()
        {
            if (environmentManager != null)
            {
                environmentManager.ResetEnvironment();
            }
        }

        /// <summary>
        /// Get state observation for a specific monster
        /// </summary>
        public float[] GetMonsterState(Monster monster)
        {
            if (!isInitialized || environmentManager == null)
                return new float[20];

            return environmentManager.GetMonsterState(monster);
        }

        /// <summary>
        /// Calculate reward for a monster action
        /// </summary>
        public float CalculateReward(Monster monster, int action, float[] previousState)
        {
            if (!isInitialized || environmentManager == null)
                return 0f;

            return environmentManager.CalculateReward(monster, action, previousState);
        }

        /// <summary>
        /// Check if episode is complete for a monster
        /// </summary>
        public bool IsEpisodeComplete(Monster monster)
        {
            if (!isInitialized || environmentManager == null)
                return true;

            return environmentManager.IsEpisodeComplete(monster);
        }

        /// <summary>
        /// Register a monster with the RL system
        /// </summary>
        public void RegisterMonster(Monster monster)
        {
            if (!isInitialized || environmentManager == null) return;

            environmentManager.RegisterMonster(monster);
        }

        /// <summary>
        /// Unregister a monster from the RL system
        /// </summary>
        public void UnregisterMonster(Monster monster)
        {
            if (!isInitialized || environmentManager == null) return;

            environmentManager.UnregisterMonster(monster);
        }

        /// <summary>
        /// Record monster attack for reward calculation
        /// </summary>
        public void RecordMonsterAttack(Monster monster)
        {
            if (!isInitialized || environmentManager == null) return;

            environmentManager.RecordMonsterAttack(monster);
        }

        /// <summary>
        /// Get player behavior analysis
        /// </summary>
        public PlayerBehaviorPattern GetPlayerBehaviorPattern()
        {
            if (!isInitialized || environmentManager == null)
                return new PlayerBehaviorPattern();

            return environmentManager.GetPlayerBehaviorPattern();
        }

        /// <summary>
        /// Set behavior type for reward calculation
        /// </summary>
        public void SetBehaviorType(BehaviorType behaviorType)
        {
            if (!isInitialized || environmentManager == null) return;

            environmentManager.SetBehaviorType(behaviorType);
        }

        /// <summary>
        /// Get environment statistics
        /// </summary>
        public EnvironmentStats GetEnvironmentStats()
        {
            if (!isInitialized || environmentManager == null)
                return new EnvironmentStats();

            return environmentManager.GetEnvironmentStats();
        }

        /// <summary>
        /// Enable or disable the RL system
        /// </summary>
        public void SetRLSystemEnabled(bool enabled)
        {
            enableRLSystem = enabled;

            if (environmentManager != null)
            {
                environmentManager.SetEnvironmentEnabled(enabled);
            }

            if (!enabled && isInitialized)
            {
                ShutdownRLSystem();
            }
            else if (enabled && !isInitialized)
            {
                InitializeRLSystem();
            }
        }

        /// <summary>
        /// Check if the RL system is ready and operational
        /// </summary>
        public bool IsRLSystemReady()
        {
            return isInitialized && enableRLSystem && environmentManager != null && environmentManager.IsEnvironmentReady();
        }

        /// <summary>
        /// Get the RL environment manager
        /// </summary>
        public RLEnvironmentManager GetEnvironmentManager()
        {
            return environmentManager;
        }

        private void OnDestroy()
        {
            ShutdownRLSystem();
        }

        private void OnValidate()
        {
            // Validate configuration in editor
            if (Application.isPlaying && isInitialized && !enableRLSystem)
            {
                ShutdownRLSystem();
            }
        }

        // Editor helper methods
#if UNITY_EDITOR
        [ContextMenu("Initialize RL System")]
        private void EditorInitializeRLSystem()
        {
            InitializeRLSystem();
        }

        [ContextMenu("Shutdown RL System")]
        private void EditorShutdownRLSystem()
        {
            ShutdownRLSystem();
        }

        [ContextMenu("Reset Environment")]
        private void EditorResetEnvironment()
        {
            if (environmentManager != null)
            {
                environmentManager.ResetEnvironment();
            }
        }
#endif
    }
}