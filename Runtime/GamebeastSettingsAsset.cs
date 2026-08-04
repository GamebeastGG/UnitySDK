using UnityEngine;

namespace Gamebeast
{
    /// <summary>
    /// Optional inspector-friendly settings for automatic SDK initialization.
    ///
    /// Create via "Assets → Create → Gamebeast → Settings" and place the asset at
    /// "Resources/GamebeastSettings" — the SDK then initializes itself at startup,
    /// no bootstrap code required. Calling <see cref="GamebeastSdk.Setup(GamebeastSettings)"/>
    /// manually still works and takes precedence (auto-init is skipped once initialized).
    ///
    /// Code-only options (a custom DistinctId, UserProperties) are not exposed here;
    /// set them at runtime via <see cref="GamebeastSdk.Identify"/> and
    /// <see cref="GamebeastSettings.UserProperties"/> on a manual Setup.
    /// </summary>
    [CreateAssetMenu(fileName = "GamebeastSettings", menuName = "Gamebeast/Settings")]
    public sealed class GamebeastSettingsAsset : ScriptableObject
    {
        [Tooltip("Your Gamebeast project API key. Required.")]
        public string ApiKey;

        [Tooltip("Initialize the SDK automatically at startup when this asset lives in a Resources folder.")]
        public bool AutoInitialize = true;

        [Tooltip("Which Gamebeast environment to target. Auto = Development in the Editor, Production in builds.")]
        public GamebeastEnvironment Environment = GamebeastEnvironment.Auto;

        [Tooltip("Optional: a custom environment alias from the dashboard (e.g. \"staging\"). Overrides Environment when set.")]
        public string CustomEnvironment;

        [Tooltip("Configurations to preload at startup, by alias. Preloaded configurations gate Configs.IsReady/OnReady.")]
        public string[] Configurations;

        [Tooltip("How often (seconds) the SDK re-checks the backend for configuration changes. 0 disables background refresh.")]
        public float ConfigRefreshIntervalSeconds = 60f;

        [Tooltip("Rotate the session id when the app has been backgrounded at least this many seconds. 0 keeps one session per run.")]
        public float SessionTimeoutSeconds = 1800f;

        [Tooltip("Enables verbose SDK logging in the console.")]
        public bool DebugLogging = false;

        [Tooltip("Override the Gamebeast API base URL. Leave as-is for production use.")]
        public string ApiUrl = "https://api.gamebeast.gg";

        public GamebeastSettings ToSettings()
        {
            return new GamebeastSettings
            {
                ApiKey = ApiKey,
                Environment = Environment,
                CustomEnvironment = CustomEnvironment,
                Configurations = Configurations,
                ConfigRefreshIntervalSeconds = ConfigRefreshIntervalSeconds,
                SessionTimeoutSeconds = SessionTimeoutSeconds,
                DebugLogging = DebugLogging,
                ApiUrl = ApiUrl,
            };
        }
    }
}
