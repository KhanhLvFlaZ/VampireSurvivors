using UnityEngine;
using Vampire;
using System.Collections.Generic;
using System.Linq;

namespace Vampire.RL
{
    /// <summary>
    /// Configuration for RL-specific level settings
    /// Attached to LevelBlueprint to enable RL features for that level
    /// Requirement: 1.1 - Consistent initialization across levels, 1.5 - Multi-agent coordination
    /// </summary>
    [CreateAssetMenu(fileName = "RLLevelConfiguration", menuName = "Blueprints/RL Level Configuration", order = 3)]
    public class RLLevelConfiguration : ScriptableObject
    {
        [Header("RL Monster Substitutions")]
        [SerializeField] private RLMonsterSubstitution[] monsterSubstitutions = new RLMonsterSubstitution[0];

        [Header("Level-Wide RL Settings")]
        [SerializeField] private bool enableRLForLevel = true;
        [SerializeField] private float baseDifficulty = 1.0f;
        [SerializeField] private DifficultyScalingMode difficultyMode = DifficultyScalingMode.Adaptive;

        [Header("Multi-Agent Coordination")]
        [SerializeField] private int maxConcurrentRLAgents = 10; // Requirement: 1.5
        [SerializeField] private float coordinationBonus = 0.1f;
        [SerializeField] private bool enableCoordinationLearning = true;
        [SerializeField] private int coordinationStrategyIndex = 1; // Basic coordination

        [Header("Training Mode")]
        [SerializeField] private bool trainingMode = false;
        [SerializeField] private int episodesPerSession = 100;
        [SerializeField] private float trainingDurationMinutes = 30f;

        [Header("Adaptive Learning")]
        [SerializeField] private bool enableAdaptiveProfile = true;
        [SerializeField] private string profileName = "Default";
        [SerializeField] private bool autoSaveProfile = true;
        [SerializeField] private float autoSaveInterval = 60f;

        [Header("Performance & Optimization")]
        [SerializeField] private RLPerformanceMode performanceMode = RLPerformanceMode.Balanced;
        [SerializeField] private bool enableModelQuantization = false;
        [SerializeField] private int maxModelUpdatesPerFrame = 5;

        [Header("Visualization")]
        [SerializeField] private RLVisualizationLevel visualizationLevel = RLVisualizationLevel.Basic;
        [SerializeField] private bool showCoordinationNetwork = false;
        [SerializeField] private bool recordPerformanceMetrics = true;

        [Header("References")]
        [SerializeField] private GameObject adaptiveLearningManagerPrefab;

        // Cache
        private Dictionary<string, RLMonsterBlueprint> substitutionCache;
        private bool cacheValid = false;

        public bool EnableRLForLevel => enableRLForLevel;
        public float BaseDifficulty => baseDifficulty;
        public DifficultyScalingMode DifficultyMode => difficultyMode;
        public int MaxConcurrentRLAgents => maxConcurrentRLAgents;
        public float CoordinationBonus => coordinationBonus;
        public bool EnableCoordinationLearning => enableCoordinationLearning;
        public int CoordinationStrategyIndex => coordinationStrategyIndex;

        public bool TrainingMode => trainingMode;
        public int EpisodesPerSession => episodesPerSession;
        public float TrainingDurationMinutes => trainingDurationMinutes;

        public bool EnableAdaptiveProfile => enableAdaptiveProfile;
        public string ProfileName => profileName;
        public bool AutoSaveProfile => autoSaveProfile;
        public float AutoSaveInterval => autoSaveInterval;

        public RLPerformanceMode PerformanceMode => performanceMode;
        public bool EnableModelQuantization => enableModelQuantization;
        public int MaxModelUpdatesPerFrame => maxModelUpdatesPerFrame;

        public RLVisualizationLevel VisualizationLevel => visualizationLevel;
        public bool ShowCoordinationNetwork => showCoordinationNetwork;
        public bool RecordPerformanceMetrics => recordPerformanceMetrics;

        public GameObject AdaptiveLearningManagerPrefab => adaptiveLearningManagerPrefab;

        /// <summary>
        /// Get RL blueprint for a given monster blueprint
        /// Returns the RL variant if configured, otherwise returns null
        /// Requirement: 1.1 - Consistent RL agent initialization
        /// </summary>
        public RLMonsterBlueprint GetRLMonsterBlueprint(MonsterBlueprint baseBlueprint)
        {
            if (!enableRLForLevel || baseBlueprint == null)
                return null;

            if (!cacheValid)
                RebuildSubstitutionCache();

            string baseKey = baseBlueprint.name;
            if (substitutionCache.TryGetValue(baseKey, out var rlBlueprint))
                return rlBlueprint;

            return null;
        }

        /// <summary>
        /// Check if a monster should use RL
        /// Requirement: 1.1
        /// </summary>
        public bool ShouldUseRL(MonsterBlueprint baseBlueprint)
        {
            if (!enableRLForLevel)
                return false;

            return GetRLMonsterBlueprint(baseBlueprint) != null;
        }

