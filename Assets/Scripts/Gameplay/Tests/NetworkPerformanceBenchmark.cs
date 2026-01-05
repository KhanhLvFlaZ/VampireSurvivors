using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Vampire.Tests.Gameplay
{
    /// <summary>
    /// Performance benchmarks for network operations
    /// Measures latency, throughput, CPU overhead, and memory usage
    /// </summary>
    public class NetworkPerformanceBenchmark
    {
        public struct BenchmarkResult
        {
            public string testName;
            public float averageMs;
            public float minMs;
            public float maxMs;
            public float stdDeviation;
            public int sampleCount;
            public string unit;
        }

        private List<BenchmarkResult> results = new List<BenchmarkResult>();
        private GameObject testSceneRoot;
        private MultiPlayerNetworkProfiler profiler;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            testSceneRoot = new GameObject("BenchmarkScene");
            var profilerGo = new GameObject("Profiler");
            profilerGo.transform.SetParent(testSceneRoot.transform);
            profiler = profilerGo.AddComponent<MultiPlayerNetworkProfiler>();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (testSceneRoot != null)
                UnityEngine.Object.Destroy(testSceneRoot);

            PrintBenchmarkReport();
        }

        [UnityTest]
        public IEnumerator Benchmark_MessageEncodingDecoding()
        {
            // Benchmark message serialization/deserialization
            var samples = new List<float>();
            const int iterations = 1000;

            var testData = new byte[256];
            for (int i = 0; i < testData.Length; i++)
                testData[i] = (byte)(i % 256);

            for (int i = 0; i < iterations; i++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                // Simulate message encoding
                var encoded = EncodeMessage(testData);
                var decoded = DecodeMessage(encoded);

                watch.Stop();
                samples.Add((float)watch.Elapsed.TotalMilliseconds);

                if (i % 100 == 0)
                    yield return null;
            }

            var result = CalculateStats(samples, "Message Encoding/Decoding", "ms");
            results.Add(result);

            Assert.IsTrue(result.averageMs < 1f, $"Encoding should be fast, got {result.averageMs}ms");
        }

        [UnityTest]
        public IEnumerator Benchmark_LatencyMeasurement()
        {
            // Benchmark latency measurement accuracy
            var samples = new List<float>();
            profiler.StartProfiling();

            for (int i = 0; i < 100; i++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                profiler.MeasureRoundTripTime();
                watch.Stop();
                samples.Add((float)watch.Elapsed.TotalMilliseconds);

                yield return null;
            }

            var result = CalculateStats(samples, "Latency Measurement", "ms");
            results.Add(result);

            Assert.IsTrue(result.averageMs < 0.5f, "Measurement overhead should be minimal");
        }

        [UnityTest]
        public IEnumerator Benchmark_BandwidthTracking()
        {
            // Benchmark bandwidth calculation overhead
            var samples = new List<float>();
            profiler.StartProfiling();
            profiler.SetPlayerCount(2);

            for (int i = 0; i < 1000; i++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                profiler.RecordMessageSent(128);
                profiler.RecordMessageReceived(256);
                var stats = profiler.GetBandwidthStats();

                watch.Stop();
                samples.Add((float)watch.Elapsed.TotalMilliseconds);

                if (i % 100 == 0)
                    yield return null;
            }

            var result = CalculateStats(samples, "Bandwidth Tracking", "ms");
            results.Add(result);

            Assert.IsTrue(result.averageMs < 0.1f, "Bandwidth tracking should be lightweight");
        }

        [UnityTest]
        public IEnumerator Benchmark_StateUpdateThreshold()
        {
            // Benchmark determining when to send state updates
            var samples = new List<float>();
            var lastState = Vector3.zero;
            const float updateThreshold = 0.1f;

            for (int i = 0; i < 10000; i++)
            {
                var newState = new Vector3(
                    UnityEngine.Random.Range(-100f, 100f),
                    UnityEngine.Random.Range(-100f, 100f),
                    UnityEngine.Random.Range(-100f, 100f)
                );

                var watch = System.Diagnostics.Stopwatch.StartNew();

                // Check if update needed
                bool needsUpdate = Vector3.Distance(newState, lastState) > updateThreshold;
                if (needsUpdate)
                    lastState = newState;

                watch.Stop();
                samples.Add((float)watch.Elapsed.TotalMilliseconds);

                if (i % 1000 == 0)
                    yield return null;
            }

            var result = CalculateStats(samples, "State Update Check", "ms");
            results.Add(result);

            Assert.IsTrue(result.averageMs < 0.01f, "State check should be very fast");
        }

        [UnityTest]
        public IEnumerator Benchmark_MultiPlayerUpdate()
        {
            // Benchmark updating multiple players
            var players = new List<Vector3>[4];
            for (int i = 0; i < 4; i++)
                players[i] = new List<Vector3>();

            var samples = new List<float>();

            for (int frame = 0; frame < 600; frame++) // 10 seconds at 60 FPS
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                // Update 4 players
                for (int p = 0; p < 4; p++)
                {
                    var newPos = new Vector3(
                        UnityEngine.Random.Range(-50f, 50f),
                        0,
                        UnityEngine.Random.Range(-50f, 50f)
                    );
                    players[p].Add(newPos);
                }

                watch.Stop();
                samples.Add((float)watch.Elapsed.TotalMilliseconds);

                if (frame % 100 == 0)
                    yield return null;
            }

            var result = CalculateStats(samples, "Multi-Player Update (4 players)", "ms");
            results.Add(result);

            Assert.IsTrue(result.averageMs < 1f, "Should handle 4 players efficiently");
        }

        [UnityTest]
        public IEnumerator Benchmark_NetworkMessageQueueing()
        {
            // Benchmark message queue operations
            var queue = new Queue<byte[]>();
            var samples = new List<float>();

            const int messagesPerFrame = 50;
            const int frames = 100;

            for (int frame = 0; frame < frames; frame++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                // Enqueue messages
                for (int i = 0; i < messagesPerFrame; i++)
                {
                    var msg = new byte[128];
                    queue.Enqueue(msg);
                }

                // Dequeue and process
                while (queue.Count > 0)
                {
                    var msg = queue.Dequeue();
                    // Process message
                }

                watch.Stop();
                samples.Add((float)watch.Elapsed.TotalMilliseconds);

                yield return null;
            }

            var result = CalculateStats(samples, "Message Queue (50 msgs/frame)", "ms");
            results.Add(result);

            Assert.IsTrue(result.averageMs < 2f, "Message queue should be efficient");
        }

        [UnityTest]
        public IEnumerator Benchmark_MemoryAllocation()
        {
            // Benchmark memory allocation patterns
            var samples = new List<float>();
            var allocations = new List<byte[]>();

            for (int i = 0; i < 1000; i++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                // Allocate message buffer
                var buffer = new byte[256];
                allocations.Add(buffer);

                // Deallocate old buffer if list too large
                if (allocations.Count > 100)
                {
                    allocations.RemoveAt(0);
                }

                watch.Stop();
                samples.Add((float)watch.Elapsed.TotalMilliseconds);

                if (i % 100 == 0)
                    yield return null;
            }

            var result = CalculateStats(samples, "Memory Allocation", "ms");
            results.Add(result);

            Assert.IsTrue(result.averageMs < 0.5f, "Memory allocation should be reasonable");
        }

        // ==================== Helper Methods ====================

        private byte[] EncodeMessage(byte[] data)
        {
            var encoded = new byte[data.Length + 4];
            System.BitConverter.GetBytes(data.Length).CopyTo(encoded, 0);
            data.CopyTo(encoded, 4);
            return encoded;
        }

        private byte[] DecodeMessage(byte[] encoded)
        {
            int length = System.BitConverter.ToInt32(encoded, 0);
            var decoded = new byte[length];
            System.Array.Copy(encoded, 4, decoded, 0, length);
            return decoded;
        }

        private BenchmarkResult CalculateStats(List<float> samples, string testName, string unit)
        {
            if (samples.Count == 0)
                return new BenchmarkResult();

            float sum = 0, min = float.MaxValue, max = float.MinValue;
            foreach (var sample in samples)
            {
                sum += sample;
                min = Mathf.Min(min, sample);
                max = Mathf.Max(max, sample);
            }

            float average = sum / samples.Count;

            // Calculate standard deviation
            float sumSquaredDifferences = 0;
            foreach (var sample in samples)
            {
                sumSquaredDifferences += (sample - average) * (sample - average);
            }
            float stdDev = Mathf.Sqrt(sumSquaredDifferences / samples.Count);

            return new BenchmarkResult
            {
                testName = testName,
                averageMs = average,
                minMs = min,
                maxMs = max,
                stdDeviation = stdDev,
                sampleCount = samples.Count,
                unit = unit
            };
        }

        private void PrintBenchmarkReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("\n");
            report.AppendLine("=================================================");
            report.AppendLine("NETWORK PERFORMANCE BENCHMARK REPORT");
            report.AppendLine("=================================================");
            report.AppendLine();

            foreach (var result in results)
            {
                report.AppendLine($"Test: {result.testName}");
                report.AppendLine($"  Average: {result.averageMs:F4} {result.unit}");
                report.AppendLine($"  Range:   {result.minMs:F4} - {result.maxMs:F4} {result.unit}");
                report.AppendLine($"  StdDev:  {result.stdDeviation:F4} {result.unit}");
                report.AppendLine($"  Samples: {result.sampleCount}");
                report.AppendLine();
            }

            report.AppendLine("=================================================");
            report.AppendLine();

            Debug.Log(report.ToString());
        }
    }
}
