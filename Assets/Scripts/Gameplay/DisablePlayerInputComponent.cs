using UnityEngine;
using UnityEngine.InputSystem;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Disables Unity's PlayerInput component on all players to prevent conflicts
    /// with PlayerKeyboardController for local co-op
    /// </summary>
    [DefaultExecutionOrder(-100)] // Run before other scripts
    public class DisablePlayerInputComponent : MonoBehaviour
    {
        [SerializeField] private bool disableOnAwake = true;
        [SerializeField] private bool logDisabledComponents = false;

        private void Awake()
        {
            if (disableOnAwake)
            {
                // Disabled: Don't disable any PlayerInput components
                // DisableAllPlayerInputComponents();
            }
        }

        [ContextMenu("Disable All PlayerInput Components")]
        public void DisableAllPlayerInputComponents()
        {
            PlayerInput[] playerInputs = FindObjectsOfType<PlayerInput>(true);

            int disabledCount = 0;
            foreach (var pi in playerInputs)
            {
                if (pi.enabled)
                {
                    // Only disable Player 2 and beyond (not the first player which uses WASD)
                    if (pi.gameObject.name.Contains("2") || pi.gameObject.name.Contains("Player 2"))
                    {
                        pi.enabled = false;
                        disabledCount++;

                        if (logDisabledComponents)
                        {
                            Debug.Log($"[DisablePlayerInput] Disabled PlayerInput on '{pi.gameObject.name}'");
                        }
                    }
                }
            }

            if (logDisabledComponents && disabledCount > 0)
            {
                Debug.Log($"[DisablePlayerInput] Disabled {disabledCount} PlayerInput component(s)");
            }
        }
    }
}
