using UnityEngine;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Manages RL monster pools for efficient memory usage and performance
    /// Requirement: 1.1 - Consistent RL agent initialization, 1.5 - Multi-agent coordination
    /// </summary>
    public class RLMonsterPool : MonoBehaviour
    {
        [SerializeField] private GameObject monsterPoolParent;

        private EntityManager entityManager;
        private Dictionary<string, Stack<RLMonsterAgent>> poolsByBlueprint = new Dictionary<string, Stack<RLMonsterAgent>>();
        private Dictionary<RLMonsterAgent, string> blueprintByInstance = new Dictionary<RLMonsterAgent, string>();
        private Dictionary<string, RLMonsterBlueprint> blueprintCache = new Dictionary<string, RLMonsterBlueprint>();

        /// <summary>
        /// Initialize the RL monster pool
        /// </summary>
        public void Initialize(EntityManager entityManager)
        {
            this.entityManager = entityManager;
        }

        /// <summary>
        /// Get or create pool for RL blueprint
        /// </summary>
        public Stack<RLMonsterAgent> GetOrCreatePool(RLMonsterBlueprint blueprint)
        {
            if (blueprint == null)
            {
                Debug.LogError("Cannot create pool for null blueprint");
                return null;
            }

            string key = blueprint.name;

            if (!poolsByBlueprint.TryGetValue(key, out var pool))
            {
                pool = new Stack<RLMonsterAgent>();
                poolsByBlueprint[key] = pool;
                blueprintCache[key] = blueprint;

                // Pre-allocate some instances based on expected concurrent count
                PreAllocateMonsters(blueprint, 5);
            }

            return pool;
        }

        /// <summary>
        /// Get an RL monster instance from pool
        /// Requirement: 1.1
        /// </summary>
        public RLMonsterAgent GetMonster(RLMonsterBlueprint blueprint)
        {
            var pool = GetOrCreatePool(blueprint);
            if (pool == null)
                return null;

            RLMonsterAgent monster;

            if (pool.Count > 0)
            {
                monster = pool.Pop();
                monster.gameObject.SetActive(true);
            }
            else
            {
                // Create new instance if pool exhausted
                monster = CreateMonsterInstance(blueprint);
            }

            if (monster != null)
            {
                blueprintByInstance[monster] = blueprint.name;
            }

            return monster;
        }

        /// <summary>
        /// Return RL monster to pool
        /// </summary>
        public void ReleaseMonster(RLMonsterAgent monster)
        {
            if (monster == null)
                return;

            if (blueprintByInstance.TryGetValue(monster, out string blueprintKey))
            {
                if (poolsByBlueprint.TryGetValue(blueprintKey, out var pool))
                {
                    monster.gameObject.SetActive(false);
                    pool.Push(monster);
                }
            }
        }

        /// <summary>
        /// Create a new RL monster instance
        /// </summary>
        private RLMonsterAgent CreateMonsterInstance(RLMonsterBlueprint blueprint)
        {
            // This would require having a monster prefab that includes RLMonster component
            // For now, return null - the actual implementation depends on prefab setup
            Debug.LogWarning($"RLMonsterPool: CreateMonsterInstance not fully implemented for {blueprint.name}");
            return null;
        }

        /// <summary>
        /// Pre-allocate monster instances
        /// Reduces runtime allocation
        /// </summary>
        private void PreAllocateMonsters(RLMonsterBlueprint blueprint, int count)
        {
            var pool = poolsByBlueprint[blueprint.name];

            for (int i = 0; i < count; i++)
            {
                var monster = CreateMonsterInstance(blueprint);
                if (monster != null)
                {
                    monster.gameObject.SetActive(false);
                    pool.Push(monster);
                }
            }
        }

        /// <summary>
        /// Clear all pools
        /// </summary>
        public void ClearAll()
        {
            foreach (var pool in poolsByBlueprint.Values)
            {
                foreach (var monster in pool)
                {
                    if (monster != null)
                    {
                        Destroy(monster.gameObject);
                    }
                }
                pool.Clear();
            }

            poolsByBlueprint.Clear();
            blueprintByInstance.Clear();
            blueprintCache.Clear();
        }

        /// <summary>
        /// Get pool statistics
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            var stats = new PoolStatistics();

            foreach (var kvp in poolsByBlueprint)
            {
                var blueprintKey = kvp.Key;
                var pool = kvp.Value;

                stats.poolCount++;
                stats.totalPooled += pool.Count;

                if (blueprintCache.TryGetValue(blueprintKey, out var blueprint))
                {
                    stats.poolSizeByBlueprint[blueprint.name] = pool.Count;
                }
            }

            stats.totalInstances = blueprintByInstance.Count;

            return stats;
        }
    }

    /// <summary>
    /// Statistics about RL monster pools
    /// </summary>
    public class PoolStatistics
    {
        public int poolCount = 0;
        public int totalPooled = 0;
        public int totalInstances = 0;
        public Dictionary<string, int> poolSizeByBlueprint = new Dictionary<string, int>();

        public override string ToString()
        {
            return $"PoolCount: {poolCount}, Pooled: {totalPooled}, Active: {totalInstances - totalPooled}";
        }
    }
}
