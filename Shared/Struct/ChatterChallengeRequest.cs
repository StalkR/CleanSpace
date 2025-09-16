using ProtoBuf;

namespace CleanSpaceShared.Struct
{
    [ProtoContract]
    public struct ChatterChallengeRequest
    {
        [ProtoMember(1)]
        public byte request { get; set; }

        [ProtoMember(2)]
        public byte[] context { get; set; }
    }
}