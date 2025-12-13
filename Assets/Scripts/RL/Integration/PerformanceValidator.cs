using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Vampire.RL
{
    /// <summary>
    /// Validates that all RL system performance requirements are met
    /// Tests inference time, memory usage, latency, and throughput
    /// Requirements: 5.1, 5.2, 5.3, 5.4, 5.5
    /// </summary>
    public class PerformanceValidator : MonoBehaviour
    {
        [Header("Performance Targets")]
        [SerializeField] private float targetInferenceTimeMs = 10f;
        [SerializeField] private float maxInferenceTimeMs = 16f; // 60 FPS
        [SerializeField] private float targetMemoryMB = 50f;
        [SerializeField] private float maxMemoryMB = 100f;
        [SerializeField] private float targetLatencyMs = 5f;
        [SerializeField] private int minThroughputAgentsPerFrame = 10;

        [Header("Test Configuration")]
        [SerializeField] private bool runValidationOnStart = false;
        [SerializeField] private int validationSampleCount = 100;
        [SerializeField] private bool logDetailedMetrics = true;

        private PerformanceMonitor performanceMonitor;
        private List<PerformanceValidationResult> validationResults;

        private void Start()
        {
            if (runValidationOnStart)
            {
                RunPerformanceValidation();
            }
        }

        [ContextMenu("Run Performance Validation")]
        public void RunPerformanceValidation()
        {
            Debug.Log("=== Starting RL Performance Validation ===");

            validationResults = new List<PerformanceValidationResult>();
            performanceMonitor = GetComponent<PerformanceMonitor>();

            if (performanceMonitor == null)
            {
                var go = new GameObject("PerformanceMonitor");
                go.transform.SetParent(transform);
                performanceMonitor = go.AddComponent<PerformanceMonitor>();
            }

            // Run multiple validation tests
            TestInferencePerformance();
            TestMemoryUsage();
            TestLatency();
            TestThroughput();
            TestScalability();

            PrintValidationResults();
        }

        private void TestInferencePerformance()
        {
            Debug.Log("\n--- Testing Inference Performance ---");

            var agent = CreateTestAgent();
            if (agent == null) return;

            List<float> inferenceTimes = new List<float>();

            for (int i = 0; i < validationSampleCount; i++)
            {
                var state = RLGameState.CreateDefault();

                float startTime = Time.realtimeSinceStartup;
                int action = agent.SelectAction(state, false);
                float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;

                inferenceTimes.Add(elapsedMs);
            }

            float avgTime = inferenceTimes.Average();
            float maxTime = inferenceTimes.Max();
            float minTime = inferenceTimes.Min();
            float stdDev = CalculateStandardDeviation(inferenceTimes);

            bool passed = avgTime <= targetInferenceTimeMs && maxTime <= maxInferenceTimeMs;

            validationResults.Add(new PerformanceValidationResult
            {
                testName = "Inference Performance",
                passed = passed,
                avgValue = avgTime,
                maxValue = maxTime,
                minValue = minTime,
                targetValue = targetInferenceTimeMs,
                limitValue = maxInferenceTimeMs,
                unit = "ms",
                details = $"StdDev: {stdDev:F3}ms"
            });

            if (logDetailedMetrics)
            {
                Debug.Log($"Inference Performance: Avg={avgTime:F3}ms, Max={maxTime:F3}ms, Min={minTime:F3}ms, StdDev={stdDev:F3}ms");
            }

            DestroyImmediate(agent.gameObject);
        }

        private void TestMemoryUsage()
        {
            Debug.Log("\n--- Testing Memory Usage ---");

            if (performanceMonitor == null) return;

            float initialMemory = GC.GetTotalMemory(true) / (1024f * 1024f);

            // Create multiple agents and measure memory growth
            List<DQNLearningAgent> agents = new List<DQNLearningAgent>();
            int agentCount = 5;

            for (int i = 0; i < agentCount; i++)
            {
                var agent = CreateTestAgent();
                if (agent != null)
                    agents.Add(agent);
            }

            float finalMemory = GC.GetTotalMemory(false) / (1024f * 1024f);
            float memoryUsed = finalMemory - initialMemory;
            float memoryPerAgent = memoryUsed / agentCount;

            bool passed = memoryUsed <= maxMemoryMB;

            validationResults.Add(new PerformanceValidationResult
            {
                testName = "Memory Usage",
                passed = passed,
                avgValue = memoryPerAgent,
                maxValue = memoryUsed,
                minValue = initialMemory,
                targetValue = targetMemoryMB,
                limitValue = maxMemoryMB,
                unit = "MB",
                details = $"Total Agents: {agentCount}"
            });

            if (logDetailedMetrics)
            {
                Debug.Log($"Memory Usage: {memoryUsed:F2}MB total, {memoryPerAgent:F2}MB per agent");
            }

            foreach (var agent in agents)
            {
                DestroyImmediate(agent.gameObject);
            }
        }

        private void TestLatency()
        {
            Debug.Log("\n--- Testing Decision Latency ---");

            var agent = CreateTestAgent();
            if (agent == null) return;

            List<float> latencies = new List<float>();

            for (int i = 0; i < validationSampleCount; i++)
            {
                var state = RLGameState.CreateDefault();

                float inferenceStart = Time.realtimeSinceStartup;
                int action = agent.SelectAction(state, false);
                float inferenceTime = (Time.realtimeSinceStartup - inferenceStart) * 1000f;

                latencies.Add(inferenceTime);
            }

            float avgLatency = latencies.Average();
            float maxLatency = latencies.Max();

            bool passed = avgLatency <= targetLatencyMs;

            validationResults.Add(new PerformanceValidationResult
            {
                testName = "Decision Latency",
                passed = passed,
                avgValue = avgLatency,
                maxValue = maxLatency,
                targetValue = targetLatencyMs,
                limitValue = 15f,
                unit = "ms"
            });

            if (logDetailedMetrics)
            {
                Debug.Log($"Latency: Avg={avgLatency:F3}ms, Max={maxLatency:F3}ms");
            }

            DestroyImmediate(agent.gameObject);
        }

        private void TestThroughput()
        {
            Debug.Log("\n--- Testing Throughput ---");

            int agentCount = 20;
            var agents = new List<DQNLearningAgent>();

            for (int i = 0; i < agentCount; i++)
            {
                var agent = CreateTestAgent();
                if (agent != null)
                    agents.Add(agent);
            }

            float startTime = Time.realtimeSinceStartup;
            int totalInferences = 0;

            for (int batch = 0; batch < 10; batch++)
            {
                foreach (var agent in agents)
                {
                    var state = RLGameState.CreateDefault();
                    agent.SelectAction(state, false);
                    totalInferences++;
                }
            }

            float elapsedTime = Time.realtimeSinceStartup - startTime;
            float inferencePerSecond = totalInferences / elapsedTime;

            bool passed = inferencePerSecond >= (minThroughputAgentsPerFrame * 60); // 60 FPS baseline

            validationResults.Add(new PerformanceValidationResult
            {
                testName = "Throughput",
                passed = passed,
                avgValue = inferencePerSecond,
                maxValue = inferencePerSecond,
                targetValue = minThroughputAgentsPerFrame * 60,
                unit = "inferences/sec",
                details = $"Agents: {agentCount}"
            });

            if (logDetailedMetrics)
            {
                Debug.Log($"Throughput: {inferencePerSecond:F0} inferences/sec");
            }

            foreach (var agent in agents)
            {
                DestroyImmediate(agent.gameObject);
            }
        }

        private void TestScalability()
        {
            Debug.Log("\n--- Testing Scalability ---");

            float[] agentCounts = new float[] { 1, 5, 10, 20 };
            List<float> avgTimes = new List<float>();

            foreach (int count in agentCounts)
            {
                var agents = new List<DQNLearningAgent>();

                for (int i = 0; i < count; i++)
                {
                    var agent = CreateTestAgent();
                    if (agent != null)
                        agents.Add(agent);
                }

                float frameStart = Time.realtimeSinceStartup;
                for (int step = 0; step < 10; step++)
                {
                    foreach (var agent in agents)
                    {
                        var state = RLGameState.CreateDefault();
                        agent.SelectAction(state, false);
                    }
                }
                float frameTime = (Time.realtimeSinceStartup - frameStart) / 10f * 1000f;

                avgTimes.Add(frameTime);

                foreach (var agent in agents)
                {
                    DestroyImmediate(agent.gameObject);
                }
            }

            // Check if scaling is reasonable (shouldn't exceed 16ms even at 20 agents)
            bool passed = avgTimes.Last() <= maxInferenceTimeMs;

            validationResults.Add(new PerformanceValidationResult
            {
                testName = "Scalability",
                passed = passed,
                avgValue = avgTimes.Average(),
                maxValue = avgTimes.Max(),
                targetValue = maxInferenceTimeMs,
                unit = "ms per frame",
                details = "Tested with 1, 5, 10, 20 agents"
            });

            if (logDetailedMetrics)
            {
                Debug.Log($"Scalability: Frame times at different agent counts: {string.Join(", ", avgTimes.Select(t => $"{t:F2}ms"))}");
            }
        }

        private void PrintValidationResults()
        {
            Debug.Log("\n=== RL Performance Validation Results ===\n");

            int passed = 0;
            int failed = 0;

            foreach (var result in validationResults)
            {
                string status = result.passed ? "✓ PASS" : "✗ FAIL";
                Debug.Log($"{status} | {result.testName}");
                Debug.Log($"    Value: {result.avgValue:F3}{result.unit} (Target: {result.targetValue:F3}{result.unit})");
                if (result.limitValue > 0)
                    Debug.Log($"    Limit: {result.limitValue:F3}{result.unit}");
                if (!string.IsNullOrEmpty(result.details))
                    Debug.Log($"    {result.details}");
                Debug.Log("");

                if (result.passed)
                    passed++;
                else
                    failed++;
            }

            Debug.Log($"=== Summary: {passed}/{validationResults.Count} Tests Passed ===");

            if (failed == 0)
            {
                Debug.Log("=== ALL PERFORMANCE REQUIREMENTS MET ===");
            }
            else
            {
                Debug.LogError($"=== {failed} PERFORMANCE REQUIREMENTS NOT MET ===");
            }
        }

        private DQNLearningAgent CreateTestAgent()
        {
            try
            {
                var go = new GameObject($"TestAgent_{Guid.NewGuid()}");
                var agent = go.AddComponent<DQNLearningAgent>();
                var actionSpace = ActionSpace.CreateDefault();
                agent.Initialize(MonsterType.Melee, actionSpace);
                return agent;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create test agent: {ex.Message}");
                return null;
            }
        }

        private float CalculateStandardDeviation(List<float> values)
        {
            if (values.Count == 0) return 0f;
            float avg = values.Average();
            float sumSquaredDiff = values.Sum(v => (v - avg) * (v - avg));
            return Mathf.Sqrt(sumSquaredDiff / values.Count);
        }

        private class PerformanceValidationResult
        {
            public string testName;
            public bool passed;
            public float avgValue;
            public float maxValue;
            public float minValue;
            public float targetValue;
            public float limitValue;
            public string unit;
            public string details;
        }
    }
}
