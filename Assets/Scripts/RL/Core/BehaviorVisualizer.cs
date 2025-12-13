using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Visualizes RL agent behavior with UI indicators
    /// Shows decision confidence, action types, coordination, and adaptation
    /// Requirements: 3.1, 3.2, 3.3, 3.4, 3.5
    /// Implements IBehaviorVisualizer interface (duck typing due to assembly issues)
    /// </summary> 
    public class BehaviorVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private bool enableVisualization = true;
        [SerializeField] private bool showDecisionIndicators = true;
        [SerializeField] private bool showCoordinationLines = true;
        [SerializeField] private bool showAdaptationEffects = true;
        [SerializeField] private bool showDebugInfo = false;

        [Header("UI Prefabs")]
        [SerializeField] private GameObject decisionIndicatorPrefab;
        [SerializeField] private GameObject coordinationLinePrefab;
        [SerializeField] private GameObject adaptationEffectPrefab;
        [SerializeField] private GameObject debugInfoPanelPrefab;

        [Header("Visual Settings")]
        [SerializeField] private float indicatorDuration = 2f;
        [SerializeField] private float indicatorHeight = 1.5f;
        [SerializeField] private Color highConfidenceColor = Color.green;
        [SerializeField] private Color mediumConfidenceColor = Color.yellow;
        [SerializeField] private Color lowConfidenceColor = Color.red;

        [Header("Coordination Settings")]
        [SerializeField] private Color coordinationLineColor = new Color(0, 1, 1, 0.5f);
        [SerializeField] private float coordinationLineDuration = 1.5f;
        [SerializeField] private float coordinationLineWidth = 0.1f;

        // Active visualizations
        private Dictionary<Monster, DecisionIndicator> activeDecisionIndicators = new Dictionary<Monster, DecisionIndicator>();
        private List<CoordinationVisualization> activeCoordinationVisuals = new List<CoordinationVisualization>();
        private Dictionary<Monster, GameObject> debugInfoPanels = new Dictionary<Monster, GameObject>();

        // Pools for performance
        private Queue<GameObject> indicatorPool = new Queue<GameObject>();
        private Queue<LineRenderer> lineRendererPool = new Queue<LineRenderer>();

        private Canvas worldCanvas;

        private void Awake()
        {
            InitializeCanvas();
            InitializePools();
        }

        private void InitializeCanvas()
        {
            // Create or find world space canvas
            worldCanvas = GetComponent<Canvas>();
            if (worldCanvas == null)
            {
                worldCanvas = gameObject.AddComponent<Canvas>();
                worldCanvas.renderMode = RenderMode.WorldSpace;
            }
        }

        private void InitializePools()
        {
            // Pre-instantiate some indicators for pooling
            for (int i = 0; i < 10; i++)
            {
                if (decisionIndicatorPrefab != null)
                {
                    var indicator = Instantiate(decisionIndicatorPrefab, transform);
                    indicator.SetActive(false);
                    indicatorPool.Enqueue(indicator);
                }
            }
        }

        /// <summary>
        /// Show decision indicator for a monster's RL decision
        /// Requirement: 3.1
        /// </summary>
        public void ShowDecisionIndicator(Monster monster, int action, float confidence)
        {
            if (!enableVisualization || !showDecisionIndicators || monster == null)
                return;

            // Remove existing indicator if present
            if (activeDecisionIndicators.ContainsKey(monster))
            {
                RemoveDecisionIndicator(monster);
            }

            // Create or get indicator from pool
            GameObject indicatorObj = GetIndicatorFromPool();
            Vector3 position = monster.transform.position + Vector3.up * indicatorHeight;
            indicatorObj.transform.position = position;
            indicatorObj.SetActive(true);

            // Configure indicator
            var indicator = indicatorObj.GetComponent<DecisionIndicator>();
            if (indicator == null)
            {
                indicator = indicatorObj.AddComponent<DecisionIndicator>();
            }

            indicator.Initialize(action, confidence, GetConfidenceColor(confidence), indicatorDuration);
            activeDecisionIndicators[monster] = indicator;

            // Auto-remove after duration
            StartCoroutine(RemoveIndicatorAfterDelay(monster, indicatorDuration));
        }

        /// <summary>
        /// Show coordination indicator for team behaviors
        /// Requirement: 3.2
        /// </summary>
        public void ShowCoordinationIndicator(List<Monster> monsters)
        {
            if (!enableVisualization || !showCoordinationLines || monsters == null || monsters.Count < 2)
                return;

            var visualization = new CoordinationVisualization
            {
                monsters = new List<Monster>(monsters),
                startTime = Time.time,
                duration = coordinationLineDuration
            };

            // Create line renderers between coordinating monsters
            for (int i = 0; i < monsters.Count - 1; i++)
            {
                for (int j = i + 1; j < monsters.Count; j++)
                {
                    if (monsters[i] != null && monsters[j] != null)
                    {
                        var lineRenderer = CreateCoordinationLine(monsters[i].transform.position, monsters[j].transform.position);
                        visualization.lineRenderers.Add(lineRenderer);
                    }
                }
            }

            activeCoordinationVisuals.Add(visualization);
        }

        /// <summary>
        /// Show adaptation indicator when monster adapts strategy
        /// Requirement: 3.3
        /// </summary>
        public void ShowAdaptationIndicator(Monster monster, string adaptationType)
        {
            if (!enableVisualization || !showAdaptationEffects || monster == null)
                return;

            GameObject effectObj;

            if (adaptationEffectPrefab != null)
            {
                effectObj = Instantiate(adaptationEffectPrefab, monster.transform.position, Quaternion.identity);
            }
            else
            {
                // Create simple particle effect if no prefab
                effectObj = new GameObject("AdaptationEffect");
                effectObj.transform.position = monster.transform.position;

                var particles = effectObj.AddComponent<ParticleSystem>();
                var main = particles.main;
                main.duration = 1.5f;
                main.startLifetime = 1f;
                main.startSpeed = 2f;
                main.startColor = Color.cyan;
                main.maxParticles = 50;

                particles.Play();
            }

            // Add text label for adaptation type
            var textObj = new GameObject("AdaptationLabel");
            textObj.transform.SetParent(effectObj.transform);
            textObj.transform.localPosition = Vector3.up * 2f;

            var textMesh = textObj.AddComponent<TextMeshPro>();
            textMesh.text = $"Adapting: {adaptationType}";
            textMesh.fontSize = 3;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.color = Color.cyan;

            // Auto-destroy after duration
            Destroy(effectObj, 2f);
        }

        /// <summary>
        /// Show debug information for RL state and actions
        /// Requirement: 3.4, 3.5
        /// </summary>
        public void ShowDebugInfo(Monster monster, float[] state, int action)
        {
            if (!enableVisualization || !showDebugInfo || monster == null)
                return;

            // Create or update debug panel for this monster
            if (!debugInfoPanels.ContainsKey(monster))
            {
                CreateDebugPanel(monster);
            }

            UpdateDebugPanel(monster, state, action);
        }

        private void Update()
        {
            // Update coordination visualizations
            for (int i = activeCoordinationVisuals.Count - 1; i >= 0; i--)
            {
                var viz = activeCoordinationVisuals[i];

                if (Time.time - viz.startTime > viz.duration)
                {
                    // Remove expired visualization
                    foreach (var line in viz.lineRenderers)
                    {
                        if (line != null)
                        {
                            ReturnLineToPool(line);
                        }
                    }
                    activeCoordinationVisuals.RemoveAt(i);
                }
                else
                {
                    // Update line positions
                    UpdateCoordinationLines(viz);
                }
            }

            // Update debug panels to follow monsters
            foreach (var kvp in debugInfoPanels)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Value.transform.position = kvp.Key.transform.position + Vector3.up * 2f;
                }
            }
        }

        private GameObject GetIndicatorFromPool()
        {
            if (indicatorPool.Count > 0)
            {
                return indicatorPool.Dequeue();
            }

            // Create new indicator if pool is empty
            if (decisionIndicatorPrefab != null)
            {
                return Instantiate(decisionIndicatorPrefab, transform);
            }

            // Create basic indicator
            var obj = new GameObject("DecisionIndicator");
            obj.transform.SetParent(transform);
            return obj;
        }

        private void ReturnIndicatorToPool(GameObject indicator)
        {
            if (indicator != null)
            {
                indicator.SetActive(false);
                indicatorPool.Enqueue(indicator);
            }
        }

        private LineRenderer CreateCoordinationLine(Vector3 start, Vector3 end)
        {
            LineRenderer line;

            if (lineRendererPool.Count > 0)
            {
                line = lineRendererPool.Dequeue();
            }
            else
            {
                var lineObj = new GameObject("CoordinationLine");
                lineObj.transform.SetParent(transform);
                line = lineObj.AddComponent<LineRenderer>();

                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startWidth = coordinationLineWidth;
                line.endWidth = coordinationLineWidth;
            }

            line.gameObject.SetActive(true);
            line.startColor = coordinationLineColor;
            line.endColor = coordinationLineColor;
            line.SetPosition(0, start);
            line.SetPosition(1, end);

            return line;
        }

        private void ReturnLineToPool(LineRenderer line)
        {
            if (line != null)
            {
                line.gameObject.SetActive(false);
                lineRendererPool.Enqueue(line);
            }
        }

        private void UpdateCoordinationLines(CoordinationVisualization viz)
        {
            int lineIndex = 0;
            for (int i = 0; i < viz.monsters.Count - 1; i++)
            {
                for (int j = i + 1; j < viz.monsters.Count; j++)
                {
                    if (viz.monsters[i] != null && viz.monsters[j] != null && lineIndex < viz.lineRenderers.Count)
                    {
                        var line = viz.lineRenderers[lineIndex];
                        if (line != null)
                        {
                            line.SetPosition(0, viz.monsters[i].transform.position);
                            line.SetPosition(1, viz.monsters[j].transform.position);
                        }
                    }
                    lineIndex++;
                }
            }
        }

        private Color GetConfidenceColor(float confidence)
        {
            if (confidence >= 0.7f)
                return highConfidenceColor;
            else if (confidence >= 0.4f)
                return mediumConfidenceColor;
            else
                return lowConfidenceColor;
        }

        private void RemoveDecisionIndicator(Monster monster)
        {
            if (activeDecisionIndicators.TryGetValue(monster, out var indicator))
            {
                if (indicator != null && indicator.gameObject != null)
                {
                    ReturnIndicatorToPool(indicator.gameObject);
                }
                activeDecisionIndicators.Remove(monster);
            }
        }

        private System.Collections.IEnumerator RemoveIndicatorAfterDelay(Monster monster, float delay)
        {
            yield return new WaitForSeconds(delay);
            RemoveDecisionIndicator(monster);
        }

        private void CreateDebugPanel(Monster monster)
        {
            GameObject panelObj;

            if (debugInfoPanelPrefab != null)
            {
                panelObj = Instantiate(debugInfoPanelPrefab);
            }
            else
            {
                // Create basic debug panel
                panelObj = new GameObject("DebugPanel");

                var canvas = panelObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.transform.localScale = Vector3.one * 0.01f;

                var background = new GameObject("Background");
                background.transform.SetParent(panelObj.transform);
                var image = background.AddComponent<Image>();
                image.color = new Color(0, 0, 0, 0.8f);

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(background.transform);
                var text = textObj.AddComponent<TextMeshProUGUI>();
                text.fontSize = 12;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.TopLeft;
            }

            panelObj.transform.position = monster.transform.position + Vector3.up * 2f;
            debugInfoPanels[monster] = panelObj;
        }

        private void UpdateDebugPanel(Monster monster, float[] state, int action)
        {
            if (!debugInfoPanels.TryGetValue(monster, out var panel))
                return;

            var textUI = panel.GetComponentInChildren<TextMeshProUGUI>();
            var textMesh = panel.GetComponentInChildren<TextMeshPro>();

            TMP_Text text = textUI != null ? (TMP_Text)textUI : (TMP_Text)textMesh;

            if (text != null)
            {
                var statePreview = state != null && state.Length > 0
                    ? $"[{state[0]:F2}, {state[1]:F2}, ...]"
                    : "[]";

                text.text = $"Monster: {monster.name}\n" +
                           $"Action: {action}\n" +
                           $"State: {statePreview}\n" +
                           $"State Dim: {state?.Length ?? 0}";
            }
        }

        public void ToggleVisualization(bool enabled)
        {
            enableVisualization = enabled;
        }

        public void ToggleDebugInfo(bool enabled)
        {
            showDebugInfo = enabled;

            if (!enabled)
            {
                // Clear all debug panels
                foreach (var panel in debugInfoPanels.Values)
                {
                    if (panel != null)
                    {
                        Destroy(panel);
                    }
                }
                debugInfoPanels.Clear();
            }
        }

        private void OnDestroy()
        {
            // Cleanup
            foreach (var indicator in activeDecisionIndicators.Values)
            {
                if (indicator != null && indicator.gameObject != null)
                {
                    Destroy(indicator.gameObject);
                }
            }

            foreach (var viz in activeCoordinationVisuals)
            {
                foreach (var line in viz.lineRenderers)
                {
                    if (line != null)
                    {
                        Destroy(line.gameObject);
                    }
                }
            }

            foreach (var panel in debugInfoPanels.Values)
            {
                if (panel != null)
                {
                    Destroy(panel);
                }
            }
        }
    }

    /// <summary>
    /// Component for decision indicator visualization
    /// </summary>
    public class DecisionIndicator : MonoBehaviour
    {
        private int action;
        private float confidence;
        private Color color;
        private float duration;
        private float startTime;

        private TextMeshPro actionText;
        private SpriteRenderer confidenceBar;

        public void Initialize(int action, float confidence, Color color, float duration)
        {
            this.action = action;
            this.confidence = confidence;
            this.color = color;
            this.duration = duration;
            this.startTime = Time.time;

            SetupVisuals();
        }

        private void SetupVisuals()
        {
            // Create action label
            if (actionText == null)
            {
                var textObj = new GameObject("ActionText");
                textObj.transform.SetParent(transform);
                textObj.transform.localPosition = Vector3.zero;
                actionText = textObj.AddComponent<TextMeshPro>();
            }

            actionText.text = GetActionName(action);
            actionText.fontSize = 2;
            actionText.color = color;
            actionText.alignment = TextAlignmentOptions.Center;

            // Create confidence bar
            if (confidenceBar == null)
            {
                var barObj = new GameObject("ConfidenceBar");
                barObj.transform.SetParent(transform);
                barObj.transform.localPosition = Vector3.down * 0.3f;
                confidenceBar = barObj.AddComponent<SpriteRenderer>();
                confidenceBar.sprite = CreateBarSprite();
            }

            confidenceBar.color = color;
            confidenceBar.transform.localScale = new Vector3(confidence, 0.1f, 1f);
        }

        private string GetActionName(int action)
        {
            // Map action index to readable name
            string[] actionNames = { "Move", "Attack", "Retreat", "Flank", "Coordinate" };
            return action >= 0 && action < actionNames.Length ? actionNames[action] : $"Action {action}";
        }

        private Sprite CreateBarSprite()
        {
            // Create simple white square sprite
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            // Fade out over time
            float lifetime = Time.time - startTime;
            float alpha = 1f - (lifetime / duration);

            if (actionText != null)
            {
                var textColor = actionText.color;
                textColor.a = alpha;
                actionText.color = textColor;
            }

            if (confidenceBar != null)
            {
                var barColor = confidenceBar.color;
                barColor.a = alpha;
                confidenceBar.color = barColor;
            }
        }
    }

    /// <summary>
    /// Data structure for coordination visualization
    /// </summary>
    public class CoordinationVisualization
    {
        public List<Monster> monsters;
        public List<LineRenderer> lineRenderers = new List<LineRenderer>();
        public float startTime;
        public float duration;
    }
}
