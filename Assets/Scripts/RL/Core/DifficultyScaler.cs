using UnityEngine;
using System;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Dynamic difficulty scaling system that adjusts game difficulty based on player skill
    /// Modulates monster capabilities, spawn rates, and AI behavior complexity
    /// Requirement: 7.2 - Dynamic difficulty scaling based on player skill
    /// </summary>
    public class DifficultyScaler : MonoBehaviour
    {
        [Header("Difficulty Levels")]
        [SerializeField] private DifficultySettings[] difficultyLevels;
        [SerializeField] private DifficultyLevel currentDifficulty = DifficultyLevel.Normal;

        [Header("Scaling Settings")]
        [SerializeField] private float scalingCheckInterval = 5f; // Check every 5 seconds
        [SerializeField] private float scaleUpThreshold = 0.3f; // Scale up if player is dominating
        [SerializeField] private float scaleDownThreshold = 0.7f; // Scale down if player is struggling
        [SerializeField] private int minSamplesForScaling = 10;

        [Header("Performance Metrics")]
        [SerializeField] private float targetPlayerHealthPercent = 0.6f; // Keep player at 60% health
        [SerializeField] private float targetMonsterSurvivalRate = 0.3f; // 30% monsters should survive encounters

        private PlayerStrategyDetector strategyDetector;
        private float lastScalingTime;
        private Queue<DifficultyMetric> metricsHistory;
        private DifficultyLevel targetDifficulty;

        public event Action<DifficultyLevel, DifficultyLevel> OnDifficultyChanged;
        public event Action<float> OnDifficultyMultiplierChanged;

        public DifficultyLevel CurrentDifficulty => currentDifficulty;
        public DifficultySettings CurrentSettings => GetSettingsForLevel(currentDifficulty);
        public float DifficultyMultiplier => GetDifficultyMultiplier(currentDifficulty);

        private void Awake()
        {
            metricsHistory = new Queue<DifficultyMetric>();
            targetDifficulty = currentDifficulty;

            InitializeDefaultDifficulties();
        }

        private void Start()
        {
            strategyDetector = FindFirstObjectByType<PlayerStrategyDetector>();
            if (strategyDetector != null)
            {
                strategyDetector.OnSkillLevelChanged += OnPlayerSkillLevelChanged;
            }
        }

        private void Update()
        {
            if (Time.time - lastScalingTime >= scalingCheckInterval)
            {
                EvaluateDifficulty();
                lastScalingTime = Time.time;
            }
        }

        /// <summary>
        /// Evaluate current difficulty and scale if necessary
        /// Requirement: 7.2
        /// </summary>
        private void EvaluateDifficulty()
        {
            // In a real implementation, this would track game metrics
            // For now, provide the framework

            if (metricsHistory.Count < minSamplesForScaling)
                return;

            // Calculate average metrics
            float avgPlayerHealth = 0f;
            float avgMonsterSurvival = 0f;
            foreach (var metric in metricsHistory)
            {
                avgPlayerHealth += metric.playerHealthPercent;
                avgMonsterSurvival += metric.monsterSurvivalRate;
            }
            avgPlayerHealth /= metricsHistory.Count;
            avgMonsterSurvival /= metricsHistory.Count;

            // Determine if scaling is needed
            if (avgPlayerHealth > targetPlayerHealthPercent + 0.2f &&
                avgMonsterSurvival < targetMonsterSurvivalRate - 0.2f)
            {
                // Player is dominating, increase difficulty
                ScaleDifficultyUp();
            }
            else if (avgPlayerHealth < targetPlayerHealthPercent - 0.2f ||
                     avgMonsterSurvival > targetMonsterSurvivalRate + 0.2f)
            {
                // Player is struggling, decrease difficulty
                ScaleDifficultyDown();
            }
        }

        /// <summary>
        /// Handle player skill level changes
        /// </summary>
        private void OnPlayerSkillLevelChanged(PlayerSkillLevel newSkillLevel)
        {
            DifficultyLevel suggestedDifficulty = GetDifficultyForSkillLevel(newSkillLevel);
            SetDifficulty(suggestedDifficulty);
        }

        /// <summary>
        /// Record a difficulty metric sample
        /// </summary>
        public void RecordMetric(float playerHealthPercent, float monsterSurvivalRate)
        {
            var metric = new DifficultyMetric
            {
                timestamp = Time.time,
                playerHealthPercent = playerHealthPercent,
                monsterSurvivalRate = monsterSurvivalRate
            };

            metricsHistory.Enqueue(metric);

            // Keep limited history
            while (metricsHistory.Count > 20)
            {
                metricsHistory.Dequeue();
            }
        }

        /// <summary>
        /// Scale difficulty up to next level
        /// </summary>
        private void ScaleDifficultyUp()
        {
            DifficultyLevel newLevel = currentDifficulty;

            switch (currentDifficulty)
            {
                case DifficultyLevel.VeryEasy:
                    newLevel = DifficultyLevel.Easy;
                    break;
                case DifficultyLevel.Easy:
                    newLevel = DifficultyLevel.Normal;
                    break;
                case DifficultyLevel.Normal:
                    newLevel = DifficultyLevel.Hard;
                    break;
                case DifficultyLevel.Hard:
                    newLevel = DifficultyLevel.VeryHard;
                    break;
                case DifficultyLevel.VeryHard:
                    newLevel = DifficultyLevel.Insane;
                    break;
                case DifficultyLevel.Insane:
                    // Already at maximum
                    return;
            }

            SetDifficulty(newLevel);
            Debug.Log($"Difficulty scaled up: {currentDifficulty} -> {newLevel}");
        }

        /// <summary>
        /// Scale difficulty down to previous level
        /// </summary>
        private void ScaleDifficultyDown()
        {
            DifficultyLevel newLevel = currentDifficulty;

            switch (currentDifficulty)
            {
                case DifficultyLevel.Insane:
                    newLevel = DifficultyLevel.VeryHard;
                    break;
                case DifficultyLevel.VeryHard:
                    newLevel = DifficultyLevel.Hard;
                    break;
                case DifficultyLevel.Hard:
                    newLevel = DifficultyLevel.Normal;
                    break;
                case DifficultyLevel.Normal:
                    newLevel = DifficultyLevel.Easy;
                    break;
                case DifficultyLevel.Easy:
                    newLevel = DifficultyLevel.VeryEasy;
                    break;
                case DifficultyLevel.VeryEasy:
                    // Already at minimum
                    return;
            }

            SetDifficulty(newLevel);
            Debug.Log($"Difficulty scaled down: {currentDifficulty} -> {newLevel}");
        }

        /// <summary>
        /// Manually set difficulty level
        /// </summary>
        public void SetDifficulty(DifficultyLevel level)
        {
            if (level == currentDifficulty)
                return;

            DifficultyLevel oldLevel = currentDifficulty;
            currentDifficulty = level;

            // Apply settings to relevant systems
            ApplyDifficultySettings();

            OnDifficultyChanged?.Invoke(oldLevel, level);
            OnDifficultyMultiplierChanged?.Invoke(DifficultyMultiplier);
        }

        /// <summary>
        /// Apply difficulty settings to game systems
        /// </summary>
        private void ApplyDifficultySettings()
        {
            var settings = CurrentSettings;

            Debug.Log($"Applying {currentDifficulty} difficulty: " +
                     $"Health Multiplier: {settings.monsterHealthMultiplier}x, " +
                     $"Damage Multiplier: {settings.monsterDamageMultiplier}x, " +
                     $"Speed Multiplier: {settings.monsterSpeedMultiplier}x, " +
                     $"Spawn Rate: {settings.spawnRateMultiplier}x");

            // These would be applied to the actual game systems
            // For now, just log the settings
        }

        /// <summary>
        /// Get difficulty level appropriate for skill level
        /// </summary>
        private DifficultyLevel GetDifficultyForSkillLevel(PlayerSkillLevel skillLevel)
        {
            switch (skillLevel)
            {
                case PlayerSkillLevel.Novice:
                    return DifficultyLevel.VeryEasy;
                case PlayerSkillLevel.Beginner:
                    return DifficultyLevel.Easy;
                case PlayerSkillLevel.Medium:
                    return DifficultyLevel.Normal;
                case PlayerSkillLevel.Advanced:
                    return DifficultyLevel.Hard;
                case PlayerSkillLevel.Expert:
                    return DifficultyLevel.VeryHard;
                default:
                    return DifficultyLevel.Normal;
            }
        }

        /// <summary>
        /// Get settings for difficulty level
        /// </summary>
        private DifficultySettings GetSettingsForLevel(DifficultyLevel level)
        {
            int index = (int)level;
            if (index >= 0 && index < difficultyLevels.Length)
            {
                return difficultyLevels[index];
            }
            return GetDefaultSettings(level);
        }

        /// <summary>
        /// Get difficulty multiplier (0-1 range where 1 is normal)
        /// </summary>
        private float GetDifficultyMultiplier(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.VeryEasy: return 0.4f;
                case DifficultyLevel.Easy: return 0.65f;
                case DifficultyLevel.Normal: return 1.0f;
                case DifficultyLevel.Hard: return 1.4f;
                case DifficultyLevel.VeryHard: return 1.8f;
                case DifficultyLevel.Insane: return 2.5f;
                default: return 1.0f;
            }
        }

        /// <summary>
        /// Initialize default difficulty settings
        /// </summary>
        private void InitializeDefaultDifficulties()
        {
            if (difficultyLevels == null || difficultyLevels.Length == 0)
            {
                difficultyLevels = new DifficultySettings[6];

                difficultyLevels[0] = GetDefaultSettings(DifficultyLevel.VeryEasy);
                difficultyLevels[1] = GetDefaultSettings(DifficultyLevel.Easy);
                difficultyLevels[2] = GetDefaultSettings(DifficultyLevel.Normal);
                difficultyLevels[3] = GetDefaultSettings(DifficultyLevel.Hard);
                difficultyLevels[4] = GetDefaultSettings(DifficultyLevel.VeryHard);
                difficultyLevels[5] = GetDefaultSettings(DifficultyLevel.Insane);
            }
        }

        /// <summary>
        /// Get default settings for a difficulty level
        /// </summary>
        private DifficultySettings GetDefaultSettings(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.VeryEasy:
                    return new DifficultySettings
                    {
                        level = DifficultyLevel.VeryEasy,
                        monsterHealthMultiplier = 0.5f,
                        monsterDamageMultiplier = 0.4f,
                        monsterSpeedMultiplier = 0.6f,
                        spawnRateMultiplier = 0.3f,
                        aiAggressiveness = 0.2f,
                        reinforcementFrequency = 0.5f
                    };

                case DifficultyLevel.Easy:
                    return new DifficultySettings
                    {
                        level = DifficultyLevel.Easy,
                        monsterHealthMultiplier = 0.7f,
                        monsterDamageMultiplier = 0.65f,
                        monsterSpeedMultiplier = 0.8f,
                        spawnRateMultiplier = 0.65f,
                        aiAggressiveness = 0.4f,
                        reinforcementFrequency = 0.7f
                    };

                case DifficultyLevel.Normal:
                    return new DifficultySettings
                    {
                        level = DifficultyLevel.Normal,
                        monsterHealthMultiplier = 1.0f,
                        monsterDamageMultiplier = 1.0f,
                        monsterSpeedMultiplier = 1.0f,
                        spawnRateMultiplier = 1.0f,
                        aiAggressiveness = 0.6f,
                        reinforcementFrequency = 1.0f
                    };

                case DifficultyLevel.Hard:
                    return new DifficultySettings
                    {
                        level = DifficultyLevel.Hard,
                        monsterHealthMultiplier = 1.4f,
                        monsterDamageMultiplier = 1.3f,
                        monsterSpeedMultiplier = 1.2f,
                        spawnRateMultiplier = 1.3f,
                        aiAggressiveness = 0.75f,
                        reinforcementFrequency = 1.3f
                    };

                case DifficultyLevel.VeryHard:
                    return new DifficultySettings
                    {
                        level = DifficultyLevel.VeryHard,
                        monsterHealthMultiplier = 1.8f,
                        monsterDamageMultiplier = 1.7f,
                        monsterSpeedMultiplier = 1.5f,
                        spawnRateMultiplier = 1.7f,
                        aiAggressiveness = 0.85f,
                        reinforcementFrequency = 1.6f
                    };

                case DifficultyLevel.Insane:
                    return new DifficultySettings
                    {
                        level = DifficultyLevel.Insane,
                        monsterHealthMultiplier = 2.5f,
                        monsterDamageMultiplier = 2.3f,
                        monsterSpeedMultiplier = 2.0f,
                        spawnRateMultiplier = 2.2f,
                        aiAggressiveness = 1.0f,
                        reinforcementFrequency = 2.0f
                    };

                default:
                    return GetDefaultSettings(DifficultyLevel.Normal);
            }
        }

        /// <summary>
        /// Get current difficulty as string
        /// </summary>
        public string GetDifficultyName()
        {
            return currentDifficulty.ToString();
        }
    }

    /// <summary>
    /// Difficulty levels in the game
    /// </summary>
    public enum DifficultyLevel
    {
        VeryEasy = 0,
        Easy = 1,
        Normal = 2,
        Hard = 3,
        VeryHard = 4,
        Insane = 5
    }

    /// <summary>
    /// Configuration for a difficulty level
    /// </summary>
    [Serializable]
    public class DifficultySettings
    {
        public DifficultyLevel level;
        public float monsterHealthMultiplier;      // Scales max health
        public float monsterDamageMultiplier;      // Scales damage output
        public float monsterSpeedMultiplier;       // Scales movement/attack speed
        public float spawnRateMultiplier;          // Scales spawn frequency
        public float aiAggressiveness;             // 0-1, controls attack frequency
        public float reinforcementFrequency;       // How often reinforcements arrive
    }

    /// <summary>
    /// Difficulty metric for tracking game balance
    /// </summary>
    [Serializable]
    public class DifficultyMetric
    {
        public float timestamp;
        public float playerHealthPercent;      // Player health as percentage of max
        public float monsterSurvivalRate;      // Percentage of monsters surviving encounter
    }
}
