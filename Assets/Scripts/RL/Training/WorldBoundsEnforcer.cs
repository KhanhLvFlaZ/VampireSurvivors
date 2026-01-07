using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// Hard boundary enforcer for any 2D object (Player or Monster).
    /// Clamps position every LateUpdate and zeroes velocity when crossing bounds.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldBoundsEnforcer : MonoBehaviour
    {
        [Header("Arena Bounds")]
        [SerializeField] private Vector2 arenaCenter = Vector2.zero;
        [SerializeField] private float arenaHalfSize = 12f;
        [SerializeField] private float wallMargin = 0.5f;

        private Rigidbody2D rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        void LateUpdate()
        {
            Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
            Vector2 clamped = Clamp(pos);

            if ((pos - clamped).sqrMagnitude > 1e-6f)
            {
                if (rb != null)
                {
                    rb.position = clamped;
                    rb.linearVelocity = Vector2.zero; // stop any drift beyond walls
                }
                else
                {
                    transform.position = clamped;
                }
            }
        }

        Vector2 Clamp(Vector2 pos)
        {
            float minX = arenaCenter.x - (arenaHalfSize - wallMargin);
            float maxX = arenaCenter.x + (arenaHalfSize - wallMargin);
            float minY = arenaCenter.y - (arenaHalfSize - wallMargin);
            float maxY = arenaCenter.y + (arenaHalfSize - wallMargin);
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            return pos;
        }

        public void SetBounds(Vector2 center, float halfSize, float margin = 0.5f)
        {
            arenaCenter = center;
            arenaHalfSize = halfSize;
            wallMargin = margin;
        }
    }
}
