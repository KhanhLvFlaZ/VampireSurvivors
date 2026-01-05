# Phụ lục: Cài đặt và Triển khai (Co-op Survivors RL)

Phụ lục này cung cấp hướng dẫn chi tiết về cài đặt môi trường, cấu hình hệ thống, huấn luyện mô hình, và triển khai game sinh tồn thể loại Survivors với chế độ Co-op tích hợp Reinforcement Learning đa agent.

---

## A. Yêu cầu hệ thống

### A.1. Phần cứng

**Cấu hình tối thiểu (Development):**

- CPU: Intel i5/AMD Ryzen 5 (4 cores)
- RAM: 16 GB
- GPU: NVIDIA GTX 1660 hoặc tương đương (6 GB VRAM)
- Ổ cứng: 20 GB trống

**Cấu hình khuyến nghị (Training + Development):**

- CPU: Intel i7/i9 hoặc AMD Ryzen 7/9 (8+ cores)
- RAM: 32 GB
- GPU: NVIDIA RTX 3070/3080/4090 (8+ GB VRAM, hỗ trợ CUDA 11.8+)
- Ổ cứng: SSD 50 GB trống

**Cấu hình Production Inference:**

- CPU: 4+ cores
- RAM: 8 GB
- GPU: NVIDIA GTX 1660+ hoặc RTX series
- Ổ cứng: 5 GB trống

### A.2. Phần mềm

| Thành phần                  | Phiên bản  | Ghi chú                 |
| --------------------------- | ---------- | ----------------------- |
| **Unity Editor**            | 2022.3 LTS | Khuyến nghị 2022.3.x    |
| **ML-Agents Unity Package** | 2.0.1+     | Cài qua Package Manager |
| **Python**                  | 3.8–3.10   | Không hỗ trợ 3.11+      |
| **mlagents**                | 0.30.0+    | Python package          |
| **PyTorch**                 | 1.13.0+    | Backend cho ML-Agents   |
| **CUDA Toolkit**            | 11.8       | Nếu dùng GPU training   |
| **cuDNN**                   | 8.6+       | Đi kèm CUDA             |
| **.NET Framework**          | 4.x        | Tích hợp trong Unity    |
| **Git**                     | 2.x        | Quản lý source code     |

---

## B. Cài đặt môi trường

### B.1. Cài đặt Unity và ML-Agents

#### Bước 1: Cài đặt Unity Hub và Editor

```bash
# Tải Unity Hub từ: https://unity.com/download
# Cài đặt Unity 2022.3 LTS qua Unity Hub
```

**Modules cần thiết:**

- Windows Build Support
- Android Build Support (nếu build mobile)
- Linux Build Support (nếu deploy Linux)

#### Bước 2: Clone repository

```bash
git clone https://github.com/KhanhLvFlaZ/VampireSurvivors.git
cd VampireSurvivors
```

#### Bước 3: Mở project trong Unity

1. Mở Unity Hub
2. Click **Add** → chọn thư mục `VampireSurvivors`
3. Click vào project để mở Unity Editor
4. Đợi import packages (3–5 phút)

#### Bước 4: Cài đặt ML-Agents Package

**Phương pháp 1: Package Manager (Khuyến nghị)**

1. Unity Editor → **Window** → **Package Manager**
2. Click **+** → **Add package by name**
3. Nhập: `com.unity.ml-agents`
4. Version: `2.0.1`
5. Click **Add**

**Phương pháp 2: manifest.json**

Mở `Packages/manifest.json`, thêm vào `dependencies`:

```json
{
  "dependencies": {
    "com.unity.ml-agents": "2.0.1",
    ...
  }
}
```

### B.2. Cài đặt Python và ML-Agents

#### Bước 1: Cài đặt Python

**Windows:**

```powershell
# Tải Python 3.9 từ: https://www.python.org/downloads/
# Hoặc dùng Chocolatey
choco install python --version=3.9.13
```

**Linux/Mac:**

```bash
# Dùng pyenv
pyenv install 3.9.13
pyenv global 3.9.13
```

