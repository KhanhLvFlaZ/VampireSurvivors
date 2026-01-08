# Inference Cost Control System

## Overview

The Inference Cost Control System manages computational expenses of RL agent inference through two main mechanisms:

1. **Inference Batching**: Groups multiple agent observations for efficient batch processing
2. **Spawn Limiting**: Dynamically controls RL agent population based on performance budget

## Architecture

### Components

#### 1. InferenceBatcher

**Purpose:** Batch multiple agent observations for efficient neural network inference

**Key Features:**

- Configurable batch size (default: 32 agents)
- Timeout-based processing (default: 5ms) to prevent stale observations
- Automatic statistics tracking (avg batch size, inference time)
- Zero-overhead when disabled

**Configuration:**

```csharp
[SerializeField] private int maxBatchSize = 32;
[SerializeField] private float batchTimeoutMs = 5f;
[SerializeField] private bool enableBatching = true;
```

**Processing Logic:**

```
Queue observations from agents
↓
Wait until: batch full OR timeout reached OR force process
↓
Process all pending observations in single batch
↓
Return actions to agents
↓
Update statistics
```

**Performance Impact:**

- Reduces per-agent inference cost by ~60-80% (typical)
- Small latency increase (<5ms) for batching delay
- Best for: 10+ active agents

#### 2. RLSpawnLimiter

**Purpose:** Control RL agent spawning to maintain target frame time

**Key Features:**

- Dynamic capacity adjustment based on actual performance
- Latency-based spawn decisions
- Scripted behavior fallback when at capacity
- Exponential moving average for stable latency estimates

**Configuration:**

```csharp
[SerializeField] private int maxRLAgents = 50;
[SerializeField] private float targetLatencyMs = 16f;
[SerializeField] private float latencyPerAgentMs = 0.3f;
[SerializeField] private bool enableDynamicLimit = true;
```

**Decision Flow:**

```
New monster spawn requested
↓
Check: activeRLAgents < maxRLAgents?
  ├─ No → Use scripted behavior (fallback)
  └─ Yes → Check: projected latency < target?
      ├─ No → Use scripted behavior (fallback)
      └─ Yes → Spawn RL agent
↓
Update active agent count
↓
Monitor actual latency
↓
Adjust maxRLAgents dynamically (every 2s)
```

**Dynamic Adjustment:**

```csharp
// Calculate actual cost per agent
actualLatencyPerAgent = avgLatency / activeAgents

// Estimate theoretical max within budget
theoreticalMax = targetLatency / actualLatencyPerAgent

// Apply safety margin (80%)
safeMax = theoreticalMax * 0.8

// Gradual adjustment (±5 agents per update)
maxRLAgents = clamp(maxRLAgents + delta, 10, 200)
```

### Integration with RLSystem

**Initialization:**

```csharp
private void InitializeInferenceCostControl()
{
    // Initialize batching
    if (enableBatching)
        inferenceBatcher = new InferenceBatcher(maxBatchSize, batchTimeoutMs);

    // Initialize spawn limiter
    spawnLimiter = new RLSpawnLimiter(
        maxRLAgents, targetLatencyMs, latencyPerAgentMs, enableDynamicLimit
    );
}
```

**Spawn Decision:**

```csharp
public ILearningAgent CreateAgentForMonster(MonsterType monsterType)
{
    // Check capacity before spawning RL agent
    if (spawnLimiter != null && !spawnLimiter.CanSpawnRLAgent())
    {
        spawnLimiter.RegisterScriptedFallback();
        return null; // Caller uses scripted behavior
    }

    // Create RL agent
    var agent = CreateRLAgent(monsterType);
    spawnLimiter?.RegisterRLAgent();
    return agent;
}
```

**Update Loop:**

```csharp
void Update()
{
    // Process batched inferences
    if (enableBatching && inferenceBatcher != null)
    {
        int processed = inferenceBatcher.ProcessBatch();
        spawnLimiter?.UpdateLatency(batchTime);
    }

    // Update agents
    trainingCoordinator?.UpdateAgents();

    // Update spawn limiter with frame time
    spawnLimiter?.UpdateLatency(totalProcessingTime);
}
```

## Usage Examples

### Example 1: Basic Setup

```csharp
// In Unity Inspector:
// - Enable Batching: ✓
// - Max Batch Size: 32
// - Batch Timeout Ms: 5
// - Max RL Agents: 50
// - Target Latency Ms: 16
// - Enable Dynamic Limit: ✓

// In code:
RLSystem rlSystem = GetComponent<RLSystem>();

// Check if can spawn RL agent
if (rlSystem.CanSpawnRLAgent())
{
    var agent = rlSystem.CreateAgentForMonster(MonsterType.Melee);
    // Agent created successfully
}
else
{
    // Use scripted behavior fallback
    UseScriptedAI(monster);
}
```

### Example 2: Monitoring Performance

