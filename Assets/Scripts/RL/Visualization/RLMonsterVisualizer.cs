using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Vampire;

namespace Vampire.RL.Visualization
{
    /// <summary>
    /// Visualizes RL monster behavior in real-time
    /// Shows decision indicators, health bars, and tactical information
    /// ONLY displays when monster is actively controlled by RL system
    /// </summary>
    public class RLMonsterVisualizer : MonoBehaviour
    {
        [Header("References")]
        private RLMonsterAgent rlAgent;
        private Monster baseMonster;
        private SpriteRenderer spriteRenderer;
        private Canvas canvas;

        [Header("Visual Elements")]
        [SerializeField] private float decisionIndicatorSize = 0.5f;
        [SerializeField] private float decisionIndicatorDuration = 0.15f;
        [SerializeField] private float healthBarHeight = 0.2f;
        [SerializeField] private float healthBarOffset = 1.2f;

        [Header("Colors")]
        [SerializeField] private Color actionAggressive = Color.red;
        [SerializeField] private Color actionMaintainDistance = Color.yellow;
        [SerializeField] private Color actionRetreat = Color.magenta;
        [SerializeField] private Color actionFlank = Color.cyan;
        [SerializeField] private Color actionWait = Color.green;

        [Header("UI")]
        [SerializeField] private bool showActionLabel = true;
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private bool showConfidence = false;
        [SerializeField] private bool showTacticalInfo = false;
        [SerializeField] private TMP_FontAsset labelFont;

        [Header("Canvas Sorting")]
        [SerializeField] private string canvasSortingLayerName = "UI";
        [SerializeField] private int canvasSortingOrder = 1000;

        [Header("Layout Settings")]
        [SerializeField] private float canvasScale = 1.0f;
        [SerializeField] private float actionLabelFontSize = 2.0f;
        [SerializeField] private float actionLabelOffsetY = 1.5f;
        [SerializeField] private float healthBarWidth = 1.5f;
        [SerializeField] private float labelWidth = 2.0f;
        [SerializeField] private float labelHeight = 0.5f;
        [SerializeField] private bool useConstantPixelSize = false;
        [SerializeField] private float referencePixelsPerUnit = 100f;

        private TextMeshProUGUI actionLabelUI;
        private TextMeshProUGUI confidenceUI;
        private RectTransform healthBarRect;
        private Image healthBarImage;
        private GameObject visualizerContainer;
        private Color originalSpriteColor;
        private bool uiActivationLogged = false;
        [SerializeField] private bool faceCamera = false;
        [SerializeField] private bool addLabelOutline = true;
        [SerializeField] private Color labelOutlineColor = new Color(0f, 0f, 0f, 0.8f);
        [SerializeField] private float labelOutlineWidth = 0.15f;
        [SerializeField] private bool useUILayerForUI = true;
        [SerializeField] private string uiLayerName = "UI";
        [SerializeField] private bool showLabelBackground = false;
        [SerializeField] private bool debugForceLargeLabel = false;

        [Header("Debug")]
        [SerializeField] private bool debugAlwaysShowLabel = false;
        [SerializeField] private bool debugFreezeLabelFade = false;
        [SerializeField] private string debugLabelText = "---";

        // State tracking
        private int lastAction = -1;
        private float actionChangeTime = -1f;
        private float lastConfidence = 0.5f;
        private bool wasControllingLastFrame = false;

        // Action names for display
        private string[] actionNames = new string[]
        {
            "AGGRESSIVE",
            "MAINTAIN",
            "RETREAT",
            "FLANK",
            "WAIT"
        };

        void OnEnable()
        {
            InitializeVisualizer();
        }

        void OnDisable()
        {
            CleanupVisualizer();
        }

        void Start()
        {
            InitializeVisualizer();
        }

        void Update()
        {
            if (!rlAgent)
            {
                rlAgent = GetComponent<RLMonsterAgent>();
                if (!rlAgent) return;
            }

            if (!rlAgent.IsControlling)
            {
                // Hide everything if RL is not controlling
                if (wasControllingLastFrame)
                {
                    HideVisualizer();
                    wasControllingLastFrame = false;
                }
                return;
            }

            wasControllingLastFrame = true;

            // Show visualizer only when RL is controlling
            if (visualizerContainer != null && !visualizerContainer.activeInHierarchy)
            {
                visualizerContainer.SetActive(true);
                EnsureUIElements();
                if (!uiActivationLogged)
                {
                    Debug.Log($"[RLMonsterVisualizer] UI activated for {gameObject.name}. showActionLabel={showActionLabel}, showHealthBar={showHealthBar}");
                    uiActivationLogged = true;
                }
            }

            UpdateVisuals();
        }

