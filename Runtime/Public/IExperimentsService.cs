using System;
using System.Collections.Generic;

namespace Gamebeast
{
    /// <summary>
    /// An active experiment assignment for the local user. Assignments are resolved by
    /// the backend whenever a configuration is evaluated; the values you read through
    /// <see cref="IConfigsService"/> already include the assigned group's changes.
    /// </summary>
    public sealed class ExperimentAssignment
    {
        public long ExperimentId { get; internal set; }

        /// <summary>Display name of the experiment. Null until the experiment catalog has loaded.</summary>
        public string ExperimentName { get; internal set; }

        public long GroupId { get; internal set; }

        /// <summary>Display label of the assigned group. Null until the experiment catalog has loaded.</summary>
        public string GroupLabel { get; internal set; }

        /// <summary>Alias of the configuration the experiment applies to.</summary>
        public string ConfigurationAlias { get; internal set; }

        /// <summary>"weightedHash", "roundRobin" or "manual".</summary>
        public string Source { get; internal set; }
    }

    /// <summary>
    /// Read access to the experiments the local user is currently enrolled in.
    ///
    /// Enrollment happens automatically: fetching a configuration evaluates active
    /// experiments for this user on the backend, and any applied assignments surface
    /// here. There is nothing to opt into — this service exists for display,
    /// analytics segmentation, and debugging.
    /// </summary>
    public interface IExperimentsService
    {
        /// <summary>
        /// The user's active experiment assignments across all fetched configurations.
        /// Empty until the first evaluated configuration lands.
        /// </summary>
        IReadOnlyList<ExperimentAssignment> Assignments { get; }

        /// <summary>
        /// Invoke <paramref name="callback"/> with the full assignment list whenever it
        /// changes (including when names finish resolving from the experiment catalog).
        /// </summary>
        IDisposable OnAssignmentsChanged(Action<IReadOnlyList<ExperimentAssignment>> callback);
    }
}
