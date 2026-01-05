# Vampire Survivors - Co-op Networking Architecture

## Overview

This document describes the complete networking system for co-op gameplay using Unity Netcode for GameObjects (NGO). The system is designed for **server-authoritative gameplay** with **client-side prediction** and **server reconciliation**.

## Architecture Principles

### 1. Server-Authoritative Model

- **Server** is the single source of truth for all game state
- **Clients** apply input locally for immediate feedback (client-side prediction)
- **Server** validates client predictions and reconciles when needed
- **Prevents cheating** by not trusting client physics/movement

### 2. Client-Side Prediction

- Owner client applies movement input immediately without waiting for server
- Provides responsive gameplay with low perceived latency
- Client sees smooth motion before server confirmation

### 3. Server Reconciliation

- Server receives client position and validates it
- If error > 2m: hard teleport (likely lag spike)
- If error > 0.1m: smooth lerp (normal network jitter)
- Server broadcasts corrected state to all clients

### 4. NetworkVariables vs RPCs

**Use NetworkVariables for:**

- Continuous state (position, velocity, health)
- Synced every 0.1s (10Hz tick rate)
- Automatic replication to all clients
- No code needed to listen for changes (can override OnNetworkVariableUpdate)

**Use RPCs for:**

- Rare events (ability activation, special effects)
- Non-critical data (animation triggers)
- Minimizes bandwidth usage

## Class Hierarchy

```
NetworkEntity (abstract base)
├── NetworkCharacter (player)
└── NetworkEnemy (monster)

CoopNetworkManager (initialization)
NetworkSpawner (spawn/despawn management)
CoopOwnershipRegistry (ID mapping)
```

## Component Details

### NetworkEntity (Base Class)

**Purpose:** Common networked object functionality

**NetworkVariables:**

- `networkPosition` (Vector2, Server write)
- `networkVelocity` (Vector2, Server write)
- `networkHealth` (float, Server write)

**Virtual Methods:**

```csharp
UpdateServerState()          // Server updates network state
ApplyLocalInput()            // Owner client applies local input
InterpolateToNetworkState()  // Non-owner clients smooth to state
```

**Usage:**

```csharp
// Get networked state (all clients)
Vector2 pos = entity.GetNetworkPosition();
float health = entity.GetNetworkHealth();

// Server updates state
networkHealth.Value = 50f;
```

### NetworkCharacter (Player)

**Extends:** NetworkEntity

**Features:**

- Client-side movement prediction
- Server validation of position
- Health/damage synchronization
- Death broadcast to all clients

**RPCs:**

```csharp
[ServerRpc]
SyncStateToServerServerRpc()     // Client → Server: position/velocity/health
ReportDamageServerRpc()           // Client → Server: damage taken
ReportHealServerRpc()             // Client → Server: healing

[ClientRpc]
UpdateClientStateClientRpc()      // Server → All: corrected state
OnCharacterDeadClientRpc()        // Server → All: death event
```

**Server Reconciliation Thresholds:**

- Hard correction: position error > 2m
- Soft correction (lerp): position error > 0.1m

**Example:**

```csharp
// Owner client automatically syncs to server every 0.1s
// Server validates and broadcasts correction
// Non-owner clients interpolate smoothly

// Taking damage (works on any client)
networkCharacter.TakeDamage(10f);
// Server receives RPC, applies damage, broadcasts health update
```

### NetworkEnemy (Monster)

**Extends:** NetworkEntity

**Features:**

- Server-side AI logic
- Nearest player targeting
- Wander behavior when no target
- All clients interpolate smoothly

**Server AI (UpdateServerState):**

1. Find nearest player (20m detection)
2. Chase if within range
3. Attack if close enough
4. Wander if no target

**NetworkVariables:**

- `networkIsAttacking` (bool, Server write)
- `networkCurrentAction` (int, Server write)

**Example:**

```csharp
// Server spawns enemy
NetworkSpawner.Instance.SpawnEnemy(position, enemyType);
// AI runs on server only
// All clients see smooth interpolation of position/velocity
```

### NetworkSpawner (Spawn Manager)

**Purpose:** Centralized spawn/despawn for all network objects

**Public Methods:**

```csharp
// Player spawning
SpawnPlayerForClient(ulong clientId)
DespawnPlayer(ulong clientId)

// Enemy spawning
SpawnEnemy(Vector2 position, int enemyTypeIndex = 0, ulong? ownerId = null)
DespawnEnemy(NetworkObject enemyInstance)
ClearAllEnemies()

// Queries
GetAllPlayers()
GetAllEnemies()
GetPlayerForClient(ulong clientId)
GetEnemyCount()
GetPlayerCount()
```

**Example:**

```csharp
// Server spawns player for connected client
NetworkSpawner.Instance.SpawnPlayerForClient(clientId);

// Spawn enemy with server ownership
NetworkSpawner.Instance.SpawnEnemy(Vector2.zero, 0);

// Spawn enemy owned by specific player (for co-op rewards)
NetworkSpawner.Instance.SpawnEnemy(pos, 0, playerId);

// Cleanup
NetworkSpawner.Instance.DespawnPlayer(clientId);
NetworkSpawner.Instance.ClearAllEnemies();
```

### CoopNetworkManager (Initialization)

**Setup Steps:**

1. Initializes NetworkManager component
2. Configures connection approval
3. Hooks connection/disconnection callbacks
4. Integrates with NetworkSpawner

**Connection Callbacks:**

```csharp
OnClientConnected(clientId)    // Server spawns player
OnClientDisconnected(clientId) // Server despawns player
```

**Example Integration:**

