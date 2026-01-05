using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Vampire.RL
{
    /// <summary>
    /// Co-op reward calculator that provides rewards for:
    /// - Assist (helping teammates kill enemies)
    /// - Aggro share (drawing enemy attention away from teammates)
    /// - Formation (maintaining good positioning relative to team)
    /// 
    /// Requires extended observation data from StateEncoder with co-op fields
    /// </summary>
    public class CoopRewardCalculator : MonoBehaviour, IRewardCalculator
    {
        [Header("Assist Rewards")]
        [SerializeField] private float assistReward = 15f;
        [SerializeField] private float assistDistanceThreshold = 10f; // Max distance for assist credit
        [SerializeField] private float assistTimeWindow = 5f; // Seconds to give assist credit

        [Header("Aggro Share Rewards")]
        [SerializeField] private float aggroShareReward = 10f;
        [SerializeField] private float aggroDistanceThreshold = 8f; // Distance to consider "drawing aggro"
        [SerializeField] private float aggroHealthThreshold = 0.5f; // Teammate health below this gets aggro bonus

        [Header("Formation Rewards")]
        [SerializeField] private float formationReward = 8f;
        [SerializeField] private float optimalSpreadDistance = 5f; // Optimal distance between teammates
        [SerializeField] private float formationCheckRadius = 15f; // Max radius for formation consideration
        [SerializeField] private float flankingBonusMultiplier = 1.5f;
        [SerializeField] private float surroundBonusMultiplier = 2.0f;

        [Header("Team Coordination")]
        [SerializeField] private float focusFireReward = 12f; // Reward for attacking same target as team
        [SerializeField] private float protectWeakReward = 10f; // Reward for protecting low-health teammates

        [Header("Dependencies")]
        [SerializeField] private RLEnvironment environment;
        [SerializeField] private EntityManager entityManager;

        // Tracking structures
        private Dictionary<Monster, DamageTracker> monsterDamageTracking;
        private Dictionary<Monster, AggroTracker> monsterAggroTracking;
        private Dictionary<Monster, FormationTracker> monsterFormationTracking;

        // Statistics
        private int totalAssists;
        private int totalAggroShares;
        private int totalFormationBonuses;

        public int TotalAssists => totalAssists;
        public int TotalAggroShares => totalAggroShares;
        public int TotalFormationBonuses => totalFormationBonuses;

        private void Awake()
        {
            monsterDamageTracking = new Dictionary<Monster, DamageTracker>();
            monsterAggroTracking = new Dictionary<Monster, AggroTracker>();
            monsterFormationTracking = new Dictionary<Monster, FormationTracker>();
        }

        public void Initialize(RLEnvironment environment, EntityManager entityManager)
        {
            this.environment = environment;
            this.entityManager = entityManager;
        }

        #region IRewardCalculator Implementation

        public float CalculateReward(Monster monster, int action, float[] previousState)
        {
            // Legacy interface - convert to RLGameState-based calculation
            if (environment == null) return 0f;

            var gameState = environment.BuildGameState(monster);
            return CalculateCoopReward(gameState, monster);
        }

        public float CalculateReward(RLGameState previousState, MonsterAction action, RLGameState currentState, ActionOutcome actionOutcome)
        {
            // Full co-op reward calculation with state context
            float totalReward = 0f;

            // Base rewards (damage, survival, etc.)
            totalReward += CalculateBaseReward(previousState, currentState, actionOutcome);

            // Co-op specific rewards
            totalReward += CalculateAssistReward(currentState, actionOutcome);
            totalReward += CalculateAggroShareReward(currentState);
            totalReward += CalculateFormationReward(currentState);
            totalReward += CalculateFocusFireReward(currentState);

            return totalReward;
        }

        public float CalculateTerminalReward(RLGameState finalState, float episodeLength, bool killedByPlayer)
        {
            float terminalReward = 0f;

            // Bonus for team coordination throughout episode
            if (finalState.totalTeammateCount > 0)
            {
                // Reward based on team damage dealt vs taken ratio
                float damageRatio = finalState.teamDamageDealt / Mathf.Max(1f, finalState.teamDamageTaken);
                terminalReward += Mathf.Clamp(damageRatio * 10f, -10f, 50f);

                // Reward for maintaining formation (based on avgTeammateDistance)
                float formationScore = Mathf.Clamp01(1f - Mathf.Abs(finalState.avgTeammateDistance - optimalSpreadDistance) / optimalSpreadDistance);
                terminalReward += formationScore * 20f;
            }

            // Penalty for death (standard)
            if (killedByPlayer)
            {
                terminalReward -= 10f;
            }

            return terminalReward;
        }

        public float ShapeReward(float baseReward, RLGameState state)
        {
            // Apply potential-based shaping for co-op scenarios
            float shapedReward = baseReward;

            // Potential based on team cohesion
            if (state.totalTeammateCount > 0)
            {
                float cohesionPotential = CalculateCohesionPotential(state);
                shapedReward += cohesionPotential * 0.1f;
            }

            return shapedReward;
        }

        public void UpdateRewardConfiguration(object newRewardComponents)
        {
            // Future: Support dynamic reward configuration
            Debug.Log("CoopRewardCalculator: Configuration update not yet implemented");
        }

        #endregion

        #region Assist Rewards

        /// <summary>
        /// Calculate assist reward when teammates help kill a monster
        /// Reward distributed among all teammates who dealt damage within time window
        /// </summary>
        private float CalculateAssistReward(RLGameState currentState, ActionOutcome outcome)
        {
            if (currentState.totalTeammateCount == 0) return 0f;
            if (!outcome.targetKilled) return 0f;

            float assistRewardTotal = 0f;

            // Check if monster was damaged by multiple teammates (assist)
            // In full implementation, would check DamageTracker for multi-source damage
            // For now, use proximity as proxy

            int nearbyTeammates = 0;
            for (int i = 0; i < currentState.totalTeammateCount && i < currentState.teammates.Length; i++)
            {
                var teammate = currentState.teammates[i];
                if (teammate.health <= 0) continue;

                float distanceToMonster = Vector2.Distance(teammate.position, currentState.monsterPosition);
                if (distanceToMonster <= assistDistanceThreshold)
                {
                    nearbyTeammates++;
                }
            }

            // Reward for team kill (multiple nearby teammates)
            if (nearbyTeammates >= 1)
            {
                assistRewardTotal += assistReward * (nearbyTeammates / 3f); // Normalize by max teammates
                totalAssists++;
            }

            return assistRewardTotal;
        }

        /// <summary>
        /// Track damage contributors for assist calculation
        /// </summary>
        private void TrackDamage(Monster monster, ulong contributorId, float damage)
        {
            if (!monsterDamageTracking.ContainsKey(monster))
            {
                monsterDamageTracking[monster] = new DamageTracker();
            }

            var tracker = monsterDamageTracking[monster];
            if (!tracker.contributors.ContainsKey(contributorId))
            {
                tracker.contributors[contributorId] = 0f;
            }

            tracker.contributors[contributorId] += damage;
            tracker.lastDamageTime = Time.time;
        }

        #endregion

        #region Aggro Share Rewards

        /// <summary>
        /// Calculate aggro share reward when drawing enemy attention away from weak teammates
        /// Higher reward when protecting low-health teammates
        /// </summary>
        private float CalculateAggroShareReward(RLGameState currentState)
        {
            if (currentState.totalTeammateCount == 0) return 0f;

            float aggroRewardTotal = 0f;
            Vector2 monsterPos = currentState.monsterPosition;

            // Find closest weak teammate
            float closestWeakDistance = float.MaxValue;
            bool hasWeakTeammate = false;

            for (int i = 0; i < currentState.totalTeammateCount && i < currentState.teammates.Length; i++)
            {
                var teammate = currentState.teammates[i];
                if (teammate.health <= 0) continue;

                float healthRatio = teammate.health / 100f; // Assume max 100
                if (healthRatio < aggroHealthThreshold)
                {
                    hasWeakTeammate = true;
                    float distance = Vector2.Distance(teammate.position, monsterPos);
                    closestWeakDistance = Mathf.Min(closestWeakDistance, distance);
                }
            }

            // Reward for being closer to monster than weak teammate (drawing aggro)
            if (hasWeakTeammate)
            {
                float distanceToMonster = Vector2.Distance(currentState.playerPosition, monsterPos);

                if (distanceToMonster < closestWeakDistance && distanceToMonster < aggroDistanceThreshold)
                {
                    // Stronger reward the closer we are (proportional aggro draw)
                    float aggroStrength = 1f - (distanceToMonster / aggroDistanceThreshold);
                    aggroRewardTotal += aggroShareReward * aggroStrength;
                    totalAggroShares++;
                }
            }

            return aggroRewardTotal;
        }

        /// <summary>
        /// Track which teammate monster is currently aggro'd on
        /// </summary>
        private void TrackAggro(Monster monster, int agentId)
        {
            if (!monsterAggroTracking.ContainsKey(monster))
            {
                monsterAggroTracking[monster] = new AggroTracker();
            }

            var tracker = monsterAggroTracking[monster];
            tracker.currentTarget = agentId;
            tracker.lastAggroChangeTime = Time.time;
        }

        #endregion

        #region Formation Rewards

        /// <summary>
        /// Calculate formation reward for maintaining good team positioning
        /// Rewards:
        /// - Optimal spread (not too clumped, not too far)
        /// - Flanking positions (surrounding monster)
        /// - Protecting weak teammates from behind
        /// </summary>
        private float CalculateFormationReward(RLGameState currentState)
        {
            if (currentState.totalTeammateCount == 0) return 0f;

            float formationRewardTotal = 0f;

            // 1. Spread reward: maintain optimal distance from teammates
            float spreadScore = CalculateSpreadScore(currentState);
            formationRewardTotal += spreadScore * formationReward * 0.4f;

            // 2. Flanking reward: surround the monster from multiple angles
            float flankingScore = CalculateFlankingScore(currentState);
            formationRewardTotal += flankingScore * formationReward * 0.4f;

            // 3. Protection reward: position between monster and weak teammates
            float protectionScore = CalculateProtectionScore(currentState);
            formationRewardTotal += protectionScore * formationReward * 0.2f;

            if (spreadScore > 0.7f || flankingScore > 0.7f)
            {
                totalFormationBonuses++;
            }

            return formationRewardTotal;
        }

        /// <summary>
        /// Calculate how well team maintains optimal spread
        /// </summary>
        private float CalculateSpreadScore(RLGameState state)
        {
            float avgDistance = 0f;
            int count = 0;

            // Distance from player to each teammate
            for (int i = 0; i < state.totalTeammateCount && i < state.teammates.Length; i++)
            {
                var teammate = state.teammates[i];
                if (teammate.health <= 0) continue;

                float distance = Vector2.Distance(state.playerPosition, teammate.position);
                avgDistance += distance;
                count++;
            }

            if (count == 0) return 0f;
            avgDistance /= count;

            // Score based on how close to optimal spread
            float deviation = Mathf.Abs(avgDistance - optimalSpreadDistance);
            float score = Mathf.Clamp01(1f - deviation / optimalSpreadDistance);

            return score;
        }

        /// <summary>
        /// Calculate flanking score (surrounding monster from multiple angles)
        /// </summary>
        private float CalculateFlankingScore(RLGameState state)
        {
            Vector2 monsterPos = state.monsterPosition;

            // Calculate angles of each teammate relative to monster
            List<float> angles = new List<float>();

            // Add player angle
            Vector2 playerDir = (state.playerPosition - monsterPos).normalized;
            float playerAngle = Mathf.Atan2(playerDir.y, playerDir.x) * Mathf.Rad2Deg;
            angles.Add(playerAngle);

            // Add teammate angles
            for (int i = 0; i < state.totalTeammateCount && i < state.teammates.Length; i++)
            {
                var teammate = state.teammates[i];
                if (teammate.health <= 0) continue;

                float distance = Vector2.Distance(teammate.position, monsterPos);
                if (distance > formationCheckRadius) continue;

                Vector2 teammateDir = (teammate.position - monsterPos).normalized;
                float teammateAngle = Mathf.Atan2(teammateDir.y, teammateDir.x) * Mathf.Rad2Deg;
                angles.Add(teammateAngle);
            }

            if (angles.Count <= 1) return 0f;

            // Sort angles
            angles.Sort();

            // Calculate angular spread (ideal is evenly distributed 360 degrees)
            float idealAngleSpacing = 360f / angles.Count;
            float totalDeviation = 0f;

            for (int i = 0; i < angles.Count; i++)
            {
                int nextIdx = (i + 1) % angles.Count;
                float actualSpacing = angles[nextIdx] - angles[i];
                if (actualSpacing < 0) actualSpacing += 360f;

                float deviation = Mathf.Abs(actualSpacing - idealAngleSpacing);
                totalDeviation += deviation;
            }

            float avgDeviation = totalDeviation / angles.Count;
            float score = Mathf.Clamp01(1f - avgDeviation / 180f); // Normalize by max deviation (180°)

            // Bonus for full surround (3+ angles well distributed)
            if (angles.Count >= 3 && score > 0.6f)
            {
                score *= surroundBonusMultiplier;
            }
            else if (angles.Count >= 2 && score > 0.5f)
            {
                score *= flankingBonusMultiplier;
            }

            return Mathf.Clamp01(score);
        }

        /// <summary>
        /// Calculate protection score (positioning to shield weak teammates)
        /// </summary>
        private float CalculateProtectionScore(RLGameState state)
        {
            float protectionScore = 0f;
            Vector2 monsterPos = state.monsterPosition;
            Vector2 playerPos = state.playerPosition;

            for (int i = 0; i < state.totalTeammateCount && i < state.teammates.Length; i++)
            {
                var teammate = state.teammates[i];
                if (teammate.health <= 0) continue;

                float healthRatio = teammate.health / 100f;
                if (healthRatio >= 0.5f) continue; // Only protect weak teammates

                // Check if player is between monster and weak teammate
                Vector2 teammatePos = teammate.position;
                Vector2 monsterToTeammate = (teammatePos - monsterPos).normalized;
                Vector2 monsterToPlayer = (playerPos - monsterPos).normalized;

                float alignmentScore = Vector2.Dot(monsterToPlayer, monsterToTeammate);

                // Player is in front of weak teammate (protecting)
                if (alignmentScore > 0.7f)
                {
                    float distanceToMonster = Vector2.Distance(playerPos, monsterPos);
                    float teammateDistance = Vector2.Distance(teammatePos, monsterPos);

                    if (distanceToMonster < teammateDistance)
                    {
                        // Closer to monster = better protection
                        float protectionStrength = (1f - healthRatio) * alignmentScore;
                        protectionScore += protectionStrength * protectWeakReward;
                    }
                }
            }

            return Mathf.Clamp01(protectionScore / 10f); // Normalize to 0-1
        }

        #endregion

        #region Focus Fire Rewards

        /// <summary>
        /// Reward for attacking the same target as teammates (focus fire)
        /// </summary>
        private float CalculateFocusFireReward(RLGameState state)
        {
            if (state.totalTeammateCount == 0) return 0f;

            // Check if team is focused on same target (teamFocusTarget)
            Vector2 focusTarget = state.teamFocusTarget;
            Vector2 monsterPos = state.monsterPosition;

            // Monster is near team focus target
            float distanceToFocus = Vector2.Distance(monsterPos, focusTarget);

            if (distanceToFocus < 5f) // Close to focus target
            {
                // Count teammates also near focus
                int teammatesNearFocus = 0;

                for (int i = 0; i < state.totalTeammateCount && i < state.teammates.Length; i++)
                {
                    var teammate = state.teammates[i];
                    if (teammate.health <= 0) continue;

                    float teammateDistance = Vector2.Distance(teammate.position, focusTarget);
                    if (teammateDistance < 10f)
                    {
                        teammatesNearFocus++;
                    }
                }

                // Reward for coordinated focus fire
                if (teammatesNearFocus >= 1)
                {
                    float focusStrength = teammatesNearFocus / 3f; // Normalize
                    return focusFireReward * focusStrength;
                }
            }

            return 0f;
        }

        #endregion

        #region Base Rewards

        /// <summary>
        /// Calculate base rewards (damage, survival, positioning)
        /// </summary>
        private float CalculateBaseReward(RLGameState previousState, RLGameState currentState, ActionOutcome outcome)
        {
            float baseReward = 0f;

            // Damage reward
            if (outcome.damageDealt > 0)
            {
                baseReward += outcome.damageDealt * 0.5f;
            }

            // Survival reward
            baseReward += 0.1f;

            // Distance reward (getting closer to player)
            float prevDistance = Vector2.Distance(previousState.monsterPosition, previousState.playerPosition);
            float currDistance = Vector2.Distance(currentState.monsterPosition, currentState.playerPosition);

            if (currDistance < prevDistance)
            {
                baseReward += (prevDistance - currDistance) * 0.5f;
            }

            return baseReward;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Calculate cohesion potential for reward shaping
        /// </summary>
        private float CalculateCohesionPotential(RLGameState state)
        {
            if (state.totalTeammateCount == 0) return 0f;

            // Use avgTeammateDistance as cohesion metric
            float ideal = optimalSpreadDistance;
            float deviation = Mathf.Abs(state.avgTeammateDistance - ideal);
            float cohesion = Mathf.Clamp01(1f - deviation / ideal);

            return cohesion - 0.5f; // Center around 0 for potential-based shaping
        }

        /// <summary>
        /// Calculate full co-op reward from game state
        /// </summary>
        private float CalculateCoopReward(RLGameState gameState, Monster monster)
        {
            float totalReward = 0f;

            // Assist (simplified without ActionOutcome)
            // Would need full implementation with damage tracking

            // Aggro share
            totalReward += CalculateAggroShareReward(gameState);

            // Formation
            totalReward += CalculateFormationReward(gameState);

            // Focus fire
            totalReward += CalculateFocusFireReward(gameState);

            return totalReward;
        }

        #endregion

        #region Cleanup & Statistics

        public void CleanupMonster(Monster monster)
        {
            monsterDamageTracking.Remove(monster);
            monsterAggroTracking.Remove(monster);
            monsterFormationTracking.Remove(monster);
        }

        public CoopRewardStats GetStats()
        {
            return new CoopRewardStats
            {
                totalAssists = totalAssists,
                totalAggroShares = totalAggroShares,
                totalFormationBonuses = totalFormationBonuses,
                trackedMonsters = monsterDamageTracking.Count
            };
        }

        private void OnDestroy()
        {
            monsterDamageTracking?.Clear();
            monsterAggroTracking?.Clear();
            monsterFormationTracking?.Clear();
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Tracks damage contributors for assist calculation
    /// </summary>
    public class DamageTracker
    {
        public Dictionary<ulong, float> contributors = new Dictionary<ulong, float>();
        public float lastDamageTime;
    }

    /// <summary>
    /// Tracks aggro target for aggro share calculation
    /// </summary>
    public class AggroTracker
    {
        public int currentTarget; // Agent ID
        public float lastAggroChangeTime;
    }

    /// <summary>
    /// Tracks formation metrics for formation reward calculation
    /// </summary>
    public class FormationTracker
    {
        public float lastSpreadScore;
        public float lastFlankingScore;
        public float lastProtectionScore;
    }

    /// <summary>
    /// Statistics for co-op reward system
    /// </summary>
    public struct CoopRewardStats
    {
        public int totalAssists;
        public int totalAggroShares;
        public int totalFormationBonuses;
        public int trackedMonsters;
    }

    #endregion
}
