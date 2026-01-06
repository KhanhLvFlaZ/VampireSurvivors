using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// RL Environment implementation for state observation and reward calculation
    /// Integrates with SpatialHashGrid for spatial awareness and provides player behavior analysis
    /// </summary>
    public class RLEnvironment : MonoBehaviour
    {
        [Header("Environment Configuration")]
        [SerializeField] private float observationRadius = 10f;
        [SerializeField] private int maxNearbyMonsters = 5;
        [SerializeField] private int maxNearbyObstacles = 3;
        [SerializeField] private float episodeTimeLimit = 300f; // 5 minutes

        [Header("Player Behavior Analysis")]
        [SerializeField] private float behaviorAnalysisWindow = 30f; // 30 seconds
        [SerializeField] private int maxBehaviorSamples = 100;

        [Header("Dependencies")]
        private EntityManager entityManager;
        private Character playerCharacter;
        private SpatialHashGrid spatialGrid;
        private IRewardCalculator rewardCalculator;

        // Player behavior tracking
        private Queue<Vector2> playerPositionHistory;
        private Queue<float> playerHealthHistory;
        private Queue<float> timestampHistory;
        private Vector2 lastPlayerPosition;
        private float lastObservationTime;

        // Environment state
        private Dictionary<Monster, float> monsterEpisodeStartTimes;
        private Dictionary<Monster, Vector2> monsterLastPositions;
        private Dictionary<Monster, float> monsterLastAttackTimes;

        // Team damage tracking (per episode)
        private float episodeTeamDamageDealt;
        private float episodeTeamDamageTaken;
        private Dictionary<Character, float> characterLastHealth;
        private Dictionary<Monster, float> monsterLastHealth;

        // Encoders
        private StateEncoder stateEncoder;

        public float ObservationRadius => observationRadius;
        public EntityManager EntityManager => entityManager;
        public Character PlayerCharacter => playerCharacter;

        private void Awake()
        {
            // Initialize collections
            playerPositionHistory = new Queue<Vector2>();
            playerHealthHistory = new Queue<float>();
            timestampHistory = new Queue<float>();
            monsterEpisodeStartTimes = new Dictionary<Monster, float>();
            monsterLastPositions = new Dictionary<Monster, Vector2>();
            monsterLastAttackTimes = new Dictionary<Monster, float>();
            characterLastHealth = new Dictionary<Character, float>();
            monsterLastHealth = new Dictionary<Monster, float>();

            episodeTeamDamageDealt = 0f;
            episodeTeamDamageTaken = 0f;

            stateEncoder = new StateEncoder();
        }

        /// <summary>
        /// Initialize the RL environment with required dependencies
        /// </summary>
        public void Initialize(EntityManager entityManager, Character playerCharacter, IRewardCalculator rewardCalculator)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            this.spatialGrid = entityManager.Grid;
            this.rewardCalculator = rewardCalculator;

            lastPlayerPosition = playerCharacter.transform.position;
            lastObservationTime = Time.time;

            // Start behavior tracking
            InvokeRepeating(nameof(UpdatePlayerBehaviorTracking), 0f, 0.1f);
        }

        private void Update()
        {
            // Update environment state periodically
            UpdateEnvironmentState();
        }

        /// <summary>
        /// Get current state observations for a monster
        /// </summary>
        public float[] GetState(Monster monster)
        {
            var gameState = BuildGameState(monster);
            return stateEncoder.EncodeState(gameState);
        }

        /// <summary>
        /// Build rich RLGameState including co-op teammates and nearby entities.
        /// </summary>
        public RLGameState BuildGameState(Monster monster, int agentId = 0)
        {
            var state = RLGameState.CreateDefault();

            if (monster == null)
                return state;

            // Select primary player (first active), fallback to injected playerCharacter.
            var players = GetActiveCharacters();
            Character mainPlayer = players.Count > 0 ? players[0] : playerCharacter;

            // Agent identity (which player this observation is for)
            state.agentId = agentId;

            // Player block
            if (mainPlayer != null)
            {
                state.playerPosition = mainPlayer.transform.position;
                state.playerVelocity = mainPlayer.Velocity;
                state.playerHealth = mainPlayer.HP;
            }

            // Teammates (excluding main)
            var teammateList = new List<TeammateInfo>();
            if (players.Count > 1)
            {
                for (int i = 1; i < players.Count && teammateList.Count < 3; i++)
                {
                    var p = players[i];
                    teammateList.Add(new TeammateInfo
                    {
                        position = p.transform.position,
                        velocity = p.Velocity,
                        health = p.HP,
                        isDowned = false // no downed state in current character; assume alive
                    });
                }
                state.teammates = teammateList.ToArray();
            }

            // Teammate count for masking
            state.totalTeammateCount = teammateList.Count;

            // Team aggregates
            if (teammateList.Count > 0 && monster != null)
            {
                Vector2 focusTarget = monster.transform.position; // Boss as focus target
                state.teamFocusTarget = focusTarget;

                float totalDistance = 0f;
                foreach (var teammate in teammateList)
                {
                    totalDistance += Vector2.Distance(teammate.position, focusTarget);
                }
                state.avgTeammateDistance = totalDistance / teammateList.Count;
            }

            // Monster block
            state.monsterPosition = monster.transform.position;
            state.monsterHealth = monster.HP;
            state.currentAction = 0;
            state.timeSinceLastAction = monsterLastAttackTimes.TryGetValue(monster, out var lastAtk)
                ? Time.time - lastAtk
                : 0f;

            // Environment
            var nearbyMonsters = GetNearbyEntities(monster);
            state.nearbyMonsters = nearbyMonsters
                .Take(maxNearbyMonsters)
                .Select(m => new NearbyMonster
                {
                    position = m.transform.position,
                    monsterType = MonsterType.Melee, // TODO: read from blueprint
                    health = m.HP,
                    currentAction = 0
                })
                .Concat(Enumerable.Repeat(NearbyMonster.CreateEmpty(), Mathf.Max(0, maxNearbyMonsters - nearbyMonsters.Count)))
                .ToArray();

            state.nearbyCollectibles = CollectibleInfoArrayEmpty(10);

            // Temporal
            state.timeAlive = monsterEpisodeStartTimes.TryGetValue(monster, out var start)
                ? Time.time - start
                : 0f;
            state.timeSincePlayerDamage = float.MaxValue;

            // Team damage aggregates (tracked across episode)
            state.teamDamageDealt = episodeTeamDamageDealt;
            state.teamDamageTaken = episodeTeamDamageTaken;

            return state;
        }

        /// <summary>
        /// Update environment state
        /// </summary>
        private void UpdateEnvironmentState()
        {
            // Update environment observations
            if (entityManager?.LivingMonsters == null) return;

            // Track team damage
            UpdateTeamDamageTracking();

            // Track monster states for learning
            foreach (var monster in entityManager.LivingMonsters)
            {
                if (monster != null)
                {
                    // Update tracking dictionaries for reward calculation
                    if (!monsterLastPositions.ContainsKey(monster))
                    {
                        monsterLastPositions[monster] = monster.transform.position;
                    }

                    if (!monsterEpisodeStartTimes.ContainsKey(monster))
                    {
                        monsterEpisodeStartTimes[monster] = Time.time;
                    }
                }
            }
        }

        /// <summary>
        /// Track team damage dealt and taken across episode
        /// </summary>
        private void UpdateTeamDamageTracking()
        {
            var players = GetActiveCharacters();

            // Track damage taken (health reduction)
            foreach (var character in players)
            {
                if (!characterLastHealth.ContainsKey(character))
                {
                    characterLastHealth[character] = character.HP;
                }

                float healthDelta = characterLastHealth[character] - character.HP;
                if (healthDelta > 0) // Health decreased = damage taken
                {
                    episodeTeamDamageTaken += healthDelta;
                }

                characterLastHealth[character] = character.HP;
            }

            // Track damage dealt (monster health reduction)
            // Note: This is a simplified approach. For accurate tracking, consider
            // subscribing to monster damage events or maintaining per-monster health tracking.
            if (entityManager?.LivingMonsters != null)
            {
                foreach (var monster in entityManager.LivingMonsters)
                {
                    if (!monsterLastHealth.ContainsKey(monster))
                    {
                        monsterLastHealth[monster] = monster.HP;
                    }

                    float healthDelta = monsterLastHealth[monster] - monster.HP;
                    if (healthDelta > 0) // Monster health decreased
                    {
                        episodeTeamDamageDealt += healthDelta;
                    }

                    monsterLastHealth[monster] = monster.HP;
                }

                // Clean up dead monsters from tracking
                var deadMonsters = monsterLastHealth.Keys.Where(m => m == null || m.HP <= 0).ToList();
                foreach (var dead in deadMonsters)
                {
                    monsterLastHealth.Remove(dead);
                }
            }
        }

        private List<Character> GetActiveCharacters()
        {
            var list = new List<Character>();
            if (playerCharacter != null)
            {
                list.Add(playerCharacter);
            }
            return list;
        }

        private CollectibleInfo[] CollectibleInfoArrayEmpty(int count)
        {
            var arr = new CollectibleInfo[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = CollectibleInfo.CreateEmpty();
            }
            return arr;
        }

        /// <summary>
        /// Get nearby entities using spatial hash grid
        /// </summary>
        private List<Monster> GetNearbyEntities(Monster monster)
        {
            if (spatialGrid == null) return new List<Monster>();

            var nearbyClients = spatialGrid.FindNearbyInRadius(monster.transform.position, observationRadius);
            var nearbyMonsters = new List<Monster>();

            foreach (var client in nearbyClients)
            {
                if (client is Monster nearbyMonster && nearbyMonster != monster)
                {
                    nearbyMonsters.Add(nearbyMonster);
                    if (nearbyMonsters.Count >= maxNearbyMonsters)
                        break;
                }
            }

            return nearbyMonsters;
        }

        /// <summary>
        /// Get positions of nearby monsters for state observation
        /// </summary>
        private Vector2[] GetNearbyMonsterPositions(Monster monster, List<Monster> nearbyMonsters)
        {
            var positions = new List<Vector2>();
            Vector2 monsterPos = monster.transform.position;

            // Sort by distance and take closest ones
            var sortedMonsters = nearbyMonsters
                .OrderBy(m => Vector2.Distance(m.transform.position, monsterPos))
                .Take(2) // Limit to 2 as per state array size
                .ToList();

            foreach (var nearbyMonster in sortedMonsters)
            {
                // Store relative position for better learning
                Vector2 relativePos = (Vector2)nearbyMonster.transform.position - monsterPos;
                positions.Add(relativePos);
            }

            // Pad with zeros if needed
            while (positions.Count < 2)
            {
                positions.Add(Vector2.zero);
            }

            return positions.ToArray();
        }

        /// <summary>
        /// Get nearby obstacles (simplified - could be expanded with actual obstacle detection)
        /// </summary>
        private Vector2[] GetNearbyObstacles(Monster monster)
        {
            // For now, return screen boundaries as obstacles
            var obstacles = new List<Vector2>();
            Vector2 monsterPos = monster.transform.position;
            Vector2 playerPos = playerCharacter.transform.position;

            // Calculate screen boundaries relative to player
            float screenWidth = Camera.main.orthographicSize * Camera.main.aspect * 2;
            float screenHeight = Camera.main.orthographicSize * 2;

            Vector2 screenCenter = playerPos;
            Vector2 leftBoundary = screenCenter + Vector2.left * (screenWidth / 2);
            Vector2 rightBoundary = screenCenter + Vector2.right * (screenWidth / 2);
            Vector2 topBoundary = screenCenter + Vector2.up * (screenHeight / 2);
            Vector2 bottomBoundary = screenCenter + Vector2.down * (screenHeight / 2);

            // Find closest boundary
            float minDistance = float.MaxValue;
            Vector2 closestObstacle = Vector2.zero;

            Vector2[] boundaries = { leftBoundary, rightBoundary, topBoundary, bottomBoundary };
            foreach (var boundary in boundaries)
            {
                float distance = Vector2.Distance(monsterPos, boundary);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestObstacle = boundary - monsterPos; // Relative position
                }
            }

            obstacles.Add(closestObstacle);
            return obstacles.ToArray();
        }

        /// <summary>
        /// Calculate reward for an action taken by a monster
        /// </summary>
        public float CalculateReward(Monster monster, int action, float[] previousState)
        {
            if (rewardCalculator == null)
            {
                Debug.LogWarning("RewardCalculator not set. Using default reward calculation.");
                return CalculateDefaultReward(monster, action, previousState);
            }

            return rewardCalculator.CalculateReward(monster, action, previousState);
        }

        /// <summary>
        /// Default reward calculation when no reward calculator is provided
        /// </summary>
        private float CalculateDefaultReward(Monster monster, int action, float[] previousState)
        {
            float reward = 0f;

            if (monster == null || playerCharacter == null) return reward;

            // Distance-based reward (encourage getting closer to player)
            float currentDistance = Vector2.Distance(monster.transform.position, playerCharacter.transform.position);
            if (previousState != null && previousState.Length >= 12)
            {
                Vector2 prevPlayerPos = new Vector2(previousState[0], previousState[1]);
                Vector2 prevMonsterPos = new Vector2(previousState[5], previousState[6]);
                float previousDistance = Vector2.Distance(prevMonsterPos, prevPlayerPos);

                if (currentDistance < previousDistance)
                    reward += 1f; // Reward for getting closer
                else
                    reward -= 0.5f; // Penalty for moving away
            }

            // Survival reward
            reward += 0.1f;

            // Coordination reward (simplified)
            var nearbyMonsters = GetNearbyEntities(monster);
            if (nearbyMonsters.Count > 0)
                reward += 0.2f * nearbyMonsters.Count;

            return reward;
        }

        /// <summary>
        /// Check if the episode is complete for a monster
        /// </summary>
        public bool IsEpisodeComplete(Monster monster)
        {
            if (monster == null) return true;

            // Episode complete if monster is dead
            if (monster.HP <= 0) return true;

            // Episode complete if player is dead
            if (playerCharacter == null) return true;

            // Episode complete if time limit reached
            if (monsterEpisodeStartTimes.ContainsKey(monster))
            {
                float episodeTime = Time.time - monsterEpisodeStartTimes[monster];
                if (episodeTime > episodeTimeLimit) return true;
            }

            return false;
        }

        /// <summary>
        /// Reset the environment to initial state
        /// </summary>
        public void ResetEnvironment()
        {
            // Clear tracking data
            monsterEpisodeStartTimes.Clear();
            monsterLastPositions.Clear();
            monsterLastAttackTimes.Clear();

            // Reset player behavior tracking
            playerPositionHistory.Clear();
            playerHealthHistory.Clear();
            timestampHistory.Clear();

            if (playerCharacter != null)
            {
                lastPlayerPosition = playerCharacter.transform.position;
            }
            lastObservationTime = Time.time;
        }

        /// <summary>
        /// Register a new monster for episode tracking
        /// </summary>
        public void RegisterMonster(Monster monster)
        {
            if (monster != null && !monsterEpisodeStartTimes.ContainsKey(monster))
            {
                monsterEpisodeStartTimes[monster] = Time.time;
                monsterLastPositions[monster] = monster.transform.position;
                monsterLastAttackTimes[monster] = Time.time;
            }
        }

        /// <summary>
        /// Unregister a monster from episode tracking
        /// </summary>
        public void UnregisterMonster(Monster monster)
        {
            if (monster != null)
            {
                monsterEpisodeStartTimes.Remove(monster);
                monsterLastPositions.Remove(monster);
                monsterLastAttackTimes.Remove(monster);
            }
        }

        /// <summary>
        /// Update player behavior tracking for adaptation analysis
        /// </summary>
        private void UpdatePlayerBehaviorTracking()
        {
            if (playerCharacter == null) return;

            float currentTime = Time.time;
            Vector2 currentPosition = playerCharacter.transform.position;

            // Add current data
            playerPositionHistory.Enqueue(currentPosition);
            playerHealthHistory.Enqueue(GetNormalizedPlayerHealth());
            timestampHistory.Enqueue(currentTime);

            // Remove old data outside the analysis window
            while (timestampHistory.Count > 0 &&
                   currentTime - timestampHistory.Peek() > behaviorAnalysisWindow)
            {
                playerPositionHistory.Dequeue();
                playerHealthHistory.Dequeue();
                timestampHistory.Dequeue();
            }

            // Limit sample count
            while (playerPositionHistory.Count > maxBehaviorSamples)
            {
                playerPositionHistory.Dequeue();
                playerHealthHistory.Dequeue();
                timestampHistory.Dequeue();
            }

            lastPlayerPosition = currentPosition;
        }

        /// <summary>
        /// Analyze player movement patterns for adaptation
        /// </summary>
        public PlayerBehaviorPattern AnalyzePlayerBehavior()
        {
            if (playerPositionHistory.Count < 10)
                return new PlayerBehaviorPattern(); // Not enough data

            var positions = playerPositionHistory.ToArray();
            var pattern = new PlayerBehaviorPattern();

            // Calculate average movement speed
            float totalDistance = 0f;
            for (int i = 1; i < positions.Length; i++)
            {
                totalDistance += Vector2.Distance(positions[i], positions[i - 1]);
            }
            pattern.averageSpeed = totalDistance / (positions.Length - 1);

            // Calculate movement direction preference
            Vector2 totalMovement = Vector2.zero;
            for (int i = 1; i < positions.Length; i++)
            {
                totalMovement += positions[i] - positions[i - 1];
            }
            pattern.preferredDirection = totalMovement.normalized;

            // Calculate movement predictability (variance in direction)
            float directionVariance = 0f;
            for (int i = 2; i < positions.Length; i++)
            {
                Vector2 dir1 = (positions[i - 1] - positions[i - 2]).normalized;
                Vector2 dir2 = (positions[i] - positions[i - 1]).normalized;
                float angle = Vector2.Angle(dir1, dir2);
                directionVariance += angle * angle;
            }
            pattern.predictability = 1f - (directionVariance / (positions.Length - 2)) / 180f;
            pattern.predictability = Mathf.Clamp01(pattern.predictability);

            return pattern;
        }

        // Helper methods
        private float GetNormalizedPlayerHealth()
        {
            if (playerCharacter?.Blueprint == null) return 1f;
            return Mathf.Clamp01(playerCharacter.HP / playerCharacter.Blueprint.hp);
        }

        private float GetNormalizedMonsterHealth(Monster monster)
        {
            // Assuming monster has a way to get max health from blueprint
            // This might need adjustment based on actual Monster implementation
            return Mathf.Clamp01(monster.HP / 100f); // Placeholder - adjust based on actual max health
        }

        private Vector2 GetMonsterVelocity(Monster monster)
        {
            var rb = monster.GetComponent<Rigidbody2D>();
            return rb != null ? rb.linearVelocity : Vector2.zero;
        }

        private float GetTimeSinceLastAttack(Monster monster)
        {
            if (monsterLastAttackTimes.ContainsKey(monster))
            {
                return Time.time - monsterLastAttackTimes[monster];
            }
            return 0f;
        }

        /// <summary>
        /// Update the last attack time for a monster
        /// </summary>
        public void RecordMonsterAttack(Monster monster)
        {
            if (monster != null)
            {
                monsterLastAttackTimes[monster] = Time.time;
            }
        }

        /// <summary>
        /// Reset episode state, clearing damage tracking and episode timers
        /// </summary>
        public void ResetEpisode()
        {
            episodeTeamDamageDealt = 0f;
            episodeTeamDamageTaken = 0f;
            characterLastHealth.Clear();
            monsterLastHealth.Clear();
            monsterEpisodeStartTimes.Clear();
            monsterLastPositions.Clear();
            monsterLastAttackTimes.Clear();

            Debug.Log("RL Episode reset - damage tracking cleared");
        }

        private void OnDestroy()
        {
            // Clean up
            CancelInvoke();
        }
    }

    /// <summary>
    /// Data structure for player behavior analysis
    /// </summary>
    [System.Serializable]
    public struct PlayerBehaviorPattern
    {
        public float averageSpeed;
        public Vector2 preferredDirection;
        public float predictability; // 0 = unpredictable, 1 = very predictable

        public bool IsValid => averageSpeed >= 0 && predictability >= 0 && predictability <= 1;
    }
}