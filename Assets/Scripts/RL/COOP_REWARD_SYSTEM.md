# Co-op Reward System

## Overview

The Co-op Reward System extends the standard RL reward calculation to provide signals for cooperative multi-player gameplay. It rewards agents for:

- **Assist**: Helping teammates kill enemies
- **Aggro Share**: Drawing enemy attention away from weak teammates
- **Formation**: Maintaining good positioning relative to team

These rewards leverage the extended observation data from `StateEncoder` with co-op fields (agent ID, teammate info, team aggregates).

## Architecture

### Components

1. **CoopRewardCalculator**: Main co-op reward implementation
2. **RewardCalculator**: Base calculator with optional co-op integration
3. **StateEncoder**: Provides co-op observation data (teammates, team aggregates)
4. **RLGameState**: Contains co-op fields (agentId, teammates[], teamFocusTarget, etc.)

### Integration Pattern

```
RLGameState (with co-op data)
    ↓
CoopRewardCalculator
    ├─→ AssistReward (kill contribution)
    ├─→ AggroShareReward (protecting teammates)
    ├─→ FormationReward (positioning)
    └─→ FocusFireReward (coordinated attacks)
    ↓
Total Reward
```

## Reward Components

### 1. Assist Reward

**Purpose**: Reward agents for helping teammates kill enemies

**Mechanism**:

- Tracks damage contributors per monster
- When monster dies, checks all agents within `assistDistanceThreshold`
- Distributes assist reward proportional to nearby teammates

**Configuration**:

```csharp
[SerializeField] private float assistReward = 15f;
[SerializeField] private float assistDistanceThreshold = 10f;
[SerializeField] private float assistTimeWindow = 5f;
```

**Formula**:

```csharp
assistReward = baseAssistReward × (nearbyTeammates / maxTeammates)
```

**Example**:

- Monster killed with 2 teammates nearby (within 10 units)
- Assist reward = 15 × (2/3) = 10 points

**Use Case**: Encourages cooperative takedowns instead of solo kills

### 2. Aggro Share Reward

**Purpose**: Reward agents for drawing enemy attention away from weak teammates

**Mechanism**:

- Identifies teammates below `aggroHealthThreshold` (default: 50%)
- Measures distance from monster to weak teammates
- Rewards agent if closer to monster than weak teammate

**Configuration**:

```csharp
[SerializeField] private float aggroShareReward = 10f;
[SerializeField] private float aggroDistanceThreshold = 8f;
[SerializeField] private float aggroHealthThreshold = 0.5f;
```

**Formula**:

```csharp
aggroStrength = 1 - (distanceToMonster / aggroDistanceThreshold)
aggroReward = baseAggroReward × aggroStrength
```

**Example**:

- Teammate at 30% health, 12 units from monster
- Agent at 5 units from monster (closer)
- Aggro strength = 1 - (5/8) = 0.375
- Aggro reward = 10 × 0.375 = 3.75 points

**Use Case**: Encourages tanking behavior to protect low-health allies

### 3. Formation Reward

**Purpose**: Reward agents for maintaining optimal team positioning

**Sub-components**:

#### A. Spread Score (40% of formation reward)

Maintains optimal distance between teammates (not too clumped, not too spread)

**Formula**:

```csharp
avgDistance = sum(distanceToTeammates) / teammateCount
deviation = |avgDistance - optimalSpreadDistance|
spreadScore = clamp01(1 - deviation / optimalSpreadDistance)
```

**Configuration**:

```csharp
[SerializeField] private float optimalSpreadDistance = 5f;
```

**Example**:

- Optimal spread: 5 units
- Actual average: 6 units
- Deviation: 1 unit
- Spread score = 1 - (1/5) = 0.8

#### B. Flanking Score (40% of formation reward)

Surrounds monster from multiple angles for coordinated attacks

**Formula**:

```csharp
idealAngleSpacing = 360° / agentCount
actualSpacing = calculate angles between agents
flankingScore = 1 - (avgDeviation / 180°)

// Bonuses:
if (agents >= 3 && score > 0.6): score × 2.0 (surround bonus)
if (agents >= 2 && score > 0.5): score × 1.5 (flanking bonus)
```

**Example**:

- 3 agents at 120° apart (perfect triangle)
- Ideal spacing: 360/3 = 120°
- Deviation: 0°
- Flanking score = 1.0 × 2.0 (surround bonus) = 2.0 (capped at 1.0 after normalization)

#### C. Protection Score (20% of formation reward)

Positions agent between monster and weak teammates

**Formula**:

```csharp
alignment = dot(monsterToPlayer, monsterToTeammate)
if (alignment > 0.7 && distanceToMonster < teammateDistance):
    protectionStrength = (1 - healthRatio) × alignment
    protectionScore = protectionStrength × protectWeakReward
```

**Configuration**:

```csharp
[SerializeField] private float formationReward = 8f;
[SerializeField] private float protectWeakReward = 10f;
[SerializeField] private float flankingBonusMultiplier = 1.5f;
[SerializeField] private float surroundBonusMultiplier = 2.0f;
```

**Example**:

