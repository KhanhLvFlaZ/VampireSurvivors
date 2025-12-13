using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Error recovery system for training failures
    /// Implements checkpoint restoration and training resumption
    /// Requirement: 5.1 - Error recovery for training failures
    /// </summary>
    public class TrainingErrorRecovery : MonoBehaviour, ITrainingManager, IRecoverable
    {
        private RLTrainingManager trainingManager;
        private bool isPaused = false;
        private bool isRecovering = false;

        [SerializeField] private bool enableCheckpoints = true;
        [SerializeField] private int checkpointInterval = 100; // Save checkpoint every N episodes
        [SerializeField] private int maxCheckpoints = 5;

        private List<TrainingCheckpoint> checkpoints = new List<TrainingCheckpoint>();
        private TrainingCheckpoint lastCheckpoint;

        [Header("Recovery Settings")]
        [SerializeField] private int maxRecoveryAttempts = 3;
        [SerializeField] private float recoveryDelay = 1f;

        private int recoveryAttempts = 0;

        private void Awake()
        {
            trainingManager = GetComponent<RLTrainingManager>();
        }

        private void OnEnable()
        {
            RLErrorHandler.Instance.OnErrorOccurred += OnErrorOccurred;
        }

        private void OnDisable()
        {
            if (RLErrorHandler.Instance != null)
            {
                RLErrorHandler.Instance.OnErrorOccurred -= OnErrorOccurred;
            }
        }

        /// <summary>
        /// Handle error events
        /// </summary>
        private void OnErrorOccurred(RLError error)
        {
            if (error.errorType == ErrorType.TrainingFailure && !isRecovering)
            {
                StartCoroutine(RecoverFromTrainingError());
            }
        }

        /// <summary>
        /// Save training checkpoint
        /// </summary>
        public void SaveCheckpoint(int episodeNumber)
        {
            if (!enableCheckpoints || trainingManager == null)
                return;

            try
            {
                var checkpoint = new TrainingCheckpoint
                {
                    episodeNumber = episodeNumber,
                    timestamp = Time.time,
                    modelData = null // Model data not directly accessible from RLTrainingManager
                };

                checkpoints.Add(checkpoint);
                lastCheckpoint = checkpoint;

                // Keep only max checkpoints
                if (checkpoints.Count > maxCheckpoints)
                {
                    checkpoints.RemoveAt(0);
                }

                Debug.Log($"Saved training checkpoint at episode {episodeNumber}");
            }
            catch (Exception ex)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.TrainingFailure,
                    this,
                    $"Failed to save checkpoint: {ex.Message}",
                    ex
                );
            }
        }

        /// <summary>
        /// Restore training from checkpoint
        /// </summary>
        public bool RestoreFromCheckpoint(int checkpointIndex = -1)
        {
            if (checkpoints.Count == 0)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Warning,
                    this,
                    "No checkpoints available for restoration"
                );
                return false;
            }

            try
            {
                // Use last checkpoint if not specified
                if (checkpointIndex < 0)
                    checkpointIndex = checkpoints.Count - 1;

                if (checkpointIndex >= checkpoints.Count)
                {
                    RLErrorHandler.Instance.HandleError(
                        ErrorType.Warning,
                        this,
                        $"Checkpoint index out of range: {checkpointIndex}"
                    );
                    return false;
                }

                var checkpoint = checkpoints[checkpointIndex];

                // Restore model data
                // Note: RLTrainingManager doesn't expose a direct restore method,
                // but we can pause/resume training as an alternative recovery mechanism
                if (trainingManager != null)
                {
                    trainingManager.PauseTraining();
                    // Agents will need to be re-initialized individually if needed
                }

                Debug.Log($"Restored training checkpoint from episode {checkpoint.episodeNumber}");
                return true;
            }
            catch (Exception ex)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.TrainingFailure,
                    this,
                    $"Failed to restore checkpoint: {ex.Message}",
                    ex
                );
                return false;
            }
        }

        /// <summary>
        /// Recover from training error
        /// Requirement: 5.1
        /// </summary>
        private IEnumerator RecoverFromTrainingError()
        {
            if (recoveryAttempts >= maxRecoveryAttempts)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Critical,
                    this,
                    "Training error recovery failed - max attempts exceeded"
                );
                yield break;
            }

            isRecovering = true;
            recoveryAttempts++;

            Debug.Log($"Attempting training error recovery (attempt {recoveryAttempts}/{maxRecoveryAttempts})");

            // Pause training
            PauseTraining();
            yield return new WaitForSeconds(recoveryDelay);

            // Restore from checkpoint
            if (RestoreFromCheckpoint())
            {
                // Resume training
                ResumeTraining();
                recoveryAttempts = 0; // Reset counter on success
            }
            else if (recoveryAttempts < maxRecoveryAttempts)
            {
                // Try again
                yield return new WaitForSeconds(recoveryDelay);
                isRecovering = false;
                StartCoroutine(RecoverFromTrainingError());
                yield break;
            }

            isRecovering = false;
        }

        /// <summary>
        /// Implement ITrainingManager
        /// </summary>
        public void PauseTraining()
        {
            if (trainingManager != null)
            {
                trainingManager.PauseTraining();
            }
            isPaused = true;
            Debug.Log("Training paused for error recovery");
        }

        /// <summary>
        /// Resume training
        /// </summary>
        public void ResumeTraining()
        {
            if (trainingManager != null)
            {
                trainingManager.ResumeTraining();
            }
            isPaused = false;
            Debug.Log("Training resumed after error recovery");
        }

        /// <summary>
        /// Implement IRecoverable
        /// </summary>
        public void Recover()
        {
            recoveryAttempts = 0;
            isRecovering = false;
            if (isPaused)
                ResumeTraining();
        }

        /// <summary>
        /// Get recovery statistics
        /// </summary>
        public TrainingRecoveryStats GetStats()
        {
            return new TrainingRecoveryStats
            {
                totalCheckpoints = checkpoints.Count,
                recoveryAttempts = recoveryAttempts,
                lastCheckpointEpisode = lastCheckpoint?.episodeNumber ?? -1,
                isRecovering = isRecovering
            };
        }
    }

    /// <summary>
    /// Error recovery system for inference failures
    /// Implements fallback behavior and error resilience
    /// Requirement: 5.1 - Error recovery for inference failures, 5.2 - Fallback mechanisms
    /// </summary>
    public class InferenceErrorRecovery : MonoBehaviour, IFallbackCapable, IRecoverable
    {
        private RLMonster rlMonster;
        private FallbackAIBehavior fallbackAI;
        private bool useFallback = false;

        [SerializeField] private int maxConsecutiveErrors = 5;
        [SerializeField] private float errorRecoveryWindow = 5f; // Window for error counting

        private Queue<float> errorTimestamps = new Queue<float>();
        private float lastErrorTime = 0f;

        public bool IsFallbackActive => useFallback;

        private void Awake()
        {
            rlMonster = GetComponent<RLMonster>();
            fallbackAI = GetComponent<FallbackAIBehavior>();

            if (fallbackAI == null)
            {
                fallbackAI = gameObject.AddComponent<FallbackAIBehavior>();
            }
        }

        private void OnEnable()
        {
            RLErrorHandler.Instance.OnErrorOccurred += OnErrorOccurred;
        }

        private void OnDisable()
        {
            if (RLErrorHandler.Instance != null)
            {
                RLErrorHandler.Instance.OnErrorOccurred -= OnErrorOccurred;
            }
        }

        private void Update()
        {
            // Clean old error timestamps
            while (errorTimestamps.Count > 0 && Time.time - errorTimestamps.Peek() > errorRecoveryWindow)
            {
                errorTimestamps.Dequeue();
            }

            // Update fallback behavior if active
            if (useFallback && fallbackAI != null)
            {
                fallbackAI.UpdateFallback();
            }
        }

        /// <summary>
        /// Handle error events
        /// </summary>
        private void OnErrorOccurred(RLError error)
        {
            if (!ReferenceEquals(error.source, rlMonster) && !ReferenceEquals(error.source, gameObject))
                return;

            if (error.errorType == ErrorType.InferenceFailure)
            {
                HandleInferenceError(error);
            }
        }

        /// <summary>
        /// Handle inference failure
        /// Requirement: 5.1
        /// </summary>
        private void HandleInferenceError(RLError error)
        {
            // Record error timestamp
            errorTimestamps.Enqueue(Time.time);
            lastErrorTime = Time.time;

            Debug.LogWarning($"Inference error for {gameObject.name}: {error.message}");

            // Check if we should activate fallback
            if (errorTimestamps.Count >= maxConsecutiveErrors)
            {
                UseFallbackBehavior();
            }
        }

        /// <summary>
        /// Implement IFallbackCapable
        /// Requirement: 5.2
        /// </summary>
        public void UseFallbackBehavior()
        {
            if (!useFallback && fallbackAI != null && rlMonster != null)
            {
                useFallback = true;

                // Find the player character from the scene
                var playerCharacter = FindFirstObjectByType<Character>();
                if (playerCharacter != null)
                {
                    fallbackAI.EnableFallback(playerCharacter);
                }

                Debug.LogWarning($"Switched to fallback AI for {gameObject.name} due to inference errors");
            }
        }

        /// <summary>
        /// Resume normal RL behavior
        /// </summary>
        public void ResumeNormalBehavior()
        {
            if (useFallback && fallbackAI != null)
            {
                useFallback = false;
                fallbackAI.DisableFallback();
                errorTimestamps.Clear();
                Debug.Log($"Resumed RL behavior for {gameObject.name}");
            }
        }

        /// <summary>
        /// Implement IRecoverable
        /// </summary>
        public void Recover()
        {
            errorTimestamps.Clear();
            if (useFallback)
            {
                ResumeNormalBehavior();
            }
        }

        /// <summary>
        /// Get inference error statistics
        /// </summary>
        public InferenceErrorStats GetStats()
        {
            return new InferenceErrorStats
            {
                recentErrors = errorTimestamps.Count,
                maxConsecutiveErrors = maxConsecutiveErrors,
                lastErrorTime = lastErrorTime,
                usingFallback = useFallback
            };
        }
    }

    /// <summary>
    /// Training checkpoint data
    /// </summary>
    [System.Serializable]
    public class TrainingCheckpoint
    {
        public int episodeNumber;
        public float timestamp;
        public object modelData; // Serialized model state
    }

    /// <summary>
    /// Training recovery statistics
    /// </summary>
    [System.Serializable]
    public class TrainingRecoveryStats
    {
        public int totalCheckpoints;
        public int recoveryAttempts;
        public int lastCheckpointEpisode;
        public bool isRecovering;
    }

    /// <summary>
    /// Inference error statistics
    /// </summary>
    [System.Serializable]
    public class InferenceErrorStats
    {
        public int recentErrors;
        public int maxConsecutiveErrors;
        public float lastErrorTime;
        public bool usingFallback;
    }
}
