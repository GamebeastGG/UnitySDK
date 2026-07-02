using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Gamebeast.Internal.Utils;
using UnityEngine.Networking;

namespace Gamebeast.Internal.Http
{
    /// <summary>
    /// Raw HTTP response. Protocol errors (4xx/5xx) are returned, not thrown, so
    /// callers can react to specific status codes (e.g. 304 Not Modified, 404).
    /// Transport failures (no connection, DNS, ...) set <see cref="TransportError"/>.
    /// </summary>
    internal sealed class GBHttpResponse
    {
        public long StatusCode;
        public string Body;
        public string TransportError;

        public bool IsSuccess => TransportError == null && StatusCode >= 200 && StatusCode < 300;
        public bool IsNotModified => TransportError == null && StatusCode == 304;

        public string Describe()
        {
            return TransportError != null ? $"transport error: {TransportError}" : $"HTTP {StatusCode}";
        }
    }

    /// <summary>
    /// Thin async wrapper around UnityWebRequest. JSON in/out by default; byte[]
    /// bodies are sent raw (callers set Content-Type via headers).
    /// </summary>
    internal static class HttpRequester
    {
        public static async Task<GBHttpResponse> SendAsync(
            string method,
            string url,
            Dictionary<string, string> query = null,
            object body = null,
            Dictionary<string, string> headers = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Url must not be null or empty.", nameof(url));
            }

            url = AppendQuery(url, query);

            using (var request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();

                if (body != null)
                {
                    if (body is byte[] rawBytes)
                    {
                        request.uploadHandler = new UploadHandlerRaw(rawBytes);
                        request.SetRequestHeader("Content-Type", "application/octet-stream");
                    }
                    else
                    {
                        var json = GamebeastJson.Serialize(body);
                        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                        request.SetRequestHeader("Content-Type", "application/json");
                        GBLog.Info($"{method} {url} body: {json}");
                    }
                }

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.SetRequestHeader(header.Key, header.Value);
                    }
                }

                request.SetRequestHeader("Accept", "application/json");

                await AwaitRequest(request);

                var response = new GBHttpResponse
                {
                    StatusCode = request.responseCode,
                    Body = request.downloadHandler != null ? request.downloadHandler.text : null,
                };

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    response.TransportError = request.error;
                }

                GBLog.Info($"{method} {url} -> {response.Describe()}");
                return response;
            }
        }

        /// <summary>Deserialize a JSON response body into <typeparamref name="T"/>; null on failure.</summary>
        public static T TryParse<T>(GBHttpResponse response) where T : class
        {
            if (response == null || string.IsNullOrEmpty(response.Body)) return null;

            try
            {
                return GamebeastJson.Deserialize<T>(response.Body);
            }
            catch (Exception ex)
            {
                GBLog.Warn($"Failed to parse response as {typeof(T).Name}: {ex.Message}");
                return null;
            }
        }

        private static Task AwaitRequest(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<object>();
            var operation = request.SendWebRequest();
            operation.completed += _ => tcs.TrySetResult(null);
            return tcs.Task;
        }

        private static string AppendQuery(string url, Dictionary<string, string> query)
        {
            if (query == null || query.Count == 0) return url;

            var sb = new StringBuilder(url);
            var first = !url.Contains("?");
            foreach (var kv in query)
            {
                if (kv.Value == null) continue;

                sb.Append(first ? '?' : '&');
                first = false;
                sb.Append(Uri.EscapeDataString(kv.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(kv.Value));
            }

            return sb.ToString();
        }
    }
}
