using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vampire.RL.Integration;

namespace Vampire.RL.Training
{
    /// <summary>
    /// Orchestrates training loop: runs episodes, evaluates periodically, saves checkpoints.
    /// Manages training lifecycle: episodes/steps, eval intervals, best model tracking.
    /// </summary>
    public class TrainingController : MonoBehaviour
    {
        [Header("Training Configuration")]
        [SerializeField] private int totalEpisodesToRun = 100000;
        [SerializeField] private int stepsPerEpisode = 3600; // ~10 minutes at 60 FPS
        [SerializeField] private int evaluationIntervalSteps = 10000;
        [SerializeField] private bool autoStartTraining = false;

        [Header("Manager References")]
        [SerializeField] private LevelRLIntegration levelIntegration;

        private CheckpointManager checkpointManager;
        private TrainingMetricsLogger metricsLogger;
        private EvaluationScenarioManager evaluationManager;

        // Training state
        private int currentEpisode = 0;
        private int currentStep = 0;
        private float episodeStartTime;
        private bool isTraining = false;
        private Coroutine trainingCoroutine;

        // Metrics tracking
        private float currentEpisodeReward = 0f;
        private Dictionary<MonsterType, LearningMetrics> lastMetrics = new Dictionary<MonsterType, LearningMetrics>();

        public event Action OnTrainingStarted;
        public event Action OnTrainingPaused;
        public event Action OnTrainingCompleted;
        public event Action<int> OnEpisodeCompleted;
        public event Action<int> OnEvaluationTriggered;

        public int CurrentEpisode => currentEpisode;
        public int CurrentStep => currentStep;
        public int TotalEpisodes => totalEpisodesToRun;
        public bool IsTraining => isTraining;

        private void OnEnable()
        {
            if (levelIntegration == null)
                levelIntegration = FindAnyObjectByType<LevelRLIntegration>();
        }

        public void Initialize(CheckpointManager checkpoints, TrainingMetricsLogger logger, EvaluationScenarioManager evaluator)
        {
            checkpointManager = checkpoints;
            metricsLogger = logger;
            evaluationManager = evaluator;

            Debug.Log($"[Training Controller] Initialized: {totalEpisodesToRun} episodes, eval every {evaluationIntervalSteps} steps");
        }

        public void StartTraining()
        {
            if (isTraining)
            {
                Debug.LogWarning("[Training Controller] Training already running");
                return;
            }

            if (trainingCoroutine != null)
                StopCoroutine(trainingCoroutine);

            isTraining = true;
            currentEpisode = 0;
            currentStep = 0;
            trainingCoroutine = StartCoroutine(TrainingLoop());
            OnTrainingStarted?.Invoke();
            Debug.Log("[Training Controller] Training started");
        }

        public void PauseTraining()
        {
            if (!isTraining) return;
            
            isTraining = false;
            if (trainingCoroutine != null)
                StopCoroutine(trainingCoroutine);
            
            OnTrainingPaused?.Invoke();
            Debug.Log("[Training Controller] Training paused");
        }

        public void ResumeTraining()
        {
            if (isTraining) return;
            
            isTraining = true;
            trainingCoroutine = StartCoroutine(TrainingLoop());
            Debug.Log("[Training Controller] Training resumed");
        }

        private IEnumerator TrainingLoop()
        {
            while (currentEpisode < totalEpisodesToRun && isTraining)
            {
                yield return StartCoroutine(RunEpisode());
                currentEpisode++;

                // Check if should evaluate
                if (currentStep % evaluationIntervalSteps < stepsPerEpisode)
                {
                    OnEvaluationTriggered?.Invoke(currentEpisode);
                    yield return StartCoroutine(RunEvaluation());
                }

                // Save periodic checkpoint
                if (currentEpisode % 100 == 0)
                {
                    checkpointManager?.SaveCheckpoint(currentStep, currentEpisode, currentEpisodeReward, 0f);
                }

                OnEpisodeCompleted?.Invoke(currentEpisode);
                yield return null; // Frame break
            }

            isTraining = false;
            OnTrainingCompleted?.Invoke();
            Debug.Log($"[Training Controller] Training completed: {currentEpisode} episodes, {currentStep} steps");
        }

        private IEnumerator RunEpisode()
        {
            episodeStartTime = Time.time;
            currentEpisodeReward = 0f;

            // Episode runs for stepsPerEpisode or until level ends
            float episodeEndTime = episodeStartTime + (stepsPerEpisode / 60f); // Convert steps to seconds at 60 FPS
            
            while (Time.time < episodeEndTime && isTraining)
            {
                // Step metrics: simplified (actual agent update in RLSystem)
                float stepReward = UnityEngine.Random.Range(0.1f, 5f); // Placeholder
                metricsLogger?.LogStep(stepReward, 0.1f, 5);
                currentEpisodeReward += stepReward;
                currentStep++;

                yield return new WaitForSeconds(1f / 60f); // 60 FPS step
            }

            // Episode complete: log metrics
            float episodeDuration = Time.time - episodeStartTime;
            metricsLogger?.LogEpisode(currentEpisodeReward, episodeDuration, lastMetrics);
        }

        private IEnumerator RunEvaluation()
        {
            Debug.Log($"[Training Controller] Triggering evaluation at step {currentStep}");
            
            // Run fixed-seed scenario (index 0)
            if (evaluationManager != null)
            {
                evaluationManager.RunScenario(0);
                
                // Wait for evaluation to complete (simplified)
                yield return new WaitForSeconds(30f);
            }
            else
            {
                yield return null;
            }
        }

        public void SetTotalEpisodes(int count)
        {
            totalEpisodesToRun = Mathf.Max(1, count);
        }

        public void SetEvaluationInterval(int stepInterval)
        {
            evaluationIntervalSteps = Mathf.Max(1000, stepInterval);
        }

        public float GetTrainingProgress()
        {
            return totalEpisodesToRun > 0 ? (float)currentEpisode / totalEpisodesToRun : 0f;
        }

        private void OnDestroy()
        {
            if (trainingCoroutine != null)
                StopCoroutine(trainingCoroutine);
        }
    }
}
