using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Vampire.RL
{
    /// <summary>
    /// Model comparison and evaluation system
    /// Compares different trained models and evaluates their performance
    /// Requirement: 2.5 - Model comparison and evaluation
    /// </summary>
    public class ModelEvaluationSystem : MonoBehaviour
    {
        [Header("Evaluation Settings")]
        [SerializeField] private int evaluationEpisodes = 10;
        [SerializeField] private bool useDeterministicPolicy = true;
        [SerializeField] private bool logEvaluationResults = true;

        [Header("Model Storage")]
        [SerializeField] private string modelStoragePath = "Assets/Models/RL/";

        // Model registry
        private Dictionary<string, ModelMetadata> loadedModels;
        private Dictionary<string, EvaluationResult> evaluationResults;
        private ModelComparison lastComparison;

        public event Action<EvaluationResult> OnModelEvaluated;
        public event Action<ModelComparison> OnComparisonComplete;

        private void Awake()
        {
            loadedModels = new Dictionary<string, ModelMetadata>();
            evaluationResults = new Dictionary<string, EvaluationResult>();
        }

        /// <summary>
        /// Register a model for comparison
        /// </summary>
        public void RegisterModel(string modelName, string modelPath, string description = "")
        {
            if (loadedModels.ContainsKey(modelName))
            {
                Debug.LogWarning($"Model '{modelName}' already registered");
                return;
            }

            var fileSize = 0L;
            try
            {
                var fileInfo = new System.IO.FileInfo(modelPath);
                fileSize = fileInfo.Length;
            }
            catch
            {
                // File size remains 0
            }

            var metadata = new ModelMetadata
            {
                modelName = modelName,
                path = modelPath,
                description = description,
                registrationTime = Time.time,
                fileSize = fileSize
            };

            loadedModels[modelName] = metadata;

            if (logEvaluationResults)
                Debug.Log($"Model registered: {modelName}");
        }

        /// <summary>
        /// Evaluate a model's performance
        /// Requirement: 2.5
        /// </summary>
        public EvaluationResult EvaluateModel(string modelName, RLEnvironment environment)
        {
            if (!loadedModels.ContainsKey(modelName))
            {
                Debug.LogError($"Model '{modelName}' not registered");
                return null;
            }

            var model = loadedModels[modelName];
            var result = new EvaluationResult
            {
                modelName = modelName,
                evaluationTime = DateTime.Now.ToString(),
                episodeCount = evaluationEpisodes
            };

            // Simulate evaluation (in real scenario, would run actual episodes)
            List<float> episodeRewards = new List<float>();

            for (int i = 0; i < evaluationEpisodes; i++)
            {
                float reward = SimulateEpisode(model);
                episodeRewards.Add(reward);
            }

            // Calculate statistics
            result.averageReward = episodeRewards.Average();
            result.maxReward = episodeRewards.Max();
            result.minReward = episodeRewards.Min();
            result.standardDeviation = CalculateStandardDeviation(episodeRewards);
            result.episodeRewards = episodeRewards;

            evaluationResults[modelName] = result;

            OnModelEvaluated?.Invoke(result);

            if (logEvaluationResults)
                LogEvaluationResult(result);

            return result;
        }

        /// <summary>
        /// Compare multiple models
        /// </summary>
        public ModelComparison CompareModels(List<string> modelNames, RLEnvironment environment)
        {
            if (modelNames.Count < 2)
            {
                Debug.LogError("Need at least 2 models to compare");
                return null;
            }

            var comparison = new ModelComparison
            {
                timestamp = DateTime.Now.ToString(),
                comparedModels = new List<string>(modelNames),
                results = new Dictionary<string, EvaluationResult>()
            };

            // Evaluate each model
            foreach (var modelName in modelNames)
            {
                var result = EvaluateModel(modelName, environment);
                if (result != null)
                {
                    comparison.results[modelName] = result;
                }
            }

            // Rank models by average reward
            var rankedModels = comparison.results
                .OrderByDescending(kvp => kvp.Value.averageReward)
                .ToList();

            comparison.ranking = rankedModels
                .Select(kvp => kvp.Key)
                .ToList();

            lastComparison = comparison;
            OnComparisonComplete?.Invoke(comparison);

            if (logEvaluationResults)
                LogComparisonResult(comparison);

            return comparison;
        }

        /// <summary>
        /// Simulate a single episode with a model
        /// </summary>
        private float SimulateEpisode(ModelMetadata model)
        {
            // Placeholder for actual model evaluation
            // In production, would load model and run inference
            float reward = UnityEngine.Random.Range(10f, 100f);
            return reward;
        }

        /// <summary>
        /// Calculate standard deviation
        /// </summary>
        private float CalculateStandardDeviation(List<float> values)
        {
            if (values.Count == 0)
                return 0f;

            float mean = values.Average();
            float sumOfSquares = values.Sum(x => (x - mean) * (x - mean));
            return Mathf.Sqrt(sumOfSquares / values.Count);
        }

        /// <summary>
        /// Get file size in MB
        /// </summary>
        private float GetFileSize(string filePath)
        {
            try
            {
                var fileInfo = new System.IO.FileInfo(filePath);
                return fileInfo.Length / (1024f * 1024f); // Convert to MB
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Log evaluation results
        /// </summary>
        private void LogEvaluationResult(EvaluationResult result)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine($"=== Model Evaluation: {result.modelName} ===");
            log.AppendLine($"Time: {result.evaluationTime}");
            log.AppendLine($"Episodes: {result.episodeCount}");
            log.AppendLine($"Average Reward: {result.averageReward:F2}");
            log.AppendLine($"Max Reward: {result.maxReward:F2}");
            log.AppendLine($"Min Reward: {result.minReward:F2}");
            log.AppendLine($"Std Dev: {result.standardDeviation:F2}");

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Log comparison results
        /// </summary>
        private void LogComparisonResult(ModelComparison comparison)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine($"=== Model Comparison ({comparison.timestamp}) ===");

            for (int i = 0; i < comparison.ranking.Count; i++)
            {
                string modelName = comparison.ranking[i];
                var result = comparison.results[modelName];
                log.AppendLine($"{i + 1}. {modelName}: {result.averageReward:F2}");
            }

            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Get all registered models
        /// </summary>
        public Dictionary<string, ModelMetadata> GetLoadedModels()
        {
            return loadedModels;
        }

        /// <summary>
        /// Get evaluation result for a model
        /// </summary>
        public EvaluationResult GetEvaluationResult(string modelName)
        {
            return evaluationResults.TryGetValue(modelName, out var result) ? result : null;
        }

        /// <summary>
        /// Get last comparison
        /// </summary>
        public ModelComparison GetLastComparison()
        {
            return lastComparison;
        }

        /// <summary>
        /// Export comparison results
        /// </summary>
        public bool ExportComparison(string filePath)
        {
            if (lastComparison == null)
            {
                Debug.LogError("No comparison to export");
                return false;
            }

            try
            {
                string json = JsonUtility.ToJson(lastComparison, true);
                System.IO.File.WriteAllText(filePath, json);
                Debug.Log($"Comparison exported to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to export comparison: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generate performance report
        /// </summary>
        public string GeneratePerformanceReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Model Performance Report ===");
            report.AppendLine($"Generated: {DateTime.Now}");
            report.AppendLine();

            report.AppendLine("Registered Models:");
            foreach (var kvp in loadedModels)
            {
                var model = kvp.Value;
                report.AppendLine($"  - {model.modelName}");
                report.AppendLine($"    Path: {model.path}");
                report.AppendLine($"    Size: {(model.fileSize / (1024.0 * 1024.0)):F2} MB");

                if (evaluationResults.TryGetValue(model.modelName, out var evalResult))
                {
                    report.AppendLine($"    Avg Reward: {evalResult.averageReward:F2}");
                }
            }

            return report.ToString();
        }
    }
}
