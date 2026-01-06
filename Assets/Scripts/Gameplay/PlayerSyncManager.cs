using UnityEngine;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Automatically syncs Rigidbody2D settings across all player characters at game start
    /// Ensures consistent movement behavior for local co-op players
    /// </summary>
    public class PlayerSyncManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool syncOnStart = true;
        [SerializeField] private bool logSyncDetails = true;
        [SerializeField] private bool enableRuntimeMonitoring = true;
        [SerializeField] private float monitoringInterval = 2f;

        private float lastMonitorTime;

        private void Start()
        {
            if (syncOnStart)
            {
                SyncAllPlayers();
            }
        }

        private void Update()
        {
            if (enableRuntimeMonitoring && Time.time - lastMonitorTime > monitoringInterval)
            {
                lastMonitorTime = Time.time;
                MonitorPlayers();
            }
        }

        /// <summary>
        /// Sync all player Rigidbody2D settings to ensure consistent physics behavior
        /// </summary>
        [ContextMenu("Sync All Players")]
        public void SyncAllPlayers()
        {
            Character[] allCharacters = FindObjectsOfType<Character>();

            if (allCharacters.Length == 0)
            {
                Debug.LogWarning("[PlayerSync] No Character components found in scene");
                return;
            }

            if (allCharacters.Length == 1)
            {
                if (logSyncDetails)
                    Debug.Log("[PlayerSync] Only one player found, no sync needed");

                // Still ensure the single player has correct settings
                EnsureCorrectRigidbodySettings(allCharacters[0]);
                return;
            }

            if (logSyncDetails)
                Debug.Log($"[PlayerSync] Syncing {allCharacters.Length} players...");

            // Use first character as reference
            Character referenceCharacter = allCharacters[0];
            Rigidbody2D referenceRb = referenceCharacter.GetComponent<Rigidbody2D>();

            if (referenceRb == null)
            {
                Debug.LogError("[PlayerSync] Reference character missing Rigidbody2D!");
                return;
            }

            // Ensure reference character has correct settings
            EnsureCorrectRigidbodySettings(referenceCharacter);

            // Sync all other characters to match reference
            for (int i = 1; i < allCharacters.Length; i++)
            {
                SyncCharacterToReference(allCharacters[i], referenceRb, i);
            }

            if (logSyncDetails)
                Debug.Log($"[PlayerSync] ✓ Successfully synced all {allCharacters.Length} players");
        }

        /// <summary>
        /// Ensure a character has correct Rigidbody2D settings
        /// </summary>
        private void EnsureCorrectRigidbodySettings(Character character)
        {
            Rigidbody2D rb = character.GetComponent<Rigidbody2D>();
            if (rb == null) return;

            // Standard settings for top-down character movement
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // Let Character.UpdateMoveSpeed() calculate the correct linearDamping
            // based on the character's blueprint values
            character.UpdateMoveSpeed();

            if (logSyncDetails)
            {
                Debug.Log($"[PlayerSync] {character.gameObject.name}: " +
                         $"linearDamping={rb.linearDamping:F2}, " +
                         $"gravityScale={rb.gravityScale}, " +
                         $"constraints={rb.constraints}");
            }
        }

        /// <summary>
        /// Sync a character's Rigidbody2D to match reference settings
        /// </summary>
        private void SyncCharacterToReference(Character character, Rigidbody2D referenceRb, int playerIndex)
        {
            Rigidbody2D rb = character.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                Debug.LogError($"[PlayerSync] Player {playerIndex} ({character.gameObject.name}) missing Rigidbody2D!");
                return;
            }

            // Copy physics settings from reference
            rb.gravityScale = referenceRb.gravityScale;
            rb.constraints = referenceRb.constraints;

            // Important: Call UpdateMoveSpeed to calculate linearDamping based on character's own blueprint
            // This ensures each character uses their own movespeed/acceleration values
            character.UpdateMoveSpeed();

            if (logSyncDetails)
            {
                Debug.Log($"[PlayerSync] Player {playerIndex} ({character.gameObject.name}): " +
                         $"linearDamping={rb.linearDamping:F2}, " +
                         $"gravityScale={rb.gravityScale}, " +
                         $"constraints={rb.constraints}");
            }
        }

        /// <summary>
        /// Force sync a specific character to match another
        /// </summary>
        public void SyncCharacter(Character target, Character reference)
        {
            if (target == null || reference == null)
            {
                Debug.LogError("[PlayerSync] Cannot sync null characters");
                return;
            }

            Rigidbody2D refRb = reference.GetComponent<Rigidbody2D>();
            if (refRb == null)
            {
                Debug.LogError("[PlayerSync] Reference character missing Rigidbody2D");
                return;
            }

            SyncCharacterToReference(target, refRb, 0);
        }

        /// <summary>
        /// Monitor runtime player statistics to help debug movement differences
        /// </summary>
        private void MonitorPlayers()
        {
            Character[] allCharacters = FindObjectsOfType<Character>();
            if (allCharacters.Length < 2) return;

            Debug.Log("=== Player Movement Monitor ===");

            for (int i = 0; i < allCharacters.Length; i++)
            {
                Character c = allCharacters[i];
                Rigidbody2D rb = c.GetComponent<Rigidbody2D>();

                if (rb == null || c.Blueprint == null) continue;

                Debug.Log($"Player {i} ({c.gameObject.name}):\n" +
                         $"  Position: {c.transform.position}\n" +
                         $"  Velocity: {rb.linearVelocity} (magnitude: {rb.linearVelocity.magnitude:F2})\n" +
                         $"  Blueprint: movespeed={c.Blueprint.movespeed:F2}, accel={c.Blueprint.acceleration:F2}\n" +
                         $"  Rigidbody: linearDamping={rb.linearDamping:F2}, mass={rb.mass:F2}\n" +
                         $"  Constraints: {rb.constraints}, GravityScale: {rb.gravityScale}");
            }
        }
    }
}
