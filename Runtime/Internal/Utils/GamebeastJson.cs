using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Gamebeast.Internal.Utils
{
    /// <summary>
    /// Central JSON configuration for the SDK. Registers converters for common Unity
    /// value types so marker payloads can contain Vector3 etc. without the caller
    /// doing any conversion (Newtonsoft would otherwise choke on their self-referencing
    /// properties like Vector3.normalized).
    /// </summary>
    internal static class GamebeastJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters =
            {
                new Vector2Converter(),
                new Vector3Converter(),
                new QuaternionConverter(),
                new ColorConverter(),
            },
        };

        public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);

        // Unity vectors serialize as plain arrays ([x, y, z]) to match the wire format
        // the v1 markers backend already ingests.

        private sealed class Vector2Converter : JsonConverter<Vector2>
        {
            public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
            {
                new JArray(value.x, value.y).WriteTo(writer);
            }

            public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var arr = JArray.Load(reader);
                return new Vector2(arr.Value<float>(0), arr.Value<float>(1));
            }
        }

        private sealed class Vector3Converter : JsonConverter<Vector3>
        {
            public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
            {
                new JArray(value.x, value.y, value.z).WriteTo(writer);
            }

            public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var arr = JArray.Load(reader);
                return new Vector3(arr.Value<float>(0), arr.Value<float>(1), arr.Value<float>(2));
            }
        }

        private sealed class QuaternionConverter : JsonConverter<Quaternion>
        {
            public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
            {
                new JArray(value.x, value.y, value.z, value.w).WriteTo(writer);
            }

            public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var arr = JArray.Load(reader);
                return new Quaternion(arr.Value<float>(0), arr.Value<float>(1), arr.Value<float>(2), arr.Value<float>(3));
            }
        }

        private sealed class ColorConverter : JsonConverter<Color>
        {
            public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
            {
                new JArray(value.r, value.g, value.b, value.a).WriteTo(writer);
            }

            public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var arr = JArray.Load(reader);
                return new Color(arr.Value<float>(0), arr.Value<float>(1), arr.Value<float>(2), arr.Value<float>(3));
            }
        }
    }
}
