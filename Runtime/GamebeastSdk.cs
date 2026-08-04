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
        /// Flush pending data (markers persist to disk first) and tear the SDK down.
        /// <see cref="Setup(GamebeastSettings)"/> may be called again afterwards —
        /// useful for tests and for reconfiguring the SDK at runtime.
        /// </summary>
        public static void Shutdown()
        {
            if (!GamebeastRuntime.IsInitialized) return;
            GamebeastRuntime.Instance.Shutdown();
        }

        /// <summary>
        /// The effective distinct id for this session: the id supplied in settings or via
        /// <see cref="Identify"/>, or the anonymous id generated and persisted by the SDK.
        /// Null before Setup.
        /// </summary>
        public static string DistinctId => GamebeastRuntime.IsInitialized
            ? GamebeastRuntime.Instance.Context.Identity.DistinctId
            : null;

        /// <summary>
        /// Switch the local user's distinct id (e.g. after login). Configurations are
        /// re-evaluated for the new user right away, and experiment assignments update
        /// with them. Markers recorded before the switch keep the id they were recorded
        /// under. The id is not persisted — call Identify again on the next launch once
        /// your login state has restored (or pass it in <see cref="GamebeastSettings.DistinctId"/>).
        /// </summary>
        public static void Identify(string distinctId)
        {
            GamebeastRuntime.Instance.Identify(distinctId);
        }

        /// <summary>
        /// Revert to the device's persisted anonymous distinct id (e.g. after logout).
        /// </summary>
        public static void ResetIdentity()
        {
            GamebeastRuntime.Instance.ResetIdentity();
        }

        /// <summary>Sends engagement markers (analytics events).</summary>
        public static IMarkersService Markers => GetService<IMarkersService>();

        /// <summary>Reads remote configurations, evaluated for the local user.</summary>
        public static IConfigsService Configs => GetService<IConfigsService>();

        /// <summary>Reads the experiments the local user is currently enrolled in.</summary>
        public static IExperimentsService Experiments => GetService<IExperimentsService>();

        /// <summary>Checks the local user's membership in dashboard-defined cohorts.</summary>
        public static ICohortsService Cohorts => GetService<ICohortsService>();

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
