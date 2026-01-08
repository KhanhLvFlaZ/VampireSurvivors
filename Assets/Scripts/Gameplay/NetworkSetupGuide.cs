#if ENABLE_NETCODE
using UnityEngine;
using Unity.Netcode;
using Vampire.Gameplay.Networking;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Setup guide for networking in scene
    /// Provides helper methods to configure network prefabs and manager
    /// </summary>
    public class NetworkSetupGuide : MonoBehaviour
    {
        [ContextMenu("Setup Network Game Mode")]
        public void SetupNetworkGameMode()
        {
            Debug.Log("\n========== NETWORK SETUP GUIDE ==========\n");

            // Step 1: Create NetworkManager
            var netManagerGO = GameObject.Find("NetworkManager");
            if (netManagerGO == null)
            {
                netManagerGO = new GameObject("NetworkManager");
                netManagerGO.AddComponent<NetworkManager>();
                netManagerGO.AddComponent<CoopNetworkManager>();
                netManagerGO.AddComponent<CoopOwnershipRegistry>();
                Debug.Log("✓ Created NetworkManager GameObject");
            }

            // Step 2: Check player prefab
            var netManager = netManagerGO.GetComponent<CoopNetworkManager>();
            Debug.Log("\n[Required Setup Steps]:");
            Debug.Log("1. Select 'CoopPlayer' prefab in Project");
            Debug.Log("2. Add NetworkObject component");
            Debug.Log("3. Set 'Is Player Object' = true");
            Debug.Log("4. Add NetworkCharacter component");
            Debug.Log("5. Assign prefab to CoopNetworkManager.playerPrefab in Inspector");

            Debug.Log("\n[Scene Setup]:");
            Debug.Log("6. Create spawn points as child GameObjects");
            Debug.Log("7. Assign to CoopNetworkManager.spawnPoints array");
            Debug.Log("8. Set isServer = true for Server build");
            Debug.Log("9. Set isServer = false for Client build");

            Debug.Log("\n[Testing]:");
            Debug.Log("- Run Server build first");
            Debug.Log("- Run multiple Client builds");
            Debug.Log("- Players should spawn and sync positions");

            Debug.Log("\n========================================\n");
        }

        [ContextMenu("Show Network Prefabs Status")]
        public void ShowNetworkPrefabsStatus()
        {
            Debug.Log("\n========== NETWORK PREFABS STATUS ==========\n");

            // Check CoopPlayer prefab
            var coopPlayerPrefab = Resources.Load<GameObject>("Prefabs/CoopPlayer");
            if (coopPlayerPrefab != null)
            {
                bool hasNetworkObject = coopPlayerPrefab.GetComponent<NetworkObject>() != null;
                bool hasNetworkCharacter = coopPlayerPrefab.GetComponent<NetworkCharacter>() != null;
                bool hasCharacter = coopPlayerPrefab.GetComponent<Character>() != null;

                Debug.Log($"CoopPlayer Prefab:");
                Debug.Log($"  ✓ Found at Assets/Prefabs/CoopPlayer.prefab");
                Debug.Log($"  {(hasNetworkObject ? "✓" : "✗")} NetworkObject component");
                Debug.Log($"  {(hasNetworkCharacter ? "✓" : "✗")} NetworkCharacter component");
                Debug.Log($"  {(hasCharacter ? "✓" : "✗")} Character component");
            }
            else
            {
                Debug.LogWarning("✗ CoopPlayer prefab not found at Resources/Prefabs/");
            }

            // Check NetworkManager
            var netManager = FindObjectOfType<CoopNetworkManager>();
            if (netManager != null)
            {
                Debug.Log($"\nCoopNetworkManager:");
                Debug.Log($"  ✓ Found in scene");
                Debug.Log($"  Is Server: {netManager.IsServer}");
                Debug.Log($"  Local Client ID: {netManager.LocalClientId}");
            }
            else
            {
                Debug.LogWarning("✗ CoopNetworkManager not found in scene");
            }

            Debug.Log("\n==========================================\n");
        }
    }
}
#endif
