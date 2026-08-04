using System;
using System.Collections.Generic;
using Gamebeast.Internal.Http;
using Gamebeast.Internal.Models;
using Gamebeast.Internal.Utils;

namespace Gamebeast.Internal.Services
{
    /// <summary>
    /// Batches engagement markers and posts them to /sdk/v1/markers.
    /// Flushes when the batch is full, every few seconds, and when the app
    /// quits or is backgrounded.
    ///
    /// Failed batches are retried with exponential backoff: transport errors and
    /// server failures requeue (bounded by attempts and a total buffer cap), while
    /// permanent rejections (4xx — invalid markers, subscription limits) are dropped.
    ///
    /// On pause/quit the remaining buffer is persisted to disk before the final send
    /// attempt, and restored on the next launch, so markers survive app kills and
    /// offline sessions. Restored markers keep their original ids/session/identity.
    /// </summary>
    internal sealed class MarkersService : IMarkersService, IGamebeastService
    {
        private const int MaxBatchSize = 10;
        private const float FlushIntervalSeconds = 10f;

        private const int MaxSendAttempts = 5;
        private const float RetryBaseDelaySeconds = 2f; // 2s, 4s, 8s, 16s
        private const int MaxRetryBufferedMarkers = 500;

        private sealed class RetryBatch
        {
            public MarkerBatch Batch;
            public int NextAttempt;
            public float SecondsUntilSend;
        }

        private readonly List<MarkerPayload> _pending = new List<MarkerPayload>();
        private readonly object _pendingLock = new object();

        // Failed batches awaiting resend. Main-thread only (Tick + await continuations).
        private readonly List<RetryBatch> _retryBatches = new List<RetryBatch>();

        private GamebeastContext _context;
        private MarkerDiskStore _diskStore;
        private float _timeSinceLastFlush;

        public void Initialize(GamebeastContext context)
        {
            _context = context;
            _diskStore = new MarkerDiskStore(context.EnvironmentAlias);

            RestorePersistedMarkers();
        }

        /// <summary>Requeue markers persisted by a previous run; they send on the next Tick.</summary>
        private void RestorePersistedMarkers()
        {
            var restored = _diskStore.LoadAndClear();
            if (restored.Count == 0) return;

            if (restored.Count > MaxRetryBufferedMarkers)
            {
                GBLog.Warn($"Dropping {restored.Count - MaxRetryBufferedMarkers} persisted marker(s) over the buffer cap.");
                restored.RemoveRange(MaxRetryBufferedMarkers, restored.Count - MaxRetryBufferedMarkers);
            }

            for (var start = 0; start < restored.Count; start += MaxBatchSize)
            {
                var count = Math.Min(MaxBatchSize, restored.Count - start);
                _retryBatches.Add(new RetryBatch
                {
                    Batch = new MarkerBatch { Markers = restored.GetRange(start, count) },
                    NextAttempt = 1,
                    SecondsUntilSend = 0f,
                });
            }

            GBLog.Info($"Restored {restored.Count} persisted marker(s) from a previous session.");
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

            SendBatchAsync(batch, attempt: 1);
        }

        private async void SendBatchAsync(MarkerBatch batch, int attempt)
        {
            try
            {
                var response = await _context.Api.PostMarkersAsync(batch);
                if (response.IsSuccess)
                {
                    GBLog.Info($"Sent {batch.Markers.Count} marker(s).");

                    // Once nothing is buffered, drop the persisted snapshot so restored
                    // markers aren't replayed again on a later launch.
                    if (_retryBatches.Count == 0 && !HasPendingMarkers())
                    {
                        _diskStore.Save(null);
                    }
                    return;
                }

                // Permanent rejections won't improve on resend: invalid markers,
                // subscription limits, auth problems. Drop the batch.
                if (IsPermanentRejection(response))
                {
                    var error = HttpRequester.TryParse<SdkV2ErrorResponse>(response);
                    GBLog.Error($"Dropped {batch.Markers.Count} marker(s): " +
                                $"{(error != null ? $"{error.ErrorCode}: {error.Message}" : response.Describe())}");
                    return;
                }

                if (attempt >= MaxSendAttempts)
                {
                    GBLog.Error($"Dropped {batch.Markers.Count} marker(s) after {attempt} attempts: {response.Describe()}");
                    return;
                }

                QueueRetry(batch, attempt, response.Describe());
            }
            catch (Exception ex)
            {
                GBLog.Error($"Error sending markers: {ex}");
            }
        }

