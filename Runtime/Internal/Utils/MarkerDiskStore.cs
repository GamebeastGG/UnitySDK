using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Gamebeast.Internal.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace Gamebeast.Internal.Utils
{
    /// <summary>
    /// Persists unsent markers on disk so events recorded right before the app was
    /// killed (or while offline) survive to the next launch. One file per environment.
    ///
    /// The file is a snapshot of whatever was still buffered when the app was last
    /// paused or quit. Markers carry unique ids, so the rare replay of an
    /// already-delivered marker is deduplicated by the backend.
    /// </summary>
    internal sealed class MarkerDiskStore
    {
        private sealed class Envelope
        {
            [JsonProperty("markers")]
            public List<MarkerPayload> Markers;
        }

        private readonly string _filePath;

        public MarkerDiskStore(string environmentAlias)
        {
            var directory = Path.Combine(Application.persistentDataPath, "Gamebeast");
            _filePath = Path.Combine(directory, $"markers_{Sanitize(environmentAlias)}.json");
        }

        /// <summary>Read persisted markers and remove the file; returns an empty list when there are none.</summary>
        public List<MarkerPayload> LoadAndClear()
        {
            try
            {
                if (!File.Exists(_filePath)) return new List<MarkerPayload>();

                var envelope = GamebeastJson.Deserialize<Envelope>(File.ReadAllText(_filePath));
                File.Delete(_filePath);

                return envelope?.Markers ?? new List<MarkerPayload>();
            }
            catch (Exception ex)
            {
                GBLog.Warn($"Could not read persisted markers ({_filePath}): {ex.Message}");
                return new List<MarkerPayload>();
            }
        }

        /// <summary>Write the current buffered markers; an empty list removes the file.</summary>
        public void Save(List<MarkerPayload> markers)
        {
            try
            {
                if (markers == null || markers.Count == 0)
                {
                    if (File.Exists(_filePath))
                    {
                        File.Delete(_filePath);
                    }
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
                File.WriteAllText(_filePath, GamebeastJson.Serialize(new Envelope { Markers = markers }));
            }
            catch (Exception ex)
            {
                GBLog.Warn($"Could not persist markers ({_filePath}): {ex.Message}");
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "default";

            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? char.ToLowerInvariant(c) : '-');
            }
            return sb.ToString();
        }
    }
}
