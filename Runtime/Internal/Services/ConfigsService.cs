using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Gamebeast.Runtime.Internal.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gamebeast.Runtime.Internal.Services
{
    [Serializable]
    public class ConfigsResponseArgs
    {
        public Dictionary<string, object> configs;
    }
    [Serializable]
    public class ConfigsResponse 
    {
        public ConfigsResponseArgs args;
    }
    internal sealed class ConfigsService : IConfigsService
    {
		private static ConfigsService _instance;
		internal static ConfigsService Instance => _instance;

        private static bool isReady = false;
        private static Dictionary<string, object> activeConfigs = new Dictionary<string, object>();

		private readonly Dictionary<string, List<Action>> _changedHandlers = new Dictionary<string, List<Action>>();

        private sealed class Subscription : IDisposable
        {
            private ConfigsService _service;
            private readonly string _key;
            private Action _callback;
            private bool _disposed;

            public Subscription(ConfigsService service, string key, Action callback)
            {
                _service = service;
                _key = key;
                _callback = callback;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                var service = _service;
                var callback = _callback;
                _service = null;
                _callback = null;

                if (service == null || callback == null) return;
                service.RemoveChangedHandler(_key, callback);
            }
        }

        private Action onReady;

        internal ConfigsService()
        {
            if (_instance != null && !ReferenceEquals(_instance, this))
            {
                Debug.LogWarning("[ConfigsService] Multiple instances created; overwriting Instance reference.");
            }
            _instance = this;
        }

        public event Action OnReady
        {
            add
            {
                if (isReady)
                {
                    value?.Invoke();
                    return;
                }

                onReady += value;
            }
            remove
            {
                onReady -= value;
            }
        }

        private void FetchConfigs()
        {
            // Implementation to fetch configs from the server
            Debug.Log("Fetching configs from server...");
			FetchConfigsAsync();
        }

		private async void FetchConfigsAsync()
		{
			try
			{
				var result = await GBRequest.MakeRequestAsync<ConfigsResponse>(GBRequestType.GetConfigs);
				if (isReady == true) {
                    return;
                }
                
                activeConfigs = result.args.configs;
				isReady = true;
				Debug.Log("Configs fetched and ready.");
                onReady?.Invoke();
                onReady = null;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[ConfigsService] Error fetching configs: {ex}");
			}
		}

        public IDisposable OnChanged(string configName, Action callback)
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                Debug.LogError("[ConfigsService] OnChanged called with empty configName.");
                return null;
            }
            if (callback == null)
            {
                Debug.LogError("[ConfigsService] OnChanged called with null callback.");
                return null;
            }

            if (!_changedHandlers.TryGetValue(configName, out var list))
            {
                list = new List<Action>();
                _changedHandlers[configName] = list;
            }
            list.Add(callback);
            return new Subscription(this, configName, callback);
        }

        private void RemoveChangedHandler(string configName, Action callback)
        {
            if (string.IsNullOrWhiteSpace(configName) || callback == null) return;
            if (!_changedHandlers.TryGetValue(configName, out var list) || list == null) return;

            list.Remove(callback);
            if (list.Count == 0)
            {
                _changedHandlers.Remove(configName);
            }
        }

        internal void ApplyConfigs(IDictionary<string, JToken> configs)
        {
            if (configs == null)
            {
                Debug.LogWarning("[ConfigsService] No configs to apply.");
                return;
            }

            List<Action> handlersSnapshot = null;
            foreach (var kvp in configs)
            {
                var key = kvp.Key;
                var newValue = kvp.Value;

                activeConfigs.TryGetValue(key, out var oldValue);
                activeConfigs[key] = newValue;

                // Only trigger if there are handlers for this key.
                if (_changedHandlers.TryGetValue(key, out var handlers) && handlers.Count > 0)
                {
                    // Optional: only fire when value actually changes.
                    var changed = !JToken.DeepEquals(oldValue as JToken, newValue);
                    if (changed)
                    {
                        handlersSnapshot ??= new List<Action>();
                        handlersSnapshot.AddRange(handlers);
                    }
                }
            }

            isReady = true;
            Debug.Log("[ConfigsService] Configs applied.");
            onReady?.Invoke();
            onReady = null;

            // Invoke after applying to avoid re-entrancy issues.
            if (handlersSnapshot != null)
            {
                for (var i = 0; i < handlersSnapshot.Count; i++)
                {
                    try { handlersSnapshot[i]?.Invoke(); }
                    catch (Exception ex) { Debug.LogError($"[ConfigsService] OnChanged callback threw: {ex}"); }
                }
            }
        }

        public TValue Get<TValue>(string configName)
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                Debug.LogError("[ConfigsService] configName missing, will not get.");
                return default;
            }

            if (activeConfigs != null && activeConfigs.TryGetValue(configName, out var value))
            {
                try
                {
                    if (value == null)
                    {
                        return default;
                    }

                    if (value is TValue typed)
                    {
                        return typed;
                    }

                    // Json.NET will often materialize Dictionary<string, object> values as JToken types.
                    if (value is JToken token)
                    {
                        return token.ToObject<TValue>();
                    }

                    var targetType = typeof(TValue);
                    if (targetType == typeof(string))
                    {
                        return (TValue)(object)value.ToString();
                    }

                    // Fast path for primitives + enums
                    if (value is IConvertible)
                    {
                        if (targetType.IsEnum)
                        {
                            return (TValue)Enum.Parse(targetType, value.ToString(), ignoreCase: true);
                        }

                        return (TValue)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                    }

                    // Complex types (objects/arrays): serialize then deserialize into requested type.
                    return JsonConvert.DeserializeObject<TValue>(JsonConvert.SerializeObject(value));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ConfigsService] Error converting config '{configName}' to type {typeof(TValue).Name}: {ex}");
                    return default;
                }
            }
            return default;
        }

        public bool IsReady()
        {
            return isReady;
        }

        internal void Setup()
        {
            FetchConfigs();
        }

        internal void Cleanup()
        {
            
        }
    }
}