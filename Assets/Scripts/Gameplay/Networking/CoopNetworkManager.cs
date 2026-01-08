#if ENABLE_NETCODE
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Vampire.Gameplay.Networking
{
    /// <summary>
    /// Network initialization and management for co-op gameplay
    /// Handles client/server setup, player spawning, and network object ownership
    /// Integrates with NetworkSpawner for centralized spawn management
    /// </summary>
    public class CoopNetworkManager : MonoBehaviour
    {
        public static CoopNetworkManager Instance { get; private set; }

        [Header("Network Settings")]
        [SerializeField] private ushort port = 7777;
        [SerializeField] private string serverAddress = "localhost";
        [SerializeField] private bool isServer = true;

        [Header("Spawner Reference")]
        [SerializeField] private NetworkSpawner networkSpawner;

        private NetworkManager networkManager;
        private CoopOwnershipRegistry ownershipRegistry;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeNetworkManager();
        }

        private void InitializeNetworkManager()
        {
            networkManager = GetComponent<NetworkManager>();
            if (networkManager == null)
            {
                networkManager = gameObject.AddComponent<NetworkManager>();
            }

            // Configure NetworkManager
            networkManager.NetworkConfig.ConnectionApprovalCallback += ApprovalCheck;
            networkManager.OnServerStarted += OnServerStarted;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;

            // Get or create ownership registry
            ownershipRegistry = GetComponent<CoopOwnershipRegistry>();
            if (ownershipRegistry == null)
            {
                ownershipRegistry = gameObject.AddComponent<CoopOwnershipRegistry>();
            }

            Debug.Log($"[Network] CoopNetworkManager initialized. Mode: {(isServer ? "Server" : "Client")}");
        }

        private void Start()
        {
            if (isServer)
            {
                StartServer();
            }
            else
            {
                StartClient();
            }
        }

        private void StartServer()
        {
            if (!networkManager.StartServer())
            {
                Debug.LogError("[Network] Failed to start server");
            }
            else
            {
                Debug.Log("[Network] Server started successfully");
            }
        }

        private void StartClient()
        {
            if (!networkManager.StartClient())
            {
                Debug.LogError("[Network] Failed to start client");
            }
            else
            {
                Debug.Log("[Network] Client connecting...");
            }
        }

        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            // Approve all connections for now (in production, validate client)
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.PlayerPrefabHash = playerPrefab != null
                ? networkManager.NetworkConfig.Prefabs.GetPrefabHash(playerPrefab)
                : null;
        }

        private void OnServerStarted()
        {
            Debug.Log("[Network] Server started and accepting connections");
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[Network] Client {clientId} connected");

            if (IsServer)
            {
                // Server spawns player for connected client
                SpawnPlayerForClient(clientId);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[Network] Client {clientId} disconnected");

            if (spawnedPlayers.TryGetValue(clientId, out var playerGO))
            {
                if (playerGO != null && playerGO.TryGetComponent<NetworkObject>(out var networkObject))
                {
                    networkObject.Despawn();
                }
                spawnedPlayers.Remove(clientId);
            }
        }

        /// <summary>
        /// Server-side: Spawn player for a connected client
        /// </summary>
        private void SpawnPlayerForClient(ulong clientId)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[Network] Player prefab not assigned");
                return;
            }

            // Get spawn point
            Vector3 spawnPos = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[(int)(clientId % spawnPoints.Length)].position
                : Vector3.zero;

            // Instantiate player
            GameObject playerGO = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

            // Make it a network object owned by the client
            NetworkObject networkObject = playerGO.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.SpawnAsPlayerObject(clientId, destroyWithScene: true);
                spawnedPlayers[clientId] = playerGO;

                // Register ownership
                ownershipRegistry.RegisterPlayer(playerGO, (int)clientId);

                Debug.Log($"[Network] Player spawned for client {clientId} at {spawnPos}");
            }
            else
            {
                Debug.LogError("[Network] Player prefab missing NetworkObject component");
                Destroy(playerGO);
            }
        }

        /// <summary>
        /// Register enemy ownership (for RPC-based updates or observer pattern)
        /// </summary>
        public void RegisterEnemyOwnership(GameObject enemy, ulong ownerId)
        {
            if (enemy.TryGetComponent<NetworkObject>(out var networkObject))
            {
                ownershipRegistry.RegisterEnemyOwnership(enemy, (int)ownerId);
                Debug.Log($"[Network] Enemy {enemy.name} registered to owner {ownerId}");
            }
        }

        public bool IsServer => networkManager.IsServer;
        public bool IsClient => networkManager.IsClient;
        public ulong LocalClientId => networkManager.LocalClientId;

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.NetworkConfig.ConnectionApprovalCallback -= ApprovalCheck;
            }
        }
    }
}
#endif
