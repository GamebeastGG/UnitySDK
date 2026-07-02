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
        public DistinctIdentity Identity { get; }
        public GamebeastApiClient Api { get; }

        /// <summary>Environment alias sent to the backend: "production", "development" or "studio".</summary>
        public string EnvironmentAlias { get; }

        /// <summary>Unique id for this app run; groups markers from the same session.</summary>
        public string SessionId { get; }

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
    }
}
