using UnityEngine;
using System.Collections;
using Vampire.RL;

namespace Vampire.RL.Examples
{
    /// <summary>
    /// Demo script showing RL system functionality
    /// Demonstrates RL system initialization and basic operations
    /// </summary>
    public class RLSystemDemo : MonoBehaviour
    {
        [Header("Demo Settings")]
        [SerializeField] private bool runDemoOnStart = true;
        [SerializeField] private float demoStepDelay = 2f;
        [SerializeField] private bool showDebugInfo = true;

        private RLSystem demoRLSystem;
        private MonoBehaviour demoPlayer;

        private void Log(string message)
        {
            if (showDebugInfo)
            {
                Debug.Log(message);
            }
        }

        void Start()
        {
            if (runDemoOnStart)
            {
                StartCoroutine(RunDemo());
            }
        }

        IEnumerator RunDemo()
        {
            Log("=== RL System Demo Started ===");

            // Step 1: Setup demo environment
            yield return StartCoroutine(DemoStep1_Setup());
            yield return new WaitForSeconds(demoStepDelay);

            // Step 2: Initialize RL system
            yield return StartCoroutine(DemoStep2_InitializeRLSystem());
            yield return new WaitForSeconds(demoStepDelay);

            // Step 3: Demonstrate training modes
            yield return StartCoroutine(DemoStep3_TrainingModes());
            yield return new WaitForSeconds(demoStepDelay);

            // Step 4: Show performance monitoring
            yield return StartCoroutine(DemoStep4_PerformanceMonitoring());
            yield return new WaitForSeconds(demoStepDelay);

            // Step 5: Demonstrate optimization

            Log("=== RL System Demo Completed ===");
        }

        IEnumerator DemoStep1_Setup()
        {
            Log("Step 1: Setting up demo environment");

            // Create RLSystem
            var rlSystemGO = new GameObject("DemoRLSystem");
            demoRLSystem = rlSystemGO.AddComponent<RLSystem>();

            // Create player
            var playerGO = new GameObject("DemoPlayer");
            demoPlayer = playerGO.AddComponent<MockPlayer>();

            Log("Demo environment created successfully");
            yield return null;
        }

        IEnumerator DemoStep2_InitializeRLSystem()
        {
            Log("Step 2: Initializing RL system");

            // Initialize RLSystem
            demoRLSystem.Initialize(demoPlayer, "demo_profile");

            // Verify initialization
            Log($"RL System Enabled: {demoRLSystem.IsEnabled}");
            Log($"Current Training Mode: {demoRLSystem.CurrentTrainingMode}");

            yield return null;
        }

        IEnumerator DemoStep3_TrainingModes()
        {
            Log("Step 3: Demonstrating training modes");

            // Test different training modes
            Log("Setting training mode to Training");
            demoRLSystem.SetTrainingMode(TrainingMode.Training);
            yield return new WaitForSeconds(0.5f);

            Log("Setting training mode to Inference");
            demoRLSystem.SetTrainingMode(TrainingMode.Inference);
            yield return new WaitForSeconds(0.5f);

            Log("Setting training mode to Mixed");
            demoRLSystem.SetTrainingMode(TrainingMode.Mixed);

            // Show current state
            Log($"Current training mode: {demoRLSystem.CurrentTrainingMode}");

            yield return null;
        }

        IEnumerator DemoStep4_PerformanceMonitoring()
        {
            Log("Step 4: Demonstrating performance monitoring");

            // Show system performance
            Log($"RL System enabled: {demoRLSystem.IsEnabled}");
            Log($"Active agent count: {demoRLSystem.ActiveAgentCount}");
            Log($"Current frame time: {demoRLSystem.CurrentFrameTime:F2}ms");

            // Show performance constraints
            bool meetsConstraints = demoRLSystem.MeetsPerformanceConstraints();
            Log($"Meets performance constraints: {meetsConstraints}");

            // Show optimization status
            string optimizationStatus = demoRLSystem.GetOptimizationStatus();
            Log($"Optimization status: {optimizationStatus}");

            yield return null;
        }

        IEnumerator DemoStep5_Optimization()
        {
            Log("Step 5: Demonstrating performance optimization");

            // Show learning metrics
            var allMetrics = demoRLSystem.GetAllMetrics();
            Log($"Learning metrics available for {allMetrics.Count} monster types");

            // Trigger optimization
            Log("Triggering performance optimization...");
            demoRLSystem.OptimizePerformance();

            // Show performance report
            var report = demoRLSystem.GetPerformanceReport();
            if (report != null)
            {
                Log($"Performance report - Strategy: {report.optimizationStrategy}, Emergency: {report.emergencyModeActive}");
            }

            yield return null;
        }

        void OnDestroy()
        {
            // Cleanup demo objects
            if (demoRLSystem != null) DestroyImmediate(demoRLSystem.gameObject);
            if (demoPlayer != null) DestroyImmediate(demoPlayer.gameObject);
        }

        // Mock player for demo
        public class MockPlayer : MonoBehaviour
        {
            public Vector2 Velocity => Vector2.zero;

            void Awake()
            {
                // Add required components for player
                if (GetComponent<Rigidbody2D>() == null)
                    gameObject.AddComponent<Rigidbody2D>();
            }
        }
    }
}