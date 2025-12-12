using UnityEngine;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Interface for behavior visualization as specified in the design document
    /// </summary>
    public interface IBehaviorVisualizer
    {
        /// <summary>
        /// Show decision indicator for a monster's RL decision
        /// </summary>
        /// <param name="monster">Monster making the decision</param>
        /// <param name="action">Action selected</param>
        /// <param name="confidence">Confidence level of the decision</param>
        void ShowDecisionIndicator(Monster monster, int action, float confidence);

        /// <summary>
        /// Show coordination indicator for team behaviors
        /// </summary>
        /// <param name="monsters">List of monsters coordinating</param>
        void ShowCoordinationIndicator(List<Monster> monsters);

        /// <summary>
        /// Show adaptation indicator when monster adapts strategy
        /// </summary>
        /// <param name="monster">Monster adapting</param>
        /// <param name="adaptationType">Type of adaptation</param>
        void ShowAdaptationIndicator(Monster monster, string adaptationType);

        /// <summary>
        /// Show debug information for RL state and actions
        /// </summary>
        /// <param name="monster">Monster to show debug info for</param>
        /// <param name="state">Current state</param>
        /// <param name="action">Current action</param>
        void ShowDebugInfo(Monster monster, float[] state, int action);
    }
}