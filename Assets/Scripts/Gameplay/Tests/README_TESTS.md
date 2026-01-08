# Multi-Player Integration Testing Guide

## Overview

This document describes the complete testing infrastructure for co-op multi-player scenarios in the Vampire Survivors RL project. Tests measure latency, bandwidth, and overall network performance for 2+ player games.

**Current Test Coverage:**

- ✅ 2-player co-op join/leave
- ✅ 3-4 player concurrent gameplay
- ✅ Latency measurement (RTT tracking)
- ✅ Bandwidth tracking (upstream/downstream)
- ✅ Network stress tests
- ✅ Edge cases (packet loss, jitter, timeouts)
- ✅ Performance benchmarks

---

## Test Files Overview

### 1. CoopMultiPlayerIntegrationTest.cs

**Purpose:** Core integration tests for 2+ player scenarios  
**Test Count:** 10 main tests  
**Execution Time:** ~30 seconds

#### Test Cases:

| Test                                                    | Scenario                                      | Validates                                |
| ------------------------------------------------------- | --------------------------------------------- | ---------------------------------------- |
| `TestTwoPlayersJoinSequentially`                        | 2 players join one after another              | Player join mechanics, registry tracking |
| `TestThreePlayersJoinAndLeave`                          | 3 players join, one leaves, 2 remain          | Dynamic player management                |
| `TestLatencyMeasurement_TwoPlayers`                     | Measure RTT for 2 players                     | Latency < 100ms average                  |
| `TestBandwidthTracking_ThreePlayers`                    | Track upload/download for 3 players           | Bandwidth scaling with player count      |
| `TestNetworkMessageOrder_TwoPlayers`                    | Verify 20 sequential messages arrive in order | Message ordering (0% loss)               |
| `TestNetworkStress_FourPlayersWithHighFrequencyUpdates` | 4 players with 240 Hz input frequency         | Sustained network load                   |
| `TestConcurrentPlayerActions_TwoPlayers`                | Both players attack/move simultaneously       | Concurrent action handling               |
| `TestPlayerDisconnectRecovery`                          | Player 2 disconnects and reconnects           | Graceful disconnection handling          |
| `TestNetworkSynchronization_TwoPlayers`                 | Verify position sync with <50ms delta         | State synchronization quality            |
| `TestBandwidthScaling_VariablePlayerCount`              | Measure bandwidth for 1/2/3/4 players         | Linear bandwidth scaling                 |

**Key Assertions:**

```csharp
Assert.AreEqual(N, playerManager.ActivePlayers.Count)  // Player count verification
Assert.IsTrue(stats.averageLatencyMs < 100)             // Latency target
Assert.IsTrue(bandwidthStats.avgBandwidthKbps < 1000)   // Bandwidth ceiling
Assert.IsTrue(stats.maxLatencyMs < 200)                 // Max latency threshold
```

---

### 2. CoopEdgeCasesIntegrationTest.cs

**Purpose:** Stress tests and edge cases  
**Test Count:** 10 stress tests  
**Execution Time:** ~45 seconds

#### Test Cases:

| Test                                             | Scenario                       | Validates                       |
| ------------------------------------------------ | ------------------------------ | ------------------------------- |
| `TestRapidPlayerJoinLeave`                       | 10 players join/leave rapidly  | System stability under churn    |
| `TestHighLatencyScenario`                        | RTT 150-250ms                  | Game playable with high latency |
| `TestPacketLoss_SimulatedDropRate`               | 5% packet loss                 | Graceful degradation            |
| `TestBandwidthConstraint_LimitedConnection`      | 1 Mbps connection              | Works on limited bandwidth      |
| `TestNetworkJitter_LatencySpikes`                | Alternating 20ms/150ms latency | Jitter tolerance                |
| `TestConnectionTimeout_NoMessagesReceived`       | No responses for 2 seconds     | Timeout detection               |
| `TestOutOfOrderMessages_Reordering`              | 10% out-of-order arrival       | Reordering detection            |
| `TestMaxPlayerLimit_FourPlayers`                 | Attempt 5th player when max=4  | Graceful player limits          |
| `TestAsyncMessageProcessing_DifferentFrameRates` | 60 FPS then 30 FPS             | Frame rate adaptation           |
| `TestCongestionHandling`                         | Bursty traffic patterns        | Congestion robustness           |

**Expected Results:**

- 5% packet loss → System continues functioning
- 200ms latency → Playable but noticeable
- Out-of-order messages → Detected and logged
- Timeouts → Handled without crashes

---

### 3. MultiPlayerNetworkProfiler.cs

**Purpose:** Real-time network metrics collection and analysis  
**Features:**

- Round-trip latency measurement
- Bandwidth tracking (bytes sent/received)
- Message counting and loss detection
- Disconnection/reconnection tracking
- State synchronization metrics
- Position update delta measurement

