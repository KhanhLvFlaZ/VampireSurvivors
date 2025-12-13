using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Manages model versioning, saving, and loading
    /// Handles ONNX model files with version control
    /// Requirements: 6.1, 6.2, 6.3, 6.4, 6.5
    /// Note: Model files are managed as external ONNX files
    /// </summary>
    public class ModelManager : MonoBehaviour
    {
        [Header("Model Configuration")]
        [SerializeField] private string modelsDirectory = "Assets/Models/RL";
        [SerializeField] private string currentModelName = "monster_model";
        [SerializeField] private int currentVersion = 1;

        [Header("Versioning")]
        [SerializeField] private bool autoIncrementVersion = true;
        [SerializeField] private int maxVersionsToKeep = 10;

        [Header("Model Metadata")]
        [SerializeField] private List<ModelMetadata> loadedModels = new List<ModelMetadata>();

        private Dictionary<string, string> modelCache = new Dictionary<string, string>(); // Cache model paths

        private static ModelManager instance;

        public static ModelManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("ModelManager");
                    instance = go.AddComponent<ModelManager>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Ensure models directory exists
            if (!Directory.Exists(modelsDirectory))
            {
                Directory.CreateDirectory(modelsDirectory);
                Debug.Log($"Created models directory: {modelsDirectory}");
            }

            LoadModelRegistry();
        }

        /// <summary>
        /// Save model with versioning
        /// Requirements: 6.1, 6.2
        /// </summary>
        public string SaveModel(string modelPath, string modelName = null, ModelMetadata metadata = null)
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                Debug.LogError("Cannot save: model path is empty");
                return null;
            }

            modelName = modelName ?? currentModelName;

            // Auto-increment version if enabled
            if (autoIncrementVersion)
            {
                currentVersion = GetNextVersion(modelName);
            }

            // Generate versioned filename
            string filename = GetVersionedFilename(modelName, currentVersion);
            string fullPath = Path.Combine(modelsDirectory, filename);

            try
            {
                // Create metadata if not provided
                if (metadata == null)
                {
                    metadata = new ModelMetadata
                    {
                        modelName = modelName,
                        version = currentVersion,
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        description = $"Model {modelName} version {currentVersion}"
                    };
                }
                else
                {
                    metadata.modelName = modelName;
                    metadata.version = currentVersion;
                    metadata.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }

                // Save metadata
                SaveModelMetadata(metadata, fullPath);

                // Add to registry
                loadedModels.Add(metadata);

                // Cleanup old versions if needed
                CleanupOldVersions(modelName);

                Debug.Log($"Model saved: {filename} (Version {currentVersion})");
                return fullPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save model: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load model by name and version
        /// Requirements: 6.2, 6.4
        /// </summary>
        public string LoadModel(string modelName, int version = -1)
        {
            // Use latest version if not specified
            if (version < 0)
            {
                version = GetLatestVersion(modelName);
            }

            string filename = GetVersionedFilename(modelName, version);
            string cacheKey = $"{modelName}_v{version}";

            // Check cache first
            if (modelCache.ContainsKey(cacheKey))
            {
                Debug.Log($"Model path loaded from cache: {filename}");
                return modelCache[cacheKey];
            }

            string fullPath = Path.Combine(modelsDirectory, filename);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"Model file not found: {fullPath}");
                return null;
            }

            modelCache[cacheKey] = fullPath;
            Debug.Log($"Model loaded: {filename} (Version {version})");
            return fullPath;
        }

        /// <summary>
        /// Load latest model version
        /// Requirements: 6.2
        /// </summary>
        public string LoadLatestModel(string modelName)
        {
            int latestVersion = GetLatestVersion(modelName);
            if (latestVersion < 0)
            {
                Debug.LogWarning($"No versions found for model: {modelName}");
                return null;
            }

            return LoadModel(modelName, latestVersion);
        }

        /// <summary>
        /// Switch between different model versions at runtime
        /// Requirements: 6.4
        /// </summary>
        public bool SwitchModelVersion(string modelName, int targetVersion)
        {
            var modelPath = LoadModel(modelName, targetVersion);

            if (string.IsNullOrEmpty(modelPath))
                currentVersion = targetVersion;

            Debug.Log($"Switched to model: {modelName} v{targetVersion}");
            return true;
        }

        /// <summary>
        /// Get model metadata
        /// Requirements: 6.3
        /// </summary>
        public ModelMetadata GetModelMetadata(string modelName, int version)
        {
            string metadataPath = GetMetadataPath(modelName, version);

            if (!File.Exists(metadataPath))
            {
                Debug.LogWarning($"Metadata not found for {modelName} v{version}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(metadataPath);
                return JsonUtility.FromJson<ModelMetadata>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load metadata: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// List all available model versions
        /// Requirements: 6.3
        /// </summary>
        public List<ModelMetadata> ListModelVersions(string modelName)
        {
            var versions = new List<ModelMetadata>();

            foreach (var metadata in loadedModels)
            {
                if (metadata.modelName == modelName)
                {
                    versions.Add(metadata);
                }
            }

            versions.Sort((a, b) => b.version.CompareTo(a.version)); // Sort by version descending
            return versions;
        }

        /// <summary>
        /// Delete specific model version
        /// Requirements: 6.5
        /// </summary>
        public bool DeleteModelVersion(string modelName, int version)
        {
            string filename = GetVersionedFilename(modelName, version);
            string fullPath = Path.Combine(modelsDirectory, filename);
            string metadataPath = GetMetadataPath(modelName, version);

            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }

                // Remove from registry
                loadedModels.RemoveAll(m => m.modelName == modelName && m.version == version);

                // Clear from cache
                string cacheKey = $"{modelName}_v{version}";
                modelCache.Remove(cacheKey);

                Debug.Log($"Deleted model: {modelName} v{version}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete model: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Export model for deployment
        /// Requirements: 6.5
        /// </summary>
        public string ExportModel(string modelName, int version, string exportPath)
        {
            string filename = GetVersionedFilename(modelName, version);
            string sourcePath = Path.Combine(modelsDirectory, filename);

            if (!File.Exists(sourcePath))
            {
                Debug.LogError($"Model not found: {sourcePath}");
                return null;
            }

            try
            {
                string exportFilename = Path.Combine(exportPath, filename);
                File.Copy(sourcePath, exportFilename, true);

                // Also copy metadata
                string metadataSource = GetMetadataPath(modelName, version);
                string metadataExport = Path.Combine(exportPath, Path.GetFileName(metadataSource));
                if (File.Exists(metadataSource))
                {
                    File.Copy(metadataSource, metadataExport, true);
                }

                Debug.Log($"Model exported to: {exportFilename}");
                return exportFilename;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to export model: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compare two model versions
        /// Requirements: 6.3
        /// </summary>
        public ModelComparison CompareModels(string modelName, int version1, int version2)
        {
            var metadata1 = GetModelMetadata(modelName, version1);
            var metadata2 = GetModelMetadata(modelName, version2);

            if (metadata1 == null || metadata2 == null)
            {
                Debug.LogError("Cannot compare models: metadata not found");
                return null;
            }

            var comparison = new ModelComparison
            {
                modelName = modelName,
                version1 = version1,
                version2 = version2,
                metadata1 = metadata1,
                metadata2 = metadata2
            };

            Debug.Log($"Model comparison: {modelName} v{version1} vs v{version2}");
            return comparison;
        }

        #region Private Helper Methods

        private string GetVersionedFilename(string modelName, int version)
        {
            return $"{modelName}_v{version}.onnx";
        }

        private string GetMetadataPath(string modelName, int version)
        {
            return Path.Combine(modelsDirectory, $"{modelName}_v{version}_metadata.json");
        }

        private int GetNextVersion(string modelName)
        {
            int maxVersion = 0;

            foreach (var metadata in loadedModels)
            {
                if (metadata.modelName == modelName && metadata.version > maxVersion)
                {
                    maxVersion = metadata.version;
                }
            }

            return maxVersion + 1;
        }

        private int GetLatestVersion(string modelName)
        {
            int latestVersion = -1;

            foreach (var metadata in loadedModels)
            {
                if (metadata.modelName == modelName && metadata.version > latestVersion)
                {
                    latestVersion = metadata.version;
                }
            }

            return latestVersion;
        }

        private void SaveModelMetadata(ModelMetadata metadata, string modelPath)
        {
            string metadataPath = GetMetadataPath(metadata.modelName, metadata.version);
            string json = JsonUtility.ToJson(metadata, true);
            File.WriteAllText(metadataPath, json);
        }

        private void CleanupOldVersions(string modelName)
        {
            if (maxVersionsToKeep <= 0)
                return;

            var versions = ListModelVersions(modelName);

            if (versions.Count > maxVersionsToKeep)
            {
                // Keep only the latest N versions
                for (int i = maxVersionsToKeep; i < versions.Count; i++)
                {
                    DeleteModelVersion(modelName, versions[i].version);
                }

                Debug.Log($"Cleaned up old versions of {modelName}. Kept {maxVersionsToKeep} versions.");
            }
        }

        private void LoadModelRegistry()
        {
            // Load all metadata files from models directory
            if (!Directory.Exists(modelsDirectory))
                return;

            string[] metadataFiles = Directory.GetFiles(modelsDirectory, "*_metadata.json");

            foreach (string file in metadataFiles)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var metadata = JsonUtility.FromJson<ModelMetadata>(json);
                    loadedModels.Add(metadata);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load metadata from {file}: {e.Message}");
                }
            }

            Debug.Log($"Loaded {loadedModels.Count} model metadata entries");
        }

        #endregion
    }

    /// <summary>
    /// Model metadata for versioning and tracking
    /// </summary>
    [Serializable]
    public class ModelMetadata
    {
        public string modelName;
        public int version;
        public string timestamp;
        public string description;
        public int totalEpisodesTrained;
        public float averageReward;
        public string trainingDuration;
        public Dictionary<string, float> performanceMetrics = new Dictionary<string, float>();
    }

    /// <summary>
    /// Model comparison result
    /// </summary>
    [Serializable]
    public class ModelComparison
    {
        public string modelName;
        public int version1;
        public int version2;
        public ModelMetadata metadata1;
        public ModelMetadata metadata2;
    }
}