- Teammate at 20% health behind agent
- Agent positioned between monster and teammate
- Alignment score: 0.9 (nearly perfect)
- Protection strength = (1 - 0.2) × 0.9 = 0.72
- Protection score = 0.72 × 10 = 7.2 points

### 4. Focus Fire Reward

**Purpose**: Reward agents for coordinated attacks on same target

**Mechanism**:

- Uses `teamFocusTarget` from RLGameState
- Checks if monster is near team focus target
- Counts teammates also attacking focus target
- Rewards proportional to team participation

**Configuration**:

```csharp
[SerializeField] private float focusFireReward = 12f;
```

**Formula**:

```csharp
if (distanceToFocusTarget < 5f):
    focusStrength = teammatesNearFocus / maxTeammates
    focusFireReward = baseFocusReward × focusStrength
```

**Example**:

- Team focus target: Boss at (10, 10)
- Monster at (11, 11) - 1.4 units from focus
- 2 teammates within 10 units of focus
- Focus strength = 2/3 = 0.67
- Focus fire reward = 12 × 0.67 = 8 points

## Configuration

### Inspector Settings

```csharp
[Header("Assist Rewards")]
[SerializeField] private float assistReward = 15f;
[SerializeField] private float assistDistanceThreshold = 10f;
[SerializeField] private float assistTimeWindow = 5f;

[Header("Aggro Share Rewards")]
[SerializeField] private float aggroShareReward = 10f;
[SerializeField] private float aggroDistanceThreshold = 8f;
[SerializeField] private float aggroHealthThreshold = 0.5f;

[Header("Formation Rewards")]
[SerializeField] private float formationReward = 8f;
[SerializeField] private float optimalSpreadDistance = 5f;
[SerializeField] private float formationCheckRadius = 15f;
[SerializeField] private float flankingBonusMultiplier = 1.5f;
[SerializeField] private float surroundBonusMultiplier = 2.0f;

[Header("Team Coordination")]
[SerializeField] private float focusFireReward = 12f;
[SerializeField] private float protectWeakReward = 10f;
```

### Integration in RewardCalculator

```csharp
[Header("Reward Configuration")]
[SerializeField] private bool enableCoopRewards = false;
[SerializeField] private CoopRewardCalculator coopRewardCalculator;
```

**Setup**:

1. Create `CoopRewardCalculator` component
2. Assign to `RewardCalculator.coopRewardCalculator` field
3. Enable `enableCoopRewards` checkbox
4. Configure reward weights in inspector

## Usage Examples

### Basic Setup

```csharp
public class GameManager : MonoBehaviour
{
    [SerializeField] private RewardCalculator rewardCalculator;
    [SerializeField] private CoopRewardCalculator coopRewardCalculator;
    [SerializeField] private RLEnvironment environment;

    void Start()
    {
        // Initialize base reward calculator
        rewardCalculator.Initialize(environment, entityManager, playerCharacter);

        // Enable co-op rewards for multiplayer
        if (IsMultiplayer())
        {
            rewardCalculator.enableCoopRewards = true;
        }
    }
}
```

### Calculate Reward with Co-op Context

```csharp
public class MonsterAI : MonoBehaviour
{
    [SerializeField] private Monster monster;
    [SerializeField] private RewardCalculator rewardCalculator;

    void OnActionComplete(int action, ActionOutcome outcome)
    {
        // Get current and previous game states
        RLGameState previousState = GetPreviousState();
        RLGameState currentState = environment.BuildGameState(monster, agentId);

        // Calculate reward (includes co-op if enabled)
        float reward = rewardCalculator.CalculateReward(
            previousState,
            new MonsterAction { actionType = action },
            currentState,
            outcome
        );

        // Apply reward to learning
        agent.ApplyReward(reward);
    }
}
```

### Check Co-op Statistics

```csharp
public class RewardDebugPanel : MonoBehaviour
{
    [SerializeField] private CoopRewardCalculator coopRewards;
    [SerializeField] private Text statsText;

    void Update()
    {
        var stats = coopRewards.GetStats();
        statsText.text = $"Co-op Rewards:\n" +
                        $"  Assists: {stats.totalAssists}\n" +
                        $"  Aggro Shares: {stats.totalAggroShares}\n" +
                        $"  Formation Bonuses: {stats.totalFormationBonuses}\n" +
                        $"  Tracked Monsters: {stats.trackedMonsters}";
    }
}
```

### Custom Reward Tuning

```csharp
public class DifficultyScaler : MonoBehaviour
{
    [SerializeField] private CoopRewardCalculator coopRewards;

    public void SetDifficulty(string difficulty)
    {
        switch (difficulty)
        {
            case "Easy":
                // Higher co-op rewards to encourage teamwork
                coopRewards.assistReward = 20f;
                coopRewards.aggroShareReward = 15f;
                coopRewards.formationReward = 12f;
                break;

            case "Hard":
                // Lower co-op rewards, more individual skill
                coopRewards.assistReward = 10f;
                coopRewards.aggroShareReward = 5f;
                coopRewards.formationReward = 5f;
                break;

            case "Normal":
            default:
                // Balanced
                coopRewards.assistReward = 15f;
                coopRewards.aggroShareReward = 10f;
                coopRewards.formationReward = 8f;
                break;
        }
    }
}
```

