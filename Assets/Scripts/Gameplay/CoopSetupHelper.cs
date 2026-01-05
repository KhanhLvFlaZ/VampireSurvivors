using UnityEngine;
using UnityEngine.InputSystem;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Setup helper for co-op player management
    /// Provides context menu options to configure players
    /// </summary>
    public class CoopSetupHelper : MonoBehaviour
    {
        [ContextMenu("Setup Co-op Players (2 Players Split-Screen)")]
        public void SetupTwoPlayerSplitScreen()
        {
            var manager = GetComponent<CoopPlayerManager>();
            if (manager == null)
            {
                Debug.LogError("CoopPlayerManager not found");
                return;
            }

            Debug.Log("\n========== CO-OP SETUP GUIDE ==========\n");

            Debug.Log("[1] Player Prefab Setup:");
            Debug.Log("  • Ensure CoopPlayer prefab has:");
            Debug.Log("    ✓ Character component");
            Debug.Log("    ✓ PlayerInput component");
            Debug.Log("    ✓ CoopPlayerInput component");
            Debug.Log("    ✓ Rigidbody2D");
            Debug.Log("    ✓ Input Actions asset assigned");

            Debug.Log("\n[2] Input System Setup:");
            Debug.Log("  • Create Input Actions asset with actions:");
            Debug.Log("    ✓ 'Move' (Value Type: Vector2)");
            Debug.Log("    ✓ 'Look' (Value Type: Vector2)");
            Debug.Log("    ✓ 'Attack' (Button)");

            Debug.Log("\n[3] PlayerInputManager Setup:");
            Debug.Log("  • Select PlayerInputManager in scene");
            Debug.Log("  • Assign CoopPlayer prefab to 'Player Prefab'");
            Debug.Log("  • Set 'Join Behavior' = 'Join Players When Button Is Pressed'");
            Debug.Log("  • Default join button: Space, Gamepad A, Enter");

            Debug.Log("\n[4] Scene Setup:");
            Debug.Log("  • Create spawn points:");
            string[] bindings = { "WASD + Mouse", "Arrows + Numpad", "Gamepad 1", "Gamepad 2" };
            for (int i = 0; i < 2; i++)
            {
                Debug.Log($"    • Player {i}: {bindings[i]}");
            }

            Debug.Log("\n[5] Test:");
            Debug.Log("  • Press Space to join as Player 1");
            Debug.Log("  • Press Space/Enter or Gamepad A to join as Player 2");
            Debug.Log("  • Use configured inputs to move players");

            Debug.Log("\n=====================================\n");
        }

        [ContextMenu("Show Active Players")]
        public void ShowActivePlayers()
        {
            var manager = GetComponent<CoopPlayerManager>();
            if (manager == null)
            {
                Debug.LogError("CoopPlayerManager not found");
                return;
            }

            int playerCount = manager.GetPlayerCount();
            Debug.Log($"\n=== Active Players ({playerCount}) ===\n");

            foreach (var context in manager.GetAllPlayerContexts())
            {
                Debug.Log($"Player {context.playerId}:");
                Debug.Log($"  Character: {context.character?.name ?? "None"}");
                Debug.Log($"  Camera: {context.camera?.name ?? "None"}");
                Debug.Log($"  UI Canvas: {context.uiCanvas?.name ?? "None"}");
                Debug.Log($"  Position: {context.character?.transform.position}");
            }

            Debug.Log("\n========================\n");
        }

        [ContextMenu("Test Player Spawn")]
        public void TestPlayerSpawn()
        {
            var playerInputManager = GetComponent<PlayerInputManager>();
            if (playerInputManager == null)
            {
                Debug.LogError("PlayerInputManager not found");
                return;
            }

            Debug.Log("Testing player spawn...");
            Debug.Log("Press Space (or configured join button) to spawn players");
            Debug.Log("Each player will spawn at designated spawn point");
        }
    }
}
