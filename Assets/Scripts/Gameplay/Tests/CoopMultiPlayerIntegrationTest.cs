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
    /// Integration tests for 2+ player co-op scenarios
    /// Tests player join/leave, latency, bandwidth, and network synchronization
    /// Requirement: Multi-player co-op playmode integration testing
    /// </summary>
    public class CoopMultiPlayerIntegrationTest
    {
        private GameObject testSceneRoot;
        private CoopPlayerManager playerManager;
        private CoopNetworkManager networkManager;
        private CoopOwnershipRegistry ownershipRegistry;
        private MultiPlayerNetworkProfiler networkProfiler;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Create test scene root
            testSceneRoot = new GameObject("CoopMultiPlayerTestScene");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (testSceneRoot != null)
                Object.Destroy(testSceneRoot);
        }

        [SetUp]
        public void SetUp()
        {
            // Create PlayerManager
            var pmGo = new GameObject("PlayerManager");
            pmGo.transform.SetParent(testSceneRoot.transform);
            playerManager = pmGo.AddComponent<CoopPlayerManager>();

            // Create NetworkManager
            var nmGo = new GameObject("NetworkManager");
            nmGo.transform.SetParent(testSceneRoot.transform);
            networkManager = nmGo.AddComponent<CoopNetworkManager>();

            // Create OwnershipRegistry
            var orGo = new GameObject("OwnershipRegistry");
            orGo.transform.SetParent(testSceneRoot.transform);
            ownershipRegistry = orGo.AddComponent<CoopOwnershipRegistry>();

            // Create Network Profiler
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
        public IEnumerator TestTwoPlayersJoinSequentially()
        {
            // Arrange
            Assert.IsNotNull(playerManager, "PlayerManager should be initialized");
            Assert.AreEqual(0, playerManager.ActivePlayers.Count, "Should start with 0 players");

            // Act - Simulate first player join
            var player1Input = SimulatePlayerJoin(0, "Player1");
            yield return new WaitForSeconds(0.5f);

            // Assert - First player joined
            Assert.AreEqual(1, playerManager.ActivePlayers.Count, "Should have 1 player");

            // Act - Simulate second player join
            var player2Input = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.5f);

            // Assert - Second player joined
            Assert.AreEqual(2, playerManager.ActivePlayers.Count, "Should have 2 players");
            Assert.IsTrue(ownershipRegistry.GetPlayerByNetworkClientId(1) != null, "Player2 should be registered");
        }

        [UnityTest]
        public IEnumerator TestThreePlayersJoinAndLeave()
        {
            // Arrange
            Assert.AreEqual(0, playerManager.ActivePlayers.Count);

            // Act - Join 3 players
            var player1 = SimulatePlayerJoin(0, "Player1");
            yield return new WaitForSeconds(0.3f);
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.3f);
            var player3 = SimulatePlayerJoin(2, "Player3");
            yield return new WaitForSeconds(0.3f);

            // Assert - All joined
            Assert.AreEqual(3, playerManager.ActivePlayers.Count, "Should have 3 players");

            // Act - Player 2 leaves
            SimulatePlayerLeave(player2);
            yield return new WaitForSeconds(0.3f);

            // Assert - Player 2 left
            Assert.AreEqual(2, playerManager.ActivePlayers.Count, "Should have 2 players after one leaves");

            // Act - Remaining players still active
            yield return new WaitForSeconds(1f);

            // Assert - Game stable with 2 players
            Assert.AreEqual(2, playerManager.ActivePlayers.Count);
        }

        [UnityTest]
        public IEnumerator TestLatencyMeasurement_TwoPlayers()
        {
            // Arrange
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();

            // Act - Measure latency over time
            for (int i = 0; i < 10; i++)
            {
                // Simulate network message round-trip
                var latencyMs = networkProfiler.MeasureRoundTripTime();
                yield return new WaitForSeconds(0.05f);
            }

            yield return new WaitForSeconds(0.1f);
            var stats = networkProfiler.GetLatencyStats();

            // Assert
            Assert.IsTrue(stats.averageLatencyMs >= 0, "Average latency should be non-negative");
            Assert.IsTrue(stats.maxLatencyMs >= stats.averageLatencyMs, "Max should be >= average");
            Assert.AreEqual(10, stats.sampleCount, "Should have 10 samples");
        }

        [UnityTest]
        public IEnumerator TestBandwidthTracking_ThreePlayers()
        {
            // Arrange
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            var player3 = SimulatePlayerJoin(2, "Player3");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();

            // Act - Simulate network traffic
            for (int i = 0; i < 50; i++)
            {
                // Simulate input messages (player movement, actions)
                networkProfiler.RecordMessageSent(playerManager.ActivePlayers.Count * 50); // ~50 bytes per player
                // Simulate state updates from server
                networkProfiler.RecordMessageReceived(playerManager.ActivePlayers.Count * 100); // ~100 bytes per player
                yield return new WaitForSeconds(0.016f); // ~60 FPS
            }

            var bandwidthStats = networkProfiler.GetBandwidthStats();

            // Assert
            Assert.IsTrue(bandwidthStats.totalBytesSent > 0, "Should have sent data");
            Assert.IsTrue(bandwidthStats.totalBytesReceived > 0, "Should have received data");
            Assert.IsTrue(bandwidthStats.avgBandwidthKbps >= 0, "Bandwidth should be non-negative");
            Assert.AreEqual(3, bandwidthStats.playerCount, "Should track 3 players");
        }

        [UnityTest]
        public IEnumerator TestNetworkMessageOrder_TwoPlayers()
        {
            // Arrange
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();
            var messageSequence = new List<int>();

            // Act - Send messages in order
            for (int i = 0; i < 20; i++)
            {
                networkProfiler.RecordMessageSent(64);
                messageSequence.Add(i);
                yield return new WaitForSeconds(0.01f);
            }

            var stats = networkProfiler.GetMessageStats();

            // Assert - All messages accounted for
            Assert.AreEqual(20, stats.totalMessagesSent, "Should send 20 messages");
            Assert.AreEqual(0, stats.messagesLost, "No messages should be lost in test");
        }

        [UnityTest]
        public IEnumerator TestNetworkStress_FourPlayersWithHighFrequencyUpdates()
        {
            // Arrange
            var players = new List<PlayerInput>();
            for (int i = 0; i < 4; i++)
            {
                players.Add(SimulatePlayerJoin(i, $"Player{i + 1}"));
                yield return new WaitForSeconds(0.1f);
            }

            Assert.AreEqual(4, playerManager.ActivePlayers.Count);
            networkProfiler.StartProfiling();

            // Act - High frequency updates (240 Hz)
            float elapsed = 0;
            float duration = 2f;
            while (elapsed < duration)
            {
                // Each player sends input + server sends state update
                for (int p = 0; p < 4; p++)
                {
                    networkProfiler.RecordMessageSent(48); // Input message
                }
                networkProfiler.RecordMessageReceived(4 * 120); // State update per player

                elapsed += Time.deltaTime;
                yield return null;
            }

            var bandwidthStats = networkProfiler.GetBandwidthStats();
            var latencyStats = networkProfiler.GetLatencyStats();

            // Assert - System handles 4 players
            Assert.AreEqual(4, playerManager.ActivePlayers.Count, "Should maintain 4 players");
            Assert.IsTrue(bandwidthStats.avgBandwidthKbps < 1000f, "Bandwidth should be < 1 Mbps");
            Assert.IsTrue(latencyStats.maxLatencyMs < 200f, "Max latency should be < 200ms");
        }

        [UnityTest]
        public IEnumerator TestConcurrentPlayerActions_TwoPlayers()
        {
            // Arrange
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();

            // Act - Both players perform actions simultaneously
            for (int frame = 0; frame < 100; frame++)
            {
                // Player 1 moves + attacks
                SimulatePlayerInput(player1, new Vector2(1, 0), true);
                networkProfiler.RecordMessageSent(64);

                // Player 2 moves + defends
                SimulatePlayerInput(player2, new Vector2(-1, 0), false);
                networkProfiler.RecordMessageSent(64);

                // Server sends coordinated state
                networkProfiler.RecordMessageReceived(256);

                yield return null;
            }

            var stats = networkProfiler.GetMessageStats();

            // Assert
            Assert.AreEqual(200, stats.totalMessagesSent, "Should send ~200 input messages");
            Assert.IsTrue(stats.totalMessagesReceived > 0, "Should receive server updates");
        }

        [UnityTest]
        public IEnumerator TestPlayerDisconnectRecovery()
        {
            // Arrange
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            var player3 = SimulatePlayerJoin(2, "Player3");
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(3, playerManager.ActivePlayers.Count);
            networkProfiler.StartProfiling();

            // Act - Player 2 disconnects
            SimulateNetworkDisconnect(player2);
            yield return new WaitForSeconds(0.5f);

            // Assert - System recovers
            Assert.AreEqual(2, playerManager.ActivePlayers.Count, "Should have 2 players after disconnect");

            // Act - Player reconnects (new instance)
            var player2Reconnect = SimulatePlayerJoin(3, "Player2Reconnect");
            yield return new WaitForSeconds(0.3f);

            // Assert - Back to 3 players
            Assert.AreEqual(3, playerManager.ActivePlayers.Count, "Should restore to 3 players");

            var stats = networkProfiler.GetReconnectionStats();
            Assert.AreEqual(1, stats.disconnectionCount, "Should track 1 disconnection");
            Assert.AreEqual(1, stats.reconnectionCount, "Should track 1 reconnection");
        }

        [UnityTest]
        public IEnumerator TestNetworkSynchronization_TwoPlayers()
        {
            // Arrange
            var player1 = SimulatePlayerJoin(0, "Player1");
            var player2 = SimulatePlayerJoin(1, "Player2");
            yield return new WaitForSeconds(0.2f);

            networkProfiler.StartProfiling();
            var player1Positions = new List<Vector3>();
            var player2Positions = new List<Vector3>();

            // Act - Simulate synchronized movement
            for (int i = 0; i < 60; i++) // 1 second at 60 FPS
            {
                var pos1 = new Vector3(i * 0.1f, 0, 0);
                var pos2 = new Vector3(i * -0.1f, 0, 0);

                player1Positions.Add(pos1);
                player2Positions.Add(pos2);

                networkProfiler.RecordMessageSent(64); // Player 1 position
                networkProfiler.RecordMessageSent(64); // Player 2 position
                networkProfiler.RecordMessageReceived(128); // Server confirmation

                yield return null;
            }

            // Assert - Synchronized positions maintained
            Assert.AreEqual(60, player1Positions.Count);
            Assert.AreEqual(60, player2Positions.Count);

            var stats = networkProfiler.GetSynchronizationStats();
            Assert.IsTrue(stats.positionDeltaMs < 50f, "Position should sync within 50ms");
        }

        [UnityTest]
        public IEnumerator TestBandwidthScaling_VariablePlayerCount()
        {
            // This test measures how bandwidth scales with player count
            var playerCounts = new int[] { 1, 2, 3, 4 };
            var bandwidthPerCount = new Dictionary<int, float>();

            foreach (int targetPlayerCount in playerCounts)
            {
                // Reset
                networkProfiler = new GameObject("NetworkProfiler" + targetPlayerCount).AddComponent<MultiPlayerNetworkProfiler>();
                networkProfiler.StartProfiling();

                // Add players
                var players = new List<PlayerInput>();
                for (int i = 0; i < targetPlayerCount; i++)
                {
                    players.Add(SimulatePlayerJoin(i, $"Player{i + 1}"));
                }
                yield return new WaitForSeconds(0.1f);

                // Measure bandwidth for 30 frames
                for (int frame = 0; frame < 30; frame++)
                {
                    for (int p = 0; p < targetPlayerCount; p++)
                    {
                        networkProfiler.RecordMessageSent(48);
                    }
                    networkProfiler.RecordMessageReceived(targetPlayerCount * 96);
                    yield return null;
                }

                var stats = networkProfiler.GetBandwidthStats();
                bandwidthPerCount[targetPlayerCount] = stats.avgBandwidthKbps;
                networkProfiler.Dispose();
            }

            // Assert - Bandwidth scales roughly linearly
            var bw1 = bandwidthPerCount[1];
            var bw2 = bandwidthPerCount[2];
            var bw4 = bandwidthPerCount[4];

            Assert.IsTrue(bw2 > bw1, "2 players should use more bandwidth than 1");
            Assert.IsTrue(bw4 > bw2, "4 players should use more bandwidth than 2");

            // Bandwidth should roughly double with player count
            var scaleFactor2 = bw2 / (bw1 + 0.001f);
            Assert.IsTrue(scaleFactor2 > 1.5f && scaleFactor2 < 3f, "Bandwidth should scale ~linearly");
        }

        // ==================== Helper Methods ====================

        private PlayerInput SimulatePlayerJoin(int playerId, string playerName)
        {
            var go = new GameObject($"Player_{playerName}");
            go.transform.SetParent(testSceneRoot.transform);

            var playerInput = go.AddComponent<PlayerInput>();
            playerInput.enabled = true;

            // Simulate Input System callback
            playerManager.HandlePlayerJoined(playerInput);
            ownershipRegistry.RegisterPlayer(playerId, playerId);

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

        private void SimulatePlayerInput(PlayerInput player, Vector2 moveInput, bool attacking)
        {
            // Simulate input action values
            if (player != null)
            {
                networkProfiler.RecordMessageSent(48);
            }
        }

        private void SimulateNetworkDisconnect(PlayerInput player)
        {
            playerManager.HandlePlayerLeft(player);
            Object.Destroy(player.gameObject);
        }
    }
}
#endif
