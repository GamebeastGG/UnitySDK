using System;
using System.Collections.Generic;
using UnityEngine;
using Gamebeast.Runtime.Internal.Services;
using Gamebeast.Runtime.Internal.Utils;

namespace Gamebeast.Internal
{
    // Internal: only code inside the Gamebeast assembly can see this by default.
    
    internal class GamebeastRuntime : MonoBehaviour
    {
        private static GamebeastRuntime _instance;
        public static GamebeastRuntime Instance => EnsureInstance();
        
        // Registry for SDK services that live on the [Gamebeast] object
        private readonly Dictionary<Type, object> _services = new();
        private static GamebeastRuntime EnsureInstance()
        {
            if (_instance != null) return _instance;

            // Try to find an existing instance (in case user dropped a prefab)
            _instance = UnityEngine.Object.FindFirstObjectByType<GamebeastRuntime>();
            if (_instance != null)
            {
                UnityEngine.Object.DontDestroyOnLoad(_instance.gameObject);
                return _instance;
            }

            // Otherwise create a hidden GameObject for it
            var go = new GameObject("[Gamebeast]");
            _instance = go.AddComponent<GamebeastRuntime>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            return _instance;
        }

        private bool _initialized;
        private string _apiKey;

        public void Init(string apiKey)
        {
            if (_initialized) return;

            _apiKey = apiKey;
            _initialized = true;

            GBRequest.SetApiKey(_apiKey);

            // Register built-in services that should live on the [Gamebeast] object
            RegisterCoreServices();
        }

        /// <summary>
        /// Register all core Gamebeast services that should live on the [Gamebeast] GameObject.
        /// </summary>
        private void RegisterCoreServices()
        {
            // MarkersService lives on the [Gamebeast] GameObject
            var markers = new MarkersService();
            RegisterService<MarkersService>(markers);
        }

        /// <summary>
        /// Registers a service instance so it can be resolved via GetService&lt;T&gt;().
        /// </summary>
        internal void RegisterService<TService>(TService instance) where TService : class
        {
            if (instance == null)
            {
                Debug.LogError($"[Gamebeast] Tried to register null service of type {typeof(TService).Name}.");
                return;
            }

            var type = typeof(TService);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[Gamebeast] Service of type {type.Name} is already registered. Overwriting.");
            }

            _services[type] = instance;
        }

        /// <summary>
        /// Get a registered service of type TService. Returns null if not found or if SDK not initialized.
        /// </summary>
        internal TService GetService<TService>() where TService : class
        {
            if (!_initialized)
            {
                Debug.LogWarning("[Gamebeast] GetService called before Init.");
                return null;
            }

            var type = typeof(TService);
            if (_services.TryGetValue(type, out var serviceObj))
            {
                return serviceObj as TService;
            }

            Debug.LogWarning($"[Gamebeast] No service registered for type {type.Name}.");
            return null;
        }

        private void Update()
        {
            if (!_initialized) return;
            // Drive per-frame work for known services.

            var markersService = GetService<MarkersService>();
            if (markersService != null)
            {
                markersService.Tick(Time.deltaTime);
            }

        }

        private void OnApplicationQuit()
        {
            if (!_initialized) return;

            var markersService = GetService<MarkersService>();
            markersService.Cleanup();
        }
    }
}
