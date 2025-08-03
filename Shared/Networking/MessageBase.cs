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
    [ProtoInclude(110, typeof(PluginValidationRequest))]
    public abstract class MessageBase
    {
        [ProtoMember(1)] public ulong SenderId;
        [ProtoMember(2)] public MessageTarget TargetType;
        [ProtoMember(3)] public ulong Target;
        [ProtoMember(4)] public long UnixTimestamp;
        [ProtoMember(5)] public string Nonce;

        [ProtoIgnore] public bool should_compress = false;

        public static event Action<MessageBase> ProcessClientAction;
        public static event Action<MessageBase> ProcessServerAction;

        public void ProcessClient()
        {
            ProcessClientAction?.Invoke(this);
        }

        public void ProcessServer()
        {
            ProcessServerAction?.Invoke(this);
        }
    }
}