#### Bước 2: Tạo môi trường ảo

```bash
# Tạo virtual environment
python -m venv mlagents-env

# Kích hoạt
# Windows
mlagents-env\Scripts\activate
# Linux/Mac
source mlagents-env/bin/activate
```

#### Bước 3: Cài đặt ML-Agents Python package

```bash
# Upgrade pip
pip install --upgrade pip setuptools wheel

# Cài mlagents
pip install mlagents==0.30.0

# Cài PyTorch (CPU)
pip install torch==1.13.0 torchvision torchaudio

# Cài PyTorch (GPU - CUDA 11.8)
pip install torch==1.13.0+cu118 torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118

# Các dependencies khác
pip install numpy==1.21.6 protobuf==3.20.3 grpcio==1.48.2
```

#### Bước 4: Kiểm tra cài đặt

```bash
# Kiểm tra mlagents-learn
mlagents-learn --help

# Kiểm tra PyTorch
python -c "import torch; print(torch.__version__); print(torch.cuda.is_available())"
```

**Output mong đợi:**

```
1.13.0
True  # Nếu có GPU
```

### B.3. Cài đặt CUDA và cuDNN (GPU Training)

#### Bước 1: Cài CUDA Toolkit 11.8

```powershell
# Tải từ: https://developer.nvidia.com/cuda-11-8-0-download-archive
# Chọn: Windows → x86_64 → 11 → exe (network)
# Cài đặt với default settings
```

#### Bước 2: Cài cuDNN 8.6

1. Tải từ: https://developer.nvidia.com/cudnn (cần đăng ký NVIDIA)
2. Chọn **cuDNN v8.6.0 for CUDA 11.x**
3. Giải nén và copy vào thư mục CUDA:
   - `bin\` → `C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8\bin\`
   - `include\` → `C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8\include\`
   - `lib\` → `C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8\lib\`

#### Bước 3: Kiểm tra

```powershell
nvcc --version  # CUDA version
nvidia-smi      # GPU info
```

---

## C. Cấu hình dự án

### C.1. Cấu hình ML-Agents Training

File cấu hình: `ml-agents-configs/ppo_vampire.yaml`

```yaml
behaviors:
  VampireAgent:
    trainer_type: ppo
    hyperparameters:
      batch_size: 1024
      buffer_size: 10240
      learning_rate: 3.0e-4
      beta: 1.0e-3 # Entropy coefficient
      epsilon: 0.2 # PPO clip
      lambd: 0.95 # GAE lambda
      num_epoch: 3
      learning_rate_schedule: constant
    network_settings:
      normalize: false
      hidden_units: 128
      num_layers: 2
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
    keep_checkpoints: 5
    max_steps: 5.0e6
    time_horizon: 64
    summary_freq: 20000
```

**Tham số quan trọng:**

- `batch_size`: Kích thước batch (tăng nếu có GPU mạnh)
- `buffer_size`: Số experience trước khi update (phải ≥ batch_size)
- `learning_rate`: Tốc độ học (giảm nếu loss không ổn định)
- `max_steps`: Tổng số bước huấn luyện (5M = ~10–15 giờ GPU)

### C.2. Scene Setup trong Unity

#### Bước 1: Mở Main Scene

1. Unity Editor → **Project** tab
2. Navigate: `Assets/Scenes/Game/Main`
3. Double-click để mở scene

#### Bước 2: Kiểm tra RL Components

Trong **Hierarchy**, tìm GameObject có components:

- **RLSystem** (singleton)
- **VampireAgent** (trên Player object)
- **BehaviorParameters** (ML-Agents component)

#### Bước 3: Cấu hình BehaviorParameters

Chọn Player GameObject, trong Inspector:

```
Behavior Parameters:
  Behavior Name: VampireAgent
  Vector Observation Space: 20
  Actions: Discrete (5 branches)
  Model: [None hoặc trained .onnx file]
  Inference Device: GPU/CPU
  Behavior Type: Default (training) / Heuristic Only (testing)
