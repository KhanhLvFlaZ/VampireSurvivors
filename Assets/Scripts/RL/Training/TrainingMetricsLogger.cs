using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Vampire;

namespace Vampire.RL.Training
{
    /// <summary>
    /// Centralized logging for training metrics.
    /// Tracks reward curve, metrics per step/episode, hyperparameters, and generates JSON/CSV exports.
    /// </summary>
    public class TrainingMetricsLogger : MonoBehaviour
    {
        [Header("Logging Settings")]
        [SerializeField] private bool enableLogging = true;
        [SerializeField] private int stepsBetweenEval = 1000; // Evaluation every N steps
        [SerializeField] private float evalIntervalSeconds = 60f; // Evaluation every N seconds
        [SerializeField] private string logDirectory = "TrainingLogs";
        [SerializeField] private bool autoExportJson = true;
        [SerializeField] private bool autoExportCsv = true;

        // Session metadata
        private string sessionId;
        private int rngSeed;
        private TrainingConfig trainingConfig;
        private DateTime sessionStartTime;
        private string sessionLogPath;

        // Metrics tracking
        private List<StepMetrics> stepMetrics = new List<StepMetrics>();
        private List<EpisodeMetrics> episodeMetrics = new List<EpisodeMetrics>();
        private List<EvaluationMetrics> evaluationMetrics = new List<EvaluationMetrics>();

        // State tracking
        private int currentStep;
        private int currentEpisode;
        private float cumulativeReward;
        private float lastEvalTime;
        private int lastEvalStep;

