# Hướng Dẫn Tích Hợp RL Visualization cho Monster

## Tổng Quan

Để **thể hiện rõ nhất hành vi học tăng cường** của monster cho người chơi, chúng ta cần implement nhiều tầng visualization và feedback:

## 1. Visual Feedback Trực Tiếp (Đã Tạo)

### A. RLBehaviorVisualizer Component

File: `Assets/Scripts/RL/Visualization/RLBehaviorVisualizer.cs`

**Tính năng:**

- ✅ Decision Indicator: Hiển thị icon phía trên monster khi AI ra quyết định
- ✅ Confidence Meter: Thanh hiển thị độ tự tin (Q-value)
- ✅ Reward Feedback: Flash màu khi nhận reward (xanh = tích cực, đỏ = tiêu cực)
- ✅ Action Trail: Vệt đường đi với gradient màu theo performance

**Cách dùng:**

1. Component tự động được add vào RLMonster khi `enableVisualization = true`
2. Tạo prefab cho decision indicator (arrow, icon, vv.)
3. Assign prefab vào `decisionIndicatorPrefab` field

**Ý nghĩa cho người chơi:**

- **Màu vàng**: Monster đang explore (thử nghiệm)
- **Màu xanh lam**: Monster đang exploit (dùng kinh nghiệm đã học)
- **Vệt xanh lá**: Monster đang nhận positive reward
- **Vệt đỏ**: Monster đang nhận negative reward

### B. RLDashboardUI Component

File: `Assets/Scripts/RL/Visualization/RLDashboardUI.cs`

**Tính năng:**

- ✅ Real-time stats: Số lượng RL agents, training/inference mode
- ✅ Reward graph: Biểu đồ reward theo episode
- ✅ Episode info: Thông tin chi tiết episode hiện tại
- ✅ Exploration rate slider: Hiển thị tỷ lệ exploration

**Setup:**

```csharp
// Trong scene, tạo Canvas UI với các components:
- TextMeshProUGUI: statsText
- TextMeshProUGUI: episodeInfoText
- TextMeshProUGUI: modeText
- Slider: explorationSlider
- Image: rewardGraph

// Attach RLDashboardUI script vào Canvas
// Assign các references
```

## 2. Game Design Patterns để Thể Hiện RL

### Pattern 1: Progressive Difficulty Stages

**Concept**: Chia game thành stages, mỗi stage spawn monsters với behavior profile khác nhau

```csharp
// Stage 1 (0-5 min): Untrained monsters (random behavior)
// Stage 2 (5-10 min): Partially trained (mix random + learned)
// Stage 3 (10-15 min): Fully trained (pure exploitation)
// Stage 4 (15+ min): Advanced trained (trained vs strong players)
```

**Triển khai:**

```csharp
// Trong LevelManager hoặc EntityManager
public void SpawnRLMonsterForStage(int stage)
{
    RLMonsterBlueprint blueprint = GetBlueprintForStage(stage);

    switch (stage)
    {
        case 1:
            blueprint.explorationRate = 0.9f; // 90% random
            blueprint.modelName = ""; // No pre-trained model
            break;
        case 2:
            blueprint.explorationRate = 0.5f; // 50% random
            blueprint.modelName = "partially_trained_model";
            break;
        case 3:
            blueprint.explorationRate = 0.1f; // 10% random
            blueprint.modelName = "fully_trained_model";
            break;
        case 4:
            blueprint.explorationRate = 0.05f; // 5% random
            blueprint.modelName = "expert_model";
            break;
    }

    // Spawn with blueprint
    var monster = rlEntityIntegration.SpawnRLMonster(blueprint, spawnPosition);
}
```

**Người chơi sẽ thấy:**

- Stage 1: Monsters di chuyển loạn xạ, dễ đoán
- Stage 2: Monsters bắt đầu có pattern, đôi khi surprise
- Stage 3: Monsters di chuyển thông minh, khó đánh
- Stage 4: Monsters phối hợp, predict player movement

### Pattern 2: Learning Arena Mode

**Concept**: Mode đặc biệt cho phép player xem monster học real-time

