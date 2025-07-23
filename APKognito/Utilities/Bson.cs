using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace APKognito.Utilities;

internal static class Bson
{
    public static byte[] Serialize<T>(T obj)
    {
        using MemoryStream ms = new();
        using BsonDataWriter writer = new(ms);

        JsonSerializer serializer = new();
        serializer.Serialize(writer, obj);

        return ms.ToArray();
    }

    public static T? Deserialize<T>(byte[] data)
    {
        using MemoryStream stream = new(data);
        using BsonDataReader reader = new(stream);

        var deserializer = new JsonSerializer();
        return deserializer.Deserialize<T>(reader);
    }
}
