using CleanSpace;
using CleanSpaceShared.Scanner;
using ProtoBuf;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
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
                
        public override void ProcessClient<PluginValidationRequest>(PluginValidationRequest r)
        {          
            string token = r.Nonce;            
            if (token == null)
            {                
                PacketRegistry.Logger.Error($"{PacketRegistry.PluginName}: Received a validation request from the server, but the server did not provide a token for a response!");
                return;
            }

            var steamId = Sandbox.Engine.Networking.MyGameService.OnlineUserId;
            string newToken = ValidationManager.RegisterNonceForPlayer(r.SenderId);         
            var message = new PluginValidationResponse
            {
                SenderId = steamId,
                TargetType = MessageTarget.Server,
                Target = r.SenderId,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Nonce = token,
                PluginHashes = AssemblyScanner.GetPluginAssemblies().Select<Assembly, string>((a)=>AssemblyScanner.GetAssemblyFingerprint(a)).ToList()
            };

           // MiscUtil.PrintStateFor(PacketRegistry.Logger, r.SenderId);           
            PacketRegistry.Send(message, new EndpointId(r.SenderId), newToken, token);            
            PacketRegistry.Logger.Info($"{PacketRegistry.PluginName}: Sending a response to validation request.");
        }

        public override void ProcessServer<PluginValidationRequest>(PluginValidationRequest r)
        {
            PacketRegistry.Logger.Error($"{PacketRegistry.PluginName}: Received something that I shouldn'tve.");
            throw new System.SystemException("Something is wrong. Closing down");
        }
    }

}
