using UnityEngine;
using System;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Runtime parameter adjustment system
    /// Allows real-time tuning of RL parameters without restarting
    /// Requirement: 2.3, 2.5 - Real-time parameter adjustment
    /// </summary>
    public class ParameterAdjustmentManager : MonoBehaviour
    {
        [Header("Adjustment Settings")]
        [SerializeField] private bool enableAdjustments = true;
        [SerializeField] private bool logAdjustments = true;
        [SerializeField] private float adjustmentSensitivity = 1f;

        // Parameter adjustment ranges
        [System.Serializable]
        public class ParameterRange
        {
            public string parameterName;
            public float minValue;
            public float maxValue;
            public float defaultValue;
        }

        [SerializeField] private List<ParameterRange> parameterRanges;

        // Adjustment profiles
        [System.Serializable]
        public class AdjustmentProfile
        {
            public string profileName;
            public Dictionary<string, float> parameters;
        }

        private Dictionary<string, ParameterRange> rangeMap;
        private Dictionary<string, AdjustmentProfile> profiles;
        private string activeProfile;

        // Events
        public event Action<string, float> OnParameterAdjusted;
        public event Action<string> OnProfileActivated;

        private void Awake()
        {
            rangeMap = new Dictionary<string, ParameterRange>();
            profiles = new Dictionary<string, AdjustmentProfile>();

            // Build range map
            foreach (var range in parameterRanges)
            {
                rangeMap[range.parameterName] = range;
            }

            // Create default profiles
            InitializeProfiles();
        }

        /// <summary>
        /// Initialize default adjustment profiles
        /// </summary>
        private void InitializeProfiles()
        {
            // Conservative profile
            profiles["Conservative"] = new AdjustmentProfile
            {
                profileName = "Conservative",
                parameters = new Dictionary<string, float>
                {
                    { "learningRate", 0.0005f },
                    { "explorationRate", 0.05f },
                    { "discountFactor", 0.95f }
                }
            };

            // Balanced profile
            profiles["Balanced"] = new AdjustmentProfile
            {
                profileName = "Balanced",
                parameters = new Dictionary<string, float>
                {
                    { "learningRate", 0.001f },
                    { "explorationRate", 0.1f },
                    { "discountFactor", 0.99f }
                }
            };

            // Aggressive profile
            profiles["Aggressive"] = new AdjustmentProfile
            {
                profileName = "Aggressive",
                parameters = new Dictionary<string, float>
                {
                    { "learningRate", 0.002f },
                    { "explorationRate", 0.15f },
                    { "discountFactor", 0.99f }
                }
            };
        }

        /// <summary>
        /// Adjust a parameter within valid range
        /// Requirement: 2.5
        /// </summary>
        public bool AdjustParameter(string parameterName, float value)
        {
            if (!enableAdjustments)
                return false;

            if (!rangeMap.TryGetValue(parameterName, out var range))
            {
                Debug.LogWarning($"Parameter '{parameterName}' not found in ranges");
                return false;
            }

            // Clamp value to valid range
            float clampedValue = Mathf.Clamp(value, range.minValue, range.maxValue);

            // Apply adjustment through configuration system
            var config = RLSystemConfiguration.Instance;
            if (config != null)
            {
                config.SetParameter(parameterName, clampedValue);
            }

            OnParameterAdjusted?.Invoke(parameterName, clampedValue);

            if (logAdjustments)
            {
                Debug.Log($"Parameter '{parameterName}' adjusted to {clampedValue:F4}");
            }

            return true;
        }

        /// <summary>
        /// Adjust multiple parameters at once
        /// </summary>
        public bool AdjustMultipleParameters(Dictionary<string, float> adjustments)
        {
            bool allSuccessful = true;

            foreach (var kvp in adjustments)
            {
                if (!AdjustParameter(kvp.Key, kvp.Value))
                {
                    allSuccessful = false;
                }
            }

            return allSuccessful;
        }

        /// <summary>
        /// Increase parameter by offset
        /// </summary>
        public bool IncreaseParameter(string parameterName, float offset)
        {
            var config = RLSystemConfiguration.Instance;
            if (config == null)
                return false;

            float currentValue = config.GetParameter(parameterName);
            return AdjustParameter(parameterName, currentValue + offset * adjustmentSensitivity);
        }

        /// <summary>
        /// Decrease parameter by offset
        /// </summary>
        public bool DecreaseParameter(string parameterName, float offset)
        {
            return IncreaseParameter(parameterName, -offset);
        }

        /// <summary>
        /// Reset parameter to default
        /// </summary>
        public bool ResetParameter(string parameterName)
        {
            if (!rangeMap.TryGetValue(parameterName, out var range))
                return false;

            return AdjustParameter(parameterName, range.defaultValue);
        }

        /// <summary>
        /// Reset all parameters to default
        /// </summary>
        public void ResetAllParameters()
        {
            foreach (var kvp in rangeMap)
            {
                ResetParameter(kvp.Key);
            }

            if (logAdjustments)
                Debug.Log("All parameters reset to default values");
        }

        /// <summary>
        /// Apply adjustment profile
        /// Requirement: 2.5
        /// </summary>
        public bool ApplyProfile(string profileName)
        {
            if (!profiles.TryGetValue(profileName, out var profile))
            {
                Debug.LogError($"Profile '{profileName}' not found");
                return false;
            }

            bool success = AdjustMultipleParameters(profile.parameters);

            if (success)
            {
                activeProfile = profileName;
                OnProfileActivated?.Invoke(profileName);

                if (logAdjustments)
                    Debug.Log($"Applied profile: {profileName}");
            }

            return success;
        }

        /// <summary>
        /// Create custom profile
        /// </summary>
        public bool CreateProfile(string profileName, Dictionary<string, float> parameters)
        {
            if (profiles.ContainsKey(profileName))
            {
                Debug.LogWarning($"Profile '{profileName}' already exists");
                return false;
            }

            profiles[profileName] = new AdjustmentProfile
            {
                profileName = profileName,
                parameters = new Dictionary<string, float>(parameters)
            };

            if (logAdjustments)
                Debug.Log($"Created profile: {profileName}");

            return true;
        }

        /// <summary>
        /// Save current parameters as profile
        /// </summary>
        public bool SaveCurrentAsProfile(string profileName)
        {
            var config = RLSystemConfiguration.Instance;
            if (config == null)
                return false;

            var parameters = config.GetAllParameters();
            return CreateProfile(profileName, parameters);
        }

        /// <summary>
        /// Get parameter range
        /// </summary>
        public ParameterRange GetParameterRange(string parameterName)
        {
            return rangeMap.TryGetValue(parameterName, out var range) ? range : null;
        }

        /// <summary>
        /// Get all parameter ranges
        /// </summary>
        public Dictionary<string, ParameterRange> GetAllRanges()
        {
            return new Dictionary<string, ParameterRange>(rangeMap);
        }

        /// <summary>
        /// Get available profiles
        /// </summary>
        public List<string> GetAvailableProfiles()
        {
            return new List<string>(profiles.Keys);
        }

        /// <summary>
        /// Get active profile
        /// </summary>
        public string GetActiveProfile()
        {
            return activeProfile;
        }

        /// <summary>
        /// Export current parameters
        /// </summary>
        public bool ExportParameters(string filePath)
        {
            var config = RLSystemConfiguration.Instance;
            if (config == null)
                return false;

            try
            {
                var parameters = config.GetAllParameters();
                var exportData = new System.Collections.Generic.Dictionary<string, float>(parameters);
                string json = JsonUtility.ToJson(new ParameterExportWrapper { parameters = parameters });
                System.IO.File.WriteAllText(filePath, json);

                Debug.Log($"Parameters exported to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to export parameters: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Import parameters from file
        /// </summary>
        public bool ImportParameters(string filePath)
        {
            try
            {
                string json = System.IO.File.ReadAllText(filePath);
                var wrapper = JsonUtility.FromJson<ParameterExportWrapper>(json);

                if (wrapper?.parameters != null)
                {
                    AdjustMultipleParameters(wrapper.parameters);
                    Debug.Log($"Parameters imported from {filePath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to import parameters: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Wrapper for JSON serialization of parameters
    /// </summary>
    [System.Serializable]
    public class ParameterExportWrapper
    {
        public Dictionary<string, float> parameters;
    }
}
