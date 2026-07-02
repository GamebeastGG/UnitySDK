using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gamebeast.Internal.Http;
using UnityEngine.Networking;

namespace Gamebeast.Editor
{
    [Serializable]
    public class HeatmapPoint
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class HeatmapBounds
    {
        public HeatmapPoint pointA;
        public HeatmapPoint pointB;
    }

    [Serializable]
    public class HeatmapDetails
    {
        public string id;
        public string name;
        public string description;
        public HeatmapBounds bounds;
        public float resolutionFactor;
    }

    /// <summary>
    /// Editor-only client for the heatmap management endpoints. Uses its own API key
    /// (entered in the window) rather than the runtime SDK session.
    /// </summary>
    public sealed class HeatmapEditorApi
    {
        private const string BaseUrl = "https://api.gamebeast.gg";

        private class CreatedHeatmapResponse
        {
            public string id;
        }

        private readonly string _apiKey;

        public HeatmapEditorApi(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task<HeatmapDetails[]> ListAsync()
        {
            var response = await SendAsync(UnityWebRequest.kHttpVerbGET, "/sdk/v1/heatmaps/list");
            return Parse<HeatmapDetails[]>(response);
        }

        /// <summary>Creates a heatmap and returns its new id.</summary>
        public async Task<string> CreateAsync(HeatmapDetails heatmap)
        {
            var response = await SendAsync(UnityWebRequest.kHttpVerbPOST, "/sdk/v1/heatmaps/create", body: heatmap);
            return Parse<CreatedHeatmapResponse>(response).id;
        }

        public async Task UpdateAsync(string id, HeatmapDetails details)
        {
            await SendAsync(UnityWebRequest.kHttpVerbPUT, $"/sdk/v1/heatmaps/{Uri.EscapeDataString(id)}", body: details);
        }

        public async Task DeleteAsync(string id)
        {
            await SendAsync(UnityWebRequest.kHttpVerbDELETE, $"/sdk/v1/heatmaps/{Uri.EscapeDataString(id)}");
        }

        public async Task UploadImageAsync(string id, byte[] pngBytes)
        {
            await SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                $"/sdk/v1/heatmaps/{Uri.EscapeDataString(id)}/image",
                body: pngBytes,
                extraHeaders: new Dictionary<string, string> { { "Content-Type", "image/png" } });
        }

        private async Task<GBHttpResponse> SendAsync(
            string method,
            string path,
            object body = null,
            Dictionary<string, string> extraHeaders = null)
        {
            var headers = new Dictionary<string, string>
            {
                { "authorization", _apiKey },
                { "sdkversion", GamebeastApiClient.SdkVersion },
                { "universeid", "0" },
                { "serverid", "unity-editor" },
                { "isstudio", "true" },
            };

            if (extraHeaders != null)
            {
                foreach (var header in extraHeaders)
                {
                    headers[header.Key] = header.Value;
                }
            }

            var response = await HttpRequester.SendAsync(method, BaseUrl + path, body: body, headers: headers);
            if (!response.IsSuccess)
            {
                throw new Exception($"{method} {path} failed: {response.Describe()} {response.Body}");
            }

            return response;
        }

        private static T Parse<T>(GBHttpResponse response) where T : class
        {
            var parsed = HttpRequester.TryParse<T>(response);
            if (parsed == null)
            {
                throw new Exception($"Could not parse response as {typeof(T).Name}: {response.Body}");
            }

            return parsed;
        }
    }
}