#### Key Metrics:

```csharp
LatencyStats
├── averageLatencyMs     // RTT average
├── minLatencyMs         // Best case
├── maxLatencyMs         // Worst case
└── sampleCount          // Number of measurements

BandwidthStats
├── totalBytesSent       // Total upstream
├── totalBytesReceived   // Total downstream
├── avgBandwidthKbps     // Average rate
├── peakBandwidthKbps    // Spike rate
└── playerCount          // Active players

MessageStats
├── totalMessagesSent    // Outgoing count
├── totalMessagesReceived // Incoming count
├── messagesLost         // Drop count
└── avgMessagesPerSecond // Throughput
```

#### Usage Example:

```csharp
var profiler = gameObject.AddComponent<MultiPlayerNetworkProfiler>();
profiler.StartProfiling();

// Record network events
profiler.RecordMessageSent(64);  // 64 bytes sent
profiler.RecordMessageReceived(128);  // 128 bytes received
profiler.MeasureRoundTripTime();  // Log RTT sample

// Get statistics
var stats = profiler.GetBandwidthStats();
Debug.Log($"Bandwidth: {stats.avgBandwidthKbps} Kbps");
```

---

### 4. NetworkPerformanceBenchmark.cs

**Purpose:** Baseline performance measurements  
**Test Count:** 8 benchmarks  
**Execution Time:** ~60 seconds

#### Benchmarks:

| Benchmark                   | Metric                    | Target         |
| --------------------------- | ------------------------- | -------------- |
| `Message Encoding/Decoding` | Serialization overhead    | <1ms/iteration |
| `Latency Measurement`       | Profiler overhead         | <0.5ms         |
| `Bandwidth Tracking`        | Stats calculation         | <0.1ms         |
| `State Update Check`        | Distance threshold check  | <0.01ms        |
| `Multi-Player Update`       | 4-player frame update     | <1ms           |
| `Message Queue Operations`  | Enqueue/dequeue (50 msgs) | <2ms           |
| `Memory Allocation`         | Buffer allocation         | <0.5ms         |

**Output Format:**

```
Test: Message Encoding/Decoding
  Average: 0.0234 ms
  Range:   0.0100 - 0.1500 ms
  StdDev:  0.0089 ms
  Samples: 1000
```

---

## Running the Tests

### Option 1: Unity Test Runner UI

1. Open Test Runner window: `Window > Testing > Test Runner`
2. Select "Playmode" tab
3. Find tests under `Vampire.Gameplay.Tests`
4. Click "Run All" or select individual tests

### Option 2: Command Line

```bash
# Run all playmode tests
unity -runTests -testMode playmode -testCategory Vampire.Tests.Gameplay

# Run specific test
unity -runTests -testMode playmode -testFilter CoopMultiPlayerIntegrationTest

# Generate XML report
unity -runTests -testMode playmode -testResults test-results.xml
```

### Option 3: GitHub Actions (CI/CD)

Tests run automatically on:

- Push to `main` or `develop` branches
- Pull requests affecting `Assets/Scripts/Gameplay/` or `Assets/Scripts/RL/`

Pipeline stages:

1. **Playmode Tests** → CoopMultiPlayerIntegrationTest + CoopEdgeCasesIntegrationTest
2. **Performance Benchmarks** → NetworkPerformanceBenchmark
3. **Metrics Report** → Generate network metrics JSON
4. **PR Comment** → Post results to pull request

---

## Performance Targets

### Latency Requirements

| Scenario     | Target   | Threshold |
| ------------ | -------- | --------- |
| Local (LAN)  | 10-20ms  | <50ms     |
| 2 Players    | 15-30ms  | <100ms    |
| 3-4 Players  | 20-40ms  | <150ms    |
| Online (WAN) | 50-150ms | <300ms    |

### Bandwidth Requirements

| Scenario  | Per Player | Total (4P) |
| --------- | ---------- | ---------- |
| 2 Players | ~50 Kbps   | ~100 Kbps  |
| 3 Players | ~60 Kbps   | ~180 Kbps  |
| 4 Players | ~75 Kbps   | ~300 Kbps  |

**Note:** Includes input messages + state updates + handshakes

### Reliability Targets

| Metric               | Target        |
| -------------------- | ------------- |
| Message loss rate    | <1%           |
| Message ordering     | >99% in-order |
| Connection stability | 99.9% uptime  |
| Reconnection time    | <2 seconds    |

---

## Test Data Interpretation

### Latency Analysis

```
If latency < 50ms:   ✅ Excellent (LAN-like)
If latency 50-100ms: ✅ Good (acceptable for action game)
If latency 100-200ms: ⚠️  Fair (noticeable delay, playable)
If latency > 200ms:  ❌ Poor (unplayable)
```

### Bandwidth Analysis

