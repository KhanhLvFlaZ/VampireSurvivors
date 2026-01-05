# Network Entity Manager Adapter

## Overview

The `NetworkEntityManagerAdapter` wraps the local `EntityManager` to provide networked gameplay support for co-op mode. It handles spawn ownership, event routing through the network, and separates local vs remote entity simulation.

## Architecture

### Core Responsibilities

1. **Spawn Ownership**: Tracks which client owns each entity
2. **Event Broadcasting**: Routes spawn/despawn events through network
3. **Local/Remote Separation**: Distinguishes between locally simulated and remotely simulated entities
4. **Client Prediction**: Optionally predicts spawns on clients before server confirmation
5. **Reconciliation**: Periodically syncs remote entities with server state

### Design Pattern: Adapter + Proxy

```
Game Code
    ↓
NetworkEntityManagerAdapter (Adapter/Proxy)
    ↓
    ├─→ Local EntityManager (single-player logic)
    ├─→ Network Event Broadcasting
    ├─→ Ownership Tracking
    └─→ Local/Remote Simulation Management
```

## Key Features

### 1. Server-Authoritative Spawning

**Server Behavior:**

- All spawns go through server
- Server assigns ownership to requesting client
- Broadcasts spawn events to all clients
- Maintains authoritative state

**Client Behavior (without prediction):**

- Waits for server spawn confirmation
- Spawns entity locally when event received
- Marks as remote entity

**Client Behavior (with prediction):**

- Immediately spawns entity locally (predicted)
- Waits for server confirmation
- Reconciles if server state differs

### 2. Ownership Tracking

```csharp
// Entity instance ID → Owner client ID
Dictionary<int, ulong> entityOwnershipMap;

// Client ID → Set of owned entities
Dictionary<ulong, HashSet<int>> clientOwnedEntities;
```

**Use Cases:**

- Determine who gets experience/loot from kills
- Priority for damage calculation (owner simulates first)
- Authority for entity behavior decisions

### 3. Local vs Remote Simulation

```csharp
// Entities spawned by this client/server
HashSet<int> localEntities;

// Entities spawned by other clients
HashSet<int> remoteEntities;
```

**Local Entities:**

- Full simulation (AI, collision, physics)
- Send state updates to server
- Authoritative on this client

**Remote Entities:**

- Lightweight simulation (interpolation, animation)
- Receive state updates from server
- Non-authoritative, visual only

### 4. Event Broadcasting

**Event Types:**

- `MonsterSpawn`: New monster created
- `MonsterDespawn`: Monster destroyed
- `MonsterDamage`: Monster took damage
- `CollectibleSpawn`: Gem/coin spawned
- `CollectibleDespawn`: Collectible picked up
- `ChestSpawn`: Chest spawned
- `ChestOpen`: Chest opened

**Flow:**

```
Server:
  Spawn monster → Track ownership → Broadcast event → All clients receive

Client:
  Receive event → Spawn locally → Mark as remote → Subscribe to state updates
```

## Usage Examples

### Basic Setup

```csharp
public class GameManager : MonoBehaviour
{
    [SerializeField] private EntityManager entityManager;
    [SerializeField] private CoopNetworkManager networkManager;
    [SerializeField] private NetworkEntityManagerAdapter networkAdapter;

    void Start()
    {
        // Adapter wraps entity manager
        // All spawn calls go through adapter

        if (networkManager.IsNetworked)
        {
            // Use networked spawning
            UseNetworkAdapter();
        }
        else
        {
            // Use local entity manager directly
            UseLocalEntityManager();
        }
    }

    void UseNetworkAdapter()
    {
        // Spawn monster through network adapter
        Monster monster = networkAdapter.SpawnMonsterRandomPositionNetworked(
            monsterPoolIndex: 0,
            monsterBlueprint: bossBlueprint,
            hpBuff: 0f,
            ownerId: localClientId
        );
    }

    void UseLocalEntityManager()
    {
        // Direct local spawning (single-player)
        Monster monster = entityManager.SpawnMonsterRandomPosition(
            monsterPoolIndex: 0,
            monsterBlueprint: bossBlueprint,
            hpBuff: 0f
        );
    }
}
```

### Spawn with Ownership

