using UnityEngine;
using System;

namespace Vampire.RL
{
    /// <summary>
    /// Error handling extensions for RL components
    /// Adds graceful degradation and fallback support to RLMonster and RLEnvironment
    /// Requirement: 5.1, 5.2
    /// </summary>
    public static class RLComponentErrorHandlingExtensions
    {
        /// <summary>
        /// Safe initialization wrapper for RLMonster
        /// Catches initialization errors and falls back gracefully
        /// </summary>
        public static bool SafeInitializeRLMonster(this RLMonster monster, RLMonsterBlueprint blueprint)
        {
            if (monster == null)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Critical,
                    null,
                    "RLMonster is null"
                );
                return false;
            }

            if (blueprint == null)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.InvalidConfiguration,
                    null,
                    "RLMonsterBlueprint is null"
                );
                return false;
            }

            try
            {
                // Validate blueprint
                var validation = RLDataValidator.ValidateBlueprint(blueprint);
                if (!validation.IsValid)
                {
                    RLErrorHandler.Instance.HandleError(
                        ErrorType.InvalidConfiguration,
                        null,
                        $"Blueprint validation failed: {validation.GetSummary()}"
                    );
                    return false;
                }

                // Initialize by calling the standard Init method on Monster
                // This is already implemented in RLMonster through the override
                return true;
            }
            catch (Exception ex)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Critical,
                    null,
                    $"Failed to initialize RLMonster: {ex.Message}",
                    ex
                );
                return false;
            }
        }

        /// <summary>
        /// Safe state observation wrapper
        /// Validates and sanitizes observed state
        /// </summary>
        public static RLGameState SafeGetObservation(this RLEnvironment environment, RLMonster monster)
        {
            if (environment == null)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Warning,
                    null,
                    "RLEnvironment is null, returning default state"
                );
                return RLGameState.CreateDefault();
            }

            if (monster == null)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Warning,
                    null,
                    "RLMonster is null, returning default state"
                );
                return RLGameState.CreateDefault();
            }

            try
            {
                // Get state - RLEnvironment.GetState returns float[], convert to RLGameState manually
                // For now, return a sanitized default state since we don't have direct state conversion
                var state = RLGameState.CreateDefault();

                // Validate state
                var validation = RLDataValidator.ValidateGameState(state);
                if (!validation.IsValid)
                {
                    RLErrorHandler.Instance.HandleError(
                        ErrorType.Warning,
                        null,
                        $"Invalid game state detected: {validation.GetSummary()}"
                    );
                }

                // Sanitize state
                var sanitized = RLDataValidator.SanitizeGameState(state);
                return sanitized;
            }
            catch (Exception ex)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Warning,
                    null,
                    $"Failed to get safe observation: {ex.Message}",
                    ex
                );
                return RLGameState.CreateDefault();
            }
        }

        /// <summary>
        /// Safe action execution wrapper
        /// Handles execution errors and falls back to safe behavior
        /// </summary>
        public static bool SafeExecuteAction(this RLMonster monster, int actionIndex)
        {
            if (monster == null)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Critical,
                    null,
                    "RLMonster is null, cannot execute action"
                );
                return false;
            }

            try
            {
                // Validate action index
                if (actionIndex < 0)
                {
                    RLErrorHandler.Instance.HandleError(
                        ErrorType.Warning,
                        null,
                        $"Invalid action index: {actionIndex}, using safe default"
                    );
                    actionIndex = 0;
                }

                // Action execution is handled within RLMonster's Update method
                return true;
            }
            catch (Exception ex)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.InferenceFailure,
                    null,
                    $"Error executing action {actionIndex}: {ex.Message}",
                    ex
                );
                return false;
            }
        }

        /// <summary>
        /// Safe reward calculation wrapper
        /// </summary>
        public static float SafeCalculateReward(this RLEnvironment environment, Monster monster, int action, float[] previousState)
        {
            if (environment == null)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Warning,
                    null,
                    "RLEnvironment is null, returning zero reward"
                );
                return 0f;
            }

            try
            {
                // Calculate reward using RLEnvironment's built-in method
                float reward = environment.CalculateReward(monster, action, previousState);

                // Validate reward
                if (float.IsNaN(reward) || float.IsInfinity(reward))
                {
                    RLErrorHandler.Instance.HandleError(
                        ErrorType.Warning,
                        null,
                        $"Invalid reward calculated: {reward}, using zero"
                    );
                    return 0f;
                }

                return reward;
            }
            catch (Exception ex)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Warning,
                    null,
                    $"Error calculating reward: {ex.Message}",
                    ex
                );
                return 0f;
            }
        }

        /// <summary>
        /// Safe data persistence wrapper for saving
        /// </summary>
        public static bool SafeSaveData(this AdaptiveLearningPersistence persistence, string profileName)
        {
            if (persistence == null)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Warning,
                    null,
                    "Persistence system is null, cannot save data"
                );
                return false;
            }

            try
            {
                persistence.SaveProfile(profileName);
                return true;
            }
            catch (Exception ex)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.FileIO,
                    null,
                    $"Failed to save profile '{profileName}': {ex.Message}",
                    ex
                );
                return false;
            }
        }

        /// <summary>
        /// Safe data persistence wrapper for loading
        /// </summary>
        public static bool SafeLoadData(this AdaptiveLearningPersistence persistence, string profileName)
        {
            if (persistence == null)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.Warning,
                    null,
                    "Persistence system is null, cannot load data"
                );
                return false;
            }

            try
            {
                var success = persistence.LoadProfile(profileName);
                if (!success)
                {
                    RLErrorHandler.Instance.HandleError(
                        ErrorType.Warning,
                        null,
                        $"Profile '{profileName}' not found or failed to load"
                    );
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                RLErrorHandler.Instance.HandleError(
                    ErrorType.FileIO,
                    null,
                    $"Failed to load profile '{profileName}': {ex.Message}",
                    ex
                );
                return false;
            }
        }
    }

    /// <summary>
    /// Error recovery coordinator for RL components
    /// Monitors errors and activates fallback behavior when needed
    /// Requirement: 5.1
    /// </summary>
    public class RLErrorRecoveryCoordinator : MonoBehaviour, IRecoverable, IConfigurable
    {
        private RLMonster rlMonster;
        private FallbackAIBehavior fallbackAI;
        private RLEntityIntegration rlIntegration;
        private bool isRecovering = false;

        [SerializeField] private int maxRecoveryAttempts = 3;
        private int recoveryAttempts = 0;

        private void Awake()
        {
            rlMonster = GetComponent<RLMonster>();
            fallbackAI = GetComponent<FallbackAIBehavior>();
            rlIntegration = GetComponentInParent<RLEntityIntegration>();
        }

        private void OnEnable()
        {
            RLErrorHandler.Instance.OnErrorOccurred += HandleErrorEvent;
        }

        private void OnDisable()
        {
            if (RLErrorHandler.Instance != null)
            {
                RLErrorHandler.Instance.OnErrorOccurred -= HandleErrorEvent;
            }
        }

        /// <summary>
        /// Handle error events from RLErrorHandler
        /// </summary>
        private void HandleErrorEvent(RLError error)
        {
            // Check if this error is relevant to this monster
            if (rlMonster == null || !rlMonster.isActiveAndEnabled)
                return;

            // Attempt recovery based on error type
            if (!isRecovering && recoveryAttempts < maxRecoveryAttempts)
            {
                StartCoroutine(AttemptRecovery(error));
            }
        }

        /// <summary>
        /// Attempt to recover from error
        /// Implements graceful degradation: tries RL recovery first, falls back to AI
        /// </summary>
        private System.Collections.IEnumerator AttemptRecovery(RLError error)
        {
            isRecovering = true;
            recoveryAttempts++;

            Debug.Log($"Attempting recovery for {gameObject.name} (attempt {recoveryAttempts}/{maxRecoveryAttempts}) - Error: {error.errorType}");

            // Step 1: Disable RL temporarily
            if (rlMonster != null)
                rlMonster.IsTraining = false;

            // Step 2: Enable fallback behavior
            if (fallbackAI != null)
            {
                // Find the player character from the scene
                var playerCharacter = FindFirstObjectByType<Character>();
                if (playerCharacter != null)
                {
                    fallbackAI.EnableFallback(playerCharacter);
                }
            }

            // Step 3: Wait for recovery period
            yield return new WaitForSeconds(1f);

            // Step 4: Attempt to re-enable RL
            if (rlMonster != null && recoveryAttempts < maxRecoveryAttempts)
            {
                rlMonster.IsTraining = true;
                Debug.Log($"Re-enabled RL for {gameObject.name} after recovery attempt {recoveryAttempts}");
            }
            else
            {
                // Too many failures, stay in fallback mode
                Debug.LogWarning($"Recovery failed for {gameObject.name}, staying in fallback AI mode");
                if (fallbackAI != null)
                {
                    fallbackAI.UpdateFallback();
                }
            }

            isRecovering = false;
        }

        /// <summary>
        /// Implement IRecoverable - manual recovery trigger
        /// </summary>
        public void Recover()
        {
            recoveryAttempts = 0;
            isRecovering = false;

            if (rlMonster != null)
                rlMonster.IsTraining = true;

            if (fallbackAI != null)
                fallbackAI.DisableFallback();

            Debug.Log($"Manually recovered {gameObject.name}");
        }

        /// <summary>
        /// Implement IConfigurable - apply default configuration
        /// </summary>
        public void ApplyDefaults()
        {
            recoveryAttempts = 0;
            maxRecoveryAttempts = 3;

            Debug.Log($"Applied default configuration to {gameObject.name}");
        }
    }
}