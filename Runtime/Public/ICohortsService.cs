using System.Threading.Tasks;

namespace Gamebeast
{
    /// <summary>
    /// Checks the local user's membership in cohorts defined on the Gamebeast
    /// dashboard (segments computed from analytics, e.g. "high_spenders").
    ///
    /// Results are cached briefly on-device; membership is recomputed by the
    /// backend, so treat it as eventually consistent rather than real-time.
    /// </summary>
    public interface ICohortsService
    {
        /// <summary>
        /// Whether the local user is a member of the named cohort. Returns false when
        /// the cohort does not exist or the check fails (failures are logged).
        /// </summary>
        Task<bool> IsMemberAsync(string cohortName);
    }
}
