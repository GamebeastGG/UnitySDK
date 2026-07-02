using System;
using System.IO;
using System.Text;
using Gamebeast.Internal.Http;
using Gamebeast.Internal.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace Gamebeast.Internal.Utils
{
    /// <summary>
    /// Persists the last configuration response on disk so configs are available
    /// immediately on the next launch (and offline), before the network refresh
    /// lands. One file per (environment, configuration alias) pair.
    ///
    /// Entries are stamped with the app version (and SDK version) that wrote them:
    /// a cache written by a different build is discarded, so an updated game never
    /// boots with configs meant for the previous version.
    /// </summary>
    internal sealed class ConfigDiskCache
    {
        private sealed class Envelope
        {
            [JsonProperty("appVersion")]
            public string AppVersion;

            [JsonProperty("sdkVersion")]
            public string SdkVersion;

            [JsonProperty("response")]
            public SdkConfigurationV2Response Response;
        }

        private readonly string _filePath;

        public ConfigDiskCache(string environmentAlias, string configurationAlias)
        {
            var directory = Path.Combine(Application.persistentDataPath, "Gamebeast");
            var fileName = $"configuration_{Sanitize(environmentAlias)}_{Sanitize(configurationAlias)}.json";
            _filePath = Path.Combine(directory, fileName);
        }

        public SdkConfigurationV2Response Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return null;

                var envelope = GamebeastJson.Deserialize<Envelope>(File.ReadAllText(_filePath));
                if (envelope?.Response == null) return null;

                if (envelope.AppVersion != Application.version || envelope.SdkVersion != GamebeastApiClient.SdkVersion)
                {
                    GBLog.Info($"Discarding config cache written by app {envelope.AppVersion}/sdk {envelope.SdkVersion} " +
                               $"(running app {Application.version}/sdk {GamebeastApiClient.SdkVersion}).");
                    return null;
                }

                return envelope.Response;
            }
            catch (Exception ex)
            {
                GBLog.Warn($"Could not read config cache ({_filePath}): {ex.Message}");
                return null;
            }
        }

        public void Save(SdkConfigurationV2Response response)
        {
            try
            {
                var envelope = new Envelope
                {
                    AppVersion = Application.version,
                    SdkVersion = GamebeastApiClient.SdkVersion,
                    Response = response,
                };

                Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
                File.WriteAllText(_filePath, GamebeastJson.Serialize(envelope));
            }
            catch (Exception ex)
            {
                GBLog.Warn($"Could not write config cache ({_filePath}): {ex.Message}");
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
