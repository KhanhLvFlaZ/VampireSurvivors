using UnityEngine;
using System;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Training progress monitoring and metrics collection
    /// Tracks episode statistics and model performance
    /// Requirement: 2.3 - Training progress monitoring
    /// </summary>
    public class TrainingProgressDashboard : MonoBehaviour
    {
        [Header("Dashboard Settings")]
        [SerializeField] private bool enableDashboard = true;
        [SerializeField] private int historySize = 100; // Keep last N episodes
        [SerializeField] private float updateInterval = 1f; // Update display every N seconds

        [Header("Display Settings")]
        [SerializeField] private bool showInConsole = true;
        [SerializeField] private bool showOnScreen = true;
        [SerializeField] private bool logToFile = true;
        [SerializeField] private string logFilePath = "Logs/RL_Training.log";

        // Training statistics
        private TrainingSessionData currentSession;
        private Queue<EpisodeData> episodeHistory;
        private Dictionary<int, ModelSnapshot> modelCheckpoints;
        private float timeSinceLastUpdate;

        // Events
        public event Action<TrainingSessionData> OnSessionUpdated;
        public event Action<EpisodeData> OnEpisodeCompleted;

        private void Awake()
        {
            episodeHistory = new Queue<EpisodeData>();
            modelCheckpoints = new Dictionary<int, ModelSnapshot>();
            currentSession = new TrainingSessionData();
        }

        private void OnEnable()
        {
            // Subscribe to training events
            RLTrainingManager trainingManager = FindFirstObjectByType<RLTrainingManager>();
            if (trainingManager != null)
            {
                // Would need to add event subscriptions to RLTrainingManager
            }
        }

        private void Update()
        {
            if (!enableDashboard)
                return;

            timeSinceLastUpdate += Time.deltaTime;

            if (timeSinceLastUpdate >= updateInterval)
            {
                UpdateDisplay();
                timeSinceLastUpdate = 0f;
            }
        }

        /// <summary>
        /// Record episode completion
        /// </summary>
        public void RecordEpisode(int episodeNumber, float totalReward, float averageReward, float duration)
        {
            var episodeData = new EpisodeData
            {
                episodeNumber = episodeNumber,
                totalReward = totalReward,
                averageReward = averageReward,
                duration = duration,
                timestamp = Time.time
            };

            episodeHistory.Enqueue(episodeData);

            // Keep only last N episodes
            if (episodeHistory.Count > historySize)
                episodeHistory.Dequeue();

            // Update session stats
            currentSession.totalEpisodes++;
            currentSession.totalReward += totalReward;
            currentSession.averageReward = currentSession.totalReward / currentSession.totalEpisodes;
            currentSession.lastEpisodeReward = totalReward;
            currentSession.sessionDuration = Time.time - currentSession.sessionStartTime;

            // Notify subscribers
            OnEpisodeCompleted?.Invoke(episodeData);
        }

        /// <summary>
        /// Save model checkpoint
        /// </summary>
        public void SaveCheckpoint(int episodeNumber, string modelPath)
        {
            var snapshot = new ModelSnapshot
            {
                episodeNumber = episodeNumber,
                modelPath = modelPath,
                timestamp = Time.time,
                sessionReward = currentSession.averageReward
            };

            modelCheckpoints[episodeNumber] = snapshot;

            if (showInConsole)
                Debug.Log($"Checkpoint saved at episode {episodeNumber}: {modelPath}");
        }

        /// <summary>
        /// Update display with current statistics
        /// </summary>
        private void UpdateDisplay()
        {
            if (showInConsole)
                DisplayConsoleStats();

            if (logToFile)
                LogStatsToFile();

            OnSessionUpdated?.Invoke(currentSession);
        }

        /// <summary>
        /// Display statistics to console
        /// </summary>
        private void DisplayConsoleStats()
        {
            string stats = GetStatsString();
            Debug.Log($"[RL Training Dashboard]\n{stats}");
        }

        /// <summary>
        /// Log statistics to file
        /// </summary>
        private void LogStatsToFile()
        {
            try
            {
                string directory = System.IO.Path.GetDirectoryName(logFilePath);
                if (!System.IO.Directory.Exists(directory))
                    System.IO.Directory.CreateDirectory(directory);

                string stats = $"[{DateTime.Now}] {GetStatsString()}\n";
                System.IO.File.AppendAllText(logFilePath, stats);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to log stats: {ex.Message}");
            }
        }

        /// <summary>
        /// Get formatted statistics string
        /// </summary>
        private string GetStatsString()
        {
            var stats = new System.Text.StringBuilder();

            stats.AppendLine($"Episodes: {currentSession.totalEpisodes}");
            stats.AppendLine($"Average Reward: {currentSession.averageReward:F2}");
            stats.AppendLine($"Last Reward: {currentSession.lastEpisodeReward:F2}");
            stats.AppendLine($"Session Duration: {currentSession.sessionDuration:F1}s");

            if (episodeHistory.Count > 0)
            {
                float minReward = float.MaxValue;
                float maxReward = float.MinValue;

                foreach (var episode in episodeHistory)
                {
                    minReward = Mathf.Min(minReward, episode.totalReward);
                    maxReward = Mathf.Max(maxReward, episode.totalReward);
                }

                stats.AppendLine($"Reward Range: [{minReward:F2}, {maxReward:F2}]");
            }

            return stats.ToString();
        }

        /// <summary>
        /// Get episode history
        /// </summary>
        public Queue<EpisodeData> GetEpisodeHistory()
        {
            return episodeHistory;
        }

        /// <summary>
        /// Get all checkpoints
        /// </summary>
        public Dictionary<int, ModelSnapshot> GetCheckpoints()
        {
            return modelCheckpoints;
        }

        /// <summary>
        /// Get current session data
        /// </summary>
        public TrainingSessionData GetCurrentSession()
        {
            return currentSession;
        }

        /// <summary>
        /// Reset session
        /// </summary>
        public void ResetSession()
        {
            currentSession = new TrainingSessionData();
            episodeHistory.Clear();
            modelCheckpoints.Clear();

            if (showInConsole)
                Debug.Log("Training session reset");
        }

        /// <summary>
        /// Export statistics to file
        /// </summary>
        public bool ExportStatistics(string filePath)
        {
            try
            {
                var data = new TrainingExportData
                {
                    sessionData = currentSession,
                    episodes = new List<EpisodeData>(episodeHistory),
                    checkpoints = modelCheckpoints
                };

                string json = JsonUtility.ToJson(data, true);
                System.IO.File.WriteAllText(filePath, json);

                Debug.Log($"Statistics exported to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to export statistics: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Training session data
    /// </summary>
    [System.Serializable]
    public class TrainingSessionData
    {
        public int totalEpisodes;
        public float totalReward;
        public float averageReward;
        public float lastEpisodeReward;
        public float sessionDuration;
        public float sessionStartTime = Time.time;
    }

    /// <summary>
    /// Episode data record
    /// </summary>
    [System.Serializable]
    public class EpisodeData
    {
        public int episodeNumber;
        public float totalReward;
        public float averageReward;
        public float duration;
        public float timestamp;
    }

    /// <summary>
    /// Model checkpoint snapshot
    /// </summary>
    [System.Serializable]
    public class ModelSnapshot
    {
        public int episodeNumber;
        public string modelPath;
        public float timestamp;
        public float sessionReward;
    }

    /// <summary>
    /// Data structure for exporting training statistics
    /// </summary>
    [System.Serializable]
    public class TrainingExportData
    {
        public TrainingSessionData sessionData;
        public List<EpisodeData> episodes;
        public Dictionary<int, ModelSnapshot> checkpoints;
    }
}
