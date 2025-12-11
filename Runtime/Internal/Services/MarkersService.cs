using System;
using System.Collections.Generic;
using UnityEngine;
using Gamebeast.Runtime.Internal.Utils;

namespace Gamebeast.Runtime.Internal.Services
{
    [Serializable]
    public class MarkerProperties
    {
        public string sdkPlatform;
        public int placeId;
        public int placeVersion;
        public string origin;
    }

    [Serializable]
    public class MarkerPayload
    {
        public string markerId;
        public long timestamp;
        public string type;
        public string serverId;
        public object value;
        public MarkerProperties properties;
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

            GBRequest.MakeRequestAsync<string>(GBRequestType.PostMarker, wrapper).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    Debug.Log("[MarkersService] Successfully sent markers.");
                }
                else
                {
                    // TODO: implement retry logic
                    Debug.LogError($"[MarkersService] Error sending markers: {task.Exception}");
                }
            });
        }
        private static bool IsPrimitiveLike(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }

        private void GenerateMarkerPayload<TValue>(string markerName, TValue value)
        {
            // Enforce that only object-like payloads are allowed (no primitives/strings).
            if (value != null && IsPrimitiveLike(typeof(TValue)))
            {
                Debug.LogError($"[MarkersService] TValue '{typeof(TValue).Name}' is a primitive; markers must use object-like payload types.");
                return;
            }

            var marker = new MarkerPayload
            {
                markerId = Guid.NewGuid().ToString("N"),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                type = markerName,
                serverId = "unity-0000",
                value = value,
                properties = new MarkerProperties
                {
                    sdkPlatform = "roblox",
                    placeId = 1,
                    placeVersion = 1,
                    origin = "sdk"
                }
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

            GenerateMarkerPayload(markerName, value);
        }

        /// <summary>
        /// Called regularly (e.g. from GamebeastRuntime.Update) to handle time-based flushing.
        /// </summary>
        internal void Tick(float deltaTime)
        {
            if (_markerCache.Count == 0)
            {
                _timeSinceLastFlush = 0f;
                return;
            }

            _timeSinceLastFlush += deltaTime;

            if (_timeSinceLastFlush >= FlushIntervalSeconds)
            {
                FlushMarkers();
            }
        }

        internal void Cleanup()
        {
            FlushMarkers();
        }
    }
}