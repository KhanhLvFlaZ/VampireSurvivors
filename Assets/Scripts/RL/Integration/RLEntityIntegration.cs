using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Vampire.RL
{
    /// <summary>
    /// Extension component for EntityManager that handles RL-specific monster spawning and management
    /// Requirement: 1.1 - RL monster agent initialization, 1.5 - Multi-agent coordination
    /// </summary>
    [RequireComponent(typeof(EntityManager))]
    public class RLEntityIntegration : MonoBehaviour
    {
        private EntityManager entityManager;
        private RLLevelConfiguration rlLevelConfig;
        private List<RLMonster> activeRLMonsters = new List<RLMonster>();
        private Dictionary<string, int> rlMonsterPoolIndices = new Dictionary<string, int>();
        private int baseMonsterPoolCount;

        // RL-specific pools for coordination
        private RLMonsterCoordinationPool coordinationPool;

        // Performance tracking
        private int currentRLAgentCount = 0;
        private float lastCoordinationUpdateTime = 0f;
        private float coordinationUpdateInterval = 0.1f;

        // Event system
        public delegate void RLMonsterSpawnedHandler(RLMonster monster);
        public delegate void RLMonsterDespawnedHandler(RLMonster monster);
        public delegate void RLAgentCountChangedHandler(int newCount);

        public event RLMonsterSpawnedHandler OnRLMonsterSpawned;
        public event RLMonsterDespawnedHandler OnRLMonsterDespawned;
        public event RLAgentCountChangedHandler OnRLAgentCountChanged;

        public int CurrentRLAgentCount => currentRLAgentCount;
        public RLMonster[] ActiveRLMonsters => activeRLMonsters.ToArray();
        public RLLevelConfiguration RLLevelConfig => rlLevelConfig;

        private void Awake()
        {
            entityManager = GetComponent<EntityManager>();
        }

        /// <summary>
        /// Initialize RL integration with level configuration
        /// Requirement: 1.1 - Consistent initialization
        /// </summary>
        public void InitializeRL(RLLevelConfiguration levelConfig)
        {
            if (levelConfig == null || !levelConfig.EnableRLForLevel)
                return;

            rlLevelConfig = levelConfig;

            // Validate configuration
            if (!levelConfig.Validate(out string errorMsg))
            {
                Debug.LogError($"RLLevelConfiguration validation failed: {errorMsg}");
                return;
            }

            // Store initial pool count before adding RL pools
            baseMonsterPoolCount = entityManager.GetMonsterPoolCount();

            // Initialize coordination pool if enabled
            if (levelConfig.EnableCoordinationLearning)
            {
                coordinationPool = new RLMonsterCoordinationPool(
                    levelConfig.MaxConcurrentRLAgents,
                    (CoordinationStrategy)levelConfig.CoordinationStrategyIndex
                );
            }

            Debug.Log($"RL Integration initialized for level with max {levelConfig.MaxConcurrentRLAgents} concurrent agents");
        }

        /// <summary>
        /// Spawn an RL-enabled monster
        /// Returns null if RL is not enabled or agent limit exceeded
        /// Requirement: 1.1
        /// </summary>
        public RLMonster SpawnRLMonster(RLMonsterBlueprint rlBlueprint, Vector2 position)
        {
            if (rlLevelConfig == null || !rlLevelConfig.EnableRLForLevel)
                return null;

            // Check concurrent agent limit
            // Requirement: 1.5 - Multi-agent coordination constraint
            if (currentRLAgentCount >= rlLevelConfig.MaxConcurrentRLAgents)
            {
                Debug.LogWarning($"Cannot spawn RL monster: at max concurrent agents ({rlLevelConfig.MaxConcurrentRLAgents})");
                return null;
            }

            if (rlBlueprint == null)
            {
                Debug.LogError("Cannot spawn RL monster: blueprint is null");
                return null;
            }

            // Validate the blueprint
            if (!rlBlueprint.Validate(out string errorMsg))
            {
                Debug.LogError($"RLMonsterBlueprint validation failed: {errorMsg}");
                return null;
            }

            // Get or create pool for this RL blueprint
            int poolIndex = GetOrCreateRLMonsterPool(rlBlueprint);

            // Spawn the monster
            Monster baseMonster = entityManager.SpawnMonster(poolIndex, position, rlBlueprint);

            // Convert to RL monster if needed
            RLMonster rlMonster = baseMonster as RLMonster;
            if (rlMonster == null)
            {
                // If the monster isn't an RL monster, try to initialize it with RL behavior
                rlMonster = SetupRLBehavior(baseMonster, rlBlueprint);
            }

            if (rlMonster != null)
            {
                // Register in coordination pool
                if (coordinationPool != null)
                {
                    coordinationPool.RegisterAgent(rlMonster);
                }

                activeRLMonsters.Add(rlMonster);
                currentRLAgentCount++;

                OnRLMonsterSpawned?.Invoke(rlMonster);
                OnRLAgentCountChanged?.Invoke(currentRLAgentCount);

                return rlMonster;
            }

            return null;
        }

        /// <summary>
        /// Spawn RL monster at random position
        /// Requirement: 1.1
        /// </summary>
        public RLMonster SpawnRLMonsterRandomPosition(RLMonsterBlueprint rlBlueprint)
        {
            if (rlLevelConfig == null || !rlLevelConfig.EnableRLForLevel)
                return null;

            // Use EntityManager's random spawning logic
            Vector2 spawnPosition = entityManager.GetRandomMonsterSpawnPosition();
            return SpawnRLMonster(rlBlueprint, spawnPosition);
        }

        /// <summary>
        /// Convert standard monster to RL monster
        /// </summary>
        private RLMonster SetupRLBehavior(Monster baseMonster, RLMonsterBlueprint rlBlueprint)
        {
            // This would require adding an RLMonster component to the spawned monster
            // For now, we assume the prefab already includes RLMonster component
            RLMonster rlMonster = baseMonster.GetComponent<RLMonster>();
            if (rlMonster != null)
            {
                // Configure RL monster from blueprint
                rlMonster.IsTraining = rlBlueprint.EnableTraining;

                // Load model if pre-trained
                if (rlBlueprint.UsePreTrainedModel && !string.IsNullOrEmpty(rlBlueprint.PreTrainedModelPath))
                {
                    rlMonster.LoadBehaviorProfile(rlBlueprint.PreTrainedModelPath);
                }
            }
            return rlMonster;
        }

        /// <summary>
        /// Despawn RL monster and update tracking
        /// </summary>
        public void DespawnRLMonster(RLMonster rlMonster, bool killedByPlayer = true)
        {
            if (rlMonster == null)
                return;

            // Deregister from coordination pool
            if (coordinationPool != null)
            {
                coordinationPool.UnregisterAgent(rlMonster);
            }

            activeRLMonsters.Remove(rlMonster);
            currentRLAgentCount--;

            OnRLMonsterDespawned?.Invoke(rlMonster);
            OnRLAgentCountChanged?.Invoke(currentRLAgentCount);
        }

        /// <summary>
        /// Get RL monster by instance
        /// </summary>
        public RLMonster GetRLMonster(Monster monster)
        {
            return activeRLMonsters.FirstOrDefault(rlm => rlm == monster as RLMonster);
        }

        /// <summary>
        /// Update RL monster coordination
        /// Requirement: 1.5 - Multi-agent coordination
        /// </summary>
        private void UpdateCoordination()
        {
            if (coordinationPool == null || !rlLevelConfig.EnableCoordinationLearning)
                return;

            // Update coordination at specified intervals
            if (Time.time - lastCoordinationUpdateTime >= coordinationUpdateInterval)
            {
                coordinationPool.UpdateCoordination(activeRLMonsters);
                lastCoordinationUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Apply dynamic difficulty scaling to all RL agents
        /// </summary>
        public void ApplyDifficultyScaling(DifficultyLevel difficulty)
        {
            if (rlLevelConfig == null)
                return;

            foreach (var rlMonster in activeRLMonsters)
            {
                rlMonster.SetDifficultyLevel(difficulty);
            }
        }

        /// <summary>
        /// Get or create pool for RL blueprint
        /// </summary>
        private int GetOrCreateRLMonsterPool(RLMonsterBlueprint rlBlueprint)
        {
            string blueprintKey = rlBlueprint.name;

            if (rlMonsterPoolIndices.TryGetValue(blueprintKey, out int poolIndex))
                return poolIndex;

            // This would require extending EntityManager to support adding new pools
            // For now, we use the base implementation
            poolIndex = baseMonsterPoolCount + rlMonsterPoolIndices.Count;
            rlMonsterPoolIndices[blueprintKey] = poolIndex;

            return poolIndex;
        }

        /// <summary>
        /// Get all active RL monsters
        /// </summary>
        public RLMonster[] GetActiveRLMonsters()
        {
            return activeRLMonsters.ToArray();
        }

        /// <summary>
        /// Clear all RL monsters
        /// </summary>
        public void ClearRLMonsters()
        {
            foreach (var monster in activeRLMonsters.ToList())
            {
                DespawnRLMonster(monster, false);
            }
            activeRLMonsters.Clear();
            currentRLAgentCount = 0;
        }

        private void Update()
        {
            if (rlLevelConfig == null)
                return;

            UpdateCoordination();
        }

        private void OnDestroy()
        {
            ClearRLMonsters();
            if (coordinationPool != null)
            {
                coordinationPool.Dispose();
            }
        }
    }

    /// <summary>
    /// Manages coordination between RL agents
    /// Requirement: 1.5 - Multi-agent coordination
    /// </summary>
    public class RLMonsterCoordinationPool
    {
        private int maxAgents;
        private CoordinationStrategy strategy;
        private Dictionary<RLMonster, AgentCoordinationData> agentData = new Dictionary<RLMonster, AgentCoordinationData>();

        public RLMonsterCoordinationPool(int maxAgents, CoordinationStrategy strategy)
        {
            this.maxAgents = maxAgents;
            this.strategy = strategy;
        }

        public void RegisterAgent(RLMonster agent)
        {
            if (!agentData.ContainsKey(agent))
            {
                agentData[agent] = new AgentCoordinationData();
            }
        }

        public void UnregisterAgent(RLMonster agent)
        {
            agentData.Remove(agent);
        }

        public void UpdateCoordination(List<RLMonster> activeMonsters)
        {
            switch (strategy)
            {
                case CoordinationStrategy.Basic:
                    UpdateBasicCoordination(activeMonsters);
                    break;
                case CoordinationStrategy.Flank:
                    UpdateFlankCoordination(activeMonsters);
                    break;
                case CoordinationStrategy.Surround:
                    UpdateSurroundCoordination(activeMonsters);
                    break;
                case CoordinationStrategy.CrossFire:
                    UpdateCrossfireCoordination(activeMonsters);
                    break;
                case CoordinationStrategy.SequentialAttack:
                    UpdateSequentialAttackCoordination(activeMonsters);
                    break;
                case CoordinationStrategy.ZoneControl:
                    UpdateZoneControlCoordination(activeMonsters);
                    break;
                case CoordinationStrategy.Overwhelm:
                    UpdateOverwhelmCoordination(activeMonsters);
                    break;
                case CoordinationStrategy.None:
                default:
                    // No coordination needed
                    break;
            }
        }

        private void UpdateBasicCoordination(List<RLMonster> agents)
        {
            // Agents follow a leader
            if (agents.Count > 0)
            {
                var leader = agents[0];
                foreach (var agent in agents)
                {
                    if (agent != leader && agentData.TryGetValue(agent, out var data))
                    {
                        data.nearbyAllies.Clear();
                        data.nearbyAllies.Add(leader);
                    }
                }
            }
        }

        private void UpdateFlankCoordination(List<RLMonster> agents)
        {
            // Agents position themselves to attack from sides
            for (int i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (!agentData.TryGetValue(agent, out var data))
                    continue;

                // Find nearby allies
                data.nearbyAllies.Clear();
                for (int j = 0; j < agents.Count; j++)
                {
                    if (i != j)
                    {
                        float distance = Vector2.Distance(agent.transform.position, agents[j].transform.position);
                        if (distance < 10f)
                        {
                            data.nearbyAllies.Add(agents[j]);
                        }
                    }
                }
            }
        }

        private void UpdateSurroundCoordination(List<RLMonster> agents)
        {
            // Agents encircle targets
        }

        private void UpdateCrossfireCoordination(List<RLMonster> agents)
        {
            // Ranged agents create crossfire patterns
        }

        private void UpdateSequentialAttackCoordination(List<RLMonster> agents)
        {
            // Agents attack in sequence
        }

        private void UpdateZoneControlCoordination(List<RLMonster> agents)
        {
            // Agents control specific areas
        }

        private void UpdateOverwhelmCoordination(List<RLMonster> agents)
        {
            // All agents attack together for overwhelming force
        }

        public void Dispose()
        {
            agentData.Clear();
        }
    }

    /// <summary>
    /// Coordination data for a single RL agent
    /// </summary>
    public class AgentCoordinationData
    {
        public List<RLMonster> nearbyAllies = new List<RLMonster>();
        public bool isLeader = false;
        public Vector2 targetCoordinationDirection = Vector2.zero;
        public float coordinationIntensity = 1.0f;
    }
}
