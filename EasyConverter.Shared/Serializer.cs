using System.Text;
using System.Text.Json;

namespace EasyConverter.Shared
{
    public class Serializer
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        public static byte[] SerializeToBytes(object item)
        {
            return Encoding.UTF8.GetBytes(Serialize(item));
        }

        public static string Serialize(object item)
        {
            return JsonSerializer.Serialize(item, _options);
        }

        public static T Deserialize<T>(byte[] bytes)
        {
            var json = Encoding.UTF8.GetString(bytes);
            return Deserialize<T>(json);
        }

        public static T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }
    }
}