        public void Initialize(int seed, TrainingConfig config)
        {
            if (!enableLogging) return;

            sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            rngSeed = seed;
            trainingConfig = config;
            sessionStartTime = DateTime.UtcNow;

            // Create session log directory
            string baseLogDir = Path.Combine(Application.persistentDataPath, logDirectory);
            sessionLogPath = Path.Combine(baseLogDir, $"session_{sessionId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(sessionLogPath);

            lastEvalTime = Time.time;
            lastEvalStep = 0;

            Debug.Log($"[Training Logger] Session {sessionId} initialized at {sessionLogPath}");
        }

        /// <summary>
        /// Record metrics for a single training step.
        /// </summary>
        public void LogStep(float reward, float loss, int activeAgents)
        {
            if (!enableLogging) return;

            currentStep++;
            cumulativeReward += reward;

            var metrics = new StepMetrics
            {
                step = currentStep,
                timestamp = DateTime.UtcNow,
                reward = reward,
                cumulativeReward = cumulativeReward,
                loss = loss,
                activeAgents = activeAgents,
                learningRate = trainingConfig.learningRate > 0 ? trainingConfig.learningRate : 0.001f,
                batchSize = trainingConfig.batchSize > 0 ? trainingConfig.batchSize : 32
            };

            stepMetrics.Add(metrics);

            // Check if we should evaluate
            if (ShouldEvaluate())
            {
                TriggerEvaluation();
            }
        }

        /// <summary>
        /// Record metrics for a completed episode.
        /// </summary>
        public void LogEpisode(float episodeReward, float episodeLength, Dictionary<MonsterType, LearningMetrics> allMetrics)
        {
            if (!enableLogging) return;

            currentEpisode++;

            // Convert metrics dictionary to serializable format
            var metricsByType = new Dictionary<string, EpisodeMonsterMetrics>();
            foreach (var kvp in allMetrics)
            {
                var metrics = kvp.Value;
                metricsByType[kvp.Key.ToString()] = new EpisodeMonsterMetrics
                {
                    monsterType = kvp.Key.ToString(),
                    episodeCount = metrics.episodeCount,
                    averageReward = metrics.averageReward,
                    bestReward = metrics.bestReward,
                    recentAverageReward = metrics.recentAverageReward,
                    explorationRate = metrics.explorationRate,
                    survivalRate = metrics.survivalRate,
                    totalSteps = metrics.totalSteps,
                    lossValue = metrics.lossValue
                };
            }

            var episodeLog = new EpisodeMetrics
            {
                episode = currentEpisode,
                timestamp = DateTime.UtcNow,
                reward = episodeReward,
                length = episodeLength,
                averageRewardPerStep = episodeLength > 0 ? episodeReward / episodeLength : 0,
                monsterMetrics = metricsByType
            };

            episodeMetrics.Add(episodeLog);
        }

        /// <summary>
        /// Record evaluation run metrics.
        /// </summary>
        public void LogEvaluation(float evalReward, float survivalTime, int kills, float avgFps, float p99FrameTime)
        {
            if (!enableLogging) return;

            var evalMetrics = new EvaluationMetrics
            {
                evaluationNumber = evaluationMetrics.Count + 1,
                timestamp = DateTime.UtcNow,
                stepAtEvaluation = currentStep,
                episodeAtEvaluation = currentEpisode,
                averageReward = evalReward,
                survivalSeconds = survivalTime,
                killCount = kills,
                averageFps = avgFps,
                p99FrameTimeMs = p99FrameTime
            };

            evaluationMetrics.Add(evalMetrics);

            lastEvalTime = Time.time;
            lastEvalStep = currentStep;

            Debug.Log($"[Training Logger] Evaluation #{evalMetrics.evaluationNumber}: reward={evalReward:F2}, survival={survivalTime:F1}s, kills={kills}, fps={avgFps:F1}, p99={p99FrameTime:F2}ms");
        }

        /// <summary>
        /// Export all collected metrics to JSON and CSV.
        /// </summary>
        public void ExportMetrics()
        {
            if (!enableLogging || string.IsNullOrEmpty(sessionLogPath))
                return;

            try
            {
                var sessionData = new SessionExport
                {
                    sessionId = sessionId,
                    seed = rngSeed,
                    startTime = sessionStartTime,
                    endTime = DateTime.UtcNow,
                    totalSteps = currentStep,
                    totalEpisodes = currentEpisode,
                    trainingConfig = trainingConfig,
                    stepMetrics = stepMetrics,
                    episodeMetrics = episodeMetrics,
                    evaluationMetrics = evaluationMetrics
                };

                if (autoExportJson)
                    ExportToJson(sessionData);

                if (autoExportCsv)
                {
                    ExportStepsCsv();
                    ExportEpisodesCsv();
                    ExportEvaluationsCsv();
                }

                Debug.Log($"[Training Logger] Metrics exported to {sessionLogPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Training Logger] Failed to export metrics: {ex.Message}");
            }
        }

        private void ExportToJson(SessionExport data)
        {
            string jsonPath = Path.Combine(sessionLogPath, "session_summary.json");

            // Serialize to JSON-friendly format (Unity's JsonUtility doesn't serialize dicts)
            var summaryText = $@"{{
  ""sessionId"": ""{data.sessionId}"",
  ""seed"": {data.seed},
  ""startTime"": ""{data.startTime:O}"",
  ""endTime"": ""{data.endTime:O}"",
  ""totalSteps"": {data.totalSteps},
  ""totalEpisodes"": {data.totalEpisodes},
  ""trainingConfig"": {{
    ""learningRate"": {data.trainingConfig.learningRate},
    ""batchSize"": {data.trainingConfig.batchSize},
    ""discountFactor"": {data.trainingConfig.discountFactor},
    ""entropyBonus"": {data.trainingConfig.entropyBonus},
    ""algorithm"": ""{data.trainingConfig.algorithm}"",
    ""networkArchitecture"": ""{data.trainingConfig.networkArchitecture}""
  }},
  ""stepMetricsCount"": {data.stepMetrics.Count},
  ""episodeMetricsCount"": {data.episodeMetrics.Count},
  ""evaluationMetricsCount"": {data.evaluationMetrics.Count}
}}";

            File.WriteAllText(jsonPath, summaryText);
        }

