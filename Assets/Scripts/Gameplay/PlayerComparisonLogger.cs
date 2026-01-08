using UnityEngine;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Simple comparison logger for debugging player movement differences
    /// </summary>
    public class PlayerComparisonLogger : MonoBehaviour
    {
        private void Update()
        {
            // Press L key to log comparison
            if (Input.GetKeyDown(KeyCode.L))
            {
                LogComparison();
            }
        }

        [ContextMenu("Log Player Comparison")]
        public void LogComparison()
        {
            Character[] characters = FindObjectsOfType<Character>();
            
            if (characters.Length < 2)
            {
                Debug.LogWarning($"Only found {characters.Length} Character(s)");
                return;
            }

            Debug.Log("\n╔══════════════════════════════════════════════════════╗");
            Debug.Log("║         PLAYER COMPARISON (Press L to refresh)       ║");
            Debug.Log("╚══════════════════════════════════════════════════════╝\n");

            for (int i = 0; i < Mathf.Min(characters.Length, 2); i++)
            {
                var c = characters[i];
                var rb = c.GetComponent<Rigidbody2D>();
                var kb = c.GetComponent<PlayerKeyboardController>();
                
                Debug.Log($"┌─ Player {i + 1}: {c.gameObject.name}");
                
                if (c.Blueprint == null)
                {
                    Debug.LogError("│  ✗✗✗ NO CHARACTERBLUEPRINT! ✗✗✗");
                }
                else
                {
                    Debug.Log($"│  Blueprint: {c.Blueprint.name}");
                    Debug.Log($"│  Movespeed: {c.Blueprint.movespeed:F2}");
                    Debug.Log($"│  Acceleration: {c.Blueprint.acceleration:F2}");
                }

                if (rb != null)
                {
                    Debug.Log($"│  Mass: {rb.mass:F2}");
                    Debug.Log($"│  LinearDamping: {rb.linearDamping:F2}");
                    Debug.Log($"│  GravityScale: {rb.gravityScale:F2}");
                    Debug.Log($"│  Velocity: {rb.linearVelocity} (mag: {rb.linearVelocity.magnitude:F2})");
                    Debug.Log($"│  Position: {c.transform.position}");
                }
                else
                {
                    Debug.LogError("│  ✗ No Rigidbody2D!");
                }

                if (kb != null)
                {
                    Debug.Log($"│  PlayerKeyboardController: {(kb.enabled ? "ENABLED" : "DISABLED")}");
                }
                else
                {
                    Debug.LogWarning("│  ⚠ No PlayerKeyboardController!");
                }

                Debug.Log("└─────────────────────────────────────────────────\n");
            }

            // Compare
            if (characters.Length >= 2)
            {
                var rb1 = characters[0].GetComponent<Rigidbody2D>();
                var rb2 = characters[1].GetComponent<Rigidbody2D>();

                if (rb1 != null && rb2 != null)
                {
                    bool dampingMatch = Mathf.Abs(rb1.linearDamping - rb2.linearDamping) < 0.01f;
                    bool massMatch = Mathf.Abs(rb1.mass - rb2.mass) < 0.01f;

                    Debug.Log("═══════════════════════════════════════════════════");
                    Debug.Log(dampingMatch ? 
                        "✓ LinearDamping MATCH" : 
                        $"✗ LinearDamping MISMATCH: {rb1.linearDamping:F2} vs {rb2.linearDamping:F2}");
                    Debug.Log(massMatch ? 
                        "✓ Mass MATCH" : 
                        $"✗ Mass MISMATCH: {rb1.mass:F2} vs {rb2.mass:F2}");

                    if (characters[0].Blueprint != null && characters[1].Blueprint != null)
                    {
                        bool speedMatch = Mathf.Abs(characters[0].Blueprint.movespeed - characters[1].Blueprint.movespeed) < 0.01f;
                        bool accelMatch = Mathf.Abs(characters[0].Blueprint.acceleration - characters[1].Blueprint.acceleration) < 0.01f;
                        
                        Debug.Log(speedMatch ? 
                            "✓ Movespeed MATCH" : 
                            $"✗ Movespeed MISMATCH: {characters[0].Blueprint.movespeed} vs {characters[1].Blueprint.movespeed}");
                        Debug.Log(accelMatch ? 
                            "✓ Acceleration MATCH" : 
                            $"✗ Acceleration MISMATCH: {characters[0].Blueprint.acceleration} vs {characters[1].Blueprint.acceleration}");
                    }

                    Debug.Log("═══════════════════════════════════════════════════\n");
                }
            }
        }
    }
}
