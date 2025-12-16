using UnityEngine;
using System;
using System.Collections.Generic;
using Vampire;
using Vampire.RL.Training;

namespace Vampire.RL
{
    /// <summary>
    /// Main RL system manager that coordinates all RL components
    /// Integrates with existing game systems
    /// </summary>
    public class RLSystem : MonoBehaviour
    {
        [Header("RL System Settings")]
        [SerializeField] private bool enableRL = true;
        [SerializeField] private TrainingMode defaultTrainingMode = TrainingMode.Training;
        [SerializeField] private float maxFrameTimeMs = 16f; // Max 16ms per frame for 60 FPS
        [SerializeField] private int maxMemoryUsageMB = 100; // Max 100MB for RL components

        [Header("Network Settings")]
        [SerializeField] private NetworkArchitecture defaultArchitecture = NetworkArchitecture.Simple;
        [SerializeField] private int[] defaultHiddenLayers = new int[] { 64, 32 };
        [SerializeField] private LearningAlgorithm defaultAlgorithm = LearningAlgorithm.DQN;

        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour playerCharacter;

        // Core RL components
        private ITrainingCoordinator trainingCoordinator;
        private IBehaviorProfileManager profileManager;
        private Dictionary<MonsterType, ActionSpace> actionSpaces;
        private Dictionary<MonsterType, ILearningAgent> agentTemplates;
        private PerformanceMonitor performanceMonitor;
        private PerformanceOptimizationManager optimizationManager;
        private TrainingMetricsLogger metricsLogger;

        // Performance monitoring
        private float frameStartTime;
        private float totalRLProcessingTime;
        private int activeAgentCount;

        // System state
        private bool isInitialized = false;
        private string currentPlayerProfileId;

        public bool IsEnabled => enableRL && isInitialized;
        public TrainingMode CurrentTrainingMode => trainingCoordinator?.GetTrainingMode() ?? TrainingMode.Inference;
        public float CurrentFrameTime => totalRLProcessingTime;
        public int ActiveAgentCount => activeAgentCount;

        /// <summary>
        /// Initialize the RL system
        /// </summary>
        public void Initialize(MonoBehaviour playerCharacter, string playerProfileId = null)
        {
            if (isInitialized) return;

            this.playerCharacter = playerCharacter;
            this.currentPlayerProfileId = playerProfileId ?? "default";

            InitializeComponents();
            InitializeActionSpaces();
            InitializeAgentTemplates();
            InitializeMetricsLogger();

            isInitialized = true;

            Debug.Log($"RL System initialized with training mode: {defaultTrainingMode}");
        }

        private void InitializeMetricsLogger()
        {
            try
            {
                var loggerGO = new GameObject("TrainingMetricsLogger");
                loggerGO.transform.SetParent(transform);
                metricsLogger = loggerGO.AddComponent<TrainingMetricsLogger>();

                var config = new TrainingConfig
                {
                    learningRate = 0.001f,
                    batchSize = 32,
                    discountFactor = 0.99f,
                    entropyBonus = 0.01f,
                    algorithm = defaultAlgorithm.ToString(),
                    networkArchitecture = defaultArchitecture.ToString()
                };

                metricsLogger.Initialize(UnityEngine.Random.Range(int.MinValue, int.MaxValue), config);
                Debug.Log("Training metrics logger initialized");
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("RLSystem", "InitializeMetricsLogger", ex);
                Debug.LogWarning("Failed to initialize metrics logger; continuing without logging");
            }
        }

