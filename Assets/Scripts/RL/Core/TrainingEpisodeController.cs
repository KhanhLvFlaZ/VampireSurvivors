using UnityEngine;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Manages training episodes and episode lifecycle
    /// Coordinates episode resets, termination conditions, and episode metrics
    /// Requirements: 2.1, 2.3
    /// </summary>
    public class TrainingEpisodeController : MonoBehaviour
    {
        [Header("Episode Configuration")]
        [SerializeField] private int maxStepsPerEpisode = 5000;
        [SerializeField] private float maxEpisodeDuration = 300f;
        [SerializeField] private bool autoResetOnTermination = true;

        [Header("Termination Conditions")]
        [SerializeField] private bool terminateOnAllAgentsDead = true;
        [SerializeField] private bool terminateOnTimeLimit = true;
        [SerializeField] private bool terminateOnStepLimit = true;

        [Header("Episode Tracking")]
        [SerializeField] private int currentEpisodeNumber = 0;
        [SerializeField] private int currentStep = 0;
        [SerializeField] private float episodeStartTime = 0f;
        [SerializeField] private bool episodeActive = false;

        // Episode callbacks
        public event System.Action OnEpisodeStart;
        public event System.Action OnEpisodeEnd;
        public event System.Action OnEpisodeReset;

        // Tracked agents
        private List<RLMonsterAgent> episodeAgents = new List<RLMonsterAgent>();
        private Dictionary<RLMonsterAgent, float> agentRewards = new Dictionary<RLMonsterAgent, float>();

        public int CurrentEpisode => currentEpisodeNumber;
        public int CurrentStep => currentStep;
        public bool IsEpisodeActive => episodeActive;
        public float EpisodeDuration => episodeActive ? Time.time - episodeStartTime : 0f;

        /// <summary>
        /// Start a new training episode
        /// </summary>
        public void StartEpisode()
        {
            if (episodeActive)
            {
                Debug.LogWarning("Episode already active. End current episode first.");
                return;
            }

            currentEpisodeNumber++;
            currentStep = 0;
            episodeStartTime = Time.time;
            episodeActive = true;

            // Reset agent rewards
            agentRewards.Clear();
            foreach (var agent in episodeAgents)
            {
                if (agent != null)
                {
                    agentRewards[agent] = 0f;
                }
            }

            OnEpisodeStart?.Invoke();

            Debug.Log($"Episode {currentEpisodeNumber} started with {episodeAgents.Count} agents");
        }

        /// <summary>
        /// End current episode
        /// </summary>
        public void EndEpisode()
        {
            if (!episodeActive)
                return;

            episodeActive = false;

            // Calculate episode statistics
            float totalReward = 0f;
            foreach (var reward in agentRewards.Values)
            {
                totalReward += reward;
            }

            float averageReward = episodeAgents.Count > 0 ? totalReward / episodeAgents.Count : 0f;

            Debug.Log($"Episode {currentEpisodeNumber} ended: " +
                     $"Steps={currentStep}, Duration={EpisodeDuration:F2}s, " +
                     $"Total Reward={totalReward:F2}, Avg Reward={averageReward:F2}");

            OnEpisodeEnd?.Invoke();

            // Auto-reset if enabled
            if (autoResetOnTermination)
            {
                ResetEpisode();
            }
        }

        /// <summary>
        /// Reset episode environment
        /// </summary>
        public void ResetEpisode()
        {
            OnEpisodeReset?.Invoke();

            // Reset all agents
            foreach (var agent in episodeAgents)
            {
                if (agent != null)
                {
                    ResetAgent(agent);
                }
            }
        }

        /// <summary>
        /// Step the episode forward
        /// </summary>
        public void StepEpisode()
        {
            if (!episodeActive)
                return;

            currentStep++;

            // Check termination conditions
            if (ShouldTerminateEpisode())
            {
                EndEpisode();
            }
        }

        private void Update()
        {
            if (episodeActive)
            {
                StepEpisode();
            }
        }

        /// <summary>
        /// Check if episode should terminate
        /// </summary>
        private bool ShouldTerminateEpisode()
        {
            // Step limit
            if (terminateOnStepLimit && currentStep >= maxStepsPerEpisode)
            {
                Debug.Log("Episode terminated: Step limit reached");
                return true;
            }

            // Time limit
            if (terminateOnTimeLimit && EpisodeDuration >= maxEpisodeDuration)
            {
                Debug.Log("Episode terminated: Time limit reached");
                return true;
            }

            // All agents dead
            if (terminateOnAllAgentsDead)
            {
                bool anyAgentAlive = false;
                foreach (var agent in episodeAgents)
                {
                    if (agent != null && agent.gameObject.activeSelf)
                    {
                        anyAgentAlive = true;
                        break;
                    }
                }

                if (!anyAgentAlive)
                {
                    Debug.Log("Episode terminated: All agents dead");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Register agent for episode tracking
        /// </summary>
        public void RegisterAgent(RLMonsterAgent agent)
        {
            if (agent == null || episodeAgents.Contains(agent))
                return;

            episodeAgents.Add(agent);
            agentRewards[agent] = 0f;

            Debug.Log($"Agent {agent.name} registered for episode tracking");
        }

        /// <summary>
        /// Unregister agent
        /// </summary>
        public void UnregisterAgent(RLMonsterAgent agent)
        {
            if (agent == null)
                return;

            episodeAgents.Remove(agent);
            agentRewards.Remove(agent);

            Debug.Log($"Agent {agent.name} unregistered from episode tracking");
        }

        /// <summary>
        /// Record reward for an agent
        /// </summary>
        public void RecordReward(RLMonsterAgent agent, float reward)
        {
            if (agent == null || !agentRewards.ContainsKey(agent))
                return;

            agentRewards[agent] += reward;
        }

        /// <summary>
        /// Get total reward for an agent in current episode
        /// </summary>
        public float GetAgentReward(RLMonsterAgent agent)
        {
            return agentRewards.ContainsKey(agent) ? agentRewards[agent] : 0f;
        }

        /// <summary>
        /// Reset a single agent
        /// </summary>
        private void ResetAgent(RLMonsterAgent agent)
        {
            // Reset position
            agent.transform.position = GetRandomSpawnPosition();

            // Reset health (if applicable)
            // agent.ResetHealth();

            // Reset state
            agent.gameObject.SetActive(true);
        }

        /// <summary>
        /// Get random spawn position
        /// </summary>
        private Vector2 GetRandomSpawnPosition()
        {
            float radius = 15f;
            return new Vector2(
                Random.Range(-radius, radius),
                Random.Range(-radius, radius)
            );
        }

        /// <summary>
        /// Get episode progress (0 to 1)
        /// </summary>
        public float GetEpisodeProgress()
        {
            if (!episodeActive)
                return 0f;

            float stepProgress = maxStepsPerEpisode > 0 ? (float)currentStep / maxStepsPerEpisode : 0f;
            float timeProgress = maxEpisodeDuration > 0 ? EpisodeDuration / maxEpisodeDuration : 0f;

            return Mathf.Max(stepProgress, timeProgress);
        }

        /// <summary>
        /// Get remaining steps in episode
        /// </summary>
        public int GetRemainingSteps()
        {
            return Mathf.Max(0, maxStepsPerEpisode - currentStep);
        }

        /// <summary>
        /// Get remaining time in episode
        /// </summary>
        public float GetRemainingTime()
        {
            return Mathf.Max(0f, maxEpisodeDuration - EpisodeDuration);
        }

        /// <summary>
        /// Force end episode
        /// </summary>
        public void ForceEndEpisode(string reason = "")
        {
            if (!episodeActive)
                return;

            Debug.Log($"Episode force-ended: {reason}");
            EndEpisode();
        }
    }
}
