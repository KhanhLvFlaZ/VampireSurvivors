using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Vampire.RL
{
    /// <summary>
    /// Behavior adaptation system that enables monsters to counter-adapt to player strategies
    /// Monitors detected player strategies and adjusts monster behavior accordingly
    /// Requirement: 7.3 - Behavior adaptation system for counter-strategies
    /// </summary>
    public class BehaviorAdaptationSystem : MonoBehaviour
    {
        [Header("Adaptation Settings")]
        [SerializeField] private float adaptationStrength = 0.5f; // 0-1, how quickly to adapt
        [SerializeField] private float minAdaptationInterval = 3f; // Min time between adaptations
        [SerializeField] private bool enableCounterStrategies = true;
        [SerializeField] private int maxActiveAdaptations = 5;

        [Header("Counter-Strategy Weights")]
        [SerializeField] private float aggressiveCounterWeight = 0.8f;
        [SerializeField] private float evasiveCounterWeight = 0.7f;
        [SerializeField] private float calculatedCounterWeight = 0.6f;

        private PlayerStrategyDetector strategyDetector;
        private Dictionary<PlayerStrategy, CounterStrategy> counterStrategies;
        private List<ActiveAdaptation> activeAdaptations;
        private float lastAdaptationTime;

        public event Action<AdaptationResponse> OnAdaptationApplied;
        public event Action<PlayerStrategy, CounterStrategy> OnCounterStrategyEngaged;

        private void Awake()
        {
            counterStrategies = new Dictionary<PlayerStrategy, CounterStrategy>();
            activeAdaptations = new List<ActiveAdaptation>();

            InitializeCounterStrategies();
        }

        private void Start()
        {
            strategyDetector = FindFirstObjectByType<PlayerStrategyDetector>();
            if (strategyDetector != null)
            {
                strategyDetector.OnStrategyDetected += OnPlayerStrategyDetected;
            }
        }

        private void Update()
        {
            if (!enableCounterStrategies)
                return;

            // Update active adaptations
            UpdateAdaptations();
        }

        /// <summary>
        /// Handle detected player strategy
        /// Requirement: 7.3
        /// </summary>
        private void OnPlayerStrategyDetected(DetectedStrategy detectedStrategy)
        {
            if (Time.time - lastAdaptationTime < minAdaptationInterval)
                return;

            ApplyCounterStrategy(detectedStrategy.strategy, detectedStrategy.confidence);
            lastAdaptationTime = Time.time;
        }

        /// <summary>
        /// Apply counter-strategy to detected player strategy
        /// Requirement: 7.3
        /// </summary>
        private void ApplyCounterStrategy(PlayerStrategy playerStrategy, float confidence)
        {
            if (!counterStrategies.ContainsKey(playerStrategy))
            {
                Debug.LogWarning($"No counter-strategy defined for player strategy: {playerStrategy}");
                return;
            }

            var counter = counterStrategies[playerStrategy];

            // Create adaptation based on counter-strategy
            var adaptation = new ActiveAdaptation
            {
                playerStrategy = playerStrategy,
                counterStrategy = counter,
                startTime = Time.time,
                confidence = confidence,
                isActive = true
            };

            // Manage active adaptations
            if (activeAdaptations.Count >= maxActiveAdaptations)
            {
                // Remove least confident adaptation
                var leastConfident = activeAdaptations.OrderBy(a => a.confidence).First();
                activeAdaptations.Remove(leastConfident);
            }

            activeAdaptations.Add(adaptation);

            // Create response
            var response = new AdaptationResponse
            {
                playerStrategy = playerStrategy,
                counterStrategy = counter,
                appliedAt = DateTime.Now,
                adaptationStrength = adaptationStrength * confidence
            };

            OnCounterStrategyEngaged?.Invoke(playerStrategy, counter);
            OnAdaptationApplied?.Invoke(response);

            Debug.Log($"Counter-strategy applied: {playerStrategy} -> {counter.name} " +
                     $"(Confidence: {confidence:P}, Strength: {response.adaptationStrength:F2})");
        }

        /// <summary>
        /// Update active adaptations and fade them out
        /// </summary>
        private void UpdateAdaptations()
        {
            for (int i = activeAdaptations.Count - 1; i >= 0; i--)
            {
                var adaptation = activeAdaptations[i];
                float elapsedTime = Time.time - adaptation.startTime;

                // Fade out adaptation over time (5 second fade)
                float fadeDuration = 5f;
                if (elapsedTime > fadeDuration)
                {
                    activeAdaptations.RemoveAt(i);
                }
                else
                {
                    // Update strength based on fade
                    adaptation.currentStrength = adaptationStrength * (1f - (elapsedTime / fadeDuration));
                }
            }
        }

        /// <summary>
        /// Get composite adaptation response from all active adaptations
        /// </summary>
        public BehaviorModifier GetCompositeAdaptation()
        {
            var composite = new BehaviorModifier();

            foreach (var adaptation in activeAdaptations)
            {
                var counter = adaptation.counterStrategy;
                float strength = adaptation.currentStrength;

                // Blend adaptations
                composite.aggressivenessModifier += counter.aggressivenessAdjustment * strength;
                composite.rangePreferenceModifier += counter.rangePreferenceAdjustment * strength;
                composite.coordinationModifier += counter.coordinationBonus * strength;
                composite.speedModifier += counter.speedAdjustment * strength;
                composite.attackPatternVariance += counter.attackVariance * strength;
            }

            return composite;
        }

        /// <summary>
        /// Get current active adaptations
        /// </summary>
        public List<ActiveAdaptation> GetActiveAdaptations()
        {
            return new List<ActiveAdaptation>(activeAdaptations.Where(a => a.isActive));
        }

        /// <summary>
        /// Reset all adaptations
        /// </summary>
        public void ResetAdaptations()
        {
            activeAdaptations.Clear();
            lastAdaptationTime = 0;
        }

        /// <summary>
        /// Initialize default counter-strategies for each player strategy
        /// </summary>
        private void InitializeCounterStrategies()
        {
            // Counter to Aggressive: Focus on defensive positioning and evasion
            counterStrategies[PlayerStrategy.Aggressive] = new CounterStrategy
            {
                name = "Evasive Defense",
                description = "Evade incoming attacks while maintaining distance",
                aggressivenessAdjustment = -0.3f,
                rangePreferenceAdjustment = 1.0f,      // Increase preferred range
                coordinationBonus = 0.5f,              // Coordinate with allies
                speedAdjustment = 0.4f,                // Increase movement speed
                attackVariance = 0.3f                  // More varied attacks
            };

            // Counter to Evasive: Use range attacks and positioning
            counterStrategies[PlayerStrategy.Evasive] = new CounterStrategy
            {
                name = "Ranged Pressure",
                description = "Use ranged attacks to limit evasion space",
                aggressivenessAdjustment = 0.2f,
                rangePreferenceAdjustment = 0.5f,      // Medium range attacks
                coordinationBonus = 0.4f,
                speedAdjustment = -0.2f,               // Slight speed reduction
                attackVariance = 0.2f
            };

            // Counter to Calculated: Unpredictable behavior and overwhelming
            counterStrategies[PlayerStrategy.Calculated] = new CounterStrategy
            {
                name = "Chaos Strategy",
                description = "Use unpredictable attacks and overwhelming numbers",
                aggressivenessAdjustment = 0.4f,
                rangePreferenceAdjustment = 0.0f,      // Variable range
                coordinationBonus = 0.6f,              // High coordination
                speedAdjustment = 0.2f,
                attackVariance = 0.7f                  // High variance
            };

            // Counter to Passive: Aggressive pushing
            counterStrategies[PlayerStrategy.Passive] = new CounterStrategy
            {
                name = "Aggressive Assault",
                description = "Relentless aggressive attacks",
                aggressivenessAdjustment = 0.6f,
                rangePreferenceAdjustment = -0.3f,     // Close range
                coordinationBonus = 0.3f,
                speedAdjustment = 0.3f,
                attackVariance = 0.1f                  // Consistent attacks
            };

            // Counter to Zoning: Break established zones
            counterStrategies[PlayerStrategy.Zoning] = new CounterStrategy
            {
                name = "Zone Breaking",
                description = "Rush in to disrupt zone control",
                aggressivenessAdjustment = 0.5f,
                rangePreferenceAdjustment = -0.5f,     // Very close range
                coordinationBonus = 0.7f,
                speedAdjustment = 0.4f,
                attackVariance = 0.3f
            };

            // Default counter for unknown strategy
            counterStrategies[PlayerStrategy.Unknown] = new CounterStrategy
            {
                name = "Balanced Response",
                description = "Maintain balanced aggressive and defensive play",
                aggressivenessAdjustment = 0.1f,
                rangePreferenceAdjustment = 0.0f,
                coordinationBonus = 0.3f,
                speedAdjustment = 0.0f,
                attackVariance = 0.2f
            };
        }
    }

    /// <summary>
    /// Counter-strategy configuration
    /// </summary>
    [Serializable]
    public class CounterStrategy
    {
        public string name;
        public string description;
        public float aggressivenessAdjustment;      // Change to attack frequency
        public float rangePreferenceAdjustment;     // Preferred attack range
        public float coordinationBonus;             // Bonus to team coordination
        public float speedAdjustment;               // Movement/attack speed modifier
        public float attackVariance;                // Unpredictability of attacks
    }

    /// <summary>
    /// Active adaptation being applied
    /// </summary>
    [Serializable]
    public class ActiveAdaptation
    {
        public PlayerStrategy playerStrategy;
        public CounterStrategy counterStrategy;
        public float startTime;
        public float confidence;
        public float currentStrength;
        public bool isActive;
    }

    /// <summary>
    /// Response to player strategy adaptation
    /// </summary>
    [Serializable]
    public class AdaptationResponse
    {
        public PlayerStrategy playerStrategy;
        public CounterStrategy counterStrategy;
        public DateTime appliedAt;
        public float adaptationStrength;
    }

    /// <summary>
    /// Behavior modifications from adaptations
    /// </summary>
    [Serializable]
    public class BehaviorModifier
    {
        public float aggressivenessModifier;
        public float rangePreferenceModifier;
        public float coordinationModifier;
        public float speedModifier;
        public float attackPatternVariance;

        /// <summary>
        /// Clamp all modifiers to reasonable ranges
        /// </summary>
        public void Clamp()
        {
            aggressivenessModifier = Mathf.Clamp(aggressivenessModifier, -1f, 1f);
            rangePreferenceModifier = Mathf.Clamp(rangePreferenceModifier, -2f, 2f);
            coordinationModifier = Mathf.Clamp(coordinationModifier, -1f, 1f);
            speedModifier = Mathf.Clamp(speedModifier, -0.5f, 0.5f);
            attackPatternVariance = Mathf.Clamp(attackPatternVariance, 0f, 1f);
        }
    }
}
