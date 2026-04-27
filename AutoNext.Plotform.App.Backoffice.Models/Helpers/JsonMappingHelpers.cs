using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoNext.Plotform.App.Backoffice.Models.Helpers
{
    public static class JsonMappingHelpers
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static string? SerializeList(List<string>? value)
            => value is not null
                ? JsonSerializer.Serialize(value, _jsonOptions)
                : null;

        public static string? SerializeObject<T>(T? value) where T : class
            => value is not null
                ? JsonSerializer.Serialize(value, _jsonOptions)
                : null;

        public static List<string>? DeserializeList(string? json)
            => !string.IsNullOrWhiteSpace(json)
                ? JsonSerializer.Deserialize<List<string>>(json, _jsonOptions)
                : null;

        public static Dictionary<string, object>? DeserializeDict(string? json)
            => !string.IsNullOrWhiteSpace(json)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions)
                : null;
    }
}
