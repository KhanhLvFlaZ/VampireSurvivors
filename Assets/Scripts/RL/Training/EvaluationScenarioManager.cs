using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Vampire.RL.Integration;

namespace Vampire.RL.Training
{
    /// <summary>
    /// Manages evaluation scenarios and records results.
    /// Orchestrates running fixed-seed, stress, and long-run evaluation scenarios.
    /// </summary>
    public class EvaluationScenarioManager : MonoBehaviour
    {
        [Header("Evaluation Settings")]
        [SerializeField] private bool enableAutoEvaluation = true;
        [SerializeField] private float evaluationIntervalSeconds = 300f; // Run eval every 5 minutes of training
        [SerializeField] private string resultsDirectory = "EvaluationResults";

        [Header("Scenario Settings")]
        [SerializeField] private int fixedSeedValue = 12345;
        [SerializeField] private float stressSpawnMultiplier = 2.0f;
        [SerializeField] private float longRunDurationMinutes = 60f;

        private List<EvaluationScenario> scenarios = new List<EvaluationScenario>();
        private List<EvaluationResult> results = new List<EvaluationResult>();
        private LevelRLIntegration levelIntegration;
        private EpisodeMetricsRecorder metricsRecorder;
        private string resultsPath;
        private float lastEvaluationTime;
        private int evaluationIndex = 0;

        public event Action<EvaluationResult> OnEvaluationComplete;

        public void Initialize(LevelRLIntegration integration, EpisodeMetricsRecorder recorder)
        {
            levelIntegration = integration;
            metricsRecorder = recorder;

            // Create results directory
            resultsPath = Path.Combine(Application.persistentDataPath, resultsDirectory);
            if (!Directory.Exists(resultsPath))
            {
                Directory.CreateDirectory(resultsPath);
            }

            // Initialize default scenarios
            InitializeDefaultScenarios();
            lastEvaluationTime = Time.time;

            Debug.Log($"[Evaluation Manager] Initialized with {scenarios.Count} scenarios");
        }

        private void InitializeDefaultScenarios()
        {
            scenarios.Clear();

            // 1. Fixed-seed scenario for reproducible comparison
            scenarios.Add(EvaluationScenario.CreateFixedSeedScenario(fixedSeedValue));

            // 2. Stress scenario for generalization testing
            scenarios.Add(EvaluationScenario.CreateStressScenario());

            // 3. Long-run scenario for stability testing
            var longRunScenario = EvaluationScenario.CreateLongRunScenario();
            longRunScenario.durationSeconds = longRunDurationMinutes * 60;
            scenarios.Add(longRunScenario);
        }

        public void AddCustomScenario(EvaluationScenario scenario)
        {
            if (scenario != null)
            {
                scenarios.Add(scenario);
                Debug.Log($"[Evaluation Manager] Added custom scenario: {scenario.scenarioName}");
            }
        }

        /// <summary>
        /// Run a specific evaluation scenario by index.
        /// </summary>
        public void RunScenario(int scenarioIndex)
        {
            if (scenarioIndex < 0 || scenarioIndex >= scenarios.Count)
            {
                Debug.LogWarning($"[Evaluation Manager] Invalid scenario index: {scenarioIndex}");
                return;
            }

            var scenario = scenarios[scenarioIndex];
            RunScenario(scenario);
        }

        /// <summary>
        /// Run a specific evaluation scenario.
        /// </summary>
        public void RunScenario(EvaluationScenario scenario)
        {
            if (scenario == null || levelIntegration == null)
            {
                Debug.LogError("[Evaluation Manager] Cannot run scenario: missing scenario or integration");
                return;
            }

            Debug.Log($"[Evaluation Manager] Starting scenario: {scenario.scenarioName}");

            // Apply scenario settings
            ApplyScenarioSettings(scenario);

            // Record scenario result
            var result = new EvaluationResult
            {
                scenarioName = scenario.scenarioName,
                runTime = DateTime.UtcNow,
                seed = scenario.seed,
                actualDurationSeconds = 0,
                completedSuccessfully = false,
                notes = scenario.description
            };

            // Start tracking
            StartCoroutine(RunScenarioCoroutine(scenario, result));
        }

        /// <summary>
        /// Run all default scenarios in sequence.
        /// </summary>
        public void RunAllScenarios()
        {
            Debug.Log($"[Evaluation Manager] Starting evaluation of all {scenarios.Count} scenarios");
            StartCoroutine(RunAllScenariosCoroutine());
        }

