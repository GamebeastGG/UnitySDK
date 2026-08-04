using System.Collections.Generic;
using UnityEngine;

namespace Gamebeast.Internal.Utils
{
    /// <summary>
    /// Builds the targeting context sent with configuration evaluation requests:
    /// a stable set of device/app properties, overlaid with any developer-supplied
    /// <see cref="GamebeastSettings.UserProperties"/> (developer values win per key).
    /// </summary>
    internal static class EvaluationContextBuilder
    {
        private static Dictionary<string, object> _deviceProperties;

        public static Dictionary<string, object> BuildProperties(GamebeastSettings settings)
        {
            // Device/app facts don't change during a run; snapshot them once
            // (must happen on the main thread, which all SDK requests are).
            if (_deviceProperties == null)
            {
                _deviceProperties = new Dictionary<string, object>
                {
                    { "platform", Application.platform.ToString() },
                    { "operatingSystem", SystemInfo.operatingSystem },
                    { "deviceModel", SystemInfo.deviceModel },
                    { "deviceType", SystemInfo.deviceType.ToString() },
                    { "systemLanguage", Application.systemLanguage.ToString() },
                    { "appVersion", Application.version },
                };
            }

            var properties = new Dictionary<string, object>(_deviceProperties);

            var custom = settings.UserProperties;
            if (custom != null)
            {
                foreach (var pair in custom)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key)) continue;
                    properties[pair.Key] = pair.Value;
                }
            }

            return properties;
        }
    }
}