        /// <summary>
        /// Initialize visualizer components
        /// </summary>
        private void InitializeVisualizer()
        {
            rlAgent = GetComponent<RLMonsterAgent>();
            baseMonster = GetComponent<Monster>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (!rlAgent)
            {
                Debug.LogWarning($"[RLMonsterVisualizer] RLMonsterAgent not found on {gameObject.name}");
                return;
            }

            if (spriteRenderer)
            {
                originalSpriteColor = spriteRenderer.color;
            }

            // Create visualizer container
            CreateVisualizerUI();
        }

        /// <summary>
        /// Create UI elements for displaying RL information
        /// </summary>
        private void CreateVisualizerUI()
        {
            // Create canvas for this monster
            visualizerContainer = new GameObject("RLVisualizerUI");
            visualizerContainer.transform.SetParent(transform);
            visualizerContainer.transform.localPosition = Vector3.zero;
            visualizerContainer.transform.localScale = Vector3.one;
            // Optionally place UI GameObject on UI layer to match camera culling setup
            if (useUILayerForUI)
            {
                int uiLayer = LayerMask.NameToLayer(uiLayerName);
                if (uiLayer != -1)
                {
                    visualizerContainer.layer = uiLayer;
                }
            }

            // Create world space canvas
            canvas = visualizerContainer.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = false;
            canvas.sortingOrder = 0;
            // Assign sorting layer if available
            if (!string.IsNullOrEmpty(canvasSortingLayerName))
            {
                var id = SortingLayer.NameToID(canvasSortingLayerName);
                if (id != 0) canvas.sortingLayerID = id;
            }
            // Bind to main camera for consistent scale in world space
            canvas.worldCamera = null;

            var canvasScaler = visualizerContainer.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;

            var rectTransform = visualizerContainer.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(2, 2);

            // Action label
            if (showActionLabel)
            {
                actionLabelUI = CreateTextElement("ActionLabel", new Vector3(0, actionLabelOffsetY, 0), actionLabelFontSize, Color.white);
            }

            // Confidence display
            if (showConfidence)
            {
                confidenceUI = CreateTextElement("ConfidenceLabel", new Vector3(0, 0.8f, 0), 1.5f, Color.cyan);
            }

            // Health bar background
            if (showHealthBar)
            {
                CreateHealthBar();
            }

            // Hide by default - will show when RL is controlling
            visualizerContainer.SetActive(false);
        }

        private void EnsureUIElements()
        {
            if (showActionLabel && actionLabelUI == null)
            {
                actionLabelUI = CreateTextElement("ActionLabel", new Vector3(0, actionLabelOffsetY, 0), actionLabelFontSize, Color.white);
            }
            else if (!showActionLabel && actionLabelUI != null)
            {
                Destroy(actionLabelUI.gameObject);
                actionLabelUI = null;
            }

            if (showHealthBar && healthBarRect == null)
            {
                CreateHealthBar();
            }
            else if (!showHealthBar && healthBarRect != null)
            {
                var bg = healthBarRect.transform.parent;
                if (bg) Destroy(bg.gameObject);
                healthBarRect = null;
                healthBarImage = null;
            }
        }

        /// <summary>
        /// Create a text element in the visualizer
        /// </summary>
        private TextMeshProUGUI CreateTextElement(string name, Vector3 localPos, float fontSize, Color color)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(visualizerContainer.transform);
            textObj.transform.localPosition = localPos;
            // Optionally place text on UI layer to match camera culling setup
            if (useUILayerForUI)
            {
                int uiLayer = LayerMask.NameToLayer(uiLayerName);
                if (uiLayer != -1)
                {
                    textObj.layer = uiLayer;
                }
            }

            var rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(labelWidth, labelHeight);

            var textMesh = textObj.AddComponent<TextMeshProUGUI>();
            textMesh.text = "---";
            textMesh.fontSize = fontSize;
            if (labelFont != null)
            {
                textMesh.font = labelFont;
            }
            else if (TMP_Settings.defaultFontAsset != null)
            {
                textMesh.font = TMP_Settings.defaultFontAsset;
            }
            else
            {
                Debug.LogWarning("[RLMonsterVisualizer] No TMP font asset assigned and no defaultFontAsset found. Label may not render.");
            }
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.color = color;
            textMesh.enableAutoSizing = false;
            textMesh.raycastTarget = false;

