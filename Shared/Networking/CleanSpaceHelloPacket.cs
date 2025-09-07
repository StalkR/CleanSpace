using ProtoBuf;
using Shared.Events;
namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class CleanSpaceHelloPacket : MessageBase
    {
        [ProtoMember(6)]
        public byte[] sessionParameters;
        public uint client_ip_echo;
        // When a client receives CleanSpaceHelloPacket, the client is receiving hello instructions.
        public override void ProcessClient<CleanSpaceHelloPacket>(CleanSpaceHelloPacket r)
        {
            Shared.Util.MiscUtil.BasicReceivingPacketChecks(r);
            EventHub.OnCleanSpaceHelloReceived(this, r.SenderId, r.Target);
        }

        // When a server receives CleanSpaceHelloPacket, the server is sending hello instructions. 
        public override void ProcessServer<CleanSpaceHelloPacket>(CleanSpaceHelloPacket r)
        {
            Shared.Util.MiscUtil.BasicReceivingPacketChecks(r);
            EventHub.OnCleanSpaceHelloReceived(this, r.SenderId, r.Target);
        }
    }

}