```csharp
public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private NetworkEntityManagerAdapter adapter;

    public void SpawnForClient(ulong clientId, MonsterBlueprint blueprint)
    {
        // Server spawns monster owned by specific client
        if (adapter.IsNetworkActive)
        {
            Vector2 spawnPos = GetSpawnPosition();
            Monster monster = adapter.SpawnMonsterNetworked(
                monsterPoolIndex: 0,
                position: spawnPos,
                monsterBlueprint: blueprint,
                hpBuff: 0f,
                ownerId: clientId
            );

            if (monster != null)
            {
                Debug.Log($"Spawned monster for client {clientId}");
            }
        }
    }
}
```

### Check Ownership

```csharp
public class LootDistributor : MonoBehaviour
{
    [SerializeField] private NetworkEntityManagerAdapter adapter;

    public void OnMonsterKilled(Monster monster, ulong killerId)
    {
        int instanceId = monster.GetInstanceID();
        ulong ownerId = adapter.GetEntityOwner(instanceId);

        // Give loot to owner
        if (ownerId == killerId)
        {
            Debug.Log($"Client {killerId} killed their own monster");
            GiveLootToClient(killerId, fullAmount: true);
        }
        else
        {
            Debug.Log($"Client {killerId} killed monster owned by {ownerId}");
            GiveLootToClient(killerId, fullAmount: false); // Shared loot
            GiveLootToClient(ownerId, fullAmount: false);
        }
    }
}
```

### Local vs Remote Check

```csharp
public class MonsterAI : MonoBehaviour
{
    [SerializeField] private NetworkEntityManagerAdapter adapter;

    void Update()
    {
        int instanceId = GetInstanceID();

        if (adapter.IsLocalEntity(instanceId))
        {
            // Full AI simulation
            UpdateAI();
            SendStateToServer();
        }
        else if (adapter.IsRemoteEntity(instanceId))
        {
            // Lightweight simulation (interpolation only)
            InterpolatePosition();
            PlayAnimations();
        }
    }
}
```

### Client Prediction

```csharp
public class PredictiveSpawner : MonoBehaviour
{
    [SerializeField] private NetworkEntityManagerAdapter adapter;

    public void RequestSpawn(MonsterBlueprint blueprint)
    {
        // Client requests spawn with prediction enabled
        Monster predictedMonster = adapter.SpawnMonsterRandomPositionNetworked(
            monsterPoolIndex: 0,
            monsterBlueprint: blueprint,
            hpBuff: 0f,
            ownerId: localClientId
        );

        if (predictedMonster != null)
        {
            // Client sees monster immediately (predicted)
            Debug.Log("Monster spawned locally (predicted)");

            // Will reconcile when server confirmation arrives
        }
    }
}
```

### Passthrough for Non-Networked Entities

```csharp
public class WeaponSystem : MonoBehaviour
{
    [SerializeField] private NetworkEntityManagerAdapter adapter;

    public void FireProjectile(Vector2 position, float damage)
    {
        // Projectiles use local simulation (no networking needed)
        Projectile projectile = adapter.SpawnProjectile(
            projectileIndex: 0,
            position: position,
            damage: damage,
            knockback: 5f,
            speed: 10f,
            targetLayer: LayerMask.GetMask("Enemy")
        );

        // Direct passthrough to local EntityManager
        // No network overhead for frequent projectiles
    }
}
```

## Network Event Flow

### Monster Spawn Flow

**Server Side:**

```
1. SpawnMonsterNetworked() called
2. Spawn locally via EntityManager
3. Assign ownership (ownerId)
4. Track in entityOwnershipMap
5. Add to localEntities set
6. BroadcastMonsterSpawn() to all clients
   ├─ Send poolIndex, position, blueprint, hpBuff, ownerId
   └─ Include server instanceId for mapping
7. Return monster reference
```

**Client Side:**

```
1. Receive BroadcastMonsterSpawn event
2. OnRemoteMonsterSpawn() called
3. Load blueprint by name
4. Spawn locally via EntityManager
5. Add to remoteEntities set
6. Map server instanceId → local instanceId
7. Subscribe to state updates
```

### Monster Despawn Flow

**Server Side:**

