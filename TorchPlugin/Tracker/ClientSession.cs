using CleanSpace;
using CleanSpace.Patch;
using CleanSpaceShared.Networking;
using CleanSpaceShared.Scanner;
using NLog.Fluent;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Shared.Config;
using Shared.Events;
using Shared.Hasher;
using Shared.Logging;
using Shared.Plugin;
using Shared.Struct;
using Shared.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TorchPlugin.Hasher;
using VRage.GameServices;
using VRage.Network;

namespace TorchPlugin.Tracker
{
    public enum CS_CLIENT_STATE
    {
        PENDING,
        VALIDATION_REQUESTED,
        AWAITING_VALIDATION,
        VALIDATION_RESPONDED,
        VALIDATION_FINALIZED,
        CONNECTED
    }

    public class ClientSession
    {
        ulong steamId;
        uint origin;
        DateTime sessionStartTime;
        DateTime sessionEndTime;
        DateTime lastInteractionTime;
        DateTime lastValidationTime;
        DateTime nextValidationTime;
        ValidationResultData? validationResultData;
        
        byte[] clientHasherBytes;
        string hasherSignature;

        private CS_CLIENT_STATE connectionState;
        CS_CLIENT_STATE ConnectionState
        {
            get { return connectionState; }
            set
            {
                EventHub.OnConnectionStateChanged(this, steamId, (int)value);
                connectionState = value;
            }
        }
        readonly byte[] clientSalt = Encoding.UTF8.GetBytes(Common.InstanceSecret).Span(new Random().Next(0, (Encoding.UTF8.GetBytes(Common.InstanceSecret).Length - 16)), 16).ToArray();
        string clientPendingNonce;
        string serverPendingNonce;
        string lastServerNonce;
        string lastClientNonce;
        List<string> lastHashList;
        private ConnectedClientDataMsg initialMsg;

        bool events_initialized = false;
        private ClientSessionManager Manager => ClientSessionManager.Instance;
        public ClientSession(ConnectedClientDataMsg initialMsg)
        {
            this.initialMsg = initialMsg;
            this.connectionState = CS_CLIENT_STATE.PENDING;
            this.sessionStartTime = DateTime.UtcNow;
            EventHub.ClientCleanSpaceResponded += EventHub_ClientCleanSpaceResponded;
            this.clientHasherBytes = HasherFactory.GetNewHasherWithSecret(Common.InstanceSecret);

            MyP2PSessionState state = new MyP2PSessionState();
            if (MyGameService.Peer2Peer.GetSessionState(steamId, ref state))
            {
                this.origin = state.RemoteIP;
            }
            else
            {
                Log.Debug($"Failed to get a valid remote address for client {steamId}. Disconnecting.");
                Task.Run(()=>DelayedDisconnect());
                return;
            }
               

            Log.Debug($"Hasher generated for client with id {steamId}. Doing quick test. ");

            try
            {
                Shared.Hasher.HasherRunner.ValidateHasherRunnerBytes(clientHasherBytes);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to generate hasher for client with id {steamId}. " + ex.Message);
                throw new Exception("Security error");
            }
            Log.Debug($"Hasher validated for client with id {steamId}. Signing.");
            this.hasherSignature = TokenUtility.SignPayload(clientHasherBytes, clientSalt);
        }

        private void EventHub_ClientCleanSpaceResponded(object sender, CleanSpaceTargetedEventArgs e)
        {          
            if(e.Args.Length < 2)
            {
                return;
            }
       
            ulong senderId = (ulong)e.Args[0];
            if(senderId == this.steamId)
            {
                PluginValidationResponse r = (PluginValidationResponse)e.Args[1];

                List<string> realAnalysis = r.PluginHashes;

                if (!TokenUtility.SignPayload(clientHasherBytes, clientSalt).Equals(this.hasherSignature))
                {
                    throw new Exception("Security violation during clean space response.");
                }

                List<string> transformedAnalysis = realAnalysis.Select(x => HasherRunner.ExecuteRunner(this.clientHasherBytes, (string)x)).ToList();

                List<string> analysis = r.PluginHashes.Select((element) => AssemblyScanner.UnscrambleSecureFingerprint(element, Encoding.UTF8.GetBytes(r.Nonce))).ToList();
                this.validationResultData = ValidationManager.Validate(senderId, r.Nonce, analysis);
                this.ConnectionState = CS_CLIENT_STATE.VALIDATION_FINALIZED;

                if (this.validationResultData?.Code == ValidationResultCode.ALLOWED)
                {
                    this.AcceptConnection();               
                }
                else
                {
                    this.RejectConnection();
                }
            }



        }

