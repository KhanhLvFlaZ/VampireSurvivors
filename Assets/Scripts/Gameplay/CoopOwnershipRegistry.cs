#if ENABLE_NETCODE
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Registry for tracking spawn ownership and player IDs
    /// Bridges between NGO NetworkObject ownership and game logic
    /// Maps network client IDs to game entities for RPC routing and reconciliation
    /// </summary>
    public class CoopOwnershipRegistry : MonoBehaviour
    {
        private readonly Dictionary<int, int> enemyOwnerByInstanceId = new Dictionary<int, int>();
        private readonly Dictionary<int, int> playerIdByInstanceId = new Dictionary<int, int>();
        private readonly Dictionary<ulong, GameObject> playersByNetworkClientId = new Dictionary<ulong, GameObject>();
        private readonly Dictionary<int, ulong> networkClientIdByPlayerId = new Dictionary<int, ulong>();

        /// <summary>
        /// Register a spawned enemy with its owner (network client ID)
        /// </summary>
        public void RegisterEnemyOwnership(GameObject enemy, int ownerId)
        {
            if (enemy == null) return;
            enemyOwnerByInstanceId[enemy.GetInstanceID()] = ownerId;
            Debug.Log($"[Ownership] Enemy {enemy.name} registered to owner {ownerId}");
        }

        public int GetEnemyOwner(GameObject enemy)
        {
            if (enemy == null) return -1;
            return enemyOwnerByInstanceId.TryGetValue(enemy.GetInstanceID(), out var owner) ? owner : -1;
        }

        /// <summary>
        /// Register a player with its network identity
        /// </summary>
        public void RegisterPlayer(GameObject player, int playerId)
        {
            if (player == null) return;

            playerIdByInstanceId[player.GetInstanceID()] = playerId;

            // Also track by network client ID if available
            if (player.TryGetComponent<NetworkObject>(out var networkObject))
            {
                ulong clientId = networkObject.OwnerClientId;
                playersByNetworkClientId[clientId] = player;
                networkClientIdByPlayerId[playerId] = clientId;

                Debug.Log($"[Ownership] Player {player.name} registered: PlayerId={playerId}, NetworkClientId={clientId}");
            }
        }

        public int GetPlayerId(GameObject player)
        {
            if (player == null) return -1;
            return playerIdByInstanceId.TryGetValue(player.GetInstanceID(), out var id) ? id : -1;
        }

        /// <summary>
        /// Get player by network client ID (useful for receiving RPCs)
        /// </summary>
        public GameObject GetPlayerByNetworkClientId(ulong clientId)
        {
            return playersByNetworkClientId.TryGetValue(clientId, out var player) ? player : null;
        }

        /// <summary>
        /// Get network client ID for a player
        /// </summary>
        public ulong GetNetworkClientId(int playerId)
        {
            return networkClientIdByPlayerId.TryGetValue(playerId, out var clientId) ? clientId : ulong.MaxValue;
        }

        /// <summary>
        /// Get all registered players
        /// </summary>
        public Dictionary<ulong, GameObject> GetAllPlayers()
        {
            return new Dictionary<ulong, GameObject>(playersByNetworkClientId);
        }

        /// <summary>
        /// Clean up ownership when player disconnects
        /// </summary>
        public void UnregisterPlayer(GameObject player)
        {
            if (player == null) return;

            int instanceId = player.GetInstanceID();

            if (playerIdByInstanceId.TryGetValue(instanceId, out int playerId))
            {
                playerIdByInstanceId.Remove(instanceId);
                networkClientIdByPlayerId.Remove(playerId);
            }

            if (player.TryGetComponent<NetworkObject>(out var networkObject))
            {
                playersByNetworkClientId.Remove(networkObject.OwnerClientId);
                Debug.Log($"[Ownership] Player {player.name} unregistered");
            }
        }
    }
}
#endif
