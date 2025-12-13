using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor.UI;

namespace Gamebeast.Runtime.Internal.Utils
{
    public enum GBRequestType
    {
        GetSdkVersion,
        PostMarker
        // add more as needed
    }

    public enum GBRequestMethod
    {
        GET,
        POST
    }

    public class GBRequestInfo
    {
        public GBRequestMethod Method;
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
            { GBRequestType.GetSdkVersion, new GBRequestInfo { Method = GBRequestMethod.POST, Path = "/v1/sdk/version" } },
            { GBRequestType.PostMarker, new GBRequestInfo { Method = GBRequestMethod.POST, Path = "/v1/markers" } }
        };

        public static void SetApiKey(string apiKey)
        {
            _apiKey = apiKey;
        }

        public static Task<TResponse> MakeRequestAsync<TResponse>(GBRequestType requestType, object body = null)
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

            switch (method)
            {
                case GBRequestMethod.GET:
                    return Requester.GetAsync<TResponse>("/sdk" + path, body, headers);
                case GBRequestMethod.POST:
                    return Requester.PostAsync<TResponse>("/sdk" + path, body, headers);
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, null); // This should never happen
            }
        }
    }
}