using UnityEngine;
using System.Collections.Generic;
using Vampire.RL;

namespace Vampire
{
    /// <summary>
    /// AI Player Bot for training RLMonsterAgent
    /// Simulates dynamic player behavior to teach monsters adaptability
    /// </summary>
    public class PlayerBotAI : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float changeDirectionInterval = 2f;
        [SerializeField] private bool enableRandomMovement = true;

        [Header("Attack Simulation")]
        [SerializeField] private float attackInterval = 5f;
        [SerializeField] private float attackDamage = 3f;
        [SerializeField] private float aoeDamageRadius = 2.5f;
        [SerializeField] private bool enableAttacks = true;

        [Header("Evasion (Advanced)")]
        [SerializeField] private float dodgeChance = 0.3f; // 30% chance to dodge
        [SerializeField] private float dodgeDistance = 5f;
        [SerializeField] private bool enableEvasion = true;

        [Header("Arena Bounds")]
        [SerializeField] private float arenaSize = 10f;
        [SerializeField] private Vector2 arenaCenter = Vector2.zero;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        private Rigidbody2D rb;
        private Vector2 currentDirection = Vector2.right;
        private float nextDirectionChangeTime;
        private float nextAttackTime;
        private float nextDodgeTime;

        // Track nearby monsters for strategic behavior
        private List<RLMonsterAgent> nearbyMonsters = new List<RLMonsterAgent>();
        private const float detectionRadius = 15f;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        void Start()
        {
            // Initialize timers
            nextDirectionChangeTime = Time.time + changeDirectionInterval;
            nextAttackTime = Time.time + attackInterval;
            nextDodgeTime = Time.time + 1f;

            Debug.Log("[PlayerBotAI] Initialized - ready to train monsters!");
        }

        void Update()
        {
            // Update nearby monsters list
            UpdateNearbyMonsters();

            // Handle movement
            if (enableRandomMovement)
            {
                UpdateMovement();
            }

            // Handle attacks
            if (enableAttacks)
            {
                UpdateAttacks();
            }

            // Handle evasion
            if (enableEvasion)
            {
                UpdateEvasion();
            }

            // Keep in bounds
            KeepInBounds();
        }

        /// <summary>
        /// Change direction periodically to simulate player movement
        /// </summary>
        void UpdateMovement()
        {
            // Change direction at intervals
            if (Time.time >= nextDirectionChangeTime)
            {
                // Random direction (can be optimized to move away from concentration of monsters)
                currentDirection = Random.insideUnitCircle.normalized;

                // Occasionally stop for a moment
                if (Random.value < 0.2f) // 20% chance
                {
                    currentDirection = Vector2.zero;
                }

                nextDirectionChangeTime = Time.time + Random.Range(1f, 3f);

                if (showDebugGizmos)
                    Debug.Log($"[PlayerBot] Changed direction to {currentDirection}");
            }

            rb.linearVelocity = currentDirection * moveSpeed;
        }

        /// <summary>
        /// Simulate player attacks to damage nearby monsters
        /// Teaches monsters to retreat from danger
        /// </summary>
        void UpdateAttacks()
        {
            if (Time.time >= nextAttackTime)
            {
                // Choose random attack type
                int attackType = Random.Range(0, 3);

                switch (attackType)
                {
                    case 0: // AOE attack (Garlic-like)
                        ExecuteAOEAttack();
                        break;
                    case 1: // Projectile attack (Whip-like)
                        ExecuteProjectileAttack();
                        break;
                    case 2: // Melee attack
                        ExecuteMeleeAttack();
                        break;
                }

                nextAttackTime = Time.time + Random.Range(2f, 4f);
            }
        }

        /// <summary>
        /// AOE attack - damages all monsters in radius
        /// </summary>
        void ExecuteAOEAttack()
        {
            if (showDebugGizmos)
                Debug.Log("[PlayerBot] AOE Attack!");

            float scaled = ScaleDamage(attackDamage);

            // Find all monsters in radius
            Collider2D[] monstersInRange = Physics2D.OverlapCircleAll(
                transform.position,
                aoeDamageRadius
            );

            foreach (var collider in monstersInRange)
            {
                var rlAgent = collider.GetComponent<RLMonsterAgent>();
                if (rlAgent != null && rlAgent.gameObject != gameObject)
                {
                    rlAgent.OnTakeDamage(scaled);

                    if (showDebugGizmos)
                        Debug.Log($"[PlayerBot] Damaged {rlAgent.gameObject.name} for {scaled:F1}");
                }
            }
        }

        /// <summary>
        /// Projectile attack - shoots toward nearby monster
        /// </summary>
        void ExecuteProjectileAttack()
        {
            if (nearbyMonsters.Count == 0) return;

            // Pick random monster to attack
            RLMonsterAgent target = nearbyMonsters[Random.Range(0, nearbyMonsters.Count)];

            if (showDebugGizmos)
                Debug.Log($"[PlayerBot] Projectile Attack toward {target.gameObject.name}");

            // Direct damage
            target.OnTakeDamage(ScaleDamage(attackDamage * 0.8f));
        }

        /// <summary>
        /// Melee attack - damages monsters in front
        /// </summary>
        void ExecuteMeleeAttack()
        {
            if (showDebugGizmos)
                Debug.Log("[PlayerBot] Melee Attack!");

            // Damage monsters in front direction
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                transform.position,
                currentDirection != Vector2.zero ? currentDirection : Vector2.right,
                3f
            );

