using System;
using UnityEngine;

namespace Vampire.RL.Training
{
    /// <summary>
    /// Defines an evaluation scenario for testing RL agent performance.
    /// </summary>
    [Serializable]
    public class EvaluationScenario
    {
        [SerializeField] public string scenarioName;
        [SerializeField] public EvaluationScenarioType scenarioType;
        [SerializeField] public int seed;
        [SerializeField] public float durationSeconds;
        [SerializeField] public string mapLabel = "standard";
        [SerializeField] public float spawnRateMultiplier = 1.0f;
        [SerializeField] public bool randomSeed = false;
        [SerializeField] public string description;

        /// <summary>
        /// Create a fixed-seed scenario for reproducible comparison.
        /// </summary>
        public static EvaluationScenario CreateFixedSeedScenario(int seed = 12345)
        {
            return new EvaluationScenario
            {
                scenarioName = $"Fixed_Seed_{seed}",
                scenarioType = EvaluationScenarioType.FixedSeed,
                seed = seed,
                randomSeed = false,
                durationSeconds = 30 * 60, // 30 minutes
                mapLabel = "standard",
                spawnRateMultiplier = 1.0f,
                description = "Reproducible run with fixed seed for comparing model changes"
            };
        }

        /// <summary>
        /// Create a stress scenario with high enemy spawn rate.
        /// </summary>
        public static EvaluationScenario CreateStressScenario()
        {
            return new EvaluationScenario
            {
                scenarioName = "Stress_Test",
                scenarioType = EvaluationScenarioType.Stress,
                seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                randomSeed = true,
                durationSeconds = 20 * 60, // 20 minutes
                mapLabel = "standard",
                spawnRateMultiplier = 2.0f, // 2x spawn rate
                description = "High-load scenario to test generalization and performance under stress"
            };
        }

        /// <summary>
        /// Create a long-run scenario for stability and memory testing.
        /// </summary>
        public static EvaluationScenario CreateLongRunScenario()
        {
            return new EvaluationScenario
            {
                scenarioName = "LongRun_Stability",
                scenarioType = EvaluationScenarioType.LongRun,
                seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                randomSeed = true,
                durationSeconds = 60 * 60, // 60 minutes
                mapLabel = "standard",
                spawnRateMultiplier = 1.0f,
                description = "Extended gameplay session to measure stability, memory leaks, and FPS consistency"
            };
        }

        /// <summary>
        /// Create custom evaluation scenario.
        /// </summary>
        public static EvaluationScenario CreateCustom(string name, int seed, float durationMin, float spawnMult = 1.0f)
        {
            return new EvaluationScenario
            {
                scenarioName = name,
                scenarioType = EvaluationScenarioType.Custom,
                seed = seed,
                randomSeed = false,
                durationSeconds = durationMin * 60,
                mapLabel = "standard",
                spawnRateMultiplier = spawnMult,
                description = $"Custom scenario: {name}"
            };
        }
    }

    public enum EvaluationScenarioType
    {
        FixedSeed = 0,  // Reproducible, fixed seed
        Stress = 1,     // High enemy spawn, stress test
        LongRun = 2,    // Extended session, stability test
        Custom = 3      // User-defined
    }

    /// <summary>
    /// Results from running an evaluation scenario.
    /// </summary>
    [Serializable]
    public class EvaluationResult
    {
        public string scenarioName;
        public DateTime runTime;
        public int seed;
        public float actualDurationSeconds;
        public float survivalSeconds;
        public int kills;
        public float xpGained;
        public float goldGained;
        public float averageReward;
        public float averageFps;
        public float p99FrameTimeMs;
        public float maxMemoryMB;
        public int crashCount;
        public bool completedSuccessfully;
        public string notes;
    }
}
