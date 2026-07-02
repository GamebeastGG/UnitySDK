using System.Collections.Generic;
using System.Threading.Tasks;
using Gamebeast.Internal.Models;
using UnityEngine.Networking;

namespace Gamebeast.Internal.Http
{
    /// <summary>
    /// Typed access to the Gamebeast SDK REST API. Owns base URL, auth and
    /// environment headers; one instance per SDK session.
    /// </summary>
    internal sealed class GamebeastApiClient
    {
        public const string SdkVersion = "0.1.0";

        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _environmentAlias;
        private readonly string _sessionId;

        public GamebeastApiClient(GamebeastSettings settings, string environmentAlias, string sessionId)
        {
            _baseUrl = string.IsNullOrWhiteSpace(settings.ApiUrl)
                ? "https://api.gamebeast.gg"
                : settings.ApiUrl.TrimEnd('/');
            _apiKey = settings.ApiKey;
            _environmentAlias = environmentAlias;
            _sessionId = sessionId;
        }

        /// <summary>POST /sdk/v1/markers — send a batch of engagement markers.</summary>
        public Task<GBHttpResponse> PostMarkersAsync(MarkerBatch batch)
        {
            return HttpRequester.SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                _baseUrl + "/sdk/v1/markers",
                body: batch,
                headers: BuildHeaders(includeLegacyV1Headers: true));
        }

        /// <summary>
        /// GET /sdk/v2/configurations — fetch a configuration by alias (required).
        /// Passing the hash of the copy we already hold lets the server skip the payload
        /// when nothing changed.
        /// </summary>
        public async Task<ConfigurationFetchResult> GetConfigurationAsync(string configurationAlias, string knownHash)
        {
            if (string.IsNullOrWhiteSpace(configurationAlias))
            {
                return ConfigurationFetchResult.Failed("configuration alias is required");
            }

            var query = new Dictionary<string, string>
            {
                { "configuration", configurationAlias },
            };
            if (!string.IsNullOrEmpty(knownHash))
            {
                query["hash"] = knownHash;
            }

            var response = await HttpRequester.SendAsync(
                UnityWebRequest.kHttpVerbGET,
                _baseUrl + "/sdk/v2/configurations",
                query: query,
                headers: BuildHeaders(includeLegacyV1Headers: false));

            if (response.TransportError != null)
            {
                return ConfigurationFetchResult.Failed(response.TransportError);
            }

            if (response.IsNotModified)
            {
                return ConfigurationFetchResult.NotModified();
            }

            if (response.StatusCode == 404)
            {
                var error = HttpRequester.TryParse<SdkV2ErrorResponse>(response);
                var message = error != null ? $"{error.ErrorCode}: {error.Message}" : "configuration not found";
                return ConfigurationFetchResult.NotFound(message);
            }

            if (!response.IsSuccess)
            {
                return ConfigurationFetchResult.Failed($"{response.Describe()} {Truncate(response.Body)}");
            }

            var parsed = HttpRequester.TryParse<SdkConfigurationV2Response>(response);
            if (parsed == null || string.IsNullOrEmpty(parsed.Hash))
            {
                return ConfigurationFetchResult.Failed("could not parse configuration response");
            }

            // Some servers answer a matching hash with a 200 echoing the same hash
            // rather than a 304; treat both as "unchanged".
            if (!string.IsNullOrEmpty(knownHash) && parsed.Hash == knownHash)
            {
                return ConfigurationFetchResult.NotModified();
            }

            return ConfigurationFetchResult.Updated(parsed);
        }

        private Dictionary<string, string> BuildHeaders(bool includeLegacyV1Headers)
        {
            var headers = new Dictionary<string, string>
            {
                { "authorization", _apiKey },
                { "sdkversion", SdkVersion },
                { "environment", _environmentAlias },
            };

            if (includeLegacyV1Headers)
            {
                // The v1 markers route still expects these Roblox-era headers.
                headers["universeid"] = "0";
                headers["serverid"] = _sessionId;
                headers["isstudio"] = _environmentAlias == "studio" ? "true" : "false";
            }

            return headers;
        }

        private static string Truncate(string body)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;
            return body.Length <= 200 ? body : body.Substring(0, 200) + "…";
        }
    }
}