        /// <summary>
        /// Get all RL monster blueprints configured for this level
        /// </summary>
        public RLMonsterBlueprint[] GetAllRLMonsterBlueprints()
        {
            return monsterSubstitutions
                .Where(s => s.rlBlueprint != null)
                .Select(s => s.rlBlueprint)
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// Get coordination configuration
        /// Requirement: 1.5 - Multi-agent coordination
        /// </summary>
        public CoordinationConfiguration GetCoordinationConfiguration()
        {
            return new CoordinationConfiguration
            {
                maxConcurrentAgents = maxConcurrentRLAgents,
                coordinationBonus = coordinationBonus,
                enableLearning = enableCoordinationLearning,
                strategyIndex = coordinationStrategyIndex
            };
        }

        /// <summary>
        /// Get training configuration
        /// </summary>
        public TrainingSessionConfiguration GetTrainingConfiguration()
        {
            return new TrainingSessionConfiguration
            {
                trainingMode = trainingMode,
                episodesPerSession = episodesPerSession,
                durationMinutes = trainingDurationMinutes,
                baseDifficulty = baseDifficulty
            };
        }

        /// <summary>
        /// Get performance configuration
        /// </summary>
        public PerformanceConfiguration GetPerformanceConfiguration()
        {
            return new PerformanceConfiguration
            {
                performanceMode = performanceMode,
                enableQuantization = enableModelQuantization,
                maxUpdatesPerFrame = maxModelUpdatesPerFrame
            };
        }

        /// <summary>
        /// Validate configuration
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            errorMessage = "";

            if (!enableRLForLevel)
                return true; // Valid if RL disabled for level

            if (maxConcurrentRLAgents < 1)
            {
                errorMessage = "Max concurrent RL agents must be at least 1";
                return false;
            }

            if (baseDifficulty < 0.1f || baseDifficulty > 10f)
            {
                errorMessage = "Base difficulty should be between 0.1 and 10.0";
                return false;
            }

            if (coordinationBonus < 0f || coordinationBonus > 1f)
            {
                errorMessage = "Coordination bonus should be between 0 and 1";
                return false;
            }

            if (trainingMode && episodesPerSession < 1)
            {
                errorMessage = "Episodes per session must be at least 1";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Rebuild substitution cache
        /// </summary>
        private void RebuildSubstitutionCache()
        {
            substitutionCache = new Dictionary<string, RLMonsterBlueprint>();

            foreach (var substitution in monsterSubstitutions)
            {
                if (substitution.baseBlueprint != null && substitution.rlBlueprint != null)
                {
                    string key = substitution.baseBlueprint.name;
                    substitutionCache[key] = substitution.rlBlueprint;
                }
            }

            cacheValid = true;
        }

        /// <summary>
        /// Mark cache as invalid when changes occur
        /// </summary>
        private void OnValidate()
        {
            cacheValid = false;
        }
    }

    /// <summary>
    /// Monster substitution entry for RL variants
    /// </summary>
    [System.Serializable]
    public class RLMonsterSubstitution
    {
        public MonsterBlueprint baseBlueprint;
        public RLMonsterBlueprint rlBlueprint;
    }

    /// <summary>
    /// Difficulty scaling mode
    /// </summary>
    public enum DifficultyScalingMode
    {
        None,          // No scaling
        Fixed,         // Fixed difficulty value
        Adaptive,      // Scales based on player performance
        Progressive    // Increases over time
    }

    /// <summary>
    /// Performance optimization modes
    /// </summary>
    public enum RLPerformanceMode
    {
        Quality,       // Prioritize model accuracy
        Balanced,      // Balance between quality and performance
        Performance    // Prioritize frame rate
    }

    /// <summary>
    /// Visualization detail levels
    /// </summary>
    public enum RLVisualizationLevel
    {
        None,
        Minimal,
        Basic,
        Detailed,
        Full
    }

    /// <summary>
    /// Coordination configuration
    /// Requirement: 1.5
    /// </summary>
    [System.Serializable]
    public class CoordinationConfiguration
    {
        public int maxConcurrentAgents;
        public float coordinationBonus;
        public bool enableLearning;
        public int strategyIndex;  // Index into CoordinationStrategy enum
    }

    /// <summary>
    /// Training session configuration
    /// </summary>
    [System.Serializable]
    public class TrainingSessionConfiguration
    {
        public bool trainingMode;
        public int episodesPerSession;
        public float durationMinutes;
        public float baseDifficulty;
    }

    /// <summary>
    /// Performance configuration
    /// </summary>
    [System.Serializable]
    public class PerformanceConfiguration
    {
        public RLPerformanceMode performanceMode;
        public bool enableQuantization;
        public int maxUpdatesPerFrame;
    }
}
