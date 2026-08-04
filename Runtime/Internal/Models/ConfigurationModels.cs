using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamebeast.Internal.Models
{
    /// <summary>
    /// Response body of POST /sdk/v2/configurations/evaluate (and the evaluated GET).
    /// When identity is supplied the configuration document arrives with active
    /// experiment changesets already merged in, plus the assignments that produced it.
    /// </summary>
    internal sealed class SdkConfigurationV2Response
    {
        [JsonProperty("hash")]
        public string Hash;

        [JsonProperty("configurationId")]
        public long ConfigurationId;

        [JsonProperty("name")]
        public string Name;

        /// <summary>The configuration document itself (arbitrary JSON, normally an object).</summary>
        [JsonProperty("configuration")]
        public JToken Configuration;

        /// <summary>
        /// Paths (as key segments) flagged as private in the dashboard. Informational:
        /// privacy is enforced by the backend per key type — SDK API keys are publicly
        /// accessible, so private values never reach this SDK in the first place.
        /// </summary>
        [JsonProperty("privacy")]
        public List<List<string>> Privacy;

        [JsonProperty("updatedAt")]
        public DateTimeOffset UpdatedAt;

        /// <summary>Experiment assignments applied to this evaluated configuration, if any.</summary>
        [JsonProperty("experiments")]
        public List<ConfigurationExperimentAssignment> Experiments;

        /// <summary>Context property names that active experiments target on.</summary>
        [JsonProperty("requiredProperties")]
        public List<string> RequiredProperties;
    }

    /// <summary>An experiment assignment echoed on an evaluated configuration response.</summary>
    internal sealed class ConfigurationExperimentAssignment
    {
        [JsonProperty("experimentId")]
        public long ExperimentId;

        [JsonProperty("groupId")]
        public long GroupId;

        [JsonProperty("assignmentVersion")]
        public long AssignmentVersion;

        /// <summary>"weightedHash", "roundRobin" or "manual".</summary>
        [JsonProperty("source")]
        public string Source;

        [JsonProperty("exposureProxyRecorded")]
        public bool ExposureProxyRecorded;
    }

    /// <summary>Request body of POST /sdk/v2/configurations/evaluate.</summary>
    internal sealed class ConfigurationEvaluateRequest
    {
        [JsonProperty("configuration")]
        public string Configuration;

        [JsonProperty("unit")]
        public EvaluationUnit Unit;

        [JsonProperty("context", NullValueHandling = NullValueHandling.Ignore)]
        public EvaluationContext Context;

        [JsonProperty("knownHash", NullValueHandling = NullValueHandling.Ignore)]
        public string KnownHash;
    }

    /// <summary>The unit (always the local user, for this SDK) a configuration is evaluated for.</summary>
    internal sealed class EvaluationUnit
    {
        [JsonProperty("type")]
        public string Type = "user";

        [JsonProperty("distinctId")]
        public string DistinctId;
    }

    /// <summary>Targeting context sent with evaluation requests.</summary>
    internal sealed class EvaluationContext
    {
        [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> Properties;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion = 1;
    }

    /// <summary>Error body returned by v2 SDK endpoints (e.g. unknown configuration alias).</summary>
    internal sealed class SdkV2ErrorResponse
    {
        [JsonProperty("errorCode")]
        public string ErrorCode;

        [JsonProperty("message")]
        public string Message;
    }

    internal enum ConfigurationFetchStatus
    {
        /// <summary>New configuration data was returned.</summary>
        Updated,

        /// <summary>The server (or hash comparison) confirmed our cached copy is current.</summary>
        NotModified,

        /// <summary>The requested configuration alias does not exist.</summary>
        NotFound,

        /// <summary>Transport or server failure; safe to retry later.</summary>
        Failed,
    }

    internal sealed class ConfigurationFetchResult
    {
        public ConfigurationFetchStatus Status;
        public SdkConfigurationV2Response Response;
        public string Error;

        public static ConfigurationFetchResult Updated(SdkConfigurationV2Response response) =>
            new ConfigurationFetchResult { Status = ConfigurationFetchStatus.Updated, Response = response };

        public static ConfigurationFetchResult NotModified() =>
            new ConfigurationFetchResult { Status = ConfigurationFetchStatus.NotModified };

        public static ConfigurationFetchResult NotFound(string error) =>
            new ConfigurationFetchResult { Status = ConfigurationFetchStatus.NotFound, Error = error };

        public static ConfigurationFetchResult Failed(string error) =>
            new ConfigurationFetchResult { Status = ConfigurationFetchStatus.Failed, Error = error };
    }
}
