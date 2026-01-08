using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Vampire.RL
{
    /// <summary>
    /// Persistence manager for saving and loading learned behaviors across sessions
    /// Stores player strategy patterns, difficulty settings, and adaptation history
    /// Requirement: 7.4, 7.5 - Learned behavior persistence across sessions
    /// </summary>
    public class AdaptiveLearningPersistence : MonoBehaviour
    {
        [Header("Persistence Settings")]
        [SerializeField] private string persistenceDirectory = "Assets/Data/AdaptiveLearning";
        [SerializeField] private bool enablePersistence = true;
        [SerializeField] private bool enableAutoSave = true;
        [SerializeField] private float autoSaveInterval = 60f; // Save every 60 seconds

        [Header("Data Management")]
        [SerializeField] private int maxSavedProfiles = 10; // Keep last 10 profiles
        [SerializeField] private bool includeTimestamp = true;
        [SerializeField] private bool encryptData = false;

        private float lastSaveTime;
        private string currentProfilePath;

        public event Action<string> OnProfileSaved;
        public event Action<string> OnProfileLoaded;
        public event Action<string> OnDataCleared;

        private void Start()
        {
            if (!Directory.Exists(persistenceDirectory))
            {
                Directory.CreateDirectory(persistenceDirectory);
            }

            lastSaveTime = Time.time;
        }

        private void Update()
        {
            if (!enablePersistence || !enableAutoSave)
                return;

            // Auto-save at intervals
            if (Time.time - lastSaveTime >= autoSaveInterval)
            {
                AutoSaveProfile();
                lastSaveTime = Time.time;
            }
        }

        /// <summary>
        /// Save current adaptive learning profile
        /// Requirement: 7.4
        /// </summary>
        public string SaveProfile(string profileName = "")
        {
            if (!enablePersistence)
                return null;

            try
            {
                // Generate profile name if not provided
                if (string.IsNullOrEmpty(profileName))
                {
                    profileName = includeTimestamp
                        ? $"Profile_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
                        : $"Profile_{DateTime.Now:yyyy-MM-dd}";
                }

                currentProfilePath = Path.Combine(persistenceDirectory, $"{profileName}.json");

                // Collect data from all systems
                var profileData = GatherProfileData();

                // Serialize to JSON
                string json = JsonUtility.ToJson(profileData, true);

                if (encryptData)
                {
                    json = EncryptData(json);
                }

                // Save to file
                File.WriteAllText(currentProfilePath, json);

                // Clean up old profiles if exceeding max
                CleanupOldProfiles();

                OnProfileSaved?.Invoke(profileName);
                Debug.Log($"Adaptive learning profile saved: {profileName}");

                return currentProfilePath;
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("AdaptiveLearningPersistence", "SaveProfile", ex, profileName);
                return null;
            }
        }

        /// <summary>
        /// Load adaptive learning profile
        /// Requirement: 7.4
        /// </summary>
        public bool LoadProfile(string profilePath)
        {
            if (!enablePersistence || !File.Exists(profilePath))
            {
                Debug.LogWarning($"Profile not found: {profilePath}");
                return false;
            }

            try
            {
                string json = File.ReadAllText(profilePath);

                if (encryptData)
                {
                    json = DecryptData(json);
                }

                var profileData = JsonUtility.FromJson<AdaptiveProfileData>(json);

                if (profileData == null)
                {
                    Debug.LogError("Failed to deserialize profile data");
                    return false;
                }

                // Apply loaded data to systems
                ApplyProfileData(profileData);

                currentProfilePath = profilePath;
                OnProfileLoaded?.Invoke(Path.GetFileName(profilePath));
                Debug.Log($"Adaptive learning profile loaded: {profilePath}");

                return true;
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("AdaptiveLearningPersistence", "LoadProfile", ex, profilePath);
                return false;
            }
        }

        /// <summary>
        /// Auto-save current profile
        /// </summary>
        private void AutoSaveProfile()
        {
            SaveProfile("AutoSave");
        }

        /// <summary>
        /// Gather data from all adaptive learning systems
        /// </summary>
        private AdaptiveProfileData GatherProfileData()
        {
            var data = new AdaptiveProfileData
            {
                timestamp = DateTime.Now.ToString("O"),
                gameVersion = Application.version
            };

            // Gather strategy detector data
            var strategyDetector = FindFirstObjectByType<PlayerStrategyDetector>();
            if (strategyDetector != null)
            {
                data.playerSkillLevel = strategyDetector.CurrentSkillLevel.ToString();
                data.detectedStrategies = new List<string>();
                foreach (var strategy in strategyDetector.DetectedStrategies)
                {
                    data.detectedStrategies.Add($"{strategy.strategy}:{strategy.confidence:F3}");
                }
            }

            // Gather difficulty settings
            var difficultyScaler = FindFirstObjectByType<DifficultyScaler>();
            if (difficultyScaler != null)
            {
                data.currentDifficulty = difficultyScaler.CurrentDifficulty.ToString();
                var settings = difficultyScaler.CurrentSettings;
                data.difficultyMultiplier = difficultyScaler.DifficultyMultiplier;
            }

            // Gather adaptation history
            var behaviorAdaptation = FindFirstObjectByType<BehaviorAdaptationSystem>();
            if (behaviorAdaptation != null)
            {
                data.activeAdaptations = new List<string>();
                foreach (var adaptation in behaviorAdaptation.GetActiveAdaptations())
                {
                    data.activeAdaptations.Add($"{adaptation.playerStrategy}->{adaptation.counterStrategy.name}");
                }
            }

            return data;
        }

        /// <summary>
        /// Apply loaded profile data to systems
        /// </summary>
        private void ApplyProfileData(AdaptiveProfileData data)
        {
            // Apply difficulty settings
            var difficultyScaler = FindFirstObjectByType<DifficultyScaler>();
            if (difficultyScaler != null && !string.IsNullOrEmpty(data.currentDifficulty))
            {
                if (System.Enum.TryParse<DifficultyLevel>(data.currentDifficulty, out var difficulty))
                {
                    difficultyScaler.SetDifficulty(difficulty);
                }
            }

            Debug.Log($"Applied saved profile data from {data.timestamp}");
        }

        /// <summary>
        /// Get list of saved profiles
        /// </summary>
        public List<string> GetSavedProfiles()
        {
            var profiles = new List<string>();

            if (!Directory.Exists(persistenceDirectory))
                return profiles;

            var files = Directory.GetFiles(persistenceDirectory, "*.json");
            foreach (var file in files)
            {
                profiles.Add(Path.GetFileName(file));
            }

            return profiles;
        }

        /// <summary>
        /// Delete a saved profile
        /// </summary>
        public bool DeleteProfile(string profilePath)
        {
            try
            {
                if (File.Exists(profilePath))
                {
                    File.Delete(profilePath);
                    Debug.Log($"Profile deleted: {profilePath}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("AdaptiveLearningPersistence", "DeleteProfile", ex, profilePath);
                return false;
            }
        }

        /// <summary>
        /// Clear all saved data
        /// </summary>
        public void ClearAllProfiles()
        {
            try
            {
                if (Directory.Exists(persistenceDirectory))
                {
                    var files = Directory.GetFiles(persistenceDirectory, "*.json");
                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }
                }

                OnDataCleared?.Invoke("All profiles cleared");
                Debug.Log("All adaptive learning profiles cleared");
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("AdaptiveLearningPersistence", "ClearAllProfiles", ex);
            }
        }

        /// <summary>
        /// Clean up old profiles, keeping only the most recent
        /// </summary>
        private void CleanupOldProfiles()
        {
            try
            {
                var files = Directory.GetFiles(persistenceDirectory, "*.json");

                if (files.Length > maxSavedProfiles)
                {
                    // Sort by creation time
                    System.Array.Sort(files, (a, b) =>
                        File.GetCreationTime(b).CompareTo(File.GetCreationTime(a)));

                    // Delete oldest files
                    for (int i = maxSavedProfiles; i < files.Length; i++)
                    {
                        File.Delete(files[i]);
                        Debug.Log($"Deleted old profile: {Path.GetFileName(files[i])}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("AdaptiveLearningPersistence", "CleanupOldProfiles", ex);
            }
        }

        /// <summary>
        /// Encrypt data (placeholder - implement actual encryption as needed)
        /// </summary>
        private string EncryptData(string data)
        {
            // TODO: Implement actual encryption (AES, etc.)
            return data;
        }

        /// <summary>
        /// Decrypt data (placeholder - implement actual decryption as needed)
        /// </summary>
        private string DecryptData(string encryptedData)
        {
            // TODO: Implement actual decryption
            return encryptedData;
        }

        /// <summary>
        /// Export profile as human-readable format
        /// </summary>
        public string ExportProfileAsText(string profilePath)
        {
            try
            {
                if (!File.Exists(profilePath))
                    return null;

                string json = File.ReadAllText(profilePath);
                var data = JsonUtility.FromJson<AdaptiveProfileData>(json);

                var sb = new StringBuilder();
                sb.AppendLine("=== Adaptive Learning Profile ===");
                sb.AppendLine($"Saved: {data.timestamp}");
                sb.AppendLine($"Game Version: {data.gameVersion}");
                sb.AppendLine($"\nPlayer Skill Level: {data.playerSkillLevel}");
                sb.AppendLine($"Current Difficulty: {data.currentDifficulty}");
                sb.AppendLine($"Difficulty Multiplier: {data.difficultyMultiplier:F2}x");

                sb.AppendLine($"\nDetected Strategies ({data.detectedStrategies.Count}):");
                foreach (var strategy in data.detectedStrategies)
                {
                    sb.AppendLine($"  - {strategy}");
                }

                sb.AppendLine($"\nActive Adaptations ({data.activeAdaptations.Count}):");
                foreach (var adaptation in data.activeAdaptations)
                {
                    sb.AppendLine($"  - {adaptation}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("AdaptiveLearningPersistence", "ExportProfileAsText", ex, profilePath);
                return null;
            }
        }
    }

    /// <summary>
    /// Adaptive profile data structure for serialization
    /// </summary>
    [Serializable]
    public class AdaptiveProfileData
    {
        public string timestamp;
        public string gameVersion;
        public string playerSkillLevel;
        public string currentDifficulty;
        public float difficultyMultiplier;
        public List<string> detectedStrategies = new List<string>();
        public List<string> activeAdaptations = new List<string>();
    }
}
