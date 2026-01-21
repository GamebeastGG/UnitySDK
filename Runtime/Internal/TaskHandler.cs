using System;
using System.Collections.Generic;
using UnityEngine;
using Gamebeast.Runtime.Internal.Services;
using Gamebeast.Runtime.Internal.Utils;

namespace Gamebeast.Runtime.Internal
{
    internal class TaskHandler
    {
        private float _timeSinceLastCheck = 0f;
        private const int CheckIntervalSeconds = 15;
		private async void CheckRequests()
		{
			try
			{
				var result = await GBRequest.MakeRequestAsync<string>(GBRequestType.GetRequest);
				Debug.Log("Got requests:");
				Debug.Log(result);
			}
			catch (Exception ex)
			{
				
				Debug.LogError($"[TaskHandler] Error fetching requests: {ex}");
			}
		}
        
        private void Update()   
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