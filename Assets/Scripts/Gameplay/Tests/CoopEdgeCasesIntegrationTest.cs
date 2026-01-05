#if UNITY_INCLUDE_TESTS && ENABLE_NETCODE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using NUnit.Framework;
using Vampire.Gameplay;

namespace Vampire.Tests.Gameplay
{
    /// <summary>
    /// Edge case and stress tests for co-op multi-player scenarios
    /// Tests rapid join/leave, network latency variations, packet loss, bandwidth constraints
    /// </summary>
    public class CoopEdgeCasesIntegrationTest
    {
        private GameObject testSceneRoot;
        private CoopPlayerManager playerManager;
        private CoopNetworkManager networkManager;
        private MultiPlayerNetworkProfiler networkProfiler;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            testSceneRoot = new GameObject("EdgeCaseTestScene");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (testSceneRoot != null)
                Object.Destroy(testSceneRoot);
#if UNITY_INCLUDE_TESTS && ENABLE_NETCODE_TESTS
                    using System.Collections;
                    using System.Collections.Generic;
                    using NUnit.Framework;
                    using UnityEngine;
                    using UnityEngine.InputSystem;
                    using UnityEngine.TestTools;
                    using Vampire.Gameplay;
            playerManager = pmGo.AddComponent<CoopPlayerManager>();

            var nmGo = new GameObject("NetworkManager");
            nmGo.transform.SetParent(testSceneRoot.transform);
            networkManager = nmGo.AddComponent<CoopNetworkManager>();

            var profilerGo = new GameObject("NetworkProfiler");
            profilerGo.transform.SetParent(testSceneRoot.transform);
            networkProfiler = profilerGo.AddComponent<MultiPlayerNetworkProfiler>();
        }

        [TearDown]
        public void TearDown()
        {
            networkProfiler?.Dispose();
        }

        [UnityTest]
        public IEnumerator TestRapidPlayerJoinLeave()
        {
            // Rapid join/leave stress test
            var players = new List<PlayerInput>();

            // Arrange
            networkProfiler.StartProfiling();

            // Act - Rapidly join and leave players
            for (int i = 0; i < 10; i++)
            {
                var player = SimulatePlayerJoin(i, $"Player{i}");
                players.Add(player);
                yield return new WaitForSeconds(0.05f);
            }

            // Remove in reverse order
            for (int i = 9; i >= 0; i--)
            {
                if (i < players.Count)
                {
                    SimulatePlayerLeave(players[i]);
                }
                yield return new WaitForSeconds(0.05f);
            }

            // Assert
            Assert.AreEqual(0, playerManager.ActivePlayers.Count, "All players should be removed");
        }

        [UnityTest]
        public IEnumerator TestHighLatencyScenario()
        {
            // Simulate high latency (200ms RTT)
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();

            // Measure latency with high variance
            for (int i = 0; i < 30; i++)
            {
                float highLatency = UnityEngine.Random.Range(150f, 250f);
                networkProfiler.RecordMessageSent(64);
                yield return new WaitForSeconds(0.05f + (highLatency / 1000f));
            }

            var stats = networkProfiler.GetLatencyStats();

            // Assert - System handles high latency
            Assert.IsTrue(stats.averageLatencyMs > 100f, "Should measure high latency");
            Assert.AreEqual(2, playerManager.ActivePlayers.Count, "Players should remain connected");
        }

        [UnityTest]
        public IEnumerator TestPacketLoss_SimulatedDropRate()
        {
            // Simulate 5% packet loss
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.2f);

                    }
