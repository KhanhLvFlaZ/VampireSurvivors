# Hybrid RL Training: PlayerBotAI → Real Player Fine-tuning

## Tổng Quan

```
Phase 1: Training (PlayerBotAI)        Phase 2: Fine-tuning (Real Player)
├── Monsters học tactical basics       ├── Monsters adapt to real player
├── 1M steps (~3-5 giờ)               ├── Continuous learning
├── Generate: model.onnx              ├── Load pre-trained model
└── Save checkpoint                   └── Output: final tactical model
```

---

## PHASE 1: Intensive Training Với PlayerBotAI

### 1.1 Setup Training Scene

Theo hướng dẫn trong [TRAINING_SCENE_SETUP.md](TRAINING_SCENE_SETUP.md), nhưng **thêm PlayerBotAI**:

```
TrainingScene
├── Main Camera
├── Player (NEW: with PlayerBotAI)
│   ├── Sprite Renderer
│   ├── Rigidbody 2D
│   ├── Circle Collider 2D
│   ├── SimplePlayer script (hoặc để trống)
│   └── PlayerBotAI ← NEW!
├── TrainingManager + Spawner
└── RLMonster_0 ... RLMonster_11 (12 instances)
```

### 1.2 Cấu Hình PlayerBotAI

Trong Inspector, đặt:

```
Movement Settings:
  ✓ Move Speed: 4
  ✓ Change Direction Interval: 2
  ✓ Enable Random Movement: TRUE

Attack Simulation:
  ✓ Attack Interval: 3
  ✓ Attack Damage: 10
  ✓ AOE Damage Radius: 3
  ✓ Enable Attacks: TRUE

Evasion (Advanced):
  ✓ Dodge Chance: 0.3 (30%)
  ✓ Dodge Distance: 5
  ✓ Enable Evasion: TRUE

Arena Bounds:
  ✓ Arena Size: 10
  ✓ Arena Center: (0, 0)

Debug:
  ✓ Show Debug Gizmos: TRUE
```

### 1.3 Hành Vi Của PlayerBotAI

Monsters sẽ **học được**:

| Hành Vi Bot               | Monster Learns                |
| ------------------------- | ----------------------------- |
| Chạy random direction     | Theo dõi target động          |
| Attack AOE                | Rút lui từ danger zone        |
| Dodge khi bị bao vây      | Không bao vây từ 3 phía       |
| Thay đổi hành vi liên tục | Adapt vs unpredictable player |

### 1.4 Run Training

```bash
# Terminal 1: Vào project directory
cd C:\Users\khoil\UnityProjects\VampireSurvivors

# Activate venv
.\.venv\Scripts\Activate.ps1

# Terminal 2: Start training
mlagents-learn ml-agents-configs/monster_config.yaml \
  --run-id=monster_tactical_v1 \
  --force-envs

# Terminal 3: Monitor with TensorBoard
tensorboard --logdir=results/
```

### 1.5 Monitor Training Progress

**Expected timeline:**

| Steps      | Observations                  |
| ---------- | ----------------------------- |
| 0-100k     | Random behavior, hành vi loạn |
| 100k-300k  | Bắt đầu retreat từ damage     |
| 300k-600k  | Học maintain distance         |
| 600k-1000k | Phối hợp nhóm, bao vây tối ưu |

**Key metrics (TensorBoard):**

- **Cumulative Reward**: Tăng từ ~0 → 50-100
- **Episode Length**: Tăng (agents sống lâu hơn)
- **Policy Loss**: Giảm (policy được tối ưu)
- **Entropy**: Giữ cao (vẫn explore)

### 1.6 Save Checkpoint (Tùy chọn)

Khi hài lòng với behavior (ví dụ ở step 600k):

```bash
# Dừng training (Ctrl+C)
# Model được auto-save tại:
# results/monster_tactical_v1/RLMonster.onnx
```

---

## PHASE 2: Fine-tuning Với Real Player

### 2.1 Deploy Model Vào Game Scene

**Bước 1: Copy model**

```
Từ: results/monster_tactical_v1/RLMonster.onnx
Tới: Assets/Models/RLMonster_PreTrained.onnx
```

**Bước 2: Tạo Game Scene** (hoặc mở Level 1)

