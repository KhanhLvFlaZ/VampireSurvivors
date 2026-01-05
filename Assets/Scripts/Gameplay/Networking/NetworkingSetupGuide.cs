#if ENABLE_NETCODE
using UnityEngine;

namespace Vampire.Gameplay.Networking
{
    /// <summary>
    /// Setup guide for networking integration
    /// Copy this file to Editor folder if auto-setup needed
    /// </summary>
    public static class NetworkingSetupGuide
    {
        // SETUP CHECKLIST FOR CO-OP NETWORKING
        // =====================================

        /*
         * 1. PREFAB CONFIGURATION
         * 
         * Player Prefab:
         *   ✓ Add NetworkObject component
         *   ✓ Set IsPlayerObject = true
         *   ✓ Set AutoSpawn = false (server controls)
         *   ✓ Add NetworkCharacter script
         *   ✓ Add Character script (existing)
         *   ✓ Add Rigidbody2D with Constraints (Gravity Scale = 0, Freeze Rotation)
         *   ✓ Add CharacterController if using input system
         *   
         * Enemy Prefab (each variant):
         *   ✓ Add NetworkObject component
         *   ✓ Set IsPlayerObject = false
         *   ✓ Set AutoSpawn = false (server controls)
         *   ✓ Add NetworkEnemy script
         *   ✓ Add Monster script (existing)
         *   ✓ Add Rigidbody2D with Constraints
         * 
         * 2. SCENE SETUP
         * 
         * Create empty GameObjects:
         *   ✓ "NetworkManager" with CoopNetworkManager script
         *   ✓ "NetworkSpawner" with NetworkSpawner script
         *   ✓ "CoopPlayersManager" with CoopPlayerManager script
         *   ✓ "CoopOwnershipRegistry" with CoopOwnershipRegistry script
         * 
         * NetworkSpawner Configuration:
         *   ✓ Assign Player Prefab
         *   ✓ Add Enemy Prefabs to list (Vampire, Zombie, etc.)
         * 
         * 3. SCRIPT DEPENDENCIES
         * 
         * Make sure these exist and implement IDamageable:
         *   ✓ Character class (player)
         *   ✓ Monster class (enemy)
         *   ✓ Both implement TakeDamage(), Heal(), CurrentHealth, IsAlive
         * 
         * 4. NETWORK MANAGER SETUP
         * 
         * In CoopNetworkManager.cs OnConnectionApproved():
         *   ✓ Call NetworkSpawner.Instance.SpawnPlayerForClient(clientId)
         * 
         * In CoopNetworkManager.cs OnClientDisconnected():
         *   ✓ Call NetworkSpawner.Instance.DespawnPlayer(clientId)
         * 
         * 5. GAMEPLAY INTEGRATION
         * 
         * Spawning Enemies:
         *   NetworkSpawner.Instance.SpawnEnemy(position, typeIndex);
         * 
         * Damage System:
         *   networkCharacter.TakeDamage(amount);
         *   networkEnemy.TakeDamage(amount);
         * 
         * 6. TESTING
         * 
         * Single Player:
         *   ✓ Run game with IsServer = true
         *   ✓ Verify player spawns at correct position
         *   ✓ Verify movement is smooth
         *   ✓ Verify damage works
         * 
         * Two Player (Local):
         *   ✓ Keep IsServer = true
         *   ✓ Use PlayerInputManager for 2 players
         *   ✓ Verify both players visible
         *   ✓ Verify both can move/attack
         * 
         * Network (if implementing remote):
         *   ✓ Test with 2 game instances
         *   ✓ One as server, one as client
         *   ✓ Verify connection established
         *   ✓ Verify players synchronize
         */

        // CODE EXAMPLES
        // =============

        /// <summary>
        /// Example: Spawn player when client connects (in CoopNetworkManager)
        /// </summary>
        public static void ExampleSpawnPlayer(ulong clientId)
        {
            // Server only
            if (NetworkManager.Singleton.IsServer)
            {
                NetworkSpawner.Instance.SpawnPlayerForClient(clientId);
            }
        }

        /// <summary>
        /// Example: Spawn enemy wave
        /// </summary>
        public static void ExampleSpawnEnemyWave()
        {
            for (int i = 0; i < 5; i++)
            {
                Vector2 randomPos = new Vector2(
                    Random.Range(-10f, 10f),
                    Random.Range(-10f, 10f)
                );
                int randomType = Random.Range(0, 2); // 2 enemy types
                
                NetworkSpawner.Instance.SpawnEnemy(randomPos, randomType);
            }
        }

        /// <summary>
        /// Example: Take damage (works from any client)
        /// </summary>
        public static void ExampleTakeDamage(NetworkCharacter player)
        {
            player.TakeDamage(10f);
            // Server validates, broadcasts health update
        }

        /// <summary>
        /// Example: Check player alive status
        /// </summary>
        public static void ExampleCheckAlive(NetworkCharacter player)
        {
            if (player.IsCharacterAlive)
            {
                Debug.Log("Player is alive");
            }
        }

        /// <summary>
        /// Example: Get all spawned entities
        /// </summary>
        public static void ExampleGetAllEntities()
        {
            var players = NetworkSpawner.Instance.GetAllPlayers();
            var enemies = NetworkSpawner.Instance.GetAllEnemies();
            
            Debug.Log($"Players: {players.Count}, Enemies: {enemies.Count}");
        }
    }
}
#endif
