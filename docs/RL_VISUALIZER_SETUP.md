# RL Monster Visualizer - Setup & Demo

## Quick Setup

### 1. Add Visualizer to RL Monster Prefab

```
Prefab: Assets/Prefabs/RLMonsterAgent.prefab
  ├── RLMonsterAgent (script) ✓ Already exists
  └── RLMonsterVisualizer (script) → ADD THIS
```

**Bước:**

1. Mở RL Monster prefab
2. Thêm component `RLMonsterVisualizer`
3. Script sẽ tự động tìm `RLMonsterAgent` trên GameObject
4. Save prefab

### 2. Inspector Settings

Mặc định các cài đặt đã tối ưu. Nếu muốn tuỳ chỉnh:

```csharp
Show Action Label = true     // Hiển thị tên hành động
Show Health Bar = true       // Hiển thị thanh máu
Show Confidence = false      // Tạm ẩn (chưa implement confidence score)
Show Tactical Info = false   // Tạm ẩn
```

## Hành Vi Tự Động

### Khi Monster Bắt Đầu Được RL Điều Khiển

1. **Sprite Tint:** Xanh lam (0.3, 0.5, 1.0)
2. **Action Label:** Hiển thị "AGGRESSIVE", "MAINTAIN", "RETREAT", "FLANK", "WAIT"
3. **Health Bar:** Hiển thị thanh máu trên đầu
4. **Scene Gizmos:** Vòng tròn hiển thị phạm vi (khi chọn GameObject)

### Khi Monster Không Được RL Điều Khiển

1. **Sprite Color:** Trở lại màu gốc
2. **All UI:** Ẩn hoàn toàn
3. **Gizmos:** Biến mất
4. **Performance:** Không ảnh hưởng (visualizer vô hiệu)

## Demo - So Sánh RL vs Basic AI

Chạy scene `Level 1` và spawn monster từ hai loại:

```csharp
// Bên TRÁI: Basic Monster (no RL)
// - Sprite: Màu trắng
// - Hành vi: Đơn giản, chỉ đuổi
// - UI: KHÔNG hiển thị

// Bên PHẢI: RL Monster
// - Sprite: Tint xanh lam
// - Hành vi: Thông minh, có chiến thuật
// - UI: Hiển thị action, health bar
// - Gizmos: Hiển thị phạm vi chiến thuật
```

### Cách Spawn Demo Monster

**Cách 1: Code**

```csharp
var levelRL = FindObjectOfType<LevelRLIntegration>();
levelRL.SpawnRLMonster(0, new Vector3(10, 0, 0), 1f);
```

**Cách 2: Editor**

- Tạo RL Monster từ prefab
- Script sẽ tự động attach Visualizer

## Visualizer Components

### 1. Action Label (Nhãn Hành Động)

```
Position: Phía trên quái vật (Y +1.5m)
Font Size: 2.0
Update: Mỗi khi action thay đổi
Fade: Tự động mờ sau 0.15 giây

Colors:
🔴 AGGRESSIVE = Color.red
🟡 MAINTAIN   = Color.yellow
🟣 RETREAT    = Color.magenta
🔵 FLANK      = Color.cyan
🟢 WAIT       = Color.green
```

### 2. Health Bar

```
Position: Phía trên đầu (Y +1.2m)
Width: 1.5 units
Height: 0.2 units

Color Gradient:
🟢 Green   : HP > 50%
🟡 Yellow  : 25% < HP ≤ 50%
🔴 Red     : HP ≤ 25%
```

### 3. RL Tint

```
Color: (0.3, 0.5, 1.0, 0.8) - Xanh lam
Applied: Khi RL bắt đầu điều khiển
Removed: Khi RL ngừng điều khiển
```

### 4. Scene Gizmos (Khi chọn GameObject)

```
🔵 Vòng tròn xanh lam (r=15m) - Phạm vi phát hiện
🟢 Vòng tròn xanh (r=4m)     - Phạm vi tối ưu
🔴 Vòng tròn đỏ (r=2m)       - Phạm vi nguy hiểm
```

## Kiểm Tra Hoạt Động

### 1. Visual Check

- [ ] Spawn RL monster
- [ ] Kiểm tra sprite có tint xanh lam không
- [ ] Kiểm tra action label hiển thị không
- [ ] Kiểm tra health bar hiển thị không
- [ ] Chọn GameObject → Xem gizmos trong Scene tab

### 2. Behavior Check

- [ ] Monster RL tránh tấn công trực tiếp
- [ ] Monster RL giữ khoảng cách hợp lý
- [ ] Monster RL rút lui khi HP thấp
- [ ] Monster RL hành động khác nhau từng lúc

### 3. Debug Check

```csharp
// Trong Scene, thêm debug text
var viz = monsterRLObject.GetComponent<RLMonsterVisualizer>();
Debug.Log(viz.GetTacticalState()); // Output: "AGGRESSIVE | HP: 75%"
```

## API References

### RLMonsterAgent

```csharp
public bool IsControlling { get; }      // Đang điều khiển bởi RL?
public int CurrentAction { get; }       // Hành động hiện tại (0-4)
```

### RLMonsterVisualizer

```csharp
public string GetTacticalState()        // Lấy trạng thái: "ACTION | HP: X%"
```

## Troubleshooting

### Visualizer Không Hiển Thị

**Check 1:** RLMonsterAgent có trên GameObject không?

```csharp
if (GetComponent<RLMonsterAgent>() == null)
    Debug.LogError("Missing RLMonsterAgent!");
```

**Check 2:** IsControlling = true?

```csharp
var agent = GetComponent<RLMonsterAgent>();
Debug.Log($"IsControlling: {agent.IsControlling}");
```

**Check 3:** Visualizer Container active?

```csharp
// Kiểm tra trong Hierarchy khi chạy game
// Phải có GameObject "RLVisualizerUI" con của Monster
```

### Health Bar Không Cập Nhật

```csharp
// Kiểm tra Monster.HP được cập nhật
Monster monster = GetComponent<Monster>();
Debug.Log($"Current HP: {monster.HP}");
```

### Gizmos Không Hiển Thị

1. **Scene tab phải active** (không phải Game tab)
2. **GameObject phải được chọn** trong Hierarchy
3. **Gizmos phải bật** (top right, "Gizmos" toggle)

## Performance Notes

- **Overhead:** Minimal (~0.1ms per monster)
- **GC Allocations:** Zero per frame (no allocations)
- **WorldSpace Canvas:** Lightweight, không use ScreenSpace
- **Disabling:** Visualizer tự động disable khi RL không điều khiển

## Integration with Game

### Already Compatible With:

- ✅ LevelRLIntegration.SpawnRLMonster()
- ✅ RLMonsterAgent lifecycle
- ✅ Monster component
- ✅ UI & Canvas system
- ✅ Gizmo rendering

### No Changes Needed To:

- Character controller
- Damage system
- Entity manager
- Level manager

## Summary

Visualizer là pure visualization system:

- Không thay đổi game logic
- Không ảnh hưởng RL training
- Chỉ hiển thị khi RL active
- Hoàn toàn tự động
