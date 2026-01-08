using UnityEngine;
using System;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Central configuration system for RL components
    /// Allows real-time parameter adjustment and monitoring
    /// Requirement: 2.3 - RL system configuration
    /// </summary>
    public class RLSystemConfiguration : MonoBehaviour
    {
        [Header("Singleton")]
        private static RLSystemConfiguration instance;

        public static RLSystemConfiguration Instance
        {
            get
            {
                if (instance == null)
                    instance = FindFirstObjectByType<RLSystemConfiguration>();
                return instance;
            }
        }

        [Header("Training Configuration")]
        [SerializeField] private bool enableTraining = true;
        [SerializeField] private int maxEpisodes = 1000;
        [SerializeField] private float learningRate = 0.001f;
        [SerializeField] private float discountFactor = 0.99f;
        [SerializeField] private float explorationRate = 0.1f;

        [Header("Environment Configuration")]
        [SerializeField] private float observationRadius = 10f;
        [SerializeField] private int maxNearbyMonsters = 5;
        [SerializeField] private float episodeTimeLimit = 300f;

        [Header("Reward Configuration")]
        [SerializeField] private float damageRewardScale = 1f;
        [SerializeField] private float survivalRewardScale = 0.1f;
        [SerializeField] private float cooperationRewardScale = 0.5f;
        [SerializeField] private float positioningRewardScale = 0.3f;

        [Header("Model Configuration")]
        [SerializeField] private bool autoSaveModels = true;
        [SerializeField] private int saveInterval = 100; // Save every N episodes
        [SerializeField] private string modelDirectory = "Assets/Models/RL/";

        [Header("Debug Settings")]
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private bool visualizeDecisions = true;

        // Runtime parameters that can be modified
        private Dictionary<string, float> runtimeParameters = new Dictionary<string, float>();
        private List<IConfigurable> configurableComponents = new List<IConfigurable>();

        // Events for parameter changes
        public event Action<string, float> OnParameterChanged;
        public event Action OnConfigurationLoaded;

        private void OnEnable()
        {
            if (instance == null)
                instance = this;
            else if (instance != this)
                Destroy(gameObject);
        }

        private void Awake()
        {
            // Initialize runtime parameters from inspector values
            InitializeRuntimeParameters();

            // Find all configurable components
            FindConfigurableComponents();
        }

        /// <summary>
        /// Initialize runtime parameters
        /// </summary>
        private void InitializeRuntimeParameters()
        {
            runtimeParameters.Clear();

            // Training parameters
            runtimeParameters["learningRate"] = learningRate;
            runtimeParameters["discountFactor"] = discountFactor;
            runtimeParameters["explorationRate"] = explorationRate;

            // Environment parameters
            runtimeParameters["observationRadius"] = observationRadius;
            runtimeParameters["maxNearbyMonsters"] = maxNearbyMonsters;
            runtimeParameters["episodeTimeLimit"] = episodeTimeLimit;

            // Reward parameters
            runtimeParameters["damageRewardScale"] = damageRewardScale;
            runtimeParameters["survivalRewardScale"] = survivalRewardScale;
            runtimeParameters["cooperationRewardScale"] = cooperationRewardScale;
            runtimeParameters["positioningRewardScale"] = positioningRewardScale;
        }

        /// <summary>
        /// Find all configurable components
        /// </summary>
        private void FindConfigurableComponents()
        {
            configurableComponents.Clear();
            var components = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            foreach (var component in components)
            {
                if (component is IConfigurable configurable)
                {
                    configurableComponents.Add(configurable);
                }
            }

            Debug.Log($"Found {configurableComponents.Count} configurable components");
        }

        /// <summary>
        /// Get parameter value
        /// </summary>
        public float GetParameter(string parameterName)
        {
            if (runtimeParameters.TryGetValue(parameterName, out float value))
                return value;

            Debug.LogWarning($"Parameter '{parameterName}' not found");
            return 0f;
        }

        /// <summary>
        /// Set parameter value and notify components
        /// Requirement: 2.3
        /// </summary>
        public void SetParameter(string parameterName, float value)
        {
            if (!runtimeParameters.ContainsKey(parameterName))
            {
                Debug.LogWarning($"Parameter '{parameterName}' not found");
                return;
            }

            float oldValue = runtimeParameters[parameterName];
            runtimeParameters[parameterName] = value;

            // Log change if debug enabled
            if (debugLogs)
            {
                Debug.Log($"Parameter '{parameterName}' changed from {oldValue} to {value}");
            }

            // Notify components
            OnParameterChanged?.Invoke(parameterName, value);

            // Apply to configurable components
            ApplyParameterToComponents(parameterName, value);
        }

        /// <summary>
        /// Apply parameter change to all configurable components
        /// </summary>
        private void ApplyParameterToComponents(string parameterName, float value)
        {
            foreach (var component in configurableComponents)
            {
                try
                {
                    // Only call UpdateParameter if the component implements IConfigurable
                    // Otherwise skip to avoid errors
                    if (component is IConfigurable configurable)
                    {
                        configurable.UpdateParameter(parameterName, value);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error updating parameter on {component}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get all parameters
        /// </summary>
        public Dictionary<string, float> GetAllParameters()
        {
            return new Dictionary<string, float>(runtimeParameters);
        }

        /// <summary>
        /// Load configuration from file
        /// </summary>
        public bool LoadConfiguration(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    Debug.LogError($"Configuration file not found: {filePath}");
                    return false;
                }

                string jsonContent = System.IO.File.ReadAllText(filePath);
                ConfigurationData data = JsonUtility.FromJson<ConfigurationData>(jsonContent);

                if (data != null)
                {
                    ApplyConfiguration(data);
                    OnConfigurationLoaded?.Invoke();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save configuration to file
        /// </summary>
        public bool SaveConfiguration(string filePath)
        {
            try
            {
                var data = new ConfigurationData
                {
                    enableTraining = enableTraining,
                    learningRate = learningRate,
                    discountFactor = discountFactor,
                    explorationRate = explorationRate,
                    observationRadius = observationRadius,
                    damageRewardScale = damageRewardScale,
                    survivalRewardScale = survivalRewardScale,
                    cooperationRewardScale = cooperationRewardScale,
                    positioningRewardScale = positioningRewardScale
                };

                string jsonContent = JsonUtility.ToJson(data, true);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
                System.IO.File.WriteAllText(filePath, jsonContent);

                Debug.Log($"Configuration saved to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Apply configuration data
        /// </summary>
        private void ApplyConfiguration(ConfigurationData data)
        {
            enableTraining = data.enableTraining;
            learningRate = data.learningRate;
            discountFactor = data.discountFactor;
            explorationRate = data.explorationRate;
            observationRadius = data.observationRadius;
            damageRewardScale = data.damageRewardScale;
            survivalRewardScale = data.survivalRewardScale;
            cooperationRewardScale = data.cooperationRewardScale;
            positioningRewardScale = data.positioningRewardScale;

            InitializeRuntimeParameters();
        }

        // Property accessors
        public bool EnableTraining => enableTraining;
        public int MaxEpisodes => maxEpisodes;
        public float LearningRate => learningRate;
        public float DiscountFactor => discountFactor;
        public float ExplorationRate => explorationRate;
        public float ObservationRadius => observationRadius;
        public int MaxNearbyMonsters => maxNearbyMonsters;
        public float EpisodeTimeLimit => episodeTimeLimit;
        public float DamageRewardScale => damageRewardScale;
        public float SurvivalRewardScale => survivalRewardScale;
        public float CooperationRewardScale => cooperationRewardScale;
        public float PositioningRewardScale => positioningRewardScale;
        public bool AutoSaveModels => autoSaveModels;
        public int SaveInterval => saveInterval;
        public string ModelDirectory => modelDirectory;
        public bool DebugLogs => debugLogs;
        public bool VisualizeDecisions => visualizeDecisions;
    }

    /// <summary>
    /// Configuration data structure for serialization
    /// </summary>
    [System.Serializable]
    public class ConfigurationData
    {
        public bool enableTraining;
        public float learningRate;
        public float discountFactor;
        public float explorationRate;
        public float observationRadius;
        public float damageRewardScale;
        public float survivalRewardScale;
        public float cooperationRewardScale;
        public float positioningRewardScale;
    }
}