```
GameScene
├── Main Camera
├── Player (Character thật)
│   ├── Sprite Renderer
│   ├── Rigidbody 2D
│   ├── Character script
│   ├── Ability systems
│   ├── Input handling
│   └── NO PlayerBotAI!
├── Spawn Manager
└── Monsters với RLMonsterAgent (hybrid)
```

### 2.2 Cấu Hình RLMonsterAgent Cho Fine-tuning

**Script modification:**

```csharp
public class RLMonsterAgent : Agent
{
    [Header("Fine-tuning Settings")]
    [SerializeField] private bool enableFineTuning = true; // Toggle for game
    [SerializeField] private string preTrainedModelPath = "Models/RLMonster_PreTrained";
    [SerializeField] private float finetuneExplorationRate = 0.05f; // Rất thấp

    void Start()
    {
        if (enableFineTuning)
        {
            LoadPreTrainedModel();
        }
    }

    void LoadPreTrainedModel()
    {
        // Load from: Assets/Models/RLMonster_PreTrained.onnx
        // Continue with very low learning rate
        Debug.Log("[RLMonsterAgent] Loaded pre-trained model, ready for fine-tuning");
    }

    // ... rest of implementation
}
```

**Hoặc dùng Behavior Parameters trong Unity:**

```
Behavior Parameters:
  Model: RLMonster_PreTrained (drag model.onnx vào)
  Inference Device: CPU (hoặc GPU nếu có)

Decision Requester:
  Decision Period: 5 (nhanh hơn vì fine-tuning không cần thường xuyên)
```

### 2.3 Fine-tuning Setup

**Option A: Continuous Learning (Game keeps training)**

```bash
# Vẫn chạy mlagents-learn nhưng Learning Rate rất thấp
mlagents-learn ml-agents-configs/monster_config.yaml \
  --run-id=monster_tactical_v1_finetuned \
  --initialize-from=results/monster_tactical_v1 \
  --force-envs

# Game scene vẫn kết nối, monsters tiếp tục học từ real player
```

**Option B: Static Model (Không training trong game)**

```csharp
// RLMonsterAgent chỉ dùng model cho inference (không update weights)
[SerializeField] private bool allowTrainingInGame = false;

void Start()
{
    if (!allowTrainingInGame)
    {
        // Model chỉ dùng cho prediction, không training
        // Monsters sẽ sử dụng pre-trained behavior
    }
}
```

### 2.4 Monitor Real Player Behavior

Thêm telemetry script để tracking:

```csharp
public class GameplayMonitor : MonoBehaviour
{
    [SerializeField] private RLMonsterAgent[] rlMonsters;

    void Update()
    {
        // Track monster behavior
        int retreatingCount = 0;
        int flankingCount = 0;

        foreach (var monster in rlMonsters)
        {
            // Log behavior adaptation
            if (monster.GetCurrentAction() == ActionType.Retreat)
                retreatingCount++;
        }

        Debug.Log($"Monsters: Retreating={retreatingCount}, Flanking={flankingCount}");
    }
}
```

### 2.5 Collect Data For Retraining (Optional)

Sau ~2 giờ gameplay, có thể retrain với real player data:

```csharp
public class ExperienceRecorder : MonoBehaviour
{
    [SerializeField] private bool recordExperiences = true;
    private List<Experience> experiences = new List<Experience>();

    // Record gameplay experiences
    void OnMonsterBehavior(MonsterAction action, float reward)
    {
        if (recordExperiences)
        {
            experiences.Add(new Experience
            {
                action = action,
                reward = reward,
                context = "real_player_gameplay"
            });
        }
    }

    public void SaveExperiencesForRetraining()
    {
        // Save to file for Phase 1.5: Retrain with real player data
    }
}
```

---

## PHASE 1.5 (Optional): Retrain Với Real Player Data

Nếu muốn monsters học tốt hơn từ real player:

```bash
# Sau khi chơi game 2-3 giờ, collect experiences
# Retrain từ checkpoint v1:

mlagents-learn ml-agents-configs/monster_config.yaml \
  --run-id=monster_tactical_v2_realplayer \
  --initialize-from=results/monster_tactical_v1 \
  --force-envs

# Monsters sẽ adapt tốt hơn cho real player pattern
```

