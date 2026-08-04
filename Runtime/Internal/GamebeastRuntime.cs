using System;
using System.Collections.Generic;
using UnityEngine;
using Gamebeast.Internal.Http;
using Gamebeast.Internal.Services;

namespace Gamebeast.Internal
{
    /// <summary>
    /// Hidden MonoBehaviour host for the SDK. Owns the service registry and drives
    /// service lifecycles (Initialize / Tick / OnApplicationPause / Shutdown) from
    /// Unity's player loop. Lives on a DontDestroyOnLoad "[Gamebeast]" GameObject.
    /// </summary>
    internal sealed class GamebeastRuntime : MonoBehaviour
    {
        private static GamebeastRuntime _instance;

        public static GamebeastRuntime Instance => EnsureInstance();

        public static bool IsInitialized => _instance != null && _instance._initialized;

        // Keyed by public interface type (e.g. IMarkersService). Iterated in
        // registration order for Initialize/Tick/Shutdown.
        private readonly Dictionary<Type, IGamebeastService> _services = new Dictionary<Type, IGamebeastService>();
        private readonly List<IGamebeastService> _serviceOrder = new List<IGamebeastService>();

        private bool _initialized;

        /// <summary>Wall-clock time the app was backgrounded, for session rotation.</summary>
        private DateTime? _backgroundedAtUtc;

        public GamebeastContext Context { get; private set; }

        private static GamebeastRuntime EnsureInstance()
        {
            if (_instance != null) return _instance;

            // Reuse an existing instance in case one was placed in the scene.
#if UNITY_2022_2_OR_NEWER
            _instance = FindFirstObjectByType<GamebeastRuntime>();
#else
            _instance = FindObjectOfType<GamebeastRuntime>();
#endif
            if (_instance == null)
            {
                var go = new GameObject("[Gamebeast]");
                _instance = go.AddComponent<GamebeastRuntime>();
            }

            DontDestroyOnLoad(_instance.gameObject);
            return _instance;
        }

        public void Setup(GamebeastSettings settings)
        {
            if (_initialized)
            {
                GBLog.Warn("Setup called more than once; ignoring.");
                return;
            }

            if (settings == null || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                GBLog.Error("Setup requires a non-empty ApiKey.");
                return;
            }

            GBLog.DebugEnabled = settings.DebugLogging;

            var environmentAlias = ResolveEnvironmentAlias(settings);
            var identity = DistinctIdentity.Resolve(settings.DistinctId);
            var sessionId = "unity-" + Guid.NewGuid().ToString("N");
            var api = new GamebeastApiClient(settings, environmentAlias, sessionId);

            Context = new GamebeastContext(settings, identity, api, environmentAlias, sessionId);

            RegisterService<IMarkersService>(new MarkersService());
            RegisterService<IConfigsService>(new ConfigsService());
            RegisterService<IExperimentsService>(new ExperimentsService());
            RegisterService<ICohortsService>(new CohortsService());

            _initialized = true;

            foreach (var service in _serviceOrder)
            {
                try
                {
                    service.Initialize(Context);
                }
                catch (Exception ex)
                {
                    GBLog.Error($"Failed to initialize {service.GetType().Name}: {ex}");
                }
            }

            GBLog.Info($"SDK initialized (environment: {environmentAlias}, distinctId: {identity.DistinctId}).");
        }

        private void RegisterService<TInterface>(IGamebeastService instance) where TInterface : class
        {
            var type = typeof(TInterface);
            if (_services.ContainsKey(type))
            {
                GBLog.Warn($"Service {type.Name} is already registered; overwriting.");
            }

            _services[type] = instance;
            _serviceOrder.Add(instance);
        }

        /// <summary>Switch the local user's distinct id (e.g. after login).</summary>
        public void Identify(string distinctId)
        {
            if (!_initialized)
            {
                GBLog.Error("Identify called before Setup.");
                return;
            }

            if (string.IsNullOrWhiteSpace(distinctId))
            {
                GBLog.Error("Identify requires a non-empty distinct id.");
                return;
            }

            var identity = DistinctIdentity.Explicit(distinctId);
            if (Context.Identity.DistinctId == identity.DistinctId) return;

            Context.SetIdentity(identity);
            NotifyIdentityChanged();
        }

