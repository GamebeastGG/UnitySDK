using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gamebeast.Internal.Models;
using Gamebeast.Internal.Utils;
using Newtonsoft.Json.Linq;

namespace Gamebeast.Internal.Services
{
    /// <summary>
    /// Remote configurations backed by GET /sdk/v2/configurations.
    ///
    /// Each configuration gets its own state: last response cached on disk (stamped
    /// with the app version, so stale caches from older builds are discarded),
    /// refreshed at startup and on an interval using hash-based caching. Values are
    /// addressed by dot-path whose first segment is the configuration alias
    /// ("ConfigA.PlayerSpeed").
    ///
    /// Configurations declared in GamebeastSettings.Configurations are preloaded at
    /// startup and gate IsReady/OnReady; any other configuration is registered and
    /// fetched on demand the first time it is accessed.
    /// </summary>
    internal sealed class ConfigsService : IConfigsService, IGamebeastService
    {
        private sealed class ConfigState
        {
            public string Alias;
            public ConfigDiskCache Cache;
            public JObject Values;
            public string Hash;
            public bool Loaded;
            public Task RefreshInFlight;

            /// <summary>Declared in Setup: gates IsReady/OnReady. Lazily discovered configs do not.</summary>
            public bool RequiredForReady;
        }

        private sealed class ChangeListener : IDisposable
        {
            public string ConfigAlias;
            public string[] PathSegments;
            public Action<JToken> Callback;

            /// <summary>Observe-style listeners also fire when the config first loads.</summary>
            public bool IncludeInitial;

            private ConfigsService _owner;

            public ChangeListener(ConfigsService owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                _owner?._listeners.Remove(this);
                _owner = null;
            }
        }

        private GamebeastContext _context;

        private readonly Dictionary<string, ConfigState> _configs =
            new Dictionary<string, ConfigState>(StringComparer.OrdinalIgnoreCase);

        private readonly List<ChangeListener> _listeners = new List<ChangeListener>();
        private readonly Subscribeable<bool> _onReady = new Subscribeable<bool>();

        private bool _isReady;
        private float _timeSinceLastRefresh;

        public bool IsReady => _isReady;

        public void Initialize(GamebeastContext context)
        {
            _context = context;

            var declared = context.Settings.Configurations;
            if (declared != null)
            {
                foreach (var alias in declared)
                {
                    if (string.IsNullOrWhiteSpace(alias)) continue;
                    RegisterConfig(alias.Trim(), requiredForReady: true);
                }
            }

            CheckReady();
            KickOffRefresh();
        }

        /// <summary>Create tracking state for a configuration and load any disk-cached copy.</summary>
        private ConfigState RegisterConfig(string alias, bool requiredForReady)
        {
            if (_configs.TryGetValue(alias, out var existing))
            {
                existing.RequiredForReady |= requiredForReady;
                return existing;
            }

            var state = new ConfigState
            {
                Alias = alias,
                RequiredForReady = requiredForReady,
                Cache = new ConfigDiskCache(_context.EnvironmentAlias, alias),
            };
            _configs[alias] = state;

            var cached = state.Cache.Load();
            if (cached != null)
            {
                ApplyResponse(state, cached, persist: false);
                GBLog.Info($"Loaded cached configuration '{alias}' (hash {cached.Hash}).");
            }

            return state;
        }

        /// <summary>
        /// Configurations load on demand: accessing one that was not declared in Setup
        /// registers it and starts fetching it immediately.
        /// </summary>
        private ConfigState EnsureConfig(string alias)
        {
            if (_configs.TryGetValue(alias, out var existing)) return existing;

            var state = RegisterConfig(alias, requiredForReady: false);
            GBLog.Info($"Configuration '{alias}' was not declared in Setup; fetching it on demand.");
            KickOffConfigRefresh(state);
            return state;
        }

        private async void KickOffConfigRefresh(ConfigState state)
        {
            await RefreshConfigAsync(state);
        }

        // --- Public API ---------------------------------------------------------

        public TValue Get<TValue>(string configPath)
        {
            TryGetInternal<TValue>(configPath, out var value, logErrors: true);
            return value;
        }

        public TValue Get<TValue>(string configPath, TValue defaultValue)
        {
            return TryGetInternal<TValue>(configPath, out var value, logErrors: true)
                ? value
                : defaultValue;
        }

        public bool TryGet<TValue>(string configPath, out TValue value)
        {
            return TryGetInternal(configPath, out value, logErrors: false);
        }