```

### C.3. Build Settings

#### Training Build (Headless)

1. **File** → **Build Settings**
2. Platform: **Windows/Linux**
3. Chọn scene: `Main`
4. **Player Settings**:
   - Run in Background: **✓**
   - Display Resolution Dialog: **Disabled**
   - Fullscreen Mode: **Windowed**
   - Resolution: 640x480 (low để train nhanh)
5. Build vào thư mục: `Build/Training/VampireSurvivors.exe`

#### Production Build

1. Platform: **Windows/Android**
2. Resolution: 1920x1080+
3. Quality: High
4. Include trained model (.onnx) trong `StreamingAssets/`

---

## D. Huấn luyện mô hình

### D.1. Training từ Scratch

#### Bước 1: Kích hoạt Python environment

```bash
cd VampireSurvivors
mlagents-env\Scripts\activate  # Windows
```

#### Bước 2: Chạy training

```bash
mlagents-learn ml-agents-configs/ppo_vampire.yaml --run-id=vampire_v1 --num-envs=4 --time-scale=5
```

**Tham số:**

- `--run-id`: Tên run (kết quả lưu vào `results/vampire_v1/`)
- `--num-envs`: Số môi trường song song (4–8 tùy RAM)
- `--time-scale`: Tăng tốc game (5–20x)

#### Bước 3: Khởi động Unity

Sau khi thấy:

```
[INFO] Listening on port 5004. Start training by pressing Play in Unity Editor
```

→ Chuyển sang Unity Editor, nhấn **Play**

#### Bước 4: Giám sát Training

Mở terminal mới:

```bash
tensorboard --logdir results/
```

Truy cập: http://localhost:6006

**Metrics quan tâm:**

- Cumulative Reward (tăng dần)
- Episode Length (ổn định)
- Policy Loss (giảm)
- Value Loss (giảm)
- Entropy (giảm chậm từ ~1.8 → 0.3)

### D.2. Resume Training

```bash
mlagents-learn ml-agents-configs/ppo_vampire.yaml --run-id=vampire_v1 --resume
```

### D.3. Curriculum Learning (Tùy chọn)

Trong `ppo_vampire.yaml`, thêm:

```yaml
environment_parameters:
  difficulty:
    curriculum:
      - name: easy
        value: 0.2
        completion_criteria:
          measure: reward
          threshold: 200
      - name: hard
        value: 0.8
```

### D.4. Xuất Model

Sau khi training xong, model tự động lưu tại:

```
results/vampire_v1/VampireAgent.onnx
```

Copy file `.onnx` vào Unity project:

```
Assets/StreamingAssets/Models/VampireAgent.onnx
```

---

## E. Suy luận và Testing

### E.1. Load Model trong Unity

#### Cách 1: Inspector

1. Chọn Player GameObject
2. **Behavior Parameters** → **Model**: Kéo file `.onnx` vào
3. **Inference Device**: GPU (nếu có) hoặc CPU

#### Cách 2: Script

```csharp
using Unity.MLAgents.Policies;

