using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamebeast.Runtime.Internal.Models
{
    // Root response: an array of requests.
    public sealed class RemoteRequest
    {
        [JsonProperty("requestId")]
        public long RequestId { get; set; }

        [JsonProperty("requestType")]
        public string RequestType { get; set; }

        [JsonProperty("args")]
        public RemoteRequestArgs Args { get; set; }

        [JsonProperty("details")]
        public RemoteRequestDetails Details { get; set; }

        [JsonProperty("constraints")]
        public RemoteRequestConstraints Constraints { get; set; }

        // Allows forward-compat if backend adds fields.
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    public sealed class RemoteRequestArgs
    {
        // This is your "configs" payload; it can vary over time.
        // We model the known keys but keep an extension bag for unknown ones.
        [JsonProperty("configs")]
        public IDictionary<string, JToken> Configs { get; set; }

        [JsonProperty("options")]
        public RemoteRequestOptions Options { get; set; }

        [JsonProperty("GBConfigs")]
        public GbConfigs GBConfigs { get; set; }

        [JsonProperty("environmentId")]
        public int EnvironmentId { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    public sealed class RemoteRequestOptions
    {
        [JsonProperty("privacy")]
        public List<string> Privacy { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    public sealed class GbConfigs
    {
        [JsonProperty("GBRates")]
        public GbRates GBRates { get; set; }

        // Unknown/empty object in your sample. Keep as JObject for flexibility.
        [JsonProperty("EventData")]
        public JObject EventData { get; set; }

        [JsonProperty("Experiments")]
        public GbExperiments Experiments { get; set; }

        [JsonProperty("GBPublishTime")]
        public long GBPublishTime { get; set; }

        [JsonProperty("HeatmapMetadata")]
        public HeatmapMetadata HeatmapMetadata { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    public sealed class GbRates
    {
        [JsonProperty("CheckRequests")]
        public int CheckRequests { get; set; }

        [JsonProperty("EngagementMarkers")]
        public int EngagementMarkers { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    public sealed class GbExperiments
    {
        [JsonProperty("groups")]
        public List<JToken> Groups { get; set; }

        [JsonProperty("scheduled")]
        public JObject Scheduled { get; set; }

        [JsonProperty("timestampMs")]
        public long TimestampMs { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    public sealed class HeatmapMetadata
    {
        [JsonProperty("size")]
        public List<double> Size { get; set; }

        [JsonProperty("center")]
        public List<double> Center { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    public sealed class RemoteRequestDetails
    {
        [JsonProperty("async")]
        public bool Async { get; set; }

        [JsonProperty("hostOnly")]
        public bool HostOnly { get; set; }

        [JsonProperty("custom")]
        public bool Custom { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }

    public sealed class RemoteRequestConstraints
    {
        [JsonProperty("startTime")]
        public DateTimeOffset StartTime { get; set; }

        [JsonProperty("endTime")]
        public DateTimeOffset? EndTime { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }
    }
}