        private static bool IsPermanentRejection(GBHttpResponse response)
        {
            if (response.TransportError != null) return false;

            // 408 (timeout) and 429 (rate limited) are worth retrying; other 4xx are not.
            return response.StatusCode >= 400 && response.StatusCode < 500
                && response.StatusCode != 408 && response.StatusCode != 429;
        }

        private void QueueRetry(MarkerBatch batch, int failedAttempt, string reason)
        {
            var delay = RetryBaseDelaySeconds * (1 << (failedAttempt - 1));
            _retryBatches.Add(new RetryBatch
            {
                Batch = batch,
                NextAttempt = failedAttempt + 1,
                SecondsUntilSend = delay,
            });

            GBLog.Warn($"Failed to send {batch.Markers.Count} marker(s) ({reason}); " +
                       $"retrying in {delay:0}s (attempt {failedAttempt} of {MaxSendAttempts}).");

            // Bound memory during long outages: drop the oldest buffered batches first.
            var buffered = 0;
            foreach (var entry in _retryBatches)
            {
                buffered += entry.Batch.Markers.Count;
            }
            while (buffered > MaxRetryBufferedMarkers && _retryBatches.Count > 1)
            {
                buffered -= _retryBatches[0].Batch.Markers.Count;
                GBLog.Error($"Marker retry buffer full; dropped {_retryBatches[0].Batch.Markers.Count} oldest marker(s).");
                _retryBatches.RemoveAt(0);
            }
        }

        public void Tick(float deltaTime)
        {
            TickRetries(deltaTime);

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

        private void TickRetries(float deltaTime)
        {
            if (_retryBatches.Count == 0) return;

            List<RetryBatch> due = null;
            foreach (var entry in _retryBatches)
            {
                entry.SecondsUntilSend -= deltaTime;
                if (entry.SecondsUntilSend <= 0f)
                {
                    (due ??= new List<RetryBatch>()).Add(entry);
                }
            }

            if (due == null) return;

            foreach (var entry in due)
            {
                _retryBatches.Remove(entry);
                SendBatchAsync(entry.Batch, entry.NextAttempt);
            }
        }

        private bool HasPendingMarkers()
        {
            lock (_pendingLock)
            {
                return _pending.Count > 0;
            }
        }

        /// <summary>
        /// Persist everything buffered, then send it right now (quit/backgrounding).
        /// The disk snapshot is written first: if the process dies before the sends
        /// land, the next launch replays them (the backend dedupes by marker id).
        /// </summary>
        private void PersistAndFlushEverything()
        {
            var snapshot = new List<MarkerPayload>();
            foreach (var entry in _retryBatches)
            {
                snapshot.AddRange(entry.Batch.Markers);
            }
            lock (_pendingLock)
            {
                snapshot.AddRange(_pending);
            }
            _diskStore.Save(snapshot);

            Flush();

            if (_retryBatches.Count == 0) return;

            var entries = _retryBatches.ToArray();
            _retryBatches.Clear();
            foreach (var entry in entries)
            {
                SendBatchAsync(entry.Batch, MaxSendAttempts);
            }
        }

        public void OnApplicationPause(bool paused)
        {
            // Backgrounded apps can be killed without OnApplicationQuit; persist + flush now.
            if (paused)
            {
                PersistAndFlushEverything();
            }
        }

        public void OnIdentityChanged()
        {
            // Markers stamp the distinct id at creation, so pending markers already
            // carry the identity they were recorded under; nothing to do.
        }

        public void Shutdown()
        {
            PersistAndFlushEverything();
        }
    }
}
