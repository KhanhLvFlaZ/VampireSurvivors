# Kế Hoạch Tích Hợp Unity ML-Agents Cho Monster

## Vấn Đề Với Hướng Tiếp Cận Cũ

**Monster hiện tại đã có AI scripted tốt với pathfinding tối ưu.**  
→ Nếu chỉ dùng RL để học lại hành vi "đuổi thẳng" = **KHÔNG TẠO RA KHÁC BIỆT**

## Mục Tiêu Mới

**Tạo hành vi RL mà scripted AI khó/không hợp lý làm:**  
→ Monster thể hiện **tactical intelligence** thay vì chỉ đuổi thẳng

---

## Hành Vi RL Đặc Trưng: TACTICAL SPACING & RISK ASSESSMENT

### 1. Tactical Spacing (Giữ Khoảng Cách Tối Ưu)

**Hành vi:**

- **Melee Monster**: Tiến lại gần vừa đủ để đánh, không sát quá (tránh bị AOE/反擊)
- **Ranged Monster**: Giữ khoảng cách an toàn, rút lui nếu player đến gần
- **Kiting**: Tấn công → rút lui → tấn công lại (hit-and-run)

**Vì sao chỉ RL làm tốt:**

- Khoảng cách tối ưu khác nhau cho mỗi weapon type của player
- Rule-based cần hardcode threshold cho từng case → 20+ if-else
- RL tự discover optimal range qua training (agent "cảm nhận" được vùng nguy hiểm)

**Khác biệt thực tế:**
| Scripted AI | RL Agent |
|--------------|----------|
| Lao thẳng vào, dính hết AOE | Nhảy lùi khi player nạp skill AOE |
| Ranged đứng yên bắn | Ranged vừa bắn vừa di chuyển giữ khoảng cách |
| Pattern cố định | Adapt theo weapon/skill player đang dùng |

### 2. Risk Assessment (Đánh Giá Nguy Cơ)

**Hành vi:**

- **HP cao (>70%) + nhiều đồng đội**: Aggressive, lao vào
- **HP thấp (<30%)**: Rút lui, chạy vòng, chờ đồng đội
- **Player có nhiều weapon mạnh**: Thận trọng hơn, không lao thẳng
- **Isolated (một mình)**: Cẩn thận, kéo player vào đám đông

**Vì sao chỉ RL làm tốt:**

- Quyết định dựa trên nhiều yếu tố động: HP, vị trí đồng đội, weapon player, terrain
- Rule-based: "If HP < 30% → retreat" quá đơn giản, không optimal
- RL: Học được timing chính xác (khi nào nên retreat, khi nào nên all-in)

**Khác biệt thực tế:**
| Scripted AI | RL Agent |
|--------------|----------|
| HP 10% vẫn lao vào | HP thấp thì "rùn", chạy quanh player |
| Không quan tâm đồng đội | Đợi đồng đội tới rồi mới tấn công đồng loạt |
| Luôn đuổi player | Nếu player quá mạnh, giữ khoảng cách, đợi hỗ trợ |

---

## Tại Sao Không Dùng Rule-Based?

### Ví Dụ: Tactical Spacing

**Rule-based approach (phức tạp, cứng nhắc):**

```csharp
if (playerWeapon == "Whip" && distance < 3f)
    Retreat(); // Whip có range 3
else if (playerWeapon == "Garlic" && distance < 2f)
    Retreat(); // Garlic AOE 2 units
else if (playerWeapon == "Axe" && distance < 5f && playerIsAttacking)
    Dodge(); // Axe projectile
// ... 20+ cases cho mỗi weapon
// ... Không handle được combination (Whip + Garlic)
```

**RL approach (flexible, emergent):**

```csharp
// Observation:
// - distance to player
// - player weapon power
// - recent damage taken
// - ally positions
// → Agent tự học optimal distance cho MỌI tình huống
// → Behavior emergent từ reward function
```

### Ví Dụ: Risk Assessment

**Rule-based:**

```csharp
if (HP < 30f)
    Retreat();
else if (nearbyAllies > 2)
    Aggressive();
else
    Normal();
// → Không học từ kinh nghiệm
// → Không adapt theo player skill
```

**RL:**

