using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire.RL.Integration
{
    /// <summary>
    /// Collects per-run (episode) metrics for reporting/training evaluation.
    /// Designed to keep logging cheap; serialize out-of-band if needed.
    /// </summary>
    public class EpisodeMetricsRecorder : MonoBehaviour
    {
        [Header("Sampling Settings")]
        [SerializeField] private float performanceSampleInterval = 5f; // seconds
        [SerializeField] private bool enableDropHistogram = true;

        // Run context
        public string runId { get; private set; }
        public int seed { get; private set; }
        public string configLabel { get; private set; }
        public DateTime startTime { get; private set; }
        public DateTime endTime { get; private set; }

        // Core counters
        public float survivalSeconds { get; private set; }
        public int kills { get; private set; }
        public float xpGained { get; private set; }
        public float goldGained { get; private set; }

        // Drops histogram: item type -> count
        private readonly Dictionary<string, int> dropHistogram = new Dictionary<string, int>();

        // Performance samples over time
        private readonly List<PerformanceSample> perfSamples = new List<PerformanceSample>();
        private float sampleTimer;
        private PerformanceMonitor perfMonitor;

        public void Initialize(PerformanceMonitor monitor)
        {
            perfMonitor = monitor;
        }

        public void StartRun(int runSeed, string runConfigLabel)
        {
            seed = runSeed;
            configLabel = runConfigLabel;
            startTime = DateTime.UtcNow;
            endTime = default;
            survivalSeconds = 0f;
            kills = 0;
            xpGained = 0f;
            goldGained = 0f;
            dropHistogram.Clear();
            perfSamples.Clear();
            sampleTimer = 0f;
            runId = Guid.NewGuid().ToString("N");
        }

        public void AddKill(int count = 1)
        {
            kills += Mathf.Max(0, count);
        }

        public void AddXp(float amount)
        {
            xpGained += Mathf.Max(0f, amount);
        }

        public void AddGold(float amount)
        {
            goldGained += Mathf.Max(0f, amount);
        }

        public void AddDrop(string dropType)
        {
            if (!enableDropHistogram || string.IsNullOrEmpty(dropType)) return;
            if (dropHistogram.ContainsKey(dropType))
                dropHistogram[dropType] += 1;
            else
                dropHistogram[dropType] = 1;
        }

        private void Update()
        {
            if (startTime == default || perfMonitor == null) return;

            survivalSeconds = (float)(DateTime.UtcNow - startTime).TotalSeconds;
            sampleTimer += Time.deltaTime;

            if (sampleTimer >= performanceSampleInterval)
            {
                CapturePerformanceSample();
                sampleTimer = 0f;
            }
        }

        private void CapturePerformanceSample()
        {
            var metrics = perfMonitor.CurrentMetrics;
            var sample = new PerformanceSample
            {
                timestamp = DateTime.UtcNow,
                frameTimeMs = metrics.frameTimeMs,
                averageFrameTimeMs = metrics.averageFrameTime,
                memoryUsageMB = metrics.memoryUsageMB,
                activeAgents = metrics.activeAgents,
                spikeOver50ms = 0 // spikeOver50ms calculated post-hoc or external integration
            };
            perfSamples.Add(sample);
        }

        public EpisodeMetricsSnapshot FinishRun()
        {
            endTime = DateTime.UtcNow;
            var snapshot = new EpisodeMetricsSnapshot
            {
                runId = runId,
                seed = seed,
                configLabel = configLabel,
                startTime = startTime,
                endTime = endTime,
                survivalSeconds = survivalSeconds,
                kills = kills,
                xpGained = xpGained,
                goldGained = goldGained,
                drops = new Dictionary<string, int>(dropHistogram),
                performance = new List<PerformanceSample>(perfSamples)
            };
            return snapshot;
        }
    }

    [Serializable]
    public struct PerformanceSample
    {
        public DateTime timestamp;
        public float frameTimeMs;
        public float averageFrameTimeMs;
        public float memoryUsageMB;
        public int activeAgents;
        public int spikeOver50ms;
    }

    [Serializable]
    public struct EpisodeMetricsSnapshot
    {
        public string runId;
        public int seed;
        public string configLabel;
        public DateTime startTime;
        public DateTime endTime;
        public float survivalSeconds;
        public int kills;
        public float xpGained;
        public float goldGained;
        public Dictionary<string, int> drops;
        public List<PerformanceSample> performance;
    }
}
