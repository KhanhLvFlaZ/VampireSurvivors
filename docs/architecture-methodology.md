# Phương pháp, kiến trúc và công nghệ theo mô hình tổng quát

## 1) Phương pháp xây dựng phần mềm (Co-op Survivors RL)

- Quy trình lặp (sprint ngắn 1-2 tuần), mỗi vòng gồm: thiết kế nhỏ (sequence/plantuml), hiện thực, unit test, playtest co-op, đo metric (FPS, latency, reward/team score), chỉnh sửa.
- Mỗi tính năng theo tiêu chí Definition of Done: có test tối thiểu (edit mode/play mode multi-player), log/metric cơ bản (per-player, per-team), tài liệu ngắn mô tả trách nhiệm thành phần.
- Tách dữ liệu cấu hình bằng ScriptableObject (thông số vũ khí, quái, bản đồ, role người chơi) để cân chỉnh không cần sửa code; dùng Addressables cho nội dung tải động.
- Dùng event-bus và dependency injection (Zenject hoặc Unity DI) để giảm liên kết cứng; logging qua UnityLogger + hooks sang Application Insights/Seq nếu cần.

## 2) Kiến trúc phân lớp (Layered)

- Presentation: UI/HUD, input, menu; tách prefab UI khỏi logic; hỗ trợ nhiều player join/leave; binding sự kiện qua event-bus.
- Game Logic: combat, cooldown, spawn, collision; cập nhật theo game tick; tránh logic rời rạc trong Update, gom vào GameManager; PlayerManager hỗ trợ nhiều player instances cho co-op (track từng player state, health, position, inventory riêng biệt), quản lý join/leave động.
- AI & Learning: Multi-agent RL (hoặc shared policy) cho phối hợp giữa các monster; encoder trạng thái gồm teammate/enemy; reward shaping theo team bổ sung hành vi hợp tác (heal/support teammate, aggro share bảo vệ đồng đội yếu, positioning/formation tối ưu); inference runtime đa agent; tách pipeline train/infer; giới hạn tần suất quyết định.
- Networking: Server-authoritative architecture (NetworkManager ưu tiên server làm authority); combat/cooldown/spawn tính trên server; client-side prediction + reconciliation cho smooth movement; RPC cho sự kiện hiếm (skill cast, damage events); NetworkManager chịu tick sync, spawn/despawn nhiều player instances, phân phối RL actions cho multi-agent monsters.
- Persistence: repository cho player/session/team, leaderboard co-op; local cache SQLite, đồng bộ nền lên PostgreSQL/Redis.

## 1.2.3) Công nghệ xây dựng game

- Engine và ngôn ngữ: Unity 2022 LTS, C#; 2D Physics (Box2D), New Input System (PlayerInputManager cho multi-player), TextMesh Pro, Addressables cho tải nội dung động.
- AI/RL: ML-Agents (PPO multi-agent cho train offline với coordination, DQN nhẹ cho thử nhanh), Barracuda cho inference; state encoder gồm teammate/enemy info cho multi-agent awareness; reward shaping tách cấu hình theo team với bonuses cho hợp tác (assist kills, aggro share, formation); hỗ trợ shared policy (nhiều agent dùng chung model) hoặc per-agent policy (mỗi agent model riêng).
- Backend/network: Netcode for GameObjects (server-authoritative), RPC và NetworkVariable tối ưu băng thông; sync nhiều player + prediction/reconcile; SQLite cho offline, PostgreSQL + Redis cho cloud/cache.
- Công cụ hỗ trợ: ScriptableObject để cấu hình quái/vũ khí/role player; localization bảng LocalizedString; CI chạy test in-editor multi-player, build addressables, build player; telemetry/log qua UnityLogger và exporter tùy chọn (per-player/per-team).

## 1.2.4) Kiến trúc phần mềm

- Phân lớp rõ ràng: Presentation (UI/Input), Game Logic, AI & Learning, Networking, Persistence; giao tiếp qua event-bus hoặc interface để giảm liên kết cứng.
- Tách cấu hình khỏi code: ScriptableObject cho thông số quái/vũ khí/bản đồ/role; Addressables cho asset động; cân chỉnh mà không build lại.
- Điều phối trung tâm: GameManager điều khiển tick, gọi PlayerManager (quản lý nhiều player instances trong co-op) và EnemyManager (điều phối multi-agent RL monsters); NetworkManager (server-authoritative) chịu sync state, spawn/despawn player, broadcast RL actions; Persistence Service lưu team/session/co-op leaderboard.
- AI pipeline tách rời: RL System multi-agent nhận state encode gồm teammate monsters (position/health/aggro) và enemy players (team composition); trả action theo agent_id cho phối hợp; DecisionRequester đặt nhịp cố định (batching inference); checkpoint model lưu qua Persistence, hỗ trợ A/B model và shared vs per-agent policy comparison.
- Kiến trúc server-authoritative: Server là authority cho combat/cooldown/spawning/RL decisions; tất cả player actions validated trên server; client-side prediction + reconciliation cho smooth movement nhiều player; RPC cho sự kiện hiếm (damage dealt, skill triggered), NetworkVariable cho state định kỳ (player positions, health bars); latency compensation cho co-op fairness.
- Cross-cutting: Dependency Injection để hoán đổi implementation (mock multi-agent, fake persistence); telemetry/metrics per-player/per-team ở boundary; test theo lớp (unit, playmode multi-player, integration) bám theo phân lớp trên.

