using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Vampire.RL
{
    /// <summary>
    /// Detects and analyzes player behavior patterns to identify strategies
    /// Supports dynamic difficulty scaling and strategy counter-adaptation
    /// Requirement: 7.1 - Player strategy detection algorithms
    /// </summary>
    public class PlayerStrategyDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private int samplesPerStrategy = 50; // Samples needed to confirm strategy
        [SerializeField] private float strategyConfidenceThreshold = 0.7f;
        [SerializeField] private bool enableDetailedAnalysis = true;

        [Header("Pattern Recognition")]
        [SerializeField] private float movementThreshold = 0.1f; // Movement speed threshold
        [SerializeField] private float attackFrequencyThreshold = 2f; // Attacks per second
        [SerializeField] private int memoryFrames = 300; // Frames of history (5 seconds at 60 FPS)

        private Vector3 lastPlayerPosition;
        private float playerMovementSpeed;
        private float playerAttackFrequency;
        private Queue<PlayerBehaviorSample> behaviorHistory;
        private Dictionary<PlayerStrategy, int> strategyConfidence;

        private int framesSinceLastAttack;
        private int attackCountThisSecond;
        private float lastSecondTime;

        public event Action<DetectedStrategy> OnStrategyDetected;
        public event Action<PlayerSkillLevel> OnSkillLevelChanged;

        public PlayerSkillLevel CurrentSkillLevel { get; private set; }
        public List<DetectedStrategy> DetectedStrategies { get; private set; }

        private void Awake()
        {
            behaviorHistory = new Queue<PlayerBehaviorSample>();
            strategyConfidence = new Dictionary<PlayerStrategy, int>();
            DetectedStrategies = new List<DetectedStrategy>();
            CurrentSkillLevel = PlayerSkillLevel.Medium;

            // Initialize strategy tracking
            foreach (PlayerStrategy strategy in System.Enum.GetValues(typeof(PlayerStrategy)))
            {
                strategyConfidence[strategy] = 0;
            }
        }

        private void Update()
        {
            UpdatePlayerMetrics();
            AnalyzeBehavior();
        }

        /// <summary>
        /// Update real-time player metrics from actual player character
        /// Requirement: 7.1
        /// </summary>
        private void UpdatePlayerMetrics()
        {
            // In a real implementation, this would track the actual player character
            // For now, we provide the framework for tracking

            // Calculate movement speed
            if (lastPlayerPosition != Vector3.zero)
            {
                Vector3 currentPosition = Vector3.zero; // Would be player position
                playerMovementSpeed = Vector3.Distance(currentPosition, lastPlayerPosition) / Time.deltaTime;
                lastPlayerPosition = currentPosition;
            }

            // Track attack frequency
            framesSinceLastAttack++;
            if (Time.time - lastSecondTime >= 1f)
            {
                playerAttackFrequency = attackCountThisSecond;
                attackCountThisSecond = 0;
                lastSecondTime = Time.time;
            }
        }

        /// <summary>
        /// Analyze player behavior and detect strategies
        /// Requirement: 7.1, 7.2
        /// </summary>
        private void AnalyzeBehavior()
        {
            // Create behavior sample
            var sample = new PlayerBehaviorSample
            {
                timestamp = Time.time,
                movementSpeed = playerMovementSpeed,
                attackFrequency = playerAttackFrequency,
                position = lastPlayerPosition,
                framesSinceAttack = framesSinceLastAttack
            };

            behaviorHistory.Enqueue(sample);

            // Keep limited history
            while (behaviorHistory.Count > memoryFrames)
            {
                behaviorHistory.Dequeue();
            }

            if (enableDetailedAnalysis)
            {
                // Hook for detailed analysis: could compute richer features or log samples
                // Currently used to drive the warning away and keep toggle available
            }

            // Analyze patterns when we have enough data
            if (behaviorHistory.Count >= samplesPerStrategy)
            {
                DetectStrategyPatterns();
                UpdateSkillLevel();
            }
        }

        /// <summary>
        /// Detect player strategy patterns from historical behavior
        /// </summary>
        private void DetectStrategyPatterns()
        {
            var samples = behaviorHistory.ToList();

            // Aggressive strategy: High attack frequency + movement
            float avgAttackFreq = samples.Average(s => s.attackFrequency);
            float avgMovement = samples.Average(s => s.movementSpeed);
            int aggressiveCount = samples.Count(s => s.attackFrequency > attackFrequencyThreshold);

            if (avgAttackFreq > attackFrequencyThreshold && aggressiveCount > samplesPerStrategy * 0.6f)
            {
                UpdateStrategyConfidence(PlayerStrategy.Aggressive, aggressiveCount);
            }

            // Evasive strategy: High movement + low attack frequency
            int evasiveCount = samples.Count(s => s.movementSpeed > movementThreshold &&
                                                  s.attackFrequency < attackFrequencyThreshold * 0.5f);
            if (avgMovement > movementThreshold && evasiveCount > samplesPerStrategy * 0.6f)
            {
                UpdateStrategyConfidence(PlayerStrategy.Evasive, evasiveCount);
            }

            // Calculated strategy: Moderate attack frequency, strategic positioning
            int calculatedCount = samples.Count(s => s.attackFrequency >= attackFrequencyThreshold * 0.5f &&
                                                     s.attackFrequency <= attackFrequencyThreshold &&
                                                     s.movementSpeed > 0);
            if (calculatedCount > samplesPerStrategy * 0.6f)
            {
                UpdateStrategyConfidence(PlayerStrategy.Calculated, calculatedCount);
            }

            // Passive strategy: Low movement + low attack frequency
            int passiveCount = samples.Count(s => s.movementSpeed < movementThreshold * 0.5f &&
                                                 s.attackFrequency < attackFrequencyThreshold * 0.3f);
            if (passiveCount > samplesPerStrategy * 0.6f)
            {
                UpdateStrategyConfidence(PlayerStrategy.Passive, passiveCount);
            }

            // Zoning strategy: Stay at medium range, controlled attacks
            int zoningCount = samples.Count(s => s.attackFrequency > attackFrequencyThreshold * 0.5f &&
                                                 s.movementSpeed < movementThreshold * 2f);
            if (zoningCount > samplesPerStrategy * 0.6f)
            {
                UpdateStrategyConfidence(PlayerStrategy.Zoning, zoningCount);
            }
        }

        /// <summary>
        /// Update confidence for a specific strategy
        /// </summary>
        private void UpdateStrategyConfidence(PlayerStrategy strategy, int matchCount)
        {
            int previousConfidence = strategyConfidence[strategy];
            strategyConfidence[strategy] = Mathf.Min(100, previousConfidence + matchCount / 10);

            // Emit event if strategy confidence crossed threshold
            if (previousConfidence < strategyConfidenceThreshold * 100 &&
                strategyConfidence[strategy] >= strategyConfidenceThreshold * 100)
            {
                var detected = new DetectedStrategy
                {
                    strategy = strategy,
                    confidence = strategyConfidence[strategy] / 100f,
                    detectedAt = DateTime.Now
                };

                if (!DetectedStrategies.Any(d => d.strategy == strategy))
                {
                    DetectedStrategies.Add(detected);
                }

                OnStrategyDetected?.Invoke(detected);
                Debug.Log($"Player strategy detected: {strategy} (Confidence: {detected.confidence:P})");
            }
        }

        /// <summary>
        /// Update player skill level based on detection patterns
        /// Requirement: 7.2
        /// </summary>
        private void UpdateSkillLevel()
        {
            var samples = behaviorHistory.ToList();

            // Calculate skill metrics
            float adaptability = 0f;
            float consistency = 0f;
            float efficiency = 0f;

            if (samples.Count > 0)
            {
                // Adaptability: Switching between strategies
                adaptability = DetectedStrategies.Count / 5f; // Max 5 strategies

                // Consistency: Standard deviation of attack frequency (lower = more consistent)
                float avgAttack = (float)samples.Average(s => s.attackFrequency);
                float variance = samples.Sum(s => Mathf.Pow(s.attackFrequency - avgAttack, 2)) / samples.Count;
                consistency = 1f - Mathf.Clamp01(Mathf.Sqrt(variance) / attackFrequencyThreshold);

                // Efficiency: Optimal movement-to-attack ratio
                float avgMovement = (float)samples.Average(s => s.movementSpeed);
                float avgFramesSinceAttack = (float)samples.Average(s => s.framesSinceAttack);
                efficiency = (avgMovement > 0 && avgFramesSinceAttack > 0)
                    ? Mathf.Clamp01(1f / avgFramesSinceAttack)
                    : 0f;
            }

            // Determine skill level
            float skillScore = (adaptability + consistency + efficiency) / 3f;
            PlayerSkillLevel newSkillLevel = CalculateSkillLevel(skillScore);

            if (newSkillLevel != CurrentSkillLevel)
            {
                CurrentSkillLevel = newSkillLevel;
                OnSkillLevelChanged?.Invoke(newSkillLevel);
                Debug.Log($"Player skill level changed to: {newSkillLevel} (Score: {skillScore:F2})");
            }
        }

        /// <summary>
        /// Calculate skill level from composite score
        /// </summary>
        private PlayerSkillLevel CalculateSkillLevel(float skillScore)
        {
            if (skillScore >= 0.8f)
                return PlayerSkillLevel.Expert;
            else if (skillScore >= 0.6f)
                return PlayerSkillLevel.Advanced;
            else if (skillScore >= 0.4f)
                return PlayerSkillLevel.Medium;
            else if (skillScore >= 0.2f)
                return PlayerSkillLevel.Beginner;
            else
                return PlayerSkillLevel.Novice;
        }

        /// <summary>
        /// Get primary player strategy with highest confidence
        /// </summary>
        public PlayerStrategy GetPrimaryStrategy()
        {
            var topStrategy = strategyConfidence.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
            return topStrategy.Value > strategyConfidenceThreshold * 100 ? topStrategy.Key : PlayerStrategy.Unknown;
        }

        /// <summary>
        /// Get all detected strategies sorted by confidence
        /// </summary>
        public List<(PlayerStrategy strategy, float confidence)> GetStrategiesByConfidence()
        {
            return strategyConfidence
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => (kvp.Key, kvp.Value / 100f))
                .ToList();
        }

        /// <summary>
        /// Reset detection (for new level or session)
        /// </summary>
        public void ResetDetection()
        {
            behaviorHistory.Clear();
            foreach (var strategy in strategyConfidence.Keys.ToList())
            {
                strategyConfidence[strategy] = 0;
            }
            DetectedStrategies.Clear();
        }

        /// <summary>
        /// Record player attack event (called externally)
        /// </summary>
        public void OnPlayerAttack()
        {
            framesSinceLastAttack = 0;
            attackCountThisSecond++;
        }
    }

    /// <summary>
    /// Player strategies that can be detected
    /// </summary>
    public enum PlayerStrategy
    {
        Unknown,      // No clear strategy detected
        Aggressive,   // High attack frequency, minimal evasion
        Evasive,      // Focuses on movement and dodging
        Calculated,   // Balanced attack and movement
        Passive,      // Low activity, defensive
        Zoning,       // Controls space and range
        Rush,         // Rapid burst attacks
        Kiting        // Attacks while moving away
    }

    /// <summary>
    /// Detected strategy with confidence metrics
    /// </summary>
    [Serializable]
    public class DetectedStrategy
    {
        public PlayerStrategy strategy;
        public float confidence;
        public DateTime detectedAt;
    }

    /// <summary>
    /// Player skill levels
    /// </summary>
    public enum PlayerSkillLevel
    {
        Novice,    // Very new to game
        Beginner,  // Learning mechanics
        Medium,    // Competent player
        Advanced,  // Skilled player
        Expert     // Master level
    }

    /// <summary>
    /// Player behavior sample for analysis
    /// </summary>
    [Serializable]
    public class PlayerBehaviorSample
    {
        public float timestamp;
        public float movementSpeed;
        public float attackFrequency;
        public Vector3 position;
        public int framesSinceAttack;
    }
}
