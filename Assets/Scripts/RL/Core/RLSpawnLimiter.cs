using UnityEngine;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Controls RL agent spawning based on inference cost and performance constraints
    /// Ensures system stays within target latency by limiting active RL agents
    /// Falls back to scripted behavior when capacity exceeded
    /// </summary>
    public class RLSpawnLimiter
    {
        // Configuration
        private int maxRLAgents;
        private float targetLatencyMs;
        private float latencyPerAgentMs;
        private bool enableDynamicLimit;

        // Current state
        private int activeRLAgentCount;
        private int scriptedFallbackCount;
        private float currentLatencyMs;
        private float latencyMovingAverage;
        private const float latencyAverageAlpha = 0.2f; // Exponential moving average factor

        // Performance tracking
        private Queue<float> recentLatencies;
        private const int latencySampleSize = 30;

        // Dynamic adjustment
        private float lastAdjustmentTime;
        private const float adjustmentInterval = 2f; // Adjust limit every 2 seconds

        public int ActiveRLAgentCount => activeRLAgentCount;
        public int ScriptedFallbackCount => scriptedFallbackCount;
        public int MaxRLAgents => maxRLAgents;
        public float CurrentLatencyMs => currentLatencyMs;
        public float AverageLatencyMs => latencyMovingAverage;
        public bool IsAtCapacity => activeRLAgentCount >= maxRLAgents;
        public float CapacityUtilization => maxRLAgents > 0 ? (float)activeRLAgentCount / maxRLAgents : 0f;

        public RLSpawnLimiter(
            int maxRLAgents = 50,
            float targetLatencyMs = 16f,
            float latencyPerAgentMs = 0.3f,
            bool enableDynamicLimit = true)
        {
            this.maxRLAgents = maxRLAgents;
            this.targetLatencyMs = targetLatencyMs;
            this.latencyPerAgentMs = latencyPerAgentMs;
            this.enableDynamicLimit = enableDynamicLimit;

            this.activeRLAgentCount = 0;
            this.scriptedFallbackCount = 0;
            this.currentLatencyMs = 0f;
            this.latencyMovingAverage = 0f;
            this.recentLatencies = new Queue<float>();
            this.lastAdjustmentTime = Time.time;
        }

        /// <summary>
        /// Check if a new RL agent can be spawned within performance budget
        /// </summary>
        public bool CanSpawnRLAgent()
        {
            // Hard limit check
            if (activeRLAgentCount >= maxRLAgents)
                return false;

            // Latency-based check
            float projectedLatency = EstimateLatencyWithAgents(activeRLAgentCount + 1);
            return projectedLatency <= targetLatencyMs;
        }

        /// <summary>
        /// Register a new RL agent spawn
        /// </summary>
        public void RegisterRLAgent()
        {
            activeRLAgentCount++;
        }

        /// <summary>
        /// Unregister an RL agent (destroyed or despawned)
        /// </summary>
        public void UnregisterRLAgent()
        {
            activeRLAgentCount = Mathf.Max(0, activeRLAgentCount - 1);
        }

        /// <summary>
        /// Register a fallback to scripted behavior
        /// </summary>
        public void RegisterScriptedFallback()
        {
            scriptedFallbackCount++;
        }

        /// <summary>
        /// Update latency measurements and adjust limits dynamically
        /// </summary>
        public void UpdateLatency(float measuredLatencyMs)
        {
            currentLatencyMs = measuredLatencyMs;

            // Update moving average
            if (latencyMovingAverage == 0f)
                latencyMovingAverage = measuredLatencyMs;
            else
                latencyMovingAverage = latencyAverageAlpha * measuredLatencyMs +
                                      (1f - latencyAverageAlpha) * latencyMovingAverage;

            // Track recent samples
            recentLatencies.Enqueue(measuredLatencyMs);
            if (recentLatencies.Count > latencySampleSize)
                recentLatencies.Dequeue();

            // Dynamic limit adjustment
            if (enableDynamicLimit && Time.time - lastAdjustmentTime >= adjustmentInterval)
            {
                AdjustDynamicLimit();
                lastAdjustmentTime = Time.time;
            }
        }

        /// <summary>
        /// Dynamically adjust max RL agents based on actual performance
        /// </summary>
        private void AdjustDynamicLimit()
        {
            if (activeRLAgentCount == 0) return;

            // Calculate actual latency per agent from recent samples
            float avgLatency = CalculateAverageLatency();
            float actualLatencyPerAgent = avgLatency / Mathf.Max(1, activeRLAgentCount);

            // Update estimate for better future predictions
            latencyPerAgentMs = 0.9f * latencyPerAgentMs + 0.1f * actualLatencyPerAgent;

            // Adjust max agents to stay within target latency
            int theoreticalMax = Mathf.FloorToInt(targetLatencyMs / Mathf.Max(0.1f, latencyPerAgentMs));

            // Apply safety margin (80% of theoretical max)
            int safeMax = Mathf.FloorToInt(theoreticalMax * 0.8f);

            // Gradual adjustment (max ±5 agents per adjustment)
            int delta = Mathf.Clamp(safeMax - maxRLAgents, -5, 5);
            maxRLAgents = Mathf.Clamp(maxRLAgents + delta, 10, 200); // Min 10, max 200

            if (delta != 0)
            {
                Debug.Log($"[RLSpawnLimiter] Adjusted max RL agents: {maxRLAgents - delta} → {maxRLAgents} " +
                         $"(actual latency/agent: {actualLatencyPerAgent:F2}ms, target: {targetLatencyMs}ms)");
            }
        }

        /// <summary>
        /// Estimate total latency with N agents
        /// </summary>
        private float EstimateLatencyWithAgents(int agentCount)
        {
            return agentCount * latencyPerAgentMs;
        }

        /// <summary>
        /// Calculate average latency from recent samples
        /// </summary>
        private float CalculateAverageLatency()
        {
            if (recentLatencies.Count == 0) return currentLatencyMs;

            float sum = 0f;
            foreach (float latency in recentLatencies)
                sum += latency;

            return sum / recentLatencies.Count;
        }

        /// <summary>
        /// Get recommended spawn decision (RL or scripted fallback)
        /// </summary>
        public SpawnDecision GetSpawnDecision()
        {
            if (CanSpawnRLAgent())
            {
                return new SpawnDecision
                {
                    useRL = true,
                    reason = "Within capacity"
                };
            }
            else
            {
                string reason = activeRLAgentCount >= maxRLAgents ?
                    "Max RL agents reached" :
                    "Projected latency exceeds target";

                return new SpawnDecision
                {
                    useRL = false,
                    reason = reason
                };
            }
        }

        /// <summary>
        /// Reset counters (e.g., on scene reset)
        /// </summary>
        public void Reset()
        {
            activeRLAgentCount = 0;
            scriptedFallbackCount = 0;
            currentLatencyMs = 0f;
            recentLatencies.Clear();
        }

        /// <summary>
        /// Get limiter statistics
        /// </summary>
        public LimiterStats GetStats()
        {
            return new LimiterStats
            {
                activeRLAgents = activeRLAgentCount,
                maxRLAgents = maxRLAgents,
                scriptedFallbacks = scriptedFallbackCount,
                currentLatencyMs = currentLatencyMs,
                averageLatencyMs = latencyMovingAverage,
                capacityUtilization = CapacityUtilization,
                estimatedLatencyPerAgent = latencyPerAgentMs,
                canSpawnMore = CanSpawnRLAgent()
            };
        }
    }

    /// <summary>
    /// Spawn decision result
    /// </summary>
    public struct SpawnDecision
    {
        public bool useRL;
        public string reason;
    }

    /// <summary>
    /// Statistics for RL spawn limiter
    /// </summary>
    public struct LimiterStats
    {
        public int activeRLAgents;
        public int maxRLAgents;
        public int scriptedFallbacks;
        public float currentLatencyMs;
        public float averageLatencyMs;
        public float capacityUtilization;
        public float estimatedLatencyPerAgent;
        public bool canSpawnMore;

        public override string ToString()
        {
            return $"RL Agents: {activeRLAgents}/{maxRLAgents} ({capacityUtilization:P0}), " +
                   $"Fallbacks: {scriptedFallbacks}, " +
                   $"Latency: {currentLatencyMs:F1}ms (avg: {averageLatencyMs:F1}ms), " +
                   $"Can Spawn: {canSpawnMore}";
        }
    }
}
