using UnityEngine;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Manages experience collection and replay buffer coordination
    /// Handles storage of training experiences from all active RL agents
    /// </summary>
    public class ExperienceManager : MonoBehaviour
    {
        [Header("Buffer Configuration")]
        [SerializeField] private int bufferSize = 10000;
        [SerializeField] private int minExperiencesForTraining = 500;

        [Header("Sampling Configuration")]
        [SerializeField] private int batchSize = 32;
        [SerializeField] private float priorityDecayRate = 0.95f;

        /// <summary>
        /// Simple list-based storage for experiences
        /// </summary>
        private List<Experience> experienceBuffer = new List<Experience>();
        private List<float> experiencePriorities = new List<float>();

        /// <summary>
        /// Statistics for monitoring
        /// </summary>
        private int totalExperiencesAdded = 0;
        private int totalBatchesSampled = 0;

        private static ExperienceManager instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            experienceBuffer = new List<Experience>(bufferSize);
            experiencePriorities = new List<float>(bufferSize);
        }

        /// <summary>
        /// Get the singleton instance
        /// </summary>
        public static ExperienceManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject managerObj = new GameObject("ExperienceManager");
                    instance = managerObj.AddComponent<ExperienceManager>();
                }
                return instance;
            }
        }

        /// <summary>
        /// Add an experience to the replay buffer
        /// </summary>
        public void StoreExperience(Experience experience, float priority = 1.0f)
        {
            // Remove oldest experience if buffer is full
            if (experienceBuffer.Count >= bufferSize)
            {
                experienceBuffer.RemoveAt(0);
                experiencePriorities.RemoveAt(0);
            }

            experienceBuffer.Add(experience);
            experiencePriorities.Add(priority);
            totalExperiencesAdded++;

            // Debug info
            if (totalExperiencesAdded % 1000 == 0)
            {
                Debug.Log($"Experience Manager: {totalExperiencesAdded} total experiences added. " +
                    $"Buffer: {experienceBuffer.Count}/{bufferSize} " +
                    $"({(experienceBuffer.Count * 100f / bufferSize):F1}%)");
            }
        }

        /// <summary>
        /// Sample a batch of experiences for training
        /// </summary>
        public Experience[] SampleBatch()
        {
            if (!IsReadyForTraining())
            {
                return new Experience[0];
            }

            totalBatchesSampled++;

            // Sample based on priorities
            Experience[] batch = new Experience[Mathf.Min(batchSize, experienceBuffer.Count)];

            // Simple random sampling without replacement
            HashSet<int> sampledIndices = new HashSet<int>();
            for (int i = 0; i < batch.Length; i++)
            {
                int randomIndex;
                do
                {
                    randomIndex = Random.Range(0, experienceBuffer.Count);
                } while (sampledIndices.Contains(randomIndex));

                sampledIndices.Add(randomIndex);
                batch[i] = experienceBuffer[randomIndex];
            }

            return batch;
        }

        /// <summary>
        /// Check if buffer has enough experiences for training
        /// </summary>
        public bool IsReadyForTraining()
        {
            return experienceBuffer.Count >= minExperiencesForTraining;
        }

        /// <summary>
        /// Clear all experiences from the buffer
        /// </summary>
        public void ClearBuffer()
        {
            experienceBuffer.Clear();
            experiencePriorities.Clear();
        }

        /// <summary>
        /// Get current buffer statistics
        /// </summary>
        public void GetBufferStats(out int currentSize, out int maxSize, out float fillPercentage)
        {
            currentSize = experienceBuffer.Count;
            maxSize = bufferSize;
            fillPercentage = (experienceBuffer.Count * 100f / bufferSize);
        }

        /// <summary>
        /// Update priority for experience-based learning
        /// </summary>
        public void UpdateExperiencePriority(int index, float tdError)
        {
            if (index >= 0 && index < experiencePriorities.Count)
            {
                // Convert TD error to priority (higher error = higher priority)
                float priority = Mathf.Pow(Mathf.Abs(tdError) + 1f, 0.6f);
                experiencePriorities[index] = priority;
            }
        }

        /// <summary>
        /// Get total experiences added since startup
        /// </summary>
        public int GetTotalExperiencesAdded() => totalExperiencesAdded;

        /// <summary>
        /// Get total batches sampled for training
        /// </summary>
        public int GetTotalBatchesSampled() => totalBatchesSampled;

        /// <summary>
        /// Set batch size for sampling
        /// </summary>
        public void SetBatchSize(int newBatchSize)
        {
            batchSize = Mathf.Max(1, newBatchSize);
        }

        /// <summary>
        /// Runtime reward adjustment - updates reward values in stored experiences
        /// </summary>
        public void AdjustExperienceRewards(float rewardMultiplier)
        {
            // Adjust all rewards in buffer by multiplier
            for (int i = 0; i < experienceBuffer.Count; i++)
            {
                Experience exp = experienceBuffer[i];
                exp.reward *= rewardMultiplier;
                experienceBuffer[i] = exp;
            }
            Debug.Log($"Adjusted experience rewards by factor: {rewardMultiplier}");
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
