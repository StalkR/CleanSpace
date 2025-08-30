using ProtoBuf;

namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class Envelope
    {
        [ProtoMember(1)] public ushort PacketId;
        [ProtoMember(2)] public bool IsCompressed;
        [ProtoMember(3)] public byte[] Payload; // Serialized (and maybe compressed) message
        [ProtoMember(4)] public bool IsEncrypted;
        [ProtoMember(5)] public byte[] Key;
        [ProtoMember(6)] public byte[] Salt;
        [ProtoMember(7)] public byte[] IV;
    }
}