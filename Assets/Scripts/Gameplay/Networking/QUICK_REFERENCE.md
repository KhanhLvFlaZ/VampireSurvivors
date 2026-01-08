# Networking Quick Reference

## Core Classes at a Glance

### NetworkEntity (Abstract Base)

```csharp
// Read network state (all clients)
Vector2 pos = entity.GetNetworkPosition();
Vector2 vel = entity.GetNetworkVelocity();
float health = entity.GetNetworkHealth();

// Configuration
[SerializeField] float networkTickRate = 0.1f;     // 10 Hz
[SerializeField] bool useInterpolation = true;      // Client smoothing
```

### NetworkCharacter (Player)

```csharp
// Damage/Heal (works from any client, server validates)
character.TakeDamage(10f);
character.Heal(5f);

// Query status
if (character.IsCharacterAlive) { }
Character ch = character.GetCharacter();

// Reconciliation thresholds
POSITION_ERROR_THRESHOLD_HARD = 2f;    // Hard teleport
POSITION_ERROR_THRESHOLD_SOFT = 0.1f;   // Smooth lerp
```

### NetworkEnemy (Monster AI)

```csharp
// Query
bool alive = enemy.IsEnemyAlive;
Monster m = enemy.GetMonster();

// Damage
enemy.TakeDamage(20f);

// Server-only:
// AI runs automatically in UpdateAILogic()
// No manual control needed
```

### NetworkSpawner (Spawn Manager)

```csharp
// Player spawning
NetworkSpawner.Instance.SpawnPlayerForClient(clientId);
NetworkSpawner.Instance.DespawnPlayer(clientId);

// Enemy spawning
NetworkSpawner.Instance.SpawnEnemy(position, typeIndex);
NetworkSpawner.Instance.SpawnEnemy(position, typeIndex, ownerId);
NetworkSpawner.Instance.DespawnEnemy(enemyInstance);
NetworkSpawner.Instance.ClearAllEnemies();

// Queries
var players = NetworkSpawner.Instance.GetAllPlayers();
var enemies = NetworkSpawner.Instance.GetAllEnemies();
var player = NetworkSpawner.Instance.GetPlayerForClient(clientId);
int playerCount = NetworkSpawner.Instance.GetPlayerCount();
int enemyCount = NetworkSpawner.Instance.GetEnemyCount();
```

### CoopNetworkManager (Initialization)

```csharp
// Setup (in Start or OnEnable)
if (NetworkManager.Singleton.IsServer)
{
    CoopNetworkManager.Instance.InitializeServer();
}
else
{
    CoopNetworkManager.Instance.InitializeClient();
}

// Connection callbacks (auto-managed)
OnClientConnected() → SpawnPlayerForClient()
OnClientDisconnected() → DespawnPlayer()
```

### CoopOwnershipRegistry (ID Mapping)

```csharp
// Register player
registry.RegisterPlayer(playerId, networkClientId, gameObject);

// Lookup
var player = registry.GetPlayerByNetworkClientId(clientId);
var players = registry.GetAllPlayers();

// Unregister
registry.UnregisterPlayer(playerId);
```

## Common Tasks

### Spawn Player When Connected

```csharp
// In CoopNetworkManager.OnClientConnected()
NetworkSpawner.Instance.SpawnPlayerForClient(clientId);
```

### Spawn Enemy Wave

```csharp
for (int i = 0; i < 5; i++)
{
    Vector2 pos = Random.insideUnitCircle * 10f;
    int type = Random.Range(0, 2);
    NetworkSpawner.Instance.SpawnEnemy(pos, type);
}
```

### Apply Damage (Works Everywhere)

```csharp
// Owner client
networkCharacter.TakeDamage(damage);

// Non-owner client or server
// Also works! Server validates and syncs to all.
```

### Check All Entities

```csharp
var spawner = NetworkSpawner.Instance;
foreach (var player in spawner.GetAllPlayers())
{
    Debug.Log($"Player {player.Key}: {player.Value.name}");
}

foreach (var enemy in spawner.GetAllEnemies())
{
    if (enemy.TryGetComponent<NetworkEnemy>(out var netEnemy))
    {
        Debug.Log($"Enemy health: {netEnemy.GetNetworkHealth()}");
    }
}
```

### Setup Prefab (Checklist)

**Player:**

```
✓ NetworkObject (IsPlayerObject=true, AutoSpawn=false)
✓ NetworkCharacter script
✓ Character script
✓ Rigidbody2D (Gravity=0, FreezeRotationZ)
```

**Enemy:**

```
✓ NetworkObject (IsPlayerObject=false, AutoSpawn=false)
✓ NetworkEnemy script
✓ Monster script
✓ Rigidbody2D (Gravity=0, FreezeRotationZ)
```

## Network Flow (Simplified)

```
Owner: Input → Move (instant) → Sync every 0.1s
Server: Validate position → Update NetworkVariables
Others: Interpolate smoothly (0.1s between updates)
```

## Sync Rates & Thresholds

| Setting       | Value   | Effect                     |
| ------------- | ------- | -------------------------- |
| Network Tick  | 0.1s    | 10 Hz sync rate            |
| Hard Error    | 2.0m    | Hard teleport correction   |
| Soft Error    | 0.1m    | Smooth lerp correction     |
| Interpolation | Enabled | Smooth client-side visuals |

## Debugging

### Enable Logging

```csharp
NetworkManager.Singleton.LogLevel = LogLevel.Developer;
```

### Check Spawner Status

```csharp
var spawner = NetworkSpawner.Instance;
Debug.Log($"Players: {spawner.GetPlayerCount()}, Enemies: {spawner.GetEnemyCount()}");
```

### Monitor Position Sync

```csharp
Vector2 netPos = entity.GetNetworkPosition();
Vector2 localPos = entity.transform.position;
float error = Vector2.Distance(netPos, localPos);
Debug.Log($"Position sync error: {error}m");
```

## Files Reference

| File                      | Use For                  |
| ------------------------- | ------------------------ |
| NetworkEntity.cs          | Base class patterns      |
| NetworkCharacter.cs       | Player networking        |
| NetworkEnemy.cs           | Enemy networking         |
| NetworkSpawner.cs         | Spawn/despawn management |
| CoopNetworkManager.cs     | Network setup            |
| NETWORKING_GUIDE.md       | Architecture details     |
| IMPLEMENTATION_SUMMARY.md | System overview          |
| STATE_FLOW_DIAGRAMS.md    | Visual flows             |

## Key Concepts

**NetworkVariables**

- Auto-replicated state (position, velocity, health)
- Server writes, all clients read
- 10 Hz update rate
- ~21 bytes per entity per tick

**RPCs**

- Server validates received state
- Broadcasts corrections
- Used for rare events (damage, death)

**Client Prediction**

- Owner client applies input immediately
- No wait for server response
- Server reconciles position errors

**Interpolation**

- Non-owner clients smooth between updates
- 0.1s between ticks = smooth visuals
- Hides network latency

**Server Authority**

- Server is single source of truth
- Server validates all state changes
- Prevents cheating

---

**Quick Navigation:**

- Setup: → NETWORKING_GUIDE.md (Prefab section)
- Architecture: → IMPLEMENTATION_SUMMARY.md
- Visuals: → STATE_FLOW_DIAGRAMS.md
- Examples: → NetworkingSetupGuide.cs