```csharp
// Agent quan sát:
// - HP ratio
// - Ally count
// - Player damage output (observed qua episodes)
// - Win/loss history
// → Học được: "Player này mạnh, cần 3 allies mới all-in"
// → Học được: "HP 40% với player này = nguy hiểm, phải retreat"
```

---

## Implementation Plan (Tối Giản)

---

## Bước 1: Setup ML-Agents Package

- Import ML-Agents Unity Package (com.unity.ml-agents) qua Package Manager
- Tạo script `RLMonsterAgent.cs` kế thừa từ `Agent`

## Bước 2: Observations (7 giá trị cho Tactical Behavior)

```csharp
public override void CollectObservations(VectorSensor sensor)
{
    // SPATIAL AWARENESS
    sensor.AddObservation(directionToPlayer.normalized); // 2 floats: hướng đến player
    sensor.AddObservation(distanceToPlayer / maxRange);   // 1 float: khoảng cách 0-1

    // RISK ASSESSMENT
    sensor.AddObservation(currentHP / maxHP);             // 1 float: HP ratio
    sensor.AddObservation(nearbyAlliesCount / 5f);        // 1 float: số đồng đội gần đó (max 5)

    // TACTICAL INFO
    sensor.AddObservation(playerDamageRate);              // 1 float: damage/s player gây ra gần đây
    sensor.AddObservation(timeSinceLastDamaged);          // 1 float: thời gian từ lần bị đánh cuối
}
// TOTAL: 7 observations
```

**Tại sao các observation này:**

- `directionToPlayer`, `distanceToPlayer`: Cần cho mọi quyết định di chuyển
- `currentHP`, `nearbyAlliesCount`: Cho **Risk Assessment** (lao vào hay rút lui)
- `playerDamageRate`: Agent "nhớ" player mạnh/yếu → adjust aggression
- `timeSinceLastDamaged`: Tránh vùng nguy hiểm (vừa bị đánh = vùng đó nguy hiểm)

## Bước 3: Actions (5 discrete actions - thêm Tactical Actions)

```csharp
public override void OnActionReceived(ActionBuffers actions)
{
    int tacticalAction = actions.DiscreteActions[0]; // 0-4: tactical decisions

    switch(tacticalAction)
    {
        case 0: // AGGRESSIVE - Lao thẳng vào player
            MoveTowards(playerPosition, fullSpeed);
            break;

        case 1: // MAINTAIN_DISTANCE - Giữ khoảng cách tối ưu (3-5 units)
            if (distanceToPlayer < optimalRange)
                MoveAway(playerPosition, normalSpeed);
            else if (distanceToPlayer > optimalRange + 2f)
                MoveTowards(playerPosition, normalSpeed);
            break;

        case 2: // RETREAT - Rút lui khi nguy hiểm
            MoveAway(playerPosition, fullSpeed);
            break;

        case 3: // FLANK - Di chuyển vòng quanh player (perpendicular)
            Vector2 perpDir = new Vector2(-dirToPlayer.y, dirToPlayer.x);
            Move(perpDir, normalSpeed);
            break;

        case 4: // WAIT - Đợi đồng đội (di chuyển chậm, giữ khoảng cách)
            MoveTowards(playerPosition, slowSpeed);
            break;
    }
}
```

- Action Space Type: **Discrete(5)** - 5 tactical behaviors
- Mỗi action thể hiện 1 chiến thuật rõ ràng, dễ quan sát

## Bước 4: Rewards (Khuyến khích Tactical Behavior)

```csharp
void CalculateRewards()
{
    // === SURVIVAL & EFFECTIVENESS ===
    // +1.0: Gây damage lên player
    // +0.5: Survive thêm 10 giây (khuyến khích sống lâu)

    // === TACTICAL SPACING ===
    // +0.02: Ở trong optimal range (3-5 units) - không quá gần, không quá xa
    // -0.01: Quá gần player (<2 units) khi HP thấp → dễ chết
    // -0.01: Quá xa player (>10 units) → không tham gia combat

    // === RISK ASSESSMENT ===
    // +0.05: Retreat khi HP < 30% và không có allies gần
    // +0.03: Aggressive khi HP > 70% và có 2+ allies
    // -0.1: Lao vào khi HP < 20% (suicide)

    // === COORDINATION ===
    // +0.02: Có 2+ allies trong range 5 units (đang bao vây)
    // -0.01: Isolated (không có ally nào trong 10 units)

    // === DEATH PENALTY ===
    // -2.0: Bị player giết (penalty cao → học tránh chết)

    // End Episode:
    // - OnDeath() → EndEpisode()
    // - MaxStep = 10000 (episode dài hơn để học tactical)
}
```

