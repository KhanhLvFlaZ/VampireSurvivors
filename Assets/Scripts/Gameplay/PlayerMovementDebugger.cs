using UnityEngine;
using UnityEngine.InputSystem;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Debug tool to diagnose player movement issues
    /// </summary>
    public class PlayerMovementDebugger : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool enableLogging = true;
        [SerializeField] private float logInterval = 1f;

        private float lastLogTime;

        private void Start()
        {
            Debug.Log("=== Player Movement Debugger Started ===");
            DiagnoseAllPlayers();
        }

        private void Update()
        {
            if (enableLogging && Time.time - lastLogTime > logInterval)
            {
                lastLogTime = Time.time;
                LogPlayerStates();
            }
        }

        [ContextMenu("Diagnose All Players")]
        public void DiagnoseAllPlayers()
        {
            Debug.Log("\n=== PLAYER MOVEMENT DIAGNOSIS ===\n");

            // Try to find Characters with includeInactive
            Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
            Debug.Log($"Found {characters.Length} Character(s) (including inactive)");

            if (characters.Length == 0)
            {
                Debug.LogWarning("⚠️ No Character components found in scene!");
                Debug.Log("Searching for GameObjects with 'Character' or player names...");

                // Try to find by name
                GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var go in allObjects)
                {
                    if (go.name.Contains("角色") || go.name.Contains("Player") || go.name.Contains("Character"))
                    {
                        Debug.Log($"Found GameObject: {go.name} (active: {go.activeInHierarchy})");
                        ListComponents(go);
                    }
                }
                return;
            }

            for (int i = 0; i < characters.Length; i++)
            {
                DiagnosePlayer(characters[i], i);
            }
        }

        private void ListComponents(GameObject go)
        {
            var components = go.GetComponents<Component>();
            Debug.Log($"  Components on {go.name}:");
            foreach (var comp in components)
            {
                if (comp != null)
                    Debug.Log($"    - {comp.GetType().Name}");
            }
        }

        private void DiagnosePlayer(Character character, int index)
        {
            GameObject go = character.gameObject;
            Debug.Log($"\n--- Player {index}: {go.name} ---");

            // Check components
            var rb = go.GetComponent<Rigidbody2D>();
            var playerInput = go.GetComponent<PlayerInput>();
            var keyboardController = go.GetComponent<PlayerKeyboardController>();

            Debug.Log($"Rigidbody2D: {(rb != null ? "✓" : "✗")}");
            if (rb != null)
            {
                Debug.Log($"  Mass: {rb.mass}, LinearDamping: {rb.linearDamping:F2}");
                Debug.Log($"  GravityScale: {rb.gravityScale}, Constraints: {rb.constraints}");
                Debug.Log($"  Current Velocity: {rb.linearVelocity} (magnitude: {rb.linearVelocity.magnitude:F2})");
            }

            Debug.Log($"PlayerInput: {(playerInput != null ? (playerInput.enabled ? "✓ ENABLED" : "✗ DISABLED") : "✗")}");
            if (playerInput != null && playerInput.enabled)
            {
                Debug.LogWarning($"  ⚠️ PlayerInput is ENABLED on {go.name} - this may conflict with PlayerKeyboardController!");
            }

            Debug.Log($"PlayerKeyboardController: {(keyboardController != null ? (keyboardController.enabled ? "✓ ENABLED" : "✗ DISABLED") : "✗")}");

            // Check CharacterBlueprint
            if (character.Blueprint != null)
            {
                Debug.Log($"CharacterBlueprint: {character.Blueprint.name}");
                Debug.Log($"  Movespeed: {character.Blueprint.movespeed:F2}");
                Debug.Log($"  Acceleration: {character.Blueprint.acceleration:F2}");
            }
            else
            {
                Debug.LogError($"  ✗ No CharacterBlueprint assigned!");
            }

            // Check for other movement scripts
            var allComponents = go.GetComponents<MonoBehaviour>();
            Debug.Log($"Total MonoBehaviour components: {allComponents.Length}");
            foreach (var comp in allComponents)
            {
                if (comp == null) continue;
                var typeName = comp.GetType().Name;
                if (typeName.Contains("Input") || typeName.Contains("Move") || typeName.Contains("Control"))
                {
                    Debug.Log($"  - {typeName} ({(comp.enabled ? "enabled" : "disabled")})");
                }
            }
        }

        private void LogPlayerStates()
        {
            Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);

            Debug.Log("\n=== Player States ===");
            foreach (var c in characters)
            {
                var rb = c.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    Debug.Log($"{c.gameObject.name}: " +
                             $"Pos={c.transform.position}, " +
                             $"Vel={rb.linearVelocity} (mag={rb.linearVelocity.magnitude:F2}), " +
                             $"Damping={rb.linearDamping:F2}");
                }
            }
        }
    }
}
