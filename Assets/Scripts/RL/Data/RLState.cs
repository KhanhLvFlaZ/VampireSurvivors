using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// RL state representation as specified in the design document
    /// </summary>
    [System.Serializable]
    public class RLState
    {
        [Header("Player Information")]
        public Vector2 playerPosition;
        public Vector2 playerVelocity;
        public float playerHealth;

        [Header("Monster Information")]
        public Vector2 monsterPosition;
        public Vector2 monsterVelocity;
        public float monsterHealth;

        [Header("Environment Information")]
        public Vector2[] nearbyMonsterPositions;
        public Vector2[] nearbyObstacles;
        public float timeSinceLastAttack;
        public float distanceToPlayer;
        public int monstersInRange;

        /// <summary>
        /// Convert state to float array for neural network input
        /// </summary>
        /// <returns>State as float array</returns>
        public float[] ToArray()
        {
            var stateArray = new float[20]; // Legacy encoder path (single-player); co-op uses StateEncoder/RLGameState
            int index = 0;

            // Player information (5 values)
            stateArray[index++] = playerPosition.x;
            stateArray[index++] = playerPosition.y;
            stateArray[index++] = playerVelocity.x;
            stateArray[index++] = playerVelocity.y;
            stateArray[index++] = playerHealth;

            // Monster information (5 values)
            stateArray[index++] = monsterPosition.x;
            stateArray[index++] = monsterPosition.y;
            stateArray[index++] = monsterVelocity.x;
            stateArray[index++] = monsterVelocity.y;
            stateArray[index++] = monsterHealth;

            // Environment information (10 values)
            stateArray[index++] = timeSinceLastAttack;
            stateArray[index++] = distanceToPlayer;
            stateArray[index++] = monstersInRange;

            // Nearby monsters (4 values - 2 positions max)
            for (int i = 0; i < 2; i++)
            {
                if (nearbyMonsterPositions != null && i < nearbyMonsterPositions.Length)
                {
                    stateArray[index++] = nearbyMonsterPositions[i].x;
                    stateArray[index++] = nearbyMonsterPositions[i].y;
                }
                else
                {
                    stateArray[index++] = 0f;
                    stateArray[index++] = 0f;
                }
            }

            // Nearby obstacles (3 values - 1 obstacle position + distance)
            if (nearbyObstacles != null && nearbyObstacles.Length > 0)
            {
                stateArray[index++] = nearbyObstacles[0].x;
                stateArray[index++] = nearbyObstacles[0].y;
                stateArray[index++] = Vector2.Distance(monsterPosition, nearbyObstacles[0]);
            }
            else
            {
                stateArray[index++] = 0f;
                stateArray[index++] = 0f;
                stateArray[index++] = 0f;
            }

            return stateArray;
        }

        /// <summary>
        /// Create state from float array
        /// </summary>
        /// <param name="stateArray">Float array representation</param>
        /// <returns>RLState object</returns>
        public static RLState FromArray(float[] stateArray)
        {
            if (stateArray.Length < 20)
                return new RLState();

            var state = new RLState();
            int index = 0;

            // Player information
            state.playerPosition = new Vector2(stateArray[index++], stateArray[index++]);
            state.playerVelocity = new Vector2(stateArray[index++], stateArray[index++]);
            state.playerHealth = stateArray[index++];

            // Monster information
            state.monsterPosition = new Vector2(stateArray[index++], stateArray[index++]);
            state.monsterVelocity = new Vector2(stateArray[index++], stateArray[index++]);
            state.monsterHealth = stateArray[index++];

            // Environment information
            state.timeSinceLastAttack = stateArray[index++];
            state.distanceToPlayer = stateArray[index++];
            state.monstersInRange = (int)stateArray[index++];

            // Nearby monsters
            state.nearbyMonsterPositions = new Vector2[2];
            for (int i = 0; i < 2; i++)
            {
                state.nearbyMonsterPositions[i] = new Vector2(stateArray[index++], stateArray[index++]);
            }

            // Nearby obstacles
            state.nearbyObstacles = new Vector2[1];
            state.nearbyObstacles[0] = new Vector2(stateArray[index++], stateArray[index++]);
            // Skip the distance value as it's calculated
            index++;

            return state;
        }



        /// <summary>
        /// Validate the state data for consistency
        /// </summary>
        /// <returns>True if state is valid</returns>
        public bool IsValid()
        {
            // Check for NaN or infinite values
            if (!IsFiniteVector2(playerPosition) || !IsFiniteVector2(playerVelocity) ||
                !IsFiniteVector2(monsterPosition) || !IsFiniteVector2(monsterVelocity))
                return false;

            if (!float.IsFinite(playerHealth) || !float.IsFinite(monsterHealth) ||
                !float.IsFinite(timeSinceLastAttack) || !float.IsFinite(distanceToPlayer))
                return false;

            // Check reasonable value ranges
            if (playerHealth < 0 || monsterHealth < 0 || timeSinceLastAttack < 0 ||
                distanceToPlayer < 0 || monstersInRange < 0)
                return false;

            return true;
        }

        /// <summary>
        /// Helper method to check if Vector2 has finite values
        /// </summary>
        private bool IsFiniteVector2(Vector2 vector)
        {
            return float.IsFinite(vector.x) && float.IsFinite(vector.y);
        }

        /// <summary>
        /// Create a copy of this state
        /// </summary>
        /// <returns>Deep copy of the state</returns>
        public RLState Clone()
        {
            var clone = new RLState
            {
                playerPosition = playerPosition,
                playerVelocity = playerVelocity,
                playerHealth = playerHealth,
                monsterPosition = monsterPosition,
                monsterVelocity = monsterVelocity,
                monsterHealth = monsterHealth,
                timeSinceLastAttack = timeSinceLastAttack,
                distanceToPlayer = distanceToPlayer,
                monstersInRange = monstersInRange
            };

            // Deep copy arrays
            if (nearbyMonsterPositions != null)
            {
                clone.nearbyMonsterPositions = new Vector2[nearbyMonsterPositions.Length];
                System.Array.Copy(nearbyMonsterPositions, clone.nearbyMonsterPositions, nearbyMonsterPositions.Length);
            }

            if (nearbyObstacles != null)
            {
                clone.nearbyObstacles = new Vector2[nearbyObstacles.Length];
                System.Array.Copy(nearbyObstacles, clone.nearbyObstacles, nearbyObstacles.Length);
            }

            return clone;
        }
    }
}