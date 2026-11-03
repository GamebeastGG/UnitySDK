using System.Collections.Generic;
using Newtonsoft.Json;

namespace Gamebeast.Internal.Models
{
    /// <summary>Request body of POST /sdk/v1/cohorts/membership.</summary>
    internal sealed class CohortMembershipRequest
    {
        [JsonProperty("cohortName")]
        public string CohortName;

        [JsonProperty("userIds")]
        public List<string> UserIds;
    }

    /// <summary>One entry of the membership response.</summary>
    internal sealed class CohortMembershipEntry
    {
        [JsonProperty("userId")]
        public string UserId;

        [JsonProperty("isMember")]
        public bool IsMember;
    }

    /// <summary>Response body of POST /sdk/v1/cohorts/membership.</summary>
    internal sealed class CohortMembershipResponse
    {
        /// <summary>False when no cohort with that name exists (every user is then a non-member).</summary>
        [JsonProperty("cohortExists")]
        public bool CohortExists;

        [JsonProperty("users")]
        public List<CohortMembershipEntry> Users;
    }
}
