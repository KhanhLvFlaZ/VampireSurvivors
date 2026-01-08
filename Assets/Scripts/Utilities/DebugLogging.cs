using UnityEngine;

namespace Vampire.Utilities
{
    /// <summary>
    /// Global logging control - toggle all Debug.Log calls for the game
    /// </summary>
    public static class DebugLogging
    {
        public static bool EnableLogging { get; set; } = false;

        public static void Log(object message)
        {
            if (EnableLogging)
                Debug.Log(message);
        }

        public static void Log(object message, Object context)
        {
            if (EnableLogging)
                Debug.Log(message, context);
        }

        public static void LogWarning(object message)
        {
            Debug.LogWarning(message);
        }

        public static void LogWarning(object message, Object context)
        {
            Debug.LogWarning(message, context);
        }

        public static void LogError(object message)
        {
            Debug.LogError(message);
        }

        public static void LogError(object message, Object context)
        {
            Debug.LogError(message, context);
        }
    }
}
