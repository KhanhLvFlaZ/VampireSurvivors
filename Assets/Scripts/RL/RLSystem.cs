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
        [Tooltip("Minimum interval between RL decision updates (seconds), caps per-frame cost when many agents are active")]
        [SerializeField] private float decisionIntervalSeconds = 0.1f;
        [Tooltip("Upper bound on agent updates per tick to avoid spikes")]
        [SerializeField] private int maxAgentUpdatesPerTick = 16;

        [Header("Inference Cost Control")]
        [Tooltip("Maximum RL agents before falling back to scripted behavior")]
        [SerializeField] private int maxRLAgents = 50;
        [Tooltip("Target latency budget in milliseconds")]
        [SerializeField] private float targetLatencyMs = 16f;
        [Tooltip("Estimated inference cost per agent (ms)")]
        [SerializeField] private float latencyPerAgentMs = 0.3f;
        [Tooltip("Enable dynamic adjustment of max agents based on performance")]
        [SerializeField] private bool enableDynamicLimit = true;
        [Tooltip("Maximum batch size for inference batching")]
        [SerializeField] private int maxBatchSize = 32;
        [Tooltip("Batch timeout in milliseconds")]
        [SerializeField] private float batchTimeoutMs = 5f;
        [Tooltip("Enable inference batching")]
        [SerializeField] private bool enableBatching = true;

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
        private CheckpointManager checkpointManager;
        private TrainingController trainingController;

        // Inference cost control
        private InferenceBatcher inferenceBatcher;
        private RLSpawnLimiter spawnLimiter;

        // Performance monitoring
        private float frameStartTime;
        private float totalRLProcessingTime;
        private int activeAgentCount;
        private float lastDecisionUpdateTime;

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

            // Defer heavy initialization to prevent frame spike
            StartCoroutine(InitializeComponentsGradually());
        }

        /// <summary>
        /// Initialize components gradually across frames to prevent freeze
        /// </summary>
        private System.Collections.IEnumerator InitializeComponentsGradually()
        {
            // Phase 1: Core components
            InitializeComponents();
            yield return null;

            // Phase 2: Action spaces
            InitializeActionSpaces();
            yield return null;

            // Phase 3: Agent templates
            InitializeAgentTemplates();
            yield return null;

            // Phase 4: Metrics logger
            InitializeMetricsLogger();
            yield return null;

            // Phase 5: Inference cost control
            InitializeInferenceCostControl();
            yield return null;

            // Complete
            isInitialized = true;
            Debug.Log($"RL System initialized gradually with training mode: {defaultTrainingMode}");
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

        private void InitializeInferenceCostControl()
        {
            try
            {
                // Initialize inference batcher
                if (enableBatching)
                {
                    inferenceBatcher = new InferenceBatcher(maxBatchSize, batchTimeoutMs);
                    Debug.Log($"Inference batcher initialized (batch size: {maxBatchSize}, timeout: {batchTimeoutMs}ms)");
                }

                // Initialize spawn limiter
                spawnLimiter = new RLSpawnLimiter(
                    maxRLAgents,
                    targetLatencyMs,
                    latencyPerAgentMs,
                    enableDynamicLimit
                );
                Debug.Log($"RL spawn limiter initialized (max agents: {maxRLAgents}, target latency: {targetLatencyMs}ms)");
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("RLSystem", "InitializeInferenceCostControl", ex);
                Debug.LogWarning("Failed to initialize inference cost control; continuing with defaults");
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

                // Initialize checkpoint manager
                var checkpointGO = new GameObject("CheckpointManager");
                checkpointGO.transform.SetParent(transform);
                checkpointManager = checkpointGO.AddComponent<CheckpointManager>();
                checkpointManager.Initialize();

                // Initialize training controller
                var trainingGO = new GameObject("TrainingController");
                trainingGO.transform.SetParent(transform);
                trainingController = trainingGO.AddComponent<TrainingController>();

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
                // Throttle decision updates to reduce per-frame spikes when many agents are active
                if (Time.time - lastDecisionUpdateTime >= decisionIntervalSeconds)
                {
                    // Process batched inferences first
                    if (enableBatching && inferenceBatcher != null)
                    {
                        int processed = inferenceBatcher.ProcessBatch();
                        if (processed > 0)
                        {
                            // Update spawn limiter with batch processing time
                            float batchTime = (Time.realtimeSinceStartup - frameStartTime) * 1000f;
                            spawnLimiter?.UpdateLatency(batchTime);
                        }
                    }

                    trainingCoordinator?.UpdateAgents();
                    lastDecisionUpdateTime = Time.time;
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("RLSystem", "UpdateAgents", ex);
            }

            // Monitor performance
            totalRLProcessingTime = (Time.realtimeSinceStartup - frameStartTime) * 1000f; // Convert to ms

            // Update spawn limiter with total processing time
            if (spawnLimiter != null)
            {
                spawnLimiter.UpdateLatency(totalRLProcessingTime);
            }

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
                // Check spawn limiter - fallback to scripted if at capacity
                if (spawnLimiter != null && !spawnLimiter.CanSpawnRLAgent())
                {
                    spawnLimiter.RegisterScriptedFallback();
                    var decision = spawnLimiter.GetSpawnDecision();
                    Debug.Log($"[RL SPAWN LIMITER] Cannot spawn RL agent for {monsterType}: {decision.reason}. " +
                             $"Active: {spawnLimiter.ActiveRLAgentCount}/{spawnLimiter.MaxRLAgents}, " +
                             $"Fallbacks: {spawnLimiter.ScriptedFallbackCount}");
                    return null; // Caller should use scripted behavior
                }

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

                // Notify spawn limiter
                spawnLimiter?.RegisterRLAgent();

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

            // Notify spawn limiter
            spawnLimiter?.UnregisterRLAgent();

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

        /// <summary>
        /// Start training loop (50k-200k episodes with periodic eval).
        /// </summary>
        public void StartTraining(int totalEpisodes = 100000, int evalIntervalSteps = 10000)
        {
            if (trainingController == null)
            {
                Debug.LogError("Training controller not initialized");
                return;
            }

            trainingController.SetTotalEpisodes(totalEpisodes);
            trainingController.SetEvaluationInterval(evalIntervalSteps);
            trainingController.StartTraining();
        }

        /// <summary>
        /// Pause training loop.
        /// </summary>
        public void PauseTraining()
        {
            trainingController?.PauseTraining();
        }

        /// <summary>
        /// Resume paused training.
        /// </summary>
        public void ResumeTraining()
        {
            trainingController?.ResumeTraining();
        }

        /// <summary>
        /// Get current training progress (0-1).
        /// </summary>
        public float GetTrainingProgress()
        {
            return trainingController != null ? trainingController.GetTrainingProgress() : 0f;
        }

        /// <summary>
        /// Get current episode count.
        /// </summary>
        public int GetCurrentEpisode()
        {
            return trainingController != null ? trainingController.CurrentEpisode : 0;
        }

        /// <summary>
        /// Get best checkpoint metadata.
        /// </summary>
        public CheckpointMetadata GetBestCheckpoint()
        {
            return checkpointManager != null ? checkpointManager.GetBestCheckpoint() : null;
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

        /// <summary>
        /// Check if a new RL agent can be spawned within performance budget
        /// </summary>
        public bool CanSpawnRLAgent()
        {
            return spawnLimiter?.CanSpawnRLAgent() ?? true;
        }

        /// <summary>
        /// Get spawn limiter statistics
        /// </summary>
        public LimiterStats GetSpawnLimiterStats()
        {
            return spawnLimiter?.GetStats() ?? default;
        }

        /// <summary>
        /// Get inference batching statistics
        /// </summary>
        public BatchingStats GetBatchingStats()
        {
            return inferenceBatcher?.GetStats() ?? default;
        }

        /// <summary>
        /// Get comprehensive inference cost control status
        /// </summary>
        public string GetInferenceCostStatus()
        {
            if (spawnLimiter == null) return "Spawn limiter not initialized";

            var limiterStats = spawnLimiter.GetStats();
            var batchingStats = inferenceBatcher?.GetStats() ?? default;

            return $"RL Agents: {limiterStats.activeRLAgents}/{limiterStats.maxRLAgents} ({limiterStats.capacityUtilization:P0}), " +
                   $"Fallbacks: {limiterStats.scriptedFallbacks}, " +
                   $"Latency: {limiterStats.currentLatencyMs:F1}ms (avg: {limiterStats.averageLatencyMs:F1}ms), " +
                   $"Batching: {batchingStats.pendingRequests} pending, avg batch: {batchingStats.averageBatchSize:F1}";
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