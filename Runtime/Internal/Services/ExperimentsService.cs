using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gamebeast.Internal.Models;
using Gamebeast.Internal.Utils;

namespace Gamebeast.Internal.Services
{
    /// <summary>
    /// Tracks the experiment assignments echoed on evaluated configuration responses
    /// and exposes them through <see cref="IExperimentsService"/>.
    ///
    /// Assignments only carry ids on the wire; display names are enriched from
    /// GET /sdk/v2/experiments/active, fetched lazily (hash-cached) whenever an
    /// unknown experiment id appears. Assignment resolution itself happens on the
    /// backend during configuration evaluation — this service is read-only state.
    /// </summary>
    internal sealed class ExperimentsService : IExperimentsService, IGamebeastService
    {
        private GamebeastContext _context;

        // Assignments as last reported per configuration alias.
        private readonly Dictionary<string, List<ConfigurationExperimentAssignment>> _assignmentsByConfig =
            new Dictionary<string, List<ConfigurationExperimentAssignment>>(StringComparer.OrdinalIgnoreCase);

        // Catalog of active experiments (names/labels), keyed by experiment id.
        private readonly Dictionary<long, ActiveExperimentModel> _catalog =
            new Dictionary<long, ActiveExperimentModel>();
        private string _catalogHash;
        private Task _catalogRefreshInFlight;

        private readonly Subscribeable<IReadOnlyList<ExperimentAssignment>> _onChanged =
            new Subscribeable<IReadOnlyList<ExperimentAssignment>>();

        private List<ExperimentAssignment> _current = new List<ExperimentAssignment>();
        private bool _rebuildPending;

        public IReadOnlyList<ExperimentAssignment> Assignments => _current;

        public IDisposable OnAssignmentsChanged(Action<IReadOnlyList<ExperimentAssignment>> callback)
        {
            if (callback == null)
            {
                GBLog.Error("OnAssignmentsChanged called with a null callback.");
                return null;
            }

            return _onChanged.Subscribe(callback);
        }

        // --- IGamebeastService ---------------------------------------------------

        public void Initialize(GamebeastContext context)
        {
            _context = context;

            // Disk-cached configurations may have reported assignments before we
            // had a context to enrich them with; catch up now.
            if (_rebuildPending)
            {
                KickOffRebuild();
            }
        }

        public void Tick(float deltaTime)
        {
        }

        public void OnApplicationPause(bool paused)
        {
        }

        public void OnIdentityChanged()
        {
            // Assignments belong to the previous user; clear now and repopulate as
            // configurations re-evaluated for the new user land.
            if (_assignmentsByConfig.Count == 0) return;

            _assignmentsByConfig.Clear();
            KickOffRebuild();
        }

        public void Shutdown()
        {
        }

        // --- Internal feed from ConfigsService -----------------------------------

        /// <summary>
        /// Called by ConfigsService with the assignments echoed on an evaluated
        /// configuration response (cached or fresh). Replaces that configuration's
        /// assignment set wholesale.
        /// </summary>
        internal void ReportAssignments(string configAlias, List<ConfigurationExperimentAssignment> assignments)
        {
            var newList = assignments ?? new List<ConfigurationExperimentAssignment>();

            if (_assignmentsByConfig.TryGetValue(configAlias, out var existing) && SameAssignments(existing, newList))
            {
                return;
            }

            _assignmentsByConfig[configAlias] = newList;
            KickOffRebuild();
        }

        // --- Snapshot building ---------------------------------------------------

        private async void KickOffRebuild()
        {
            // Before Initialize we cannot fetch the catalog; remember to catch up.
            if (_context == null)
            {
                _rebuildPending = true;
                return;
            }
            _rebuildPending = false;

            try
            {
                await EnsureCatalogAsync();
            }
            catch (Exception ex)
            {
                GBLog.Warn($"Could not refresh experiment catalog: {ex.Message}");
            }

            _current = BuildSnapshot();
            _onChanged.Trigger(_current);
        }

        /// <summary>Fetches the active experiment catalog when an unknown experiment id appears.</summary>
        private Task EnsureCatalogAsync()
        {
            var hasUnknown = false;
            foreach (var assignments in _assignmentsByConfig.Values)
            {
                foreach (var assignment in assignments)
                {
                    if (!_catalog.ContainsKey(assignment.ExperimentId))
                    {
                        hasUnknown = true;
                        break;
                    }
                }
                if (hasUnknown) break;
            }

            if (!hasUnknown)
            {
                return Task.CompletedTask;
            }

            if (_catalogRefreshInFlight != null)
            {
                return _catalogRefreshInFlight;
            }

            _catalogRefreshInFlight = DoRefreshCatalog();
            return _catalogRefreshInFlight;
        }

        private async Task DoRefreshCatalog()
        {
            try
            {
                var result = await _context.Api.GetActiveExperimentsAsync(_catalogHash);
                switch (result.Status)
                {
                    case ConfigurationFetchStatus.Updated:
                        _catalogHash = result.Response.Hash;
                        _catalog.Clear();
                        if (result.Response.Experiments != null)
                        {
                            foreach (var experiment in result.Response.Experiments)
                            {
                                _catalog[experiment.Id] = experiment;
                            }
                        }
                        GBLog.Info($"Experiment catalog updated ({_catalog.Count} active).");
                        break;

                    case ConfigurationFetchStatus.NotModified:
                        break;

                    default:
                        GBLog.Warn($"Experiment catalog fetch failed ({result.Error}); " +
                                   "assignments will surface without names until it succeeds.");
                        break;
                }
            }
            finally
            {
                _catalogRefreshInFlight = null;
            }
        }

        private List<ExperimentAssignment> BuildSnapshot()
        {
            var snapshot = new List<ExperimentAssignment>();

            foreach (var pair in _assignmentsByConfig)
            {
                foreach (var assignment in pair.Value)
                {
                    _catalog.TryGetValue(assignment.ExperimentId, out var experiment);

                    string groupLabel = null;
                    if (experiment?.Groups != null)
                    {
                        foreach (var group in experiment.Groups)
                        {
                            if (group.Id == assignment.GroupId)
                            {
                                groupLabel = group.Label;
                                break;
                            }
                        }
                    }

                    snapshot.Add(new ExperimentAssignment
                    {
                        ExperimentId = assignment.ExperimentId,
                        ExperimentName = experiment?.Name,
                        GroupId = assignment.GroupId,
                        GroupLabel = groupLabel,
                        ConfigurationAlias = pair.Key,
                        Source = assignment.Source,
                    });
                }
            }

            snapshot.Sort((a, b) => a.ExperimentId.CompareTo(b.ExperimentId));
            return snapshot;
        }

        private static bool SameAssignments(
            List<ConfigurationExperimentAssignment> a,
            List<ConfigurationExperimentAssignment> b)
        {
            if (a.Count != b.Count) return false;

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i].ExperimentId != b[i].ExperimentId
                    || a[i].GroupId != b[i].GroupId
                    || a[i].AssignmentVersion != b[i].AssignmentVersion)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