```
1. DespawnMonsterNetworked() called
2. Remove from entityOwnershipMap
3. Remove from clientOwnedEntities
4. Remove from localEntities
5. Despawn locally via EntityManager
6. BroadcastMonsterDespawn() to all clients
   ├─ Send instanceId, killedByPlayer, killerId
   └─ Clients use instanceId mapping
```

**Client Side:**

```
1. Receive BroadcastMonsterDespawn event
2. OnRemoteMonsterDespawn() called
3. Find local monster by instanceId mapping
4. Despawn locally via EntityManager
5. Remove from remoteEntities
6. Cleanup instanceId mapping
```

### Collectible Spawn Flow

**Server Side:**

```
1. SpawnExpGemNetworked() / SpawnCoinNetworked()
2. Spawn locally via EntityManager
3. BroadcastCollectibleSpawn() to clients
   ├─ Send type (ExpGem/Coin), position, value
   └─ Collectibles spawned for all clients
4. Return collectible reference
```

**Client Side:**

```
1. Receive BroadcastCollectibleSpawn event
2. Spawn locally via EntityManager
3. No ownership tracking (collectibles free-for-all)
```

## Client Prediction & Reconciliation

### Prediction Strategy

**Benefits:**

- Zero perceived latency for local player
- Responsive gameplay feel
- Smooth enemy spawning

**Challenges:**

- Misprediction corrections visible to player
- Increased complexity
- More network traffic

### Reconciliation Process

**Every `reconciliationInterval` (default: 0.1s):**

```csharp
void ReconcileRemoteEntities()
{
    foreach (int instanceId in remoteEntities)
    {
        // 1. Request state from server
        RequestStateUpdate(instanceId);

        // 2. Compare predicted state vs server state
        Vector2 predictedPos = GetLocalPosition(instanceId);
        Vector2 serverPos = GetServerPosition(instanceId);
        float error = Vector2.Distance(predictedPos, serverPos);

        // 3. Apply correction if error exceeds threshold
        if (error > 0.5f) // 0.5 units threshold
        {
            ApplyPositionCorrection(instanceId, serverPos);
        }
    }
}
```

**Correction Methods:**

- **Snap**: Instant teleport (high error, >2 units)
- **Lerp**: Smooth interpolation (medium error, 0.5-2 units)
- **None**: Keep prediction (low error, <0.5 units)

## Configuration

### Inspector Settings

```csharp
[Header("Network Settings")]
[SerializeField] private bool enableNetworkSpawning = true;
[SerializeField] private bool clientPrediction = true;
[SerializeField] private float reconciliationInterval = 0.1f;
```

**enableNetworkSpawning:**

- `true`: All spawns go through network (co-op mode)
- `false`: Direct local spawning (single-player mode)

**clientPrediction:**

- `true`: Clients predict spawns immediately
- `false`: Clients wait for server confirmation

**reconciliationInterval:**

- Frequency of state reconciliation (seconds)
- Lower = more accurate, higher network cost
- Recommended: 0.05s (20 Hz) to 0.2s (5 Hz)

### Performance Tuning

**High Player Count (4+ players):**

```csharp
enableNetworkSpawning = true;
clientPrediction = false; // Reduce mispredictions
reconciliationInterval = 0.2f; // Lower network cost
```

**Low Latency (<50ms):**

```csharp
enableNetworkSpawning = true;
clientPrediction = true; // Responsive feel
reconciliationInterval = 0.1f; // Standard
```

**High Latency (>150ms):**

```csharp
enableNetworkSpawning = true;
clientPrediction = true; // Hide latency
reconciliationInterval = 0.05f; // Frequent corrections
```

## Statistics & Debugging

### Get Statistics

```csharp
NetworkEntityManagerStats stats = adapter.GetStats();
Debug.Log($"Network Spawns: {stats.totalNetworkSpawns}");
Debug.Log($"Local Spawns: {stats.totalLocalSpawns}");
Debug.Log($"Events Sent: {stats.totalEventsSent}");
Debug.Log($"Events Received: {stats.totalEventsReceived}");
Debug.Log($"Local Entities: {stats.localEntityCount}");
Debug.Log($"Remote Entities: {stats.remoteEntityCount}");
```

### Debug Info