            // Debug: force a large, visible label regardless of autosize
            if (debugForceLargeLabel)
            {
                textMesh.enableAutoSizing = false;
                textMesh.fontSize = Mathf.Max(24f, fontSize * 2f);
                rectTransform.sizeDelta = new Vector2(Mathf.Max(labelWidth, 1.6f), Mathf.Max(labelHeight, 0.4f));
                var c = textMesh.color;
                c.a = 1f;
                textMesh.color = c;
                Debug.Log($"[RLMonsterVisualizer] Debug large label enabled for {gameObject.name} (fontSize={textMesh.fontSize})");
            }

            if (addLabelOutline)
            {
                var outline = textObj.AddComponent<Outline>();
                outline.effectColor = labelOutlineColor;
                outline.effectDistance = new Vector2(labelOutlineWidth, labelOutlineWidth);
            }

            if (showLabelBackground)
            {
                var bg = new GameObject(name + "_BG");
                bg.transform.SetParent(visualizerContainer.transform);
                bg.transform.localPosition = localPos + new Vector3(0, -0.01f, 0);
                if (useUILayerForUI)
                {
                    int uiLayer = LayerMask.NameToLayer(uiLayerName);
                    if (uiLayer != -1) bg.layer = uiLayer;
                }
                var bgRect = bg.AddComponent<RectTransform>();
                bgRect.sizeDelta = new Vector2(labelWidth, labelHeight);
                var bgImg = bg.AddComponent<Image>();
                bgImg.color = new Color(0f, 0f, 0f, 0.35f);
                // Ensure background renders behind text
                bg.transform.SetSiblingIndex(textObj.transform.GetSiblingIndex());
                textObj.transform.SetSiblingIndex(bg.transform.GetSiblingIndex() + 1);
            }

            var layoutElement = textObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 2f;
            layoutElement.preferredHeight = 0.5f;

            return textMesh;
        }

        /// <summary>
        /// Create health bar display
        /// </summary>
        private void CreateHealthBar()
        {
            // Background
            GameObject bgObj = new GameObject("HealthBarBG");
            bgObj.transform.SetParent(visualizerContainer.transform);
            bgObj.transform.localPosition = new Vector3(0, healthBarOffset, 0);

            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(healthBarWidth, healthBarHeight);

            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Health fill
            GameObject fillObj = new GameObject("HealthBarFill");
            fillObj.transform.SetParent(bgObj.transform);
            fillObj.transform.localPosition = Vector3.zero;

            healthBarRect = fillObj.AddComponent<RectTransform>();
            healthBarRect.sizeDelta = new Vector2(healthBarWidth, healthBarHeight);
            healthBarRect.anchoredPosition = Vector2.zero;

            healthBarImage = fillObj.AddComponent<Image>();
            healthBarImage.color = Color.green;
        }

        /// <summary>
        /// Update all visual indicators
        /// </summary>
        private void UpdateVisuals()
        {
            // Debug: force show label and container if requested
            if (debugAlwaysShowLabel && visualizerContainer != null && !visualizerContainer.activeInHierarchy)
            {
                visualizerContainer.SetActive(true);
                EnsureUIElements();
            }

            // Optional: keep UI facing the camera for readability
            if (faceCamera && visualizerContainer != null && Camera.main != null)
            {
                var cam = Camera.main.transform;
                visualizerContainer.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            }

            // Update health bar
            if (showHealthBar && baseMonster)
            {
                float currentHp = baseMonster.HP;
                float maxHp = baseMonster.MaxHP > 0 ? baseMonster.MaxHP : currentHp;
                if (maxHp <= 0) maxHp = 1f; // avoid division by zero

                float healthRatio = Mathf.Clamp01(currentHp / maxHp);
                if (healthBarRect)
                {
                    healthBarRect.localScale = new Vector3(healthRatio, 1f, 1f);
                    healthBarRect.anchoredPosition = new Vector3(-(healthBarWidth * (1 - healthRatio)) / 2, 0, 0);
                }

                // Color based on health
                if (healthBarImage)
                {
                    if (healthRatio > 0.5f)
                        healthBarImage.color = Color.green;
                    else if (healthRatio > 0.25f)
                        healthBarImage.color = Color.yellow;
                    else
                        healthBarImage.color = Color.red;
                }
            }

            // Update action label and indicators
            if (showActionLabel && actionLabelUI)
            {
                UpdateActionDisplay();
            }

            // Update confidence
            if (showConfidence && confidenceUI)
            {
                confidenceUI.text = $"Confidence: {lastConfidence:F0}%";
            }
        }

