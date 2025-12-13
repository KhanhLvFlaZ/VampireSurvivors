using UnityEngine;
using UnityEngine.Profiling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;

namespace Vampire.RL
{
    /// <summary>
    /// Comprehensive profiling system for detecting performance bottlenecks in RL system
    /// Tracks component-level performance, memory allocations, and identifies slow operations
    /// Requirement: 5.4 - Profiling system for bottleneck detection
    /// </summary>
    public class RLProfilingSystem : MonoBehaviour
    {
        [Header("Profiling Settings")]
        [SerializeField] private bool enableProfiling = true;
        [SerializeField] private bool enableDetailedProfiling = false;
        [SerializeField] private float profilingInterval = 5f; // Generate reports every 5 seconds
        [SerializeField] private int maxProfileSamples = 100;

        [Header("Bottleneck Detection")]
        [SerializeField] private float bottleneckThresholdMs = 5f; // Operations over 5ms are bottlenecks
        [SerializeField] private int minSamplesForBottleneck = 3; // Need 3 samples to confirm bottleneck
        [SerializeField] private bool autoLogBottlenecks = true;

        [Header("Memory Profiling")]
        [SerializeField] private bool enableMemoryProfiling = true;
        [SerializeField] private long allocationThresholdBytes = 1024 * 1024; // 1MB threshold

        private Dictionary<string, ProfileData> profileData;
        private Dictionary<string, List<float>> performanceSamples;
        private Dictionary<string, MemoryProfile> memoryProfiles;
        private List<BottleneckInfo> detectedBottlenecks;
        private float lastReportTime;

        public event Action<ProfilingReport> OnProfilingReportGenerated;
        public event Action<BottleneckInfo> OnBottleneckDetected;
        public event Action<MemoryAllocationWarning> OnMemoryAllocationWarning;

        private void Awake()
        {
            profileData = new Dictionary<string, ProfileData>();
            performanceSamples = new Dictionary<string, List<float>>();
            memoryProfiles = new Dictionary<string, MemoryProfile>();
            detectedBottlenecks = new List<BottleneckInfo>();
        }

        private void Update()
        {
            if (!enableProfiling)
                return;

            // Generate periodic reports
            if (Time.time - lastReportTime >= profilingInterval)
            {
                GenerateProfilingReport();
                lastReportTime = Time.time;
            }
        }

        /// <summary>
        /// Begin profiling a code section
        /// Usage: using (profiler.BeginProfileScope("MyOperation")) { ... }
        /// Requirement: 5.4
        /// </summary>
        public IDisposable BeginProfileScope(string operationName)
        {
            if (!enableProfiling)
                return new NoOpDisposable();

            return new ProfilingScope(this, operationName);
        }

