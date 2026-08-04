using System.Collections.Generic;
using Newtonsoft.Json;

namespace Gamebeast.Internal.Models
{
    /// <summary>
    /// Response body of GET /sdk/v2/experiments/active. Used only to enrich the
    /// assignments echoed on evaluated configurations with display names; the
    /// per-group changesets in the payload are ignored (the backend merges them
    /// into evaluated configurations for us).
    /// </summary>
    internal sealed class SdkActiveExperimentsV2Response
    {
        [JsonProperty("hash")]
        public string Hash;

        [JsonProperty("requiredProperties")]
        public List<string> RequiredProperties;

        [JsonProperty("experiments")]
        public List<ActiveExperimentModel> Experiments;
    }

    internal sealed class ActiveExperimentModel
    {
        [JsonProperty("id")]
        public long Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("unitType")]
        public string UnitType;

        [JsonProperty("baseConfigurationId")]
        public long BaseConfigurationId;

        [JsonProperty("groups")]
        public List<ActiveExperimentGroupModel> Groups;
    }

    internal sealed class ActiveExperimentGroupModel
    {
        [JsonProperty("id")]
        public long Id;

        [JsonProperty("label")]
        public string Label;
    }

    internal sealed class ActiveExperimentsFetchResult
    {
        public ConfigurationFetchStatus Status;
        public SdkActiveExperimentsV2Response Response;
        public string Error;

        public static ActiveExperimentsFetchResult Updated(SdkActiveExperimentsV2Response response) =>
            new ActiveExperimentsFetchResult { Status = ConfigurationFetchStatus.Updated, Response = response };

        public static ActiveExperimentsFetchResult NotModified() =>
            new ActiveExperimentsFetchResult { Status = ConfigurationFetchStatus.NotModified };

        public static ActiveExperimentsFetchResult Failed(string error) =>
            new ActiveExperimentsFetchResult { Status = ConfigurationFetchStatus.Failed, Error = error };
    }
}
