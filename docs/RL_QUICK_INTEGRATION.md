# Quick RL Integration Guide

## Mục Tiêu

Tích hợp học tăng cường (RL) cho monsters trong game **ngay lập tức**, bỏ qua visualization.

## Prerequisites - Components Đã Có

### ✅ Core RL Components

1. **RLEnvironment** - State observation & reward calculation
2. **DQNLearningAgent** - Deep Q-Network agent
3. **RLMonster** - Monster với RL behavior
4. **ActionDecoder** - Convert network output → game actions
5. **ExperienceReplayBuffer** - Store training experiences

## Step-by-Step Integration

### Step 1: Create RL Monster Blueprint

```bash
# Trong Unity Editor:
1. Right-click trong Project window
2. Create → Blueprints → RL Monster
3. Name: "RLMeleeMonsterBlueprint"
```

**Configure Blueprint:**

```csharp
// Trong Inspector:
[RL Configuration]
- Enable RL: ✓ true
- Monster Type: Melee
- Exploration Rate: 0.2 (20% random actions)
- Learning Rate: 0.001
- Discount Factor: 0.99

[Training]
- Enable Training: ✓ true
- Experience Buffer Size: 5000
- Batch Size: 32
- Training Update Interval: 100 steps

[Visualization]
- Enable Visualization: ✗ false  // BỎ QUA VISUALIZATION
```

### Step 2: Create RL Monster Prefab

```bash
1. Duplicate existing monster prefab (e.g., MeleeMonster)
2. Rename → "RLMeleeMonster"
3. Add Component → RLMonster script
4. Replace Monster script với RLMonster script
```

**Configure Prefab:**

```csharp
[RLMonster Component]
- Enable RL: ✓ true
- RL Monster Type: Melee
- Enable Visualization: ✗ false  // TẮT VISUALIZATION
- Exploration Rate: 0.2
- Learning Rate: 0.001
- Discount Factor: 0.99
- Update Frequency: 100
```

### Step 3: Setup RLSystem trong Scene

```bash
# Trong scene hierarchy:
1. Create Empty GameObject → Name: "RLSystem"
2. Add Component: RLSystem
3. Add Component: RLEnvironment
4. Add Component: RLEnvironmentManager
```

**Configure RLSystem:**

```csharp
[RLSystem]
- Max RL Agents: 10
- Enable Training: true
- Training Mode: Online  // Train during gameplay

[RLEnvironment]
- Observation Radius: 10f
- Max Nearby Monsters: 5
- Episode Time Limit: 300f (5 minutes)

[Dependencies - Auto-link these in Start()]
- Entity Manager: [drag EntityManager]
- Player Character: [drag Player]
```

### Step 4: Initialize RLSystem trong LevelManager

```csharp
// Assets/Scripts/Gameplay/LevelManager.cs

public class LevelManager : MonoBehaviour
{
    [SerializeField] private RLSystem rlSystem;
    [SerializeField] private RLEnvironment rlEnvironment;

    public void Init(LevelBlueprint levelBlueprint)
    {
        // ... existing init code ...

        // Initialize RL System
        InitializeRLSystem();
    }

    private void InitializeRLSystem()
    {
        if (rlSystem != null && rlEnvironment != null)
        {
            // Initialize environment
            rlEnvironment.Initialize(
                entityManager,
                playerCharacter,
                null  // Will use default reward calculator
            );

            Debug.Log("[RL] System initialized successfully");
        }
    }
}
```

### Step 5: Spawn RL Monsters

**Option A: Spawn Specific RL Monster**

```csharp
// Trong EntityManager hoặc LevelManager:

private void SpawnRLMonster(Vector2 position)
{
    // Get blueprint
    var rlBlueprint = Resources.Load<RLMonsterBlueprint>("RLMeleeMonsterBlueprint");

    // Use RLEntityIntegration if available
    var rlIntegration = GetComponent<RLEntityIntegration>();
    if (rlIntegration != null)
    {
        var rlMonster = rlIntegration.SpawnRLMonster(rlBlueprint, position);
        rlMonster.IsTraining = true;  // Enable training mode
        Debug.Log($"Spawned RL monster at {position}");
    }
}
```

**Option B: Mix RL & Normal Monsters**

```csharp
// Trong level blueprint hoặc spawn logic:

private void SpawnMixedMonsters()
{
    float rlMonsterRatio = 0.3f;  // 30% RL monsters

    for (int i = 0; i < totalMonstersToSpawn; i++)
    {
        Vector2 spawnPos = GetRandomSpawnPosition();

        if (Random.value < rlMonsterRatio)
        {
            // Spawn RL monster
            SpawnRLMonster(spawnPos);
        }
        else
        {
            // Spawn normal monster
            SpawnNormalMonster(spawnPos);
        }
    }
}
```

### Step 6: Verify RL is Working

**Debug Logs to Check:**

```csharp
// Trong RLMonster.Update():
if (enableRL && learningAgent != null)
{
    Debug.Log($"[RL] Action selected: {currentAction.actionType}, " +
              $"Q-value: {qValue:F2}, Training: {isTrainingMode}");
}

// Trong DQNLearningAgent.UpdatePolicy():
Debug.Log($"[DQN] Training step {updateCounter}, " +
          $"Loss: {loss:F4}, " +
          $"Experiences: {experienceBuffer.Count}");
```

**Console Output Should Show:**

