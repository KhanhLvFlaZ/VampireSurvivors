using UnityEngine;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Validates RL configuration and data for corruption or invalidity
    /// Requirement: 5.1 - Validation for corrupted data and invalid states
    /// </summary>
    public static class RLDataValidator
    {
        /// <summary>
        /// Validate RLMonsterBlueprint
        /// </summary>
        public static ValidationResult ValidateBlueprint(RLMonsterBlueprint blueprint)
        {
            var result = new ValidationResult();

            if (blueprint == null)
            {
                result.AddError("Blueprint is null");
                return result;
            }

            // Check enable RL
            if (!blueprint.EnableRL)
            {
                result.AddInfo("RL is disabled for this blueprint");
            }

            // Check hidden layer sizes
            if (blueprint.HiddenLayerSizes == null || blueprint.HiddenLayerSizes.Length == 0)
            {
                result.AddError("No hidden layer sizes specified");
            }
            else
            {
                foreach (int size in blueprint.HiddenLayerSizes)
                {
                    if (size <= 0)
                    {
                        result.AddError($"Invalid hidden layer size: {size}");
                    }
                    if (size > 1024)
                    {
                        result.AddWarning($"Large hidden layer size may impact performance: {size}");
                    }
                }
            }

            // Check learning parameters
            if (blueprint.ExplorationRate < 0 || blueprint.ExplorationRate > 1)
            {
                result.AddError($"Exploration rate out of range: {blueprint.ExplorationRate}");
            }

            if (blueprint.LearningRate <= 0)
            {
                result.AddError($"Learning rate must be positive: {blueprint.LearningRate}");
            }

            if (blueprint.DiscountFactor < 0 || blueprint.DiscountFactor > 1)
            {
                result.AddError($"Discount factor out of range: {blueprint.DiscountFactor}");
            }

            // Check reward configuration
            if (!blueprint.Validate(out string blueprintError))
            {
                result.AddError($"Blueprint validation failed: {blueprintError}");
            }

            return result;
        }

        /// <summary>
        /// Validate RLLevelConfiguration
        /// </summary>
        public static ValidationResult ValidateLevelConfig(RLLevelConfiguration config)
        {
            var result = new ValidationResult();

            if (config == null)
            {
                result.AddError("Level configuration is null");
                return result;
            }

            if (!config.EnableRLForLevel)
            {
                result.AddInfo("RL is disabled for this level");
                return result;
            }

            // Check max concurrent agents
            if (config.MaxConcurrentRLAgents < 1)
            {
                result.AddError($"Max concurrent agents must be >= 1: {config.MaxConcurrentRLAgents}");
            }

            // Check base difficulty
            if (config.BaseDifficulty < 0.1f || config.BaseDifficulty > 10f)
            {
                result.AddWarning($"Base difficulty outside recommended range: {config.BaseDifficulty}");
            }

            // Check coordination settings
            if (config.EnableCoordinationLearning)
            {
                if (config.CoordinationBonus < 0 || config.CoordinationBonus > 1)
                {
                    result.AddError($"Coordination bonus out of range: {config.CoordinationBonus}");
                }
            }

            // Check training settings
            if (config.TrainingMode)
            {
                if (config.EpisodesPerSession < 1)
                {
                    result.AddError($"Episodes per session must be >= 1: {config.EpisodesPerSession}");
                }

                if (config.TrainingDurationMinutes <= 0)
                {
                    result.AddError($"Training duration must be positive: {config.TrainingDurationMinutes}");
                }
            }

            // Validate using the config's own validation
            if (!config.Validate(out string configError))
            {
                result.AddError($"Configuration validation failed: {configError}");
            }

            return result;
        }

        /// <summary>
        /// Validate RLGameState
        /// </summary>
        public static ValidationResult ValidateGameState(RLGameState state)
        {
            var result = new ValidationResult();

            // RLGameState is a struct, check its fields for validity

            // Check for NaN or infinite values
            if (float.IsNaN(state.monsterHealth) || float.IsInfinity(state.monsterHealth))
            {
                result.AddError($"Invalid monster health value: {state.monsterHealth}");
            }

            if (float.IsNaN(state.timeAlive) || float.IsInfinity(state.timeAlive))
            {
                result.AddError($"Invalid time alive value: {state.timeAlive}");
            }

            // Check position validity
            if (float.IsNaN(state.monsterPosition.x) || float.IsNaN(state.monsterPosition.y))
            {
                result.AddError($"Invalid monster position: {state.monsterPosition}");
            }

            if (float.IsNaN(state.playerPosition.x) || float.IsNaN(state.playerPosition.y))
            {
                result.AddError($"Invalid player position: {state.playerPosition}");
            }

            // Check health is in valid range
            if (state.monsterHealth < 0 || state.monsterHealth > 1)
            {
                result.AddWarning($"Monster health outside [0,1] range: {state.monsterHealth}");
            }

            return result;
        }

        /// <summary>
        /// Validate Experience for training data
        /// </summary>
        public static ValidationResult ValidateExperience(Experience experience)
        {
            var result = new ValidationResult();

            // Validate state
            var stateValidation = ValidateGameState(experience.state);
            if (!stateValidation.IsValid)
            {
                result.MergeErrors(stateValidation);
            }

            // Validate next state
            var nextStateValidation = ValidateGameState(experience.nextState);
            if (!nextStateValidation.IsValid)
            {
                result.AddError("Next state validation failed");
                result.MergeErrors(nextStateValidation);
            }

            // Validate action
            if (experience.action < 0)
            {
                result.AddError($"Invalid action index: {experience.action}");
            }

            // Validate reward
            if (float.IsNaN(experience.reward) || float.IsInfinity(experience.reward))
            {
                result.AddError($"Invalid reward value: {experience.reward}");
            }

            return result;
        }

        /// <summary>
        /// Sanitize game state by removing NaN/Infinity
        /// </summary>
        public static RLGameState SanitizeGameState(RLGameState state)
        {
            var sanitized = new RLGameState
            {
                playerPosition = SanitizeVector2(state.playerPosition),
                playerVelocity = SanitizeVector2(state.playerVelocity),
                playerHealth = SanitizeFloat(state.playerHealth, 100f),
                activeAbilities = state.activeAbilities,
                monsterPosition = SanitizeVector2(state.monsterPosition),
                monsterHealth = SanitizeFloat(state.monsterHealth, 0.5f),
                currentAction = state.currentAction,
                timeSinceLastAction = SanitizeFloat(state.timeSinceLastAction, 0f),
                nearbyMonsters = state.nearbyMonsters ?? new NearbyMonster[5],
                nearbyCollectibles = state.nearbyCollectibles ?? new CollectibleInfo[10],
                timeAlive = SanitizeFloat(state.timeAlive, 0f),
                timeSincePlayerDamage = SanitizeFloat(state.timeSincePlayerDamage, 0f)
            };

            return sanitized;
        }

        /// <summary>
        /// Sanitize float value
        /// </summary>
        private static float SanitizeFloat(float value, float defaultValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return defaultValue;

            // Clamp to reasonable range
            return Mathf.Clamp(value, -1000f, 1000f);
        }

        /// <summary>
        /// Sanitize vector2 value
        /// </summary>
        private static Vector2 SanitizeVector2(Vector2 value)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x))
                value.x = 0;
            if (float.IsNaN(value.y) || float.IsInfinity(value.y))
                value.y = 0;

            // Clamp magnitude
            if (value.magnitude > 1000f)
                value = value.normalized * 1000f;

            return value;
        }
    }

    /// <summary>
    /// Validation result with errors, warnings, and info messages
    /// </summary>
    public class ValidationResult
    {
        private List<string> errors = new List<string>();
        private List<string> warnings = new List<string>();
        private List<string> infos = new List<string>();

        public bool IsValid => errors.Count == 0;
        public int ErrorCount => errors.Count;
        public int WarningCount => warnings.Count;
        public int InfoCount => infos.Count;

        public List<string> Errors => errors;
        public List<string> Warnings => warnings;
        public List<string> Infos => infos;

        /// <summary>
        /// Add error message
        /// </summary>
        public void AddError(string message)
        {
            errors.Add(message);
            Debug.LogError($"[RL Validation Error] {message}");
        }

        /// <summary>
        /// Add warning message
        /// </summary>
        public void AddWarning(string message)
        {
            warnings.Add(message);
            Debug.LogWarning($"[RL Validation Warning] {message}");
        }

        /// <summary>
        /// Add info message
        /// </summary>
        public void AddInfo(string message)
        {
            infos.Add(message);
            Debug.Log($"[RL Validation Info] {message}");
        }

        /// <summary>
        /// Merge another validation result
        /// </summary>
        public void MergeErrors(ValidationResult other)
        {
            if (other == null)
                return;

            errors.AddRange(other.errors);
            warnings.AddRange(other.warnings);
            infos.AddRange(other.infos);
        }

        /// <summary>
        /// Get summary message
        /// </summary>
        public string GetSummary()
        {
            return $"Validation: {ErrorCount} errors, {WarningCount} warnings, {InfoCount} infos";
        }

        /// <summary>
        /// Get detailed report
        /// </summary>
        public string GetDetailedReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine(GetSummary());

            if (errors.Count > 0)
            {
                report.AppendLine("\nErrors:");
                foreach (var error in errors)
                    report.AppendLine($"  - {error}");
            }

            if (warnings.Count > 0)
            {
                report.AppendLine("\nWarnings:");
                foreach (var warning in warnings)
                    report.AppendLine($"  - {warning}");
            }

            if (infos.Count > 0)
            {
                report.AppendLine("\nInfo:");
                foreach (var info in infos)
                    report.AppendLine($"  - {info}");
            }

            return report.ToString();
        }
    }

    /// <summary>
    /// Extension methods for validation
    /// </summary>
    public static class ValidationExtensions
    {
        /// <summary>
        /// Validate and log blueprint
        /// </summary>
        public static bool ValidateAndLog(this RLMonsterBlueprint blueprint, out ValidationResult result)
        {
            result = RLDataValidator.ValidateBlueprint(blueprint);

            if (result.IsValid)
            {
                Debug.Log($"Blueprint validation passed: {result.GetSummary()}");
            }
            else
            {
                Debug.LogError($"Blueprint validation failed:\n{result.GetDetailedReport()}");
            }

            return result.IsValid;
        }

        /// <summary>
        /// Validate and log level config
        /// </summary>
        public static bool ValidateAndLog(this RLLevelConfiguration config, out ValidationResult result)
        {
            result = RLDataValidator.ValidateLevelConfig(config);

            if (result.IsValid)
            {
                Debug.Log($"Level config validation passed: {result.GetSummary()}");
            }
            else
            {
                Debug.LogError($"Level config validation failed:\n{result.GetDetailedReport()}");
            }

            return result.IsValid;
        }
    }
}
