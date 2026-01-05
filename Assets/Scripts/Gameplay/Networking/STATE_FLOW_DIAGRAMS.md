# Network State Flow Diagrams

## Architecture Overview

```
MULTI-PLAYER CO-OP NETWORKING SYSTEM
====================================

                    ┌─────────────────────────────────────────┐
                    │   UNITY NETCODE FOR GAMEOBJECTS (NGO)   │
                    │  - NetworkManager                       │
                    │  - NetworkObject spawning               │
                    │  - NetworkVariable replication          │
                    │  - RPC delivery                         │
                    └─────────────────────────────────────────┘
                                    △
                                    │
                ┌───────────────────┼───────────────────┐
                │                   │                   │
                ▼                   ▼                   ▼
        ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
        │ NetworkChar. │    │ NetworkEnemy │    │ NetworkSp.   │
        │ (Player)     │    │ (Monster)    │    │ (Spawner)    │
        │              │    │              │    │              │
        │ -Position    │    │ -Position    │    │ -Spawn Player│
        │ -Velocity    │    │ -Velocity    │    │ -Spawn Enemy │
        │ -Health      │    │ -Health      │    │ -Despawn     │
        │ -IsAttacking │    │ -IsAttacking │    │ -Track all   │
        └──────────────┘    └──────────────┘    └──────────────┘
                △                   △                   △
                │                   │                   │
                └───────────────────┼───────────────────┘
                                    │
                            ┌───────┴────────┐
                            │                │
                    ┌───────▼────────┐   ┌──▼──────────┐
                    │ NetworkEntity  │   │ Spawner     │
                    │ (Abstract Base)│   │ Mgmt Layer  │
                    │                │   │             │
                    │ -networkPos    │   │ -Players{}  │
                    │ -networkVel    │   │ -Enemies[]  │
                    │ -networkHealth │   │ -Registry   │
                    │ 0.1s sync tick │   │             │
                    └────────────────┘   └─────────────┘
                            △                   △
                            │                   │
                    ┌───────┴─────────────────┬─┘
                    │                         │
                 SERVER                    CLIENTS
              (Game Logic)              (Prediction)
```

## Player Movement Synchronization Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       OWNER CLIENT SIDE                                 │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Frame Loop                                                       │  │
│  │ 1. Input.GetAxis() or InputSystem action                        │  │
│  │ 2. Character.Move(input direction)        ← IMMEDIATE           │  │
│  │ 3. rb.position/velocity update            ← LOCAL ONLY          │  │
│  │                                                                  │  │
│  │ Every 0.1s (networkTickRate):                                  │  │
│  │ 4. SyncStateToServerServerRpc()           ← NETWORK RPC       │  │
│  │    └─ position, velocity, health          ← CLIENT PREDICTION  │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                              │ RPC call                                 │
│                              └───────────────┐                          │
│                                              ▼                          │
└─────────────────────────────────────────────────────────────────────────┘
                                       │
                                       │
┌──────────────────────────────────────┴──────────────────────────────────┐
│                         SERVER SIDE                                     │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Received RPC: SyncStateToServerServerRpc()                      │  │
│  │                                                                  │  │
│  │ Step 1: Validate Position Error                                │  │
│  │   posError = Distance(clientPos, serverPos)                    │  │
│  │                                                                  │  │
│  │   If error > 2.0m:                                             │  │
│  │     ├─ HARD CORRECTION (teleport)                             │  │
│  │     └─ rb.position = clientPos                                │  │
│  │                                                                  │  │
│  │   Else if error > 0.1m:                                        │  │
│  │     ├─ SOFT CORRECTION (lerp)                                 │  │
│  │     └─ rb.position = Lerp(current, client, 0.5)               │  │
│  │                                                                  │  │
│  │   Else:                                                         │  │
│  │     └─ ACCEPT (no correction)                                 │  │
│  │                                                                  │  │
│  │ Step 2: Update NetworkVariables                               │  │
│  │   networkPosition.Value = rb.position                         │  │
│  │   networkVelocity.Value = rb.linearVelocity                   │  │
│  │   networkHealth.Value = character.CurrentHealth               │  │
│  │                                                                  │  │
│  │ Step 3: Broadcast Correction                                  │  │
│  │   UpdateClientStateClientRpc(correctedPos, correctedVel)      │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                              │ ClientRpc call                           │
│                              └────────────┬────────────┐                │
│                                           ▼            ▼                │
└─────────────────────────────────────────────────────────────────────────┘
                    │                                 │
        ┌───────────▼─────────────┐       ┌──────────▼───────────┐
        │  OWNER CLIENT (again)   │       │  OTHER CLIENTS      │
        │  ┌───────────────────┐  │       │  ┌─────────────────┐ │
        │  │ ClientRpc Received│  │       │  │ ClientRpc Rcv'd │ │
        │  │                   │  │       │  │                 │ │
        │  │ If correction big │  │       │  │ Set targets:    │ │
        │  │ (> 0.5m error):   │  │       │  │                 │ │
        │  │   lerp towards    │  │       │  │ startPos = cur  │ │
        │  │   server pos      │  │       │  │ targetPos = new │ │
        │  │ (reconciliation)  │  │       │  │                 │ │
        │  │                   │  │       │  │ Interpolate:    │ │
        │  │ Next frame sees   │  │       │  │ pos = Lerp(...) │ │
        │  │ corrected state   │  │       │  │ vel = network   │ │
        │  └───────────────────┘  │       │  │ (smooth anim)   │ │
        │                         │       │  └─────────────────┘ │
        └─────────────────────────┘       └─────────────────────┘
                  RESULT:
        Owner sees immediate response,
        Server validates movement,
        Other clients see smooth sync
