using UnityEngine;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Automatically disables duplicate AudioListener components to prevent Unity warnings
    /// Keeps only the first AudioListener enabled
    /// </summary>
    public class AudioListenerCleanup : MonoBehaviour
    {
        private void Awake()
        {
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();

            if (listeners.Length > 1)
            {
                // Disable all AudioListeners except the first one
                for (int i = 1; i < listeners.Length; i++)
                {
                    listeners[i].enabled = false;
                }
            }
        }
    }
}
