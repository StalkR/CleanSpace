using CleanSpaceShared.Util;
using ProtoBuf;
using System.IO;

namespace CleanSpaceShared.Networking
{
    public static class MessageFactory
    {
        public static NetworkEnvelope Wrap<T>(T message, bool compress = false) where T : MessageBase
        {
            ushort packetId = PacketRegistry.GetPacketId<T>();
            byte[] payload;
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, message);
                payload = ms.ToArray();
            }

            if (compress)
                payload = CompressionUtil.Compress(payload);

            return new NetworkEnvelope
            {
                PacketId = packetId,
                IsCompressed = compress,
                Payload = payload
            };
        }

        public static T Unwrap<T>(NetworkEnvelope envelope) where T : MessageBase
        {
            byte[] data = envelope.IsCompressed
                ? CompressionUtil.Decompress(envelope.Payload)
                : envelope.Payload;
            MemoryStream ms;
            T res = null;
            using (ms = new MemoryStream(data))
            {
                res = Serializer.Deserialize<T>(ms);
            }
            return res;
        }
        
    }
}
