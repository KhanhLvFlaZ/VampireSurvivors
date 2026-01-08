using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// RL System debugging UI
    /// Displays real-time metrics and provides control interfaces
    /// Requirement: 2.3, 2.5, 3.5 - Configuration UI and debugging
    /// </summary>
    public class RLDebugUI : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Canvas debugCanvas;
        [SerializeField] private bool showByDefault = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F10;

        [Header("Display Settings")]
        [SerializeField] private Font debugFont;
        [SerializeField] private int fontSize = 14;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);

        private RLSystemConfiguration config;
        private TrainingProgressDashboard dashboard;
        private ModelEvaluationSystem evaluationSystem;
        private ParameterAdjustmentManager parameterManager;

        private bool isVisible;
        private Text debugText;
        private float updateInterval = 0.5f;
        private float timeSinceLastUpdate;

        private void Start()
        {
            // Find systems
            config = RLSystemConfiguration.Instance;
            dashboard = FindFirstObjectByType<TrainingProgressDashboard>();
            evaluationSystem = FindFirstObjectByType<ModelEvaluationSystem>();
            parameterManager = FindFirstObjectByType<ParameterAdjustmentManager>();

            // Create UI
            CreateDebugUI();

            isVisible = showByDefault;
            if (debugCanvas != null)
                debugCanvas.enabled = isVisible;
        }

        private void Update()
        {
            // Toggle visibility
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleVisibility();
            }

            // Update display
            if (isVisible)
            {
                timeSinceLastUpdate += Time.deltaTime;
                if (timeSinceLastUpdate >= updateInterval)
                {
                    UpdateDebugDisplay();
                    timeSinceLastUpdate = 0f;
                }
            }
        }

        /// <summary>
        /// Create the debug UI elements
        /// </summary>
        private void CreateDebugUI()
        {
            if (debugCanvas == null)
            {
                // Create canvas if not assigned
                var canvasObj = new GameObject("RL_DebugCanvas");
                debugCanvas = canvasObj.AddComponent<Canvas>();
                debugCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();

                // Create background image
                var bgObj = new GameObject("Background");
                bgObj.transform.SetParent(debugCanvas.transform, false);
                var bgImage = bgObj.AddComponent<Image>();
                bgImage.color = backgroundColor;

                var bgRect = bgObj.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = new Vector2(0.3f, 0.5f);
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
            }

            // Create text display
            var textObj = new GameObject("DebugText");
            textObj.transform.SetParent(debugCanvas.transform, false);

            debugText = textObj.AddComponent<Text>();
            debugText.font = debugFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            debugText.fontSize = fontSize;
            debugText.fontStyle = FontStyle.Bold;
            debugText.color = textColor;
            debugText.alignment = TextAnchor.UpperLeft;

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = new Vector2(0.3f, 0.5f);
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);
        }

        /// <summary>
        /// Update debug display with current information
        /// Requirement: 2.3 - Debugging and monitoring
        /// </summary>
        private void UpdateDebugDisplay()
        {
            if (debugText == null)
                return;

            var display = new System.Text.StringBuilder();

            display.AppendLine("=== RL System Debug ===");
            display.AppendLine($"Frame: {Time.frameCount}");
            display.AppendLine($"FPS: {(1f / Time.deltaTime):F1}");
            display.AppendLine();

            // Configuration info
            if (config != null)
            {
                display.AppendLine("--- Configuration ---");
                display.AppendLine($"Training: {config.EnableTraining}");
                display.AppendLine($"Learning Rate: {config.LearningRate:F4}");
                display.AppendLine($"Exploration: {config.ExplorationRate:F2}");
                display.AppendLine($"Discount: {config.DiscountFactor:F2}");
                display.AppendLine();
            }

            // Training progress
            if (dashboard != null)
            {
                var session = dashboard.GetCurrentSession();
                display.AppendLine("--- Training ---");
                display.AppendLine($"Episodes: {session.totalEpisodes}");
                display.AppendLine($"Avg Reward: {session.averageReward:F2}");
                display.AppendLine($"Last Reward: {session.lastEpisodeReward:F2}");
                display.AppendLine($"Duration: {session.sessionDuration:F1}s");
                display.AppendLine();
            }

            // Active profile
            if (parameterManager != null)
            {
                string activeProfile = parameterManager.GetActiveProfile();
                display.AppendLine("--- Active Profile ---");
                display.AppendLine(activeProfile ?? "None");
                display.AppendLine();
            }

            // Instructions
            display.AppendLine("--- Controls ---");
            display.AppendLine($"Press {toggleKey} to toggle");
            display.AppendLine("Check logs for details");

            debugText.text = display.ToString();
        }

        /// <summary>
        /// Toggle UI visibility
        /// </summary>
        public void ToggleVisibility()
        {
            isVisible = !isVisible;
            if (debugCanvas != null)
                debugCanvas.enabled = isVisible;

            Debug.Log($"RL Debug UI {(isVisible ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Log system status
        /// </summary>
        public void LogSystemStatus()
        {
            var status = new System.Text.StringBuilder();

            status.AppendLine("\n=== RL System Status ===");

            if (config != null)
            {
                status.AppendLine("\nConfiguration:");
                var parameters = config.GetAllParameters();
                foreach (var kvp in parameters)
                {
                    status.AppendLine($"  {kvp.Key}: {kvp.Value:F4}");
                }
            }

            if (dashboard != null)
            {
                var session = dashboard.GetCurrentSession();
                status.AppendLine($"\nTraining Session:");
                status.AppendLine($"  Episodes: {session.totalEpisodes}");
                status.AppendLine($"  Average Reward: {session.averageReward:F2}");
                status.AppendLine($"  Duration: {session.sessionDuration:F1}s");
            }

            if (evaluationSystem != null)
            {
                var models = evaluationSystem.GetLoadedModels();
                status.AppendLine($"\nRegistered Models: {models.Count}");
                foreach (var kvp in models)
                {
                    status.AppendLine($"  - {kvp.Key}");
                }
            }

            Debug.Log(status.ToString());
        }

        /// <summary>
        /// Apply a parameter adjustment profile
        /// </summary>
        public void ApplyProfile(string profileName)
        {
            if (parameterManager != null)
            {
                if (parameterManager.ApplyProfile(profileName))
                {
                    Debug.Log($"Applied profile: {profileName}");
                }
            }
        }

        /// <summary>
        /// Get debug information as string
        /// </summary>
        public string GetDebugInfo()
        {
            if (debugText != null)
                return debugText.text;
            return "";
        }
    }
}
