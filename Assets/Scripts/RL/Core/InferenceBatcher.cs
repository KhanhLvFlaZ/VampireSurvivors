using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Vampire.RL
{
    /// <summary>
    /// Batches observations from multiple agents for efficient inference
    /// Reduces per-agent inference cost by processing multiple observations simultaneously
    /// </summary>
    public class InferenceBatcher
    {
        private int maxBatchSize;
        private float batchTimeoutMs;

        // Pending inference requests
        private List<InferenceRequest> pendingRequests;
        private float lastBatchTime;

        // Performance metrics
        private float totalInferenceTimeMs;
        private int totalBatchesProcessed;
        private int totalRequestsProcessed;

        public int PendingRequestCount => pendingRequests.Count;
        public float AverageBatchSize => totalBatchesProcessed > 0 ?
            (float)totalRequestsProcessed / totalBatchesProcessed : 0f;
        public float AverageInferenceTimeMs => totalBatchesProcessed > 0 ?
            totalInferenceTimeMs / totalBatchesProcessed : 0f;

        public InferenceBatcher(int maxBatchSize = 32, float batchTimeoutMs = 5f)
        {
            this.maxBatchSize = maxBatchSize;
            this.batchTimeoutMs = batchTimeoutMs;
            this.pendingRequests = new List<InferenceRequest>();
            this.lastBatchTime = Time.realtimeSinceStartup * 1000f;
        }

        /// <summary>
        /// Queue an inference request for batching
        /// </summary>
        public void QueueRequest(ILearningAgent agent, float[] observation)
        {
            pendingRequests.Add(new InferenceRequest
            {
                agent = agent,
                observation = observation,
                timestamp = Time.realtimeSinceStartup * 1000f
            });
        }

        /// <summary>
        /// Process batched requests if batch is full or timeout reached
        /// Returns number of agents processed
        /// </summary>
        public int ProcessBatch(bool forceProcess = false)
        {
            if (pendingRequests.Count == 0) return 0;

            float currentTime = Time.realtimeSinceStartup * 1000f;
            float timeSinceLastBatch = currentTime - lastBatchTime;

            // Process if batch is full or timeout reached
            bool shouldProcess = forceProcess ||
                                pendingRequests.Count >= maxBatchSize ||
                                timeSinceLastBatch >= batchTimeoutMs;

            if (!shouldProcess) return 0;

            float startTime = Time.realtimeSinceStartup * 1000f;

            // Process all pending requests
            int processedCount = 0;
            foreach (var request in pendingRequests)
            {
                if (request.agent != null)
                {
                    // In a real implementation, this would batch the neural network forward pass
                    // For now, we call individual agent policies but group them to reduce overhead
                    request.agent.UpdatePolicy();
                    processedCount++;
                }
            }

            // Update metrics
            float inferenceTime = (Time.realtimeSinceStartup * 1000f) - startTime;
            totalInferenceTimeMs += inferenceTime;
            totalBatchesProcessed++;
            totalRequestsProcessed += processedCount;

            // Clear processed requests
            pendingRequests.Clear();
            lastBatchTime = currentTime;

            return processedCount;
        }

        /// <summary>
        /// Clear all pending requests (e.g., on scene reset)
        /// </summary>
        public void ClearAll()
        {
            pendingRequests.Clear();
        }

        /// <summary>
        /// Get batching statistics
        /// </summary>
        public BatchingStats GetStats()
        {
            return new BatchingStats
            {
                pendingRequests = pendingRequests.Count,
                averageBatchSize = AverageBatchSize,
                averageInferenceTimeMs = AverageInferenceTimeMs,
                totalBatches = totalBatchesProcessed,
                totalRequests = totalRequestsProcessed
            };
        }
    }

    /// <summary>
    /// Represents a pending inference request
    /// </summary>
    public struct InferenceRequest
    {
        public ILearningAgent agent;
        public float[] observation;
        public float timestamp;
    }

    /// <summary>
    /// Statistics for inference batching
    /// </summary>
    public struct BatchingStats
    {
        public int pendingRequests;
        public float averageBatchSize;
        public float averageInferenceTimeMs;
        public int totalBatches;
        public int totalRequests;

        public override string ToString()
        {
            return $"Pending: {pendingRequests}, Avg Batch: {averageBatchSize:F1}, " +
                   $"Avg Inference: {averageInferenceTimeMs:F2}ms, " +
                   $"Total Batches: {totalBatches}";
        }
    }
}