**Tại sao reward này khác:**

- Không chỉ reward cho "đuổi gần player" (scripted AI đã làm tốt)
- Reward cho **survival + damage**, không chỉ damage → học cách sống sót
- Penalty cho hành vi "ngu" (lao vào khi sắp chết, đứng xa quá)
- Reward cho coordination → emergent behavior (bao vây)

## Bước 5: Training Configuration

Tạo file `monster_config.yaml`:

```yaml
behaviors:
  RLMonster:
    trainer_type: ppo
    hyperparameters:
      batch_size: 256 # Tăng lên vì action space phức tạp hơn
      buffer_size: 10240 # Buffer lớn để học từ nhiều tình huống
      learning_rate: 2.0e-4 # Chậm hơn để stable khi học tactical
      beta: 1.0e-2 # Entropy cao hơn → exploration nhiều hơn
      epsilon: 0.2
      lambd: 0.95
      num_epoch: 3
      learning_rate_schedule: linear
    network_settings:
      normalize: true # Normalize observations quan trọng cho tactical
      hidden_units: 256 # Network lớn hơn cho tactical decisions
      num_layers: 3 # Thêm 1 layer cho complex behavior
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
      curiosity: # Thêm curiosity để explore tactical options
        strength: 0.02
        gamma: 0.99
        encoding_size: 128
    max_steps: 1000000 # Training lâu hơn cho tactical behavior
    time_horizon: 128 # Dài hơn để agent "nhớ" tactical decisions
    summary_freq: 10000
```

**Tại sao config này khác:**

- Buffer lớn + time_horizon dài: Agent nhớ được hậu quả tactical decisions
- Curiosity reward: Khuyến khích explore các tactical options (flank, retreat, wait)
- Network lớn hơn: Cần cho complex decision-making

## Bước 6: Training Setup

- Tạo Training Scene với **10-15 monsters** + 1 player bot
- Player bot di chuyển ngẫu nhiên, tấn công khi monster gần
- Attach components:
  - `RLMonsterAgent` script
  - `Decision Requester` (Decision Period = **10** - tactical không cần quyết định mỗi frame)
  - `Behavior Parameters`: Behavior Name = "RLMonster", Space Type = **Discrete(5)**
- Run training: `mlagents-learn monster_config.yaml --run-id=monster_tactical_v1`

## Bước 7: Quan Sát Hành Vi Học (Tactical Evolution)

**Giai đoạn đầu (0-100k steps):**

- Monster ngẫu nhiên chọn actions (aggressive, retreat, flank)
- Chết nhiều vì lao vào khi HP thấp
- **Observation**: Hành vi loạn, không có chiến thuật

**Giai đoạn giữa (100k-400k steps):**

- Bắt đầu học: HP thấp → retreat nhiều hơn
- Nhóm lại gần nhau (discover coordination reward)
- Còn lao vào player quá gần (chưa optimal spacing)
- **Observation**: Thấy monster "rùn" khi sắp chết, nhóm đám đông

**Giai đoạn cuối (400k-1000k steps):**

- **Tactical Spacing**: Monster giữ khoảng cách 3-5 units, không lao sát
- **Risk Assessment**: HP < 30% thì chạy vòng, đợi HP hồi (nếu có mechanic)
- **Coordination**: Bao vây player từ nhiều hướng, không đuổi thẳng
- **Kiting**: Ranged monster vừa bắn vừa lùi, giữ khoảng cách
- **Observation**: Monster "thông minh", như người chơi PvP

## Bước 8: Testing & Comparison

**Setup test:**

- 1 scene với 50% Scripted AI, 50% RL Agent
- Player thực tế chơi, so sánh hành vi

**Checklist hành vi RL đặc trưng:**

- [ ] Monster rút lui khi HP thấp (scripted luôn lao vào)
- [ ] Monster giữ khoảng cách, không lao sát player liên tục
- [ ] Monster nhóm lại trước khi tấn công (scripted đuổi riêng lẻ)
- [ ] Ranged monster kite (vừa bắn vừa lùi)
- [ ] Monster flank (đi vòng) thay vì đuổi thẳng
- [ ] Hành vi thay đổi khi player mạnh lên (adapt)