var behaviorParams = GetComponent<BehaviorParameters>();
behaviorParams.Model = Resources.Load<NNModel>("Models/VampireAgent");
```

### E.2. Testing Mode

**Heuristic Testing (Không dùng RL):**

```csharp
// BehaviorParameters Inspector
Behavior Type: Heuristic Only
```

**RL Inference:**

```csharp
Behavior Type: Default
```

### E.3. Performance Profiling

#### Unity Profiler

1. **Window** → **Analysis** → **Profiler**
2. Play mode
3. Quan sát:
   - CPU Usage (Main Thread)
   - Rendering
   - Scripts (RLSystem, Agent)

**Mục tiêu:**

- FPS ≥ 55
- Frame time ≤ 18ms
- RL inference ≤ 10ms

#### Custom Logging

```csharp
// Trong VampireAgent.cs
void LogPerformance()
{
    Debug.Log($"FPS: {1.0f / Time.deltaTime:F1}");
    Debug.Log($"Inference: {inferenceTime:F2}ms");
}
```

---

## F. Deployment

### F.1. Build for Windows

```bash
# Unity Editor
File → Build Settings
Platform: Windows
Architecture: x86_64
Build
```

**Kiểm tra:**

- File `.onnx` có trong `GameName_Data/StreamingAssets/`
- File `mlagents-*.dll` có trong `GameName_Data/Managed/`

### F.2. Build for Android

#### Bước 1: Cấu hình

1. **Build Settings** → **Android**
2. **Player Settings**:
   - Minimum API Level: 24 (Android 7.0)
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64

#### Bước 2: Tối ưu Model

```bash
# Quantize model (giảm kích thước)
python -m tf2onnx.convert --saved-model models/vampire_v1 --output vampire_quantized.onnx --quantize uint8
```

#### Bước 3: Build

```
File → Build Settings → Build
Output: VampireSurvivors.apk
```

### F.3. Optimization Tips

**Giảm inference latency:**

```csharp
// BehaviorParameters
Decision Requester:
  Decision Period: 5  // Request mỗi 5 frame thay vì mỗi frame
```

**Giảm memory:**

- Giảm `hidden_units` từ 128 → 64
- Giảm `num_layers` từ 2 → 1
- Quantize model

**Tăng FPS:**

- Giảm particle effects
- Dùng GPU instancing
- Object pooling (đã có)

---

## G. Troubleshooting

### G.1. Lỗi thường gặp

#### Lỗi 1: "Connection timeout" khi training

**Nguyên nhân:** Unity không kết nối được ML-Agents Python.

**Giải pháp:**

```bash
# Kiểm tra port
netstat -ano | findstr 5004

# Nếu bị chiếm, đổi port
mlagents-learn --base-port=5005 ...
```

#### Lỗi 2: "Out of memory" (OOM)

**Giải pháp:**

- Giảm `--num-envs` từ 8 → 4
- Giảm `buffer_size` từ 10240 → 5120
- Giảm resolution build (640x480)

#### Lỗi 3: FPS thấp khi inference

**Giải pháp:**

- Chuyển sang GPU inference
- Tăng `Decision Period` lên 5–10
- Compress model

#### Lỗi 4: NaN trong training

**Giải pháp:**

- Giảm `learning_rate` từ 3e-4 → 1e-4
- Kiểm tra reward function (tránh infinity/NaN)
- Normalize observations

### G.2. Debugging Tips

**Log inference time:**

```csharp
float startTime = Time.realtimeSinceStartup;
RequestDecision();
float inferenceTime = (Time.realtimeSinceStartup - startTime) * 1000f;
Debug.Log($"Inference: {inferenceTime:F2}ms");
```

**Visualize observations:**

```csharp
public override void CollectObservations(VectorSensor sensor)
{
    var obs = GetObservationVector();
    Debug.Log($"Obs: [{string.Join(", ", obs)}]");
    sensor.AddObservation(obs);
}
```

**Check reward:**

```csharp
AddReward(reward);
Debug.Log($"Step reward: {reward}, Cumulative: {GetCumulativeReward()}");
```

---

## H. Checklist Triển Khai

### H.1. Pre-release Checklist

- [ ] Training converged (reward stable)
- [ ] Model exported (.onnx)
- [ ] Model tested trong Unity (FPS ≥ 55)
- [ ] Fallback FSM hoạt động
- [ ] Memory leak test (60 min run)
- [ ] Crash test (50+ runs)
- [ ] Player test (n ≥ 5)
- [ ] Build tested trên target platform
- [ ] Logging/telemetry enabled
- [ ] Documentation complete

### H.2. Performance Targets

| Metric            | Target | Measured |
| ----------------- | ------ | -------- |
| FPS (GPU)         | ≥ 55   | 58.2 ✓   |
| Inference latency | ≤ 10ms | 8.5ms ✓  |
| Memory            | ≤ 3 GB | 2.8 GB ✓ |
| Crash rate        | ≤ 1%   | 2% ⚠     |

### H.3. Post-release Monitoring

```csharp
// Telemetry logging
void LogTelemetry()
{
    TelemetryManager.Log(new {
        fps = 1.0f / Time.deltaTime,
        inferenceTime = lastInferenceTime,
        rewardPerMinute = totalReward / (Time.time / 60f),
        fallbackRate = fallbackCount / totalDecisions
    });
}
```

---

## I. Tài liệu tham khảo

### I.1. Official Documentation

- **Unity ML-Agents:** https://github.com/Unity-Technologies/ml-agents/blob/main/docs/Readme.md
- **Unity Manual:** https://docs.unity3d.com/Manual/
- **PyTorch:** https://pytorch.org/docs/stable/index.html

### I.2. Cộng đồng

- **Unity Forum:** https://forum.unity.com/forums/ml-agents.453/
- **Discord:** Unity ML-Agents server
- **Stack Overflow:** Tag `unity-ml-agents`

### I.3. Liên kết Repository

- **GitHub:** https://github.com/KhanhLvFlaZ/VampireSurvivors
- **Issues:** https://github.com/KhanhLvFlaZ/VampireSurvivors/issues

---

## J. Phụ lục Scripts

### J.1. Training Script (train.bat)

```batch
@echo off
echo Starting ML-Agents Training...
call mlagents-env\Scripts\activate
mlagents-learn ml-agents-configs/ppo_vampire.yaml --run-id=vampire_%date:~-4,4%%date:~-10,2%%date:~-7,2% --num-envs=4 --time-scale=5
pause
```

### J.2. TensorBoard Script (tensorboard.bat)

```batch
@echo off
call mlagents-env\Scripts\activate
tensorboard --logdir results/
pause
```

### J.3. Export Model Script (export.py)

```python
import os
import shutil

