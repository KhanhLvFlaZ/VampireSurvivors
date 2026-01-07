using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using UnityEngine;
using System.Collections.Generic;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// ML-Agents compatible monster agent with tactical behavior
    /// Implements tactical spacing, risk assessment, and coordination
    /// Based on Unity ML-Agents framework for proper training support
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class RLMonsterAgent : Agent
    {
        [Header("Monster References")]
        [SerializeField] private Monster baseMonster; // Can attach existing Monster component
        private Rigidbody2D rb;
        private EntityManager entityManager;
        private Character playerCharacter;
        private Transform playerTransform; // Fallback when Character component is missing

        [Header("Stats")]
        public float maxHP = 50f; // Higher HP for training - allows time to learn retreat
        private float currentHP;
        public float moveSpeed = 3f;
        private float lastDamagedTime;

        [Header("Tactical Settings")]
        public float maxDetectionRange = 15f;
        public float optimalRange = 4f; // Optimal distance from player
        private float playerDamageRate; // Damage/s player is dealing
        private float damageReceivedRecently = 0f;
        private float damageTrackingWindow = 5f; // Track damage in last 5s

        [Header("Allies Tracking")]
        private List<RLMonsterAgent> nearbyAllies = new List<RLMonsterAgent>();
        public float allyCheckRadius = 10f;

        [Header("Survival Tracking")]
        private float episodeStartTime;
        private float lastSurvivalRewardTime;

        [Header("Arena Bounds")]
        [SerializeField] private Vector2 arenaCenter = Vector2.zero;
        [SerializeField] private float arenaHalfSize = 12f;
        [SerializeField] private float wallMargin = 1.0f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;

        // Inference control state
        private bool isControlling = false;
        public bool IsControlling => isControlling;
        private float decisionInterval = 0.1f; // seconds
        private float nextDecisionTime = 0f;

        /// <summary>
        /// Initialize the agent (called by Unity ML-Agents)
        /// </summary>
        public override void Initialize()
        {
            rb = GetComponent<Rigidbody2D>();
            currentHP = maxHP;

            // Try to find Monster component if not assigned
            if (baseMonster == null)
            {
                baseMonster = GetComponent<Monster>();
            }

            // Auto-link with Monster to sync HP; native AI will be disabled when we start controlling
            if (baseMonster != null)
            {
                LinkWithMonster(baseMonster);
            }

            Debug.Log($"[RLMonsterAgent] Initialized with maxHP={maxHP}, moveSpeed={moveSpeed}");
        }

        /// <summary>
        /// Called when episode begins (respawn/reset)
        /// </summary>
        public override void OnEpisodeBegin()
        {
            // Reset position randomly within spawn area (clamped to arena)
            Vector2 randomPos = new Vector2(
                Random.Range(-10f, 10f),
                Random.Range(-10f, 10f)
            );
            Vector2 clampedPos = ClampToArena(randomPos);
            transform.position = clampedPos;

            // Ensure rigidbody is properly configured
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            // Reset stats
            currentHP = maxHP;
            lastDamagedTime = 0f;
            damageReceivedRecently = 0f;
            episodeStartTime = Time.time;
            lastSurvivalRewardTime = Time.time;

            // Find nearest player
            playerCharacter = FindNearestPlayer();
            if (playerCharacter == null)
            {
                playerTransform = FindPlayerTransformFallback();
            }

            // Update nearby allies
            UpdateNearbyAllies();

            Debug.Log($"[RLMonsterAgent] Episode started at position {transform.position}");
        }

        /// <summary>
        /// Collect observations for the neural network (7 values)
        /// Called automatically by ML-Agents before action selection
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            Vector3 playerPos;
            if (!TryGetPlayerPosition(out playerPos))
            {
                // No player found, send zero observations
                sensor.AddObservation(Vector2.zero); // 2 floats
                sensor.AddObservation(0f); // 1 float
                sensor.AddObservation(0f); // 1 float
                sensor.AddObservation(0f); // 1 float
                sensor.AddObservation(0f); // 1 float
                sensor.AddObservation(0f); // 1 float
                return;
            }

            // === SPATIAL AWARENESS (3 values) ===
            Vector2 dirToPlayer = (playerPos - transform.position);
            float distance = dirToPlayer.magnitude;

            // Safe normalize: avoid NaN from zero vectors
            Vector2 dirNormalized = distance > 0.001f ? dirToPlayer.normalized : Vector2.zero;
            sensor.AddObservation(dirNormalized); // 2 floats: direction to player

            // Clamp distance to safe range (avoid division edge cases)
            float normalizedDistance = maxDetectionRange > 0.001f ? Mathf.Clamp01(distance / maxDetectionRange) : 0f;
            sensor.AddObservation(normalizedDistance); // 1 float: normalized distance

            // === RISK ASSESSMENT (2 values) ===
            float hpRatio = maxHP > 0.001f ? Mathf.Clamp01(currentHP / maxHP) : 1f;
            sensor.AddObservation(hpRatio); // 1 float: HP ratio

            sensor.AddObservation(Mathf.Min(nearbyAllies.Count / 5f, 1f)); // 1 float: allies count (max 5)

            // === TACTICAL INFO (2 values) ===
            float safeDamageRate = Mathf.Clamp(playerDamageRate, 0f, 100f); // Clamp to reasonable range
            sensor.AddObservation(safeDamageRate); // 1 float: player's recent damage rate

            float timeSinceLastDamaged = Time.time - lastDamagedTime;
            float safeTimeSinceLastDamaged = Mathf.Clamp01(timeSinceLastDamaged / 5f);
            sensor.AddObservation(safeTimeSinceLastDamaged); // 1 float: time since damaged

            // TOTAL: 7 observations
        }

        /// <summary>
        /// Execute actions from neural network (5 discrete actions)
        /// Called automatically by ML-Agents after action selection
        /// </summary>
        public override void OnActionReceived(ActionBuffers actions)
        {
            Vector3 playerPos;
            if (!TryGetPlayerPosition(out playerPos)) return;

            // We are actively controlling (received at least one decision)
            isControlling = true;
            if (baseMonster != null) baseMonster.ExternalControlEnabled = true;

            int tacticalAction = actions.DiscreteActions[0]; // 0-4: tactical decisions

            Vector2 dirToPlayer = (playerPos - transform.position).normalized;
            float distanceToPlayer = Vector2.Distance(transform.position, playerPos);

            Vector2 moveDirection = Vector2.zero;
            float speedMultiplier = 1f;

            switch (tacticalAction)
            {
                case 0: // AGGRESSIVE - Rush toward player
                    moveDirection = dirToPlayer;
                    speedMultiplier = 1.2f;
                    break;

                case 1: // MAINTAIN_DISTANCE - Keep optimal range
                    if (distanceToPlayer < optimalRange)
                        moveDirection = -dirToPlayer; // Move away
                    else if (distanceToPlayer > optimalRange + 2f)
                        moveDirection = dirToPlayer; // Move closer
                    else
                        moveDirection = Vector2.zero; // Hold position
                    speedMultiplier = 0.8f;
                    break;

                case 2: // RETREAT - Pull back when in danger
                    moveDirection = -dirToPlayer;
                    speedMultiplier = 1.0f;
                    break;

                case 3: // FLANK - Circle around player
                    Vector2 perpDir = new Vector2(-dirToPlayer.y, dirToPlayer.x);
                    moveDirection = perpDir;
                    speedMultiplier = 0.9f;
                    break;

                case 4: // WAIT - Slow approach, wait for allies
                    moveDirection = dirToPlayer;
                    speedMultiplier = 0.5f;
                    break;
            }

            // Apply movement with smoothing so external impulses (e.g., knockback) aren't hard-cancelled
            Vector2 desiredVelocity = moveDirection * moveSpeed * speedMultiplier;
            float blend = 0.3f; // smoothing factor (0..1)
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, blend);

            // === CALCULATE REWARDS ===
            CalculateTacticalRewards(tacticalAction, distanceToPlayer);
        }

        void Update()
        {
            // Version-safe: request decisions at a fixed interval (works for training/inference/heuristic)
            if (Time.time >= nextDecisionTime)
            {
                RequestDecision();
                nextDecisionTime = Time.time + decisionInterval;
            }
        }

        /// <summary>
        /// Calculate rewards to guide learning toward tactical behavior
        /// </summary>
        private void CalculateTacticalRewards(int action, float distance)
        {
            float hpRatio = currentHP / maxHP;

            // === ENGAGEMENT INCENTIVE ===
            // Reward for being close enough to engage (unless low HP)
            if (distance < 6f && hpRatio > 0.5f)
                AddReward(0.02f); // Good engagement range when healthy

            // === TACTICAL SPACING REWARD ===
            bool inOptimalRange = distance >= optimalRange && distance <= (optimalRange + 2f);
            if (inOptimalRange)
                AddReward(0.03f); // Reward balanced positioning

            if (distance < 2f && hpRatio < 0.5f)
                AddReward(-0.02f); // Too close when low HP - dangerous!

            if (distance > 12f)
                AddReward(-0.02f); // Too far - not engaging

            // === LOW HP RETREAT INCENTIVES ===
            if (hpRatio < 0.3f)
            {
                if (distance > 6f)
                    AddReward(0.02f); // Reward for keeping safe distance when low HP
                else if (distance < 3f)
                    AddReward(-0.03f); // Penalty for being too close when low HP
            }

            if (hpRatio < 0.3f && action == 2) // Retreat when low HP
                AddReward(0.05f); // Smart decision!
            else if (hpRatio < 0.2f && action == 0) // Aggressive when critical HP
                AddReward(-0.1f); // Bad decision - suicide!

            if (hpRatio > 0.7f && action == 0) // Aggressive when strong
                AddReward(0.02f); // Good tactical opportunity

            // === INACTIVITY PENALTY ===
            if (action == 4 && distance > 8f && hpRatio > 0.6f)
                AddReward(-0.01f); // Penalty for excessive waiting/fleeing

            // === COORDINATION REWARD ===
            if (nearbyAllies.Count >= 2)
                AddReward(0.02f); // Staying with group
            else if (nearbyAllies.Count == 0 && distance > 7f && hpRatio > 0.5f)
                AddReward(-0.01f); // Isolated and far - should engage

            // === SURVIVAL REWARD (every 10 seconds) ===
            if (Time.time - lastSurvivalRewardTime > 10f)
            {
                AddReward(0.5f); // Reward for surviving
                lastSurvivalRewardTime = Time.time;
            }

            // Update player damage tracking
            UpdatePlayerDamageRate();
        }

        /// <summary>
        /// Called when monster takes damage
        /// Should be called from damage system or Monster component
        /// </summary>
        public void OnTakeDamage(float damage)
        {
            currentHP -= damage;
            lastDamagedTime = Time.time;
            damageReceivedRecently += damage;

            // Reduced damage penalty - allow tactical damage to encourage engagement
            AddReward(-0.015f * damage);

            if (currentHP <= 0)
            {
                OnDeath();
            }
        }

        /// <summary>
        /// Called when monster successfully damages player
        /// Should be called from attack/collision system
        /// </summary>
        public void OnDamagePlayer(float damage)
        {
            AddReward(0.2f); // Moderate reward - encourages engagement without overriding survival
        }

        /// <summary>
        /// Called when monster dies
        /// </summary>
        public void OnDeath()
        {
            AddReward(-2.0f); // Big penalty for dying - learn to survive!
            EndEpisode(); // Trigger episode reset
        }

        /// <summary>
        /// Update player damage rate calculation
        /// </summary>
        private void UpdatePlayerDamageRate()
        {
            // Calculate damage/s in recent window
            playerDamageRate = damageReceivedRecently / damageTrackingWindow;

            // Decay old damage over time
            damageReceivedRecently *= 0.99f;
        }

        /// <summary>
        /// Update list of nearby ally monsters
        /// </summary>
        private void UpdateNearbyAllies()
        {
            nearbyAllies.Clear();
            var allMonsters = FindObjectsOfType<RLMonsterAgent>();

            foreach (var monster in allMonsters)
            {
                if (monster == this) continue;

                float dist = Vector2.Distance(transform.position, monster.transform.position);
                if (dist <= allyCheckRadius)
                {
                    nearbyAllies.Add(monster);
                }
            }
        }

        /// <summary>
        /// Find nearest player character
        /// </summary>
        private Character FindNearestPlayer()
        {
            var players = FindObjectsOfType<Character>();
            Character nearest = null;
            float minDist = float.MaxValue;

            foreach (var p in players)
            {
                float dist = Vector2.Distance(transform.position, p.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = p;
                }
            }
            return nearest;
        }

        /// <summary>
        /// Fallback: find a player Transform when Character component isn't present
        /// </summary>
        private Transform FindPlayerTransformFallback()
        {
            // Try tag "Player"
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) return tagged.transform;

            // Try common name
            var byName = GameObject.Find("Player");
            if (byName != null) return byName.transform;

            // Try PlayerBotAI
            var bot = FindFirstObjectByType<PlayerBotAI>();
            if (bot != null) return bot.transform;

            return null;
        }

        /// <summary>
        /// Helper: get player position from Character or fallback Transform
        /// </summary>
        private bool TryGetPlayerPosition(out Vector3 playerPos)
        {
            if (playerCharacter != null)
            {
                playerPos = playerCharacter.transform.position;
                return true;
            }
            if (playerTransform != null)
            {
                playerPos = playerTransform.position;
                return true;
            }
            playerPos = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Update nearby allies periodically (called from FixedUpdate)
        /// </summary>
        private void FixedUpdate()
        {
            // Update allies every ~2.5 seconds (50 physics frames at 20Hz)
            if (StepCount % 50 == 0)
            {
                UpdateNearbyAllies();
            }
        }

        /// <summary>
        /// LateUpdate enforces bounds AFTER all physics/movement
        /// </summary>
        private void LateUpdate()
        {
            KeepInBounds();
        }

        /// <summary>
        /// Clamp position to arena bounds
        /// </summary>
        private Vector2 ClampToArena(Vector2 pos)
        {
            float minX = arenaCenter.x - (arenaHalfSize - wallMargin);
            float maxX = arenaCenter.x + (arenaHalfSize - wallMargin);
            float minY = arenaCenter.y - (arenaHalfSize - wallMargin);
            float maxY = arenaCenter.y + (arenaHalfSize - wallMargin);
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            return pos;
        }

        /// <summary>
        /// Keep agent within arena bounds during gameplay
        /// AGGRESSIVE: Zero velocity when out of bounds
        /// </summary>
        private void KeepInBounds()
        {
            Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
            Vector2 clampedPos = ClampToArena(pos);

            // If out of bounds, snap back and zero velocity
            if ((pos - clampedPos).sqrMagnitude > 0.001f)
            {
                if (rb != null)
                {
                    rb.position = clampedPos;
                }
                else
                {
                    transform.position = clampedPos;
                }

                // Stop movement when hitting boundary
                if (rb != null)
                {
                    Vector2 vel = rb.linearVelocity;

                    // Zero out velocity in direction of boundary
                    if (Mathf.Abs(pos.x - clampedPos.x) > 0.001f)
                    {
                        vel.x = 0;
                    }
                    if (Mathf.Abs(pos.y - clampedPos.y) > 0.001f)
                    {
                        vel.y = 0;
                    }

                    rb.linearVelocity = vel;
                }

                // Penalty for going out of bounds
                AddReward(-0.1f);
            }
        }

        /// <summary>
        /// Set arena bounds from external source (spawner)
        /// </summary>
        public void SetArenaBounds(Vector2 center, float halfSize)
        {
            arenaCenter = center;
            arenaHalfSize = halfSize;
        }

        /// <summary>
        /// Optional: Heuristic for manual testing (keyboard control)
        /// Press keys to test different actions before training
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discreteActions = actionsOut.DiscreteActions;

            // Default: Aggressive (0)
            discreteActions[0] = 0;

            // Keyboard controls for testing
            if (Input.GetKey(KeyCode.Alpha1)) discreteActions[0] = 0; // Aggressive
            if (Input.GetKey(KeyCode.Alpha2)) discreteActions[0] = 1; // Maintain Distance
            if (Input.GetKey(KeyCode.Alpha3)) discreteActions[0] = 2; // Retreat
            if (Input.GetKey(KeyCode.Alpha4)) discreteActions[0] = 3; // Flank
            if (Input.GetKey(KeyCode.Alpha5)) discreteActions[0] = 4; // Wait
        }

        /// <summary>
        /// Debug visualization
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw optimal range
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, optimalRange);

            // Draw max detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, maxDetectionRange);

            // Draw ally check radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, allyCheckRadius);

            // Draw line to player
            if (playerCharacter != null || playerTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, (playerCharacter != null ? playerCharacter.transform.position : playerTransform.position));
            }

            // Draw connections to nearby allies
            Gizmos.color = Color.blue;
            foreach (var ally in nearbyAllies)
            {
                if (ally != null)
                    Gizmos.DrawLine(transform.position, ally.transform.position);
            }
        }

        /// <summary>
        /// Integration with existing Monster component
        /// Call this to link RLMonsterAgent with a Monster instance
        /// </summary>
        public void LinkWithMonster(Monster monster)
        {
            baseMonster = monster;

            // Sync stats from monster blueprint
            if (monster != null)
            {
                maxHP = monster.HP;
                currentHP = maxHP;
                // moveSpeed would come from monster.monsterBlueprint.movespeed
                // Subscribe to damage events to keep RL HP in sync (for game integration)
                monster.OnDamaged.RemoveListener(OnTakeDamage);
                monster.OnDamaged.AddListener(OnTakeDamage);
            }

            Debug.Log($"[RLMonsterAgent] Linked with Monster component");
        }

        /// <summary>
        /// Set entity manager reference (for spawn/despawn integration)
        /// </summary>
        public void SetEntityManager(EntityManager manager)
        {
            entityManager = manager;
        }
    }
}
