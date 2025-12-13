using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// Consolidated model metadata for use across all model management systems
    /// Unified from ModelManager and ModelEvaluationSystem
    /// Requirements: 6.1, 6.2, 2.5
    /// </summary>
    [Serializable]
    public class ModelMetadata
    {
        // Core identification
        public string modelName;
        public string path;

        // Versioning
        public int version;

        // Metadata
        public string description;
        public string timestamp;

        // File information
        public long fileSize;

        // Registration information
        public float registrationTime;

        // Additional properties
        public Dictionary<string, string> metadata = new Dictionary<string, string>();

        public ModelMetadata()
        {
            metadata = new Dictionary<string, string>();
        }

        public ModelMetadata(string name, string modelPath)
        {
            modelName = name;
            path = modelPath;
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            registrationTime = Time.time;
            version = 1;
            metadata = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return $"Model: {modelName} (v{version}) - {path}";
        }
    }

    /// <summary>
    /// Evaluation result for a single model
    /// Contains performance metrics from evaluation episodes
    /// Requirement: 2.5
    /// </summary>
    [Serializable]
    public class EvaluationResult
    {
        public string modelName;
        public string evaluationTime;
        public int episodeCount;
        public float averageReward;
        public float maxReward;
        public float minReward;
        public float standardDeviation;
        public List<float> episodeRewards;

        public EvaluationResult()
        {
            episodeRewards = new List<float>();
        }

        public override string ToString()
        {
            return $"[{modelName}] Avg: {averageReward:F2}, Max: {maxReward:F2}, Min: {minReward:F2}, StdDev: {standardDeviation:F2}";
        }
    }

    /// <summary>
    /// Comparison result between multiple models
    /// Contains comparison data and rankings
    /// Requirement: 2.5
    /// </summary>
    [Serializable]
    public class ModelComparison
    {
        // Comparison identification
        public string timestamp;
        public List<string> comparedModels;

        // Comparison results
        public Dictionary<string, EvaluationResult> results;
        public List<string> ranking; // Ordered by performance (best to worst)

        // Additional comparison data
        public Dictionary<string, string> comparisonMetadata;

        public ModelComparison()
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            comparedModels = new List<string>();
            results = new Dictionary<string, EvaluationResult>();
            ranking = new List<string>();
            comparisonMetadata = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return $"Comparison ({timestamp}): {comparedModels.Count} models compared. Top: {(ranking.Count > 0 ? ranking[0] : "N/A")}";
        }
    }

    /// <summary>
    /// Model comparison with version information
    /// Specifically for comparing different versions of the same model
    /// </summary>
    [Serializable]
    public class ModelVersionComparison
    {
        public string modelName;
        public int version1;
        public int version2;
        public ModelMetadata metadata1;
        public ModelMetadata metadata2;
        public EvaluationResult result1;
        public EvaluationResult result2;

        public override string ToString()
        {
            return $"Version Comparison: {modelName} v{version1} vs v{version2}";
        }
    }
}
