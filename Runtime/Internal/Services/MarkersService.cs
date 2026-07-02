using System;
using System.Collections.Generic;
using Gamebeast.Internal.Models;

namespace Gamebeast.Internal.Services
{
    /// <summary>
    /// Batches engagement markers and posts them to /sdk/v1/markers.
    /// Flushes when the batch is full, every few seconds, and when the app
    /// quits or is backgrounded.
    /// </summary>
    internal sealed class MarkersService : IMarkersService, IGamebeastService
    {
        private const int MaxBatchSize = 10;
        private const float FlushIntervalSeconds = 10f;

        private readonly List<MarkerPayload> _pending = new List<MarkerPayload>();
        private readonly object _pendingLock = new object();

        private GamebeastContext _context;
        private float _timeSinceLastFlush;

        public void Initialize(GamebeastContext context)
        {
            _context = context;
        }

        public void SendMarker(string markerType, object value = null)
        {
            if (string.IsNullOrWhiteSpace(markerType))
            {
                GBLog.Error("SendMarker called without a markerType; marker dropped.");
                return;
            }

            if (value != null && IsPrimitiveLike(value.GetType()))
            {
                GBLog.Error($"Marker '{markerType}' has a bare {value.GetType().Name} payload; " +
                            "markers must use object-like payloads (anonymous type, POCO, or dictionary).");
                return;
            }

            var marker = new MarkerPayload
            {
                MarkerId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                EventName = markerType,
                SessionId = _context.SessionId,
                DistinctId = _context.Identity.DistinctId,
                Properties = value,
            };

            bool shouldFlush;
            lock (_pendingLock)
            {
                _pending.Add(marker);
                shouldFlush = _pending.Count >= MaxBatchSize;
            }

            if (shouldFlush)
            {
                Flush();
            }
        }

        private static bool IsPrimitiveLike(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
        }

        private void Flush()
        {
            MarkerBatch batch;
            _timeSinceLastFlush = 0f;

            lock (_pendingLock)
            {
                if (_pending.Count == 0) return;

                batch = new MarkerBatch { Markers = new List<MarkerPayload>(_pending) };
                _pending.Clear();
            }

            SendBatchAsync(batch);
        }

        private async void SendBatchAsync(MarkerBatch batch)
        {
            try
            {
                var response = await _context.Api.PostMarkersAsync(batch);
                if (response.IsSuccess)
                {
                    GBLog.Info($"Sent {batch.Markers.Count} marker(s).");
                }
                else
                {
                    // TODO: retry/persist failed batches (parity with Roblox OnMarkersFailed).
                    GBLog.Error($"Failed to send {batch.Markers.Count} marker(s): {response.Describe()}");
                }
            }
            catch (Exception ex)
            {
                GBLog.Error($"Error sending markers: {ex}");
            }
        }

        public void Tick(float deltaTime)
        {
            lock (_pendingLock)
            {
                if (_pending.Count == 0)
                {
                    _timeSinceLastFlush = 0f;
                    return;
                }
            }

            _timeSinceLastFlush += deltaTime;
            if (_timeSinceLastFlush >= FlushIntervalSeconds)
            {
                Flush();
            }
        }

        public void OnApplicationPause(bool paused)
        {
            // Backgrounded apps can be killed without OnApplicationQuit; flush now.
            if (paused)
            {
                Flush();
            }
        }

        public void Shutdown()
        {
            Flush();
        }
    }
}
