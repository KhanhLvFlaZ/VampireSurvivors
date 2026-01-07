using UnityEngine;
using Vampire.RL;

namespace Vampire
{
    /// <summary>
    /// Helper script to quickly integrate RL into existing game
    /// Handles initialization and spawning without visualization overhead
    /// </summary>
    public class RLIntegrationHelper : MonoBehaviour
    {
        [Header("RL System")]
        [SerializeField] private bool enableRLSystem = true;
        [SerializeField] private int maxRLAgents = 10;
        [SerializeField] private float rlMonsterSpawnRatio = 0.3f; // 30% of monsters are RL

        [Header("Training Config")]
        [SerializeField] private bool trainingMode = true;
        [SerializeField] private float explorationRate = 0.2f;
        [SerializeField] private int updateFrequency = 100;

        [Header("Dependencies")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private Character playerCharacter;
        [SerializeField] private RLMonsterBlueprint defaultRLBlueprint;

        // Runtime references
        private RLSystem rlSystem;
        private RLEnvironment rlEnvironment;
        private RLEntityIntegration rlEntityIntegration;

        void Start()
        {
            if (enableRLSystem)
            {
                InitializeRLSystem();
            }
        }

        private void InitializeRLSystem()
        {
            // Create or find RLSystem
            rlSystem = FindFirstObjectByType<RLSystem>();
            if (rlSystem == null)
            {
                GameObject rlSystemGO = new GameObject("RLSystem");
                rlSystem = rlSystemGO.AddComponent<RLSystem>();
                rlEnvironment = rlSystemGO.AddComponent<RLEnvironment>();
                rlEntityIntegration = rlSystemGO.AddComponent<RLEntityIntegration>();
                Debug.Log("[RL Helper] Created RLSystem");
            }
            else
            {
                rlEnvironment = rlSystem.GetComponent<RLEnvironment>();
                rlEntityIntegration = rlSystem.GetComponent<RLEntityIntegration>();
            }

            // Initialize environment
            if (rlEnvironment != null && entityManager != null && playerCharacter != null)
            {
                rlEnvironment.Initialize(entityManager, playerCharacter, null);
                Debug.Log("[RL Helper] Environment initialized");
            }

            // Initialize entity integration
            if (rlEntityIntegration != null && defaultRLBlueprint != null)
            {
                // Configure from blueprint
                Debug.Log("[RL Helper] Integration ready");
            }
        }

        /// <summary>
        /// Spawn an RL monster agent at a specific position
        /// </summary>
        public RLMonsterAgent SpawnRLMonster(Vector2 position)
        {
            if (!enableRLSystem || rlEntityIntegration == null || defaultRLBlueprint == null)
            {
                Debug.LogWarning("[RL Helper] Cannot spawn RL monster - system not initialized");
                return null;
            }

            // Note: ExplorationRate is configured in the blueprint asset itself
            // It cannot be changed at runtime as it's a read-only property

            // Spawn via integration
            RLMonsterAgent monster = rlEntityIntegration.SpawnRLMonster(defaultRLBlueprint, position);

            if (monster != null)
            {
                Debug.Log($"[RL Helper] Spawned RL monster agent at {position} (trainingMode flag={trainingMode})");
            }

            return monster;
        }

        /// <summary>
        /// Should this spawn be an RL monster?
        /// </summary>
        public bool ShouldSpawnRLMonster()
        {
            if (!enableRLSystem)
                return false;

            // Count current RL monsters
            var rlMonsters = FindObjectsByType<RLMonsterAgent>(FindObjectsSortMode.None);
            if (rlMonsters.Length >= maxRLAgents)
                return false;

            // Random chance based on ratio
            return Random.value < rlMonsterSpawnRatio;
        }

        /// <summary>
        /// Get training statistics
        /// </summary>
        public string GetTrainingStats()
        {
            var rlMonsters = FindObjectsByType<RLMonsterAgent>(FindObjectsSortMode.None);

            if (rlMonsters.Length == 0)
                return "No RL monsters active";

            // ML-Agents handles metrics externally; display simple count and mode flag.
            return $"RL Agents: {rlMonsters.Length}\nTraining Mode Flag: {trainingMode}";
        }

        /// <summary>
        /// Save all trained models
        /// </summary>
        public void SaveAllModels()
        {
            var rlMonsters = FindObjectsByType<RLMonsterAgent>(FindObjectsSortMode.None);
            Debug.Log("[RL Helper] Runtime model saving is managed by ML-Agents training. Use training outputs (ONNX) instead.");
            Debug.Log($"[RL Helper] Active agents: {rlMonsters.Length}");
        }

        /// <summary>
        /// Switch all agents to inference mode
        /// </summary>
        public void SwitchToInferenceMode()
        {
            trainingMode = false;
            Debug.Log("[RL Helper] Set trainingMode flag = false (configure BehaviorParameters for inference)");
        }

        /// <summary>
        /// Switch all agents to training mode
        /// </summary>
        public void SwitchToTrainingMode()
        {
            trainingMode = true;
            Debug.Log("[RL Helper] Set trainingMode flag = true (configure BehaviorParameters for training)");
        }

        // Editor buttons
#if UNITY_EDITOR
        [ContextMenu("Spawn Test RL Monster")]
        void SpawnTestMonster()
        {
            if (playerCharacter != null)
            {
                Vector2 spawnPos = (Vector2)playerCharacter.transform.position + Vector2.right * 5f;
                SpawnRLMonster(spawnPos);
            }
        }

        [ContextMenu("Print Training Stats")]
        void PrintStats()
        {
            Debug.Log(GetTrainingStats());
        }

        [ContextMenu("Save All Models")]
        void EditorSaveModels()
        {
            SaveAllModels();
        }

        [ContextMenu("Switch to Inference")]
        void EditorSwitchInference()
        {
            SwitchToInferenceMode();
        }

        [ContextMenu("Switch to Training")]
        void EditorSwitchTraining()
        {
            SwitchToTrainingMode();
        }
#endif

        void OnGUI()
        {
            if (!enableRLSystem)
                return;

            // Simple debug UI
            GUILayout.BeginArea(new Rect(10, 100, 300, 200));
            GUILayout.Box("RL System Status");
            GUILayout.Label(GetTrainingStats());

            if (GUILayout.Button("Save Models"))
            {
                SaveAllModels();
            }

            if (trainingMode)
            {
                if (GUILayout.Button("Switch to Inference"))
                {
                    SwitchToInferenceMode();
                }
            }
            else
            {
                if (GUILayout.Button("Switch to Training"))
                {
                    SwitchToTrainingMode();
                }
            }

            GUILayout.EndArea();
        }
    }
}