## Performance Considerations

### Computational Cost

**Per-frame calculations:**

- Assist: O(1) - simple distance checks
- Aggro Share: O(T) where T = teammate count (max 3)
- Formation: O(T²) for angle calculations (max 3×3 = 9)
- Focus Fire: O(T) for proximity checks

**Total**: ~0.05ms per monster per frame (negligible)

### Optimization Tips

1. **Cache calculations**: Store intermediate results (angles, distances)
2. **Batch updates**: Update formation scores every N frames, not every frame
3. **LOD system**: Reduce co-op reward precision for distant monsters
4. **Early exit**: Skip calculations when totalTeammateCount = 0

### Memory Usage

**Per-monster tracking:**

- DamageTracker: ~100 bytes (damage contributors dictionary)
- AggroTracker: ~20 bytes (target ID + timestamp)
- FormationTracker: ~12 bytes (cached scores)

**Total**: ~130 bytes per monster (50 monsters = 6.5 KB)

## Tuning Guide

### Reward Balance

**Default weights (normalized to 100 total):**

- Damage/Survival: 40 points
- Assist: 15 points
- Aggro Share: 10 points
- Formation: 8 points
- Focus Fire: 12 points
- Base coordination: 15 points

**Adjustment guidelines:**

**For tank/support playstyle:**

```csharp
aggroShareReward = 20f; // Increase tanking reward
protectWeakReward = 15f; // Increase protection reward
assistReward = 10f; // Reduce kill focus
```

**For DPS/aggressive playstyle:**

```csharp
assistReward = 20f; // Increase kill participation
focusFireReward = 18f; // Reward burst damage
aggroShareReward = 5f; // Reduce tanking
```

**For support/healer playstyle:**

```csharp
protectWeakReward = 25f; // High protection reward
aggroShareReward = 15f; // High aggro draw
assistReward = 8f; // Lower kill focus
```

### Formation Tuning

**Optimal spread by team size:**

- 2 players: 6-8 units (flanking)
- 3 players: 5-7 units (triangle)
- 4 players: 4-6 units (square)

**Adjust `optimalSpreadDistance` based on game scale and enemy types**

### Threshold Tuning

**Assist distance by combat range:**

- Melee combat: 8-10 units
- Ranged combat: 15-20 units
- Mixed: 12-15 units

**Aggro health threshold by difficulty:**

- Easy: 0.7 (protect at 70% health)
- Normal: 0.5 (protect at 50% health)
- Hard: 0.3 (protect only critically low)

## Integration Checklist

- [ ] Add `CoopRewardCalculator` component to scene
- [ ] Assign to `RewardCalculator.coopRewardCalculator` field
- [ ] Enable `RewardCalculator.enableCoopRewards`
- [ ] Configure reward weights in inspector
- [ ] Verify `RLGameState` has co-op fields (agentId, teammates, team aggregates)
- [ ] Test assist reward with team kills
- [ ] Test aggro share with low-health teammates
- [ ] Test formation reward with team positioning
- [ ] Profile performance with max monster count
- [ ] Add debug UI for co-op reward statistics
- [ ] Balance reward weights for target gameplay

## Dependencies

**Required:**

- `RLGameState` with co-op fields (from CO-OP_OBSERVATION_EXTENSIONS)
- `StateEncoder` with co-op encoding (agent ID, teammates, team aggregates)
- `RLEnvironment.BuildGameState()` populating co-op fields

**Optional:**

- Damage tracking system for accurate assist calculation
- Network event system for multiplayer assist tracking
- Aggro system for aggro share tracking

## Future Enhancements

### Planned Features

1. **Dynamic Assist Window**:

   ```csharp
   // Adjust assist time based on enemy type
   float GetAssistTimeWindow(MonsterType type)
   {
       return type == MonsterType.Boss ? 10f : 5f;
   }
   ```

2. **Role-based Rewards**:

   ```csharp
   enum PlayerRole { Tank, DPS, Support }
   float GetRoleMultiplier(PlayerRole role, RewardType rewardType);
   ```

3. **Combo Bonuses**:

   ```csharp
   // Bonus for chaining multiple co-op actions
   if (assistStreak >= 3) reward *= 1.5f;
   ```

4. **Adaptive Thresholds**:

   ```csharp
   // Adjust thresholds based on performance
   UpdateThresholdsBasedOnTeamStats();
   ```

5. **Context-aware Rewards**:
   ```csharp
   // Higher rewards for protecting during boss fights
   if (IsBossFight()) aggroShareReward *= 2f;
   ```

## References

- **RewardCalculator.cs**: Base reward calculation
- **CoopRewardCalculator.cs**: Co-op reward implementation
- **RLGameState.cs**: Game state with co-op fields
- **StateEncoder.cs**: Observation encoding with co-op data
- **CO-OP_OBSERVATION_EXTENSIONS.md**: Observation system documentation
- **RLEnvironment.cs**: Environment for state building
