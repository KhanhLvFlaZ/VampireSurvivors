# Huong dan tich hop RL cho Monster

Tai lieu nay huong dan cach dua he thong Reinforcement Learning (RL) vao monster trong game. Chi tiet duoc viet ngan gon de thuc hien nhanh trong scene thuc te.

## 1. Dieu kien tien quyet

- Co thu muc `Assets/Scripts/RL/` va cac thanh phan: `RLSystem`, `LevelRLIntegration`, `RLMonster`, `DQNLearningAgent`, `PerformanceMonitor`.
- Scene co san `LevelManager`, `EntityManager`, `Character` (nguoi choi), va cac prefab monster.

## 2. Them RL vao scene

1. Tao GameObject moi ten "RL_Integration".
2. Gan component `LevelRLIntegration` vao doi tuong nay.
3. Keo tham chieu `LevelManager`, `EntityManager`, `Character` vao inspector neu co.
4. Bat `Enable RL For Level` va chon `Level Training Mode`:
   - `Training`: cho phep thu thap du lieu va train.
   - `Inference`: chi suy luan tu mo hinh da train.
   - `Mixed`: vua train vua inference.
5. Dat `Update Interval Ms` (mac dinh 16ms ~ 60 FPS).

## 3. Cau hinh RLSystem tu code (tuy chon)

```csharp
// Vi du khoi tao trong script khoi dong level
var rlSystemGO = new GameObject("RLSystem_Runtime");
var rlSystem = rlSystemGO.AddComponent<RLSystem>();
rlSystem.Initialize(playerCharacter, "player_profile");
```

## 4. Spawn monster co RL

Dung API cua `LevelRLIntegration` de spawn:

```csharp
// spawnIndex la chi so blueprint RLMonster trong inspector cua LevelRLIntegration
public void SpawnRLAgent(LevelRLIntegration rl, int spawnIndex, Vector3 pos)
{
    var agent = rl.SpawnRLMonster(spawnIndex, pos, hpMultiplier: 1.0f);
    if (agent == null)
        Debug.LogWarning("Spawn RL monster that bai");
}
```

Luu y: `SpawnRLMonster` hien tai dung `MonsterType.Melee` lam mac dinh. Dieu chinh theo nhu cau neu ban mo rong blueprint.

## 5. Che do Training vs Inference

- **Training**: thu thap experience, cap nhat exploration cao. Dung khi chay offline hoac sandbox.
- **Inference**: khong ghi log train, quyet dinh mang tinh xac dinh hon. Dung cho gameplay chinh.
- Chuyen che do: trong inspector `LevelRLIntegration` > `Level Training Mode` hoac goi `trainingCoordinator.SetTrainingMode(...)` neu ban su dung RLSystem truc tiep.

## 6. Theo doi hieu nang

- Su dung `PerformanceMonitor` (tu dong tao neu chua co) de xem metric: inference time, memory.
- Goi `GetRLMonstersVisualStatus()` tu `LevelRLIntegration` de lay thong tin trang thai agent (vi tri, action cuoi, confidence) va ve overlay neu can.
- Giup kiem tra khi FPS giam: giam so agent RL hoac tang `updateIntervalMs`.

## 7. Chay bo test tich hop

- Them component `Task13FinalIntegrationTestRunner` vao GameObject bat ky trong scene.
- Check "Run All Tests On Start" hoac chuot phai chon "Run Complete Task 13 Final Integration Tests".
- Theo doi console: tat ca phase phai PASS.

## 8. Xu ly su co thuong gap

- **Character khong tim thay**: them `using Vampire;` va dam bao Scene co doi tuong `Character`.
- **RLSystem null**: goi `Initialize` sau khi co tham chieu nguoi choi.
- **Hieu nang thap**: giam so RL agents, bat quantization, tang update interval.
- **No action/agent dung**: kiem tra ActionSpace va `RLMonster.Initialize` duoc goi.

## 9. Loi khuyen tich hop nhanh

- Bat dau o che do `Inference` de kiem tra on dinh, sau do moi thu `Training`.
- Spawn it agent RL truoc (1-2 con) de theo doi hieu nang.
- Luon kiem tra console va PerformanceMonitor khi thay doi cau hinh.

## 10. Duong dan file chinh

- Tich hop: [Assets/Scripts/RL/Integration/LevelRLIntegration.cs](Assets/Scripts/RL/Integration/LevelRLIntegration.cs)
- He thong RL: [Assets/Scripts/RL/RLSystem.cs](Assets/Scripts/RL/RLSystem.cs)
- Agent: [Assets/Scripts/RL/Agents/DQNLearningAgent.cs](Assets/Scripts/RL/Agents/DQNLearningAgent.cs)
- Test runner: [Assets/Scripts/RL/Tests/Task13FinalIntegrationTestRunner.cs](Assets/Scripts/RL/Tests/Task13FinalIntegrationTestRunner.cs)
- Hieu nang: [Assets/Scripts/RL/Integration/PerformanceValidator.cs](Assets/Scripts/RL/Integration/PerformanceValidator.cs)

Chuc ban tich hop thanh cong va giu FPS on dinh!