        public IDisposable OnReady(Action callback)
        {
            if (callback == null)
            {
                GBLog.Error("OnReady called with a null callback.");
                return null;
            }

            IDisposable subscription = null;
            var fired = false;
            Action<bool> handler = _ =>
            {
                if (fired) return;
                fired = true;
                callback();
                subscription?.Dispose();
            };

            subscription = _onReady.Subscribe(handler);
            if (_isReady)
            {
                handler(true);
            }

            return subscription;
        }

        public IDisposable OnChanged(string configPath, Action callback)
        {
            if (callback == null || !TrySplitPath(configPath, out var alias, out var segments))
            {
                GBLog.Error("OnChanged requires a valid config path and a callback.");
                return null;
            }

            return AddListener(alias, segments, includeInitial: false, _ => callback());
        }

        public IDisposable Observe<TValue>(string configPath, Action<TValue> callback)
        {
            if (callback == null || !TrySplitPath(configPath, out var alias, out var segments))
            {
                GBLog.Error("Observe requires a valid config path and a callback.");
                return null;
            }

            var listener = AddListener(alias, segments, includeInitial: true,
                token => callback(ConvertToken<TValue>(token, configPath)));

            if (_configs.TryGetValue(alias, out var state) && state.Loaded)
            {
                callback(ConvertToken<TValue>(ResolvePath(state.Values, segments), configPath));
            }

            return listener;
        }

        public Task RefreshAsync()
        {
            _timeSinceLastRefresh = 0f;

            var tasks = new List<Task>();
            foreach (var state in _configs.Values)
            {
                tasks.Add(RefreshConfigAsync(state));
            }

            return tasks.Count == 0 ? Task.CompletedTask : Task.WhenAll(tasks);
        }

        // --- Refresh loop -------------------------------------------------------

        private Task RefreshConfigAsync(ConfigState state)
        {
            if (state.RefreshInFlight != null) return state.RefreshInFlight;

            state.RefreshInFlight = DoRefreshConfig(state);
            return state.RefreshInFlight;
        }

        private async Task DoRefreshConfig(ConfigState state)
        {
            try
            {
                var result = await _context.Api.GetConfigurationAsync(state.Alias, state.Hash);
                switch (result.Status)
                {
                    case ConfigurationFetchStatus.Updated:
                        ApplyResponse(state, result.Response, persist: true);
                        GBLog.Info($"Configuration '{state.Alias}' updated (hash {result.Response.Hash}).");
                        break;

                    case ConfigurationFetchStatus.NotModified:
                        GBLog.Info($"Configuration '{state.Alias}' unchanged.");
                        break;

                    case ConfigurationFetchStatus.NotFound:
                        GBLog.Error($"Configuration '{state.Alias}' not found: {result.Error}. " +
                                    "Check GamebeastSettings.Configurations against your dashboard.");
                        break;

                    case ConfigurationFetchStatus.Failed:
                        GBLog.Warn($"Refresh of configuration '{state.Alias}' failed ({result.Error}); will retry.");
                        break;
                }
            }
            catch (Exception ex)
            {
                GBLog.Error($"Unexpected error refreshing configuration '{state.Alias}': {ex}");
            }
            finally
            {
                state.RefreshInFlight = null;
            }
        }

        private async void KickOffRefresh()
        {
            await RefreshAsync();
        }

        public void Tick(float deltaTime)
        {
            if (_configs.Count == 0) return;

            var interval = _context.Settings.ConfigRefreshIntervalSeconds;
            if (interval <= 0f) return;

            _timeSinceLastRefresh += deltaTime;
            if (_timeSinceLastRefresh >= interval)
            {
                KickOffRefresh();
            }
        }

        public void OnApplicationPause(bool paused)
        {
            // Refresh right away when coming back from background; configs may have
            // changed while the app was suspended.
            if (!paused && _configs.Count > 0)
            {
                KickOffRefresh();
            }
        }

        public void Shutdown()
        {
        }

        // --- State --------------------------------------------------------------

        private void ApplyResponse(ConfigState state, SdkConfigurationV2Response response, bool persist)
        {
            JObject newValues;
            if (response.Configuration is JObject obj)
            {
                newValues = obj;
            }
            else
            {
                GBLog.Warn($"Configuration '{state.Alias}' root is not a JSON object; no values applied.");
                newValues = new JObject();
            }

            var oldValues = state.Values;
            var wasLoaded = state.Loaded;

            state.Values = newValues;
            state.Hash = response.Hash;
            state.Loaded = true;

            if (persist)
            {
                state.Cache.Save(response);
            }

            NotifyListeners(state.Alias, wasLoaded, oldValues, newValues);
            CheckReady();
        }

