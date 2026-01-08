using UnityEngine;

namespace Vampire.RL.Tests
{
    /// <summary>
    /// Utility to run all RL tests from one place
    /// </summary>
    public class RunAllTests : MonoBehaviour
    {
        [ContextMenu("Run StateEncoder Tests")]
        public void RunStateEncoderTests()
        {
            var test = GetComponent<StateEncoderTest>();
            if (test != null)
            {
                test.RunAllTests();
            }
            else
            {
                Debug.LogError("StateEncoderTest component not found!");
            }
        }

        [ContextMenu("Run All RL Tests")]
        public void RunAllRLTests()
        {
            Debug.Log("\n========== RUNNING ALL RL TESTS ==========\n");

            // Run StateEncoder tests
            Debug.Log("[1/1] Running StateEncoder Tests...");
            RunStateEncoderTests();

            Debug.Log("\n========== ALL TESTS COMPLETED ==========\n");
            Debug.Log("State size should be: 82");
            Debug.Log("Breakdown:");
            Debug.Log("  - Player state: 7");
            Debug.Log("  - Teammate state: 18 (3 teammates × 6)");
            Debug.Log("  - Monster state: 6");
            Debug.Log("  - Nearby monsters: 20 (5 × 4)");
            Debug.Log("  - Nearby collectibles: 30 (10 × 3)");
            Debug.Log("  - Temporal state: 1");
            Debug.Log("  Total: 7 + 18 + 6 + 20 + 30 + 1 = 82");
        }
    }
}
