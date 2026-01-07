using UnityEngine;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// Spawns RLMonsterAgent instances for training scenes
    /// Simple manual spawner for controlled training environments
    /// </summary>
    public class RLMonsterTrainingSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private Transform player;
        [SerializeField] private int monsterCount = 12;
        [SerializeField] private float spawnRadius = 10f; // ensure inside typical 20x20 arena
        [SerializeField] private Vector2 arenaCenter = Vector2.zero;
        [SerializeField] private float arenaHalfSize = 12f; // matches boundary positions ±12
        [SerializeField] private float wallMargin = 1.0f;   // keep distance from walls
        [SerializeField] private float minDistanceFromPlayer = 5f;

        [Header("Debug")]
        [SerializeField] private bool showSpawnGizmos = false;

        private void Start()
        {
            SpawnMonsters();
        }

        /// <summary>
        /// Spawn all monsters at random positions
        /// </summary>
        public void SpawnMonsters()
        {
            if (monsterPrefab == null)
            {
                Debug.LogError("[TrainingSpawner] Monster prefab is not assigned!");
                return;
            }

            if (player == null)
            {
                Debug.LogWarning("[TrainingSpawner] Player reference not set, finding in scene...");

                // Try multiple methods to find player
                var character = FindFirstObjectByType<Character>();
                if (character != null)
                {
                    player = character.transform;
                    Debug.Log($"[TrainingSpawner] Found Character: {player.name}");
                }
                else
                {
                    // Try finding by name
                    var playerObj = GameObject.Find("Player");
                    if (playerObj != null)
                    {
                        player = playerObj.transform;
                        Debug.Log($"[TrainingSpawner] Found Player by name: {player.name}");
                    }
                    else
                    {
                        // Try finding PlayerBotAI
                        var bot = FindFirstObjectByType<PlayerBotAI>();
                        if (bot != null)
                        {
                            player = bot.transform;
                            Debug.Log($"[TrainingSpawner] Found PlayerBotAI: {player.name}");
                        }
                        else
                        {
                            Debug.LogError("[TrainingSpawner] Could not find player! Make sure scene has Character, Player object, or PlayerBotAI component.");
                        }
                    }
                }
            }

            // Auto-detect arena bounds from boundary objects if present
            DetectArenaBounds();

            // Center player safely inside arena and sync bot bounds
            if (player != null)
            {
                Vector2 safeCenter = ClampInsideArena(arenaCenter);
                player.position = safeCenter;

                // Sync PlayerBotAI arena settings if present
                var bot = player.GetComponent<PlayerBotAI>();
                if (bot != null)
                {
                    bot.SetArena(arenaCenter, Mathf.Max(0.1f, arenaHalfSize - wallMargin));
                }

                // Ensure hard bounds enforcement on player
                var playerEnforcer = player.GetComponent<WorldBoundsEnforcer>();
                if (playerEnforcer == null)
                {
                    playerEnforcer = player.gameObject.AddComponent<WorldBoundsEnforcer>();
                }
                playerEnforcer.SetBounds(arenaCenter, arenaHalfSize, wallMargin);
            }

            Debug.Log($"[TrainingSpawner] Spawning {monsterCount} monsters...");

            for (int i = 0; i < monsterCount; i++)
            {
                Vector2 spawnPos = GetRandomSpawnPosition();

                // Spawn from prefab
                GameObject monster = Instantiate(
                    monsterPrefab,
                    spawnPos,
                    Quaternion.identity
                );

                // Rename for clarity
                monster.name = $"RLMonster_{i}";

                // Initialize if RLMonsterAgent exists
                var agent = monster.GetComponent<RLMonsterAgent>();
                if (agent != null && player != null)
                {
                    // Sync agent's arena bounds
                    agent.SetArenaBounds(arenaCenter, arenaHalfSize);
                    // Agent will find player automatically via FindNearestPlayer()
                }

                // Add hard bounds enforcer to monsters too
                var enforcer = monster.GetComponent<WorldBoundsEnforcer>();
                if (enforcer == null)
                {
                    enforcer = monster.AddComponent<WorldBoundsEnforcer>();
                }
                enforcer.SetBounds(arenaCenter, arenaHalfSize, wallMargin);

                Debug.Log($"[TrainingSpawner] Spawned monster {i} at {spawnPos}");
            }

            Debug.Log($"[TrainingSpawner] Successfully spawned {monsterCount} monsters!");
        }

        /// <summary>
        /// Get random spawn position that's not too close to player
        /// </summary>
        private Vector2 GetRandomSpawnPosition()
        {
            Vector2 randomPos;
            int attempts = 0;
            int maxAttempts = 10;

            do
            {
                randomPos = Random.insideUnitCircle * spawnRadius;
                randomPos = ClampInsideArena(randomPos);
                attempts++;

                // If no player or max attempts reached, just use this position
                if (player == null || attempts >= maxAttempts)
                    break;

                // Check distance from player
                float distToPlayer = Vector2.Distance(randomPos, (Vector2)player.position);
                if (distToPlayer >= minDistanceFromPlayer)
                    break;

            } while (attempts < maxAttempts);

            return randomPos;
        }

        /// <summary>
        /// Clamp a position inside the arena bounds with a wall margin
        /// </summary>
        private Vector2 ClampInsideArena(Vector2 pos)
        {
            float minX = arenaCenter.x - (arenaHalfSize - wallMargin);
            float maxX = arenaCenter.x + (arenaHalfSize - wallMargin);
            float minY = arenaCenter.y - (arenaHalfSize - wallMargin);
            float maxY = arenaCenter.y + (arenaHalfSize - wallMargin);
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            return pos;
        }

        // Try to auto-detect arena using named boundary objects
        private void DetectArenaBounds()
        {
            var left = GameObject.Find("Boundary_Left");
            var right = GameObject.Find("Boundary_Right");
            var top = GameObject.Find("Boundary_Top");
            var bottom = GameObject.Find("Boundary_Bottom");
            if (left != null && right != null && top != null && bottom != null)
            {
                float centerX = (left.transform.position.x + right.transform.position.x) * 0.5f;
                float centerY = (top.transform.position.y + bottom.transform.position.y) * 0.5f;
                arenaCenter = new Vector2(centerX, centerY);
                float halfX = Mathf.Abs(right.transform.position.x - left.transform.position.x) * 0.5f;
                float halfY = Mathf.Abs(top.transform.position.y - bottom.transform.position.y) * 0.5f;
                arenaHalfSize = Mathf.Min(halfX, halfY);
            }
        }

        /// <summary>
        /// Clear all spawned monsters (useful for reset)
        /// </summary>
        public void ClearMonsters()
        {
            var monsters = FindObjectsOfType<RLMonsterAgent>();
            foreach (var monster in monsters)
            {
                Destroy(monster.gameObject);
            }
            Debug.Log($"[TrainingSpawner] Cleared {monsters.Length} monsters");
        }

        private void OnDrawGizmos()
        {
            if (!showSpawnGizmos) return;

            // Draw spawn radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);

            // Draw min distance from player
            if (player != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(player.position, minDistanceFromPlayer);
            }
        }
    }
}
