using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gamebeast.Internal.Services
{
    /// <summary>
    /// Cohort membership checks for the local user, backed by
    /// POST /sdk/v1/cohorts/membership. The endpoint is rate-limited per SDK key,
    /// so results are cached for a short TTL and concurrent checks for the same
    /// cohort share one request.
    /// </summary>
    internal sealed class CohortsService : ICohortsService, IGamebeastService
    {
        private const float CacheTtlSeconds = 60f;

        private sealed class CacheEntry
        {
            public bool IsMember;
            public DateTime FetchedAtUtc;
        }

        private GamebeastContext _context;

        private readonly Dictionary<string, CacheEntry> _cache =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<bool>> _inFlight =
            new Dictionary<string, Task<bool>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Bumped on identity swaps so results for the previous user are discarded.</summary>
        private int _identityGeneration;

        public Task<bool> IsMemberAsync(string cohortName)
        {
            if (string.IsNullOrWhiteSpace(cohortName))
            {
                GBLog.Error("IsMemberAsync called without a cohort name.");
                return Task.FromResult(false);
            }

            if (_context == null)
            {
                GBLog.Error("IsMemberAsync called before Setup.");
                return Task.FromResult(false);
            }

            cohortName = cohortName.Trim();

            if (_cache.TryGetValue(cohortName, out var cached)
                && (DateTime.UtcNow - cached.FetchedAtUtc).TotalSeconds < CacheTtlSeconds)
            {
                return Task.FromResult(cached.IsMember);
            }

            if (_inFlight.TryGetValue(cohortName, out var inFlight))
            {
                return inFlight;
            }

            var task = CheckMembershipAsync(cohortName);
            _inFlight[cohortName] = task;
            return task;
        }

        private async Task<bool> CheckMembershipAsync(string cohortName)
        {
            try
            {
                var generation = _identityGeneration;
                var userId = _context.Identity.DistinctId;

                var entries = await _context.Api.CheckCohortMembershipAsync(cohortName, userId);
                if (entries == null)
                {
                    return false; // Failure already logged; not cached so the next call retries.
                }

                var isMember = false;
                foreach (var entry in entries)
                {
                    if (entry.UserId == userId)
                    {
                        isMember = entry.IsMember;
                        break;
                    }
                }

                // The identity changed while this request was in flight; the result
                // belongs to the previous user, so report but don't cache it.
                if (generation == _identityGeneration)
                {
                    _cache[cohortName] = new CacheEntry
                    {
                        IsMember = isMember,
                        FetchedAtUtc = DateTime.UtcNow,
                    };
                }

                return isMember;
            }
            catch (Exception ex)
            {
                GBLog.Error($"Error checking cohort '{cohortName}': {ex}");
                return false;
            }
            finally
            {
                _inFlight.Remove(cohortName);
            }
        }

        // --- IGamebeastService ---------------------------------------------------

        public void Initialize(GamebeastContext context)
        {
            _context = context;
        }

        public void Tick(float deltaTime)
        {
        }

        public void OnApplicationPause(bool paused)
        {
        }

        public void OnIdentityChanged()
        {
            // Membership is per-user; drop everything cached for the previous user.
            _identityGeneration++;
            _cache.Clear();
        }

        public void Shutdown()
        {
        }
    }
}
