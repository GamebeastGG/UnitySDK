using System.Collections.Generic;
using Newtonsoft.Json;

namespace Gamebeast.Internal.Models
{
    /// <summary>Wire format for a single marker (POST /sdk/v1/markers).</summary>
    internal sealed class MarkerPayload
    {
        [JsonProperty("markerId")]
        public string MarkerId;

        [JsonProperty("timestamp")]
        public long Timestamp;

        [JsonProperty("eventName")]
        public string EventName;

        [JsonProperty("sessionId")]
        public string SessionId;

        [JsonProperty("distinctId")]
        public string DistinctId;

        [JsonProperty("properties")]
        public object Properties;
    }

    internal sealed class MarkerBatch
    {
        [JsonProperty("markers")]
        public List<MarkerPayload> Markers;
    }
}
