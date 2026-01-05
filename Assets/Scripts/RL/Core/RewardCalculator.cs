using UnityEngine;
using System.Collections.Generic;
using Vampire;
using Vampire.RL;

namespace Vampire.RL
{
    /// <summary>
    /// Concrete implementation of reward calculator for monster RL agents
    /// Provides configurable reward calculation based on damage, survival, cooperation, and positioning
    /// </summary>
    public class RewardCalculator : MonoBehaviour, IRewardCalculator
    {
        [Header("Reward Configuration")]
        [SerializeField] private object rewardComponents;
        [SerializeField] private bool enableCoopRewards = false;
        [SerializeField] private CoopRewardCalculator coopRewardCalculator;

        [Header("Reward Shaping Parameters")]
        [SerializeField] private float distanceRewardScale = 1.0f;
        [SerializeField] private float velocityRewardScale = 0.5f;
        [SerializeField] private float healthRewardScale = 2.0f;
        [SerializeField] private float coordinationRadius = 5.0f;

        [Header("Dependencies")]
        private RLEnvironment environment;
        private EntityManager entityManager;
        private Character playerCharacter;

        // Tracking for reward calculation
        private Dictionary<Monster, float> lastDamageDealt;
        private Dictionary<Monster, Vector2> lastPositions;
        private Dictionary<Monster, float> lastDistances;
        private void Awake()
        {
            // Initialize with default reward components if not set
            if (rewardComponents == null)
            {
                rewardComponents = new object(); // Initialize with default object
            }

            // Initialize tracking dictionaries
            lastDamageDealt = new Dictionary<Monster, float>();
            lastPositions = new Dictionary<Monster, Vector2>();
            lastDistances = new Dictionary<Monster, float>();
        }

        /// <summary>
        /// Initialize the reward calculator with dependencies
        /// </summary>
        public void Initialize(RLEnvironment environment, EntityManager entityManager, Character playerCharacter)
        {
            this.environment = environment;
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;

            // Initialize co-op reward calculator if enabled
            if (enableCoopRewards && coopRewardCalculator != null)
            {
                coopRewardCalculator.Initialize(environment, entityManager);
                Debug.Log("Co-op reward calculator initialized");
            }
        }

        /// <summary>
        /// Calculate reward for a monster action (simplified interface for RLEnvironment)
        /// </summary>
        public float CalculateReward(Monster monster, int action, float[] previousState)
        {
            if (monster == null || playerCharacter == null)
                return 0f;

            float totalReward = 0f;

            // Calculate individual reward components
            float damageReward = CalculateDamageReward(monster);
            float survivalReward = CalculateSurvivalReward(monster);
            float coordinationReward = CalculateCoordinationReward(monster);
            float positioningReward = CalculatePositioningReward(monster, action, previousState);

            // Combine rewards
            totalReward += damageReward;
            totalReward += survivalReward;
            totalReward += coordinationReward;
            totalReward += positioningReward;

            // Add co-op rewards if enabled
            if (enableCoopRewards && coopRewardCalculator != null)
            {
                float coopReward = coopRewardCalculator.CalculateReward(monster, action, previousState);
                totalReward += coopReward;
            }

            // Apply reward shaping
            totalReward = ApplyRewardShaping(totalReward, monster, previousState);

            // Update tracking data
            UpdateTrackingData(monster);

            return totalReward;
        }

        /// <summary>
        /// Calculate reward for a monster action with detailed context
        /// </summary>
        public float CalculateReward(RLGameState previousState, MonsterAction action, RLGameState currentState, ActionOutcome actionOutcome)
        {
            // This method provides compatibility with the original interface
            // Implementation would depend on the specific RLGameState and ActionOutcome structures

            float totalReward = 0f;

            // Use reward components to calculate based on action outcome
            if (actionOutcome.damageDealt >= 0)
            {
                // Reward calculation based on action outcome
                totalReward = actionOutcome.damageDealt * 10f; // damageDealtReward
            }

            // Add co-op rewards if enabled
            if (enableCoopRewards && coopRewardCalculator != null)
            {
                float coopReward = coopRewardCalculator.CalculateReward(previousState, action, currentState, actionOutcome);
                totalReward += coopReward;
            }

            return totalReward;
        }