        private void InitializeComponents()
        {
            try
            {
                // Initialize performance monitor first
                var monitorGO = new GameObject("PerformanceMonitor");
                monitorGO.transform.SetParent(transform);
                performanceMonitor = monitorGO.AddComponent<PerformanceMonitor>();

                // Initialize performance optimization manager
                var optimizationGO = new GameObject("PerformanceOptimizationManager");
                optimizationGO.transform.SetParent(transform);
                optimizationManager = optimizationGO.AddComponent<PerformanceOptimizationManager>();

                // Initialize training coordinator
                var coordinatorGO = new GameObject("TrainingCoordinator");
                coordinatorGO.transform.SetParent(transform);
                trainingCoordinator = coordinatorGO.AddComponent<TrainingCoordinator>();
                trainingCoordinator.Initialize(playerCharacter);
                trainingCoordinator.SetTrainingMode(defaultTrainingMode);

                // Initialize profile manager
                profileManager = new BehaviorProfileManager();
                profileManager.Initialize(currentPlayerProfileId);

                Debug.Log("RL System components initialized successfully");
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("RLSystem", "InitializeComponents", ex);

                // Try to continue with fallback components
                InitializeFallbackComponents();
            }
        }

        private void InitializeFallbackComponents()
        {
            try
            {
                Debug.LogWarning("[RL FALLBACK] Initializing fallback components due to initialization failure");

                // Create minimal profile manager
                if (profileManager == null)
                {
                    profileManager = new BehaviorProfileManager();
                    profileManager.Initialize(currentPlayerProfileId ?? "fallback");
                }

                // Performance monitor is optional for fallback mode
                if (performanceMonitor == null)
                {
                    Debug.LogWarning("[RL FALLBACK] Performance monitoring disabled");
                }

                Debug.Log("[RL FALLBACK] Fallback components initialized");
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("RLSystem", "InitializeFallbackComponents", ex);
                Debug.LogError("[RL CRITICAL] Failed to initialize even fallback components. RL system may not function properly.");
            }
        }

        private void InitializeActionSpaces()
        {
            actionSpaces = new Dictionary<MonsterType, ActionSpace>();

            // Initialize action spaces using MonsterRLConfig
            foreach (MonsterType monsterType in System.Enum.GetValues(typeof(MonsterType)))
            {
                if (monsterType == MonsterType.None) continue;

                var config = MonsterRLConfig.CreateDefault(monsterType);
                actionSpaces[monsterType] = config.actionSpace;
            }
        }

        private void InitializeAgentTemplates()
        {
            agentTemplates = new Dictionary<MonsterType, ILearningAgent>();

            foreach (var monsterType in System.Enum.GetValues(typeof(MonsterType)))
            {
                if ((MonsterType)monsterType == MonsterType.None) continue;

                var agentGO = new GameObject($"AgentTemplate_{monsterType}");
                agentGO.transform.SetParent(transform);
                agentGO.SetActive(false); // Templates are inactive

                var agent = agentGO.AddComponent<DQNLearningAgent>();
                agent.Initialize((MonsterType)monsterType, actionSpaces[(MonsterType)monsterType]);

                agentTemplates[(MonsterType)monsterType] = agent;
            }
        }

        /// <summary>
        /// Create ActionDecoder for a specific monster type
        /// </summary>
        public IActionDecoder CreateActionDecoder(MonsterType monsterType)
        {
            if (!IsEnabled || !actionSpaces.ContainsKey(monsterType))
                return null;

            return ActionDecoderFactory.CreateDecoder(monsterType, actionSpaces[monsterType]);
        }

        /// <summary>
        /// Get MonsterRLConfig for a specific monster type
        /// </summary>
        public MonsterRLConfig GetMonsterConfig(MonsterType monsterType)
        {
            return MonsterRLConfig.CreateDefault(monsterType);
        }

        void Update()
        {
            if (!IsEnabled) return;

            frameStartTime = Time.realtimeSinceStartup;

            try
            {
                // Update training coordinator
                trainingCoordinator?.UpdateAgents();
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("RLSystem", "UpdateAgents", ex);
            }

            // Monitor performance
            totalRLProcessingTime = (Time.realtimeSinceStartup - frameStartTime) * 1000f; // Convert to ms

            // Update performance monitor
            if (performanceMonitor != null)
            {
                float memoryUsage = GetMemoryUsageMB();
                performanceMonitor.UpdateSystemMetrics(totalRLProcessingTime, memoryUsage, activeAgentCount);
                performanceMonitor.RecordComponentPerformance("RLSystem", totalRLProcessingTime);
            }

            // Check performance constraints and apply degradation if needed
            if (totalRLProcessingTime > maxFrameTimeMs)
            {
                ErrorHandler.LogPerformanceIssue("RLSystem", "FrameTime", totalRLProcessingTime, maxFrameTimeMs,
                    "Consider reducing batch size or limiting agents per frame");
            }
        }

