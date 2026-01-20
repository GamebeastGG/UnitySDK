using System;
using System.Collections.Generic;
using UnityEngine;
using Gamebeast.Runtime.Internal.Utils;
using Newtonsoft.Json;

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
                    // Attempt to convert the object to the requested type
                    return (TValue)Convert.ChangeType(value, typeof(TValue));
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