## 3) Công nghệ và cách triển khai theo khối

### Presentation (UI / Input)

- Unity UI Toolkit + TextMesh Pro cho HUD/menu; Input System (Actions) cho đa nền tảng.
- Pattern: View lắng nghe event-bus, không gọi thẳng logic; prefab UI load qua Addressables; localization dùng bảng LocalizedString.

### Game Logic (Combat / Spawn / Physics)

- C# MonoBehaviour thuần cho thực thể; Box2D 2D Physics; ScriptableObject chứa chỉ số quái/vũ khí để buff/nerf nhanh.
- GameManager điều phối vòng lặp: tick cố định (FixedUpdate) cho vật lý, LateUpdate cho HUD; tránh logic ngẫu nhiên trong Update của từng entity để giảm jitter.
- EnemyManager/PlayerManager chịu trách nhiệm state; combat dùng kênh sự kiện (hit/damage) thay vì gọi trực tiếp để dễ ghi log và test.

### AI & Learning (RL)

- ML-Agents cho PPO multi-agent train offline; DQN nhẹ trong-editor cho thử nghiệm nhanh; inference tách riêng (DecisionRequester) với nhịp lấy hành động cố định.
- State encoder multi-agent: vector hóa vị trí/vận tốc/health bản thân, teammates (other monsters), enemies (players trong team); khoảng cách mục tiêu, cooldown, aggro state; normalize cho multi-agent awareness.
- Reward shaping với cooperative components:
  - Base: sát thương gây ra, sống sót, giữ khoảng cách tối ưu, tránh đòn
  - Cooperative bonuses:
    - Assist/support teammates (heal, buff, shield đồng đội)
    - Aggro share (draw aggro away from low-health teammates)
    - Positioning rewards (flanking with teammates, formation maintenance)
    - Focus fire bonus (coordinated attacks on same target)
- Multi-agent coordination: Shared policy (tất cả monsters dùng chung model) hoặc per-role policies (tank/DPS/support roles riêng).
- Giới hạn chi phí: batch inference (group decisions), giới hạn tần suất quyết định per-agent (ví dụ 5-10 Hz), dùng Barracuda GPU/CPU tùy nền tảng.
- Lưu checkpoint model qua Persistence Service; hỗ trợ A/B model để so sánh shared vs per-agent policies và scripted baseline.

### Networking

- Netcode for GameObjects, mô hình server-authoritative với NetworkManager là authority center; tick sync qua NetworkManager cho co-op consistency; client-side prediction + reconcile cho chuyển động nhiều players.
- Server authority: Combat/damage/cooldown/spawn/RL decisions tất cả tính trên server; clients send input commands, server validates và broadcasts results.
- PlayerManager trên server: Quản lý nhiều player instances (tracking positions, health, inventory, team composition); handle dynamic join/leave; broadcast player states qua NetworkVariable.
- RPC dùng cho sự kiện hiếm (spawn notification, skill cast, damage dealt); state quan trọng đồng bộ qua NetworkVariable với phân giải thấp để tiết kiệm băng thông.
- Latency handling: interpolation cho vị trí enemy và teammates; rollback/reconciliation cho player movement prediction; cooldown và combat tính trên server, client chỉ hiển thị với predictive UI feedback.
- Multi-agent RL integration: Server runs RL inference cho tất cả monsters, broadcasts actions qua network; clients render RL behaviors smoothly với interpolation.

### Data Persistence

- Repository/service layer trừu tượng hóa lưu trữ; SQLite cho offline/local, PostgreSQL cho cloud; Redis để cache leaderboard/telemetry.
- Async IO (Task) để không chặn khung hình; ghi log trận đấu, thống kê reward, checkpoint RL.
- Định dạng dữ liệu: JSON hoặc protobuf nhẹ; mã hóa basic khi lưu model/checkpoint nếu cần.

### Cross-cutting (Áp dụng cho mọi khối)

- Dependency Injection để hoán đổi implementation (mock AI, fake persistence khi test).
- Telemetry/metrics: FPS, latency quyết định AI, số lượng kẻ địch, tỉ lệ va chạm; gửi nền để không ảnh hưởng frame.
- Testing: unit (logic thuần), playmode (combat/AI tick), integration (persistence + network in-editor headless).
- Build/CI: lint C# (Roslyn analyzers), chạy test in-editor batchmode, build addressables, rồi build player.

## 4) Cách dùng tài liệu này

- Mỗi khối chỉ cần 1-2 đoạn ngắn mô tả trách nhiệm, công nghệ chính, quyết định kiến trúc; tránh liệt kê dài dòng.
- Khi thêm thành phần mới (Audio, Analytics), bổ sung vào danh sách khối và ghi rõ trách nhiệm + công nghệ chính, giữ cùng cấu trúc để nhất quán.
