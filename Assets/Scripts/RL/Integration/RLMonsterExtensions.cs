using UnityEngine;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Extension methods for RLMonster to support RL integration
    /// Requirement: 1.1 - RL monster agent initialization, 1.5 - Multi-agent coordination
    /// </summary>
    public static class RLMonsterExtensions
    {
        private static Dictionary<RLMonster, RLMonsterRuntimeData> runtimeData = new Dictionary<RLMonster, RLMonsterRuntimeData>();

        /// <summary>
        /// Initialize RLMonster with blueprint configuration
        /// Requirement: 1.1
        /// </summary>
        public static void InitializeRL(this RLMonster monster, RLMonsterBlueprint blueprint)
        {
            if (monster == null || blueprint == null)
                return;

            // Ensure runtime data exists
            if (!runtimeData.ContainsKey(monster))
            {
                runtimeData[monster] = new RLMonsterRuntimeData();
            }

            var data = runtimeData[monster];
            data.blueprint = blueprint;
            data.isTraining = blueprint.EnableTraining;
            data.baseExplorationRate = blueprint.ExplorationRate;
            data.baseLearningRate = blueprint.LearningRate;
            data.baseDiscountFactor = blueprint.DiscountFactor;
            data.currentDifficulty = DifficultyLevel.Normal;
            data.rewardConfig = blueprint.GetRewardConfiguration();
            data.networkConfig = blueprint.GetNetworkConfiguration();
            data.adaptiveConfig = blueprint.GetAdaptiveLearningConfiguration();

            // Apply training settings if enabled
            if (blueprint.EnableTraining)
            {
                monster.IsTraining = true;
            }

            // Load pre-trained model if specified
            if (blueprint.UsePreTrainedModel && !string.IsNullOrEmpty(blueprint.PreTrainedModelPath))
            {
                monster.LoadBehaviorProfile(blueprint.PreTrainedModelPath);
            }
        }

        /// <summary>
        /// Set difficulty level for this monster
        /// Adjusts behavior parameters based on difficulty
        /// </summary>
        public static void SetDifficultyLevel(this RLMonster monster, DifficultyLevel difficulty)
        {
            if (monster == null || !runtimeData.TryGetValue(monster, out var data))
                return;

            data.currentDifficulty = difficulty;

            // Apply difficulty scaling to RL parameters
            float difficultyMultiplier = GetDifficultyMultiplier(difficulty);

            // Scale exploration rate (higher difficulty = more strategic)
            float adjustedExploration = data.baseExplorationRate * (1.0f / difficultyMultiplier);

            // Scale health based on difficulty
            float healthMultiplier = GetHealthMultiplier(difficulty);
            if (monster.TryGetComponent<Monster>(out var monsterComp))
            {
                // Would need a way to adjust health dynamically
            }

            // Store difficulty-adjusted parameters
            data.difficultyMultiplier = difficultyMultiplier;
        }

        /// <summary>
        /// Get difficulty multiplier for stats
        /// </summary>
        private static float GetDifficultyMultiplier(DifficultyLevel difficulty)
        {
            return difficulty switch
            {
                DifficultyLevel.VeryEasy => 0.4f,
                DifficultyLevel.Easy => 0.6f,
                DifficultyLevel.Normal => 1.0f,
                DifficultyLevel.Hard => 1.5f,
                DifficultyLevel.VeryHard => 2.0f,
                DifficultyLevel.Insane => 2.5f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get health multiplier for difficulty
        /// </summary>
        private static float GetHealthMultiplier(DifficultyLevel difficulty)
        {
            return difficulty switch
            {
                DifficultyLevel.VeryEasy => 0.5f,
                DifficultyLevel.Easy => 0.75f,
                DifficultyLevel.Normal => 1.0f,
                DifficultyLevel.Hard => 1.3f,
                DifficultyLevel.VeryHard => 1.7f,
                DifficultyLevel.Insane => 2.0f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get current RL blueprint for monster
        /// </summary>
        public static RLMonsterBlueprint GetRLBlueprint(this RLMonster monster)
        {
            if (monster != null && runtimeData.TryGetValue(monster, out var data))
            {
                return data.blueprint;
            }
            return null;
        }

        /// <summary>
        /// Get current difficulty level
        /// </summary>
        public static DifficultyLevel GetCurrentDifficulty(this RLMonster monster)
        {
            if (monster != null && runtimeData.TryGetValue(monster, out var data))
            {
                return data.currentDifficulty;
            }
            return DifficultyLevel.Normal;
        }

        /// <summary>
        /// Apply adaptive learning configuration
        /// </summary>
        public static void ApplyAdaptiveLearning(this RLMonster monster, AdaptiveLearningConfiguration config)
        {
            if (monster == null || config == null)
                return;

            if (!runtimeData.TryGetValue(monster, out var data))
                return;

            data.adaptiveConfig = config;
        }

        /// <summary>
        /// Cleanup runtime data when monster is destroyed
        /// </summary>
        public static void CleanupRL(this RLMonster monster)
        {
            if (monster != null)
            {
                runtimeData.Remove(monster);
            }
        }
    }

    /// <summary>
    /// Runtime data for RL monster instances
    /// </summary>
    public class RLMonsterRuntimeData
    {
        public RLMonsterBlueprint blueprint;
        public bool isTraining;
        public float baseExplorationRate;
        public float baseLearningRate;
        public float baseDiscountFactor;
        public float difficultyMultiplier = 1.0f;
        public DifficultyLevel currentDifficulty = DifficultyLevel.Normal;
        public Dictionary<int, RewardComponentConfig> rewardConfig; // Use int keys (RewardType indices) instead
        public NetworkConfiguration networkConfig;
        public AdaptiveLearningConfiguration adaptiveConfig;
        public float timeSpawned = 0f;
        public int actionsExecuted = 0;
        public float cumulativeReward = 0f;
    }

    /// <summary>
    /// Difficulty level enumeration
    /// </summary>
    // DifficultyLevel is already defined in the existing RL system, so we reference it from there
}