        /// <summary>
        /// Create a learning agent for a specific monster
        /// </summary>
        public ILearningAgent CreateAgentForMonster(MonsterType monsterType)
        {
            if (!IsEnabled || !actionSpaces.ContainsKey(monsterType))
                return null;

            try
            {
                // Check if we should disable this component due to repeated failures
                if (ErrorHandler.ShouldDisableComponent($"Agent_{monsterType}"))
                {
                    Debug.LogWarning($"[RL FALLBACK] Agent creation disabled for {monsterType} due to repeated failures. Using fallback agent.");
                    return CreateFallbackAgent(monsterType);
                }

                var agentGO = new GameObject($"LearningAgent_{monsterType}_{System.Guid.NewGuid()}");

                var newAgent = agentGO.AddComponent<DQNLearningAgent>();
                newAgent.Initialize(monsterType, actionSpaces[monsterType]);

                // Load existing behavior profile if available
                var profile = profileManager?.LoadProfile(monsterType);
                if (profile != null && profile.IsValid())
                {
                    newAgent.LoadBehaviorProfile(GetProfilePath(profile));
                }

                // Register with training coordinator
                trainingCoordinator?.RegisterAgent(newAgent, monsterType);
                activeAgentCount++;

                // Reset error count on successful creation
                ErrorHandler.ResetComponentErrors($"Agent_{monsterType}");

                return newAgent;
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("RLSystem", "CreateAgentForMonster", ex, monsterType.ToString());

                // Try to recover with fallback agent
                return ErrorHandler.RecoverFailedAgent(monsterType, actionSpaces[monsterType], ex) ?? CreateFallbackAgent(monsterType);
            }
        }

        private ILearningAgent CreateFallbackAgent(MonsterType monsterType)
        {
            try
            {
                var fallbackGO = new GameObject($"FallbackAgent_{monsterType}_{System.Guid.NewGuid()}");
                var fallbackAgent = fallbackGO.AddComponent<FallbackLearningAgent>();
                fallbackAgent.Initialize(monsterType, actionSpaces[monsterType]);

                activeAgentCount++;
                Debug.Log($"[RL FALLBACK] Created fallback agent for {monsterType}");

                return fallbackAgent;
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("RLSystem", "CreateFallbackAgent", ex, monsterType.ToString());
                return null; // Caller should handle with default scripted behavior
            }
        }

        /// <summary>
        /// Register an agent with the training coordinator
        /// </summary>
        public void RegisterAgent(ILearningAgent agent, MonsterType monsterType)
        {
            if (!IsEnabled || agent == null) return;

            trainingCoordinator?.RegisterAgent(agent, monsterType);
        }

        /// <summary>
        /// Unregister an agent from the training coordinator
        /// </summary>
        public void UnregisterAgent(ILearningAgent agent)
        {
            if (agent == null) return;

            trainingCoordinator?.UnregisterAgent(agent);
        }

        /// <summary>
        /// Destroy a learning agent
        /// </summary>
        public void DestroyAgent(ILearningAgent agent)
        {
            if (agent == null) return;

            trainingCoordinator?.UnregisterAgent(agent);
            activeAgentCount = Mathf.Max(0, activeAgentCount - 1);

            if (agent is MonoBehaviour agentMono)
            {
                Destroy(agentMono.gameObject);
            }
        }

        /// <summary>
        /// Set training mode for all agents
        /// </summary>
        public void SetTrainingMode(TrainingMode mode)
        {
            trainingCoordinator?.SetTrainingMode(mode);
        }

        /// <summary>
        /// Save all behavior profiles
        /// </summary>
        public void SaveAllProfiles()
        {
            trainingCoordinator?.SaveAllProfiles();
        }

        /// <summary>
        /// Load all behavior profiles
        /// </summary>
        public void LoadAllProfiles()
        {
            trainingCoordinator?.LoadAllProfiles();
        }

