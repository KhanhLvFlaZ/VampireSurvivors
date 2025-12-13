using UnityEngine;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Factory for creating and managing RewardCalculator instances
    /// Handles configuration loading and caching
    /// </summary>
    public static class RewardCalculatorFactory
    {
        private static Dictionary<MonsterType, RewardCalculator> calculatorCache = new Dictionary<MonsterType, RewardCalculator>();
        private static Dictionary<MonsterType, RewardConfig> configCache = new Dictionary<MonsterType, RewardConfig>();

        /// <summary>
        /// Create or get cached RewardCalculator for a monster type
        /// </summary>
        public static RewardCalculator GetRewardCalculator(MonsterType monsterType, MonsterRLConfig monsterConfig = null)
        {
            if (calculatorCache.TryGetValue(monsterType, out RewardCalculator cachedCalculator))
            {
                return cachedCalculator;
            }

            // Load or create reward config
            RewardConfig rewardConfig = GetRewardConfig(monsterType);

            // Use provided monster config or create default
            if (monsterConfig == null)
            {
                monsterConfig = MonsterRLConfig.CreateDefault(monsterType);
            }

            // Note: RewardCalculator is a MonoBehaviour and must be found in the scene
            // or added to a GameObject through the Unity Editor
            // For now, return null if not cached
            RewardCalculator calculator = null;

            // Cache for future use if found
            if (calculator != null)
            {
                calculatorCache[monsterType] = calculator;
            }

            return calculator;
        }

        /// <summary>
        /// Get reward configuration for a monster type
        /// </summary>
        public static RewardConfig GetRewardConfig(MonsterType monsterType)
        {
            if (configCache.TryGetValue(monsterType, out RewardConfig cachedConfig))
            {
                return cachedConfig;
            }

            // Try to load from Resources first
            RewardConfig loadedConfig = LoadRewardConfigFromResources(monsterType);

            if (loadedConfig == null)
            {
                // Create default config if none found
                loadedConfig = RewardConfig.GetConfigForMonsterType(monsterType);
            }

            // Cache the config
            configCache[monsterType] = loadedConfig;

            return loadedConfig;
        }

        /// <summary>
        /// Load reward configuration from Resources folder
        /// </summary>
        private static RewardConfig LoadRewardConfigFromResources(MonsterType monsterType)
        {
            string resourcePath = $"RL/RewardConfigs/{monsterType}RewardConfig";
            return Resources.Load<RewardConfig>(resourcePath);
        }

        /// <summary>
        /// Create RewardCalculator with custom configuration
        /// </summary>
        /// <summary>
        /// Clear all cached calculators and configs
        /// </summary>
        public static void ClearCache()
        {
            calculatorCache.Clear();
            configCache.Clear();
        }

        /// <summary>
        /// Update reward configuration for a monster type
        /// </summary>
        public static void UpdateRewardConfig(MonsterType monsterType, RewardConfig newConfig)
        {
            if (newConfig == null)
                throw new System.ArgumentNullException(nameof(newConfig));

            // Update config cache
            configCache[monsterType] = newConfig;

            // Remove calculator from cache to force recreation with new config
            calculatorCache.Remove(monsterType);
        }

        /// <summary>
        /// Apply difficulty scaling to all cached reward configurations
        /// </summary>
        public static void ApplyDifficultyScaling(float difficultyMultiplier)
        {
            foreach (var config in configCache.Values)
            {
                config.ApplyDifficultyScaling(difficultyMultiplier);
            }

            // Clear calculator cache to force recreation with scaled configs
            calculatorCache.Clear();
        }

        /// <summary>
        /// Get all available monster types with reward configurations
        /// </summary>
        public static MonsterType[] GetConfiguredMonsterTypes()
        {
            return new MonsterType[]
            {
                MonsterType.Melee,
                MonsterType.Ranged,
                MonsterType.Throwing,
                MonsterType.Boomerang,
                MonsterType.Boss
            };
        }

        /// <summary>
        /// Validate all reward configurations
        /// </summary>
        public static bool ValidateAllConfigurations()
        {
            foreach (MonsterType monsterType in GetConfiguredMonsterTypes())
            {
                RewardConfig config = GetRewardConfig(monsterType);
                if (!config.IsValid())
                {
                    Debug.LogError($"Invalid reward configuration for monster type: {monsterType}");
                    return false;
                }
            }
            return true;
        }
    }
}