```csharp
public class LearningArenaMode : MonoBehaviour
{
    [SerializeField] private RLDashboardUI dashboard;
    [SerializeField] private int episodeDuration = 60; // seconds
    [SerializeField] private int maxEpisodes = 10;

    private int currentEpisode = 0;
    private float episodeStartTime;

    void StartLearningArena()
    {
        // Spawn monsters in training mode
        var monsters = SpawnTrainingMonsters();

        // Enable visualization for all
        foreach (var monster in monsters)
        {
            monster.IsTraining = true;
            monster.enableVisualization = true;
        }

        // Show dashboard
        dashboard.SetVisible(true);

        episodeStartTime = Time.time;
    }

    void Update()
    {
        if (Time.time - episodeStartTime > episodeDuration)
        {
            EndEpisode();

            if (currentEpisode < maxEpisodes)
            {
                StartNewEpisode();
            }
        }
    }

    void EndEpisode()
    {
        // Record metrics
        var monsters = FindObjectsByType<RLMonster>(FindObjectsSortMode.None);
        float avgReward = CalculateAverageReward(monsters);
        float avgLoss = CalculateAverageLoss(monsters);

        dashboard.RecordEpisode(avgReward, avgLoss, monsters.Length);

        // Reset monsters
        foreach (var monster in monsters)
        {
            // Respawn or reset
        }

        currentEpisode++;
    }
}
```

### Pattern 3: RL Monster Variants với Visual Distinction

**Concept**: Mỗi training stage có skin/color khác nhau

```csharp
public class RLMonsterVisualController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer monsterSprite;

    public void UpdateVisualByTrainingLevel(float explorationRate)
    {
        // Untrained (high exploration): Red tint
        // Partially trained: Yellow tint
        // Well trained: Green tint
        // Expert: Blue glow

        Color tint = Color.white;

        if (explorationRate > 0.7f)
            tint = new Color(1f, 0.5f, 0.5f); // Red
        else if (explorationRate > 0.4f)
            tint = new Color(1f, 1f, 0.5f); // Yellow
        else if (explorationRate > 0.1f)
            tint = new Color(0.5f, 1f, 0.5f); // Green
        else
            tint = new Color(0.5f, 0.5f, 1f); // Blue

        monsterSprite.color = tint;
    }
}
```

## 3. UI/UX Enhancements

### A. Tutorial Pop-ups

```csharp
public class RLTutorialManager : MonoBehaviour
{
    public void ShowTutorial(string stage)
    {
        switch (stage)
        {
            case "first_rl_monster":
                ShowPopup("This is an AI-learning monster! Watch how it adapts to your playstyle.");
                break;
            case "exploration":
                ShowPopup("Yellow glow = AI exploring new strategies");
                break;
            case "exploitation":
                ShowPopup("Cyan glow = AI using learned behavior");
                break;
            case "positive_reward":
                ShowPopup("Green flash = AI received positive reward for good decision");
                break;
        }
    }
}
```

### B. Monster Info Panel

```csharp
// Khi click vào monster, hiển thị info panel
public class RLMonsterInfoPanel : MonoBehaviour
{
    public void ShowInfo(RLMonster monster)
    {
        var metrics = monster.GetMetrics();

        string info = $@"
AI Monster Stats:
- Training Status: {(monster.IsTraining ? "Learning" : "Inference")}
- Exploration Rate: {metrics.explorationRate:P0}
- Episodes Completed: {metrics.episodeCount}
- Average Reward: {metrics.averageReward:F2}
- Best Performance: {metrics.bestReward:F2}
- Current Strategy: {GetStrategyDescription(monster)}
        ";

        DisplayText(info);
    }

    string GetStrategyDescription(RLMonster monster)
    {
        // Analyze recent actions to describe strategy
        return "Aggressive flanking";
    }
}
```

## 4. Audio Feedback

```csharp
public class RLAudioFeedback : MonoBehaviour
{
    [SerializeField] private AudioClip explorationSound;
    [SerializeField] private AudioClip exploitationSound;
    [SerializeField] private AudioClip positiveRewardSound;
    [SerializeField] private AudioClip negativeRewardSound;

    public void OnActionSelected(bool isExploring)
    {
        AudioClip clip = isExploring ? explorationSound : exploitationSound;
        AudioSource.PlayClipAtPoint(clip, transform.position, 0.3f);
    }

    public void OnRewardReceived(float reward)
    {
        AudioClip clip = reward > 0 ? positiveRewardSound : negativeRewardSound;
        float volume = Mathf.Abs(reward) / 10f; // Scale by magnitude
        AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }
}
```

## 5. Particle Effects

