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

            var environmentAlias = ResolveEnvironmentAlias(settings.Environment);
            var identity = DistinctIdentity.Resolve(settings.DistinctId);
            var sessionId = "unity-" + Guid.NewGuid().ToString("N");
            var api = new GamebeastApiClient(settings, environmentAlias, sessionId);

            Context = new GamebeastContext(settings, identity, api, environmentAlias, sessionId);

            RegisterService<IMarkersService>(new MarkersService());
            RegisterService<IConfigsService>(new ConfigsService());

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

        private static string ResolveEnvironmentAlias(GamebeastEnvironment environment)
        {
            switch (environment)
            {
                case GamebeastEnvironment.Production: return "production";
                case GamebeastEnvironment.Development: return "development";
                case GamebeastEnvironment.Studio: return "studio";
                case GamebeastEnvironment.Auto:
                default:
                    return Application.isEditor ? "studio" : "production";
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

            foreach (var service in _serviceOrder)
            {
                service.OnApplicationPause(paused);
            }
        }

        private void OnApplicationQuit()
        {
            if (!_initialized) return;

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
        }
    }
}