        private void ExportStepsCsv()
        {
            string csvPath = Path.Combine(sessionLogPath, "step_metrics.csv");
            using (var writer = new StreamWriter(csvPath))
            {
                writer.WriteLine("Step,Timestamp,Reward,CumulativeReward,Loss,ActiveAgents,LearningRate,BatchSize");
                foreach (var metric in stepMetrics)
                {
                    writer.WriteLine($"{metric.step},{metric.timestamp:O},{metric.reward:F6},{metric.cumulativeReward:F6},{metric.loss:F6},{metric.activeAgents},{metric.learningRate:F6},{metric.batchSize}");
                }
            }
        }

        private void ExportEpisodesCsv()
        {
            string csvPath = Path.Combine(sessionLogPath, "episode_metrics.csv");
            using (var writer = new StreamWriter(csvPath))
            {
                writer.WriteLine("Episode,Timestamp,Reward,Length,AvgRewardPerStep");
                foreach (var metric in episodeMetrics)
                {
                    writer.WriteLine($"{metric.episode},{metric.timestamp:O},{metric.reward:F6},{metric.length:F2},{metric.averageRewardPerStep:F6}");
                }
            }
        }

        private void ExportEvaluationsCsv()
        {
            string csvPath = Path.Combine(sessionLogPath, "evaluation_metrics.csv");
            using (var writer = new StreamWriter(csvPath))
            {
                writer.WriteLine("EvalNumber,Timestamp,StepAtEval,EpisodeAtEval,AvgReward,SurvivalSeconds,Kills,AvgFps,P99FrameTimeMs");
                foreach (var metric in evaluationMetrics)
                {
                    writer.WriteLine($"{metric.evaluationNumber},{metric.timestamp:O},{metric.stepAtEvaluation},{metric.episodeAtEvaluation},{metric.averageReward:F2},{metric.survivalSeconds:F1},{metric.killCount},{metric.averageFps:F1},{metric.p99FrameTimeMs:F2}");
                }
            }
        }

        private bool ShouldEvaluate()
        {
            bool bySteps = (currentStep - lastEvalStep) >= stepsBetweenEval && stepsBetweenEval > 0;
            bool byTime = (Time.time - lastEvalTime) >= evalIntervalSeconds && evalIntervalSeconds > 0;
            return bySteps || byTime;
        }

        private void TriggerEvaluation()
        {
            // Placeholder: actual evaluation triggered by external system
            // This is called to mark that evaluation should happen
        }

        private void OnDestroy()
        {
            if (enableLogging)
            {
                ExportMetrics();
            }
        }
    }

    [Serializable]
    public struct TrainingConfig
    {
        public float learningRate;
        public int batchSize;
        public float discountFactor;
        public float entropyBonus;
        public string algorithm;
        public string networkArchitecture;
    }

    [Serializable]
    public class StepMetrics
    {
        public int step;
        public DateTime timestamp;
        public float reward;
        public float cumulativeReward;
        public float loss;
        public int activeAgents;
        public float learningRate;
        public int batchSize;
    }

    [Serializable]
    public class EpisodeMetrics
    {
        public int episode;
        public DateTime timestamp;
        public float reward;
        public float length;
        public float averageRewardPerStep;
        public Dictionary<string, EpisodeMonsterMetrics> monsterMetrics;
    }

    [Serializable]
    public class EpisodeMonsterMetrics
    {
        public string monsterType;
        public int episodeCount;
        public float averageReward;
        public float bestReward;
        public float recentAverageReward;
        public float explorationRate;
        public float survivalRate;
        public int totalSteps;
        public float lossValue;
    }

    [Serializable]
    public class EvaluationMetrics
    {
        public int evaluationNumber;
        public DateTime timestamp;
        public int stepAtEvaluation;
        public int episodeAtEvaluation;
        public float averageReward;
        public float survivalSeconds;
        public int killCount;
        public float averageFps;
        public float p99FrameTimeMs;
    }

    [Serializable]
    public class SessionExport
    {
        public string sessionId;
        public int seed;
        public DateTime startTime;
        public DateTime endTime;
        public int totalSteps;
        public int totalEpisodes;
        public TrainingConfig trainingConfig;
        public List<StepMetrics> stepMetrics;
        public List<EpisodeMetrics> episodeMetrics;
        public List<EvaluationMetrics> evaluationMetrics;
    }
}