**So sánh trực quan:**
| Metric | Scripted AI | RL Agent |
|--------|-------------|----------|
| Survive time (trung bình) | 15s | 25s (sống lâu hơn) |
| Damage dealt | 100 | 80 (ít hơn nhưng survive lâu) |
**So sánh trực quan:**
| Metric | Scripted AI | RL Agent |
|--------|-------------|----------|
| Survive time (trung bình) | 15s | 25s (sống lâu hơn) |
| Damage dealt | 100 | 80 (ít hơn nhưng survive lâu) |
| Player feeling | "Dễ đoán" | "Khó đoán, phải chú ý" |

---

## Code Template (Tactical Monster Agent)

```csharp
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Collections.Generic;

public class RLMonsterAgent : Agent
{
    [Header("References")]
    private Rigidbody2D rb;
    private Transform playerTransform;

    [Header("Stats")]
    public float maxHP = 100f;
    private float currentHP;
    public float moveSpeed = 3f;
    private float lastDamagedTime;

    [Header("Tactical Settings")]
    public float maxDetectionRange = 15f;
    public float optimalRange = 4f; // Khoảng cách tối ưu
    private float playerDamageRate; // Damage/s player gây ra
    private float damageReceivedRecently = 0f;
    private float damageTrackingWindow = 5f; // Track damage trong 5s

    [Header("Allies Tracking")]
    private List<RLMonsterAgent> nearbyAllies = new List<RLMonsterAgent>();
    private float allyCheckRadius = 10f;

    // Survival tracking
    private float episodeStartTime;
    private float lastSurvivalRewardTime;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHP = maxHP;
    }

    public override void OnEpisodeBegin()
    {
        // Reset vị trí ngẫu nhiên
        transform.localPosition = new Vector3(
            Random.Range(-10f, 10f),
            Random.Range(-10f, 10f),
            0f
        );

        // Reset stats
        currentHP = maxHP;
        lastDamagedTime = 0f;
        damageReceivedRecently = 0f;
        episodeStartTime = Time.time;
        lastSurvivalRewardTime = Time.time;

        // Tìm player gần nhất
        playerTransform = FindNearestPlayer();

        // Update nearby allies
        UpdateNearbyAllies();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (playerTransform == null)
        {
            // No player found, send zero observations
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            return;
        }

        // SPATIAL AWARENESS
        Vector2 dirToPlayer = (playerTransform.position - transform.position);
        float distance = dirToPlayer.magnitude;
        sensor.AddObservation(dirToPlayer.normalized); // 2 floats
        sensor.AddObservation(distance / maxDetectionRange); // 1 float

        // RISK ASSESSMENT
        sensor.AddObservation(currentHP / maxHP); // 1 float: HP ratio
        sensor.AddObservation(Mathf.Min(nearbyAllies.Count / 5f, 1f)); // 1 float: allies count

        // TACTICAL INFO
        sensor.AddObservation(playerDamageRate); // 1 float: player damage rate
        float timeSinceLastDamaged = Time.time - lastDamagedTime;
        sensor.AddObservation(Mathf.Clamp01(timeSinceLastDamaged / 5f)); // 1 float: normalized time

        // TOTAL: 7 observations
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (playerTransform == null) return;

        int tacticalAction = actions.DiscreteActions[0]; // 0-4

        Vector2 dirToPlayer = (playerTransform.position - transform.position).normalized;
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        Vector2 moveDirection = Vector2.zero;
        float speedMultiplier = 1f;

        switch(tacticalAction)
        {
            case 0: // AGGRESSIVE - Lao thẳng vào
                moveDirection = dirToPlayer;
                speedMultiplier = 1.2f;
                break;

            case 1: // MAINTAIN_DISTANCE - Giữ khoảng cách tối ưu
                if (distanceToPlayer < optimalRange)
                    moveDirection = -dirToPlayer; // Move away
                else if (distanceToPlayer > optimalRange + 2f)
                    moveDirection = dirToPlayer; // Move closer
                else
                    moveDirection = Vector2.zero; // Hold position
                speedMultiplier = 0.8f;
                break;

            case 2: // RETREAT - Rút lui
                moveDirection = -dirToPlayer;
                speedMultiplier = 1.0f;
                break;

            case 3: // FLANK - Di chuyển vòng
                Vector2 perpDir = new Vector2(-dirToPlayer.y, dirToPlayer.x);
                moveDirection = perpDir;
                speedMultiplier = 0.9f;
                break;

            case 4: // WAIT - Đợi đồng đội
                moveDirection = dirToPlayer;
                speedMultiplier = 0.5f;
                break;
        }

        // Apply movement
        rb.velocity = moveDirection * moveSpeed * speedMultiplier;

        // === REWARD CALCULATION ===
        CalculateTacticalRewards(tacticalAction, distanceToPlayer);
    }

    private void CalculateTacticalRewards(int action, float distance)
    {
        // TACTICAL SPACING reward
        bool inOptimalRange = distance >= optimalRange && distance <= (optimalRange + 2f);
        if (inOptimalRange)
            AddReward(0.02f); // Good spacing

        if (distance < 2f && currentHP < maxHP * 0.5f)
            AddReward(-0.01f); // Too close when low HP

        if (distance > 10f)
            AddReward(-0.01f); // Too far, not engaging

        // RISK ASSESSMENT reward
        float hpRatio = currentHP / maxHP;
        if (hpRatio < 0.3f && action == 2) // Retreat when low HP
            AddReward(0.05f);
        else if (hpRatio < 0.2f && action == 0) // Aggressive when critical HP
            AddReward(-0.1f); // Bad decision

        if (hpRatio > 0.7f && nearbyAllies.Count >= 2 && action == 0)
            AddReward(0.03f); // Aggressive when strong

        // COORDINATION reward
        if (nearbyAllies.Count >= 2)
            AddReward(0.02f); // Staying with group
        else if (nearbyAllies.Count == 0 && distance > 5f)
            AddReward(-0.01f); // Isolated and far

        // SURVIVAL reward (every 10 seconds)
        if (Time.time - lastSurvivalRewardTime > 10f)
        {
            AddReward(0.5f);
            lastSurvivalRewardTime = Time.time;
        }

        // Track player damage rate
        UpdatePlayerDamageRate();
    }

    // Call from damage system when this monster takes damage
    public void OnTakeDamage(float damage)
    {
        currentHP -= damage;
        lastDamagedTime = Time.time;
        damageReceivedRecently += damage;

        if (currentHP <= 0)
        {
            OnDeath();
        }
    }

    // Call when monster successfully damages player
    public void OnDamagePlayer(float damage)
    {
        AddReward(1.0f); // Big reward for damage
    }

    // Call when monster dies
    public void OnDeath()
    {
        AddReward(-2.0f); // Big penalty for dying
        EndEpisode();
    }

    private void UpdatePlayerDamageRate()
    {
        // Calculate damage/s in recent window
        playerDamageRate = damageReceivedRecently / damageTrackingWindow;

        // Decay old damage
        damageReceivedRecently *= 0.99f;
    }

    private void UpdateNearbyAllies()
    {
        nearbyAllies.Clear();
        var allMonsters = FindObjectsOfType<RLMonsterAgent>();

        foreach (var monster in allMonsters)
        {
            if (monster == this) continue;

            float dist = Vector2.Distance(transform.position, monster.transform.position);
            if (dist <= allyCheckRadius)
            {
                nearbyAllies.Add(monster);
            }
        }
    }

    private Transform FindNearestPlayer()
    {
        var players = FindObjectsOfType<Character>();
        Transform nearest = null;
        float minDist = float.MaxValue;

        foreach (var p in players)
        {
            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = p.transform;
            }
        }
        return nearest;
    }

    // Update nearby allies periodically
    private void FixedUpdate()
    {
        if (StepCount % 50 == 0) // Every ~2.5 seconds
        {
            UpdateNearbyAllies();
        }
    }
}
```