        private void NotifyListeners(string alias, bool wasLoaded, JObject oldValues, JObject newValues)
        {
            // Copy so listeners can dispose (or add) subscriptions while firing.
            var snapshot = _listeners.ToArray();
            foreach (var listener in snapshot)
            {
                if (!string.Equals(listener.ConfigAlias, alias, StringComparison.OrdinalIgnoreCase)) continue;

                var newToken = ResolvePath(newValues, listener.PathSegments);
                if (!wasLoaded)
                {
                    // First data for this config: only Observe-style listeners fire.
                    if (listener.IncludeInitial)
                    {
                        SafeInvoke(listener, newToken);
                    }
                    continue;
                }

                var oldToken = ResolvePath(oldValues, listener.PathSegments);
                if (!JToken.DeepEquals(oldToken, newToken))
                {
                    SafeInvoke(listener, newToken);
                }
            }
        }

        private static void SafeInvoke(ChangeListener listener, JToken newToken)
        {
            try
            {
                listener.Callback(newToken);
            }
            catch (Exception ex)
            {
                GBLog.Error($"Config subscriber threw: {ex}");
            }
        }

        private IDisposable AddListener(string alias, string[] segments, bool includeInitial, Action<JToken> callback)
        {
            EnsureConfig(alias);

            var listener = new ChangeListener(this)
            {
                ConfigAlias = alias,
                PathSegments = segments,
                IncludeInitial = includeInitial,
                Callback = callback,
            };
            _listeners.Add(listener);
            return listener;
        }

        private void CheckReady()
        {
            if (_isReady) return;

            // Only configurations declared in Setup gate readiness; lazily discovered
            // ones announce themselves through Observe instead.
            foreach (var state in _configs.Values)
            {
                if (state.RequiredForReady && !state.Loaded) return;
            }

            _isReady = true;
            _onReady.Trigger(true);
        }

        // --- Path resolution ----------------------------------------------------

        private bool TryGetInternal<TValue>(string configPath, out TValue value, bool logErrors)
        {
            value = default;

            if (!TrySplitPath(configPath, out var alias, out var segments))
            {
                GBLog.Error("Get called without a valid config path (expected \"ConfigName.SomeKey\").");
                return false;
            }

            var state = EnsureConfig(alias);
            if (!state.Loaded)
            {
                if (logErrors)
                {
                    GBLog.Warn($"Configuration '{alias}' has not loaded yet (fetch in progress); " +
                               "use Observe or OnReady to wait for it.");
                }
                return false;
            }

            var token = ResolvePath(state.Values, segments);
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }

            try
            {
                value = token.ToObject<TValue>(GamebeastJson.Serializer);
                return true;
            }
            catch (Exception ex)
            {
                if (logErrors)
                {
                    GBLog.Error($"Could not convert config '{configPath}' to {typeof(TValue).Name}: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>Splits "ConfigA.Some.Key" into alias "ConfigA" and segments ["Some", "Key"].</summary>
        private static bool TrySplitPath(string configPath, out string alias, out string[] segments)
        {
            alias = null;
            segments = null;

            if (string.IsNullOrWhiteSpace(configPath)) return false;

            var parts = configPath.Split('.');
            if (string.IsNullOrWhiteSpace(parts[0])) return false;

            alias = parts[0].Trim();
            segments = new string[parts.Length - 1];
            Array.Copy(parts, 1, segments, 0, segments.Length);
            return true;
        }

        private static JToken ResolvePath(JToken root, string[] segments)
        {
            var current = root;
            foreach (var segment in segments)
            {
                switch (current)
                {
                    case JObject obj:
                        current = obj[segment];
                        break;

                    case JArray arr when int.TryParse(segment, out var index) && index >= 0 && index < arr.Count:
                        current = arr[index];
                        break;

                    default:
                        return null;
                }
            }

            return current;
        }

        private static TValue ConvertToken<TValue>(JToken token, string configPath)
        {
            if (token == null || token.Type == JTokenType.Null) return default;

            try
            {
                return token.ToObject<TValue>(GamebeastJson.Serializer);
            }
            catch (Exception ex)
            {
                GBLog.Error($"Could not convert config '{configPath}' to {typeof(TValue).Name}: {ex.Message}");
                return default;
            }
        }
    }
}
