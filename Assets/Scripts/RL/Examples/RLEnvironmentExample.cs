using UnityEngine;
using Vampire;
using Vampire.RL;

namespace Vampire.RL
{
    /// <summary>
    /// Example script demonstrating how to use the RL environment system
    /// This can be used as a reference for integrating RL functionality with monsters
    /// </summary>
    public class RLEnvironmentExample : MonoBehaviour
    {
        [Header("Example Configuration")]
        [SerializeField] private bool runExample = false;
        [SerializeField] private float exampleUpdateInterval = 1f;

        [Header("Dependencies")]
        [SerializeField] private RLSystemIntegration rlSystemIntegration;

        private float lastExampleUpdate;

        private void Start()
        {
            // Find RL system integration if not assigned
            if (rlSystemIntegration == null)
            {
                rlSystemIntegration = FindObjectOfType<RLSystemIntegration>();
            }

            if (rlSystemIntegration == null)
            {
                Debug.LogWarning("RLSystemIntegration not found. Example will not run.");
                runExample = false;
            }
        }

        private void Update()
        {
            if (!runExample || rlSystemIntegration == null) return;

            if (Time.time - lastExampleUpdate >= exampleUpdateInterval)
            {
                RunExample();
                lastExampleUpdate = Time.time;
            }
        }

        /// <summary>
        /// Run the RL environment example
        /// </summary>
        private void RunExample()
        {
            if (!rlSystemIntegration.IsRLSystemReady())
            {
                Debug.Log("RL System is not ready yet");
                return;
            }

            // Example 1: Get environment statistics
            var stats = rlSystemIntegration.GetEnvironmentStats();
            Debug.Log($"Environment Stats - Active: {stats.isActive}, Registered Monsters: {stats.registeredMonsters}");

            // Example 2: Get player behavior pattern
            var behaviorPattern = rlSystemIntegration.GetPlayerBehaviorPattern();
            if (behaviorPattern.IsValid)
            {
                Debug.Log($"Player Behavior - Speed: {behaviorPattern.averageSpeed:F2}, " +
                         $"Direction: {behaviorPattern.preferredDirection}, " +
                         $"Predictability: {behaviorPattern.predictability:F2}");
            }

            // Example 3: Demonstrate state observation for all registered monsters
            var environmentManager = rlSystemIntegration.GetEnvironmentManager();
            if (environmentManager != null)
            {
                // This would typically be called from within a monster's RL agent
                // For demonstration, we'll just show how to get states

                var entityManager = FindObjectOfType<EntityManager>();
                if (entityManager?.LivingMonsters != null)
                {
                    foreach (var monster in entityManager.LivingMonsters)
                    {
                        if (monster != null)
                        {
                            // Get state observation
                            float[] state = rlSystemIntegration.GetMonsterState(monster);

                            // Example of how to interpret the state
                            if (state.Length >= 20)
                            {
                                Vector2 playerPos = new Vector2(state[0], state[1]);
                                Vector2 monsterPos = new Vector2(state[5], state[6]);
                                float distance = Vector2.Distance(playerPos, monsterPos);

                                Debug.Log($"Monster {monster.name} - Distance to player: {distance:F2}");
                            }
                        }
                    }
                }
            }

            // Example 4: Demonstrate reward calculation
            DemonstrateRewardCalculation();

            // Example 5: Show how to update reward configuration
            DemonstrateRewardConfiguration();
        }

        /// <summary>
        /// Demonstrate reward calculation for a hypothetical action
        /// </summary>
        private void DemonstrateRewardCalculation()
        {
            var entityManager = FindObjectOfType<EntityManager>();
            if (entityManager?.LivingMonsters != null && entityManager.LivingMonsters.Count > 0)
            {
                Monster monster = null;
                foreach (var m in entityManager.LivingMonsters)
                {
                    monster = m;
                    break; // Get first monster
                }

                if (monster != null)
                {
                    // Get current state
                    float[] currentState = rlSystemIntegration.GetMonsterState(monster);

                    // Simulate taking an action (e.g., move toward player)
                    int action = 0; // MoveTowardPlayer

                    // Calculate reward for this action
                    float reward = rlSystemIntegration.CalculateReward(monster, action, currentState);

                    Debug.Log($"Reward for monster {monster.name} action {action}: {reward:F3}");
                }
            }
        }

