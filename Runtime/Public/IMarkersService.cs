namespace Gamebeast
{
    /// <summary>
    /// Sends engagement markers (analytics events) to Gamebeast.
    /// Markers are batched and flushed automatically (every few seconds, when the
    /// batch is full, and when the app quits or is backgrounded).
    /// </summary>
    public interface IMarkersService
    {
        /// <summary>
        /// Send a marker attributed to the local user (<see cref="GamebeastSdk.DistinctId"/>).
        /// </summary>
        /// <param name="markerType">Event name, e.g. "level_completed".</param>
        /// <param name="value">
        /// Optional payload. Must be an object-like value (anonymous type, POCO, or dictionary),
        /// not a bare primitive. Unity types like Vector3 are serialized automatically.
        /// </param>
        void SendMarker(string markerType, object value = null);
    }
}
