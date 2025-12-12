using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// Interface for RL environment as specified in the design document
    /// </summary>
    public interface IRLEnvironment
    {
        /// <summary>
        /// Get current state observations for a monster
        /// </summary>
        /// <param name="monster">Monster to get state for</param>
        /// <returns>State observation array</returns>
        float[] GetState(Monster monster);

        /// <summary>
        /// Calculate reward for an action taken by a monster
        /// </summary>
        /// <param name="monster">Monster that took the action</param>
        /// <param name="action">Action that was taken</param>
        /// <param name="previousState">Previous state before action</param>
        /// <returns>Calculated reward value</returns>
        float CalculateReward(Monster monster, int action, float[] previousState);

        /// <summary>
        /// Check if the episode is complete for a monster
        /// </summary>
        /// <param name="monster">Monster to check episode completion for</param>
        /// <returns>True if episode is complete</returns>
        bool IsEpisodeComplete(Monster monster);

        /// <summary>
        /// Reset the environment to initial state
        /// </summary>
        void ResetEnvironment();
    }
}