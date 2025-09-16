using ProtoBuf;
using System.IO;
public static class ProtoUtil
{
    public static byte[] Serialize<T>(T instance)
    {
        using (var ms = new MemoryStream())
        {
            Serializer.Serialize(ms, instance);
            return ms.ToArray();
        }
    }

    public static T Deserialize<T>(byte[] data)
    {
        using (var ms = new MemoryStream(data))
        {
            return Serializer.Deserialize<T>(ms);
        }
    }
}