```csharp
// Get spawn limiter stats
var limiterStats = rlSystem.GetSpawnLimiterStats();
Debug.Log($"RL Agents: {limiterStats.activeRLAgents}/{limiterStats.maxRLAgents}");
Debug.Log($"Capacity: {limiterStats.capacityUtilization:P0}");
Debug.Log($"Avg Latency: {limiterStats.averageLatencyMs}ms");
Debug.Log($"Scripted Fallbacks: {limiterStats.scriptedFallbacks}");

// Get batching stats
var batchStats = rlSystem.GetBatchingStats();
Debug.Log($"Pending Requests: {batchStats.pendingRequests}");
Debug.Log($"Avg Batch Size: {batchStats.averageBatchSize}");
Debug.Log($"Avg Inference Time: {batchStats.averageInferenceTimeMs}ms");

// Comprehensive status
Debug.Log(rlSystem.GetInferenceCostStatus());
// Output: "RL Agents: 35/50 (70%), Fallbacks: 12, Latency: 10.2ms (avg: 9.8ms), Batching: 3 pending, avg batch: 28.5"
```

### Example 3: Custom Spawn Logic

```csharp
// Custom monster spawner with RL capacity awareness
public class SmartMonsterSpawner : MonoBehaviour
{
    [SerializeField] private RLSystem rlSystem;
    [SerializeField] private float preferredRLRatio = 0.7f; // 70% RL, 30% scripted

    private int totalSpawned = 0;
    private int rlSpawned = 0;

    public void SpawnMonster(MonsterType type)
    {
        bool shouldUseRL = ShouldSpawnAsRL();

        if (shouldUseRL && rlSystem.CanSpawnRLAgent())
        {
            var agent = rlSystem.CreateAgentForMonster(type);
            if (agent != null)
            {
                rlSpawned++;
                // Agent spawned successfully
            }
            else
            {
                // Fallback to scripted
                SpawnScriptedMonster(type);
            }
        }
        else
        {
            // Use scripted behavior
            SpawnScriptedMonster(type);
        }

        totalSpawned++;
    }

    private bool ShouldSpawnAsRL()
    {
        // Check if we're below preferred RL ratio
        float currentRLRatio = totalSpawned > 0 ? (float)rlSpawned / totalSpawned : 0f;
        return currentRLRatio < preferredRLRatio;
    }
}
```

### Example 4: Dynamic Difficulty Scaling

```csharp
// Adjust difficulty based on RL agent capacity
public class DynamicDifficulty : MonoBehaviour
{
    [SerializeField] private RLSystem rlSystem;

    public float GetDifficultyMultiplier()
    {
        var stats = rlSystem.GetSpawnLimiterStats();

        // Reduce difficulty when at capacity (more scripted agents = easier)
        float capacityUtil = stats.capacityUtilization;

        if (capacityUtil < 0.5f)
            return 1.0f; // Normal difficulty
        else if (capacityUtil < 0.8f)
            return 0.9f; // Slightly easier
        else
            return 0.75f; // Easier (more scripted enemies)
    }

    public int GetMaxSpawnCount()
    {
        var stats = rlSystem.GetSpawnLimiterStats();

        // Scale spawn count based on available capacity
        int baseSpawnCount = 100;

        if (stats.canSpawnMore)
            return baseSpawnCount;
        else
            return Mathf.RoundToInt(baseSpawnCount * 0.7f); // Reduce spawns
    }
}
```

## Performance Tuning

### Optimal Configuration by Scale

**Small Scale (1-20 agents):**

```csharp
maxRLAgents = 20
targetLatencyMs = 16f
latencyPerAgentMs = 0.3f
enableDynamicLimit = false
maxBatchSize = 16
batchTimeoutMs = 10f
enableBatching = false // Overhead not worth it
```

**Medium Scale (20-50 agents):**

```csharp
maxRLAgents = 50
targetLatencyMs = 16f
latencyPerAgentMs = 0.3f
enableDynamicLimit = true
maxBatchSize = 32
batchTimeoutMs = 5f
enableBatching = true
```

**Large Scale (50-200 agents):**

```csharp
maxRLAgents = 100
targetLatencyMs = 16f
latencyPerAgentMs = 0.2f // Optimized network
enableDynamicLimit = true
maxBatchSize = 64
batchTimeoutMs = 3f
enableBatching = true
```

### Tuning Guidelines

**If frame time too high:**

1. Reduce `maxRLAgents` (immediate effect)
2. Decrease `targetLatencyMs` (more aggressive limiting)
3. Reduce `maxBatchSize` (lower latency, but less efficient)
4. Increase `decisionIntervalSeconds` (less frequent decisions)

**If too many scripted fallbacks:**

1. Increase `maxRLAgents` (higher capacity)
2. Increase `targetLatencyMs` (looser budget)
3. Optimize neural network (reduce `latencyPerAgentMs`)
4. Enable `enableDynamicLimit` (adaptive adjustment)

**If batching inefficient:**

