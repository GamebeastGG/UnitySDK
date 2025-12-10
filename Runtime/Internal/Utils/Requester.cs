using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Gamebeast.Runtime.Internal.Utils
{
	/// <summary>
	/// Simple helper for making JSON-based HTTP requests from Unity.
	///
	/// Usage:
	/// await Requester.GetAsync<ResponseType>("/v1/path", new { foo = "bar" });
	/// await Requester.PostAsync<ResponseType>("/v1/resource", new { foo = "bar" });
	///
	/// The base URL is hardcoded but the path differs per call. The body object is
	/// converted into a query string for GET, and sent as JSON for POST.
	/// Both payloads and responses are assumed to be JSON.
	/// </summary>
	public static class Requester
	{
		private const string BaseUrl = "https://api.gamebeast.gg";

		/// <summary>
		/// Perform a GET request. The body object is encoded as a query string.
		/// </summary>
		public static Task<TResponse> GetAsync<TResponse>(string path, object body = null, Dictionary<string, string> headers = null)
		{
			return SendAsync<TResponse>(UnityWebRequest.kHttpVerbGET, path, body, headers);
		}

		/// <summary>
		/// Perform a POST request. The body object is serialized to JSON.
		/// </summary>
		public static Task<TResponse> PostAsync<TResponse>(string path, object body = null, Dictionary<string, string> headers = null)
		{
			return SendAsync<TResponse>(UnityWebRequest.kHttpVerbPOST, path, body, headers);
		}

		/// <summary>
		/// Core request logic.
		/// </summary>
		private static async Task<TResponse> SendAsync<TResponse>(string method, string path, object body, Dictionary<string, string> headers = null)
		{
			var json = await SendAsync(method, path, body, headers).ConfigureAwait(false);

			if (string.IsNullOrEmpty(json))
			{
				return default;
			}

			try
			{
				return JsonUtility.FromJson<TResponse>(json);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[Requester] Failed to deserialize JSON to {typeof(TResponse).Name}: {ex}\nJSON: {json}");
				throw;
			}
		}

		private static async Task<string> SendAsync(string method, string path, object body, Dictionary<string, string> headers = null)
		{
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentException("Path must not be null or empty", nameof(path));
			}

			// Ensure path formatting.
			if (!path.StartsWith("/"))
			{
				path = "/" + path;
			}

			string url = BaseUrl.TrimEnd('/') + path;

			// For GET, append query string built from body.
			if (method == UnityWebRequest.kHttpVerbGET && body != null)
			{
				string query = BuildQueryString(body);
				if (!string.IsNullOrEmpty(query))
				{
					url += url.Contains("?") ? "&" + query : "?" + query;
				}
			}

			using (var request = new UnityWebRequest(url, method))
			{
				request.downloadHandler = new DownloadHandlerBuffer();

				if (method == UnityWebRequest.kHttpVerbPOST)
				{
					// Use Newtonsoft.Json so we can correctly handle arbitrary object graphs and polymorphic 'value' fields.
					string jsonBody;
					if (body == null)
					{
						jsonBody = "{}";
						Debug.Log("[Requester] POST body is null, sending empty JSON object {}");
					}
					else
					{
						jsonBody = JsonConvert.SerializeObject(body);
					}

					Debug.Log($"[Requester] POST {url} with body: {jsonBody} and {(body != null ? body.GetType().Name : "<null>")}");

					byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
					request.uploadHandler = new UploadHandlerRaw(bodyRaw);
					request.SetRequestHeader("Content-Type", "application/json");
				}

                // Apply headers
				if (headers != null)
				{
					foreach (var header in headers)
					{
						request.SetRequestHeader(header.Key, header.Value);
					}
				}

				request.SetRequestHeader("Accept", "application/json");

				await request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER 
				if (request.result == UnityWebRequest.Result.ConnectionError ||
					request.result == UnityWebRequest.Result.ProtocolError)
				{
					Debug.LogError($"[Requester] HTTP {request.responseCode} {request.error} for {url}\nBody: {request.downloadHandler.text}");
					throw new Exception($"HTTP error {request.responseCode}: {request.error}");
				}
#else
				if (request.isNetworkError || request.isHttpError)
				{
					Debug.LogError($"[Requester] HTTP {request.responseCode} {request.error} for {url}\nBody: {request.downloadHandler.text}");
					throw new Exception($"HTTP error {request.responseCode}: {request.error}");
				}
#endif

				return request.downloadHandler.text;
			}
		}

		/// <summary>
		/// Builds a URL-encoded query string from an anonymous or POCO object.
		/// Only simple property types (string, numeric, bool) are supported.
		/// </summary>
		private static string BuildQueryString(object body)
		{
			if (body == null)
			{
				return string.Empty;
			}

			var dict = new Dictionary<string, string>();

			if (body is IDictionary<string, string> stringDict)
			{
				foreach (var kv in stringDict)
				{
					if (kv.Value != null)
					{
						dict[kv.Key] = kv.Value;
					}
				}
			}
			else if (body is IDictionary<string, object> objDict)
			{
				foreach (var kv in objDict)
				{
					if (kv.Value != null)
					{
						dict[kv.Key] = kv.Value.ToString();
					}
				}
			}
			else
			{
				var type = body.GetType();
				var props = type.GetProperties();
				foreach (var prop in props)
				{
					if (!prop.CanRead) continue;
					var value = prop.GetValue(body, null);
					if (value == null) continue;
					dict[prop.Name] = value.ToString();
				}
			}

			var sb = new StringBuilder();
			bool first = true;
			foreach (var kv in dict)
			{
				if (!first)
				{
					sb.Append('&');
				}
				first = false;

				sb.Append(Uri.EscapeDataString(kv.Key));
				sb.Append('=');
				sb.Append(Uri.EscapeDataString(kv.Value));
			}

			return sb.ToString();
		}
	}
}

