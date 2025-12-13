using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// Interface for calculating rewards based on monster actions and outcomes
    /// </summary>
    public interface IRewardCalculator
    {
        /// <summary>
        /// Calculate reward for a monster action
        /// </summary>
        /// <param name="monster">Monster that took the action</param>
        /// <param name="action">Action taken</param>
        /// <param name="previousState">State before action</param>
        /// <returns>Reward value</returns>
        float CalculateReward(Monster monster, int action, float[] previousState);

        /// <summary>
        /// Calculate reward for a monster action with detailed context
        /// </summary>
        /// <param name="previousState">State before action</param>
        /// <param name="action">Action taken</param>
        /// <param name="currentState">State after action</param>
        /// <param name="actionOutcome">Result of the action</param>
        /// <returns>Reward value</returns>
        float CalculateReward(RLGameState previousState, MonsterAction action, RLGameState currentState, ActionOutcome actionOutcome);

        /// <summary>
        /// Calculate terminal reward when monster dies or episode ends
        /// </summary>
        /// <param name="finalState">Final state of the episode</param>
        /// <param name="episodeLength">Length of the episode</param>
        /// <param name="killedByPlayer">Whether monster was killed by player</param>
        /// <returns>Terminal reward value</returns>
        float CalculateTerminalReward(RLGameState finalState, float episodeLength, bool killedByPlayer);

        /// <summary>
        /// Apply reward shaping for better learning convergence
        /// </summary>
        /// <param name="baseReward">Base reward from action</param>
        /// <param name="state">Current state</param>
        /// <returns>Shaped reward</returns>
        float ShapeReward(float baseReward, RLGameState state);

        /// <summary>
        /// Update reward configuration at runtime
        /// </summary>
        /// <param name="rewardComponents">New reward configuration</param>
        void UpdateRewardConfiguration(object rewardComponents);
    }
}