```csharp
// In Start/OnEnable
if (NetworkManager.IsServer)
{
    CoopNetworkManager.Instance.InitializeServer();
}
else
{
    CoopNetworkManager.Instance.InitializeClient();
}
```

## Network Flow Diagrams

### Player Movement Sync

```
Owner Client                      Server                    Other Clients
    |                             |                              |
    | Apply Input Locally         |                              |
    | Move(input) → immediate     |                              |
    |                             |                              |
    | 0.1s tick:                  |                              |
    | SyncStateToServerServerRpc->| Validate position error      |
    |                             | ReconcileState()             |
    |                             | UpdateClientStateClientRpc --| Interpolate
    |                             |                              | to new state
```

### Enemy AI Sync

```
Server                            All Clients
   |                                  |
   | UpdateAILogic() runs 60 Hz        |
   | FindNearestPlayer()               |
   | Chase/Attack decisions            |
   |                                   |
   | 0.1s tick:                        |
   | Update networkPosition.Value      |
   | Update networkVelocity.Value  ----| NetworkVariable callback
   | Update networkIsAttacking.Value   | Auto-replicate
   |                                   | Interpolate position
   |                                   | Update animation
```

## Configuration Best Practices

### Sync Rate (networkTickRate)

**Recommended:** 0.1s (10 Hz)

```csharp
// In NetworkEntity
[SerializeField] protected float networkTickRate = 0.1f;
```

- 0.05s (20 Hz): High bandwidth, very smooth
- 0.1s (10 Hz): Balanced (default)
- 0.25s (4 Hz): Low bandwidth, noticeable delay

### Position Error Thresholds

**In NetworkCharacter:**

```csharp
private const float POSITION_ERROR_THRESHOLD_HARD = 2f;  // Hard teleport
private const float POSITION_ERROR_THRESHOLD_SOFT = 0.1f; // Smooth lerp
```

Adjust based on:

- Map size (larger maps → larger thresholds)
- Network stability (unstable → smaller thresholds)
- Game feel (less correction → snappier, more correction → stable)

### Interpolation

```csharp
// Per entity
[SerializeField] protected bool useInterpolation = true;
```

- Enable for smooth client-side visuals
- Disable for turn-based or deterministic games

## Common Issues & Solutions

### Issue: Players jittering/snapping

**Causes:**

1. Sync rate too high (network congestion)
2. Position error threshold too small (oversensitive)
3. Interpolation disabled

**Solutions:**

1. Increase `networkTickRate` to 0.15s or 0.25s
2. Increase `POSITION_ERROR_THRESHOLD_SOFT` to 0.2m
3. Enable `useInterpolation = true`

### Issue: Movement feels unresponsive

**Causes:**

1. Client-side prediction disabled
2. High position error threshold (waiting for server)
3. Owner client interpolating instead of predicting

**Solutions:**

1. Verify `ApplyLocalInput()` is called on owner
2. Reduce error threshold
3. Check `IsOwner` check in interpolation code

### Issue: Enemies not appearing on clients

**Causes:**

1. Enemy prefab doesn't have NetworkObject
2. Enemy not spawned with `Spawn()` or `SpawnWithOwnership()`
3. NetworkObject's `IsPlayerObject` flag set incorrectly

**Solutions:**

1. Add NetworkObject to enemy prefab
2. Use NetworkSpawner.SpawnEnemy()
3. Verify prefab configuration

## Ownership Assignment

### Player Objects

```csharp
// Server ownership
playerInstance.SpawnAsPlayerObject(clientId);
```

- Automatically owned by the client
- Only that client's NetworkCharacter exists on their client
- Enables client-side prediction

### Enemy Objects

```csharp
// Server-owned (default)
enemyInstance.Spawn();

// Client-owned (for co-op attribution)
enemyInstance.SpawnWithOwnership(clientId);
```

## Testing the Network System

### Local Testing

```csharp
// Start as server
if (NetworkManager.Singleton.IsServer)
{
    // Test server logic
    var spawner = NetworkSpawner.Instance;
    Assert.AreEqual(spawner.GetPlayerCount(), 2);
}

// Test client prediction
float expectedPos = playerPos + velocity * deltaTime;
Assert.AreEqual(networkCharacter.GetNetworkPosition(), expectedPos, 0.1f);
```

### Network Testing Checklist

- [ ] Players spawn at correct positions
- [ ] Movement is smooth without jittering
- [ ] Damage syncs correctly across clients
- [ ] Dead players respawn correctly
- [ ] Enemies spawn and move smoothly
- [ ] Connection/disconnection handled gracefully

## Performance Considerations

### Bandwidth per Player

**Per 0.1s tick:**

- Position: 8 bytes (Vector2)
- Velocity: 8 bytes (Vector2)
- Health: 4 bytes (float)
- Status flags: 1 byte

**Total: ~21 bytes/player/tick = ~1.68 Kbps per player**

For 4 players: ~6.7 Kbps (very efficient)

### CPU Cost

- **Server:** AI logic for all entities (monsters/players)
- **Client:** Interpolation + rendering only
- **Network:** 10 Hz updates (not 60 Hz)

## Future Enhancements

1. **Lag Compensation:** Track client ping and adjust reconciliation
2. **Interest Management:** Only sync nearby entities
3. **Voice Chat:** Integrate VOIP with networking
4. **Ability System:** Network special abilities with RPCs
5. **Loot Drops:** Server-spawned collectibles with client prediction
6. **Matchmaking:** Dynamic player joining mid-game

## References

- Unity Netcode for GameObjects Documentation
- GDC Talk: "Networking in Halo: Reach" (server reconciliation)
- Overwatch Blog: "Client-Side Prediction" article
