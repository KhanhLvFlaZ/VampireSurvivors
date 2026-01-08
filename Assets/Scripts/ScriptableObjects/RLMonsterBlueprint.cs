using UnityEngine;
using Vampire;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Extends MonsterBlueprint with RL-specific configuration
    /// Allows monsters to be configured for reinforcement learning training and inference
    /// Requirement: 1.1 - RL agent initialization
    /// </summary>
    [CreateAssetMenu(fileName = "RLMonsterBlueprint", menuName = "Blueprints/RL Monster", order = 2)]
    public class RLMonsterBlueprint : MonsterBlueprint
    {
        [Header("RL Configuration")]
        [SerializeField] private bool enableRL = true;
        [SerializeField] private int networkArchitectureIndex = 0; // Simple
        [SerializeField] private int learningAlgorithmIndex = 0; // DQN
        [SerializeField] private int[] hiddenLayerSizes = new int[] { 64, 32 };

        [Header("RL Behavior")]
        [SerializeField] private float explorationRate = 0.1f; // Epsilon for epsilon-greedy
        [SerializeField] private float learningRate = 0.001f;
        [SerializeField] private float discountFactor = 0.99f; // Gamma

        [Header("Reward Configuration")]
        [SerializeField] private RewardComponentConfig damageReward = new RewardComponentConfig { weight = 0.4f };
        [SerializeField] private RewardComponentConfig survivalReward = new RewardComponentConfig { weight = 0.3f };
        [SerializeField] private RewardComponentConfig cooperationReward = new RewardComponentConfig { weight = 0.2f };
        [SerializeField] private RewardComponentConfig positioningReward = new RewardComponentConfig { weight = 0.1f };

        [Header("Training Settings")]
        [SerializeField] private bool enableTraining = false;
        [SerializeField] private int experienceBufferSize = 10000;
        [SerializeField] private int batchSize = 32;
        [SerializeField] private float trainingUpdateInterval = 0.1f;

        [Header("Model Settings")]
        [SerializeField] private string modelName = "";
        [SerializeField] private bool usePreTrainedModel = false;
        [SerializeField] private string preTrainedModelPath = "";

        [Header("Adaptive Learning")]
        [SerializeField] private bool enableAdaptiveLearning = true;
        [SerializeField] private bool enableStrategyDetection = true;
        [SerializeField] private bool enableDifficultyScaling = true;
        [SerializeField] private bool enableBehaviorAdaptation = true;

        [Header("Visualization")]
        [SerializeField] private bool showDecisionIndicators = true;
        [SerializeField] private bool showCoordinationVisuals = true;
        [SerializeField] private bool showDebugInfo = false;

        public bool EnableRL => enableRL;
        public int NetworkArchitectureIndex => networkArchitectureIndex;
        public int LearningAlgorithmIndex => learningAlgorithmIndex;
        public int[] HiddenLayerSizes => hiddenLayerSizes;
        public float ExplorationRate => explorationRate;
        public float LearningRate => learningRate;
        public float DiscountFactor => discountFactor;

        public RewardComponentConfig DamageReward => damageReward;
        public RewardComponentConfig SurvivalReward => survivalReward;
        public RewardComponentConfig CooperationReward => cooperationReward;
        public RewardComponentConfig PositioningReward => positioningReward;

        public bool EnableTraining => enableTraining;
        public int ExperienceBufferSize => experienceBufferSize;
        public int BatchSize => batchSize;
        public float TrainingUpdateInterval => trainingUpdateInterval;

        public string ModelName => modelName;
        public bool UsePreTrainedModel => usePreTrainedModel;
        public string PreTrainedModelPath => preTrainedModelPath;

        public bool EnableAdaptiveLearning => enableAdaptiveLearning;
        public bool EnableStrategyDetection => enableStrategyDetection;
        public bool EnableDifficultyScaling => enableDifficultyScaling;
        public bool EnableBehaviorAdaptation => enableBehaviorAdaptation;

        public bool ShowDecisionIndicators => showDecisionIndicators;
        public bool ShowCoordinationVisuals => showCoordinationVisuals;
        public bool ShowDebugInfo => showDebugInfo;

        /// <summary>
        /// Get reward configuration as dictionary
        /// Requirement: 1.1
        /// </summary>
        public Dictionary<int, RewardComponentConfig> GetRewardConfiguration()
        {
            return new Dictionary<int, RewardComponentConfig>
            {
                { 0, damageReward },      // RewardType.Damage
                { 1, survivalReward },    // RewardType.Survival
                { 2, cooperationReward }, // RewardType.Cooperation
                { 3, positioningReward }  // RewardType.Positioning
            };
        }

        /// <summary>
        /// Get network configuration
        /// </summary>
        public NetworkConfiguration GetNetworkConfiguration()
        {
            return new NetworkConfiguration
            {
                architectureIndex = networkArchitectureIndex,
                algorithmIndex = learningAlgorithmIndex,
                hiddenLayerSizes = hiddenLayerSizes,
                learningRate = learningRate,
                explorationRate = explorationRate,
                discountFactor = discountFactor
            };
        }

        /// <summary>
        /// Get training configuration
        /// </summary>
        public TrainingConfiguration GetTrainingConfiguration()
        {
            return new TrainingConfiguration
            {
                enableTraining = enableTraining,
                experienceBufferSize = experienceBufferSize,
                batchSize = batchSize,
                updateInterval = trainingUpdateInterval
            };
        }

        /// <summary>
        /// Get adaptive learning configuration
        /// </summary>
        public AdaptiveLearningConfiguration GetAdaptiveLearningConfiguration()
        {
            return new AdaptiveLearningConfiguration
            {
                enableAdaptiveLearning = enableAdaptiveLearning,
                enableStrategyDetection = enableStrategyDetection,
                enableDifficultyScaling = enableDifficultyScaling,
                enableBehaviorAdaptation = enableBehaviorAdaptation
            };
        }

        /// <summary>
        /// Validate RL configuration
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            errorMessage = "";

            if (!enableRL)
                return true; // Valid if RL is disabled

            if (networkArchitectureIndex < 0)
            {
                errorMessage = "Network architecture must be specified";
                return false;
            }

            if (hiddenLayerSizes == null || hiddenLayerSizes.Length == 0)
            {
                errorMessage = "At least one hidden layer size must be specified";
                return false;
            }

            float totalWeight = damageReward.weight + survivalReward.weight + cooperationReward.weight + positioningReward.weight;
            if (Mathf.Abs(totalWeight - 1.0f) > 0.01f)
            {
                errorMessage = $"Reward weights must sum to 1.0 (currently {totalWeight:F2})";
                return false;
            }

            if (enableTraining && batchSize > experienceBufferSize)
            {
                errorMessage = "Batch size cannot be larger than experience buffer size";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Reward component configuration
    /// </summary>
    [System.Serializable]
    public class RewardComponentConfig
    {
        public float weight;
        public float scale = 1.0f;
        public bool enabled = true;
    }

    /// <summary>
    /// Network configuration
    /// </summary>
    [System.Serializable]
    public class NetworkConfiguration
    {
        public int architectureIndex;  // NetworkArchitecture enum value
        public int algorithmIndex;      // LearningAlgorithm enum value
        public int[] hiddenLayerSizes;
        public float learningRate;
        public float explorationRate;
        public float discountFactor;
    }

    /// <summary>
    /// Training configuration
    /// </summary>
    [System.Serializable]
    public class TrainingConfiguration
    {
        public bool enableTraining;
        public int experienceBufferSize;
        public int batchSize;
        public float updateInterval;
    }

    /// <summary>
    /// Adaptive learning configuration
    /// </summary>
    [System.Serializable]
    public class AdaptiveLearningConfiguration
    {
        public bool enableAdaptiveLearning;
        public bool enableStrategyDetection;
        public bool enableDifficultyScaling;
        public bool enableBehaviorAdaptation;
    }

}
