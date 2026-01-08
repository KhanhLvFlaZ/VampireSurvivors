# MỤC LỤC - Co-op Survivors RL

---

## 📌 ĐẶT VẤN ĐỀ

📄 **File:** [0_DAT_VAN_DE.md](0_DAT_VAN_DE.md)

**Nội dung:**

- 1. Bối cảnh và động lực
- 2. Vấn đề cần giải quyết (P1-P6)
- 3. Mục tiêu nghiên cứu (RQ1-RQ4)
- 4. Phạm vi (In/Out of Scope)
- 5. Dự kiến giải pháp (High-level)
- 6. Tầm quan trọng và tác động
- 7. Kỳ vọng kết quả

**→ Đọc trước Chương 1 để hiểu bối cảnh**

---

## Chương 1. Tổng quan về game sinh tồn Co-op tích hợp AI kẻ địch dựa trên học tăng cường

### 1.1. Tiền sản xuất game sinh tồn Co-op

- 1.1.1. Ý tưởng
- 1.1.2. Concept
- 1.1.3. Pintch
- 1.1.4. Tài liệu thiết kế game (GDD)
- 1.1.5. Prototype

### 1.2. Phương pháp tiếp cận xây dựng game sinh tồn Co-op

- 1.2.1. Mô hình tổng quát hệ thống
- 1.2.2. Phương pháp xây dựng phần mềm
- 1.2.3. Công nghệ xây dựng game
- 1.2.4. Kiến trúc phần mềm

### 1.3. Đề xuất các kỹ thuật triển khai trong game sinh tồn Co-op

### 1.4. Kết luận chương 1

---

## Chương 2. Phân tích và thiết kế game sinh tồn Co-op

### 2.1. Phân tích hệ thống

- 2.1.1. Xác định Actor và Người Dùng Chính
- 2.1.2. Biểu đồ Use Case Tổng Quát
- 2.1.3. Biểu đồ Lớp Phân Tích (Class Diagram)
- 2.1.4. Kịch bản (Scenarios)
- 2.1.5. Phân tích quản lý model (Model Management)
- 2.1.6. Phân tích episode loop

### 2.2. Thiết kế hệ thống

- 2.2.1. Nguyên tắc thiết kế
- 2.2.2. Phương pháp hình thành lớp
- 2.2.3. Biểu đồ lớp (thiết kế)
- 2.2.4. RL ↔ Gameplay: Component & Data-Flow
- 2.2.5. Biểu đồ tuần tự (Sequence)

### 2.3. Cơ sở dữ liệu

- 2.3.1. Nguyên tắc & mô hình dữ liệu
- 2.3.2. Mô hình khái niệm & ERD
- 2.3.3. Lược đồ vật lý & lưu trữ
- 2.3.4. Tích hợp & đồng bộ Unity/ML-Agents
- 2.3.5. Vận hành, quản trị & vòng đời dữ liệu

### 2.4. Kết luận chương 2

---

## Chương 3. Thử nghiệm và đánh giá game sinh tồn Co-op

### 3.1. Dữ liệu thực nghiệm

- 3.1.1. Mục tiêu thu thập dữ liệu
- 3.1.2. Nguồn và loại dữ liệu
- 3.1.3. Thiết lập môi trường thực nghiệm
- 3.1.4. Kịch bản và tham số chạy
- 3.1.5. Cách ghi nhận và lưu trữ
- 3.1.6. Chỉ số đánh giá chính
- 3.1.7. Kiểm soát chất lượng dữ liệu

### 3.2. Cài đặt thực nghiệm

- 3.2.1. Độ đo (Metrics)
- 3.2.2. Phương pháp thực nghiệm
- 3.2.3. Các phương pháp được sử dụng để so sánh

### 3.3. Kết quả thực nghiệm

- 3.3.1. Thiết lập thực nghiệm và siêu tham số
- 3.3.2. Kết quả huấn luyện
- 3.3.3. Kết quả suy luận (Inference)
- 3.3.4. Phân tích hành vi định tính
- 3.3.5. So sánh với công bố tham khảo
- 3.3.6. Tóm tắt kết quả chính
- 3.3.7. Kết luận

### 3.4. Thử nghiệm game sinh tồn Co-op

- 3.4.1. Chiến lược kiểm thử
- 3.4.2. Kết quả kiểm thử chức năng
- 3.4.3. Kết quả kiểm thử hiệu năng
- 3.4.4. Kết quả kiểm thử độ ổn định
- 3.4.5. Kết quả kiểm thử tương thích hệ thống
- 3.4.6. Kết quả kiểm thử trải nghiệm người chơi (UX/Gameplay)
- 3.4.7. Kết quả kiểm thử regression
- 3.4.8. Tóm tắt & kết luận kiểm thử

### 3.5. Kết luận chương 3

- 3.5.1. Tóm tắt kết quả chính
- 3.5.2. Đánh giá mức độ đạt mục tiêu
- 3.5.3. Hạn chế và thách thức
- 3.5.4. Đóng góp chính của chương
- 3.5.5. Tổng kết

---

## Phụ lục. Cài đặt và triển khai

### A. Yêu cầu hệ thống

- A.1. Phần cứng
- A.2. Phần mềm

### B. Cài đặt môi trường

- B.1. Cài đặt Unity và ML-Agents
- B.2. Cài đặt Python và ML-Agents
- B.3. Cài đặt CUDA và cuDNN (GPU Training)

### C. Cấu hình dự án

- C.1. Cấu hình ML-Agents Training
- C.2. Scene Setup trong Unity
- C.3. Build Settings

### D. Huấn luyện mô hình

- D.1. Training từ Scratch
- D.2. Resume Training
- D.3. Curriculum Learning
- D.4. Xuất Model

### E. Suy luận và Testing

- E.1. Load Model trong Unity
- E.2. Testing Mode
- E.3. Performance Profiling

### F. Deployment

- F.1. Build for Windows
- F.2. Build for Android
- F.3. Optimization Tips

### G. Troubleshooting

- G.1. Lỗi thường gặp
- G.2. Debugging Tips

### H. Checklist Triển Khai

- H.1. Pre-release Checklist
- H.2. Performance Targets
- H.3. Post-release Monitoring

### I. Phụ lục Scripts

- I.1. Training Script (train.bat)
- I.2. TensorBoard Script (tensorboard.bat)
- I.3. Export Model Script (export.py)

---

## Kết luận và hướng phát triển

---

## Tài liệu tham khảo

---

**Cập nhật:** December 31, 2025
