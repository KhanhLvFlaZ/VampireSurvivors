using UnityEngine;
using Vampire;
using System.Collections;

namespace Vampire.RL
{
    /// <summary>
    /// Fallback AI behavior when RL system fails
    /// Provides traditional AI-based monster behavior as fallback
    /// Requirement: 5.2 - Fallback to traditional AI behavior
    /// </summary>
    public class FallbackAIBehavior : MonoBehaviour
    {
        private Monster monster;
        private Character playerCharacter;
        private Rigidbody2D rb;

        [SerializeField] private float fallbackMovementSpeed = 5f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackCooldown = 1f;
        [SerializeField] private float stopDistance = 0.5f;
        [SerializeField] private bool debugFallback = false;

        private float lastAttackTime = 0f;
        private bool isActive = false;

        public bool IsActive => isActive;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            rb = GetComponent<Rigidbody2D>();
        }

        /// <summary>
        /// Enable fallback AI behavior
        /// Requirement: 5.2
        /// </summary>
        public void EnableFallback(Character player)
        {
            playerCharacter = player;
            isActive = true;

            if (debugFallback)
            {
                Debug.Log($"Fallback AI enabled for {monster.name}");
            }
        }

        /// <summary>
        /// Disable fallback AI behavior
        /// </summary>
        public void DisableFallback()
        {
            isActive = false;

            if (debugFallback)
            {
                Debug.Log($"Fallback AI disabled for {monster.name}");
            }
        }

        /// <summary>
        /// Update fallback behavior
        /// Should be called from Update when RL is disabled
        /// </summary>
        public void UpdateFallback()
        {
            if (!isActive || playerCharacter == null || rb == null || monster == null)
                return;

            // Get direction to player
            Vector2 directionToPlayer = ((Vector2)playerCharacter.transform.position - rb.position).normalized;
            float distanceToPlayer = Vector2.Distance(rb.position, playerCharacter.transform.position);

            // Simple behavior: approach player and attack
            if (distanceToPlayer > attackRange)
            {
                // Move toward player
                rb.linearVelocity = directionToPlayer * fallbackMovementSpeed;
            }
            else if (distanceToPlayer > stopDistance)
            {
                // Stop and attack
                rb.linearVelocity *= 0.9f; // Decelerate

                // Attack if cooldown is ready
                if (Time.time - lastAttackTime >= attackCooldown)
                {
                    AttackPlayer();
                    lastAttackTime = Time.time;
                }
            }
            else
            {
                // Too close: hold position to avoid overlapping the player
                rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// Execute attack on player
        /// </summary>
        private void AttackPlayer()
        {
            if (playerCharacter == null || monster == null)
                return;

            // Calculate knockback direction
            Vector2 knockbackDirection = ((Vector2)playerCharacter.transform.position - rb.position).normalized;

            // Deal damage to player (simplified - in real game would use player damage system)
            // This is a placeholder since we don't have access to the actual player damage method
            if (debugFallback)
            {
                Debug.Log($"{monster.name} attacked player with fallback AI");
            }
        }

        /// <summary>
        /// Get simple behavior state for debugging
        /// </summary>
        public FallbackBehaviorState GetBehaviorState()
        {
            if (!isActive || playerCharacter == null)
                return FallbackBehaviorState.Inactive;

            float distanceToPlayer = Vector2.Distance(rb.position, playerCharacter.transform.position);

            if (distanceToPlayer > attackRange)
                return FallbackBehaviorState.Approaching;
            else
                return FallbackBehaviorState.Attacking;
        }
    }

    /// <summary>
    /// Fallback behavior state enumeration
    /// </summary>
    public enum FallbackBehaviorState
    {
        Inactive,
        Approaching,
        Attacking,
        Retreating
    }
}
