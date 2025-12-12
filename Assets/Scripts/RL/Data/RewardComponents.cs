using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// Reward components configuration as specified in the design document
    /// </summary>
    [System.Serializable]
    public class RewardComponents
    {
        [Header("Positive Rewards")]
        [Range(0f, 100f)]
        public float damageDealtReward = 10.0f;
        
        [Range(0f, 10f)]
        public float survivalReward = 0.1f;
        
        [Range(0f, 50f)]
        public float coordinationReward = 5.0f;
        
        [Range(0f, 20f)]
        public float positioningReward = 2.0f;
        
        [Header("Negative Rewards")]
        [Range(-200f, 0f)]
        public float deathPenalty = -50.0f;
        
        [Range(-50f, 0f)]
        public float timeoutPenalty = -10.0f;

        /// <summary>
        /// Validate reward components
        /// </summary>
        public bool IsValid()
        {
            return damageDealtReward >= 0 &&
                   survivalReward >= 0 &&
                   coordinationReward >= 0 &&
                   positioningReward >= 0 &&
                   deathPenalty <= 0 &&
                   timeoutPenalty <= 0;
        }

        /// <summary>
        /// Create default reward components
        /// </summary>
        public static RewardComponents CreateDefault()
        {
            return new RewardComponents
            {
                damageDealtReward = 10.0f,
                survivalReward = 0.1f,
                coordinationReward = 5.0f,
                positioningReward = 2.0f,
                deathPenalty = -50.0f,
                timeoutPenalty = -10.0f
            };
        }

        /// <summary>
        /// Calculate total reward based on action outcome
        /// </summary>
        /// <param name="damageDealt">Damage dealt to player</param>
        /// <param name="survivalTime">Time survived this step</param>
        /// <param name="coordinationBonus">Bonus for coordinated behavior</param>
        /// <param name="positioningBonus">Bonus for good positioning</param>
        /// <param name="isDead">Whether the monster died</param>
        /// <param name="isTimeout">Whether the episode timed out</param>
        /// <returns>Total calculated reward</returns>
        public float CalculateTotalReward(float damageDealt, float survivalTime, float coordinationBonus, 
            float positioningBonus, bool isDead, bool isTimeout)
        {
            float totalReward = 0f;

            // Positive rewards
            totalReward += damageDealt * damageDealtReward;
            totalReward += survivalTime * survivalReward;
            totalReward += coordinationBonus * coordinationReward;
            totalReward += positioningBonus * positioningReward;

            // Negative rewards
            if (isDead)
                totalReward += deathPenalty;
            
            if (isTimeout)
                totalReward += timeoutPenalty;

            return totalReward;
        }

        /// <summary>
        /// Scale reward components by a factor
        /// </summary>
        /// <param name="scaleFactor">Factor to scale rewards by</param>
        /// <returns>Scaled reward components</returns>
        public RewardComponents Scale(float scaleFactor)
        {
            return new RewardComponents
            {
                damageDealtReward = damageDealtReward * scaleFactor,
                survivalReward = survivalReward * scaleFactor,
                coordinationReward = coordinationReward * scaleFactor,
                positioningReward = positioningReward * scaleFactor,
                deathPenalty = deathPenalty * scaleFactor,
                timeoutPenalty = timeoutPenalty * scaleFactor
            };
        }

        /// <summary>
        /// Interpolate between two reward component configurations
        /// </summary>
        /// <param name="other">Other reward components to interpolate with</param>
        /// <param name="t">Interpolation factor (0-1)</param>
        /// <returns>Interpolated reward components</returns>
        public RewardComponents Lerp(RewardComponents other, float t)
        {
            t = Mathf.Clamp01(t);
            return new RewardComponents
            {
                damageDealtReward = Mathf.Lerp(damageDealtReward, other.damageDealtReward, t),
                survivalReward = Mathf.Lerp(survivalReward, other.survivalReward, t),
                coordinationReward = Mathf.Lerp(coordinationReward, other.coordinationReward, t),
                positioningReward = Mathf.Lerp(positioningReward, other.positioningReward, t),
                deathPenalty = Mathf.Lerp(deathPenalty, other.deathPenalty, t),
                timeoutPenalty = Mathf.Lerp(timeoutPenalty, other.timeoutPenalty, t)
            };
        }

        /// <summary>
        /// Create reward components optimized for aggressive behavior
        /// </summary>
        public static RewardComponents CreateAggressive()
        {
            return new RewardComponents
            {
                damageDealtReward = 15.0f,  // Higher reward for damage
                survivalReward = 0.05f,     // Lower survival reward
                coordinationReward = 8.0f,  // Higher coordination reward
                positioningReward = 1.0f,   // Lower positioning reward
                deathPenalty = -30.0f,      // Lower death penalty
                timeoutPenalty = -15.0f     // Higher timeout penalty
            };
        }

        /// <summary>
        /// Create reward components optimized for defensive behavior
        /// </summary>
        public static RewardComponents CreateDefensive()
        {
            return new RewardComponents
            {
                damageDealtReward = 5.0f,   // Lower reward for damage
                survivalReward = 0.2f,      // Higher survival reward
                coordinationReward = 3.0f,  // Lower coordination reward
                positioningReward = 4.0f,   // Higher positioning reward
                deathPenalty = -80.0f,      // Higher death penalty
                timeoutPenalty = -5.0f      // Lower timeout penalty
            };
        }

        /// <summary>
        /// Create reward components optimized for coordinated behavior
        /// </summary>
        public static RewardComponents CreateCoordinated()
        {
            return new RewardComponents
            {
                damageDealtReward = 8.0f,   // Moderate damage reward
                survivalReward = 0.1f,      // Standard survival reward
                coordinationReward = 12.0f, // Very high coordination reward
                positioningReward = 6.0f,   // High positioning reward
                deathPenalty = -40.0f,      // Moderate death penalty
                timeoutPenalty = -8.0f      // Moderate timeout penalty
            };
        }
    }
}