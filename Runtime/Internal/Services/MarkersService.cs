using System;
using System.Collections.Generic;
using UnityEngine;
using Gamebeast.Runtime.Internal.Utils;
using UnityEngine.UIElements;

namespace Gamebeast.Runtime.Internal.Services
{

    [Serializable]
    public class MarkerPayload
    {
        public string markerId;
        public long timestamp;
        public string type;
        public string serverId;
        public string userId;
        public object properties;
    }

    [Serializable]
    public class MarkersWrapper
    {
        public MarkerPayload[] markers;
    }
    
    internal sealed class MarkersService : IMarkersService
    {
        private static readonly List<MarkerPayload> _markerCache = new List<MarkerPayload>();
        private static readonly object _markerCacheLock = new object();

        private float _timeSinceLastFlush = 0f;
        private const int FlushIntervalSeconds = 10;


        private void FlushMarkers()
        {
            MarkerPayload[] snapshot;

            _timeSinceLastFlush = 0f;

            lock (_markerCacheLock)
            {
                if (_markerCache.Count == 0) return;

                snapshot = _markerCache.ToArray();
                _markerCache.Clear();
            }

            var wrapper = new MarkersWrapper
            {
                markers = snapshot
            };

			SendMarkersAsync(wrapper);
        }

		private async void SendMarkersAsync(MarkersWrapper wrapper)
		{
			try
			{
				await GBRequest.MakeRequestAsync<string>(GBRequestType.PostMarker, wrapper);
				Debug.Log("[MarkersService] Successfully sent markers.");
			}
			catch (Exception ex)
			{
				// TODO: implement retry logic
				Debug.LogError($"[MarkersService] Error sending markers: {ex}");
			}
		}
        private static bool IsPrimitiveLike(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }

        private void GenerateMarkerPayload<TValue>(string markerName, TValue value, string distinctId = null)
        {
            // Enforce that only object-like payloads are allowed (no primitives/strings).
            if (value != null && IsPrimitiveLike(typeof(TValue)))
            {
                Debug.LogError($"[MarkersService] TValue '{typeof(TValue).Name}' is a primitive; markers must use object-like payload types.");
                return;
            }

            // Detect Vector3 types in the value then convert to a float array.
            if (value is object obj)
            {
                var objType = obj.GetType();
                var fields = objType.GetFields();
                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(Vector3))
                    {
                        var vec = (Vector3)field.GetValue(obj);
                        field.SetValue(obj, new float[] { vec.x, vec.y, vec.z });
                    }
                }
            }

            var marker = new MarkerPayload
            {
                markerId = Guid.NewGuid().ToString("N"),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                type = markerName, // eventName
                serverId = "unity-0000",
                userId = distinctId, // distinctId
                properties = value
            };

            var shouldFlush = false;
            lock (_markerCacheLock)
            {
                _markerCache.Add(marker);

                if (_markerCache.Count >= 10)
                {
                    shouldFlush = true;
                }
            }

            if (shouldFlush)
            {
                FlushMarkers();
            }
        }
        public void SendMarker<TValue>(string markerName, TValue value)
        {
            if (string.IsNullOrWhiteSpace(markerName))
            {
                Debug.LogError("[MarkersService] markerName missing, will not send.");
                return;
            }

            GenerateMarkerPayload(markerName, value, null);
        }

        public void SendPlayerMarker<TValue>(string userId, string markerName, TValue value)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                Debug.LogError("[MarkersService] userId missing, will not send.");
                return;
            }

            if (string.IsNullOrWhiteSpace(markerName))
            {
                Debug.LogError("[MarkersService] markerName missing, will not send.");
                return;
            }

            GenerateMarkerPayload(markerName, value, userId);
        }

        /// <summary>
        /// Called regularly (e.g. from GamebeastRuntime.Update) to handle time-based flushing.
        /// </summary>
        internal void Tick(float deltaTime)
        {
            var shouldFlush = false;
            lock (_markerCacheLock) {
                if (_markerCache.Count == 0)
                {
                    _timeSinceLastFlush = 0f;
                    return;
                }
                _timeSinceLastFlush += deltaTime;

                if (_timeSinceLastFlush >= FlushIntervalSeconds)
                {
                    shouldFlush = true;
                }
            }
            
            if (shouldFlush == true) {
                FlushMarkers();
            }
        }

        internal void Cleanup()
        {
            FlushMarkers();
        }
    }
}