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

        /// <summary>Kept in sync by the runtime when the session rotates.</summary>
        public string SessionId { get; set; }

        public GamebeastApiClient(GamebeastSettings settings, string environmentAlias, string sessionId)
        {
            _baseUrl = string.IsNullOrWhiteSpace(settings.ApiUrl)
                ? "https://api.gamebeast.gg"
                : settings.ApiUrl.TrimEnd('/');
            _apiKey = settings.ApiKey;
            _environmentAlias = environmentAlias;
            SessionId = sessionId;
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
        /// POST /sdk/v2/configurations/evaluate — fetch a configuration by alias.
        /// The backend evaluates it for the local user: active experiment assignments
        /// are resolved (persisted, exposure recorded) and the winning group's changes
        /// arrive already merged into the document, with the applied assignments echoed
        /// alongside. Passing the hash of the copy we already hold lets the server skip
        /// the payload when nothing changed.
        /// </summary>
        public async Task<ConfigurationFetchResult> GetConfigurationAsync(
            string configurationAlias,
            string knownHash,
            string distinctId,
            Dictionary<string, object> contextProperties)
        {
            if (string.IsNullOrWhiteSpace(configurationAlias))
            {
                return ConfigurationFetchResult.Failed("configuration alias is required");
            }

            var body = new ConfigurationEvaluateRequest
            {
                Configuration = configurationAlias,
                Unit = new EvaluationUnit { DistinctId = distinctId },
                Context = contextProperties != null && contextProperties.Count > 0
                    ? new EvaluationContext { Properties = contextProperties }
                    : null,
                KnownHash = string.IsNullOrEmpty(knownHash) ? null : knownHash,
            };

            var response = await HttpRequester.SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                _baseUrl + "/sdk/v2/configurations/evaluate",
                body: body,
                headers: BuildHeaders(includeLegacyV1Headers: false));

            if (response.TransportError != null)
            {
                return ConfigurationFetchResult.Failed(response.TransportError);
            }

            if (response.IsNotModified)
            {
                return ConfigurationFetchResult.NotModified();
            }

            if (response.StatusCode == 404 || response.StatusCode == 400)
            {
                var error = HttpRequester.TryParse<SdkV2ErrorResponse>(response);
                if (response.StatusCode == 404 || (error != null && error.ErrorCode == "CONFIGURATION_NOT_FOUND"))
                {
                    var message = error != null ? $"{error.ErrorCode}: {error.Message}" : "configuration not found";
                    return ConfigurationFetchResult.NotFound(message);
                }
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

        /// <summary>
        /// GET /sdk/v2/experiments/active — the catalog of active user-unit experiments
        /// (names and group labels), used to enrich assignments for display. Hash-based
        /// change detection mirrors the configurations endpoint.
        /// </summary>
        public async Task<ActiveExperimentsFetchResult> GetActiveExperimentsAsync(string knownHash)
        {
            var query = new Dictionary<string, string>
            {
                { "unit-type", "user" },
            };
            if (!string.IsNullOrEmpty(knownHash))
            {
                query["hash"] = knownHash;
            }

            var response = await HttpRequester.SendAsync(
                UnityWebRequest.kHttpVerbGET,
                _baseUrl + "/sdk/v2/experiments/active",
                query: query,
                headers: BuildHeaders(includeLegacyV1Headers: false));

            if (response.TransportError != null)
            {
                return ActiveExperimentsFetchResult.Failed(response.TransportError);
            }

            if (response.IsNotModified)
            {
                return ActiveExperimentsFetchResult.NotModified();
            }

            if (!response.IsSuccess)
            {
                return ActiveExperimentsFetchResult.Failed($"{response.Describe()} {Truncate(response.Body)}");
            }

            var parsed = HttpRequester.TryParse<SdkActiveExperimentsV2Response>(response);
            if (parsed == null)
            {
                return ActiveExperimentsFetchResult.Failed("could not parse active experiments response");
            }

            if (!string.IsNullOrEmpty(knownHash) && parsed.Hash == knownHash)
            {
                return ActiveExperimentsFetchResult.NotModified();
            }

            return ActiveExperimentsFetchResult.Updated(parsed);
        }

        /// <summary>
        /// POST /sdk/v1/cohorts/membership — resolve whether the given user is a member
        /// of the named cohort. Rate-limited by the backend (callers should cache).
        /// Returns null on transport/protocol failure.
        /// </summary>
        public async Task<List<CohortMembershipEntry>> CheckCohortMembershipAsync(string cohortName, string userId)
        {
            var response = await HttpRequester.SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                _baseUrl + "/sdk/v1/cohorts/membership",
                body: new CohortMembershipRequest
                {
                    CohortName = cohortName,
                    UserIds = new List<string> { userId },
                },
                headers: BuildHeaders(includeLegacyV1Headers: true));

            if (!response.IsSuccess)
            {
                GBLog.Warn($"Cohort membership check failed: {response.Describe()} {Truncate(response.Body)}");
                return null;
            }

            return HttpRequester.TryParse<List<CohortMembershipEntry>>(response);
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
                headers["serverid"] = SessionId;
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