```
If scaling < 2x per player:  ✅ Efficient (linear or better)
If scaling 2-3x per player:  ⚠️  Moderate (expected for 4P)
If scaling > 3x per player:  ❌ Inefficient (redundant data)
```

### Message Loss Analysis

```
If loss < 0.5%:  ✅ Excellent (resilient)
If loss 0.5-2%:  ⚠️  Fair (needs monitoring)
If loss > 2%:    ❌ Poor (unacceptable)
```

---

## Debugging Network Issues

### Issue: High Latency (>150ms average)

**Diagnosis:**

1. Check network profiler for spike pattern
2. Look for burst traffic causing congestion
3. Verify state update frequency

**Solutions:**

```csharp
// Reduce state update frequency
networkManager.stateUpdateIntervalMs = 100;  // Default: 50ms

// Reduce bandwidth usage
networkProfiler.RecordMessageSent(32);  // Smaller messages

// Enable compression
message.compressed = true;
```

### Issue: Packet Loss (>1%)

**Diagnosis:**

1. Check network quality (use profiler)
2. Verify router/firewall settings
3. Test wired vs wireless connection

**Solutions:**

```csharp
// Enable retry logic
if (messagesLost > 0)
{
    ResendFailedMessages();
}

// Increase redundancy
SendStateUpdateDuplicate();
```

### Issue: Memory Leaks

**Diagnosis:**

1. Run test with memory profiler
2. Look for growing queue sizes
3. Check for unreleased player contexts

**Solutions:**

```csharp
// Proper cleanup
public void OnDestroy()
{
    latencySamples.Clear();
    playerContexts.Clear();
}
```

---

## CI/CD Integration

### GitHub Actions Workflow

File: `.github/workflows/multi-player-tests.yml`

**Features:**

- Automatic test on push/PR
- Unity Test Runner integration
- Performance benchmarks
- Network metrics generation
- PR comment with results

**Example PR Comment:**

```
## 🌐 Network Performance Metrics

### Latency
- Average RTT: 25.43ms
- Max RTT: 89.12ms
- Min RTT: 12.05ms

### Bandwidth
- Avg Upstream: 42.15 Kbps
- Avg Downstream: 58.30 Kbps
- Peak: 120.45 Kbps

### 2+ Player Scenarios
- 2 Players: ✅ PASS
- 3 Players: ✅ PASS
- 4 Players: ✅ PASS
```

---

## Test Maintenance

### Adding New Tests

1. Create test in appropriate file
2. Use `[UnityTest]` attribute with `IEnumerator`
3. Include `Assert` statements
4. Document in this guide

### Updating Performance Targets

1. Run benchmarks on reference hardware
2. Update targets in this document
3. Commit with justification
4. Update CI thresholds

### Monitoring Results

1. Track test results over time
2. Create performance dashboard
3. Alert on regressions
4. Archive historical data

---

## Hardware Requirements

### Minimum (Testing)

- CPU: Dual-core @ 2.5 GHz
- RAM: 4 GB
- Network: 100 Mbps LAN

### Recommended (Development)

- CPU: Quad-core @ 3.5 GHz
- RAM: 8 GB
- Network: Gigabit LAN

### For CI/CD

- Container: ubuntu-latest (GitHub Actions)
- Unity Version: 2022 LTS
- Timeout: 10 minutes per test suite

---

## References

- [Unity Test Framework Documentation](https://docs.unity3d.com/Packages/com.unity.test-framework@latest)
- [Netcode for GameObjects API](https://docs-multiplayer.unity3d.com/netcode/current/api/index.html)
- [Network Performance Testing Best Practices](https://developer.valvesoftware.com/wiki/Source_SDK_Performance_Testing)

---

## Glossary

| Term            | Definition                                                    |
| --------------- | ------------------------------------------------------------- |
| RTT             | Round-Trip Time - time for message to reach server and return |
| Bandwidth       | Data transfer rate (bits/bytes per second)                    |
| Jitter          | Variance in latency between messages                          |
| Packet Loss     | Percentage of messages that don't arrive                      |
| Throughput      | Actual data flow rate achieved                                |
| Synchronization | Agreement between players on game state                       |

---

## Version History

| Date       | Version | Changes                                              |
| ---------- | ------- | ---------------------------------------------------- |
| 2025-12-31 | 1.0     | Initial test suite creation                          |
|            |         | Added CoopMultiPlayerIntegrationTest (10 tests)      |
|            |         | Added CoopEdgeCasesIntegrationTest (10 stress tests) |
|            |         | Added NetworkPerformanceBenchmark (8 benchmarks)     |
|            |         | Added MultiPlayerNetworkProfiler                     |
|            |         | Added CI/CD pipeline (GitHub Actions)                |
|            |         | Added this comprehensive guide                       |

---

**Last Updated:** December 31, 2025  
**Maintained By:** Development Team  
**Status:** ✅ Production Ready
