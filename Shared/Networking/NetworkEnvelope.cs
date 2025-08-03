using ProtoBuf;

namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class NetworkEnvelope
    {
        [ProtoMember(1)] public ushort PacketId;
        [ProtoMember(2)] public bool IsCompressed;
        [ProtoMember(3)] public byte[] Payload; // Serialized (and maybe compressed) message
    }
}