        /// <summary>Revert to the device's persisted anonymous distinct id (e.g. after logout).</summary>
        public void ResetIdentity()
        {
            if (!_initialized)
            {
                GBLog.Error("ResetIdentity called before Setup.");
                return;
            }

            var identity = DistinctIdentity.Anonymous();
            if (Context.Identity.DistinctId == identity.DistinctId) return;

            Context.SetIdentity(identity);
            NotifyIdentityChanged();
        }

        private void NotifyIdentityChanged()
        {
            GBLog.Info($"Distinct id is now '{Context.Identity.DistinctId}' " +
                       $"({(Context.Identity.IsAnonymous ? "anonymous" : "explicit")}).");

            foreach (var service in _serviceOrder)
            {
                try
                {
                    service.OnIdentityChanged();
                }
                catch (Exception ex)
                {
                    GBLog.Error($"Error notifying {service.GetType().Name} of identity change: {ex}");
                }
            }
        }

        /// <summary>
        /// Resolve a service by its public interface. Returns null (with a warning)
        /// before Setup or for unknown types.
        /// </summary>
        public TService GetService<TService>() where TService : class
        {
            if (!_initialized)
            {
                GBLog.Warn($"GetService<{typeof(TService).Name}> called before Setup.");
                return null;
            }

            if (_services.TryGetValue(typeof(TService), out var service))
            {
                return service as TService;
            }

            GBLog.Warn($"No service registered for type {typeof(TService).Name}.");
            return null;
        }

        private static string ResolveEnvironmentAlias(GamebeastSettings settings)
        {
            // A custom environment alias from the dashboard wins over the enum.
            if (!string.IsNullOrWhiteSpace(settings.CustomEnvironment))
            {
                return settings.CustomEnvironment.Trim();
            }

            switch (settings.Environment)
            {
                case GamebeastEnvironment.Production: return "production";
                case GamebeastEnvironment.Development: return "development";
                case GamebeastEnvironment.Auto:
                default:
                    return Application.isEditor ? "development" : "production";
            }
        }

        private void Update()
        {
            if (!_initialized) return;

            var deltaTime = Time.unscaledDeltaTime;
            foreach (var service in _serviceOrder)
            {
                service.Tick(deltaTime);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!_initialized) return;

            if (paused)
            {
                _backgroundedAtUtc = DateTime.UtcNow;
            }
            else
            {
                // Rotate before services react, so resume-triggered work (config
                // refreshes, new markers) already runs under the new session.
                RotateSessionIfExpired();
            }

            foreach (var service in _serviceOrder)
            {
                service.OnApplicationPause(paused);
            }
        }

        /// <summary>
        /// A long stay in the background counts as a new session: rotate the session id
        /// when the app resumes after GamebeastSettings.SessionTimeoutSeconds.
        /// Wall clock, because Time.realtimeSinceStartup pauses during suspension on
        /// some platforms.
        /// </summary>
        private void RotateSessionIfExpired()
        {
            if (_backgroundedAtUtc == null) return;

            var backgroundedSeconds = (DateTime.UtcNow - _backgroundedAtUtc.Value).TotalSeconds;
            _backgroundedAtUtc = null;

            var timeout = Context.Settings.SessionTimeoutSeconds;
            if (timeout <= 0f || backgroundedSeconds < timeout) return;

            var sessionId = "unity-" + Guid.NewGuid().ToString("N");
            Context.SetSessionId(sessionId);
            Context.Api.SessionId = sessionId;

            GBLog.Info($"App resumed after {backgroundedSeconds:0}s in the background; started new session {sessionId}.");
        }

        /// <summary>Flush pending work and tear the SDK down; Setup may be called again afterwards.</summary>
        public void Shutdown()
        {
            if (!_initialized)
            {
                GBLog.Warn("Shutdown called before Setup; ignoring.");
                return;
            }

            foreach (var service in _serviceOrder)
            {
                try
                {
                    service.Shutdown();
                }
                catch (Exception ex)
                {
                    GBLog.Error($"Error shutting down {service.GetType().Name}: {ex}");
                }
            }

            _services.Clear();
            _serviceOrder.Clear();
            Context = null;
            _backgroundedAtUtc = null;
            _initialized = false;

            GBLog.Info("SDK shut down.");
        }

        private void OnApplicationQuit()
        {
            if (!_initialized) return;

            Shutdown();
        }
    }
}
