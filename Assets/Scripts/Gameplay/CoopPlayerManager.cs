using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Manages local co-op players via the Input System PlayerInputManager.
    /// Handles join/leave, spawn positioning, camera assignment, UI binding, and keeps a registry for downstream systems.
    /// Requirement: Co-op input/player lifecycle management
    /// </summary>
    public class CoopPlayerManager : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform[] spawnPoints;

        [Header("Camera & UI")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Canvas playerUIPrefab;

        private readonly List<PlayerInput> activePlayers = new List<PlayerInput>();
        private readonly Dictionary<PlayerInput, PlayerContext> playerContexts = new Dictionary<PlayerInput, PlayerContext>();

        public IReadOnlyList<PlayerInput> ActivePlayers => activePlayers;

        private void Awake()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            // Auto-spawn two local players using split keyboard controls
            SpawnLocalPlayers();
        }

        private void OnDestroy()
        {
        }

        private void SpawnLocalPlayers()
        {
            if (playerPrefab == null)
            {
                Debug.LogError("Player prefab is not assigned for local co-op");
                return;
            }

            SpawnPlayer(0, new KeySet(KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space));
            SpawnPlayer(1, new KeySet(KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.RightControl));
        }

        private void SpawnPlayer(int playerIndex, KeySet keySet)
        {
            GameObject instance = Instantiate(playerPrefab);
            instance.name = $"Player_{playerIndex + 1}";

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var spawn = spawnPoints[playerIndex % spawnPoints.Length];
                instance.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            }

            var playerInput = instance.GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                playerInput = instance.AddComponent<PlayerInput>();
            }

            var coopInput = instance.GetComponent<CoopPlayerInput>();
            if (coopInput != null)
            {
                coopInput.ConfigureSplitKeyboard(keySet.up, keySet.down, keySet.left, keySet.right, keySet.attack);
            }
            HandlePlayerJoined(playerInput, playerIndex);
        }

        private void HandlePlayerJoined(PlayerInput playerInput, int playerIndex)
        {
            activePlayers.Add(playerInput);

            // Validate prefab has required components
            Character character = playerInput.GetComponent<Character>();
            if (character == null)
            {
                Debug.LogError($"PlayerInput prefab missing Character component: {playerInput.name}");
                return;
            }

            // Reposition to spawn point
            // Setup player context (camera, UI)
            PlayerContext context = new PlayerContext
            {
                playerInput = playerInput,
                character = character,
                playerId = playerIndex
            };

            // Assign shared camera follow
            SetupSharedCamera(context);

            // Setup UI
            SetupPlayerUI(context);

            playerContexts[playerInput] = context;

            Debug.Log($"✓ Player {context.playerId} joined ({activePlayers.Count} players total)");
            Debug.Log($"  Camera: {context.camera?.name ?? "Main"}, UI: {context.uiCanvas?.name ?? "None"}");
        }

        private void SetupSharedCamera(PlayerContext context)
        {
            context.camera = mainCamera;

            if (mainCamera == null)
                return;

            var controller = mainCamera.GetComponent<PlayerCameraController>();
            if (controller != null)
            {
                controller.RegisterTarget(context.character.transform);
            }
        }

        /// <summary>
        /// Setup UI canvas for player
        /// </summary>
        private void SetupPlayerUI(PlayerContext context)
        {
            Canvas uiCanvas;

            if (playerUIPrefab != null)
            {
                uiCanvas = Instantiate(playerUIPrefab);
                uiCanvas.name = $"PlayerUI_{context.playerId}";
            }
            else
            {
                GameObject canvasGO = new GameObject($"PlayerUI_{context.playerId}");
                uiCanvas = canvasGO.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 100 + context.playerId;
            context.uiCanvas = uiCanvas;

            var rectTransform = uiCanvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = context.playerId == 0 ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                rectTransform.anchorMax = context.playerId == 0 ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                rectTransform.pivot = context.playerId == 0 ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                rectTransform.anchoredPosition = context.playerId == 0 ? new Vector2(20f, -20f) : new Vector2(-20f, -20f);
                rectTransform.sizeDelta = new Vector2(400f, 120f);
            }

            Debug.Log($"  UI Canvas created: {uiCanvas.name}");
        }

        /// <summary>
        /// Get context for a player
        /// </summary>
        public PlayerContext GetPlayerContext(PlayerInput playerInput)
        {
            return playerContexts.TryGetValue(playerInput, out var context) ? context : null;
        }

        /// <summary>
        /// Get all active player contexts
        /// </summary>
        public IEnumerable<PlayerContext> GetAllPlayerContexts()
        {
            return playerContexts.Values;
        }

        public int GetPlayerCount()
        {
            return activePlayers.Count;
        }
    }

    /// <summary>
    /// Per-player context containing camera, UI, and character references
    /// </summary>
    public class PlayerContext
    {
        public PlayerInput playerInput;
        public Character character;
        public Camera camera;
        public Canvas uiCanvas;
        public int playerId;
    }

    internal readonly struct KeySet
    {
        public readonly KeyCode up;
        public readonly KeyCode down;
        public readonly KeyCode left;
        public readonly KeyCode right;
        public readonly KeyCode attack;

        public KeySet(KeyCode up, KeyCode down, KeyCode left, KeyCode right, KeyCode attack)
        {
            this.up = up;
            this.down = down;
            this.left = left;
            this.right = right;
            this.attack = attack;
        }
    }
}
