using ProtoBuf;
using System;
using System.IO;

namespace CleanSpaceShared.Struct
{
    [ProtoContract]
    public struct ChatterChallenge
    {
        [ProtoMember(1)]
        public ChatterChallengeRequest[] requests { get; set; }

        [ProtoMember(2)]
        public byte chatterLength { get; set; }

        [ProtoMember(3)]
        public byte[] sessionSalt { get; set; }

        public byte[] ToBytes()
        {
            return ProtoUtil.Serialize(requests);
        }
        public static ChatterChallenge FromBytes(byte[] data)
        {
           return ProtoUtil.Deserialize<ChatterChallenge>(data);
        }

        public static implicit operator byte[](ChatterChallenge p) => p.ToBytes();
        public static implicit operator ChatterChallenge(byte[] data) => FromBytes(data);
    }
}