        /// <summary>
        /// Calculate terminal reward when monster dies or episode ends
        /// </summary>
        public float CalculateTerminalReward(RLGameState finalState, float episodeLength, bool killedByPlayer)
        {
            float terminalReward = 0f;

            if (killedByPlayer)
            {
                terminalReward += -10f; // deathPenalty
            }

            // Bonus for longer survival
            float survivalBonus = episodeLength * 0.1f * 10f; // Scale up for terminal reward
            terminalReward += survivalBonus;

            return terminalReward;
        }

        /// <summary>
        /// Apply reward shaping for better learning convergence
        /// </summary>
        public float ShapeReward(float baseReward, RLGameState state)
        {
            // Apply potential-based reward shaping
            // This helps guide the learning process without changing the optimal policy

            float shapedReward = baseReward;

            // Add potential-based shaping based on distance to player
            if (state.playerHealth > 0f)
            {
                // Implement potential function based on game state
                // For now, return the base reward
            }

            return shapedReward;
        }

        /// <summary>
        /// Update reward configuration at runtime
        /// </summary>
        public void UpdateRewardConfiguration(object newRewardComponents)
        {
            if (newRewardComponents != null)
            {
                rewardComponents = newRewardComponents;
                Debug.Log("Reward configuration updated successfully");
            }
            else
            {
                Debug.LogWarning("Invalid reward components provided. Configuration not updated.");
            }
        }

        /// <summary>
        /// Calculate damage-based reward
        /// </summary>
        private float CalculateDamageReward(Monster monster)
        {
            // For now, we'll use a simplified approach
            // In a full implementation, this would track actual damage dealt to the player

            float damageReward = 0f;

            // Check if monster is in attack range and facing player
            float distanceToPlayer = Vector2.Distance(monster.transform.position, playerCharacter.transform.position);
            if (distanceToPlayer < 2.0f) // Attack range
            {
                // Potential damage reward (would be actual damage in full implementation)
                damageReward = 10f * 0.1f; // damageDealtReward
            }

            return damageReward;
        }

        /// <summary>
        /// Calculate survival-based reward
        /// </summary>
        private float CalculateSurvivalReward(Monster monster)
        {
            // Simple survival reward for being alive
            return 0.1f; // survivalReward
        }

        /// <summary>
        /// Calculate coordination-based reward
        /// </summary>
        private float CalculateCoordinationReward(Monster monster)
        {
            float coordinationReward = 0f;

            if (entityManager?.LivingMonsters == null) return coordinationReward;

            // Count nearby monsters for coordination
            int nearbyMonsters = 0;
            Vector2 monsterPos = monster.transform.position;

            foreach (var otherMonster in entityManager.LivingMonsters)
            {
                if (otherMonster != monster && otherMonster != null)
                {
                    float distance = Vector2.Distance(monsterPos, otherMonster.transform.position);
                    if (distance < coordinationRadius)
                    {
                        nearbyMonsters++;
                    }
                }
            }

            // Reward coordination behavior
            if (nearbyMonsters > 0)
            {
                coordinationReward = 5f * (nearbyMonsters / 5.0f); // Normalize by max expected monsters
            }

            return coordinationReward;
        }