```

## NetworkVariable Auto-Replication

```
┌────────────────────────────────────────────────────────────┐
│            SERVER (Authority)                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Every 0.1s tick:                                     │  │
│  │                                                      │  │
│  │ networkPosition.Value = rb.position                 │  │
│  │ networkVelocity.Value = rb.linearVelocity           │  │
│  │ networkHealth.Value = character.CurrentHealth       │  │
│  │                                                      │  │
│  │ Marked dirty → NetworkVariable detects change       │  │
│  │             → Queues for replication                │  │
│  └──────────────────────────────────────────────────────┘  │
│                       ▼                                     │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Network Tick (10 Hz):                                │  │
│  │ - Serialize changed NetworkVariables                │  │
│  │ - Package update message (~21 bytes per entity)     │  │
│  │ - Queue for transmission                            │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────┘
                       ▼
              ┌────────────────┐
              │ Network Stream │
              │   (Over Wire)  │
              └────────────────┘
                       ▼
  ┌────────────────────────────────────────────────────────────┐
  │ ALL CLIENTS (Read-Only)                                    │
  │  ┌──────────────────────────────────────────────────────┐  │
  │  │ Network Tick:                                        │  │
  │  │ - Deserialize NetworkVariable updates               │  │
  │  │ - Update local cache:                               │  │
  │  │   networkPos.Value ← server value                   │  │
  │  │   networkVel.Value ← server value                   │  │
  │  │   networkHealth.Value ← server value                │  │
  │  │                                                      │  │
  │  │ - Invoke OnNetworkVariableUpdate() callbacks        │  │
  │  └──────────────────────────────────────────────────────┘  │
  │                       ▼                                    │
  │  ┌──────────────────────────────────────────────────────┐  │
  │  │ Client-Side Interpolation (FixedUpdate):            │  │
  │  │                                                      │  │
  │  │ For non-owned entities:                             │  │
  │  │   alpha = elapsed / networkTickRate                 │  │
  │  │   pos = Lerp(lastPos, networkPos, alpha)            │  │
  │  │   rb.position = pos                                 │  │
  │  │   rb.velocity = networkVel                          │  │
  │  │                                                      │  │
  │  │ For owned entity:                                   │  │
  │  │   (already moving locally, just use network state   │  │
  │  │    for reference if needed)                         │  │
  │  └──────────────────────────────────────────────────────┘  │
  └────────────────────────────────────────────────────────────┘