def export_model(run_id, destination):
    src = f"results/{run_id}/VampireAgent.onnx"
    dst = f"{destination}/VampireAgent.onnx"

    if os.path.exists(src):
        shutil.copy(src, dst)
        print(f"Exported: {dst}")
    else:
        print(f"Model not found: {src}")

if __name__ == "__main__":
    export_model("vampire_v1", "Assets/StreamingAssets/Models")
```

---

**Lưu ý cuối:** Tài liệu này được cập nhật cho Unity 2022.3 LTS và ML-Agents 2.0.1. Kiểm tra phiên bản mới nhất tại repository.

---

## K. Kết luận và hướng phát triển

- **Tóm tắt:** Quy trình cài đặt, huấn luyện, suy luận, kiểm thử và triển khai đã được chuẩn hóa cho Unity 2022.3 LTS + ML-Agents 2.0.1, với cấu hình tham chiếu cho GPU inference (≈58 FPS, latency ≈8.5ms) và checklist phát hành.
- **Độ sẵn sàng:** Hạ tầng đào tạo (TensorBoard, resume, curriculum), pipeline export ONNX, và fallback scripted AI đã sẵn sàng để đưa vào build production trên Windows/Android.
- **Hướng tối ưu ngắn hạn:**
  1. Giảm latency bằng quantization/ONNX Runtime và tăng `Decision Period` hợp lý;
  2. Giảm GC spike bằng pooling và hạn chế alloc trong Agent;
  3. Fix crash edge case (physics/raycast) để đạt crash-rate ≤ 1%.
- **Hướng mở rộng trung hạn:**
  1. Model compression (pruning/INT8) để giảm footprint < 1 GB;
  2. Domain randomization + curriculum để tăng robustness OOD;
  3. Thiết lập telemetry realtime (FPS, latency, fallback rate) cho bản release.
- **Nghiên cứu dài hạn:**
  1. Multi-agent/Hierarchical RL cho co-op hoặc macro-strategy;
  2. Adaptive Difficulty (DDA) dựa trên hành vi người chơi;
  3. So sánh thêm với GOAP/Utility AI/Rainbow/A3C để chọn kiến trúc tối ưu.
