using UnityEngine;
using System;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Automatic quality scaling system that adjusts RL system performance based on runtime metrics
    /// Scales inference frequency, agent count, and model complexity to maintain performance targets
    /// Requirement: 5.5 - Automatic quality scaling based on performance
    /// </summary>
    public class RLQualityScaler : MonoBehaviour
    {
        [Header("Quality Levels")]
        [SerializeField] private QualityLevel currentQualityLevel = QualityLevel.High;
        [SerializeField] private bool enableAutoScaling = true;
        [SerializeField] private float scalingCheckInterval = 2f;

        [Header("Performance Targets")]
        [SerializeField] private float targetFrameTimeMs = 16f; // 60 FPS
        [SerializeField] private float targetMemoryUsageMB = 100f;
        [SerializeField] private float scalingThreshold = 0.9f; // Scale at 90% of target

        [Header("Quality Settings")]
        [SerializeField] private QualitySettings ultraSettings;
        [SerializeField] private QualitySettings highSettings;
        [SerializeField] private QualitySettings mediumSettings;
        [SerializeField] private QualitySettings lowSettings;

        [Header("Scaling Behavior")]
        [SerializeField] private float scaleUpDelay = 5f; // Wait 5s before scaling up
        [SerializeField] private float scaleDownDelay = 1f; // Scale down quickly
        [SerializeField] private int minSamplesForScaling = 3;

        private PerformanceMonitor performanceMonitor;
        private RLProfilingSystem profilingSystem;
        private PerformanceOptimizationManager optimizationManager;

        private float lastScalingTime;
        private float lastScaleUpAttempt;
        private Queue<PerformanceMeasurement> performanceHistory;
        private QualityLevel targetQualityLevel;

        public event Action<QualityLevel, QualityLevel> OnQualityLevelChanged;
        public event Action<ScalingReason> OnQualityScaled;

        public QualityLevel CurrentQualityLevel => currentQualityLevel;
        public QualitySettings CurrentSettings => GetSettingsForLevel(currentQualityLevel);

        private void Awake()
        {
            performanceHistory = new Queue<PerformanceMeasurement>();
            targetQualityLevel = currentQualityLevel;

            InitializeDefaultSettings();
        }

        private void Start()
        {
            performanceMonitor = FindFirstObjectByType<PerformanceMonitor>();
            profilingSystem = FindFirstObjectByType<RLProfilingSystem>();
            optimizationManager = FindFirstObjectByType<PerformanceOptimizationManager>();

            ApplyQualityLevel(currentQualityLevel);
        }

        private void Update()
        {
            if (!enableAutoScaling)
                return;

            // Check for scaling opportunities
            if (Time.time - lastScalingTime >= scalingCheckInterval)
            {
                EvaluatePerformanceAndScale();
                lastScalingTime = Time.time;
            }
        }

        /// <summary>
        /// Evaluate current performance and scale quality if needed
        /// Requirement: 5.5
        /// </summary>
        private void EvaluatePerformanceAndScale()
        {
            if (performanceMonitor == null)
                return;

            // Get current performance metrics
            var metrics = performanceMonitor.CurrentMetrics;

            // Record measurement
            var measurement = new PerformanceMeasurement
            {
                frameTimeMs = metrics.frameTimeMs,
                memoryUsageMB = metrics.memoryUsageMB,
                activeAgents = metrics.activeAgents,
                timestamp = Time.time
            };

            performanceHistory.Enqueue(measurement);

            // Keep limited history
            while (performanceHistory.Count > 10)
            {
                performanceHistory.Dequeue();
            }

            // Need minimum samples
            if (performanceHistory.Count < minSamplesForScaling)
                return;

            // Calculate average metrics
            float avgFrameTime = 0f;
            float avgMemoryUsage = 0f;
            foreach (var m in performanceHistory)
            {
                avgFrameTime += m.frameTimeMs;
                avgMemoryUsage += m.memoryUsageMB;
            }
            avgFrameTime /= performanceHistory.Count;
            avgMemoryUsage /= performanceHistory.Count;

            // Determine if scaling is needed
            bool shouldScaleDown = ShouldScaleDown(avgFrameTime, avgMemoryUsage);
            bool shouldScaleUp = ShouldScaleUp(avgFrameTime, avgMemoryUsage);

            if (shouldScaleDown)
            {
                ScaleDown();
            }
            else if (shouldScaleUp && Time.time - lastScaleUpAttempt >= scaleUpDelay)
            {
                ScaleUp();
                lastScaleUpAttempt = Time.time;
            }
        }

        /// <summary>
        /// Check if quality should be scaled down
        /// </summary>
        private bool ShouldScaleDown(float avgFrameTime, float avgMemoryUsage)
        {
            // Scale down if exceeding targets
            float frameTimeRatio = avgFrameTime / targetFrameTimeMs;
            float memoryRatio = avgMemoryUsage / targetMemoryUsageMB;

            return frameTimeRatio >= scalingThreshold || memoryRatio >= scalingThreshold;
        }

        /// <summary>
        /// Check if quality can be scaled up
        /// </summary>
        private bool ShouldScaleUp(float avgFrameTime, float avgMemoryUsage)
        {
            // Can scale up if well below targets
            float frameTimeRatio = avgFrameTime / targetFrameTimeMs;
            float memoryRatio = avgMemoryUsage / targetMemoryUsageMB;

            float scaleUpThreshold = scalingThreshold * 0.7f; // 70% of threshold

            return frameTimeRatio < scaleUpThreshold && memoryRatio < scaleUpThreshold;
        }

        /// <summary>
        /// Scale quality level down
        /// </summary>
        private void ScaleDown()
        {
            QualityLevel newLevel = currentQualityLevel;

            switch (currentQualityLevel)
            {
                case QualityLevel.Ultra:
                    newLevel = QualityLevel.High;
                    break;
                case QualityLevel.High:
                    newLevel = QualityLevel.Medium;
                    break;
                case QualityLevel.Medium:
                    newLevel = QualityLevel.Low;
                    break;
                case QualityLevel.Low:
                    // Already at lowest
                    Debug.LogWarning("RLQualityScaler: Already at lowest quality level");
                    return;
            }

            SetQualityLevel(newLevel, ScalingReason.PerformanceConstraint);
        }

        /// <summary>
        /// Scale quality level up
        /// </summary>
        private void ScaleUp()
        {
            QualityLevel newLevel = currentQualityLevel;

            switch (currentQualityLevel)
            {
                case QualityLevel.Low:
                    newLevel = QualityLevel.Medium;
                    break;
                case QualityLevel.Medium:
                    newLevel = QualityLevel.High;
                    break;
                case QualityLevel.High:
                    newLevel = QualityLevel.Ultra;
                    break;
                case QualityLevel.Ultra:
                    // Already at highest
                    return;
            }

            SetQualityLevel(newLevel, ScalingReason.PerformanceHeadroom);
        }

        /// <summary>
        /// Set quality level manually or automatically
        /// Requirement: 5.5
        /// </summary>
        public void SetQualityLevel(QualityLevel level, ScalingReason reason = ScalingReason.Manual)
        {
            if (level == currentQualityLevel)
                return;

            QualityLevel oldLevel = currentQualityLevel;
            currentQualityLevel = level;

            ApplyQualityLevel(level);

            OnQualityLevelChanged?.Invoke(oldLevel, level);
            OnQualityScaled?.Invoke(reason);

            Debug.Log($"Quality level changed: {oldLevel} -> {level} (Reason: {reason})");
        }

        /// <summary>
        /// Apply quality level settings to RL system
        /// </summary>
        private void ApplyQualityLevel(QualityLevel level)
        {
            var settings = GetSettingsForLevel(level);

            // Apply to performance monitor
            if (performanceMonitor != null)
            {
                // This would set various performance parameters
                // For now, log the change
                Debug.Log($"Applying {level} quality settings: " +
                         $"Inference freq: {settings.inferenceFrequency}Hz, " +
                         $"Max agents: {settings.maxActiveAgents}, " +
                         $"Batch size: {settings.batchSize}");
            }

            // Notify other systems
            BroadcastQualitySettings(settings);
        }

        /// <summary>
        /// Get settings for specific quality level
        /// </summary>
        private QualitySettings GetSettingsForLevel(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Ultra:
                    return ultraSettings ?? GetDefaultSettings(QualityLevel.Ultra);
                case QualityLevel.High:
                    return highSettings ?? GetDefaultSettings(QualityLevel.High);
                case QualityLevel.Medium:
                    return mediumSettings ?? GetDefaultSettings(QualityLevel.Medium);
                case QualityLevel.Low:
                    return lowSettings ?? GetDefaultSettings(QualityLevel.Low);
                default:
                    return GetDefaultSettings(QualityLevel.Medium);
            }
        }

        /// <summary>
        /// Initialize default quality settings
        /// </summary>
        private void InitializeDefaultSettings()
        {
            if (ultraSettings == null)
                ultraSettings = GetDefaultSettings(QualityLevel.Ultra);
            if (highSettings == null)
                highSettings = GetDefaultSettings(QualityLevel.High);
            if (mediumSettings == null)
                mediumSettings = GetDefaultSettings(QualityLevel.Medium);
            if (lowSettings == null)
                lowSettings = GetDefaultSettings(QualityLevel.Low);
        }

        /// <summary>
        /// Get default settings for quality level
        /// </summary>
        private QualitySettings GetDefaultSettings(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Ultra:
                    return new QualitySettings
                    {
                        inferenceFrequency = 10f,
                        maxActiveAgents = 50,
                        batchSize = 128,
                        enableVisualization = true,
                        enableDetailedProfiling = true,
                        memoryBudgetMB = 150f
                    };

                case QualityLevel.High:
                    return new QualitySettings
                    {
                        inferenceFrequency = 5f,
                        maxActiveAgents = 40,
                        batchSize = 64,
                        enableVisualization = true,
                        enableDetailedProfiling = false,
                        memoryBudgetMB = 100f
                    };

                case QualityLevel.Medium:
                    return new QualitySettings
                    {
                        inferenceFrequency = 2f,
                        maxActiveAgents = 25,
                        batchSize = 32,
                        enableVisualization = false,
                        enableDetailedProfiling = false,
                        memoryBudgetMB = 75f
                    };

                case QualityLevel.Low:
                    return new QualitySettings
                    {
                        inferenceFrequency = 1f,
                        maxActiveAgents = 15,
                        batchSize = 16,
                        enableVisualization = false,
                        enableDetailedProfiling = false,
                        memoryBudgetMB = 50f
                    };

                default:
                    return GetDefaultSettings(QualityLevel.Medium);
            }
        }

        /// <summary>
        /// Broadcast quality settings to interested systems
        /// </summary>
        private void BroadcastQualitySettings(QualitySettings settings)
        {
            // Find and notify relevant systems
            var rlSystem = FindFirstObjectByType<RLSystem>();
            if (rlSystem != null)
            {
                // Apply settings to RLSystem
                // This would be implemented in RLSystem to receive quality settings
            }

            var visualizer = FindFirstObjectByType<BehaviorVisualizer>();
            if (visualizer != null)
            {
                visualizer.ToggleVisualization(settings.enableVisualization);
            }
        }

        /// <summary>
        /// Force immediate re-evaluation
        /// </summary>
        public void ForceEvaluation()
        {
            EvaluatePerformanceAndScale();
        }

        /// <summary>
        /// Get recommended quality level based on hardware
        /// </summary>
        public QualityLevel GetRecommendedQualityLevel()
        {
            // Check system specs
            int processorCount = SystemInfo.processorCount;
            int systemMemoryMB = SystemInfo.systemMemorySize;

            // High-end systems
            if (processorCount >= 8 && systemMemoryMB >= 16384)
            {
                return QualityLevel.Ultra;
            }
            // Mid-range systems
            else if (processorCount >= 4 && systemMemoryMB >= 8192)
            {
                return QualityLevel.High;
            }
            // Low-end systems
            else if (processorCount >= 2 && systemMemoryMB >= 4096)
            {
                return QualityLevel.Medium;
            }
            // Very low-end systems
            else
            {
                return QualityLevel.Low;
            }
        }
    }

    /// <summary>
    /// Quality levels for RL system
    /// </summary>
    public enum QualityLevel
    {
        Low,
        Medium,
        High,
        Ultra
    }

    /// <summary>
    /// Quality settings configuration
    /// </summary>
    [Serializable]
    public class QualitySettings
    {
        public float inferenceFrequency;    // Inferences per second
        public int maxActiveAgents;         // Maximum concurrent RL agents
        public int batchSize;              // Batch size for inference
        public bool enableVisualization;   // Enable visual indicators
        public bool enableDetailedProfiling; // Enable detailed profiling
        public float memoryBudgetMB;       // Memory budget
    }

    /// <summary>
    /// Performance measurement sample
    /// </summary>
    [Serializable]
    public class PerformanceMeasurement
    {
        public float frameTimeMs;
        public float memoryUsageMB;
        public int activeAgents;
        public float timestamp;
    }

    /// <summary>
    /// Reason for quality scaling
    /// </summary>
    public enum ScalingReason
    {
        Manual,                  // User-initiated
        PerformanceConstraint,   // Scaled down due to poor performance
        PerformanceHeadroom,     // Scaled up due to good performance
        MemoryPressure,          // Scaled down due to memory usage
        AgentCount               // Scaled due to agent count changes
    }
}
