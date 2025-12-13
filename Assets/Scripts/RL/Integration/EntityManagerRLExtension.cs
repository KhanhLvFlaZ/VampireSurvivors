using UnityEngine;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Partial extension to EntityManager for RL support
    /// Allows EntityManager to work with RL-specific spawning requirements
    /// This is designed to be integrated with the existing EntityManager
    /// </summary>
    public static class EntityManagerRLExtension
    {
        // Dictionary to store RL pool indices per EntityManager instance
        private static Dictionary<EntityManager, RLPoolRegistry> rlPoolRegistries = new Dictionary<EntityManager, RLPoolRegistry>();

        /// <summary>
        /// Get the number of monster pools currently initialized
        /// This is needed by RLEntityIntegration to track pool indices
        /// </summary>
        public static int GetMonsterPoolCount(this EntityManager entityManager)
        {
            // This would need to be exposed by EntityManager
            // For now, return a placeholder value that RLEntityIntegration will work around
            return 1;
        }

        /// <summary>
        /// Get random monster spawn position
        /// Exposes the existing private method for RL spawning
        /// </summary>
        public static Vector2 GetRandomMonsterSpawnPosition(this EntityManager entityManager)
        {
            // This exposes the existing GetRandomMonsterSpawnPosition method
            // The actual implementation is already in EntityManager
            // This extension provides a public interface for it
            
            // Call via reflection since the method is private
            var method = typeof(EntityManager).GetMethod(
                "GetRandomMonsterSpawnPosition",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );

            if (method != null)
            {
                return (Vector2)method.Invoke(entityManager, null);
            }

            // Fallback: spawn at player position plus offset
            Debug.LogWarning("Could not access GetRandomMonsterSpawnPosition via reflection");
            return Vector2.zero;
        }

        /// <summary>
        /// Register RL pool for blueprint
        /// </summary>
        public static void RegisterRLPool(this EntityManager entityManager, RLMonsterBlueprint blueprint, int poolIndex)
        {
            if (!rlPoolRegistries.ContainsKey(entityManager))
            {
                rlPoolRegistries[entityManager] = new RLPoolRegistry();
            }

            rlPoolRegistries[entityManager].RegisterPool(blueprint, poolIndex);
        }

        /// <summary>
        /// Get RL pool index for blueprint
        /// </summary>
        public static int GetRLPoolIndex(this EntityManager entityManager, RLMonsterBlueprint blueprint)
        {
            if (rlPoolRegistries.TryGetValue(entityManager, out var registry))
            {
                return registry.GetPoolIndex(blueprint);
            }

            return -1;
        }

        /// <summary>
        /// Clean up RL pools for entity manager
        /// </summary>
        public static void CleanupRLPools(this EntityManager entityManager)
        {
            if (rlPoolRegistries.TryGetValue(entityManager, out var registry))
            {
                registry.Clear();
                rlPoolRegistries.Remove(entityManager);
            }
        }
    }

    /// <summary>
    /// Registry for RL monster pools
    /// </summary>
    public class RLPoolRegistry
    {
        private Dictionary<string, int> poolIndices = new Dictionary<string, int>();

        public void RegisterPool(RLMonsterBlueprint blueprint, int poolIndex)
        {
            if (blueprint != null)
            {
                poolIndices[blueprint.name] = poolIndex;
            }
        }

        public int GetPoolIndex(RLMonsterBlueprint blueprint)
        {
            if (blueprint != null && poolIndices.TryGetValue(blueprint.name, out int index))
            {
                return index;
            }

            return -1;
        }

        public void Clear()
        {
            poolIndices.Clear();
        }
    }
}
