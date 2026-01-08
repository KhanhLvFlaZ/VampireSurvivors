#if ENABLE_NETCODE
using UnityEngine;
using Unity.Netcode;
using Vampire.Gameplay.Characters;

namespace Vampire.Gameplay.Networking
{
    /// <summary>
    /// Network-aware enemy/monster
    /// Spawned by server with specific owner client (or server-owned)
    /// Uses NetworkVariables for periodic state sync to all clients
    /// </summary>
    [RequireComponent(typeof(Monster))]
    public class NetworkEnemy : NetworkEntity
    {
        private Monster monster;

        // Enemy-specific network state
        protected NetworkVariable<int> networkCurrentAction = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        protected NetworkVariable<bool> networkIsAttacking = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Target tracking (only server tracks actual target)
        private Transform currentTarget;
        private float targetSearchTimer;
        private const float TARGET_SEARCH_INTERVAL = 0.5f;

        protected override void Awake()
        {
            base.Awake();
            monster = GetComponent<Monster>();
        }

        protected override void Start()
        {
            base.Start();
            targetSearchTimer = TARGET_SEARCH_INTERVAL;
        }

        /// <summary>
        /// Server: AI logic and state updates
        /// </summary>
        protected override void UpdateServerState()
        {
            if (!IsServer || monster == null)
                return;

            base.UpdateServerState();

            // Server-side AI logic
            UpdateAILogic();

            // Update monster-specific network state
            networkIsAttacking.Value = monster.IsAttacking;
        }

        /// <summary>
        /// Server-side AI behavior
        /// </summary>
        private void UpdateAILogic()
        {
            if (!monster.IsAlive)
                return;

            // Find nearest target (players)
            targetSearchTimer -= Time.fixedDeltaTime;
            if (targetSearchTimer <= 0)
            {
                currentTarget = FindNearestPlayer();
                targetSearchTimer = TARGET_SEARCH_INTERVAL;
            }

            if (currentTarget == null)
            {
                // Wander behavior
                if (!monster.IsMoving)
                {
                    Vector2 wanderDirection = Random.insideUnitCircle.normalized;
                    monster.Move(wanderDirection);
                }
            }
            else
            {
                // Chase behavior
                Vector2 directionToTarget = ((Vector2)currentTarget.position - rb.position).normalized;
                float distanceToTarget = Vector2.Distance(rb.position, currentTarget.position);

                if (distanceToTarget > monster.AttackRange)
                {
                    monster.Move(directionToTarget);
                }
                else
                {
                    // Attack
                    monster.Move(Vector2.zero);
                    monster.Attack(directionToTarget);
                }
            }
        }

        /// <summary>
        /// Find nearest player
        /// </summary>
        private Transform FindNearestPlayer()
        {
            NetworkCharacter[] players = FindObjectsOfType<NetworkCharacter>();
            Transform nearest = null;
            float nearestDistance = Mathf.Infinity;

            foreach (var player in players)
            {
                if (!player.IsCharacterAlive)
                    continue;

                float distance = Vector2.Distance(rb.position, (Vector2)player.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = player.transform;
                }
            }

            return nearestDistance < 20f ? nearest : null; // Detection range 20m
        }

        /// <summary>
        /// All clients: interpolate non-owned enemies
        /// </summary>
        protected override void InterpolateToNetworkState()
        {
            if (rb != null && useInterpolation)
            {
                // Smooth interpolation
                float alpha = Mathf.Clamp01(
                    (networkTickRate - networkSyncTimer) / networkTickRate
                );

                Vector2 interpolatedPos = Vector2.Lerp(
                    lastNetworkPosition,
                    networkPosition.Value,
                    Mathf.SmoothStep(0, 1, alpha)
                );

                rb.position = interpolatedPos;
                rb.linearVelocity = networkVelocity.Value;

                // Update animation state
                if (monster != null && monster.animationController != null)
                {
                    monster.animationController.SetMovement(networkVelocity.Value);
                }
            }
        }

        /// <summary>
        /// Damage this enemy (server RPC for authority)
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (monster == null)
                return;

            if (IsServer)
            {
                monster.TakeDamage(amount);
                networkHealth.Value = Mathf.Max(0, monster.CurrentHealth);

                if (!monster.IsAlive)
                {
                    OnEnemyDeadClientRpc();
                }
            }
            else
            {
                ReportDamageServerRpc(amount);
            }
        }

        [ServerRpc]
        private void ReportDamageServerRpc(float amount)
        {
            if (monster != null)
            {
                monster.TakeDamage(amount);
                networkHealth.Value = Mathf.Max(0, monster.CurrentHealth);

                if (!monster.IsAlive)
                {
                    OnEnemyDeadClientRpc();
                }
            }
        }

        [ClientRpc]
        private void OnEnemyDeadClientRpc()
        {
            if (monster != null && monster.IsAlive)
            {
                monster.Die();
            }
        }

        public Monster GetMonster() => monster;
        public bool IsEnemyAlive => monster != null && monster.IsAlive;
    }
}
#endif
