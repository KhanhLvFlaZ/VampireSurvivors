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
        [SerializeField] private bool logDisabledComponents = true;

        private void Awake()
        {
            if (disableOnAwake)
            {
                DisableAllPlayerInputComponents();
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
                    pi.enabled = false;
                    disabledCount++;

                    if (logDisabledComponents)
                    {
                        Debug.Log($"[DisablePlayerInput] Disabled PlayerInput on '{pi.gameObject.name}'");
                    }
                }
            }

            if (logDisabledComponents)
            {
                Debug.Log($"[DisablePlayerInput] Disabled {disabledCount} PlayerInput component(s)");
            }
        }
    }
}