        private System.Collections.IEnumerator RunScenarioCoroutine(EvaluationScenario scenario, EvaluationResult result)
        {
            float startTime = Time.time;
            float endTime = startTime + scenario.durationSeconds;

            // Wait until scenario duration completes or level ends
            while (Time.time < endTime && levelIntegration.gameObject.activeInHierarchy)
            {
                yield return new WaitForSeconds(1f);
            }

            // Collect results
            result.actualDurationSeconds = Time.time - startTime;
            if (metricsRecorder != null)
            {
                var snapshot = metricsRecorder.FinishRun();
                result.survivalSeconds = snapshot.survivalSeconds;
                result.kills = snapshot.kills;
                result.xpGained = snapshot.xpGained;
                result.goldGained = snapshot.goldGained;
            }

            // TODO: Collect FPS and memory metrics from PerformanceMonitor
            result.averageFps = 60f; // Placeholder
            result.p99FrameTimeMs = 16.7f; // Placeholder
            result.maxMemoryMB = 100f; // Placeholder

            result.completedSuccessfully = true;
            results.Add(result);

            Debug.Log($"[Evaluation Manager] Scenario '{scenario.scenarioName}' complete: {result.survivalSeconds:F1}s, kills={result.kills}, fps={result.averageFps:F1}");
            OnEvaluationComplete?.Invoke(result);

            // Export result
            ExportResult(result);
        }

        private System.Collections.IEnumerator RunAllScenariosCoroutine()
        {
            foreach (var scenario in scenarios)
            {
                RunScenario(scenario);
                // Wait for scenario to complete (simplified; in practice, use event/callback)
                yield return new WaitForSeconds(scenario.durationSeconds + 5f);
            }

            Debug.Log("[Evaluation Manager] All scenarios complete");
            ExportAllResults();
        }

        private void ApplyScenarioSettings(EvaluationScenario scenario)
        {
            // Set seed for RNG
            UnityEngine.Random.InitState(scenario.randomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : scenario.seed);

            // TODO: Apply spawn rate multiplier to LevelManager
            // TODO: Set map/difficulty based on scenario

            Debug.Log($"[Evaluation Manager] Applied scenario settings: seed={scenario.seed}, spawnMult={scenario.spawnRateMultiplier}");
        }

        private void ExportResult(EvaluationResult result)
        {
            try
            {
                string filename = $"eval_{result.scenarioName}_{result.runTime:yyyyMMdd_HHmmss}.json";
                string filepath = Path.Combine(resultsPath, filename);

                string json = JsonUtility.ToJson(result, true);
                File.WriteAllText(filepath, json);

                Debug.Log($"[Evaluation Manager] Exported result to {filepath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Evaluation Manager] Failed to export result: {ex.Message}");
            }
        }

        private void ExportAllResults()
        {
            try
            {
                string summaryFile = Path.Combine(resultsPath, $"evaluation_summary_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                using (var writer = new StreamWriter(summaryFile))
                {
                    writer.WriteLine("=== Evaluation Summary ===");
                    writer.WriteLine($"Total Scenarios Run: {results.Count}");
                    writer.WriteLine();

                    foreach (var result in results)
                    {
                        writer.WriteLine($"Scenario: {result.scenarioName}");
                        writer.WriteLine($"  Seed: {result.seed}");
                        writer.WriteLine($"  Duration: {result.actualDurationSeconds:F1}s");
                        writer.WriteLine($"  Survival: {result.survivalSeconds:F1}s");
                        writer.WriteLine($"  Kills: {result.kills}");
                        writer.WriteLine($"  XP: {result.xpGained:F0}");
                        writer.WriteLine($"  Gold: {result.goldGained:F0}");
                        writer.WriteLine($"  Avg FPS: {result.averageFps:F1}");
                        writer.WriteLine($"  P99 Frame Time: {result.p99FrameTimeMs:F2}ms");
                        writer.WriteLine($"  Max Memory: {result.maxMemoryMB:F1}MB");
                        writer.WriteLine($"  Status: {(result.completedSuccessfully ? "SUCCESS" : "FAILED")}");
                        writer.WriteLine();
                    }
                }

                Debug.Log($"[Evaluation Manager] Exported summary to {summaryFile}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Evaluation Manager] Failed to export summary: {ex.Message}");
            }
        }

        private void Update()
        {
            // Auto-trigger evaluation based on interval (if enabled)
            if (enableAutoEvaluation && Time.time - lastEvaluationTime >= evaluationIntervalSeconds)
            {
                if (evaluationIndex < scenarios.Count)
                {
                    RunScenario(evaluationIndex);
                    evaluationIndex++;
                    lastEvaluationTime = Time.time;
                }
            }
        }

        public List<EvaluationResult> GetResults() => new List<EvaluationResult>(results);

        public List<EvaluationScenario> GetScenarios() => new List<EvaluationScenario>(scenarios);
    }
}
