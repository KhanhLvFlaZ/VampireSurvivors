# Multi-Agent RL Implementation Quick Reference

## System Overview

```
Co-op Players (1-4)
    ↓ PlayerInputManager
CoopPlayerManager + CoopPlayerInput
    ↓ PlayerInput actions
Characters (with agent_id)
    ↓ RL decisions
RLSystem (Agent Group Manager)
    ├─ Tank Group (Agent ID 0, 2, 4...)
    ├─ DPS Group (Agent ID 1, 3, 5...)
    └─ Support Group (Agent ID 6, 7...)
    ↓ State Encoding
StateEncoder (90-float observation with teammate data + agent_id)
    ↓ Batch inference by role
InferenceBatcher (max 32 per role)
    ↓ Actions
CooperativeDecisionValidator
    ↓ Validated actions
RLMonsters execute actions with coordination rewards
```

## Key Files

| File                                                                                          | Purpose                           | Status                     |
| --------------------------------------------------------------------------------------------- | --------------------------------- | -------------------------- |
| [CoopPlayerManager.cs](Assets/Scripts/Gameplay/CoopPlayerManager.cs)                          | Multi-player lifecycle, spawn, UI | ✅ Complete                |
| [CoopPlayerInput.cs](Assets/Scripts/Gameplay/CoopPlayerInput.cs)                              | Input binding per-player          | ✅ Complete                |
| [CoopNetworkManager.cs](Assets/Scripts/Gameplay/Networking/CoopNetworkManager.cs)             | Server-authoritative setup        | ✅ Complete                |
| [CoopOwnershipRegistry.cs](Assets/Scripts/Gameplay/CoopOwnershipRegistry.cs)                  | Spawn ownership tracking          | ✅ Complete                |
| [RLSystem.cs](Assets/Scripts/RL/RLSystem.cs)                                                  | RL coordination hub               | ⚠️ Needs AgentGroupManager |
| [RLGameState.cs](Assets/Scripts/RL/Core/RLGameState.cs)                                       | Teammate state observation        | ✅ Complete                |
| [StateEncoder.cs](Assets/Scripts/RL/Core/StateEncoder.cs)                                     | 90-float observation encoding     | ✅ Complete                |
| [InferenceBatcher.cs](Assets/Scripts/RL/Integration/InferenceBatcher.cs)                      | Batch inference grouping          | ✅ Complete                |
| [RLSpawnLimiter.cs](Assets/Scripts/RL/Integration/RLSpawnLimiter.cs)                          | Dynamic agent limiting            | ✅ Complete                |
| [CoopRewardCalculator.cs](Assets/Scripts/RL/Core/CoopRewardCalculator.cs)                     | Cooperative rewards               | ✅ Complete                |
| **[AgentRole.cs](Assets/Scripts/RL/Core/AgentRole.cs)**                                       | **Role enum & config**            | **⚠️ To Create**           |
| **[AgentBatchGroup.cs](Assets/Scripts/RL/Core/AgentBatchGroup.cs)**                           | **Agent grouping container**      | **⚠️ To Create**           |
| **[AgentGroupManager.cs](Assets/Scripts/RL/Core/AgentGroupManager.cs)**                       | **Multi-role agent management**   | **⚠️ To Create**           |
| **[CooperativeDecisionValidator.cs](Assets/Scripts/RL/Core/CooperativeDecisionValidator.cs)** | **Team coordination validation**  | **⚠️ To Create**           |

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│          Co-op Survivors RL Multi-Agent System          │
└─────────────────────────────────────────────────────────┘

┌─ Input Layer ─────────────────────────────────────────┐
│  Player 1 (Controller)  →  PlayerInput  →  Character 1 │
│  Player 2 (Controller)  →  PlayerInput  →  Character 2 │
│  Player 3 (Controller)  →  PlayerInput  →  Character 3 │
│  Player 4 (Controller)  →  PlayerInput  →  Character 4 │
└────────────────────────────────────────────────────────┘
                           ↓
┌─ Game Logic Layer ────────────────────────────────────┐
│  CoopPlayerManager (spawn, camera, UI binding)        │
│  CoopOwnershipRegistry (player ID ↔ network client ID) │
│  EntityManager (track all entities)                   │
└────────────────────────────────────────────────────────┘
                           ↓
┌─ RL Decision Layer ───────────────────────────────────┐
│  RLSystem (orchestrator)                             │
│  ├─ AgentGroupManager                                 │
│  │  ├─ Tank Group (agents with role=Tank)            │
│  │  ├─ DPS Group (agents with role=DPS)              │
│  │  └─ Support Group (agents with role=Support)      │
│  ├─ StateEncoder (90-float obs with agent_id)        │
│  ├─ InferenceBatcher (batch by role group)           │
│  ├─ CooperativeDecisionValidator (team coordination)  │
│  └─ RLSpawnLimiter (dynamic agent limiting)          │
└────────────────────────────────────────────────────────┘
                           ↓
