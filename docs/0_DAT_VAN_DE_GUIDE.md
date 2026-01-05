# Hướng Dẫn Đọc Phần "Đặt Vấn Đề"

## 📖 Tệp Chính

**File:** [`0_DAT_VAN_DE.md`](0_DAT_VAN_DE.md) (10-12 trang)

## 🎯 Mục Đích

Phần "Đặt Vấn Đề" cung cấp:

✅ **Bối cảnh chi tiết** - Tại sao cần dự án này?  
✅ **Vấn đề cụ thể** - Những challenges nào cần giải quyết?  
✅ **Mục tiêu rõ ràng** - Thành công nếu đạt được gì?  
✅ **Phạm vi xác định** - Phần nào bao gồm, phần nào không?  
✅ **Giải pháp high-level** - Cách tiếp cận tổng quát?

## 📋 Cấu Trúc Nội Dung

### 1. Bối Cảnh và Động Lực

- **Game Sinh Tồn Hiện Tại**: Hiện trạng ngành (VCS, Raft, Vampire Survivors)
- **Hạn Chế của AI Scripted**: Tại sao cần thay đổi
- **Xu Hướng RL/ML**: Tại sao RL là giải pháp tốt
- **Co-op Multiplayer**: Thách thức khi thêm multiplayer

### 2. Vấn Đề Cần Giải Quyết

- **P1 (Chính)**: Tạo AI thông minh & thích ứng
- **P2-P6 (Con)**: Các thách thức cụ thể (training, inference, sync, rewards)

| Vấn Đề | Chi tiết              | Thách thức                 |
| ------ | --------------------- | -------------------------- |
| P1     | Multi-agent RL AI     | Reward design, exploration |
| P2     | Training              | 50+ agents, convergence    |
| P3     | Inference Performance | < 16ms latency             |
| P4     | Co-op Sync            | < 50ms, bandwidth          |
| P5     | Model Management      | Save/load, versioning      |
| P6     | Cooperation Rewards   | Credit assignment          |

### 3. Mục Tiêu Nghiên Cứu

- **3 mục tiêu chính**: Kỹ thuật, Gameplay, Khả thi
- **4 câu hỏi nghiên cứu**: RQ1-RQ4 cụ thể (có sub-questions)

### 4. Phạm Vi (In/Out of Scope)

| In Scope ✅              | Out of Scope ❌     |
| ------------------------ | ------------------- |
| Co-op 1-4 players        | PvP mode            |
| 20+ vũ khí               | Voice chat          |
| RL + Cooperative rewards | Cloud multiplayer   |
| Server-authoritative     | Mobile optimization |
| Integration tests        | Monetization        |

### 5. Dự Kiến Giải Pháp

- **Kiến Trúc Tổng Quát**: 5 layers (Presentation → Persistence)
- **Công Nghệ**: Unity, ML-Agents, Netcode, Barracuda
- **Giai Đoạn P0-P5**: 6-12 tháng phát triển

### 6. Tầm Quan Trọng

- **Khoa Học**: Multi-agent RL, Inference optimization, Networking patterns
- **Thực Tế**: Blueprint cho indie devs, Dynamic difficulty, Best practices
- **Học Tập**: Full-stack game dev, RL, Networking

### 7. Kỳ Vọng Kết Quả

**Thành Công Nếu:**

- 60 FPS với 4 players + 50 monsters ✅
- RL convergence trong 1-2M steps ✅
- Latency < 100ms ✅
- Visible cooperation (flanking, focus fire) ✅
- Positive playtesting feedback ✅

**Thất Bại Nếu:**

- Latency > 200ms ❌
- RL không hội tụ ❌
- FPS < 30 ❌
- Behavior random ❌

## 👥 Ai Nên Đọc & Khi Nào

| Độc Giả                  | Lý Do                                     | Phần Cần Đọc | Thời Gian |
| ------------------------ | ----------------------------------------- | ------------ | --------- |
| **Quản lý dự án**        | Hiểu scope & timeline                     | 1, 4, 5, 7   | 5 phút    |
| **Lập trình viên**       | Hiểu architecture & challenges            | 2, 3, 5, 6   | 10 phút   |
| **Nhà thiết kế**         | Hiểu gameplay goals                       | 1, 3, 4      | 7 phút    |
| **Sinh viên**            | Hiểu problem & context trước khi vào code | Toàn bộ      | 15 phút   |
| **Nhà quản lý khoa học** | Đánh giá tính khả thi & đóng góp          | Toàn bộ      | 20 phút   |

## 🔗 Liên Kết Với Các Chương

```
0_DAT_VAN_DE.md
    ↓
    ├─→ Chương 1 (Tổng Quan)
    │   - Tiền sản xuất: Implement GDD (từ vấn đề → design)
    │   - Phương pháp: Thiết kế solution (từ scope → architecture)
    │
    ├─→ Chương 2 (Phân Tích & Thiết Kế)
    │   - 2.1 Phân tích: Xác định actors, use cases (từ problem → analysis)
    │   - 2.2 Thiết kế: Thiết kế classes, components (từ analysis → design)
    │
    └─→ Chương 3 (Thử Nghiệm)
        - Testing: Verify success criteria (từ design → validation)
        - Evaluation: Đánh giá kết quả (từ data → conclusions)
```

## 💡 Key Takeaways

1. **Vấn đề lớn**: Tạo AI phối hợp cho co-op survivors + real-time performance
2. **Giải pháp**: Multi-agent RL + Server-authoritative networking + Batching
3. **Challenges**: P1-P6 (training, inference, sync, rewards, management)
4. **Phạm vi**: Co-op 1-4 players, 50+ agents, local/LAN only
5. **Success**: 60 FPS, < 100ms latency, visible cooperation

## 📚 Tài Liệu Bổ Sung

- [architecture-methodology.md](architecture-methodology.md) - Kiến trúc chi tiết
- [component-interaction.md](component-interaction.md) - Tương tác thành phần
- [2.1_Phan_tich_he_thong.md](2.1_Phan_tich_he_thong.md) - Phân tích hệ thống

---

**Mẹo:** Đọc 0_DAT_VAN_DE trước Chương 1 để hiểu "WHY" (tại sao), sau đó Chương 1 giải thích "HOW" (làm sao).
