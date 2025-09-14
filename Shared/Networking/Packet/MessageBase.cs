using ProtoBuf;
using System;

namespace CleanSpaceShared.Networking
{
    public enum MessageTarget
    {
        Client,
        Server
    }

    [ProtoContract]
    [ProtoInclude(107, typeof(CleanSpaceHelloPacket))]
    [ProtoInclude(108, typeof(CleanSpaceChatterPacket))]
    [ProtoInclude(110, typeof(PluginValidationRequest))]
    [ProtoInclude(111, typeof(PluginValidationResponse))]
    [ProtoInclude(112, typeof(PluginValidationResult))]
    public abstract class MessageBase
    {
        [ProtoMember(1)] public ulong SenderId;
        [ProtoMember(2)] public MessageTarget TargetType;
        [ProtoMember(3)] public ulong Target;
        [ProtoMember(4)] public long UnixTimestamp;
        [ProtoMember(5)] public string NonceS;
        [ProtoMember(6)] public string NonceC;
        [ProtoIgnore] public bool should_compress = false;
        public abstract void ProcessClient<T>(T msg) where T : MessageBase;
        public abstract void ProcessServer<T>(T msg) where T : MessageBase;
    }
}