        /// <summary>
        /// Calculate positioning-based reward
        /// </summary>
        private float CalculatePositioningReward(Monster monster, int action, float[] previousState)
        {
            float positioningReward = 0f;

            if (previousState == null || previousState.Length < 12) return positioningReward;

            Vector2 currentPos = monster.transform.position;
            Vector2 playerPos = playerCharacter.transform.position;
            Vector2 previousMonsterPos = new Vector2(previousState[5], previousState[6]);
            Vector2 previousPlayerPos = new Vector2(previousState[0], previousState[1]);

            float currentDistance = Vector2.Distance(currentPos, playerPos);
            float previousDistance = Vector2.Distance(previousMonsterPos, previousPlayerPos);

            // Reward getting closer to player
            if (currentDistance < previousDistance)
            {
                float improvement = (previousDistance - currentDistance) / previousDistance;
                positioningReward += 3f * improvement; // positioningReward
            }

            // Reward flanking behavior (being at an angle to player's movement)
            Vector2 playerVelocity = new Vector2(previousState[2], previousState[3]);
            if (playerVelocity.magnitude > 0.1f)
            {
                Vector2 toMonster = (currentPos - playerPos).normalized;
                Vector2 playerDirection = playerVelocity.normalized;

                // Reward being perpendicular to player movement (flanking)
                float angle = Vector2.Angle(toMonster, playerDirection);
                if (angle > 60f && angle < 120f) // Flanking angle
                {
                    positioningReward += 3f * 0.5f; // positioningReward
                }
            }

            return positioningReward;
        }

        /// <summary>
        /// Apply reward shaping based on monster state
        /// </summary>
        private float ApplyRewardShaping(float baseReward, Monster monster, float[] previousState)
        {
            float shapedReward = baseReward;

            // Distance-based shaping
            float distanceToPlayer = Vector2.Distance(monster.transform.position, playerCharacter.transform.position);
            float normalizedDistance = Mathf.Clamp01(distanceToPlayer / 20f); // Normalize to 20 units max
            float distanceShaping = (1f - normalizedDistance) * distanceRewardScale * 0.1f;
            shapedReward += distanceShaping;

            // Health-based shaping (encourage staying alive)
            float healthRatio = monster.HP / 100f; // Assuming max health of 100
            float healthShaping = healthRatio * healthRewardScale * 0.05f;
            shapedReward += healthShaping;

            return shapedReward;
        }

        /// <summary>
        /// Update tracking data for reward calculation
        /// </summary>
        private void UpdateTrackingData(Monster monster)
        {
            if (monster == null) return;

            lastPositions[monster] = monster.transform.position;
            lastDistances[monster] = Vector2.Distance(monster.transform.position, playerCharacter.transform.position);

            // Reset damage tracking (would be updated by actual damage events in full implementation)
            lastDamageDealt[monster] = 0f;
        }

        /// <summary>
        /// Clean up tracking data for a monster
        /// </summary>
        public void CleanupMonster(Monster monster)
        {
            if (monster == null) return;

            lastDamageDealt.Remove(monster);
            lastPositions.Remove(monster);
            lastDistances.Remove(monster);

            // Cleanup co-op tracking if enabled
            if (enableCoopRewards && coopRewardCalculator != null)
            {
                coopRewardCalculator.CleanupMonster(monster);
            }
        }

        /// <summary>
        /// Get current reward configuration
        /// </summary>
        public object GetRewardConfiguration()
        {
            return rewardComponents;
        }

        /// <summary>
        /// Set reward configuration for specific behavior type
        /// </summary>
        public void SetBehaviorType(BehaviorType behaviorType)
        {
            // Behavior type configuration would be applied to rewardComponents
            // This is typically set up through the MonsterRLConfig or during initialization
            switch (behaviorType)
            {
                case BehaviorType.Aggressive:
                    // Apply aggressive reward configuration
                    break;
                case BehaviorType.Defensive:
                    // Apply defensive reward configuration
                    break;
                case BehaviorType.Coordinated:
                    // Apply coordinated reward configuration
                    break;
                default:
                    // Apply default reward configuration
                    break;
            }
        }

        private void OnDestroy()
        {
            // Clean up tracking data
            lastDamageDealt?.Clear();
            lastPositions?.Clear();
            lastDistances?.Clear();
        }
    }

    /// <summary>
    /// Behavior types for reward configuration
    /// </summary>
    public enum BehaviorType
    {
        Default,
        Aggressive,
        Defensive,
        Coordinated
    }
}