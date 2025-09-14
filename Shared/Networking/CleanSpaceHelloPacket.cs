using ProtoBuf;
using Shared.Events;
using Shared.Struct;
namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class CleanSpaceHelloPacket : MessageBase
    {
        [ProtoMember(7)]
        public byte[] sessionParameters;
        [ProtoMember(8)]
        public uint client_ip_echo;

        public override void ProcessClient<CleanSpaceHelloPacket>(CleanSpaceHelloPacket r)
        {
            Shared.Util.MiscUtil.BasicReceivingPacketChecks(r);
            EventHub.OnCleanSpaceHelloReceived(this, r.SenderId, r.Target, r);
        }

        public override void ProcessServer<CleanSpaceHelloPacket>(CleanSpaceHelloPacket r)
        {
            Shared.Util.MiscUtil.BasicReceivingPacketChecks(r);
            EventHub.OnCleanSpaceHelloReceived(this, r.SenderId, r.Target, r);
        }
    }

}
