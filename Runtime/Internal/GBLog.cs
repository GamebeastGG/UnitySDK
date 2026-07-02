using UnityEngine;

namespace Gamebeast.Internal
{
    /// <summary>
    /// SDK logging. Info logs are gated behind GamebeastSettings.DebugLogging;
    /// warnings and errors always surface.
    /// </summary>
    internal static class GBLog
    {
        private const string Prefix = "[Gamebeast] ";

        internal static bool DebugEnabled;

        public static void Info(string message)
        {
            if (DebugEnabled)
            {
                Debug.Log(Prefix + message);
            }
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(Prefix + message);
        }
    }
}
