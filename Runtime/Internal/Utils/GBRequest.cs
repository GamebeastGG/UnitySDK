using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Gamebeast.Runtime.Internal.Utils
{
    public enum GBRequestType
    {
        GetSdkVersion,
        PostMarker,
        GetRequest,
        StartRequest,
        CompleteRequest,
        GetConfigs,

        // add more as needed

        //Plugin
        GetHeatmaps,
        CreateHeatmap,
        UpdateHeatmap,
        DeleteHeatmap
        }

    public enum GBRequestMethod
    {
        GET,
        POST,
        PUT
    }

    public class GBRequestInfo
    {
        public string Method;
        public string Path;
    }

    /// <summary>
    /// Simple helper for making JSON-based HTTP requests from Unity.
    ///
    /// Usage:
    /// await Requester.GetAsync<ResponseType>("/v1/path", new { foo = "bar" });
    /// await Requester.PostAsync<ResponseType>("/v1/resource", new { foo = "bar" });
    ///
    /// The base URL is hardcoded but the path differs per call. The body object is
    /// converted into a query string for GET, and sent as JSON for POST.
    /// </summary>
    public class GBRequest
    {

        private static string _apiKey;
        private static readonly Dictionary<GBRequestType, GBRequestInfo> GBRequestTypeMap = new Dictionary<GBRequestType, GBRequestInfo>
        {
            { GBRequestType.GetSdkVersion, new GBRequestInfo { Method = UnityWebRequest.kHttpVerbPOST, Path = "/v1/sdk/version" } },
            { GBRequestType.PostMarker, new GBRequestInfo { Method = UnityWebRequest.kHttpVerbPOST, Path = "/v1/markers" } },
            { GBRequestType.GetRequest, new GBRequestInfo { Method = UnityWebRequest.kHttpVerbGET, Path = "/v1/requests" } },
            { GBRequestType.StartRequest, new GBRequestInfo { Method = UnityWebRequest.kHttpVerbPUT, Path = "/v1/requests/started" } },
            { GBRequestType.CompleteRequest, new GBRequestInfo { Method = UnityWebRequest.kHttpVerbPOST, Path = "/v1/requests/completed" } },
            { GBRequestType.GetConfigs, new GBRequestInfo { Method = UnityWebRequest.kHttpVerbGET, Path = "/v1/configurations" } },

            // Plugin

            { GBRequestType.GetHeatmaps, new GBRequestInfo { Method = UnityWebRequest.kHttpVerbGET, Path = "/v1/heatmaps/list" } },
            { GBRequestType.CreateHeatmap, new GBRequestInfo { Method = UnityWebRequest.kHttpVerbPOST, Path = "/v1/heatmaps/create" } },
            { GBRequestType.UpdateHeatmap, new GBRequestInfo { Method = UnityWebRequest.kHttpVerbPUT, Path = "/v1/heatmaps/{id}" } },
            { GBRequestType.DeleteHeatmap, new GBRequestInfo { Method = UnityWebRequest.kHttpVerbDELETE, Path = "/v1/heatmaps/{id}" } },
        };

        public static void SetApiKey(string apiKey)
        {
            _apiKey = apiKey;
        }

        public static Task<TResponse> MakeRequestAsync<TResponse>(
            GBRequestType requestType,
            object body = null,
            Dictionary<string, string> urlParams = null)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("API key is not set. Call SetApiKey() before making requests.");
            }

            var headers = new Dictionary<string, string> //TODO update backend so these can be set properly
            {
                { "authorization", _apiKey },
                { "sdkversion", "0.8.1" },
                { "universeid", "0" },
                { "serverid", "unity-0000" },
                { "isstudio", "true" }
            };

            if (!GBRequestTypeMap.ContainsKey(requestType))
            {
                throw new ArgumentException($"Unsupported request type: {requestType}", nameof(requestType));
            }

            var path = GBRequestTypeMap[requestType].Path;
            var method = GBRequestTypeMap[requestType].Method;

            path = ApplyUrlParams(path, urlParams);

            switch (method)
            {
                case UnityWebRequest.kHttpVerbGET:
                    return Requester.GetAsync<TResponse>("/sdk" + path, body, headers);
                default:
                    return Requester.PostAsync<TResponse>("/sdk" + path, body, headers, method);
            }
        }

        private static string ApplyUrlParams(string path, Dictionary<string, string> urlParams)
        {
            if (string.IsNullOrEmpty(path) || urlParams == null || urlParams.Count == 0) return path;

            foreach (var kv in urlParams)
            {
                var key = kv.Key;
                var value = kv.Value ?? string.Empty;
                var encoded = Uri.EscapeDataString(value);
                path = path
                    .Replace("{" + key + "}", encoded)
                    .Replace(":" + key, encoded);
            }

            return path;
        }
    }
}