---

## Thời Gian Triển Khai Ước Tính

- **Setup code**: 1 giờ (tactical logic phức tạp hơn)
- **Training**: 3-5 giờ (1M steps)
- **Testing & tuning**: 1 giờ
- **TỔNG**: ~6 giờ từ zero đến có tactical monster

## Tóm Tắt: Tại Sao Hướng Tiếp Cận Này Tạo Ra Khác Biệt

### Scripted AI (hiện tại)

- ✅ Pathfinding tối ưu, đuổi thẳng
- ✅ Hiệu quả cao trong việc đến gần player
- ❌ Không có khái niệm "nguy hiểm"
- ❌ Không phối hợp với đồng đội
- ❌ Pattern cố định, dễ đoán

### RL Agent (tactical)

- ✅ Học được khi nào nên retreat (HP thấp)
- ✅ Giữ khoảng cách tối ưu, không lao sát vô tội vạ
- ✅ Phối hợp nhóm, bao vây player
- ✅ Adapt theo player strength (đọc được player damage output)
- ✅ Hành vi emergent, khó đoán
- ⚠️ Có thể không hiệu quả bằng scripted trong việc đuổi thẳng
- ⚠️ Nhưng **survive lâu hơn** và **tạo challenge thú vị hơn**

### Player Experience

