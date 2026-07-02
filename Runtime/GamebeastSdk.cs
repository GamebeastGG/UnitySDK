using Gamebeast.Internal;

namespace Gamebeast
{
    /// <summary>
    /// Entry point for the Gamebeast SDK.
    ///
    /// Usage:
    /// <code>
    /// GamebeastSdk.Setup(new GamebeastSettings
    /// {
    ///     ApiKey = "your-api-key",
    ///     Configurations = new[] { "ConfigA" },
    /// });
    /// GamebeastSdk.Markers.SendMarker("level_completed", new { level = 3 });
    /// var speed = GamebeastSdk.Configs.Get&lt;float&gt;("ConfigA.PlayerSpeed");
    /// </code>
    /// </summary>
    public static class GamebeastSdk
    {
        /// <summary>Initialize the SDK with just an API key and default settings.</summary>
        public static void Setup(string apiKey)
        {
            Setup(new GamebeastSettings { ApiKey = apiKey });
        }

        /// <summary>
        /// Initialize the SDK. Safe to call once at startup; subsequent calls are ignored.
        /// </summary>
        public static void Setup(GamebeastSettings settings)
        {
            GamebeastRuntime.Instance.Setup(settings);
        }

        /// <summary>True once <see cref="Setup(GamebeastSettings)"/> has completed.</summary>
        public static bool IsInitialized => GamebeastRuntime.IsInitialized;

        /// <summary>
        /// The effective distinct id for this session: the id supplied in settings, or the
        /// anonymous id generated and persisted by the SDK. Null before Setup.
        /// </summary>
        public static string DistinctId => GamebeastRuntime.IsInitialized
            ? GamebeastRuntime.Instance.Context.Identity.DistinctId
            : null;

        /// <summary>Sends engagement markers (analytics events).</summary>
        public static IMarkersService Markers => GetService<IMarkersService>();

        /// <summary>Reads remote configurations.</summary>
        public static IConfigsService Configs => GetService<IConfigsService>();

        /// <summary>
        /// Resolve a Gamebeast service by its public interface,
        /// e.g. GamebeastSdk.GetService&lt;IMarkersService&gt;().
        /// </summary>
        public static TService GetService<TService>() where TService : class
        {
            return GamebeastRuntime.Instance.GetService<TService>();
        }
    }
}
