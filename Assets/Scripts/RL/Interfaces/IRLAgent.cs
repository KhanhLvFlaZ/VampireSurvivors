using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// Core interface for RL agents as specified in the design document
    /// </summary>
    public interface IRLAgent
    {
        /// <summary>
        /// Initialize the agent with RL configuration
        /// </summary>
        /// <param name="config">RL configuration parameters</param>
        void Initialize(RLConfig config);

        /// <summary>
        /// Select an action based on current observations
        /// </summary>
        /// <param name="observations">Current state observations as float array</param>
        /// <returns>Selected action index</returns>
        int SelectAction(float[] observations);

        /// <summary>
        /// Store experience for training
        /// </summary>
        /// <param name="state">Previous state</param>
        /// <param name="action">Action taken</param>
        /// <param name="reward">Reward received</param>
        /// <param name="nextState">Next state</param>
        /// <param name="done">Whether episode is complete</param>
        void StoreExperience(float[] state, int action, float reward, float[] nextState, bool done);

        /// <summary>
        /// Update the model based on stored experiences
        /// </summary>
        void UpdateModel();

        /// <summary>
        /// Get current observations from the environment
        /// </summary>
        /// <returns>Observation array</returns>
        float[] GetObservations();
    }
}