using System;

namespace Gamebeast
{
    /// <summary>
    /// Gamebeast environment aliases. Sent to the backend via the "environment" header.
    /// </summary>
    public enum GamebeastEnvironment
    {
        /// <summary>Resolves to Studio inside the Unity Editor and Production in builds.</summary>
        Auto = 0,
        Production,
        Development
    }

    /// <summary>
    /// Setup options for the Gamebeast SDK. Pass to <see cref="GamebeastSdk.Setup(GamebeastSettings)"/>.
    /// </summary>
    [Serializable]
    public sealed class GamebeastSettings
    {
        /// <summary>Your Gamebeast project API key. Required.</summary>
        public string ApiKey;

        /// <summary>
        /// Optional distinct id for the local player. When omitted, the SDK generates an
        /// anonymous id on first launch and persists it on the device (PlayerPrefs),
        /// so the same user is reported across sessions.
        /// </summary>
        public string DistinctId;

        /// <summary>Which Gamebeast environment to target. Defaults to <see cref="GamebeastEnvironment.Auto"/>.</summary>
        public GamebeastEnvironment Environment = GamebeastEnvironment.Auto;

        /// <summary>
        /// Optional: configurations to preload at startup, by alias (URL-safe slug of the
        /// configuration name), e.g. new[] { "ConfigA", "ConfigB" }. Preloaded
        /// configurations are fetched immediately and gate Configs.IsReady/OnReady.
        /// Configurations not listed here are fetched automatically the first time they
        /// are accessed; read values with Configs.Get&lt;T&gt;("ConfigA.SomeKey").
        /// </summary>
        public string[] Configurations;

        /// <summary>
        /// How often (seconds) the SDK re-checks the backend for configuration changes.
        /// Uses hash-based caching, so unchanged configs are cheap round trips.
        /// Set to 0 to disable background refresh (you can still call Configs.RefreshAsync()).
        /// </summary>
        public float ConfigRefreshIntervalSeconds = 60f;

        /// <summary>Enables verbose SDK logging in the console.</summary>
        public bool DebugLogging = false;

        /// <summary>Override the Gamebeast API base URL. Leave as-is for production use.</summary>
        public string ApiUrl = "https://api.gamebeast.gg";
    }
}