            foreach (var hit in hits)
            {
                var rlAgent = hit.collider.GetComponent<RLMonsterAgent>();
                if (rlAgent != null)
                {
                    rlAgent.OnTakeDamage(ScaleDamage(attackDamage));
                }
            }
        }

        /// <summary>
        /// Dodge/evade monsters approaching (teaches monsters to be tactical)
        /// </summary>
        void UpdateEvasion()
        {
            if (Time.time >= nextDodgeTime)
            {
                // Check if monsters are too close
                if (nearbyMonsters.Count > 0)
                {
                    // Count monsters within critical distance
                    int closeMonsters = 0;
                    foreach (var monster in nearbyMonsters)
                    {
                        if (monster != null)
                        {
                            float dist = Vector2.Distance(transform.position, monster.transform.position);
                            if (dist < 4f) // Critical distance
                                closeMonsters++;
                        }
                    }

                    // If surrounded, dodge away
                    if (closeMonsters >= 3 || Random.value < dodgeChance)
                    {
                        Vector2 dodgeDirection = -GetAverageMonsterDirection();

                        // Temporary speed boost
                        StartCoroutine(TemporaryDodge(dodgeDirection));

                        if (showDebugGizmos)
                            Debug.Log($"[PlayerBot] Evading! ({closeMonsters} monsters nearby)");
                    }
                }

                nextDodgeTime = Time.time + Random.Range(0.5f, 1.5f);
            }
        }

        /// <summary>
        /// Temporary dodge burst
        /// </summary>
        System.Collections.IEnumerator TemporaryDodge(Vector2 direction)
        {
            float originalSpeed = moveSpeed;
            moveSpeed = originalSpeed * 1.5f; // 50% speed boost

            currentDirection = direction.normalized;

            yield return new WaitForSeconds(0.5f);

            moveSpeed = originalSpeed;
        }

        /// <summary>
        /// Calculate average direction to all nearby monsters
        /// </summary>
        Vector2 GetAverageMonsterDirection()
        {
            Vector2 average = Vector2.zero;
            int validCount = 0;

            foreach (var monster in nearbyMonsters)
            {
                if (monster != null)
                {
                    average += (Vector2)monster.transform.position - rb.position;
                    validCount++;
                }
            }

            if (validCount > 0)
                return average / validCount;

            return Vector2.zero;
        }

        /// <summary>
        /// Update list of nearby monsters for tactical decisions
        /// </summary>
        void UpdateNearbyMonsters()
        {
            nearbyMonsters.Clear();

            Collider2D[] colliders = Physics2D.OverlapCircleAll(
                transform.position,
                detectionRadius
            );

            foreach (var collider in colliders)
            {
                var rlAgent = collider.GetComponent<RLMonsterAgent>();
                if (rlAgent != null)
                {
                    nearbyMonsters.Add(rlAgent);
                }
            }
        }

        /// <summary>
        /// Keep player within arena bounds
        /// </summary>
        void KeepInBounds()
        {
            Vector3 pos = transform.position;
            float maxX = arenaCenter.x + arenaSize;
            float minX = arenaCenter.x - arenaSize;
            float maxY = arenaCenter.y + arenaSize;
            float minY = arenaCenter.y - arenaSize;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

            // Bounce back if hit boundary
            if (pos.x == minX || pos.x == maxX)
                currentDirection.x *= -1;
            if (pos.y == minY || pos.y == maxY)
                currentDirection.y *= -1;

            transform.position = pos;
        }

        float ScaleDamage(float baseDamage)
        {
            var manager = RLDamageMultiplierManager.Instance;
            if (manager != null)
            {
                return baseDamage * manager.GetDamageMultiplier();
            }
            return baseDamage;
        }

        /// <summary>
        /// Get current monster count for monitoring
        /// </summary>
        public int GetNearbyMonsterCount() => nearbyMonsters.Count;

        /// <summary>
        /// Debug visualization
        /// </summary>
        void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw arena bounds
            Gizmos.color = Color.gray;
            Vector2 bottomLeft = arenaCenter - Vector2.one * arenaSize;
            Vector2 topRight = arenaCenter + Vector2.one * arenaSize;

            Gizmos.DrawWireCube(
                arenaCenter,
                new Vector3(arenaSize * 2, arenaSize * 2, 0)
            );

            // Draw movement direction
            if (Application.isPlaying && rb != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, currentDirection * 2f);

                // Draw attack radius
                Gizmos.color = new Color(1, 0, 0, 0.3f);
                Gizmos.DrawWireSphere(transform.position, aoeDamageRadius);

                // Draw detection radius
                Gizmos.color = new Color(0, 1, 0, 0.2f);
                Gizmos.DrawWireSphere(transform.position, detectionRadius);
            }
        }

        /// <summary>
        /// Update arena center and size from external controller
        /// </summary>
        public void SetArena(Vector2 center, float size)
        {
            arenaCenter = center;
            arenaSize = size;
        }

        /// <summary>
        /// Get bot status string for UI display
        /// </summary>
        public string GetStatusString()
        {
            return $"PlayerBot | Speed: {moveSpeed:F1} | Nearby Monsters: {nearbyMonsters.Count} | " +
                   $"Attacks: {(enableAttacks ? "ON" : "OFF")} | Evasion: {(enableEvasion ? "ON" : "OFF")}";
        }
    }
}