        /// <summary>
        /// Update action display based on latest decision
        /// </summary>
        private void UpdateActionDisplay()
        {
            // Get current action from agent
            int currentAction = GetCurrentAction();
            if (currentAction != lastAction)
            {
                lastAction = currentAction;
                actionChangeTime = Time.time;
            }

            // Update label
            string actionText = (currentAction >= 0 && currentAction < actionNames.Length)
                ? actionNames[currentAction]
                : "UNKNOWN";

            actionLabelUI.text = actionText;

            // Color based on action type
            Color actionColor = GetActionColor(currentAction);
            actionLabelUI.color = actionColor;
            // Force full alpha while debugging always show
            if (debugAlwaysShowLabel)
            {
                var c = actionLabelUI.color;
                c.a = 1f;
                actionLabelUI.color = c;
            }

            // Add animation: fade out over time (optional freeze for debugging)
            if (!debugFreezeLabelFade)
            {
                float timeSinceChange = Time.time - actionChangeTime;
                if (timeSinceChange > decisionIndicatorDuration)
                {
                    float alpha = Mathf.Max(0.3f, 1f - (timeSinceChange / (decisionIndicatorDuration * 2)));
                    Color c = actionColor;
                    c.a = alpha;
                    actionLabelUI.color = c;
                }
            }
        }

        /// <summary>
        /// Get current action from agent
        /// </summary>
        private int GetCurrentAction()
        {
            if (!rlAgent) return -1;
            return rlAgent.CurrentAction;
        }

        /// <summary>
        /// Get color for action type
        /// </summary>
        private Color GetActionColor(int action)
        {
            return action switch
            {
                0 => actionAggressive,
                1 => actionMaintainDistance,
                2 => actionRetreat,
                3 => actionFlank,
                4 => actionWait,
                _ => Color.white
            };
        }

        /// <summary>
        /// Hide all visualizer elements
        /// </summary>
        private void HideVisualizer()
        {
            if (visualizerContainer)
            {
                visualizerContainer.SetActive(false);
            }

            // Restore original sprite color
            if (spriteRenderer)
            {
                spriteRenderer.color = originalSpriteColor;
            }
        }

        /// <summary>
        /// Cleanup visualizer when disabled
        /// </summary>
        private void CleanupVisualizer()
        {
            if (visualizerContainer)
            {
                Destroy(visualizerContainer);
            }
        }

        /// <summary>
        /// Draw debug gizmos showing RL state
        /// </summary>
        void OnDrawGizmosSelected()
        {
            var agent = GetComponent<RLMonsterAgent>();
            if (!agent) return;

            // Only show gizmos when agent is active and controlled by RL
            if (!agent.IsControlling) return;

            // Draw detection range (blue circle)
            Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
            DrawCircle(transform.position, 15f, 20);

            // Draw optimal engagement range (green circle)
            Gizmos.color = new Color(0, 1f, 0, 0.3f);
            DrawCircle(transform.position, 4f, 16);

            // Draw danger/retreat range (red circle)
            Gizmos.color = new Color(1f, 0, 0, 0.2f);
            DrawCircle(transform.position, 2f, 12);
        }

        /// <summary>
        /// Draw a circle using Gizmos
        /// </summary>
        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 lastPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                Gizmos.DrawLine(lastPoint, nextPoint);
                lastPoint = nextPoint;
            }
        }

        /// <summary>
        /// Get tactical state for debugging
        /// </summary>
        public string GetTacticalState()
        {
            if (!rlAgent || !baseMonster) return "N/A";

            int action = rlAgent.CurrentAction;
            string actionName = (action >= 0 && action < actionNames.Length) ? actionNames[action] : "UNKNOWN";
            float hpRatio = Mathf.Clamp01(baseMonster.HP / 50f); // Assuming 50 HP max as default

            return $"{actionName} | HP: {hpRatio:P0}";
        }
    }
}