        /// <summary>
        /// Get learning metrics for all monster types
        /// </summary>
        public Dictionary<MonsterType, LearningMetrics> GetAllMetrics()
        {
            return trainingCoordinator?.GetAllMetrics() ?? new Dictionary<MonsterType, LearningMetrics>();
        }

        /// <summary>
        /// Reset all learning progress
        /// </summary>
        public void ResetAllProgress()
        {
            trainingCoordinator?.ResetAllProgress();
        }

        /// <summary>
        /// Get profile path from BehaviorProfile
        /// </summary>
        private string GetProfilePath(BehaviorProfile profile)
        {
            if (profile == null) return "";
            return $"{profileManager?.ProfileDirectory ?? "Profiles"}/{profile.profileId}.json";
        }

        /// <summary>
        /// Get action space for monster type
        /// </summary>
        public ActionSpace GetActionSpace(MonsterType monsterType)
        {
            return actionSpaces.ContainsKey(monsterType) ? actionSpaces[monsterType] : ActionSpace.CreateDefault();
        }

        /// <summary>
        /// Log step metrics for training (milli-call: cheap logging)
        /// </summary>
        public void LogTrainingStep(float reward, float loss, int activeAgents)
        {
            metricsLogger?.LogStep(reward, loss, activeAgents);
        }

        /// <summary>
        /// Log episode metrics completion
        /// </summary>
        public void LogEpisodeComplete(float episodeReward, float episodeLength, Dictionary<MonsterType, LearningMetrics> metrics)
        {
            metricsLogger?.LogEpisode(episodeReward, episodeLength, metrics);
        }

        /// <summary>
        /// Log evaluation run results
        /// </summary>
        public void LogEvaluation(float evalReward, float survivalSeconds, int kills, float avgFps, float p99FrameTime)
        {
            metricsLogger?.LogEvaluation(evalReward, survivalSeconds, kills, avgFps, p99FrameTime);
        }

        /// <summary>
        /// Export all collected training metrics
        /// </summary>
        public void ExportTrainingMetrics()
        {
            metricsLogger?.ExportMetrics();
        }
        /// Check if system meets performance constraints
        /// </summary>
        public bool MeetsPerformanceConstraints()
        {
            return totalRLProcessingTime <= maxFrameTimeMs &&
                   GetMemoryUsageMB() <= maxMemoryUsageMB;
        }

        /// <summary>
        /// Get comprehensive performance report
        /// </summary>
        public PerformanceReport GetPerformanceReport()
        {
            return optimizationManager?.GetPerformanceReport();
        }

        /// <summary>
        /// Force performance optimization
        /// </summary>
        public void OptimizePerformance()
        {
            optimizationManager?.ForceOptimization();
        }

        /// <summary>
        /// Get current performance optimization status
        /// </summary>
        public string GetOptimizationStatus()
        {
            if (optimizationManager == null) return "Optimization Manager not initialized";

            var report = optimizationManager.GetPerformanceReport();
            if (report?.currentSnapshot == null) return "No performance data available";

            return $"Strategy: {report.optimizationStrategy}, " +
                   $"Frame Time: {report.currentSnapshot.frameTimeMs:F1}ms, " +
                   $"Memory: {report.currentSnapshot.memoryUsageMB:F1}MB, " +
                   $"Batch Size: {report.currentSnapshot.currentBatchSize}, " +
                   $"Emergency: {(report.emergencyModeActive ? "ACTIVE" : "Inactive")}";
        }

        private float GetMemoryUsageMB()
        {
            // Simplified memory usage calculation
            // In production, use Unity Profiler API for accurate measurement
            return (activeAgentCount * 10f) + (profileManager?.GetStorageSize() ?? 0) / (1024f * 1024f);
        }



        private void OnDestroy()
        {
            if (isInitialized)
            {
                ExportTrainingMetrics();
                SaveAllProfiles();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus && isInitialized)
            {
                SaveAllProfiles();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isInitialized)
            {
                SaveAllProfiles();
            }
        }
    }
}