        /// <summary>
        /// Demonstrate how to update reward configuration
        /// </summary>
        private void DemonstrateRewardConfiguration()
        {
            // Create custom reward configuration
            // Note: RewardComponents would normally be instantiated here with custom values
            // Example reward values that would be applied:
            // - damageDealtReward: 15f
            // - survivalReward: 0.2f
            // - coordinationReward: 8f
            // - positioningReward: 3f
            // - deathPenalty: -40f
            // - timeoutPenalty: -12f

            // Note: Custom reward configuration would be applied through the configuration system
            // The reward configuration is typically set up in the Unity Editor or through MonsterRLConfig

            Debug.Log("Reward configuration example demonstrated");
        }

        /// <summary>
        /// Example of how a monster would use the RL environment
        /// This would typically be called from within a monster's RL agent component
        /// </summary>
        public void ExampleMonsterRLUsage(Monster monster)
        {
            if (!rlSystemIntegration.IsRLSystemReady()) return;

            // 1. Register the monster with the RL system (usually done on spawn)
            rlSystemIntegration.RegisterMonster(monster);

            // 2. Get current state observation
            float[] state = rlSystemIntegration.GetMonsterState(monster);

            // 3. Select an action (this would be done by the RL agent/neural network)
            int selectedAction = SelectAction(state); // Placeholder for actual action selection

            // 4. Execute the action (this would be done by the monster's movement/behavior system)
            ExecuteAction(monster, selectedAction);

            // 5. Calculate reward for the action
            float reward = rlSystemIntegration.CalculateReward(monster, selectedAction, state);

            // 6. Check if episode is complete
            bool episodeComplete = rlSystemIntegration.IsEpisodeComplete(monster);

            // 7. Store experience for training (this would be done by the RL agent)
            StoreExperience(state, selectedAction, reward, episodeComplete);

            Debug.Log($"Monster RL Step - Action: {selectedAction}, Reward: {reward:F3}, Episode Complete: {episodeComplete}");
        }

        /// <summary>
        /// Placeholder for action selection logic
        /// In a real implementation, this would use a neural network or other RL algorithm
        /// </summary>
        private int SelectAction(float[] state)
        {
            // Simple random action selection for demonstration
            return Random.Range(0, 8); // Assuming 8 possible actions
        }

        /// <summary>
        /// Placeholder for action execution logic
        /// In a real implementation, this would control the monster's behavior
        /// </summary>
        private void ExecuteAction(Monster monster, int action)
        {
            // This would contain the actual logic to make the monster perform the action
            // For example, moving in a direction, attacking, etc.
            Debug.Log($"Executing action {action} for monster {monster.name}");
        }

        /// <summary>
        /// Placeholder for experience storage logic
        /// In a real implementation, this would store data for training the RL model
        /// </summary>
        private void StoreExperience(float[] state, int action, float reward, bool done)
        {
            // This would store the experience tuple (state, action, reward, next_state, done)
            // in a replay buffer for training the neural network
            Debug.Log($"Storing experience - Action: {action}, Reward: {reward:F3}, Done: {done}");
        }

        // Editor helper methods
#if UNITY_EDITOR
        [ContextMenu("Run Example Once")]
        private void EditorRunExample()
        {
            RunExample();
        }

        [ContextMenu("Toggle Example")]
        private void EditorToggleExample()
        {
            runExample = !runExample;
            Debug.Log($"Example running: {runExample}");
        }
#endif
    }
}