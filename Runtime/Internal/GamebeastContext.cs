using Gamebeast.Internal.Http;

namespace Gamebeast.Internal
{
    /// <summary>
    /// Shared state handed to every service at initialization: resolved settings,
    /// user identity, session info and the API client.
    /// </summary>
    internal sealed class GamebeastContext
    {
        public GamebeastSettings Settings { get; }

        /// <summary>
        /// The local user's identity. Swappable at runtime via
        /// <see cref="GamebeastSdk.Identify"/> / <see cref="GamebeastSdk.ResetIdentity"/>;
        /// services read it live rather than caching the distinct id.
        /// </summary>
        public DistinctIdentity Identity { get; private set; }

        public GamebeastApiClient Api { get; }

        /// <summary>Environment alias sent to the backend: "production" or "development".</summary>
        public string EnvironmentAlias { get; }

        /// <summary>
        /// Unique id grouping markers from the same session. Rotated by the runtime
        /// when the app returns from a long backgrounding (SessionTimeoutSeconds).
        /// </summary>
        public string SessionId { get; private set; }

        public GamebeastContext(
            GamebeastSettings settings,
            DistinctIdentity identity,
            GamebeastApiClient api,
            string environmentAlias,
            string sessionId)
        {
            Settings = settings;
            Identity = identity;
            Api = api;
            EnvironmentAlias = environmentAlias;
            SessionId = sessionId;
        }

        public void SetIdentity(DistinctIdentity identity)
        {
            Identity = identity;
        }

        public void SetSessionId(string sessionId)
        {
            SessionId = sessionId;
        }
    }
}
