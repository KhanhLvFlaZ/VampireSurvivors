#if ENABLE_NETCODE
using UnityEngine;
using Unity.Netcode;
using Vampire.Gameplay.Characters;

namespace Vampire.Gameplay.Networking
{
    /// <summary>
    /// Network-aware player character
    /// Handles client-side prediction and server reconciliation for smooth gameplay
    /// Inherits from NetworkEntity for base network functionality
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class NetworkCharacter : NetworkEntity
    {
        private Character character;

        // Client-side prediction state
        private Vector2 clientPredictionVelocity;
        private Vector2 lastConfirmedPosition;
        private float predictionCorrectionSpeed = 5f;

        // Server reconciliation settings
        private const float POSITION_ERROR_THRESHOLD_HARD = 2f; // Hard teleport if > 2m error
        private const float POSITION_ERROR_THRESHOLD_SOFT = 0.1f; // Lerp if > 0.1m error

        protected override void Awake()
        {
            base.Awake();
            character = GetComponent<Character>();
        }

        protected override void Start()
        {
            base.Start();

            if (IsServer)
            {
                lastConfirmedPosition = (Vector2)transform.position;
            }
        }

        /// <summary>
        /// Owner client: apply local input with client-side prediction
        /// </summary>
        protected override void ApplyLocalInput()
        {
            if (!IsOwner || character == null)
                return;

            // Character already applies input locally via CharacterController
            // We just need to periodically sync state to server
            networkSyncTimer -= Time.fixedDeltaTime;

            if (networkSyncTimer <= 0)
            {
                // Send current predicted state to server
                SyncStateToServerServerRpc(
                    (Vector2)transform.position,
                    rb.linearVelocity,
                    character.IsAlive ? character.CurrentHealth : 0
                );

                networkSyncTimer = networkTickRate;
            }
        }

        /// <summary>
        /// Server RPC: Receive client state and reconcile
        /// </summary>
        [ServerRpc]
        private void SyncStateToServerServerRpc(Vector2 clientPosition, Vector2 clientVelocity, float clientHealth)
        {
            // Validate position (prevent cheating/exploits)
            float positionError = Vector2.Distance(clientPosition, rb.position);

            if (positionError > POSITION_ERROR_THRESHOLD_HARD)
            {
                // Large discrepancy - hard teleport (likely lag spike or teleport ability)
                rb.position = clientPosition;
            }
            else if (positionError > POSITION_ERROR_THRESHOLD_SOFT)
            {
                // Small discrepancy - smoothly lerp (network jitter)
                rb.position = Vector2.Lerp(rb.position, clientPosition, 0.5f);
            }

            // Update velocity
            rb.linearVelocity = clientVelocity;

            // Update health (server is authoritative)
            if (character != null && character.IsAlive)
            {
                networkHealth.Value = Mathf.Max(0, character.CurrentHealth);
            }

            // Broadcast corrected state to all clients
            UpdateClientStateClientRpc((Vector2)rb.position, rb.linearVelocity);
        }

        /// <summary>
        /// Client RPC: All clients receive corrected state
        /// </summary>
        [ClientRpc]
        private void UpdateClientStateClientRpc(Vector2 serverPosition, Vector2 serverVelocity)
        {
            // Non-owner clients update their interpolation targets
            if (!IsOwner)
            {
                lastNetworkPosition = (Vector2)transform.position;
                targetNetworkPosition = serverPosition;
                networkVelocity.Value = serverVelocity;
            }
            else
            {
                // Owner client: minor correction if needed
                float correctionError = Vector2.Distance(serverPosition, (Vector2)rb.position);
                if (correctionError > 0.5f)
                {
                    // Server corrected us significantly - apply correction
                    rb.position = Vector2.Lerp((Vector2)rb.position, serverPosition, 0.3f);
                }
            }
        }

        /// <summary>
        /// Non-owner clients: smooth interpolation to network state
        /// </summary>
        protected override void InterpolateToNetworkState()
        {
            if (!IsOwner && rb != null && useInterpolation)
            {
                // Interpolate position based on network tick rate
                float alpha = Mathf.Clamp01(
                    (networkTickRate - networkSyncTimer) / networkTickRate
                );

                // Smooth interpolation from last to target
                Vector2 interpolatedPos = Vector2.Lerp(
                    lastNetworkPosition,
                    networkPosition.Value,
                    Mathf.SmoothStep(0, 1, alpha)
                );

                rb.position = interpolatedPos;
                rb.linearVelocity = networkVelocity.Value;
            }
        }

        /// <summary>
        /// Damage this character (server RPC for authority)
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (character == null)
                return;

            if (IsServer)
            {
                character.TakeDamage(amount);
                networkHealth.Value = Mathf.Max(0, character.CurrentHealth);
            }
            else if (IsOwner)
            {
                // Client reports damage to server
                ReportDamageServerRpc(amount);
            }
        }

        [ServerRpc]
        private void ReportDamageServerRpc(float amount)
        {
            if (character != null)
            {
                character.TakeDamage(amount);
                networkHealth.Value = Mathf.Max(0, character.CurrentHealth);

                // Broadcast health update to all clients
                if (!character.IsAlive)
                {
                    OnCharacterDeadClientRpc();
                }
            }
        }

        [ClientRpc]
        private void OnCharacterDeadClientRpc()
        {
            if (character != null && character.IsAlive)
            {
                character.Die();
            }
        }

        /// <summary>
        /// Heal this character
        /// </summary>
        public void Heal(float amount)
        {
            if (character == null)
                return;

            if (IsServer)
            {
                character.Heal(amount);
                networkHealth.Value = character.CurrentHealth;
            }
            else if (IsOwner)
            {
                ReportHealServerRpc(amount);
            }
        }

        [ServerRpc]
        private void ReportHealServerRpc(float amount)
        {
            if (character != null && character.IsAlive)
            {
                character.Heal(amount);
                networkHealth.Value = character.CurrentHealth;
            }
        }

        public Character GetCharacter() => character;
        public bool IsCharacterAlive => character != null && character.IsAlive;
    }
}
#endif
