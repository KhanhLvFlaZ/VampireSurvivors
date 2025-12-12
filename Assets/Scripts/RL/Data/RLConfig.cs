using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// RL configuration as specified in the design document
    /// </summary>
    [System.Serializable]
    public class RLConfig
    {
        [Header("State and Action Configuration")]
        public int stateSize = 20;
        public int actionSize = 8;
        
        [Header("Learning Parameters")]
        [Range(0.0001f, 0.1f)]
        public float learningRate = 0.001f;
        
        [Range(0.9f, 0.999f)]
        public float discountFactor = 0.99f;
        
        [Range(0.01f, 1.0f)]
        public float explorationRate = 0.1f;
        
        [Header("Experience Replay")]
        [Range(1000, 100000)]
        public int memorySize = 10000;
        
        [Range(16, 256)]
        public int batchSize = 32;
        
        [Header("Coordination Settings")]
        public bool useCoordination = true;
        
        [Range(0.0f, 1.0f)]
        public float coordinationWeight = 0.2f;

        /// <summary>
        /// Validate the configuration
        /// </summary>
        public bool IsValid()
        {
            return stateSize > 0 && 
                   actionSize > 0 && 
                   learningRate > 0 && 
                   discountFactor > 0 && 
                   memorySize > 0 && 
                   batchSize > 0;
        }

        /// <summary>
        /// Create default configuration
        /// </summary>
        public static RLConfig CreateDefault()
        {
            return new RLConfig
            {
                stateSize = 20,
                actionSize = 8,
                learningRate = 0.001f,
                discountFactor = 0.99f,
                explorationRate = 0.1f,
                memorySize = 10000,
                batchSize = 32,
                useCoordination = true,
                coordinationWeight = 0.2f
            };
        }
    }
}