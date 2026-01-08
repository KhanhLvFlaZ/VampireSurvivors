using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire.Tests.Gameplay
{
    /// <summary>
    /// Network profiler for multi-player latency and bandwidth measurement
    /// Tracks round-trip time, bandwidth usage, message stats, and synchronization quality
    /// </summary>
    public class MultiPlayerNetworkProfiler : MonoBehaviour
    {
        public struct LatencyStats
        {
            public float averageLatencyMs;
            public float minLatencyMs;
            public float maxLatencyMs;
            public int sampleCount;
        }

        public struct BandwidthStats
        {
            public long totalBytesSent;
            public long totalBytesReceived;
            public float avgBandwidthKbps;
            public float peakBandwidthKbps;
            public int playerCount;
            public float duration;
        }

        public struct MessageStats
        {
            public int totalMessagesSent;
            public int totalMessagesReceived;
            public int messagesLost;
            public float avgMessagesPerSecond;
        }

        public struct ReconnectionStats
        {
            public int disconnectionCount;
            public int reconnectionCount;
            public float avgReconnectionTimeMs;
        }

        public struct SynchronizationStats
        {
            public float positionDeltaMs;
            public float stateDeltaMs;
            public float orderingAccuracy; // 0-1, where 1 = perfect ordering
        }

        private bool isProfiling = false;
        private float profilingStartTime = 0f;

        // Latency tracking
        private Queue<float> latencySamples = new Queue<float>();
        private float lastRoundTripTime = 0f;

        // Bandwidth tracking
        private long bytesSentThisFrame = 0;
        private long bytesReceivedThisFrame = 0;
        private long totalBytesSent = 0;
        private long totalBytesReceived = 0;
        private Queue<float> bandwidthSamples = new Queue<float>();

        // Message tracking
        private int messagesSent = 0;
        private int messagesReceived = 0;
        private int messagesLost = 0;
        private List<int> messageSequenceNumbers = new List<int>();

        // Disconnection/Reconnection tracking
        private int disconnectionCount = 0;
        private int reconnectionCount = 0;
        private List<float> reconnectionTimes = new List<float>();

        // Synchronization tracking
        private Queue<Vector3> positionUpdates = new Queue<Vector3>();
        private Queue<float> stateUpdateDeltas = new Queue<float>();
        private int outOfOrderMessages = 0;

        private int playerCount = 0;

        public void StartProfiling()
        {
            isProfiling = true;
            profilingStartTime = Time.realtimeSinceStartup;

            latencySamples.Clear();
            bandwidthSamples.Clear();
            messageSequenceNumbers.Clear();
            reconnectionTimes.Clear();
            positionUpdates.Clear();
            stateUpdateDeltas.Clear();

            totalBytesSent = 0;
            totalBytesReceived = 0;
            messagesSent = 0;
            messagesReceived = 0;
            messagesLost = 0;
            disconnectionCount = 0;
            reconnectionCount = 0;
            outOfOrderMessages = 0;
        }

        public void StopProfiling()
        {
            isProfiling = false;
        }

        public void SetPlayerCount(int count)
        {
            playerCount = count;
        }

        /// <summary>
        /// Measure round-trip latency (simulated)
        /// </summary>
        public float MeasureRoundTripTime()
        {
            if (!isProfiling) return 0f;

            // Simulate network latency (in real scenario, this would measure actual RTT)
            float latency = UnityEngine.Random.Range(10f, 50f); // 10-50ms typical
            latencySamples.Enqueue(latency);
            lastRoundTripTime = latency;

            if (latencySamples.Count > 1000)
                latencySamples.Dequeue();

            return latency;
        }

        public void RecordMessageSent(int bytes)
        {
            if (!isProfiling) return;

            bytesSentThisFrame += bytes;
            totalBytesSent += bytes;
            messagesSent++;
            messageSequenceNumbers.Add(messagesSent);
        }

        public void RecordMessageReceived(int bytes)
        {
            if (!isProfiling) return;

            bytesReceivedThisFrame += bytes;
            totalBytesReceived += bytes;
            messagesReceived++;
        }

        public void RecordMessageLost()
        {
            if (!isProfiling) return;
            messagesLost++;
        }

        public void RecordDisconnection()
        {
            if (!isProfiling) return;
            disconnectionCount++;
        }

        public void RecordReconnection(float timeMs)
        {
            if (!isProfiling) return;
            reconnectionCount++;
            reconnectionTimes.Add(timeMs);
        }

        public void RecordPositionUpdate(Vector3 position)
        {
            if (!isProfiling) return;
            positionUpdates.Enqueue(position);
            if (positionUpdates.Count > 100)
                positionUpdates.Dequeue();
        }

        public void RecordStateUpdateDelta(float deltaMs)
        {
            if (!isProfiling) return;
            stateUpdateDeltas.Enqueue(deltaMs);
            if (stateUpdateDeltas.Count > 100)
                stateUpdateDeltas.Dequeue();
        }

        public void RecordOutOfOrderMessage()
        {
            if (!isProfiling) return;
            outOfOrderMessages++;
        }

        // ==================== Statistics Retrieval ====================

        public LatencyStats GetLatencyStats()
        {
            var stats = new LatencyStats();

            if (latencySamples.Count == 0)
                return stats;

            float sum = 0;
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach (var sample in latencySamples)
            {
                sum += sample;
                min = Mathf.Min(min, sample);
                max = Mathf.Max(max, sample);
            }

            stats.averageLatencyMs = sum / latencySamples.Count;
            stats.minLatencyMs = min;
            stats.maxLatencyMs = max;
            stats.sampleCount = latencySamples.Count;

            return stats;
        }

        public BandwidthStats GetBandwidthStats()
        {
            var stats = new BandwidthStats();
            float duration = Time.realtimeSinceStartup - profilingStartTime;

            stats.totalBytesSent = totalBytesSent;
            stats.totalBytesReceived = totalBytesReceived;
            stats.duration = duration;
            stats.playerCount = playerCount;

            if (duration > 0)
            {
                float totalKilobytes = (totalBytesSent + totalBytesReceived) / 1024f;
                stats.avgBandwidthKbps = (totalKilobytes / duration);
            }

            if (bandwidthSamples.Count > 0)
            {
                float max = 0;
                foreach (var sample in bandwidthSamples)
                    max = Mathf.Max(max, sample);
                stats.peakBandwidthKbps = max;
            }

            return stats;
        }

        public MessageStats GetMessageStats()
        {
            var stats = new MessageStats();
            float duration = Time.realtimeSinceStartup - profilingStartTime;

            stats.totalMessagesSent = messagesSent;
            stats.totalMessagesReceived = messagesReceived;
            stats.messagesLost = messagesLost;

            if (duration > 0)
                stats.avgMessagesPerSecond = (messagesSent + messagesReceived) / duration;

            return stats;
        }

        public ReconnectionStats GetReconnectionStats()
        {
            var stats = new ReconnectionStats();
            stats.disconnectionCount = disconnectionCount;
            stats.reconnectionCount = reconnectionCount;

            if (reconnectionTimes.Count > 0)
            {
                float sum = 0;
                foreach (var time in reconnectionTimes)
                    sum += time;
                stats.avgReconnectionTimeMs = sum / reconnectionTimes.Count;
            }

            return stats;
        }

        public SynchronizationStats GetSynchronizationStats()
        {
            var stats = new SynchronizationStats();

            if (stateUpdateDeltas.Count > 0)
            {
                float sum = 0;
                foreach (var delta in stateUpdateDeltas)
                    sum += delta;
                stats.stateDeltaMs = sum / stateUpdateDeltas.Count;
            }

            if (positionUpdates.Count > 1)
            {
                var positions = new List<Vector3>(positionUpdates);
                float sumDelta = 0;
                for (int i = 1; i < positions.Count; i++)
                {
                    sumDelta += Vector3.Distance(positions[i], positions[i - 1]);
                }
                stats.positionDeltaMs = sumDelta / (positions.Count - 1);
            }

            int totalMessages = messagesSent + messagesReceived;
            if (totalMessages > 0)
            {
                stats.orderingAccuracy = 1f - ((float)outOfOrderMessages / totalMessages);
                stats.orderingAccuracy = Mathf.Clamp01(stats.orderingAccuracy);
            }

            return stats;
        }

        /// <summary>
        /// Generate a detailed profiling report
        /// </summary>
        public string GenerateReport()
        {
            var latency = GetLatencyStats();
            var bandwidth = GetBandwidthStats();
            var messages = GetMessageStats();
            var reconnection = GetReconnectionStats();
            var sync = GetSynchronizationStats();

            var report = new System.Text.StringBuilder();
            report.AppendLine("\n=== MULTI-PLAYER NETWORK PROFILING REPORT ===");
            report.AppendLine($"Duration: {bandwidth.duration:F2}s | Players: {bandwidth.playerCount}");
            report.AppendLine();

            report.AppendLine("--- LATENCY ---");
            report.AppendLine($"Average: {latency.averageLatencyMs:F2}ms");
            report.AppendLine($"Min: {latency.minLatencyMs:F2}ms | Max: {latency.maxLatencyMs:F2}ms");
            report.AppendLine($"Samples: {latency.sampleCount}");
            report.AppendLine();

            report.AppendLine("--- BANDWIDTH ---");
            report.AppendLine($"Sent: {bandwidth.totalBytesSent / 1024f:F2} KB");
            report.AppendLine($"Received: {bandwidth.totalBytesReceived / 1024f:F2} KB");
            report.AppendLine($"Average: {bandwidth.avgBandwidthKbps:F2} Kbps");
            report.AppendLine($"Peak: {bandwidth.peakBandwidthKbps:F2} Kbps");
            report.AppendLine();

            report.AppendLine("--- MESSAGES ---");
            report.AppendLine($"Sent: {messages.totalMessagesSent}");
            report.AppendLine($"Received: {messages.totalMessagesReceived}");
            report.AppendLine($"Lost: {messages.messagesLost}");
            report.AppendLine($"Avg Rate: {messages.avgMessagesPerSecond:F2} msg/s");
            report.AppendLine();

            report.AppendLine("--- RECONNECTION ---");
            report.AppendLine($"Disconnections: {reconnection.disconnectionCount}");
            report.AppendLine($"Reconnections: {reconnection.reconnectionCount}");
            if (reconnection.reconnectionCount > 0)
                report.AppendLine($"Avg Time: {reconnection.avgReconnectionTimeMs:F2}ms");
            report.AppendLine();

            report.AppendLine("--- SYNCHRONIZATION ---");
            report.AppendLine($"Position Delta: {sync.positionDeltaMs:F2}");
            report.AppendLine($"State Update Delta: {sync.stateDeltaMs:F2}ms");
            report.AppendLine($"Message Ordering: {(sync.orderingAccuracy * 100f):F1}%");
            report.AppendLine();

            report.AppendLine("===========================================\n");

            return report.ToString();
        }

        public void Dispose()
        {
            StopProfiling();
            latencySamples.Clear();
            bandwidthSamples.Clear();
            messageSequenceNumbers.Clear();
            reconnectionTimes.Clear();
            positionUpdates.Clear();
            stateUpdateDeltas.Clear();
        }

        private void OnGUI()
        {
            if (!isProfiling) return;

            var latency = GetLatencyStats();
            var bandwidth = GetBandwidthStats();
            var messages = GetMessageStats();

            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.Label($"Network Profiler (Players: {playerCount})");
            GUILayout.Label($"Latency: {latency.averageLatencyMs:F1}ms (max: {latency.maxLatencyMs:F1}ms)");
            GUILayout.Label($"Bandwidth: {bandwidth.avgBandwidthKbps:F2} Kbps");
            GUILayout.Label($"Messages: {messages.totalMessagesSent} sent | {messages.totalMessagesReceived} received");
            GUILayout.EndArea();
        }
    }
}
