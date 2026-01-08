# RL Monster Visualizer - Hướng Dẫn Sử Dụng

## Tổng Quan

`RLMonsterVisualizer` là một hệ thống hiển thị hành vi học tăng cường của quái vật trong thời gian chạy. **Visualizer chỉ xuất hiện khi quái vật đang được điều khiển bởi hệ thống RL**.

### Yêu Cầu Chính

- ✅ Hiển thị CHỈ khi quái vật được RL điều khiển (`IsControlling = true`)
- ✅ Ẩn hoàn toàn khi quái vật không được RL điều khiển (AI thường, script, FSM, v.v.)
- ✅ Không ảnh hưởng đến hiệu suất game

## Cách Thiết Lập

### 1. Thêm Visualizer vào RLMonsterAgent

```csharp
// RLMonsterAgent sẵn có thành phần này, không cần cấu hình thêm
public class RLMonsterAgent : Agent
{
    // ...
    public bool IsControlling => isControlling;  // RL điều khiển?
    public int CurrentAction => currentAction;    // Hành động hiện tại
}
```

### 2. Tự động Attach Visualizer

Visualizer sẽ tự động tìm `RLMonsterAgent` trên cùng GameObject:

```csharp
// Thêm script này vào prefab của RL Monster:
// Assets/Scripts/RL/Visualization/RLMonsterVisualizer.cs
```

## Tính Năng Hiển Thị

### 1. Action Label (Nhãn Hành Động)

**Hiển thị:** Tên hành động RL hiện tại

```
AGGRESSIVE    (Tấn công)
MAINTAIN      (Giữ khoảng cách)
RETREAT       (Rút lui)
FLANK         (Đánh vòng)
WAIT          (Chờ đợi)
```

**Màu sắc:**

- 🔴 AGGRESSIVE = Đỏ
- 🟡 MAINTAIN = Vàng
- 🟣 RETREAT = Tím
- 🔵 FLANK = Xanh lam
- 🟢 WAIT = Xanh lá

**Hoạt động:** Nhãn sáng lên khi hành động thay đổi, tự động mờ đi sau ~0.15 giây

### 2. Health Bar (Thanh Máu)

**Vị trí:** Phía trên đầu quái vật

**Màu sắc theo HP:**

- 🟢 Xanh: HP > 50%
- 🟡 Vàng: 25% < HP ≤ 50%
- 🔴 Đỏ: HP ≤ 25%

### 3. RL Tint (Tint Màu RL)

**Màu:** Tint xanh lam nhạt trên sprite khi RL điều khiển

**Mục đích:** Phân biệt quái vật RL vs AI thường

### 4. Tactical Gizmos (Khi chọn GameObject)

**Gizmos hiển thị CHỈ trong Scene view:**

- 🔵 **Vòng tròn xanh lam (15m):** Phạm vi phát hiện
- 🟢 **Vòng tròn xanh (4m):** Phạm vi tối ưu
- 🔴 **Vòng tròn đỏ (2m):** Phạm vi nguy hiểm (quá gần)

Những gizmos này giúp hiểu rõ logic chiến thuật của RL.

## Cấu Hình

Trong Inspector của RLMonsterVisualizer:

```csharp
[Header("Visual Elements")]
public float decisionIndicatorDuration = 0.15f;  // Thời gian nhãn sáng (giây)
public float healthBarOffset = 1.2f;             // Vị trí thanh máu (trên đầu)

[Header("UI")]
public bool showActionLabel = true;              // Hiển thị nhãn hành động?
public bool showHealthBar = true;                // Hiển thị thanh máu?
public bool showConfidence = false;              // Hiển thị độ tin cậy?
public bool showTacticalInfo = false;            // Hiển thị info chiến thuật?

[Header("Colors")]
public Color rlActiveColor = new Color(0.3f, 0.5f, 1f, 0.8f);  // Tint khi RL active
public Color actionAggressive = Color.red;       // Màu AGGRESSIVE
// ... các hành động khác
```

## Hành Vi Tự Động

### Khi RL Bắt Đầu Điều Khiển

1. Visualizer container kích hoạt
2. Sprite tint xanh lam
3. Health bar và action label xuất hiện
4. Gizmos hiển thị trong Scene view

### Khi RL Ngừng Điều Khiển