```

## Enemy AI & Interpolation

```
┌──────────────────────────────────────────────────────────────┐
│                    SERVER (AI Brain)                         │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ Every Frame (60 Hz):                                   │  │
│  │                                                        │  │
│  │ 1. FindNearestPlayer()                                │  │
│  │    └─ Search all NetworkCharacter instances          │  │
│  │       Distance = 20m detection range                 │  │
│  │                                                        │  │
│  │ 2. AI Decision:                                       │  │
│  │    if target exists:                                 │  │
│  │      if distance > attackRange:                      │  │
│  │        monster.Move(directionToTarget)               │  │
│  │      else:                                           │  │
│  │        monster.Attack(directionToTarget)             │  │
│  │    else:                                             │  │
│  │      monster.Move(randomWanderDir)                   │  │
│  │                                                        │  │
│  │ 3. Physics Update:                                   │  │
│  │    rb.position = new position                        │  │
│  │    rb.velocity = new velocity                        │  │
│  └────────────────────────────────────────────────────────┘  │
│                        ▼                                     │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ Every 0.1s (Network Tick):                            │  │
│  │                                                        │  │
│  │ networkPosition.Value = rb.position        ← Replicate │  │
│  │ networkVelocity.Value = rb.velocity                   │  │
│  │ networkIsAttacking.Value = monster.IsAttacking        │  │
│  │ networkHealth.Value = monster.CurrentHealth           │  │
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
                          ▼
                    ALL CLIENTS
  ┌──────────────────────────────────────────────────────────┐
  │  ┌─────────────────────────────────────────────────────┐ │
  │  │ Every Frame (60 Hz):                                │ │
  │  │                                                     │ │
  │  │ INTERPOLATION (smooth between network updates):    │ │
  │  │                                                     │ │
  │  │ elapsedTime += deltaTime                           │ │
  │  │ alpha = elapsedTime / networkTickRate (0.1s)       │ │
  │  │                                                     │ │
  │  │ if alpha >= 1.0:                                   │ │
  │  │   alpha = 1.0                                      │ │
  │  │   elapsedTime = 0                                  │ │
  │  │   lastNetPos = currentNetPos                       │ │
  │  │                                                     │ │
  │  │ smoothPos = Lerp(lastNetPos, networkPos, alpha)    │ │
  │  │ rb.position = smoothPos                            │ │
  │  │ rb.velocity = networkVelocity (from server)        │ │
  │  │                                                     │ │
  │  │ animator.SetMovement(networkVelocity)              │ │
  │  │ animator.SetAttacking(networkIsAttacking)          │ │
  │  └─────────────────────────────────────────────────────┘ │
  │                                                          │
  │  VISUAL RESULT:                                         │
  │  - Enemy moves smoothly                                 │
  │  - No jittering (even with 10 Hz updates)              │
  │  - Attack animations sync with server                  │
  │  - All clients see same visual behavior                │
  └──────────────────────────────────────────────────────────┘
```

## Spawn/Despawn Lifecycle

```
CONNECTION EVENT
      ▼
┌─────────────────────────────────┐
│ OnClientConnected(clientId)     │
│ (Server only)                   │
└─────────────────────────────────┘
      ▼
┌─────────────────────────────────────────────────────┐
│ NetworkSpawner.SpawnPlayerForClient(clientId)      │
│ ┌──────────────────────────────────────────────┐   │
│ │ 1. Calculate spawn position                  │   │
│ │    - Round-robin based on clientId           │   │
│ │    - 90° spacing for up to 4 players         │   │
│ │                                               │   │
│ │ 2. Instantiate player prefab                 │   │
│ │    new(position, rotation)                   │   │
│ │                                               │   │
│ │ 3. Network ownership assignment              │   │
│ │    SpawnAsPlayerObject(clientId)             │   │
│ │    ├─ Sets OwnerClientId                     │   │
│ │    ├─ Marks IsPlayerObject = true            │   │
│ │    └─ Replicates to all clients              │   │
│ │                                               │   │
│ │ 4. Register in tracking                      │   │
│ │    spawnedPlayers[clientId] = instance       │   │
│ └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
      ▼
┌─────────────────────────────────────────────────────┐
│ ALL CLIENTS: Player object appears                 │
│ ┌──────────────────────────────────────────────┐   │
│ │ - NetworkObject spawned locally              │   │
│ │ - Character component initialized            │   │
│ │ - Owner can input                            │   │
│ │ - Non-owners see replicated position/state   │   │
│ └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
      ▼
   GAMEPLAY
      ▼
┌─────────────────────────────────┐
│ OnClientDisconnected(clientId)  │
│ (Server only)                   │
└─────────────────────────────────┘
      ▼
┌─────────────────────────────────────────────────────┐
│ NetworkSpawner.DespawnPlayer(clientId)             │
│ ┌──────────────────────────────────────────────┐   │
│ │ 1. Find player instance                      │   │
│ │    instance = spawnedPlayers[clientId]       │   │
│ │                                               │   │
│ │ 2. Despawn network object                    │   │
│ │    instance.Despawn()                        │   │
│ │    └─ Notifies all clients                   │   │
│ │                                               │   │
│ │ 3. Destroy game object                       │   │
│ │    Destroy(instance.gameObject)              │   │
│ │                                               │   │
│ │ 4. Unregister from tracking                  │   │
│ │    spawnedPlayers.Remove(clientId)           │   │
│ └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
      ▼
