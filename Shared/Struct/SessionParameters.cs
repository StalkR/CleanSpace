using ProtoBuf;
using System;
using System.IO;

namespace CleanSpaceShared.Struct
{
    [ProtoContract]
    public struct SessionParameters
    {
        [ProtoMember(1)]
        public SessionParameterRequest[] requests { get; set; }

        [ProtoMember(2)]
        public byte chatterLength { get; set; }

        [ProtoMember(3)]
        public byte[] sessionSalt { get; set; }

        public byte[] ToBytes()
        {
            return ProtoUtil.Serialize(requests);
        }
        public static SessionParameters FromBytes(byte[] data)
        {
           return ProtoUtil.Deserialize<SessionParameters>(data);
        }

        public static implicit operator byte[](SessionParameters p) => p.ToBytes();
        public static implicit operator SessionParameters(byte[] data) => FromBytes(data);
    }
}