```csharp
string debugInfo = adapter.GetDebugInfo();
Debug.Log(debugInfo);
// Output:
// NetworkEntityManager Stats:
//   Network Spawns: 45
//   Local Spawns: 3
//   Events Sent: 120
//   Events Received: 85
//   Local Entities: 12
//   Remote Entities: 33
//   Tracked Owners: 4
```

### Monitoring Panel

```csharp
public class NetworkDebugPanel : MonoBehaviour
{
    [SerializeField] private NetworkEntityManagerAdapter adapter;
    [SerializeField] private Text debugText;

    void Update()
    {
        var stats = adapter.GetStats();
        debugText.text = $"Network Entities:\n" +
                        $"  Spawns: {stats.totalNetworkSpawns}\n" +
                        $"  Local: {stats.localEntityCount}\n" +
                        $"  Remote: {stats.remoteEntityCount}\n" +
                        $"  Events: ↑{stats.totalEventsSent} ↓{stats.totalEventsReceived}";
    }
}
```

## Best Practices

### 1. Always Check IsNetworkActive

```csharp
if (adapter.IsNetworkActive)
{
    // Use networked methods
    adapter.SpawnMonsterNetworked(...);
}
else
{
    // Fallback to local
    entityManager.SpawnMonster(...);
}
```

### 2. Use Ownership for Loot Distribution

```csharp
ulong ownerId = adapter.GetEntityOwner(monsterInstanceId);
GiveLootToClient(ownerId);
```

### 3. Separate Local/Remote Simulation

```csharp
if (adapter.IsLocalEntity(instanceId))
{
    // Full simulation (expensive)
}
else
{
    // Visual only (cheap)
}
```

### 4. Batch Network Events

```csharp
// Instead of:
foreach (var monster in spawns)
    adapter.SpawnMonsterNetworked(monster);

// Batch:
List<SpawnRequest> requests = CollectSpawnRequests();
adapter.BatchSpawnMonsters(requests);
```

### 5. Handle Network Failures Gracefully

```csharp
Monster monster = adapter.SpawnMonsterNetworked(...);
if (monster == null && !adapter.IsNetworkActive)
{
    // Network failed, fallback to local
    monster = entityManager.SpawnMonster(...);
}
```

## Limitations & Future Enhancements

### Current Limitations

1. **Blueprint Registry**: Requires blueprint lookup by name (not implemented)
2. **Instance ID Mapping**: Server→Client ID mapping needs completion
3. **Batch Events**: No batching for multiple spawns
4. **State Compression**: No delta compression for state updates
5. **RPC Implementation**: Currently using placeholders

### Planned Enhancements

1. **Blueprint Registry**:

   ```csharp
   public interface IBlueprintRegistry
   {
       MonsterBlueprint GetBlueprintByName(string name);
       void RegisterBlueprint(MonsterBlueprint blueprint);
   }
   ```

2. **Instance ID Mapping**:

   ```csharp
   private Dictionary<int, int> serverToClientInstanceMap;
   int GetLocalInstanceId(int serverInstanceId);
   ```

3. **Batch Spawning**:

   ```csharp
   public void BatchSpawnMonsters(List<SpawnRequest> requests)
   {
       // Spawn all locally
       // Send single batch event
   }
   ```

4. **State Delta Compression**:

   ```csharp
   public struct EntityStateDelta
   {
       public int instanceId;
       public Vector2? positionDelta;
       public float? healthDelta;
   }
   ```

5. **Interest Management**:
   ```csharp
   // Only sync entities near player
   bool IsInInterestRadius(Vector2 entityPos, Vector2 playerPos, float radius);
   ```

## Integration Checklist

- [ ] Replace direct EntityManager calls with NetworkEntityManagerAdapter
- [ ] Set up CoopNetworkManager reference
- [ ] Configure network settings (prediction, reconciliation)
- [ ] Implement blueprint registry
- [ ] Test server-side spawning
- [ ] Test client-side prediction
- [ ] Test ownership tracking
- [ ] Add monitoring/debug UI
- [ ] Profile network traffic
- [ ] Optimize reconciliation frequency

## References

- **EntityManager.cs**: Original single-player entity management
- **CoopNetworkManager.cs**: Network session management
- **NetworkEntity.cs**: Base networked entity class
- **NetworkEnemy.cs**: Networked enemy implementation
- **NetworkSpawner.cs**: Original network spawner (can be replaced by adapter)
