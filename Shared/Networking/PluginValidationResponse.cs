using ProtoBuf;

using System;
using System.Collections.Generic;
using System.Linq;
namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class PluginValidationResponse : MessageBase
    {
        [ProtoMember(2)]
        public List<string> PluginHashes;
        [ProtoMember(3)]
        public string CleanSpaceHash;
    }

}
