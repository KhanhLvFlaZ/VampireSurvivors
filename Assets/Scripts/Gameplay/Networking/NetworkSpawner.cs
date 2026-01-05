#if ENABLE_NETCODE
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Vampire.Gameplay.Networking
{
    /// <summary>
    /// Server-side spawner for players and enemies with ownership assignment
    /// Handles spawn/despawn lifecycle with ownerId tracking
    /// </summary>
    public class NetworkSpawner : NetworkBehaviour
    {
        public static NetworkSpawner Instance { get; private set; }

        [Header("Spawn Settings")]
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(10f, 10f);
        [SerializeField] private Vector2 spawnAreaCenter = Vector2.zero;

        // Player prefabs
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private List<NetworkObject> enemyPrefabs = new List<NetworkObject>();

        // Spawn tracking
        private Dictionary<ulong, NetworkObject> spawnedPlayers = new Dictionary<ulong, NetworkObject>();
        private List<NetworkObject> spawnedEnemies = new List<NetworkObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Server: Spawn player for specific client with ownership
        /// </summary>
        public void SpawnPlayerForClient(ulong clientId)
        {
            if (!IsServer)
            {
                Debug.LogError("[NetworkSpawner] Only server can spawn players!");
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[NetworkSpawner] Player prefab not assigned!");
                return;
            }

            // Calculate spawn position (round-robin based on client ID)
            Vector2 spawnPosition = GetPlayerSpawnPosition(clientId);

            // Instantiate player prefab
            NetworkObject playerInstance = Instantiate(
                playerPrefab,
                spawnPosition,
                Quaternion.identity
            );

            // Spawn with network ownership
            playerInstance.SpawnAsPlayerObject(clientId);

            // Register in tracking
            spawnedPlayers[clientId] = playerInstance;

            Debug.Log($"[NetworkSpawner] Spawned player for client {clientId} at {spawnPosition}");
        }

        /// <summary>
        /// Server: Despawn player when client disconnects
        /// </summary>
        public void DespawnPlayer(ulong clientId)
        {
            if (!IsServer)
                return;

            if (spawnedPlayers.TryGetValue(clientId, out NetworkObject playerInstance))
            {
                if (playerInstance != null)
                {
                    playerInstance.Despawn();
                    Destroy(playerInstance.gameObject);
                }
                spawnedPlayers.Remove(clientId);

                Debug.Log($"[NetworkSpawner] Despawned player for client {clientId}");
            }
        }

        /// <summary>
        /// Server: Spawn enemy at position with owner assignment
        /// </summary>
        public void SpawnEnemy(Vector2 position, int enemyTypeIndex = 0, ulong? ownerId = null)
        {
            if (!IsServer)
            {
                Debug.LogError("[NetworkSpawner] Only server can spawn enemies!");
                return;
            }

            if (enemyPrefabs.Count == 0)
            {
                Debug.LogError("[NetworkSpawner] No enemy prefabs assigned!");
                return;
            }

            // Clamp enemy type index
            enemyTypeIndex = Mathf.Clamp(enemyTypeIndex, 0, enemyPrefabs.Count - 1);

            // Instantiate enemy
            NetworkObject enemyInstance = Instantiate(
                enemyPrefabs[enemyTypeIndex],
                position,
                Quaternion.identity
            );

            // Spawn with optional ownership (default: server-owned)
            if (ownerId.HasValue)
            {
                enemyInstance.SpawnWithOwnership(ownerId.Value);
            }
            else
            {
                enemyInstance.Spawn();
            }

            // Track enemy
            spawnedEnemies.Add(enemyInstance);

            Debug.Log($"[NetworkSpawner] Spawned enemy type {enemyTypeIndex} at {position}");
        }

        /// <summary>
        /// Server: Despawn enemy
        /// </summary>
        public void DespawnEnemy(NetworkObject enemyInstance)
        {
            if (!IsServer)
                return;

            if (spawnedEnemies.Contains(enemyInstance))
            {
                enemyInstance.Despawn();
                Destroy(enemyInstance.gameObject);
                spawnedEnemies.Remove(enemyInstance);

                Debug.Log($"[NetworkSpawner] Despawned enemy");
            }
        }

        /// <summary>
        /// Server: Clear all spawned enemies (for wave clear or level reset)
        /// </summary>
        public void ClearAllEnemies()
        {
            if (!IsServer)
                return;

            List<NetworkObject> enemyList = new List<NetworkObject>(spawnedEnemies);
            foreach (var enemy in enemyList)
            {
                if (enemy != null)
                {
                    enemy.Despawn();
                    Destroy(enemy.gameObject);
                }
            }
            spawnedEnemies.Clear();

            Debug.Log("[NetworkSpawner] Cleared all enemies");
        }

        /// <summary>
        /// Get spawn position for player (circular arrangement based on client ID)
        /// </summary>
        private Vector2 GetPlayerSpawnPosition(ulong clientId)
        {
            int playerIndex = (int)(clientId % 4); // Max 4 players
            float angle = (playerIndex * 90f) * Mathf.Deg2Rad; // 90 degree spacing
            float spawnRadius = 3f;

            return spawnAreaCenter + new Vector2(
                Mathf.Cos(angle) * spawnRadius,
                Mathf.Sin(angle) * spawnRadius
            );
        }

        /// <summary>
        /// Get all spawned players
        /// </summary>
        public Dictionary<ulong, NetworkObject> GetAllPlayers() => new Dictionary<ulong, NetworkObject>(spawnedPlayers);

        /// <summary>
        /// Get all spawned enemies
        /// </summary>
        public List<NetworkObject> GetAllEnemies() => new List<NetworkObject>(spawnedEnemies);

        /// <summary>
        /// Get player instance for client
        /// </summary>
        public NetworkObject GetPlayerForClient(ulong clientId)
        {
            spawnedPlayers.TryGetValue(clientId, out var player);
            return player;
        }

        public int GetEnemyCount() => spawnedEnemies.Count;
        public int GetPlayerCount() => spawnedPlayers.Count;
    }
#endif
