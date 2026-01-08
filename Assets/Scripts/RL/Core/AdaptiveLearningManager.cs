using UnityEngine;
using System;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Coordinates all adaptive learning systems (strategy detection, difficulty scaling, behavior adaptation, persistence)
    /// Manages the flow of information between components and triggers learning cycles
    /// Requirement: 7.1, 7.2, 7.3, 7.4, 7.5 - Adaptive learning and personalization system
    /// </summary>
    public class AdaptiveLearningManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private bool autoInitializeSystems = true;
        [SerializeField] private float learningCycleInterval = 10f; // Learning cycle every 10 seconds

        [Header("Adaptation Tuning")]
        [SerializeField] private float strategyConfidenceThreshold = 0.7f;
        [SerializeField] private float difficultyAdjustmentThreshold = 0.3f;
        [SerializeField] private bool enableDynamicDifficulty = true;
        [SerializeField] private bool enableBehaviorAdaptation = true;

        private PlayerStrategyDetector strategyDetector;
        private DifficultyScaler difficultyScaler;
        private BehaviorAdaptationSystem behaviorAdaptation;
        private AdaptiveLearningPersistence persistence;

        private float lastLearningCycleTime;
        private List<LearningEvent> learningHistory;
        private int learningCycleCount;

        public event Action<AdaptiveLearningState> OnLearningStateChanged;
        public event Action<LearningCycleResult> OnLearningCycleCompleted;

        public PlayerStrategyDetector StrategyDetector => strategyDetector;
        public DifficultyScaler DifficultyScaler => difficultyScaler;
        public BehaviorAdaptationSystem BehaviorAdaptation => behaviorAdaptation;
        public AdaptiveLearningPersistence Persistence => persistence;

        private void Awake()
        {
            learningHistory = new List<LearningEvent>();
        }

        private void Start()
        {
            if (autoInitializeSystems)
            {
                InitializeSystems();
            }
        }

        private void Update()
        {
            if (Time.time - lastLearningCycleTime >= learningCycleInterval)
            {
                ExecuteLearningCycle();
                lastLearningCycleTime = Time.time;
            }
        }

        /// <summary>
        /// Initialize all adaptive learning systems
        /// </summary>
        public void InitializeSystems()
        {
            // Find or create strategy detector
            strategyDetector = FindFirstObjectByType<PlayerStrategyDetector>();
            if (strategyDetector == null)
            {
                var obj = new GameObject("PlayerStrategyDetector");
                strategyDetector = obj.AddComponent<PlayerStrategyDetector>();
            }

            // Find or create difficulty scaler
            difficultyScaler = FindFirstObjectByType<DifficultyScaler>();
            if (difficultyScaler == null)
            {
                var obj = new GameObject("DifficultyScaler");
                difficultyScaler = obj.AddComponent<DifficultyScaler>();
            }

            // Find or create behavior adaptation
            behaviorAdaptation = FindFirstObjectByType<BehaviorAdaptationSystem>();
            if (behaviorAdaptation == null)
            {
                var obj = new GameObject("BehaviorAdaptationSystem");
                behaviorAdaptation = obj.AddComponent<BehaviorAdaptationSystem>();
            }

            // Find or create persistence
            persistence = FindFirstObjectByType<AdaptiveLearningPersistence>();
            if (persistence == null)
            {
                var obj = new GameObject("AdaptiveLearningPersistence");
                persistence = obj.AddComponent<AdaptiveLearningPersistence>();
            }

            // Connect events
            if (strategyDetector != null)
            {
                strategyDetector.OnStrategyDetected += OnStrategyDetected;
                strategyDetector.OnSkillLevelChanged += OnPlayerSkillChanged;
            }

            if (difficultyScaler != null)
            {
                difficultyScaler.OnDifficultyChanged += OnDifficultyChanged;
            }

            if (behaviorAdaptation != null)
            {
                behaviorAdaptation.OnAdaptationApplied += OnAdaptationApplied;
            }

            Debug.Log("Adaptive learning systems initialized");
        }

        /// <summary>
        /// Execute learning cycle - analyze current state and make adjustments
        /// Requirement: 7.1, 7.2, 7.3
        /// </summary>
        private void ExecuteLearningCycle()
        {
            learningCycleCount++;
            var result = new LearningCycleResult { cycleNumber = learningCycleCount };

            try
            {
                // Phase 1: Analyze player behavior
                if (strategyDetector != null)
                {
                    var playerStrategy = strategyDetector.GetPrimaryStrategy();
                    result.detectedStrategy = playerStrategy;
                    result.playerSkillLevel = strategyDetector.CurrentSkillLevel;

                    if (playerStrategy != PlayerStrategy.Unknown)
                    {
                        result.strategyConfidence =
                            strategyDetector.GetStrategiesByConfidence().Count > 0
                            ? strategyDetector.GetStrategiesByConfidence()[0].confidence
                            : 0f;

                        if (result.strategyConfidence < strategyConfidenceThreshold)
                        {
                            // Confidence too low: skip adaptation this cycle
                            result.adaptationsApplied = false;
                            result.difficultyChanged = false;
                            OnLearningCycleCompleted?.Invoke(result);
                            return;
                        }
                    }
                }

                // Phase 2: Adjust difficulty if enabled
                if (enableDynamicDifficulty && difficultyScaler != null)
                {
                    var oldDifficulty = difficultyScaler.CurrentDifficulty;
                    // Difficulty adjustment happens automatically through events
                    result.difficultyLevel = difficultyScaler.CurrentDifficulty;
                    result.difficultyChanged = oldDifficulty != result.difficultyLevel;
                }

                // Phase 3: Apply behavior adaptations if enabled
                if (enableBehaviorAdaptation && behaviorAdaptation != null)
                {
                    var adaptations = behaviorAdaptation.GetActiveAdaptations();
                    result.activeAdaptationCount = adaptations.Count;
                    result.adaptationsApplied = true;
                }

                // Phase 4: Save progress
                if (persistence != null)
                {
                    persistence.SaveProfile("AutoSave");
                }

                result.success = true;
            }
            catch (Exception ex)
            {
                result.success = false;
                result.errorMessage = ex.Message;
                ErrorHandler.LogError("AdaptiveLearningManager", "ExecuteLearningCycle", ex);
            }

            // Record in history
            var learningEvent = new LearningEvent
            {
                timestamp = DateTime.Now,
                cycleNumber = learningCycleCount,
                result = result
            };
            learningHistory.Add(learningEvent);

            // Keep limited history
            while (learningHistory.Count > 100)
            {
                learningHistory.RemoveAt(0);
            }

            OnLearningCycleCompleted?.Invoke(result);
        }

        /// <summary>
        /// Handle strategy detection
        /// </summary>
        private void OnStrategyDetected(DetectedStrategy strategy)
        {
            Debug.Log($"Adaptive Learning: Player strategy detected - {strategy.strategy} (Confidence: {strategy.confidence:P})");

            // This event is already handled by BehaviorAdaptationSystem
            // Log for diagnostics
            RecordEvent($"Strategy detected: {strategy.strategy}");
        }

        /// <summary>
        /// Handle skill level change
        /// </summary>
        private void OnPlayerSkillChanged(PlayerSkillLevel newSkillLevel)
        {
            Debug.Log($"Adaptive Learning: Player skill level changed to {newSkillLevel}");
            RecordEvent($"Skill level changed: {newSkillLevel}");

            // Suggest difficulty adjustment based on skill
            if (difficultyScaler != null && enableDynamicDifficulty)
            {
                var suggestedDifficulty = GetSuggestedDifficulty(newSkillLevel);
                if (suggestedDifficulty != difficultyScaler.CurrentDifficulty)
                {
                    Debug.Log($"Suggesting difficulty adjustment from {difficultyScaler.CurrentDifficulty} to {suggestedDifficulty}");
                }
            }
        }

        /// <summary>
        /// Handle difficulty change
        /// </summary>
        private void OnDifficultyChanged(DifficultyLevel oldLevel, DifficultyLevel newLevel)
        {
            Debug.Log($"Adaptive Learning: Difficulty changed from {oldLevel} to {newLevel}");
            RecordEvent($"Difficulty changed: {oldLevel} -> {newLevel}");
        }

        /// <summary>
        /// Handle adaptation application
        /// </summary>
        private void OnAdaptationApplied(AdaptationResponse response)
        {
            Debug.Log($"Adaptive Learning: Counter-strategy applied - {response.counterStrategy.name}");
            RecordEvent($"Adaptation applied: {response.counterStrategy.name}");
        }

        /// <summary>
        /// Get suggested difficulty for skill level
        /// </summary>
        private DifficultyLevel GetSuggestedDifficulty(PlayerSkillLevel skillLevel)
        {
            switch (skillLevel)
            {
                case PlayerSkillLevel.Novice:
                    return DifficultyLevel.VeryEasy;
                case PlayerSkillLevel.Beginner:
                    return DifficultyLevel.Easy;
                case PlayerSkillLevel.Medium:
                    return DifficultyLevel.Normal;
                case PlayerSkillLevel.Advanced:
                    return DifficultyLevel.Hard;
                case PlayerSkillLevel.Expert:
                    return DifficultyLevel.VeryHard;
                default:
                    return DifficultyLevel.Normal;
            }
        }

        /// <summary>
        /// Record a learning event
        /// </summary>
        private void RecordEvent(string description)
        {
            var learningEvent = new LearningEvent
            {
                timestamp = DateTime.Now,
                description = description
            };

            learningHistory.Add(learningEvent);

            if (learningHistory.Count > 100)
            {
                learningHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Get learning history
        /// </summary>
        public List<LearningEvent> GetLearningHistory()
        {
            return new List<LearningEvent>(learningHistory);
        }

        /// <summary>
        /// Get current adaptive learning state
        /// </summary>
        public AdaptiveLearningState GetCurrentState()
        {
            var state = new AdaptiveLearningState
            {
                timestamp = DateTime.Now,
                learningCycleCount = learningCycleCount
            };

            if (strategyDetector != null)
            {
                state.playerStrategy = strategyDetector.GetPrimaryStrategy();
                state.playerSkillLevel = strategyDetector.CurrentSkillLevel;
            }

            if (difficultyScaler != null)
            {
                state.currentDifficulty = difficultyScaler.CurrentDifficulty;
                state.difficultyMultiplier = difficultyScaler.DifficultyMultiplier;
            }

            if (behaviorAdaptation != null)
            {
                state.activeAdaptations = behaviorAdaptation.GetActiveAdaptations().Count;
            }

            return state;
        }

        /// <summary>
        /// Reset all adaptive learning data
        /// </summary>
        public void ResetAdaptiveLearning()
        {
            if (strategyDetector != null)
                strategyDetector.ResetDetection();

            if (behaviorAdaptation != null)
                behaviorAdaptation.ResetAdaptations();

            learningHistory.Clear();
            learningCycleCount = 0;

            Debug.Log("Adaptive learning reset");
        }

        /// <summary>
        /// Save current session
        /// </summary>
        public void SaveSession(string sessionName = "")
        {
            if (persistence != null)
            {
                persistence.SaveProfile(string.IsNullOrEmpty(sessionName) ? "Session" : sessionName);
            }
        }

        /// <summary>
        /// Load a session
        /// </summary>
        public bool LoadSession(string profilePath)
        {
            if (persistence != null)
            {
                return persistence.LoadProfile(profilePath);
            }
            return false;
        }
    }

    /// <summary>
    /// Current adaptive learning state
    /// </summary>
    [Serializable]
    public class AdaptiveLearningState
    {
        public DateTime timestamp;
        public int learningCycleCount;
        public PlayerStrategy playerStrategy;
        public PlayerSkillLevel playerSkillLevel;
        public DifficultyLevel currentDifficulty;
        public float difficultyMultiplier;
        public int activeAdaptations;
    }

    /// <summary>
    /// Result of a learning cycle
    /// </summary>
    [Serializable]
    public class LearningCycleResult
    {
        public int cycleNumber;
        public PlayerStrategy detectedStrategy;
        public float strategyConfidence;
        public PlayerSkillLevel playerSkillLevel;
        public DifficultyLevel difficultyLevel;
        public bool difficultyChanged;
        public int activeAdaptationCount;
        public bool adaptationsApplied;
        public bool success;
        public string errorMessage;
    }

    /// <summary>
    /// Learning event record
    /// </summary>
    [Serializable]
    public class LearningEvent
    {
        public DateTime timestamp;
        public int cycleNumber;
        public string description;
        public LearningCycleResult result;
    }
}