---

## So Sánh Training Phases

| Aspect               | Phase 1 (Bot)                 | Phase 2 (Real)          |
| -------------------- | ----------------------------- | ----------------------- |
| **Player behavior**  | Predictable, varied           | Unpredictable           |
| **Learning focus**   | Basic tactics                 | Player adaptation       |
| **Duration**         | 3-5 giờ (1M steps)            | Continuous              |
| **Exploration rate** | High (0.1)                    | Low (0.05)              |
| **Goal**             | Converge to tactical behavior | Adapt to real playstyle |

---

## Expected Results

### After Phase 1 (Bot training):

```
✓ Monster retreat khi HP thấp
✓ Monster maintain optimal distance
✓ Monster nhóm đám đông rồi tấn công
✓ Monster flank thay vì lao thẳng
✓ Pattern: Rất tactical, khó đoán
```

### After Phase 2 (Real player fine-tuning):

```
✓ Tất cả trên PLUS:
✓ Adapt với player weapon type
✓ Học dodge pattern cụ thể
✓ Phối hợp vs player abilities
✓ Pattern: THỰC SỰ challenging
```

---

## Troubleshooting 2 Phases

| Problem                       | Solution                                |
| ----------------------------- | --------------------------------------- |
| Monsters không learn Phase 2  | Learning rate quá cao → giảm            |
| Model forget Phase 1 behavior | Giảm fine-tune steps hoặc learning rate |
| Game lag khi training         | Disable training, chỉ dùng inference    |
| Model conflict                | Đảm bảo behavior name match config      |

---

## Quick Commands Cheat Sheet

```bash
# Phase 1: Training with PlayerBotAI
mlagents-learn ml-agents-configs/monster_config.yaml \
  --run-id=monster_tactical_v1 \
  --force-envs

# Phase 2: Fine-tuning with real player
mlagents-learn ml-agents-configs/monster_config.yaml \
  --run-id=monster_tactical_v1_finetuned \
  --initialize-from=results/monster_tactical_v1

# Monitor progress
tensorboard --logdir=results/

# Copy model to game
Copy-Item "results/monster_tactical_v1/RLMonster.onnx" `
  -Destination "Assets/Models/RLMonster_PreTrained.onnx"
```

---

## Timeline Ước Tính

```
Day 1:
  09:00 - Setup Training Scene + PlayerBotAI
  10:00 - Start Phase 1 training
  15:00 - 400k steps done, see tactical behavior emerge

Day 2:
  09:00 - Phase 1 done (1M steps), save checkpoint
  10:00 - Deploy model to game
  12:00 - Start Phase 2 fine-tuning with real player
  14:00 - See adaptation to real player pattern

Result: Monsters thực sự tactical + adaptive!
```

---

## Files Structure

```
Project/
├── Assets/
│   ├── Scripts/
│   │   ├── RL/
│   │   │   ├── Agents/RLMonsterAgent.cs (Phase 1+2)
│   │   │   └── Training/PlayerBotAI.cs (Phase 1 ONLY)
│   │   └── Character/ (Phase 2 ONLY)
│   ├── Models/
│   │   └── RLMonster_PreTrained.onnx (Phase 2)
│   └── Scenes/
│       ├── TrainingScene.unity (Phase 1)
│       └── GameScene.unity (Phase 2)
│
├── ml-agents-configs/
│   └── monster_config.yaml
│
└── results/
    ├── monster_tactical_v1/ (Phase 1 output)
    ├── monster_tactical_v1_finetuned/ (Phase 2 output)
    └── events/ (TensorBoard logs)
```

---

## Conclusion

**Hybrid approach benefits:**

1. ✅ Controlled learning (Phase 1 với bot)
2. ✅ Real-world adaptation (Phase 2 với player)
3. ✅ Convergence to true tactical behavior
4. ✅ Monsters "remember" basics từ Phase 1 + learn real patterns Phase 2

**Monsters cuối cùng sẽ:**

- Rút lui khi nguy hiểm
- Maintain distance optimal
- Flank thành smart
- Phối hợp với nhau
- **ADAPT theo player behavior**

🎮 **Kết quả**: Monsters không chỉ tactical, mà còn "thông minh" thực sự!
