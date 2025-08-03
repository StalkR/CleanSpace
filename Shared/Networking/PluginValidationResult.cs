using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Utils;

namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class PluginValidationResult : MessageBase
    {
        [ProtoMember(1)]
        public bool Success;

        [ProtoMember(2)]
        public string Message;

    }

}
