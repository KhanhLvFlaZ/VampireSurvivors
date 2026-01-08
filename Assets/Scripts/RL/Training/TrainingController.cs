using UnityEngine;

namespace Vampire.RL.Training
{
    /// <summary>
    /// Minimal training loop controller used by RLSystem.
    /// Provides episode tracking and simple progress reporting.
    /// </summary>
    public class TrainingController : MonoBehaviour
    {
        public int CurrentEpisode { get; private set; }

        private int totalEpisodes = 0;
        private int evaluationIntervalSteps = 0;
        private bool isTraining = false;
        private bool isPaused = false;

        public void SetTotalEpisodes(int total)
        {
            totalEpisodes = Mathf.Max(0, total);
        }

        public void SetEvaluationInterval(int steps)
        {
            evaluationIntervalSteps = Mathf.Max(0, steps);
        }

        public void StartTraining()
        {
            isTraining = true;
            isPaused = false;
            // Actual training loop is managed by other components; this class tracks state.
        }

        public void PauseTraining()
        {
            if (!isTraining) return;
            isPaused = true;
        }

        public void ResumeTraining()
        {
            if (!isTraining) return;
            isPaused = false;
        }

        public float GetTrainingProgress()
        {
            if (totalEpisodes <= 0) return 0f;
            return Mathf.Clamp01((float)CurrentEpisode / totalEpisodes);
        }

        // Optional: allow external systems to advance episodes
        public void IncrementEpisode()
        {
            if (!isTraining || isPaused) return;
            CurrentEpisode = Mathf.Min(CurrentEpisode + 1, totalEpisodes);
        }
    }
}