```csharp
public class RLParticleEffects : MonoBehaviour
{
    [SerializeField] private ParticleSystem explorationParticles;
    [SerializeField] private ParticleSystem rewardParticles;

    public void PlayExplorationEffect()
    {
        explorationParticles.Play();
    }

    public void PlayRewardEffect(float reward)
    {
        var main = rewardParticles.main;
        main.startColor = reward > 0 ? Color.green : Color.red;
        rewardParticles.Play();
    }
}
```

## 6. Recommended Implementation Steps

### Step 1: Setup Visualization (Immediate)

1. ✅ Files đã tạo: `RLBehaviorVisualizer.cs`, `RLDashboardUI.cs`
2. ⚙️ Tích hợp vào `RLMonster.cs` (đã làm)
3. 🎨 Tạo visual assets:
   - Decision indicator prefab (arrow icon)
   - Confidence meter sprite
   - Trail material

### Step 2: Create Learning Arena Mode (Week 1)

1. Tạo scene mới: `LearningArena`
2. Add `LearningArenaMode` script
3. Setup spawn points và UI
4. Implement episode system

### Step 3: Add Visual Distinction (Week 1-2)

1. Create tinted variants của monster sprites
2. Implement `RLMonsterVisualController`
3. Link visual changes to exploration rate

### Step 4: Polish & Audio (Week 2)

1. Add sound effects cho RL events
2. Add particle effects
3. Tutorial pop-ups
4. Monster info panel

### Step 5: Balance & Testing (Week 3)

1. Train models cho different stages
2. Test với real players
3. Adjust visualization prominence
4. Optimize performance

## 7. Performance Considerations

```csharp
// Chỉ enable visualization cho nearby monsters
public class RLVisualizationManager : MonoBehaviour
{
    [SerializeField] private float visualizationRadius = 20f;
    private Camera mainCamera;

    void Update()
    {
        Vector2 cameraPos = mainCamera.transform.position;
        var monsters = FindObjectsByType<RLMonster>(FindObjectsSortMode.None);

        foreach (var monster in monsters)
        {
            float distance = Vector2.Distance(monster.transform.position, cameraPos);
            bool shouldVisualize = distance < visualizationRadius;

            if (monster.behaviorVisualizer != null)
            {
                monster.behaviorVisualizer.enabled = shouldVisualize;
            }
        }
    }
}
```

## 8. Testing Scenarios

### Scenario A: Showcase Learning

- Spawn untrained monsters (exploration = 1.0)
- Player plays for 5 minutes
- Save model
- Respawn with saved model (exploration = 0.1)
- Player should notice smarter behavior

### Scenario B: Comparison

- Spawn 5 untrained monsters (left side)
- Spawn 5 trained monsters (right side)
- Same color but different behavior
- Player can see difference

### Scenario C: Progressive Challenge

- Wave 1: Untrained (easy)
- Wave 2: Partially trained (medium)
- Wave 3: Well trained (hard)
- Wave 4: Expert (very hard)

## 9. Debug Tools

```csharp
// Add to RLMonster for debugging
#if UNITY_EDITOR
[ContextMenu("Force Exploration Mode")]
void DebugForceExploration()
{
    explorationRate = 1.0f;
    Debug.Log("Monster now 100% exploring");
}

[ContextMenu("Force Exploitation Mode")]
void DebugForceExploitation()
{
    explorationRate = 0.0f;
    Debug.Log("Monster now 100% exploiting");
}

[ContextMenu("Print Current Strategy")]
void DebugPrintStrategy()
{
    if (currentAction != null)
    {
        Debug.Log($"Current action: {currentAction.actionType}, direction: {currentAction.moveDirection}");
    }
}
#endif
```

## Summary

**Để thể hiện rõ nhất RL behavior:**

1. **Visual**: Colors, indicators, trails
2. **UI**: Dashboard, stats, graphs
3. **Game Design**: Progressive stages, comparison scenarios
4. **Audio**: Sound feedback cho actions/rewards
5. **Tutorial**: Explain ý nghĩa của visual cues

**Người chơi sẽ thấy:**

- Monsters "học" và cải thiện theo thời gian
- Sự khác biệt giữa trained vs untrained
- Real-time decision making process
- Reward signals (positive/negative feedback)
- Learning progress qua dashboard

Hệ thống này làm cho RL behavior **visible, understandable, và engaging** cho người chơi!
