using System;
using System.IO;
using UnityEngine;

namespace Vampire.RL.Training
{
    /// <summary>
    /// Manages saving and loading model checkpoints.
    /// Keeps track of best model based on reward/survival metrics.
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        [Header("Checkpoint Settings")]
        [SerializeField] private string checkpointDirectory = "ModelCheckpoints";
        [SerializeField] private bool enableAutoCheckpoint = true;
        [SerializeField] private int checkpointIntervalSteps = 10000;
        [SerializeField] private int maxCheckpointsToKeep = 5;

        private string checkpointPath;
        private CheckpointMetadata bestCheckpoint;
        private int totalCheckpointsSaved = 0;

        public event Action<CheckpointMetadata> OnCheckpointSaved;
        public event Action<CheckpointMetadata> OnBestCheckpointUpdated;

        public void Initialize()
        {
            checkpointPath = Path.Combine(Application.persistentDataPath, checkpointDirectory);
            if (!Directory.Exists(checkpointPath))
            {
                Directory.CreateDirectory(checkpointPath);
            }

            LoadBestCheckpoint();
            Debug.Log($"[Checkpoint Manager] Initialized at {checkpointPath}");
        }

        /// <summary>
        /// Save a checkpoint with metadata.
        /// </summary>
        public void SaveCheckpoint(int step, int episode, float reward, float survivalTime, string modelData = "")
        {
            try
            {
                var metadata = new CheckpointMetadata
                {
                    step = step,
                    episode = episode,
                    timestamp = DateTime.UtcNow,
                    reward = reward,
                    survivalSeconds = survivalTime,
                    checkpointIndex = totalCheckpointsSaved
                };

                string checkpointName = $"checkpoint_{step}_{episode}";
                string checkpointFile = Path.Combine(checkpointPath, $"{checkpointName}.json");
                string metadataFile = Path.Combine(checkpointPath, $"{checkpointName}_meta.json");

                // Save model data
                if (!string.IsNullOrEmpty(modelData))
                {
                    File.WriteAllText(checkpointFile, modelData);
                }

                // Save metadata
                string json = JsonUtility.ToJson(metadata, true);
                File.WriteAllText(metadataFile, json);

                totalCheckpointsSaved++;
                OnCheckpointSaved?.Invoke(metadata);

                // Check if this is best checkpoint
                if (IsBetterCheckpoint(metadata))
                {
                    bestCheckpoint = metadata;
                    SaveBestCheckpoint(checkpointName);
                    OnBestCheckpointUpdated?.Invoke(metadata);
                    Debug.Log($"[Checkpoint Manager] New best checkpoint: step={step}, reward={reward:F2}, survival={survivalTime:F1}s");
                }
                else
                {
                    Debug.Log($"[Checkpoint Manager] Saved checkpoint: step={step}, reward={reward:F2}");
                }

                // Clean old checkpoints if exceeding limit
                CleanOldCheckpoints();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Checkpoint Manager] Failed to save checkpoint: {ex.Message}");
            }
        }

        /// <summary>
        /// Load best checkpoint metadata.
        /// </summary>
        public CheckpointMetadata GetBestCheckpoint()
        {
            return bestCheckpoint;
        }

        /// <summary>
        /// Check if given metrics are better than best checkpoint.
        /// </summary>
        private bool IsBetterCheckpoint(CheckpointMetadata candidate)
        {
            if (bestCheckpoint == null)
                return true;

            // Prioritize survival > reward
            if (candidate.survivalSeconds > bestCheckpoint.survivalSeconds)
                return true;

            if (candidate.survivalSeconds == bestCheckpoint.survivalSeconds && candidate.reward > bestCheckpoint.reward)
                return true;

            return false;
        }

        private void SaveBestCheckpoint(string checkpointName)
        {
            try
            {
                string bestMarkerFile = Path.Combine(checkpointPath, "best_checkpoint.txt");
                File.WriteAllText(bestMarkerFile, checkpointName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Checkpoint Manager] Failed to save best checkpoint marker: {ex.Message}");
            }
        }

        private void LoadBestCheckpoint()
        {
            try
            {
                string bestMarkerFile = Path.Combine(checkpointPath, "best_checkpoint.txt");
                if (File.Exists(bestMarkerFile))
                {
                    string checkpointName = File.ReadAllText(bestMarkerFile).Trim();
                    string metadataFile = Path.Combine(checkpointPath, $"{checkpointName}_meta.json");
                    
                    if (File.Exists(metadataFile))
                    {
                        string json = File.ReadAllText(metadataFile);
                        bestCheckpoint = JsonUtility.FromJson<CheckpointMetadata>(json);
                        Debug.Log($"[Checkpoint Manager] Loaded best checkpoint: step={bestCheckpoint.step}, reward={bestCheckpoint.reward:F2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Checkpoint Manager] Failed to load best checkpoint: {ex.Message}");
            }
        }

        private void CleanOldCheckpoints()
        {
            try
            {
                var files = Directory.GetFiles(checkpointPath, "checkpoint_*_meta.json");
                if (files.Length > maxCheckpointsToKeep)
                {
                    // Sort by modification time (oldest first)
                    System.Array.Sort(files, (a, b) => 
                        File.GetLastWriteTime(a).CompareTo(File.GetLastWriteTime(b)));

                    // Delete oldest
                    for (int i = 0; i < files.Length - maxCheckpointsToKeep; i++)
                    {
                        try
                        {
                            string baseFile = files[i].Replace("_meta.json", ".json");
                            File.Delete(files[i]);
                            if (File.Exists(baseFile))
                                File.Delete(baseFile);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Checkpoint Manager] Failed to clean old checkpoints: {ex.Message}");
            }
        }
    }

    [System.Serializable]
    public class CheckpointMetadata
    {
        public int step;
        public int episode;
        public DateTime timestamp;
        public float reward;
        public float survivalSeconds;
        public int checkpointIndex;
    }
}