1. Increase `maxBatchSize` (fewer batches)
2. Decrease `batchTimeoutMs` (faster processing)
3. Check `averageBatchSize` - should be >50% of `maxBatchSize`
4. Consider disabling if <10 agents active

## Monitoring & Debugging

### Key Metrics

**Spawn Limiter:**

- `activeRLAgents`: Current RL agent count
- `maxRLAgents`: Current capacity limit
- `scriptedFallbacks`: Fallback count (high = at capacity often)
- `capacityUtilization`: Percentage of capacity used
- `averageLatencyMs`: Smoothed latency estimate
- `canSpawnMore`: Boolean spawn permission

**Inference Batcher:**

- `pendingRequests`: Queued observations waiting for batch
- `averageBatchSize`: How full batches are (efficiency metric)
- `averageInferenceTimeMs`: Time to process one batch
- `totalBatches`: Total batches processed (lifetime)

### Debug Logging

Enable detailed logging:

```csharp
// Add to RLSystem.Update() for frame-by-frame monitoring
if (Time.frameCount % 60 == 0) // Every 60 frames
{
    Debug.Log($"[RL Cost Control] {GetInferenceCostStatus()}");
}

// Add to RLSystem.CreateAgentForMonster() for spawn tracking
Debug.Log($"[RL Spawn] {monsterType}: " +
         $"{(canSpawn ? "RL" : "SCRIPTED")} " +
         $"({spawnLimiter.ActiveRLAgentCount}/{spawnLimiter.MaxRLAgents})");
```

### Performance Profiler Integration

```csharp
using Unity.Profiling;

public class RLSystem : MonoBehaviour
{
    private ProfilerMarker batchProcessMarker = new ProfilerMarker("RL.BatchProcess");
    private ProfilerMarker agentUpdateMarker = new ProfilerMarker("RL.AgentUpdate");

    void Update()
    {
        using (batchProcessMarker.Auto())
        {
            inferenceBatcher?.ProcessBatch();
        }

        using (agentUpdateMarker.Auto())
        {
            trainingCoordinator?.UpdateAgents();
        }
    }
}
```

## Limitations & Considerations

### Current Limitations

1. **Batching Latency**: Small delay (up to `batchTimeoutMs`) for batching

   - **Mitigation**: Keep timeout low (<10ms), acceptable for most games

2. **Scripted Fallback Quality**: Fallback AI must be competent

   - **Mitigation**: Ensure scripted behavior is reasonable baseline

3. **Dynamic Adjustment Lag**: 2-second intervals for limit adjustments

   - **Mitigation**: Start with conservative limits, let system stabilize

4. **No Per-MonsterType Limits**: Global limit applies to all types
   - **Future**: Add per-type spawn budgets

### Best Practices

1. **Start Conservative**: Begin with low `maxRLAgents`, let dynamic adjustment increase
2. **Test Fallbacks**: Ensure scripted behavior is acceptable quality
3. **Monitor Ratios**: Track RL vs scripted ratio, adjust if heavily skewed
4. **Profile Regularly**: Check actual `latencyPerAgentMs` with profiler
5. **Gradual Rollout**: Start with RL on fewer monster types, expand gradually

## Future Enhancements

### Planned Features

1. **Per-MonsterType Budgets**: Different limits for different monster types

   ```csharp
   Dictionary<MonsterType, int> maxAgentsByType = new()
   {
       { MonsterType.Boss, 10 },     // Always RL
       { MonsterType.Elite, 20 },    // High priority
       { MonsterType.Common, 50 }    // Best effort
   };
   ```

2. **Priority-Based Spawning**: Prioritize important agents (bosses, elites)

   ```csharp
   enum SpawnPriority { Low, Medium, High, Critical }
   bool CanSpawnRL(MonsterType type, SpawnPriority priority);
   ```

3. **GPU Inference Support**: Offload inference to GPU for better batching

   ```csharp
   [SerializeField] private bool useGPUInference = true;
   [SerializeField] private int gpuBatchSize = 128;
   ```

4. **Adaptive Decision Frequency**: Vary decision rate by agent importance

   ```csharp
   float GetDecisionInterval(Monster monster)
   {
       if (monster.IsBoss) return 0.05f; // 20 Hz
       if (monster.IsElite) return 0.1f; // 10 Hz
       return 0.2f; // 5 Hz for common
   }
   ```

5. **Load Balancing**: Distribute agent updates across frames
   ```csharp
   // Update 1/N agents per frame in round-robin
   int agentsPerFrame = Mathf.CeilToInt(activeAgents / targetFPS);
   ```

## References

- **RLSystem.cs**: Main integration point
- **InferenceBatcher.cs**: Batching implementation
- **RLSpawnLimiter.cs**: Spawn limiting implementation
- **TrainingCoordinator.cs**: Agent update coordination
- **Performance Optimization Guide**: [PERFORMANCE_OPTIMIZATION.md](PERFORMANCE_OPTIMIZATION.md)
