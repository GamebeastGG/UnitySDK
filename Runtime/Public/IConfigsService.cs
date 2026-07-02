using System;
using System.Threading.Tasks;

namespace Gamebeast
{
    /// <summary>
    /// Access to remote configurations published from the Gamebeast dashboard.
    ///
    /// Configurations load on demand: the first access through Get/TryGet/OnChanged/
    /// Observe fetches the configuration automatically. Listing configurations in
    /// <see cref="GamebeastSettings.Configurations"/> additionally preloads them at
    /// startup and lets <see cref="IsReady"/>/<see cref="OnReady"/> await them. All
    /// configurations are cached on-device and kept up to date with hash-based refreshes.
    ///
    /// Values are addressed by dot-path where the first segment is the configuration
    /// alias: "ConfigA.PlayerSpeed" reads the "PlayerSpeed" key of the "ConfigA"
    /// configuration. Paths can reach deeper ("ConfigA.UI.ButtonColor") and
    /// index arrays ("ConfigA.Levels.0"). A bare alias ("ConfigA")
    /// returns the whole configuration document.
    /// </summary>
    public interface IConfigsService
    {
        /// <summary>
        /// True once every configuration declared in <see cref="GamebeastSettings.Configurations"/>
        /// is available (from cache or the network). Lazily fetched configurations do not
        /// affect readiness; use <see cref="Observe{TValue}"/> to wait for those.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Get the value at <paramref name="configPath"/> converted to <typeparamref name="TValue"/>.
        /// Returns default(TValue) when the path is missing or conversion fails. If the
        /// configuration has not been fetched yet, the fetch starts automatically and this
        /// returns default(TValue) until it lands (prefer <see cref="Observe{TValue}"/> for that case).
        /// </summary>
        TValue Get<TValue>(string configPath);

        /// <summary>
        /// Get the value at <paramref name="configPath"/>, falling back to
        /// <paramref name="defaultValue"/> when the path is missing or conversion fails.
        /// </summary>
        TValue Get<TValue>(string configPath, TValue defaultValue);

        /// <summary>Try to get the value at <paramref name="configPath"/>. Returns false when missing or conversion fails.</summary>
        bool TryGet<TValue>(string configPath, out TValue value);

        /// <summary>
        /// Invoke <paramref name="callback"/> once all configurations declared in
        /// <see cref="GamebeastSettings.Configurations"/> are ready. Fires immediately if
        /// already ready (or when nothing was declared). The subscription disposes itself after firing.
        /// </summary>
        IDisposable OnReady(Action callback);

        /// <summary>
        /// Invoke <paramref name="callback"/> whenever the value at <paramref name="configPath"/> changes.
        /// Does not fire for the initial load; use <see cref="Observe{TValue}"/> for that.
        /// </summary>
        IDisposable OnChanged(string configPath, Action callback);

        /// <summary>
        /// Invoke <paramref name="callback"/> with the current value as soon as it is
        /// available, and again with the new value each time it changes.
        /// </summary>
        IDisposable Observe<TValue>(string configPath, Action<TValue> callback);

        /// <summary>
        /// Force a refresh of all known configurations now, in addition to the
        /// automatic background refresh.
        /// </summary>
        Task RefreshAsync();
    }
}