        /// <summary>
        /// Record execution time for an operation
        /// </summary>
        public void RecordPerformance(string operationName, float durationMs)
        {
            if (!enableProfiling)
                return;

            try
            {
                // Update profile data
                if (!profileData.ContainsKey(operationName))
                {
                    profileData[operationName] = new ProfileData
                    {
                        operationName = operationName,
                        samples = new List<float>()
                    };
                }

                var data = profileData[operationName];
                data.samples.Add(durationMs);
                data.callCount++;
                data.totalTime += durationMs;
                data.lastCallTime = Time.time;

                // Keep limited samples
                if (data.samples.Count > maxProfileSamples)
                {
                    data.samples.RemoveAt(0);
                }

                // Update statistics
                data.averageTime = data.totalTime / data.callCount;
                data.maxTime = Mathf.Max(data.maxTime, durationMs);
                data.minTime = data.minTime == 0 ? durationMs : Mathf.Min(data.minTime, durationMs);

                // Check for bottleneck
                if (durationMs > bottleneckThresholdMs)
                {
                    CheckForBottleneck(operationName, durationMs);
                }

                // Detailed profiling with Unity Profiler
                if (enableDetailedProfiling)
                {
                    Profiler.BeginSample(operationName);
                    Profiler.EndSample();
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("RLProfilingSystem", "RecordPerformance", ex, operationName);
            }
        }

        /// <summary>
        /// Record memory allocation
        /// </summary>
        public void RecordMemoryAllocation(string operationName, long bytesAllocated)
        {
            if (!enableProfiling || !enableMemoryProfiling)
                return;

            try
            {
                if (!memoryProfiles.ContainsKey(operationName))
                {
                    memoryProfiles[operationName] = new MemoryProfile
                    {
                        operationName = operationName
                    };
                }

                var profile = memoryProfiles[operationName];
                profile.totalAllocations += bytesAllocated;
                profile.allocationCount++;
                profile.averageAllocation = profile.totalAllocations / profile.allocationCount;
                profile.peakAllocation = Math.Max(profile.peakAllocation, bytesAllocated);

                // Check for large allocations
                if (bytesAllocated > allocationThresholdBytes)
                {
                    var warning = new MemoryAllocationWarning
                    {
                        operationName = operationName,
                        bytesAllocated = bytesAllocated,
                        threshold = allocationThresholdBytes,
                        timestamp = DateTime.Now
                    };

                    OnMemoryAllocationWarning?.Invoke(warning);

                    if (autoLogBottlenecks)
                    {
                        UnityEngine.Debug.LogWarning($"Large memory allocation detected: {operationName} " +
                                                    $"allocated {bytesAllocated / (1024f * 1024f):F2}MB");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("RLProfilingSystem", "RecordMemoryAllocation", ex, operationName);
            }
        }

        /// <summary>
        /// Check if operation is a consistent bottleneck
        /// </summary>
        private void CheckForBottleneck(string operationName, float durationMs)
        {
            if (!profileData.ContainsKey(operationName))
                return;

            var data = profileData[operationName];

            // Check if operation consistently exceeds threshold
            int recentSlowSamples = data.samples
                .Skip(Math.Max(0, data.samples.Count - minSamplesForBottleneck))
                .Count(s => s > bottleneckThresholdMs);

            if (recentSlowSamples >= minSamplesForBottleneck)
            {
                // Check if already reported
                var existing = detectedBottlenecks.FirstOrDefault(b => b.operationName == operationName);

                if (existing == null)
                {
                    var bottleneck = new BottleneckInfo
                    {
                        operationName = operationName,
                        averageDurationMs = data.averageTime,
                        peakDurationMs = data.maxTime,
                        occurrenceCount = recentSlowSamples,
                        detectedAt = DateTime.Now,
                        severity = GetBottleneckSeverity(durationMs)
                    };

                    detectedBottlenecks.Add(bottleneck);
                    OnBottleneckDetected?.Invoke(bottleneck);

                    if (autoLogBottlenecks)
                    {
                        UnityEngine.Debug.LogWarning($"Performance bottleneck detected: {operationName} " +
                                                    $"({data.averageTime:F2}ms avg, {data.maxTime:F2}ms peak)");
                    }
                }
                else
                {
                    // Update existing bottleneck
                    existing.occurrenceCount++;
                    existing.averageDurationMs = data.averageTime;
                    existing.peakDurationMs = data.maxTime;
                }
            }
        }

        /// <summary>
        /// Generate comprehensive profiling report
        /// Requirement: 5.4
        /// </summary>
        public ProfilingReport GenerateProfilingReport()
        {
            var report = new ProfilingReport
            {
                timestamp = DateTime.Now,
                totalOperations = profileData.Count,
                bottlenecks = new List<BottleneckInfo>(detectedBottlenecks)
            };

            // Find top slowest operations
            report.slowestOperations = profileData.Values
                .OrderByDescending(d => d.averageTime)
                .Take(10)
                .Select(d => new OperationSummary
                {
                    name = d.operationName,
                    averageMs = d.averageTime,
                    totalMs = d.totalTime,
                    callCount = d.callCount,
                    peakMs = d.maxTime
                })
                .ToList();

            // Find most frequent operations
            report.mostFrequentOperations = profileData.Values
                .OrderByDescending(d => d.callCount)
                .Take(10)
                .Select(d => new OperationSummary
                {
                    name = d.operationName,
                    averageMs = d.averageTime,
                    totalMs = d.totalTime,
                    callCount = d.callCount,
                    peakMs = d.maxTime
                })
                .ToList();

            // Memory profile summary
            report.memoryProfile = new MemoryProfileSummary
            {
                totalAllocations = memoryProfiles.Values.Sum(p => p.totalAllocations),
                allocationCount = memoryProfiles.Values.Sum(p => p.allocationCount),
                topAllocators = memoryProfiles.Values
                    .OrderByDescending(p => p.totalAllocations)
                    .Take(5)
                    .Select(p => new MemoryAllocationSummary
                    {
                        operationName = p.operationName,
                        totalBytes = p.totalAllocations,
                        allocationCount = p.allocationCount,
                        averageBytes = p.averageAllocation
                    })
                    .ToList()
            };

            // Calculate overall health score (0-100)
            report.performanceHealthScore = CalculateHealthScore();

            OnProfilingReportGenerated?.Invoke(report);

            if (autoLogBottlenecks && report.bottlenecks.Count > 0)
            {
                LogProfilingReport(report);
            }

            return report;
        }

        /// <summary>
        /// Get profiling data for specific operation
        /// </summary>
        public ProfileData GetProfileData(string operationName)
        {
            return profileData.ContainsKey(operationName) ? profileData[operationName] : null;
        }

        /// <summary>
        /// Get all detected bottlenecks
        /// </summary>
        public List<BottleneckInfo> GetBottlenecks()
        {
            return new List<BottleneckInfo>(detectedBottlenecks);
        }

        /// <summary>
        /// Clear all profiling data
        /// </summary>
        public void ClearProfilingData()
        {
            profileData.Clear();
            performanceSamples.Clear();
            memoryProfiles.Clear();
            detectedBottlenecks.Clear();

            UnityEngine.Debug.Log("Profiling data cleared");
        }

        /// <summary>
        /// Reset specific operation profiling
        /// </summary>
        public void ResetOperation(string operationName)
        {
            profileData.Remove(operationName);
            performanceSamples.Remove(operationName);
            memoryProfiles.Remove(operationName);
            detectedBottlenecks.RemoveAll(b => b.operationName == operationName);
        }

        private BottleneckSeverity GetBottleneckSeverity(float durationMs)
        {
            if (durationMs > bottleneckThresholdMs * 3)
                return BottleneckSeverity.Critical;
            else if (durationMs > bottleneckThresholdMs * 2)
                return BottleneckSeverity.High;
            else if (durationMs > bottleneckThresholdMs * 1.5f)
                return BottleneckSeverity.Medium;
            else
                return BottleneckSeverity.Low;
        }

        private float CalculateHealthScore()
        {
            if (profileData.Count == 0)
                return 100f;

            float score = 100f;

            // Deduct points for bottlenecks
            score -= detectedBottlenecks.Count * 10f;

            // Deduct points for slow operations
            int slowOperations = profileData.Values.Count(d => d.averageTime > bottleneckThresholdMs);
            score -= slowOperations * 5f;

            // Deduct points for large memory allocations
            int largeAllocations = memoryProfiles.Values.Count(p => p.peakAllocation > allocationThresholdBytes);
            score -= largeAllocations * 3f;

            return Mathf.Max(0f, score);
        }

        private void LogProfilingReport(ProfilingReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RL Performance Profiling Report ===");
            sb.AppendLine($"Timestamp: {report.timestamp}");
            sb.AppendLine($"Health Score: {report.performanceHealthScore:F1}/100");
            sb.AppendLine($"\nBottlenecks Detected: {report.bottlenecks.Count}");

            foreach (var bottleneck in report.bottlenecks)
            {
                sb.AppendLine($"  - {bottleneck.operationName}: {bottleneck.averageDurationMs:F2}ms avg " +
                             $"({bottleneck.severity})");
            }

            sb.AppendLine($"\nTop 5 Slowest Operations:");
            foreach (var op in report.slowestOperations.Take(5))
            {
                sb.AppendLine($"  - {op.name}: {op.averageMs:F2}ms avg, {op.callCount} calls");
            }

            UnityEngine.Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Profiling scope for automatic timing
        /// </summary>
        private class ProfilingScope : IDisposable
        {
            private RLProfilingSystem profiler;
            private string operationName;
            private Stopwatch stopwatch;
            private long startMemory;

            public ProfilingScope(RLProfilingSystem profiler, string operationName)
            {
                this.profiler = profiler;
                this.operationName = operationName;
                this.stopwatch = Stopwatch.StartNew();

                if (profiler.enableMemoryProfiling)
                {
                    this.startMemory = GC.GetTotalMemory(false);
                }
            }

            public void Dispose()
            {
                stopwatch.Stop();
                float durationMs = (float)stopwatch.Elapsed.TotalMilliseconds;
                profiler.RecordPerformance(operationName, durationMs);

                if (profiler.enableMemoryProfiling)
                {
                    long endMemory = GC.GetTotalMemory(false);
                    long allocated = endMemory - startMemory;
                    if (allocated > 0)
                    {
                        profiler.RecordMemoryAllocation(operationName, allocated);
                    }
                }
            }
        }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Profile data for an operation
    /// </summary>
    [Serializable]
    public class ProfileData
    {
        public string operationName;
        public int callCount;
        public float totalTime;
        public float averageTime;
        public float minTime;
        public float maxTime;
        public float lastCallTime;
        public List<float> samples;
    }

    /// <summary>
    /// Bottleneck information
    /// </summary>
    [Serializable]
    public class BottleneckInfo
    {
        public string operationName;
        public float averageDurationMs;
        public float peakDurationMs;
        public int occurrenceCount;
        public DateTime detectedAt;
        public BottleneckSeverity severity;
    }

    /// <summary>
    /// Bottleneck severity levels
    /// </summary>
    public enum BottleneckSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Memory profile for an operation
    /// </summary>
    [Serializable]
    public class MemoryProfile
    {
        public string operationName;
        public long totalAllocations;
        public int allocationCount;
        public long averageAllocation;
        public long peakAllocation;
    }

    /// <summary>
    /// Comprehensive profiling report
    /// </summary>
    [Serializable]
    public class ProfilingReport
    {
        public DateTime timestamp;
        public int totalOperations;
        public float performanceHealthScore;
        public List<BottleneckInfo> bottlenecks;
        public List<OperationSummary> slowestOperations;
        public List<OperationSummary> mostFrequentOperations;
        public MemoryProfileSummary memoryProfile;
    }

    /// <summary>
    /// Operation summary
    /// </summary>
    [Serializable]
    public class OperationSummary
    {
        public string name;
        public float averageMs;
        public float totalMs;
        public int callCount;
        public float peakMs;
    }

    /// <summary>
    /// Memory profile summary
    /// </summary>
    [Serializable]
    public class MemoryProfileSummary
    {
        public long totalAllocations;
        public int allocationCount;
        public List<MemoryAllocationSummary> topAllocators;
    }

    /// <summary>
    /// Memory allocation summary
    /// </summary>
    [Serializable]
    public class MemoryAllocationSummary
    {
        public string operationName;
        public long totalBytes;
        public int allocationCount;
        public long averageBytes;
    }

    /// <summary>
    /// Memory allocation warning
    /// </summary>
    public class MemoryAllocationWarning
    {
        public string operationName;
        public long bytesAllocated;
        public long threshold;
        public DateTime timestamp;
    }
}