        private void AcceptConnection(bool force = false)
        {
            if (connectionState != CS_CLIENT_STATE.CONNECTED)
            {
                connectionState = CS_CLIENT_STATE.CONNECTED; 
                EventHub.OnServerConnectionAccepted(this, steamId, validationResultData);
                Log.Info($"Sending join message to ID {steamId}");
                ConnectedClientPatch.CallPrivateMethod((MyDedicatedServerBase)MyDedicatedServerBase.Instance, ref initialMsg, initialMsg.ClientId);
            }
        }

      

        private void RejectConnection()
        {
            string newNonce = GenerateNewNonce();
            PluginValidationResult rs = new PluginValidationResult()
            {
                Code = validationResultData?.Code ?? ValidationResultCode.INVALID_TOKEN,
                Success = false,
                PluginList = validationResultData?.PluginList,
                SenderId = MyGameService.OnlineUserId,
                TargetType = MessageTarget.Client,
                Target = steamId,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Nonce = newNonce
            };
            EventHub.OnServerConnectionRejected(this, steamId, validationResultData);
            Log.Info($"Sending result packet containing information about rejection to ID {steamId} and scheduling a ticket cancellation for the request.");
            PacketRegistry.Send(rs, new EndpointId(steamId), newNonce);
            Task.Run(() => DelayedDisconnect());
        }

        public void InitiateCleanSpaceCheck()
        {

            if (((connectionState == CS_CLIENT_STATE.VALIDATION_FINALIZED) && (validationResultData?.Success ?? false)) || !Common.Config.Enabled)
            {
                MyP2PSessionState state = new MyP2PSessionState();
                if (MyGameService.Peer2Peer.GetSessionState(steamId, ref state))
                {
                    if(state.RemoteIP == this.origin)
                    {
                        AcceptConnection();
                    }
                    else
                    {
                        this.origin = state.RemoteIP;
                        Log.Warn($"Client {steamId} connected from a different origin. Their clean space session was discarded.");
                        ResetState();                       
                    }
                }

               
            }
            EventHub.OnServerCleanSpaceRequested(this, this.steamId, initialMsg);         
            Common.Logger.Info($"{Common.PluginName}: Initiating clean space request for player {steamId}...");

            if(ConnectionState != CS_CLIENT_STATE.PENDING)
            {              
                throw new Exception($"InitiateCleanSpaceCheck called but client state was not pending for client ID {steamId}.");
            }


            byte[] messageIV = new byte[12];
            new Random().NextBytes(messageIV);

            var newNonce = GenerateNewNonce();
            var newValidationRequestMessage = new PluginValidationRequest
            {
                SenderId = MyGameService.OnlineUserId,
                TargetType = MessageTarget.Client,
                Target = steamId,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Nonce = newNonce,                
                attestationChallenge = EncryptionUtil.EncryptBytesWithIV(this.clientHasherBytes, newNonce, this.clientSalt, messageIV).Concat(messageIV).Concat(clientSalt).ToArray(),
                attestationSignature = this.hasherSignature
            };
            PacketRegistry.Send(newValidationRequestMessage, new EndpointId(steamId), newNonce);            
            Task.Run(() => DelayedDisconnectWithGroupRedirect());
        }

        private string GenerateNewNonce()
        {
            this.lastServerNonce = (string)this.serverPendingNonce.Clone();
            return (this.serverPendingNonce = ValidationManager.RegisterNonceForPlayer(steamId, true));
        }
        private void ResetState()
        {
          
        }

