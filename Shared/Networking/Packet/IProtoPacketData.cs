using System;
using System.Linq;
using VRage;


namespace CleanSpaceShared.Networking
{
    public interface IProtoPacketData : IPacketData
    {
        object MessageObject { get; }
        void Write(ByteStream stream);
        void Read(ByteStream stream);
    }
}