using System;
using System.Collections.Generic;
using UnityEngine;
using Gamebeast.Runtime.Internal.Services;
using Gamebeast.Runtime.Internal.Utils;
using Gamebeast.Runtime.Internal.Models;

namespace Gamebeast.Runtime.Internal
{
    internal class TaskHandler
    {
        private readonly Dictionary<string, Action<RemoteRequest>> _taskMap;

        private float _timeSinceLastCheck = 0f;
        private const int CheckIntervalSeconds = 5;

        internal TaskHandler()
        {
            _taskMap = new Dictionary<string, Action<RemoteRequest>>
            {
                ["UpdateConfigs"] = request =>
                {
                    var configsService = ConfigsService.Instance;
                    if (configsService == null)
                    {
                        Debug.LogWarning("[TaskHandler] ConfigsService.Instance is null; cannot apply configs (SDK not initialized yet?).");
                        return; // Shouldnt be possible.
                    }
                    configsService.ApplyConfigs(request.Args?.Configs);

                    Debug.Log("[TaskHandler] Applied UpdateConfigs request.");
                },
            };
        }
		private async void CheckRequests()
		{
			try
			{
                var requests = await GBRequest.MakeRequestAsync<RemoteRequest[]>(GBRequestType.GetRequest);
                Debug.Log("Got requests:");
                Debug.Log(requests == null ? "<null>" : $"Count={requests.Length}");

                if (requests != null)
                {
                    for (var i = 0; i < requests.Length; i++)
                    {
                        var req = requests[i];
                        Debug.Log($"[{i}] id={req.RequestId} type={req.RequestType} env={req.Args?.EnvironmentId}");

						if (!string.IsNullOrWhiteSpace(req.RequestType) && _taskMap.TryGetValue(req.RequestType, out var handler))
						{
							handler(req);
						}
                    }
                }
                
			}
			catch (Exception ex)
			{
				
				Debug.LogError($"[TaskHandler] Error fetching requests: {ex}");
			}
		}
        
        internal void Update()   
        {
            _timeSinceLastCheck += Time.deltaTime;
            if (_timeSinceLastCheck >= CheckIntervalSeconds)
            {
                _timeSinceLastCheck = 0f;
                CheckRequests();
            }
        }

        internal void StartHandler()
        {
            Debug.Log("Starting TaskHandler");
            CheckRequests();
        }
    }
}