┌─────────────────────────────────────────────────────┐
│ ALL CLIENTS: Player object removed                 │
│ └─────────────────────────────────────────────────┘
```

## State Consistency Timeline

```
TIME AXIS →

CLIENT                  t=0ms                t=100ms            t=200ms
┌─────────────┐                          ┌─────────────┐
│ Input:      │ ← Move Up                │ Input:      │ ← Still Moving
│ Dir=(0,1)   │                          │ Dir=(0,1)   │
└─────────────┘                          └─────────────┘
      ↓                                        ↓
┌─────────────────────────────────────────────────────┐
│ LOCAL POSITION UPDATES (60 Hz)                      │
│ [0: (0,0)][1: (0,0.5)][2: (0,1)][3: (0,1.5)]...   │
│ ^ Owner sees movement immediately                   │
└─────────────────────────────────────────────────────┘
      │
      │ RPC: SyncStateToServerServerRpc
      │ at t=100ms
      ▼
┌─────────────────────────────────────┐
│ SERVER at t=100ms                   │
│ ┌─────────────────────────────────┐ │
│ │ Receive: pos=(0,~1.67)          │ │
│ │ Expected (based on physics): ~1.67│
│ │ Error: ~0m (good, accept)       │ │
│ │                                   │
│ │ Update NetworkVariables:         │
│ │ networkPos = (0, 1.67)           │
│ │ networkVel = (0, 1)              │
│ └─────────────────────────────────┘ │
│       ↓                              │
│ Broadcast ClientRpc to all clients  │
└─────────────────────────────────────┘
      ↓
      │ All clients receive update
      │
┌─────────────────────────────────────────────┐
│ OTHER CLIENTS (start interpolation)         │
│ LastNetPos = (0, 1.67)                      │
│ TargetNetPos = (0, 1.67)                    │
│                                              │
│ t=100ms: InterpolateToState() = (0, 1.67)   │
│ t=133ms: InterpolateToState() = (0, 1.78)   │
│ t=166ms: InterpolateToState() = (0, 1.89)   │
│ t=200ms: InterpolateToState() = (0, 2.0)    │
│          ^ Smoothly reaches target           │
└─────────────────────────────────────────────┘

STATE AT t=200ms:
- All clients agree on position (0, 2.0)
- Movement appears smooth everywhere
- Network update latency hidden by interpolation
```

## Damage Event Flow

```
ATTACKER CLIENT                    SERVER                    DEFENDER CLIENT
(Any client)
     │                                                              │
     │ Player takes damage (networking context)                     │
     ├─ if IsServer: apply locally                                 │
     └─ else: call ReportDamageServerRpc()                        │
                       │ RPC call                                   │
                       ▼                                            │
                  ┌─────────────────────────────┐                  │
                  │ ReportDamageServerRpc       │                  │
                  │ ┌───────────────────────────┐│                 │
                  │ │ 1. character.TakeDamage() ││                 │
                  │ │ 2. Update networkHealth   ││                 │
                  │ │ 3. Check if alive:        ││                 │
                  │ │    if !IsAlive:           ││                 │
                  │ │      OnCharacterDeadRpc() ││                 │
                  │ └───────────────────────────┘│                 │
                  │ (Server is authoritative)    │                 │
                  └─────────────────────────────┘                  │
                       │                                            │
         ┌─────────────┼─────────────┐                             │
         │ ClientRpc   │             │                             │
         ▼             ▼             ▼                             │
     OWNER        OTHER CLIENTS    DEFENDER                       │
     (if owner)   (all non-owners)                                │
                                                                   │
   Ignored        Receive          ← SAME RPC
   (server was    networkHealth
    already       value updated
    authority)
                  UpdateClientStateClientRpc()
                  health = networkHealth.Value

   ↓ BOTH OWNER AND NON-OWNERS:
   All see health bar decrease
   All see same health value
   All see death animation at same time
```

---

**Key Takeaways:**

1. **Server writes all state** - source of truth
2. **Clients predict locally** - feel responsive
3. **Server reconciles** - prevents cheating
4. **NetworkVariables auto-replicate** - efficient bandwidth
5. **Interpolation hides latency** - smooth visuals