        private void PrintP2PConnectionState(ulong steamid)
        {
            MyP2PSessionState state = new MyP2PSessionState();
            if (MyGameService.Peer2Peer.GetSessionState(steamid, ref state))
            {
                Log.Debug($"Connecting: {state.Connecting}");
                Log.Debug($"ConnectionActive: {state.ConnectionActive}");
                Log.Debug($"RemoteIP: {state.RemoteIP}");
                Log.Debug($"RemotePort: {state.RemotePort}");
            }
        }

        public async Task DelayedDisconnect()
        {
            await Task.Delay(1000);
            this.sessionEndTime = DateTimeOffset.UtcNow.DateTime;
            Common.Logger.Info($"Ticket cancelled for ID {steamId}.");
            ConnectedClientSendJoinPatch.CallPrivateMethod((MyDedicatedServerBase)MyDedicatedServerBase.Instance, steamId, JoinResult.TicketCanceled);
            ClientSessionManager.Instance?.RequestDispose(steamId);
        }

        public async Task DelayedDisconnectWithGroupRedirect()
        {
            await Task.Delay(10000);
            if (((int)ConnectionState) < ((int)CS_CLIENT_STATE.VALIDATION_RESPONDED))
            {
                this.sessionEndTime = DateTimeOffset.UtcNow.DateTime;
                Common.Logger.Info($"No response from ID {steamId} (held connection still present). Attempting to direct to information group.");
                ConnectedClientSendJoinPatch.CallPrivateMethod((MyDedicatedServerBase)MyDedicatedServerBase.Instance, steamId, JoinResult.NotInGroup);
                ClientSessionManager.Instance?.RequestDispose(steamId);
            }
        }

        public void Dispose()
        {
            EventHub.ClientCleanSpaceResponded -= EventHub_ClientCleanSpaceResponded;
        }
    }

    public class ClientSessionManager
    {
        public static ClientSessionManager Instance { get; private set; }
        public static string PluginName => Common.PluginName;
        public static IPluginConfig Config => Common.Plugin.Config;
        public static IPluginLogger Logger => Common.Logger;
        private static ConcurrentDictionary<ulong, ClientSession> _clientSessions = new ConcurrentDictionary<ulong, ClientSession>();

        bool events_initialized = false;
        public ClientSessionManager()
        {
            Instance = this;
            Logger.Info("Clean Space client manager initialized.");
        }
        internal void Init_Events()
        {
            if (events_initialized) return;
            events_initialized = true;
            EventHub.ClientConnected += EventHub_ClientConnected;
            

        }
        public bool RequestDispose(ulong steamId)
        {
            ClientSession attempt = GetClient(steamId);
            if (attempt != null)
            {        
                attempt.Dispose();
                return _clientSessions.TryRemove(steamId, out attempt);    
            }
            return false;
        }

        public ClientSession CloseClientSession(ulong steamId)
        {
            if (!_clientSessions.ContainsKey(steamId)) return null;
            _clientSessions[steamId].Dispose();
            ClientSession n;
            _clientSessions.TryRemove(steamId, out n);
            return n;
     
        }

        internal ClientSession GetClient(ulong steamId)
        {
            return _clientSessions.GetValueOrDefault(steamId, null);
        }

        public ClientSession CreateNewClientSession(ulong steamId, ConnectedClientDataMsg initialMsg)
        {
            if(!_clientSessions.TryAdd(steamId, new ClientSession(initialMsg)))
            {
                Logger.Error($"Failed to create a new session for {steamId}. Could not add to dictionary.");
                return null;
            }

            var newClient = GetClient(steamId);       
            return newClient;
        }

        private void EventHub_ClientConnected(object sender, CleanSpaceEventArgs e)
        {
            ulong target = (ulong)e.Args[0];
            ConnectedClientDataMsg msg = (ConnectedClientDataMsg)e.Args[1];
            if(target == 0 || msg.ClientId == null){
                Logger.Error("Client connected but msg had 0 for endpoint ID.");
                return;
            }
            CloseClientSession(target);
            var session = CreateNewClientSession(target, msg);
            session.InitiateCleanSpaceCheck();                    
        }

    }
}
