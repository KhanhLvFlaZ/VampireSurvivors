# Hướng Dẫn Tạo Training Scene Cho RLMonsterAgent

## Bước 1: Chuẩn Bị (Pre-requisites)

Kiểm tra các component/asset sau sẵn có:

- ✅ RLMonsterAgent.cs script
- ✅ monster_config.yaml configuration
- ✅ ML-Agents package installed
- ✅ Character prefab (Player)
- ✅ Rigidbody 2D components
- ✅ Colliders 2D

---

## Bước 2: Tạo Scene Mới

1. **File → New Scene**
2. **Tên**: `TrainingScene` (hoặc `RLMonsterTraining`)
3. **Save** vào `Assets/Scenes/`

---

## Bước 3: Setup Cảnh

### 3.1 Tạo Background/Gameplay Area

```
Hierarchy → Create Empty → Rename: "GameplayArea"
  - Position: (0, 0, 0)
  - Scale: (1, 1, 1)

Add Components:
  - Sprite Renderer (để visualize)
  - Box Collider 2D (Wall)
  - Set size: (20, 20)
  - Mark as Trigger: FALSE
```

### 3.2 Setup Camera

```
Main Camera:
  - Position: (0, 0, -10)
  - Orthographic: TRUE
  - Size: 10
  - Background: #1a1a1a (dark)
```

---

## Bước 4: Tạo Player Character

### 4.1 Create Player GameObject

```
Hierarchy → Create Empty → Rename: "Player"
  - Add Sprite Renderer (small circle)
  - Add Circle Collider 2D (radius: 0.3)
  - Add Rigidbody 2D:
    - Body Type: Dynamic
    - Gravity Scale: 0
    - Constraints: Freeze Rotation Z
```

### 4.2 Link Character Script

```
Add Component: Character (nếu có)
  Hoặc tạo Simple Player Script:

using UnityEngine;

public class SimplePlayer : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody2D rb;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float y = Input.GetAxis("Vertical");
        rb.velocity = new Vector2(x, y) * moveSpeed;
    }
}
```

---

## Bước 5: Tạo RLMonsterAgent Instances (10-15 cái)

### 5.1 Tạo Base Prefab

**Tạo Monster GameObject:**

```
Hierarchy → Create Empty → Rename: "RLMonster_Prefab"
  - Position: (0, 1, 0)

Add Components:
  ✓ Sprite Renderer (enemy sprite hoặc simple circle)
  ✓ Circle Collider 2D (radius: 0.4)
  ✓ Rigidbody 2D:
    - Body Type: Dynamic
    - Gravity Scale: 0
    - Collision Detection: Continuous
    - Constraints: Freeze Rotation Z

  ✓ RLMonsterAgent script
  ✓ Decision Requester (ML-Agents)
  ✓ Behavior Parameters (ML-Agents)
```

### 5.2 Cấu Hình RLMonsterAgent Component

Trong Inspector của RLMonsterAgent:

```
Monster References:
  - Base Monster: (để trống, optional)

Stats:
  - Max HP: 100
  - Move Speed: 3

Tactical Settings:
  - Max Detection Range: 15
  - Optimal Range: 4

Allies Tracking:
  - Ally Check Radius: 10

Debug:
  - Show Debug Gizmos: TRUE (để debug)
```

### 5.3 Cấu Hình Decision Requester

```
Decision Period: 10
Take Actions: TRUE
Take Observations: TRUE
```

### 5.4 Cấu Hình Behavior Parameters

```
Behavior Name: RLMonster (⚠️ PHẢI MATCH Config YAML)
Vector Observation:
  Space Size: 7

Discrete Action: TRUE
  - Action Size: 5 (5 tactical behaviors)

Model: (để trống, sẽ training)
Inference Device: CPU
```

### 5.5 Save as Prefab

```
Drag "RLMonster_Prefab" từ Hierarchy vào Assets/Prefabs/
→ "RLMonsterAgent.prefab"

Xóa từ Hierarchy (vì đã save prefab)
```

---

## Bước 6: Spawn 10-15 Monsters

### Option A: Manual Spawn (Nhanh, dễ debug)

```csharp
using UnityEngine;

public class RLMonsterTrainingSpawner : MonoBehaviour
{
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private Transform player;
    [SerializeField] private int monsterCount = 12;
    [SerializeField] private float spawnRadius = 15f;

    void Start()
    {
        SpawnMonsters();
    }

    void SpawnMonsters()
    {
        for (int i = 0; i < monsterCount; i++)
        {
            // Random position trong circle
            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;

            // Spawn từ prefab
            GameObject monster = Instantiate(
                monsterPrefab,
                randomPos,
                Quaternion.identity
            );

            // Rename for clarity
            monster.name = $"RLMonster_{i}";

            Debug.Log($"Spawned monster {i} at {randomPos}");
        }
    }
}
```

### Option B: Script + Scene Setup

