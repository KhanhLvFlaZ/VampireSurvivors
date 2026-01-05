#if ENABLE_NETCODE
using UnityEngine;
using Unity.Netcode;

namespace Vampire.Gameplay.Networking
{
    /// <summary>
    /// Base class for networked game entities
    /// Provides common functionality for players and enemies with server-authoritative design
    /// Uses NetworkVariables for periodic state sync (preferred over frequent RPCs)
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public abstract class NetworkEntity : NetworkBehaviour
    {
        [Header("Network Settings")]
        [SerializeField] protected float networkTickRate = 0.1f; // Sync every 100ms
        [SerializeField] protected bool useInterpolation = true;

        // Network state - synced every tick
        protected NetworkVariable<Vector2> networkPosition = new NetworkVariable<Vector2>(
            Vector2.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        protected NetworkVariable<Vector2> networkVelocity = new NetworkVariable<Vector2>(
            Vector2.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        protected NetworkVariable<float> networkHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Local state for interpolation
        private Vector2 lastNetworkPosition;
        private Vector2 targetNetworkPosition;
        private float networkSyncTimer;

        protected Rigidbody2D rb;
        protected NetworkObject networkObject;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            networkObject = GetComponent<NetworkObject>();
        }

        protected virtual void Start()
        {
            networkSyncTimer = networkTickRate;
        }

        protected virtual void FixedUpdate()
        {
            if (!IsNetworkInitialized)
                return;

            if (IsServer)
            {
                // Server: update network state periodically
                UpdateServerState();
            }
            else if (IsOwner)
            {
                // Owner client: apply local input (client-side prediction)
                ApplyLocalInput();
            }
            else
            {
                // Non-owner clients: interpolate to networked position
                if (useInterpolation)
                {
                    InterpolateToNetworkState();
                }
                else
                {
                    SyncToNetworkState();
                }
            }
        }

        /// <summary>
        /// Server updates network state periodically
        /// </summary>
        protected virtual void UpdateServerState()
        {
            networkSyncTimer -= Time.fixedDeltaTime;

            if (networkSyncTimer <= 0)
            {
                // Sync position and velocity
                networkPosition.Value = (Vector2)transform.position;
                networkVelocity.Value = rb.linearVelocity;

                // Update health (if entity has IDamageable)
                if (GetComponent<IDamageable>() is IDamageable damageable)
                {
                    // Health would be updated separately by damage system
                }

                networkSyncTimer = networkTickRate;
            }
        }

        /// <summary>
        /// Owner client applies local input (client-side prediction)
        /// </summary>
        protected virtual void ApplyLocalInput()
        {
            // Override in derived classes to implement client-side prediction
            // This runs on the owner client with reduced latency
        }

        /// <summary>
        /// Interpolate smoothly to networked position (non-owner clients)
        /// </summary>
        protected virtual void InterpolateToNetworkState()
        {
            if (useInterpolation && rb != null)
            {
                // Smooth interpolation towards network position
                float lerpSpeed = 1f / networkTickRate;
                Vector2 newPos = Vector2.Lerp(
                    lastNetworkPosition,
                    networkPosition.Value,
                    lerpSpeed * Time.fixedDeltaTime
                );
                
                rb.position = newPos;
                rb.linearVelocity = networkVelocity.Value;
            }
        }

        /// <summary>
        /// Direct sync without interpolation (non-owner clients)
        /// </summary>
        protected virtual void SyncToNetworkState()
        {
            if (rb != null)
            {
                rb.position = networkPosition.Value;
                rb.linearVelocity = networkVelocity.Value;
            }
        }

        /// <summary>
        /// Called when network position changes (for interpolation setup)
        /// </summary>
        protected virtual void OnNetworkPositionChanged(Vector2 previousValue, Vector2 newValue)
        {
            if (!IsServer && !IsOwner)
            {
                lastNetworkPosition = previousValue;
                targetNetworkPosition = newValue;
            }
        }

        /// <summary>
        /// Server-side RPC for rare events (damage, special effects, etc.)
        /// Use sparingly - prefer NetworkVariables for state sync
        /// </summary>
        [ServerRpc]
        protected virtual void ReportEventServerRpc(string eventType, float value = 0)
        {
            // Server processes event and broadcasts if needed
            Debug.Log($"[Network] Entity {gameObject.name} event: {eventType}");
        }

        public bool IsNetworkInitialized => networkObject != null && IsNetworkSpawned;

        public Vector2 GetNetworkPosition() => networkPosition.Value;
        public Vector2 GetNetworkVelocity() => networkVelocity.Value;
        public float GetNetworkHealth() => networkHealth.Value;
    }
}
#endif
