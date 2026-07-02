using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamebeast.Internal.Models
{
    /// <summary>Response body of GET /sdk/v2/configurations.</summary>
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

        /// <summary>Paths (as key segments) flagged as private in the dashboard.</summary>
        [JsonProperty("privacy")]
        public List<List<string>> Privacy;

        [JsonProperty("updatedAt")]
        public DateTimeOffset UpdatedAt;
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