1. **Create Empty** → Rename: "TrainingManager"
2. **Add Component** → RLMonsterTrainingSpawner (script trên)
3. **Assign fields:**

   - Monster Prefab: RLMonsterAgent.prefab
   - Player: Player GameObject
   - Monster Count: 12
   - Spawn Radius: 15

4. **Play Scene** → 12 monsters sẽ spawn

---

## Bước 7: Environment Setup (Optional)

### 7.1 Boundaries (để monsters không chạy ra ngoài)

```
Create → Quad → Rename: "Boundary_Left"
  - Position: (-12, 0, 0)
  - Scale: (1, 20, 1)
  - Add Box Collider 2D
  - Collision Layer: Wall

Duplicate 3 lần cho 4 bức tường (Left, Right, Top, Bottom)
```

### 7.2 Ground Layer (Optional)

```
Create → Quad → Rename: "Ground"
  - Scale: (20, 20, 1)
  - Z Position: 0.1
  - Set Color: Grey
  - Add Box Collider 2D (isTrigger: FALSE)
```

---

## Bước 8: Test Training Setup

### 8.1 Verify Scene Setup

```csharp
// Checklist:
□ Player GameObject exists
□ Player has Rigidbody2D + Collider
□ 12-15 RLMonster instances exist
□ Each RLMonster has:
  - RLMonsterAgent script
  - Decision Requester
  - Behavior Parameters (name="RLMonster")
  - Rigidbody2D + Collider
□ Behavior Parameters action space = 5 (discrete)
□ Behavior Parameters observation space = 7
□ monster_config.yaml in ml-agents-configs/
```

### 8.2 Run Headless (No Graphics)

```bash
# Terminal 1: Unity Editor
Unity.exe -projectPath "path\to\VampireSurvivors" \
  --headless --nographics \
  -logFile -

# Terminal 2: Training
cd C:\Users\khoil\UnityProjects\VampireSurvivors
mlagents-learn ml-agents-configs/monster_config.yaml \
  --run-id=monster_tactical_v1
```

### 8.3 Monitor Training (TensorBoard)

```bash
# Terminal 3
tensorboard --logdir=results/
# Open: http://localhost:6006
```

---

## Bước 9: Scene Hierarchy (Final)

```
TrainingScene
├── Main Camera
├── GameplayArea
│   ├── Boundary_Left (Wall)
│   ├── Boundary_Right (Wall)
│   ├── Boundary_Top (Wall)
│   ├── Boundary_Bottom (Wall)
│   └── Ground
├── Player
│   ├── Sprite Renderer
│   ├── Rigidbody 2D
│   └── Circle Collider 2D
├── TrainingManager
│   └── RLMonsterTrainingSpawner script
├── RLMonster_0 (Instance)
│   ├── Sprite Renderer
│   ├── Rigidbody 2D
│   ├── Circle Collider 2D
│   ├── RLMonsterAgent
│   ├── Decision Requester
│   └── Behavior Parameters
├── RLMonster_1 ... RLMonster_11
```

---

## Bước 10: Quick Start Commands

```bash
# 1. Activate Python environment
cd C:\Users\khoil\UnityProjects\VampireSurvivors
.\.venv\Scripts\Activate.ps1

# 2. Start training
mlagents-learn ml-agents-configs/monster_config.yaml \
  --run-id=monster_tactical_v1 \
  --force-envs

# 3. Open TensorBoard (new terminal)
tensorboard --logdir=results/

# 4. Start Unity
# Open TrainingScene in Unity Editor
# Click Play button

# Training will auto-connect!
```

---

## Troubleshooting

| Problem                  | Solution                                            |
| ------------------------ | --------------------------------------------------- |
| "Behavior name mismatch" | Behavior Parameters name = "RLMonster"              |
| "Can't connect to Unity" | Unity scene name != config yaml name, check console |
| Low reward signal        | Adjust reward values in RLMonsterAgent              |
| Monsters stack up        | Check colliders, boundaries                         |
| Training too slow        | Reduce Monster Count or use `--force-envs`          |
| Out of memory            | Reduce batch_size in config (256→128)               |

---

## Expected Training Output

```
EpisodeStatistics:
  monster_group => Episode Length: 500.2, Cumulative Reward: 45.3

Step: 100000
  Loss: 0.234
  Entropy: 0.891
  Learning Rate: 1.8e-4

Step: 200000
  Policy improved! Agents becoming tactical.
  Monsters now retreat when low HP ✓
  Monsters now maintain distance ✓
```

---

## Next Steps

1. **Monitor training** 1-2 giờ
2. **Check behavior evolution**:
   - 0-100k steps: Random movement
   - 100k-400k: Learning tactical basics
   - 400k-1M: Refined tactical behavior
3. **Save checkpoint** nếu hài lòng
4. **Deploy model** vào game scene

---

**Estimated Time**:

- Setup scene: 30 phút
- Training: 3-5 giờ
- Total: ~4-6 giờ từ zero đến tactical monsters