1. Visualizer container tắt
2. Sprite trở lại màu gốc
3. Tất cả UI ẩn đi
4. Gizmos biến mất

## Ví Dụ Sử Dụng - Demonstration cho Giảng Viên

```csharp
// Tệp: RLComparisonDemo.cs
// Spawn 2 nhóm quái vật: RL vs AI thường

public class RLComparisonDemo : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(DemoComparison());
    }

    IEnumerator DemoComparison()
    {
        yield return new WaitForSeconds(3f);

        // Bên TRÁI: Quái vật AI thường
        // - Sprite: Màu trắng (không tint)
        // - Hành vi: Đơn giản, chỉ đuổi player
        // - Visualizer: KHÔNG hiển thị
        for (int i = 0; i < 5; i++)
        {
            SpawnBasicMonster(new Vector3(-10, i * 2, 0));
        }

        // Bên PHẢI: Quái vật RL
        // - Sprite: Tint xanh lam
        // - Hành vi: Thông minh, có chiến thuật
        // - Visualizer: HIỂN THỊ action label, health bar, gizmos
        for (int i = 0; i < 5; i++)
        {
            SpawnRLMonster(new Vector3(10, i * 2, 0));
        }
    }
}
```

## Debug & Troubleshooting

### Visualizer Không Hiển Thị

**Nguyên nhân 1:** RLMonsterAgent không được tìm thấy

```csharp
// Kiểm tra: RLMonsterAgent phải trên cùng GameObject
if (!GetComponent<RLMonsterAgent>())
    Debug.LogWarning("RLMonsterAgent missing!");
```

**Nguyên nhân 2:** `IsControlling` là `false`

```csharp
// IsControlling chỉ = true khi:
// 1. RLMonsterAgent nhận OnActionReceived từ ML-Agents
// 2. Monster bắt đầu được điều khiển bởi neural network
```

### Màu Sprite Không Đúng

```csharp
// Kiểm tra originalSpriteColor được lưu
var viz = GetComponent<RLMonsterVisualizer>();
if (viz == null)
    Debug.LogError("Visualizer not initialized!");
```

### Gizmos Không Hiển Thị

1. **Chọn GameObject trong Hierarchy**
2. **Scene tab phải active** (không phải Game tab)
3. **Gizmos chỉ vẽ khi IsControlling = true**

## Performance

- **Visualizer lightweight:** Chỉ cập nhật UI khi RL điều khiển
- **No garbage allocation:** Reuses objects, không tạo GC pressure
- **WorldSpace Canvas:** Dùng WorldSpace thay vì ScreenSpace để tránh UI camera overhead

## Customization - Tuỳ Chỉnh Nâng Cao

### Thêm Action Mới

```csharp
// Trong RLMonsterVisualizer.cs
actionNames = new string[] {
    "AGGRESSIVE",
    "MAINTAIN",
    "RETREAT",
    "FLANK",
    "WAIT",
    "NEW_ACTION"  // Thêm vào đây
};

// Trong GetActionColor()
case 5: return Color.gray; // Màu cho action mới
```

### Thêm Confident Score Display

```csharp
// Trong RLMonsterAgent.cs, thêm:
public float CurrentConfidence { get; set; }

// Trong RLMonsterVisualizer.cs
if (showConfidence && confidenceUI)
{
    confidenceUI.text = $"Confidence: {rlAgent.CurrentConfidence:P0}";
}
```

## API

### RLMonsterAgent

```csharp
public bool IsControlling { get; }      // Đang được RL điều khiển?
public int CurrentAction { get; }       // Hành động hiện tại (0-4)
```

### RLMonsterVisualizer

```csharp
public string GetTacticalState()        // Lấy trạng thái chiến thuật (string)
// Trả về: "AGGRESSIVE | HP: 75%"
```

## Tóm Tắt - Quick Checklist

- ✅ Visualizer chỉ hiển thị khi `IsControlling = true`
- ✅ Tự động ẩn khi quái vật không được RL điều khiển
- ✅ Health bar, action label, và gizmos cập nhật real-time
- ✅ Không ảnh hưởng hiệu suất
- ✅ Hoạt động tự động, không cần cấu hình thêm
- ✅ Có thể custom màu sắc và UI trong Inspector
