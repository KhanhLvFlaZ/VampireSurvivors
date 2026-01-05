#if ENABLE_NETCODE
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Vampire.Gameplay.Networking
{
    /// <summary>
    /// Co-op adapter that wraps EntityManager for networked gameplay
    /// Handles:
    /// - Spawn ownership (server vs client)
    /// - Event routing through network
    /// - Local vs remote simulation separation
    /// </summary>
    public class NetworkEntityManagerAdapter : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private EntityManager localEntityManager;
        [SerializeField] private CoopNetworkManager networkManager;

        [Header("Network Settings")]
        [SerializeField] private bool enableNetworkSpawning = true;
        [SerializeField] private bool clientPrediction = true;
        [SerializeField] private float reconciliationInterval = 0.1f;

        // Network event tracking
        private Dictionary<ulong, List<NetworkEntityEvent>> pendingEvents;
        private float lastReconciliationTime;

        // Spawn ownership tracking
        private Dictionary<int, ulong> entityOwnershipMap; // Entity instance ID -> owner client ID
        private Dictionary<ulong, HashSet<int>> clientOwnedEntities; // Client ID -> owned entities

        // Local vs remote separation
        private HashSet<int> localEntities;
        private HashSet<int> remoteEntities;

        // Statistics
        private int totalNetworkSpawns;
        private int totalLocalSpawns;
        private int totalEventsSent;
        private int totalEventsReceived;

        public EntityManager LocalEntityManager => localEntityManager;
        public bool IsNetworkActive => networkManager != null && networkManager.IsServer;
        public int TotalNetworkSpawns => totalNetworkSpawns;
        public int TotalLocalSpawns => totalLocalSpawns;

        private void Awake()
        {
            pendingEvents = new Dictionary<ulong, List<NetworkEntityEvent>>();
            entityOwnershipMap = new Dictionary<int, ulong>();
            clientOwnedEntities = new Dictionary<ulong, HashSet<int>>();
            localEntities = new HashSet<int>();
            remoteEntities = new HashSet<int>();

            if (localEntityManager == null)
            {
                localEntityManager = GetComponent<EntityManager>();
            }
        }

        private void Update()
{
    if (!IsNetworkActive) return;

    // Periodic reconciliation for client prediction
    if (clientPrediction && Time.time - lastReconciliationTime >= reconciliationInterval)
    {
        ReconcileRemoteEntities();
        lastReconciliationTime = Time.time;
    }
}

#region Monster Spawning (Network Aware)

/// <summary>
/// Spawn monster with network awareness
/// Server: spawns and broadcasts
/// Client: predicts locally if enabled, waits for server confirmation
/// </summary>
public Monster SpawnMonsterNetworked(int monsterPoolIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0, ulong ownerId = 0)
{
    bool isServer = networkManager != null && networkManager.IsServer;
    bool isClient = networkManager != null && !networkManager.IsServer;

    // Server authority: always spawn
    if (isServer || !enableNetworkSpawning)
    {
        Monster monster = localEntityManager.SpawnMonster(monsterPoolIndex, position, monsterBlueprint, hpBuff);

        if (isServer && monster != null)
        {
            int instanceId = monster.GetInstanceID();
            entityOwnershipMap[instanceId] = ownerId;

            if (!clientOwnedEntities.ContainsKey(ownerId))
                clientOwnedEntities[ownerId] = new HashSet<int>();
            clientOwnedEntities[ownerId].Add(instanceId);

            localEntities.Add(instanceId);
            totalNetworkSpawns++;

            // Broadcast spawn to clients
            BroadcastMonsterSpawn(monsterPoolIndex, position, monsterBlueprint, hpBuff, ownerId, instanceId);
        }

        return monster;
    }

    // Client prediction: spawn locally, mark as predicted
    if (isClient && clientPrediction)
    {
        Monster predictedMonster = localEntityManager.SpawnMonster(monsterPoolIndex, position, monsterBlueprint, hpBuff);

        if (predictedMonster != null)
        {
            int instanceId = predictedMonster.GetInstanceID();
            localEntities.Add(instanceId);
            totalLocalSpawns++;

            Debug.Log($"[NetworkEntityManager] Client predicted monster spawn at {position}");
        }

        return predictedMonster;
    }

    // Client without prediction: return null, wait for server
    return null;
}

/// <summary>
/// Spawn monster at random position with network awareness
/// </summary>
public Monster SpawnMonsterRandomPositionNetworked(int monsterPoolIndex, MonsterBlueprint monsterBlueprint, float hpBuff = 0, ulong ownerId = 0)
{
    bool isServer = networkManager != null && networkManager.IsServer;

    if (isServer || !enableNetworkSpawning)
    {
        Monster monster = localEntityManager.SpawnMonsterRandomPosition(monsterPoolIndex, monsterBlueprint, hpBuff);

        if (isServer && monster != null)
        {
            int instanceId = monster.GetInstanceID();
            entityOwnershipMap[instanceId] = ownerId;

            if (!clientOwnedEntities.ContainsKey(ownerId))
                clientOwnedEntities[ownerId] = new HashSet<int>();
            clientOwnedEntities[ownerId].Add(instanceId);

            localEntities.Add(instanceId);
            totalNetworkSpawns++;

            // Broadcast spawn to clients
            Vector2 spawnPos = monster.transform.position;
            BroadcastMonsterSpawn(monsterPoolIndex, spawnPos, monsterBlueprint, hpBuff, ownerId, instanceId);
        }

        return monster;
    }

    return null; // Clients don't spawn randomly
}

/// <summary>
/// Despawn monster with network awareness
/// </summary>
public void DespawnMonsterNetworked(int monsterPoolIndex, Monster monster, bool killedByPlayer = true, ulong killerId = 0)
{
    if (monster == null) return;

    int instanceId = monster.GetInstanceID();
    bool isServer = networkManager != null && networkManager.IsServer;

    // Remove from tracking
    if (entityOwnershipMap.TryGetValue(instanceId, out ulong ownerId))
    {
        entityOwnershipMap.Remove(instanceId);

        if (clientOwnedEntities.TryGetValue(ownerId, out var entities))
        {
            entities.Remove(instanceId);
        }
    }

    localEntities.Remove(instanceId);
    remoteEntities.Remove(instanceId);

    // Despawn locally
    localEntityManager.DespawnMonster(monsterPoolIndex, monster, killedByPlayer);

    // Broadcast despawn to clients
    if (isServer)
    {
        BroadcastMonsterDespawn(instanceId, killedByPlayer, killerId);
    }
}

#endregion

#region Network Event Broadcasting

/// <summary>
/// Broadcast monster spawn to all clients
/// </summary>
private void BroadcastMonsterSpawn(int poolIndex, Vector2 position, MonsterBlueprint blueprint, float hpBuff, ulong ownerId, int instanceId)
{
    if (networkManager == null) return;

    var evt = new NetworkEntityEvent
    {
        eventType = EntityEventType.MonsterSpawn,
        poolIndex = poolIndex,
        position = position,
        blueprintName = blueprint.name,
        hpBuff = hpBuff,
        ownerId = ownerId,
        instanceId = instanceId
    };

    // In real implementation, send via RPC
    // For now, just log
    totalEventsSent++;
    Debug.Log($"[NetworkEntityManager] Broadcasting monster spawn: {blueprint.name} at {position} (owner: {ownerId})");
}

/// <summary>
/// Broadcast monster despawn to all clients
/// </summary>
private void BroadcastMonsterDespawn(int instanceId, bool killedByPlayer, ulong killerId)
{
    if (networkManager == null) return;

    var evt = new NetworkEntityEvent
    {
        eventType = EntityEventType.MonsterDespawn,
        instanceId = instanceId,
        killedByPlayer = killedByPlayer,
        killerId = killerId
    };

    totalEventsSent++;
    Debug.Log($"[NetworkEntityManager] Broadcasting monster despawn: {instanceId} (killed by: {killerId})");
}

/// <summary>
/// Broadcast collectible spawn (exp gem, coin, etc)
/// </summary>
private void BroadcastCollectibleSpawn(string collectibleType, Vector2 position, int value)
{
    if (networkManager == null) return;

    var evt = new NetworkEntityEvent
    {
        eventType = EntityEventType.CollectibleSpawn,
        collectibleType = collectibleType,
        position = position,
        value = value
    };

    totalEventsSent++;
    Debug.Log($"[NetworkEntityManager] Broadcasting collectible spawn: {collectibleType} at {position}");
}

#endregion

#region Client-side Event Handling

/// <summary>
/// Handle monster spawn event from server (client-side)
/// </summary>
public void OnRemoteMonsterSpawn(int poolIndex, Vector2 position, string blueprintName, float hpBuff, ulong ownerId, int serverInstanceId)
{
    // Load blueprint (would need blueprint registry)
    // For now, assume blueprint is available
    // MonsterBlueprint blueprint = GetBlueprintByName(blueprintName);

    // Spawn locally as remote entity
    // Monster monster = localEntityManager.SpawnMonster(poolIndex, position, blueprint, hpBuff);

    // if (monster != null)
    // {
    //     int localInstanceId = monster.GetInstanceID();
    //     remoteEntities.Add(localInstanceId);
    //     // Map server instance ID to local instance ID for future events
    // }

    totalEventsReceived++;
    Debug.Log($"[NetworkEntityManager] Client received monster spawn: pool {poolIndex} at {position} (owner: {ownerId})");
}

/// <summary>
/// Handle monster despawn event from server (client-side)
/// </summary>
public void OnRemoteMonsterDespawn(int serverInstanceId, bool killedByPlayer, ulong killerId)
{
    // Find local monster by server instance ID
    // Would need instance ID mapping
    // Monster monster = FindMonsterByServerInstanceId(serverInstanceId);

    // if (monster != null)
    // {
    //     localEntityManager.DespawnMonster(monster.PoolIndex, monster, killedByPlayer);
    //     remoteEntities.Remove(monster.GetInstanceID());
    // }

    totalEventsReceived++;
    Debug.Log($"[NetworkEntityManager] Client received monster despawn: {serverInstanceId}");
}

#endregion

#region Collectible Spawning (Network Aware)

/// <summary>
/// Spawn exp gem with network awareness
/// </summary>
public ExpGem SpawnExpGemNetworked(Vector2 position, GemType gemType = GemType.White1, bool spawnAnimation = true)
{
    bool isServer = networkManager != null && networkManager.IsServer;

    ExpGem gem = localEntityManager.SpawnExpGem(position, gemType, spawnAnimation);

    if (gem != null && isServer)
    {
        BroadcastCollectibleSpawn("ExpGem", position, (int)gemType);
    }

    return gem;
}

/// <summary>
/// Spawn coin with network awareness
/// </summary>
public Coin SpawnCoinNetworked(Vector2 position, CoinType coinType = CoinType.Bronze1, bool spawnAnimation = true)
{
    bool isServer = networkManager != null && networkManager.IsServer;

    Coin coin = localEntityManager.SpawnCoin(position, coinType, spawnAnimation);

    if (coin != null && isServer)
    {
        BroadcastCollectibleSpawn("Coin", position, (int)coinType);
    }

    return coin;
}

#endregion

#region Local vs Remote Simulation

/// <summary>
/// Check if entity is locally simulated
/// </summary>
public bool IsLocalEntity(int instanceId)
{
    return localEntities.Contains(instanceId);
}

/// <summary>
/// Check if entity is remotely simulated
/// </summary>
public bool IsRemoteEntity(int instanceId)
{
    return remoteEntities.Contains(instanceId);
}

/// <summary>
/// Get owner of entity
/// </summary>
public ulong GetEntityOwner(int instanceId)
{
    return entityOwnershipMap.TryGetValue(instanceId, out ulong ownerId) ? ownerId : 0;
}

/// <summary>
/// Check if client owns entity
/// </summary>
public bool ClientOwnsEntity(ulong clientId, int instanceId)
{
    return clientOwnedEntities.TryGetValue(clientId, out var entities) && entities.Contains(instanceId);
}

/// <summary>
/// Reconcile remote entities with server state
/// </summary>
private void ReconcileRemoteEntities()
{
    // In full implementation:
    // 1. Request state update from server for remote entities
    // 2. Compare predicted state vs server state
    // 3. Apply corrections if mismatch exceeds threshold

    Debug.Log($"[NetworkEntityManager] Reconciling {remoteEntities.Count} remote entities");
}

#endregion

#region Passthrough Methods (Direct to Local EntityManager)

public void CollectAllCoinsAndGems() => localEntityManager.CollectAllCoinsAndGems();
public void DamageAllVisibileEnemies(float damage) => localEntityManager.DamageAllVisibileEnemies(damage);
public void KillAllMonsters() => localEntityManager.KillAllMonsters();
public bool TransformOnScreen(Transform transform, Vector2 buffer = default) => localEntityManager.TransformOnScreen(transform, buffer);

// Pool management passthroughs
public Projectile SpawnProjectile(int projectileIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
    => localEntityManager.SpawnProjectile(projectileIndex, position, damage, knockback, speed, targetLayer);

public void DespawnProjectile(int projectileIndex, Projectile projectile)
    => localEntityManager.DespawnProjectile(projectileIndex, projectile);

public int AddPoolForProjectile(GameObject projectilePrefab)
    => localEntityManager.AddPoolForProjectile(projectilePrefab);

public Throwable SpawnThrowable(int throwableIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
    => localEntityManager.SpawnThrowable(throwableIndex, position, damage, knockback, speed, targetLayer);

public void DespawnThrowable(int throwableIndex, Throwable throwable)
    => localEntityManager.DespawnThrowable(throwableIndex, throwable);

public Boomerang SpawnBoomerang(int boomerangIndex, Vector2 position, float damage, float knockback, float throwDistance, float throwTime, LayerMask targetLayer)
    => localEntityManager.SpawnBoomerang(boomerangIndex, position, damage, knockback, throwDistance, throwTime, targetLayer);

public void DespawnBoomerang(int boomerangIndex, Boomerang boomerang)
    => localEntityManager.DespawnBoomerang(boomerangIndex, boomerang);

public Chest SpawnChest(ChestBlueprint chestBlueprint)
    => localEntityManager.SpawnChest(chestBlueprint);

public Chest SpawnChest(ChestBlueprint chestBlueprint, Vector2 position)
    => localEntityManager.SpawnChest(chestBlueprint, position);

public void DespawnChest(Chest chest)
    => localEntityManager.DespawnChest(chest);

public DamageText SpawnDamageText(Vector2 position, float damage)
    => localEntityManager.SpawnDamageText(position, damage);

public void DespawnDamageText(DamageText text)
    => localEntityManager.DespawnDamageText(text);

#endregion

#region Statistics & Debugging

/// <summary>
/// Get adapter statistics
/// </summary>
public NetworkEntityManagerStats GetStats()
{
    return new NetworkEntityManagerStats
    {
        totalNetworkSpawns = totalNetworkSpawns,
        totalLocalSpawns = totalLocalSpawns,
        totalEventsSent = totalEventsSent,
        totalEventsReceived = totalEventsReceived,
        localEntityCount = localEntities.Count,
        remoteEntityCount = remoteEntities.Count,
        totalTrackedOwners = clientOwnedEntities.Count
    };
}

/// <summary>
/// Get debug info string
/// </summary>
public string GetDebugInfo()
{
    var stats = GetStats();
    return $"NetworkEntityManager Stats:\n" +
           $"  Network Spawns: {stats.totalNetworkSpawns}\n" +
           $"  Local Spawns: {stats.totalLocalSpawns}\n" +
           $"  Events Sent: {stats.totalEventsSent}\n" +
           $"  Events Received: {stats.totalEventsReceived}\n" +
           $"  Local Entities: {stats.localEntityCount}\n" +
           $"  Remote Entities: {stats.remoteEntityCount}\n" +
           $"  Tracked Owners: {stats.totalTrackedOwners}";
}

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Network entity event types
    /// </summary>
    public enum EntityEventType
{
    MonsterSpawn,
    MonsterDespawn,
    MonsterDamage,
    CollectibleSpawn,
    CollectibleDespawn,
    ChestSpawn,
    ChestOpen
}

/// <summary>
/// Network entity event data
/// </summary>
public struct NetworkEntityEvent
{
    public EntityEventType eventType;

    // Common fields
    public int instanceId;
    public Vector2 position;
    public ulong ownerId;

    // Monster-specific
    public int poolIndex;
    public string blueprintName;
    public float hpBuff;
    public bool killedByPlayer;
    public ulong killerId;

    // Collectible-specific
    public string collectibleType;
    public int value;

    // Damage-specific
    public float damage;
    public Vector2 damageSource;
}

/// <summary>
/// Statistics for network entity manager
/// </summary>
public struct NetworkEntityManagerStats
{
    public int totalNetworkSpawns;
    public int totalLocalSpawns;
    public int totalEventsSent;
    public int totalEventsReceived;
    public int localEntityCount;
    public int remoteEntityCount;
    public int totalTrackedOwners;
}

#endregion
#endif
