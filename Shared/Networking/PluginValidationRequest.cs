using CleanSpace;
using CleanSpaceShared.Scanner;
using ProtoBuf;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Shared.Events;
using Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using VRage.GameServices;
using VRage.Network;

namespace CleanSpaceShared.Networking
{
    [ProtoContract]
    public class PluginValidationRequest : MessageBase
    {
        public string attestationSignature;
        public byte[] attestationChallenge;
        public override void ProcessClient<PluginValidationRequest>(PluginValidationRequest r)
        {          
            string token = r.Nonce;            
            if (token == null)
            {
                Shared.Plugin.Common.Logger.Error($"{PacketRegistry.PluginName}: Received a validation request from the server, but the server did not provide a token for a response!");
                return;
            }
            EventHub.OnServerCleanSpaceRequested(this, r.Target, r);
        }

        public override void ProcessServer<PluginValidationRequest>(PluginValidationRequest r)
        {
            Shared.Plugin.Common.Logger.Error($"{PacketRegistry.PluginName}: Received something that I should not have.");
            throw new System.SystemException($"{PacketRegistry.PluginName} encountered a critical issue it could not recover from. Please contact a developer with logs.");
        }
    }

}