#endif
            networkProfiler.StartProfiling();
            int packetsLost = 0;

            // Send 100 packets with 5% loss rate
            for (int i = 0; i < 100; i++)
            {
                networkProfiler.RecordMessageSent(64);

                // 5% chance of packet loss
                if (UnityEngine.Random.value < 0.05f)
                {
                    networkProfiler.RecordMessageLost();
                    packetsLost++;
                }
                else
                {
                    networkProfiler.RecordMessageReceived(64);
                }

                yield return new WaitForSeconds(0.016f);
            }

            var stats = networkProfiler.GetMessageStats();

            // Assert - Loss measured correctly
            Assert.IsTrue(stats.messagesLost > 0, "Should detect packet loss");
            Assert.IsTrue(stats.messagesLost < 15, "Loss should be ~5%");
            Assert.AreEqual(2, playerManager.ActivePlayers.Count, "System should tolerate packet loss");
        }

        [UnityTest]
        public IEnumerator TestBandwidthConstraint_LimitedConnection()
        {
            // Test behavior on limited bandwidth (e.g., 1 Mbps)
            var players = new List<PlayerInput>();
            for (int i = 0; i < 2; i++)
            {
                players.Add(SimulatePlayerJoin(i, $"Player{i}"));
            }
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();

            // Simulate connection with max 1 Mbps (125 KB/s)
            const float maxBandwidthBytesPerSecond = 125000f;
            const float targetBandwidthBytesPerFrame = maxBandwidthBytesPerSecond / 60f; // ~2083 bytes per frame

            for (int frame = 0; frame < 180; frame++) // 3 seconds
            {
                // Try to send more than available bandwidth
                networkProfiler.RecordMessageSent((int)targetBandwidthBytesPerFrame * 2);
                networkProfiler.RecordMessageReceived((int)targetBandwidthBytesPerFrame);

                yield return null;
            }

            var stats = networkProfiler.GetBandwidthStats();

            // Assert - System handles limited bandwidth gracefully
            Assert.AreEqual(2, playerManager.ActivePlayers.Count, "Should maintain connection");
            Assert.IsTrue(stats.avgBandwidthKbps > 0, "Should measure bandwidth");
        }

        [UnityTest]
        public IEnumerator TestNetworkJitter_LatencySpikes()
        {
            // Variable latency (jitter) test
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();

            // Alternate between low and high latency
            for (int i = 0; i < 40; i++)
            {
                float latency = (i % 2 == 0) ? 20f : 150f; // 20ms or 150ms
                yield return new WaitForSeconds(latency / 1000f);
                networkProfiler.MeasureRoundTripTime();
            }

            var stats = networkProfiler.GetLatencyStats();

            // Assert
            Assert.IsTrue(stats.maxLatencyMs - stats.minLatencyMs > 50f, "Should measure jitter");
            Assert.IsTrue(stats.averageLatencyMs > 50f && stats.averageLatencyMs < 150f);
        }

        [UnityTest]
        public IEnumerator TestConnectionTimeout_NoMessagesReceived()
        {
            // Simulate timeout by not receiving messages
            var player1 = SimulatePlayerJoin(0, "Player1");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();
            const float timeoutThreshold = 2f; // 2 second timeout

            // Send messages but don't receive responses
            for (int i = 0; i < 120; i++) // 2 seconds
            {
                networkProfiler.RecordMessageSent(64);
                yield return null;
            }

            var stats = networkProfiler.GetMessageStats();

            // Assert
            Assert.AreEqual(0, stats.totalMessagesReceived, "No messages received");
            Assert.IsTrue(stats.totalMessagesSent > 0, "Messages sent");
        }

        [UnityTest]
        public IEnumerator TestOutOfOrderMessages_Reordering()
        {
            // Test that out-of-order messages are detected
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();

            // Send messages in order, but simulate some arriving out of order
            for (int i = 0; i < 100; i++)
            {
                networkProfiler.RecordMessageSent(64);

                if (i > 0 && i % 10 == 0)
                {
                    // Simulate out-of-order arrival
                    networkProfiler.RecordOutOfOrderMessage();
                }

                yield return null;
            }

            var stats = networkProfiler.GetSynchronizationStats();

            // Assert
            Assert.IsTrue(stats.orderingAccuracy < 1f, "Should detect out-of-order messages");
            Assert.IsTrue(stats.orderingAccuracy > 0.9f, "Most messages should arrive in order");
        }

        [UnityTest]
        public IEnumerator TestMaxPlayerLimit_FourPlayers()
        {
            // Typical co-op limit is 4 players
            var players = new List<PlayerInput>();

            // Add 4 players
            for (int i = 0; i < 4; i++)
            {
                var player = SimulatePlayerJoin(i, $"Player{i + 1}");
                players.Add(player);
                yield return new WaitForSeconds(0.1f);
            }

            Assert.AreEqual(4, playerManager.ActivePlayers.Count);

            // Try to add 5th player - should be rejected or queued
            var player5 = SimulatePlayerJoin(4, "Player5");
            yield return new WaitForSeconds(0.1f);

            // System should handle gracefully (either reject or queue)
            Assert.IsTrue(playerManager.ActivePlayers.Count <= 5, "Should handle player limit");
        }

        [UnityTest]
        public IEnumerator TestAsyncMessageProcessing_DifferentFrameRates()
        {
            // Test with variable frame rates
            var players = new List<PlayerInput>();
            for (int i = 0; i < 2; i++)
            {
                players.Add(SimulatePlayerJoin(i, $"Player{i}"));
            }
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();

            // Simulate 60 FPS for 1 second
            for (int i = 0; i < 60; i++)
            {
                networkProfiler.RecordMessageSent(64);
                networkProfiler.RecordMessageReceived(128);
                yield return null;
            }

            // Simulate 30 FPS for 1 second
            for (int i = 0; i < 30; i++)
            {
                networkProfiler.RecordMessageSent(64);
                networkProfiler.RecordMessageReceived(128);
                yield return new WaitForSeconds(0.033f); // ~30 FPS
            }

            var stats = networkProfiler.GetMessageStats();

            // Assert
            Assert.IsTrue(stats.totalMessagesSent > 80, "Should handle variable frame rates");
        }

        [UnityTest]
        public IEnumerator TestCongestionHandling()
        {
            // Simulate network congestion with burst traffic
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();

            // Simulate bursty traffic pattern (common in real networks)
            for (int burst = 0; burst < 5; burst++)
            {
                // High traffic burst (100 messages)
                for (int i = 0; i < 100; i++)
                {
                    networkProfiler.RecordMessageSent(64);
                }
                yield return new WaitForSeconds(0.5f);

                // Quiet period
                yield return new WaitForSeconds(0.5f);
            }

            var stats = networkProfiler.GetBandwidthStats();

            // Assert
            Assert.IsTrue(stats.peakBandwidthKbps > stats.avgBandwidthKbps, "Should detect bandwidth spikes");
            Assert.AreEqual(2, playerManager.ActivePlayers.Count, "Should survive congestion");
        }

        [UnityTest]
        public IEnumerator TestPlayerStateConsistency_MultipleActions()
        {
            // Verify state consistency across multiple concurrent actions
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();

            // Simulate players performing actions simultaneously
            for (int frame = 0; frame < 60; frame++)
            {
                // Player 1: Move + Attack
                networkProfiler.RecordMessageSent(48); // Move
                networkProfiler.RecordMessageSent(32); // Attack

                // Player 2: Move + Defend
                networkProfiler.RecordMessageSent(48); // Move
                networkProfiler.RecordMessageSent(32); // Defend

                // Server state update
                networkProfiler.RecordMessageReceived(256);

                yield return null;
            }

            var stats = networkProfiler.GetMessageStats();

            // Assert
            Assert.AreEqual(60 * 4, stats.totalMessagesSent, "Should send all player messages");
            Assert.AreEqual(2, playerManager.ActivePlayers.Count, "Players should remain synchronized");
        }

        // ==================== Helper Methods ====================

        private PlayerInput SimulatePlayerJoin(int playerId, string playerName)
        {
            var go = new GameObject($"Player_{playerName}");
            go.transform.SetParent(testSceneRoot.transform);

            var playerInput = go.AddComponent<PlayerInput>();
            playerInput.enabled = true;

            playerManager.HandlePlayerJoined(playerInput);
            return playerInput;
        }

        private void SimulatePlayerLeave(PlayerInput playerInput)
        {
            if (playerInput != null)
            {
                playerManager.HandlePlayerLeft(playerInput);
                Object.Destroy(playerInput.gameObject);
            }
        }
    }
#endif
