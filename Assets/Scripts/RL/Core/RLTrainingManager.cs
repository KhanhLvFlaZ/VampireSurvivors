using UnityEngine;
using System;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Manages offline training sessions for RL agents
    /// Handles episode management, training coordination, and metrics collection
    /// Requirements: 2.1, 2.2, 2.3, 2.4, 2.5
    /// </summary>
    public class RLTrainingManager : MonoBehaviour
    {
        [Header("Training Configuration")]
        [SerializeField] private int maxEpisodes = 1000;
        [SerializeField] private int maxStepsPerEpisode = 5000;
        [SerializeField] private int trainingUpdateFrequency = 4;
        [SerializeField] private bool saveCheckpoints = true;
        [SerializeField] private int checkpointInterval = 100;

        [Header("Training Agents")]
        [SerializeField] private List<RLMonsterAgent> trainingAgents = new List<RLMonsterAgent>();

        [Header("Episode Management")]
        [SerializeField] private float episodeTimeLimit = 300f; // 5 minutes
        [SerializeField] private bool autoResetOnEpisodeEnd = true;

        [Header("Metrics")]
        [SerializeField] private bool collectDetailedMetrics = true;
        [SerializeField] private int metricsLogInterval = 10;

        // Training state
        private bool isTraining = false;
        private int currentEpisode = 0;
        private int currentStep = 0;
        private float episodeStartTime = 0f;

        // Episode metrics
        private TrainingEpisodeMetrics currentEpisodeMetrics;
        private List<TrainingEpisodeMetrics> episodeHistory = new List<TrainingEpisodeMetrics>();

        // Training statistics
        private float totalRewardSum = 0f;
        private int totalSteps = 0;
        private int successfulEpisodes = 0;

        public bool IsTraining => isTraining;
        public int CurrentEpisode => currentEpisode;
        public int CurrentStep => currentStep;
        public TrainingEpisodeMetrics CurrentMetrics => currentEpisodeMetrics;

        private void Awake()
        {
            currentEpisodeMetrics = new TrainingEpisodeMetrics();
        }

        /// <summary>
        /// Start training session
        /// </summary>
        public void StartTraining()
        {
            if (isTraining)
            {
                Debug.LogWarning("Training already in progress");
                return;
            }

            if (trainingAgents.Count == 0)
            {
                Debug.LogError("No training agents assigned");
                return;
            }

            isTraining = true;
            currentEpisode = 0;
            totalSteps = 0;
            successfulEpisodes = 0;
            episodeHistory.Clear();

            Debug.Log($"Starting training session with {trainingAgents.Count} agents");
            Debug.Log($"Configuration: {maxEpisodes} episodes, {maxStepsPerEpisode} steps/episode");

            StartNewEpisode();
        }

        /// <summary>
        /// Stop training session
        /// </summary>
        public void StopTraining()
        {
            if (!isTraining)
                return;

            isTraining = false;
            EndCurrentEpisode();

            Debug.Log($"Training stopped at episode {currentEpisode}");
            LogTrainingStatistics();
        }

        /// <summary>
        /// Pause/Resume training
        /// </summary>
        public void PauseTraining()
        {
            isTraining = false;
        }

        public void ResumeTraining()
        {
            isTraining = true;
        }

        private void Update()
        {
            if (!isTraining)
                return;

            currentStep++;
            totalSteps++;

            // Update episode metrics
            currentEpisodeMetrics.steps = currentStep;
            currentEpisodeMetrics.duration = Time.time - episodeStartTime;

            // Check episode end conditions
            if (ShouldEndEpisode())
            {
                EndCurrentEpisode();

                if (currentEpisode < maxEpisodes)
                {
                    StartNewEpisode();
                }
                else
                {
                    CompleteTraining();
                }
            }

            // Periodic training updates
            if (currentStep % trainingUpdateFrequency == 0)
            {
                UpdateAgentPolicies();
            }

            // Checkpoint saving
            if (saveCheckpoints && currentEpisode % checkpointInterval == 0 && currentStep == 1)
            {
                SaveCheckpoint();
            }

            // Metrics logging
            if (collectDetailedMetrics && currentStep % metricsLogInterval == 0)
            {
                LogStepMetrics();
            }
        }

        /// <summary>
        /// Start a new training episode
        /// </summary>
        private void StartNewEpisode()
        {
            currentEpisode++;
            currentStep = 0;
            episodeStartTime = Time.time;

            // Reset episode metrics
            currentEpisodeMetrics = new TrainingEpisodeMetrics
            {
                episodeNumber = currentEpisode,
                startTime = episodeStartTime
            };

            // ML-Agents training is managed externally; agents run inference/training via BehaviorParameters.

            if (autoResetOnEpisodeEnd)
            {
                ResetEnvironment();
            }

            Debug.Log($"Episode {currentEpisode}/{maxEpisodes} started");
        }

        /// <summary>
        /// End current episode and collect metrics
        /// </summary>
        private void EndCurrentEpisode()
        {
            currentEpisodeMetrics.endTime = Time.time;
            currentEpisodeMetrics.duration = currentEpisodeMetrics.endTime - currentEpisodeMetrics.startTime;
            currentEpisodeMetrics.steps = currentStep;

            // Collect rewards from agents
            float episodeReward = 0f;
            foreach (var agent in trainingAgents)
            {
                if (agent != null)
                {
                    // This would get cumulative reward from the agent
                    episodeReward += 0f; // Placeholder - agents would track their own rewards
                }
            }

            currentEpisodeMetrics.totalReward = episodeReward;
            currentEpisodeMetrics.averageReward = currentStep > 0 ? episodeReward / currentStep : 0f;

            // Determine success
            currentEpisodeMetrics.success = DetermineEpisodeSuccess();
            if (currentEpisodeMetrics.success)
            {
                successfulEpisodes++;
            }

            episodeHistory.Add(currentEpisodeMetrics);
            totalRewardSum += episodeReward;

            // Log episode summary
            Debug.Log($"Episode {currentEpisode} complete: " +
                     $"Steps={currentStep}, Reward={episodeReward:F2}, " +
                     $"Success={currentEpisodeMetrics.success}");
        }

        /// <summary>
        /// Complete training session
        /// </summary>
        private void CompleteTraining()
        {
            isTraining = false;

            Debug.Log("=== Training Complete ===");
            LogTrainingStatistics();

            // Save final model
            if (saveCheckpoints)
            {
                SaveFinalModel();
            }
        }

        /// <summary>
        /// Check if episode should end
        /// </summary>
        private bool ShouldEndEpisode()
        {
            // Max steps reached
            if (currentStep >= maxStepsPerEpisode)
                return true;

            // Time limit exceeded
            if (Time.time - episodeStartTime >= episodeTimeLimit)
                return true;

            // All agents terminated (check if any agent is done)
            bool allAgentsDone = true;
            foreach (var agent in trainingAgents)
            {
                if (agent != null && agent.gameObject.activeSelf)
                {
                    allAgentsDone = false;
                    break;
                }
            }

            return allAgentsDone;
        }

        /// <summary>
        /// Update agent policies (trigger learning)
        /// </summary>
        private void UpdateAgentPolicies()
        {
            // Policy updates occur via ML-Agents trainer; no per-agent UpdatePolicy calls.
        }

        /// <summary>
        /// Reset training environment
        /// </summary>
        private void ResetEnvironment()
        {
            // Reset agents to initial positions
            foreach (var agent in trainingAgents)
            {
                if (agent != null)
                {
                    agent.transform.position = GetRandomSpawnPosition();
                    agent.gameObject.SetActive(true);
                }
            }

            // Additional environment reset logic would go here
        }

        /// <summary>
        /// Determine if episode was successful
        /// </summary>
        private bool DetermineEpisodeSuccess()
        {
            // Success criteria - can be customized
            // For now, based on if agents are still alive and achieved minimum reward
            int aliveAgents = 0;
            foreach (var agent in trainingAgents)
            {
                if (agent != null && agent.gameObject.activeSelf)
                {
                    aliveAgents++;
                }
            }

            return aliveAgents > 0 && currentEpisodeMetrics.totalReward > 0;
        }

        /// <summary>
        /// Save training checkpoint
        /// </summary>
        private void SaveCheckpoint()
        {
            Debug.Log($"Saving checkpoint at episode {currentEpisode}");

            // Model saving handled by ML-Agents training outputs (ONNX).
        }

        /// <summary>
        /// Save final trained model
        /// </summary>
        private void SaveFinalModel()
        {
            Debug.Log("Saving final trained models");

            // Final model saving handled externally.
        }

        /// <summary>
        /// Log step-level metrics
        /// </summary>
        private void LogStepMetrics()
        {
            if (!collectDetailedMetrics)
                return;

            // Log current step information
            // This would be more detailed in production
        }

        /// <summary>
        /// Log overall training statistics
        /// </summary>
        private void LogTrainingStatistics()
        {
            float averageReward = episodeHistory.Count > 0 ? totalRewardSum / episodeHistory.Count : 0f;
            float successRate = episodeHistory.Count > 0 ? (float)successfulEpisodes / episodeHistory.Count : 0f;

            Debug.Log("=== Training Statistics ===");
            Debug.Log($"Total Episodes: {episodeHistory.Count}");
            Debug.Log($"Total Steps: {totalSteps}");
            Debug.Log($"Average Reward: {averageReward:F2}");
            Debug.Log($"Success Rate: {successRate:P2}");
            Debug.Log($"Successful Episodes: {successfulEpisodes}/{episodeHistory.Count}");
        }

        /// <summary>
        /// Get random spawn position for agent
        /// </summary>
        private Vector2 GetRandomSpawnPosition()
        {
            // Simple random position - would be more sophisticated in production
            float radius = 10f;
            return new Vector2(
                UnityEngine.Random.Range(-radius, radius),
                UnityEngine.Random.Range(-radius, radius)
            );
        }

        /// <summary>
        /// Add agent to training session
        /// </summary>
        public void AddTrainingAgent(RLMonsterAgent agent)
        {
            if (!trainingAgents.Contains(agent))
            {
                trainingAgents.Add(agent);
                Debug.Log($"Added agent {agent.name} to training session");
            }
        }

        /// <summary>
        /// Remove agent from training session
        /// </summary>
        public void RemoveTrainingAgent(RLMonsterAgent agent)
        {
            if (trainingAgents.Contains(agent))
            {
                trainingAgents.Remove(agent);
                Debug.Log($"Removed agent {agent.name} from training session");
            }
        }

        /// <summary>
        /// Get episode history
        /// </summary>
        public List<TrainingEpisodeMetrics> GetEpisodeHistory()
        {
            return new List<TrainingEpisodeMetrics>(episodeHistory);
        }

        /// <summary>
        /// Get training progress percentage
        /// </summary>
        public float GetTrainingProgress()
        {
            return maxEpisodes > 0 ? (float)currentEpisode / maxEpisodes : 0f;
        }
    }

    /// <summary>
    /// Metrics for a single training episode
    /// </summary>
    [Serializable]
    public class TrainingEpisodeMetrics
    {
        public int episodeNumber;
        public float startTime;
        public float endTime;
        public float duration;
        public int steps;
        public float totalReward;
        public float averageReward;
        public bool success;
        public Dictionary<string, float> customMetrics = new Dictionary<string, float>();
    }
}