```
[RL] System initialized successfully
[RL] Action selected: Move, Q-value: 0.52, Training: true
[DQN] Training step 100, Loss: 0.1234, Experiences: 150
[RL] Action selected: Attack, Q-value: 0.78, Training: true
[DQN] Training step 200, Loss: 0.0987, Experiences: 250
...
```

## Quick Test Scenario

### Test 1: Single RL Monster

```csharp
// Test script:
public class RLQuickTest : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(TestRLMonster());
    }

    IEnumerator TestRLMonster()
    {
        yield return new WaitForSeconds(2f);  // Wait for initialization

        // Spawn 1 RL monster
        var position = playerCharacter.transform.position + Vector3.right * 5f;
        SpawnRLMonster(position);

        Debug.Log("[Test] RL Monster spawned. Watch console for RL logs.");

        // Monitor for 30 seconds
        yield return new WaitForSeconds(30f);

        // Check metrics
        var rlMonsters = FindObjectsByType<RLMonster>(FindObjectsSortMode.None);
        foreach (var monster in rlMonsters)
        {
            var metrics = monster.GetMetrics();
            Debug.Log($"[Test] Monster metrics: " +
                     $"Steps: {metrics.totalSteps}, " +
                     $"Avg Reward: {metrics.averageReward:F2}, " +
                     $"Exploration Rate: {metrics.explorationRate:P0}");
        }
    }
}
```

### Test 2: Training Progress

```csharp
// Monitor training:
public class TrainingMonitor : MonoBehaviour
{
    private float checkInterval = 10f;

    void Start()
    {
        InvokeRepeating(nameof(CheckTrainingProgress), checkInterval, checkInterval);
    }

    void CheckTrainingProgress()
    {
        var rlMonsters = FindObjectsByType<RLMonster>(FindObjectsSortMode.None);

        int trainingCount = 0;
        float avgReward = 0f;

        foreach (var monster in rlMonsters)
        {
            if (monster.IsTraining)
            {
                trainingCount++;
                var metrics = monster.GetMetrics();
                avgReward += metrics.averageReward;
            }
        }

        if (trainingCount > 0)
        {
            avgReward /= trainingCount;
            Debug.Log($"[Training] {trainingCount} agents learning, " +
                     $"Avg Reward: {avgReward:F2}");
        }
    }
}
```

## Troubleshooting

### Issue 1: No RL Logs

**Problem:** Không thấy logs từ RL system

**Solution:**

```csharp
// Check in RLMonster.Init():
Debug.Log($"RLMonster Init: enableRL={enableRL}, " +
          $"learningAgent={learningAgent != null}, " +
          $"rlEnvironment={rlEnvironment != null}");

// Check in RLMonster.Update():
Debug.Log($"RL Update: alive={alive}, " +
          $"enableRL={enableRL}, " +
          $"learningAgent={learningAgent != null}");
```

### Issue 2: Monsters Not Learning

**Problem:** Monsters spawn nhưng behavior không thay đổi

**Solution:**

```csharp
// Check training mode:
if (rlMonster != null)
{
    rlMonster.IsTraining = true;  // Force training mode
    Debug.Log($"Monster training: {rlMonster.IsTraining}");
}

// Check experience buffer:
var dqnAgent = rlMonster.GetComponent<DQNLearningAgent>();
if (dqnAgent != null)
{
    Debug.Log($"Ready to train: {dqnAgent.IsReadyToTrain()}");
}
```

### Issue 3: Performance Issues

**Problem:** Game lag khi có nhiều RL monsters

**Solution:**

```csharp
// Reduce RL monster count:
[RLSystem]
- Max RL Agents: 5  // Giảm từ 10

// Reduce update frequency:
[RLMonster]
- Update Frequency: 200  // Tăng từ 100

// Disable unused features:
- Enable Visualization: false
- Enable Audio Feedback: false
```

## Performance Tips

1. **Start Small:** Begin với 1-2 RL monsters, tăng dần
2. **Adjust Update Frequency:** Tăng `updateFrequency` nếu lag
3. **Monitor Experience Buffer:** Đừng để buffer quá lớn
4. **Save Models Periodically:** Save trained models để reuse

## Next Steps After Integration

1. **Let it Train:** Chơi game 10-15 phút để monsters học
2. **Save Models:** Save trained models để dùng lại
3. **Test Inference:** Tắt training, enable inference mode
4. **Adjust Rewards:** Tweak reward calculation nếu behavior không tốt
5. **Add More Agents:** Tăng số lượng RL monsters dần

## Model Saving/Loading

```csharp
// Save trained model:
var rlMonster = GetComponent<RLMonster>();
string modelPath = Application.persistentDataPath + "/rl_melee_model.json";
rlMonster.SaveBehaviorProfile(modelPath);
Debug.Log($"Model saved to: {modelPath}");

// Load trained model:
rlMonster.LoadBehaviorProfile(modelPath);
rlMonster.IsTraining = false;  // Switch to inference mode
Debug.Log("Model loaded, inference mode active");
```

## Summary Checklist

- [ ] RL Monster Blueprint created
- [ ] RL Monster Prefab created
- [ ] RLSystem added to scene
- [ ] RLEnvironment initialized in LevelManager
- [ ] Spawn logic updated
- [ ] Debug logs visible in console
- [ ] Monsters spawning và learning
- [ ] No performance issues
- [ ] Models saving successfully

**You're now ready to use RL in your game!** 🚀

Chạy game, observe console logs, và watch monsters học theo thời gian.
