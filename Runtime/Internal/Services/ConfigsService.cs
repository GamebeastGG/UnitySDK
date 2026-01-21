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
        private static bool isReady = false;
        private static Dictionary<string, object> activeConfigs = new Dictionary<string, object>();

        private Action onReady;

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