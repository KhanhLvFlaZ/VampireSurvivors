using UnityEngine;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Debug tool to track and visualize entity positions and physics issues
    /// Attach to any GameObject to monitor training scene
    /// </summary>
    public class ArenaDebugger : MonoBehaviour
    {
        [Header("Arena Settings")]
        [SerializeField] private Vector2 arenaCenter = Vector2.zero;
        [SerializeField] private float arenaHalfSize = 12f;
        [SerializeField] private bool autoDetectBounds = true;

        [Header("Debug Options")]
        [SerializeField] private bool logOutOfBoundsEntities = true;
        [SerializeField] private bool forceSnapBackInside = true;
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool disableMonsterCollisions = false;
        [SerializeField] private float checkInterval = 0.5f;

        private float nextCheckTime;
        private List<GameObject> outOfBoundsEntities = new List<GameObject>();

        void Start()
        {
            if (autoDetectBounds)
            {
                DetectArenaBounds();
            }

            if (disableMonsterCollisions)
            {
                DisableMonsterToMonsterCollisions();
            }

            Debug.Log($"[ArenaDebugger] Monitoring arena at {arenaCenter} with half-size {arenaHalfSize}");
        }

        void Update()
        {
            if (Time.time >= nextCheckTime)
            {
                CheckEntities();
                nextCheckTime = Time.time + checkInterval;
            }
        }

        void LateUpdate()
        {
            // Force snap in LateUpdate to override physics
            if (forceSnapBackInside)
            {
                ForceSnapAllInside();
            }
        }

        void DetectArenaBounds()
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

                Debug.Log($"[ArenaDebugger] Auto-detected arena: center={arenaCenter}, halfSize={arenaHalfSize}");
            }
            else
            {
                Debug.LogWarning("[ArenaDebugger] Could not find all boundary objects!");
            }
        }

        void CheckEntities()
        {
            outOfBoundsEntities.Clear();

            // Check monsters
            var monsters = FindObjectsOfType<RLMonsterAgent>();
            foreach (var monster in monsters)
            {
                if (!IsInsideArena(monster.transform.position))
                {
                    outOfBoundsEntities.Add(monster.gameObject);
                    if (logOutOfBoundsEntities)
                    {
                        var rb = monster.GetComponent<Rigidbody2D>();
                        Debug.LogWarning($"[ArenaDebugger] Monster '{monster.name}' OUT OF BOUNDS at {monster.transform.position} | Velocity: {(rb != null ? rb.linearVelocity : Vector2.zero)}");
                    }
                }
            }

            // Check player
            var player = FindFirstObjectByType<Character>();
            if (player != null && !IsInsideArena(player.transform.position))
            {
                outOfBoundsEntities.Add(player.gameObject);
                if (logOutOfBoundsEntities)
                {
                    var rb = player.GetComponent<Rigidbody2D>();
                    Debug.LogWarning($"[ArenaDebugger] Player OUT OF BOUNDS at {player.transform.position} | Velocity: {(rb != null ? rb.linearVelocity : Vector2.zero)}");
                }
            }

            if (outOfBoundsEntities.Count > 0)
            {
                Debug.LogWarning($"[ArenaDebugger] {outOfBoundsEntities.Count} entities are out of bounds!");
            }
        }

        void ForceSnapAllInside()
        {
            // Force monsters
            var monsters = FindObjectsOfType<RLMonsterAgent>();
            foreach (var monster in monsters)
            {
                Vector2 pos = monster.transform.position;
                Vector2 clamped = ClampToArena(pos);
                
                if ((pos - clamped).sqrMagnitude > 0.01f)
                {
                    monster.transform.position = clamped;
                    
                    // Zero out velocity when hitting boundary
                    var rb = monster.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        Vector2 vel = rb.linearVelocity;
                        if (Mathf.Abs(pos.x - clamped.x) > 0.01f) vel.x = 0;
                        if (Mathf.Abs(pos.y - clamped.y) > 0.01f) vel.y = 0;
                        rb.linearVelocity = vel;
                    }
                }
            }

            // Force player
            var player = FindFirstObjectByType<Character>();
            if (player != null)
            {
                Vector2 pos = player.transform.position;
                Vector2 clamped = ClampToArena(pos);
                
                if ((pos - clamped).sqrMagnitude > 0.01f)
                {
                    player.transform.position = clamped;
                    
                    var rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        Vector2 vel = rb.linearVelocity;
                        if (Mathf.Abs(pos.x - clamped.x) > 0.01f) vel.x = 0;
                        if (Mathf.Abs(pos.y - clamped.y) > 0.01f) vel.y = 0;
                        rb.linearVelocity = vel;
                    }
                }
            }
        }

        void DisableMonsterToMonsterCollisions()
        {
            var monsters = FindObjectsOfType<RLMonsterAgent>();
            for (int i = 0; i < monsters.Length; i++)
            {
                for (int j = i + 1; j < monsters.Length; j++)
                {
                    var col1 = monsters[i].GetComponent<Collider2D>();
                    var col2 = monsters[j].GetComponent<Collider2D>();
                    
                    if (col1 != null && col2 != null)
                    {
                        Physics2D.IgnoreCollision(col1, col2, true);
                    }
                }
            }
            Debug.Log($"[ArenaDebugger] Disabled collisions between {monsters.Length} monsters");
        }

        bool IsInsideArena(Vector2 pos)
        {
            float minX = arenaCenter.x - arenaHalfSize;
            float maxX = arenaCenter.x + arenaHalfSize;
            float minY = arenaCenter.y - arenaHalfSize;
            float maxY = arenaCenter.y + arenaHalfSize;

            return pos.x >= minX && pos.x <= maxX && pos.y >= minY && pos.y <= maxY;
        }

        Vector2 ClampToArena(Vector2 pos)
        {
            float minX = arenaCenter.x - arenaHalfSize;
            float maxX = arenaCenter.x + arenaHalfSize;
            float minY = arenaCenter.y - arenaHalfSize;
            float maxY = arenaCenter.y + arenaHalfSize;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            return pos;
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Draw arena bounds
            Gizmos.color = Color.green;
            Vector3 center = new Vector3(arenaCenter.x, arenaCenter.y, 0);
            Vector3 size = new Vector3(arenaHalfSize * 2, arenaHalfSize * 2, 0.1f);
            Gizmos.DrawWireCube(center, size);

            // Draw out of bounds entities
            if (Application.isPlaying && outOfBoundsEntities.Count > 0)
            {
                Gizmos.color = Color.red;
                foreach (var entity in outOfBoundsEntities)
                {
                    if (entity != null)
                    {
                        Gizmos.DrawWireSphere(entity.transform.position, 1f);
                        Gizmos.DrawLine(entity.transform.position, ClampToArena(entity.transform.position));
                    }
                }
            }
        }

        // Public API for external control
        public void SetArenaBounds(Vector2 center, float halfSize)
        {
            arenaCenter = center;
            arenaHalfSize = halfSize;
        }

        public void EnableMonsterCollisions()
        {
            var monsters = FindObjectsOfType<RLMonsterAgent>();
            for (int i = 0; i < monsters.Length; i++)
            {
                for (int j = i + 1; j < monsters.Length; j++)
                {
                    var col1 = monsters[i].GetComponent<Collider2D>();
                    var col2 = monsters[j].GetComponent<Collider2D>();
                    
                    if (col1 != null && col2 != null)
                    {
                        Physics2D.IgnoreCollision(col1, col2, false);
                    }
                }
            }
        }
    }
}