**Với Scripted AI:**  
"Monster lao thẳng vào, tôi đứng yên bắn AOE là chết hết."

**Với RL Agent:**  
"Monster này sao lại chạy quanh tôi vậy? Ồ nó đang rút lui, HP thấp rồi à? Ủa sao chúng nhóm lại đông vậy, nguy hiểm quá!"

→ **KHÁC BIỆT RÕ RÀNG TRONG GAMEPLAY**

    private float maxDetectionRange = 15f;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public override void OnEpisodeBegin()
    {
        // Reset vị trí ngẫu nhiên
        transform.localPosition = new Vector3(
            Random.Range(-10f, 10f),
            Random.Range(-10f, 10f),
            0f
        );

        // Tìm player gần nhất
        playerTransform = FindNearestPlayer();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (playerTransform == null) return;

        Vector2 dirToPlayer = (playerTransform.position - transform.position);
        float distance = dirToPlayer.magnitude;

        sensor.AddObservation(dirToPlayer.normalized); // 2 values
        sensor.AddObservation(distance / maxDetectionRange); // 1 value
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int moveAction = actions.DiscreteActions[0];

        Vector2 moveDir = Vector2.zero;
        switch(moveAction)
        {
            case 0: moveDir = Vector2.up; break;
            case 1: moveDir = Vector2.down; break;
            case 2: moveDir = Vector2.left; break;
            case 3: moveDir = Vector2.right; break;
        }

        rb.velocity = moveDir * moveSpeed;

        // Small reward for moving closer to player
        if (playerTransform != null)
        {
            float newDist = Vector2.Distance(transform.position, playerTransform.position);
            float oldDist = newDist + rb.velocity.magnitude * Time.fixedDeltaTime;

            if (newDist < oldDist)
                AddReward(0.01f); // Getting closer
            else
                AddReward(-0.01f); // Moving away
        }
    }

    // Call from damage system
    public void OnDamagePlayer()
    {
        AddReward(1.0f);
    }

    // Call when monster dies
    public void OnDeath()
    {
        AddReward(-1.0f);
        EndEpisode();
    }

    private Transform FindNearestPlayer()
    {
        var players = FindObjectsOfType<Character>();
        Transform nearest = null;
        float minDist = float.MaxValue;

        foreach (var p in players)
        {
            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = p.transform;
            }
        }
        return nearest;
    }

}

```

---

## Thời Gian Triển Khai Ước Tính

- **Setup code**: 30 phút
- **Training**: 1-2 giờ (500k steps)
- **Testing**: 10 phút
- **TỔNG**: ~3 giờ từ zero đến có monster RL hoạt động

## Sự Khác Biệt Dễ Quan Sát

| Scripted AI                    | RL Agent (sau training)                |
| ------------------------------ | -------------------------------------- |
| Luôn đi đường thẳng đến player | Học cách tránh obstacle                |
| Pattern cố định, dễ đoán       | Pattern thay đổi theo reward           |
| Không cải thiện theo thời gian | Hiệu quả tăng dần qua episodes         |
| Hành vi giống nhau mọi monster | Mỗi episode hành vi khác nhau một chút |

## Checkpoint Để Test Nhanh

- **Checkpoint 1** (50k steps): Monster biết hướng về player
- **Checkpoint 2** (200k steps): Monster đuổi tốt, ít đi lạc
- **Checkpoint 3** (500k steps): Monster gần tối ưu

→ Có thể dừng training sớm nếu hành vi đủ tốt để demo.
```
