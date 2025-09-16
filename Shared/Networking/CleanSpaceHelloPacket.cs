using ProtoBuf;
using CleanSpaceShared.Events;
using CleanSpaceShared.Struct;
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
            CleanSpaceShared.Util.MiscUtil.BasicReceivingPacketChecks(r);
            EventHub.OnCleanSpaceHelloReceived(this, r.SenderId, r.Target, r);
        }

        public override void ProcessServer<CleanSpaceHelloPacket>(CleanSpaceHelloPacket r)
        {
            CleanSpaceShared.Util.MiscUtil.BasicReceivingPacketChecks(r);
            EventHub.OnCleanSpaceHelloReceived(this, r.SenderId, r.Target, r);
        }
    }

}