┌─ Monster AI Layer ────────────────────────────────────┐
│  Monster₁ (agent_id=0, role=Tank)                    │
│  Monster₂ (agent_id=1, role=DPS)                     │
│  Monster₃ (agent_id=2, role=DPS)                     │
│  ...                                                  │
│  Monster_N (agent_id=N-1, role=Support)             │
└────────────────────────────────────────────────────────┘
                           ↓
┌─ Reward & Training ───────────────────────────────────┐
│  CoopRewardCalculator (base + cooperative rewards)    │
│  RewardCalculator (final reward with co-op bonuses)   │
│  TrainingCoordinator (experience buffer & updates)    │
└────────────────────────────────────────────────────────┘
```

## Decision Flow (Per Frame)

```
Frame N:
├─ Game Update
│  ├─ Update player inputs (4 max)
│  ├─ Update monster positions
│  └─ Update game state
│
├─ RL Decision Cycle (every 0.1s)
│  │
│  ├─ State Encoding
│  │  ├─ For each monster with agent_id:
│  │  │  ├─ Build RLGameState (includes teammate data)
│  │  │  ├─ Encode to 90-float observation
│  │  │  └─ Store in AgentBatchGroup by role
│  │  │
│  │  └─ Result: 3 batch groups with grouped observations
│  │
│  ├─ Batch Inference
│  │  ├─ Tank Group (max 10 agents)
│  │  │  ├─ Run tank policy network (batch size N)
│  │  │  └─ Get actions for all tanks
│  │  │
│  │  ├─ DPS Group (max 20 agents)
│  │  │  ├─ Run DPS policy network (batch size M)
│  │  │  └─ Get actions for all DPS
│  │  │
│  │  └─ Support Group (max 10 agents)
│  │     ├─ Run support policy network (batch size K)
│  │     └─ Get actions for all support
│  │
│  ├─ Cooperation Validation
│  │  ├─ Update team focus target
│  │  ├─ For each agent:
│  │  │  ├─ Validate action for team coordination
│  │  │  ├─ Adjust if conflicts with team goal
│  │  │  └─ Store validated action
│  │  │
│  │  └─ Calculate team coordination metrics
│  │     ├─ Focus fire accuracy (% on target)
│  │     ├─ Formation score (spread quality)
│  │     ├─ Aggro distribution (tank holding)
│  │     └─ Coordination bonus multiplier
│  │
│  └─ Action Execution
│     ├─ For each monster:
│     │  ├─ Get validated action from its role group
│     │  ├─ Decode action to movement/attack
│     │  └─ Execute action in game
│     │
│     └─ Log coordination metrics
│
└─ Reward & Learning
   ├─ Calculate base rewards (damage, survival, distance)
   ├─ Add cooperative rewards (assist, aggro share, formation)
   ├─ Apply coordination bonus multiplier
   ├─ Store experience (s, a, r, s', done)
   └─ Periodic training on experience buffer
```

## Configuration Example

```csharp
// In RLSystem inspector:

[Multi-Agent Grouping]
Role Configs:
  [0] Role=Tank, MaxAgentsPerRole=10, DecisionPriority=0
  [1] Role=DPS, MaxAgentsPerRole=20, DecisionPriority=1
  [2] Role=Support, MaxAgentsPerRole=10, DecisionPriority=2

[Inference Cost Control]
Decision Interval Seconds: 0.1
Max Agents: 50
Target Latency Ms: 16
Latency Per Agent Ms: 0.3
Enable Dynamic Limit: true
Max Batch Size: 32
Enable Batching: true

[Monster RL Config] (per monster type)
Skeleton:
  Role Assignment: DPS
  Action Space: 8 directions + attack
Bat:
  Role Assignment: DPS
  Action Space: 8 directions + ranged attack
Zombie:
  Role Assignment: Tank
  Action Space: 8 directions + heavy attack
```

## Usage Examples

### Register an Agent

```csharp
// When spawning a monster
int agentId = RLSystem.Instance.RegisterAgent(AgentRole.DPS);

// Store on monster for later retrieval
monster.agentId = agentId;
monster.agentRole = AgentRole.DPS;
```

### Get Decision

```csharp
// During monster update
if (Time.time - lastDecisionTime >= 0.1f)  // Decision interval
{
    // Encode observation
    var state = environment.BuildGameState(monster, agentId);
    var observation = encoder.EncodeState(state);

    // Store in group
    var group = agentGroupManager.GetGroup(monster.agentRole);
    group.StoreObservation(agentId, observation);

    // Later, retrieve validated action
    int? action = agentGroupManager.GetAction(agentId);
    if (action.HasValue)
    {
        monster.ExecuteAction(action.Value);
    }
}
```

### Monitor Coordination

```csharp
// In debug/telemetry
var groupManager = RLSystem.Instance.agentGroupManager;
Debug.Log(groupManager.ToString());

// Output:
// AgentGroupManager (Total agents: 35)
//   AgentBatchGroup(Tank, 8/10 agents)
//   AgentBatchGroup(DPS, 20/20 agents)
//   AgentBatchGroup(Support, 7/10 agents)
```

## Performance Targets

| Metric               | Target                  | Current  | Status         |
| -------------------- | ----------------------- | -------- | -------------- |
| Observation encoding | <1ms (50 agents)        | Unknown  | Need benchmark |
| Batch inference      | <5ms per role group     | Unknown  | Need benchmark |
| Decision latency     | <16ms (1 frame @ 60fps) | Unknown  | Need tuning    |
| Memory per agent     | <1KB                    | ~2-3KB   | Acceptable     |
| Max agents supported | 50+                     | 50 limit | Configured     |

## Testing Checklist

- [ ] Unit test AgentBatchGroup with variable agent counts
- [ ] Unit test AgentGroupManager registration/unregistration
- [ ] Integration test with 4 players + 30 monsters
- [ ] Performance test: batch inference latency with 20/50 agents
- [ ] Coordination test: verify focus fire bonus applied
- [ ] Network test: verify agent_id consistency across clients
- [ ] Stress test: dynamic spawn/despawn with agent recycling

## Troubleshooting

| Problem               | Cause                      | Solution                              |
| --------------------- | -------------------------- | ------------------------------------- |
| Agents not batched    | Role group not initialized | Verify RoleConfigs in inspector       |
| Decisions delayed     | Batch timeout too high     | Reduce batchTimeoutMs                 |
| Memory spike          | Too many agents            | Lower maxRLAgents or maxAgentsPerRole |
| Inconsistent behavior | Shared policy confusion    | Verify agent role assignment          |
| Poor coordination     | Validator too strict       | Lower focusFireThreshold              |

## Next Steps (Priority Order)

1. **Create Role/Batching Classes** (2-3 hours)

   - AgentRole.cs
   - AgentBatchGroup.cs
   - AgentGroupManager.cs
   - Integrate with RLSystem

2. **Create Cooperative Validator** (2-3 hours)

   - CooperativeDecisionValidator.cs
   - Integrate with RLEnvironment
   - Add validation to decision loop

3. **Add Role-based Reward Multipliers** (1 hour)

   - Tank: extra for holding aggro
   - DPS: extra for focus fire
   - Support: extra for protecting teammates

4. **Performance Tuning** (2-4 hours)

   - Benchmark actual latencies
   - Tune batch size and timeout
   - Optimize observation encoding (if needed)

5. **Testing & Validation** (3-5 hours)
   - Unit tests for all new classes
   - Integration tests with max players/monsters
   - Coordination metrics monitoring

**Total Estimated Time**: 10-18 hours of development

## Key Metrics to Monitor

```csharp
// In PerformanceMonitor.cs
public class MultiAgentMetrics
{
    public int totalAgents;                 // Current active agents
    public int agentsPerRole;               // Distribution: Tank/DPS/Support
    public float avgObservationEncodeTime;  // ms per 50 agents
    public float avgBatchInferenceTime;     // ms per role group
    public float avgCoordinationBonus;      // Multiplier (1.0 = no bonus)
    public float focusFireAccuracy;         // % on focus target
    public float formationMaintenanceScore; // 0-1 (0=scattered, 1=optimal)
    public float aggroDistributionFairness; // 0-1 (how even is damage spread)
    public int frameTotalRLTime;            // Total ms for all RL work
}
```

## References

- [MULTI_AGENT_IMPLEMENTATION_AUDIT.md](MULTI_AGENT_IMPLEMENTATION_AUDIT.md) - Full audit report
- [AGENT_ID_BATCHING_GUIDE.md](AGENT_ID_BATCHING_GUIDE.md) - Agent grouping implementation
- [COOPERATIVE_DECISION_VALIDATION_GUIDE.md](COOPERATIVE_DECISION_VALIDATION_GUIDE.md) - Team coordination
- [COOP_REWARD_SYSTEM.md](COOP_REWARD_SYSTEM.md) - Cooperative rewards
- [INFERENCE_COST_CONTROL.md](INFERENCE_COST_CONTROL.md) - Performance tuning
- [architecture-methodology.md](../../docs/architecture-methodology.md) - System architecture
