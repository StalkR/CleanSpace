using CleanSpace;
using CleanSpaceShared.Networking;
using CleanSpaceShared.Scanner;
using CleanSpaceShared.Settings;
using CleanSpaceShared.Settings.Layouts;
using EmptyKeys.UserInterface;
using NLog.Internal.Fakeables;
using ProtoBuf;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.GameSystems.Conveyors;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Shared.Config;
using Shared.Events;
using Shared.Hasher;
using Shared.Logging;
using Shared.Patches;
using Shared.Plugin;
using Shared.Util;
using SpaceEngineers.Game.GUI;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Input;
using VRage.FileSystem;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.GameServices;
using VRage.Network;
using VRage.Plugins;
using VRage.Steam;
using VRageMath;

namespace CleanSpaceShared
{

    // ReSharper disable once UnusedType.Global
    public class CleanSpaceClientPlugin : IPlugin, ICommonPlugin
    {
        public const string PluginName = "Clean Space";
        public static CleanSpaceClientPlugin Instance { get; private set; }
        private SettingsGenerator settingsGenerator;
        public long Tick { get; private set; }
        private static bool failed;

        public IPluginLogger Log => Logger;
        public static readonly IPluginLogger Logger = new PluginLogger(PluginName);

        public IPluginConfig Config => config?.Data;
        private PersistentConfig<PluginConfig> config;
        private static readonly string ConfigFileName = $"{PluginName}.cfg";


        private uint serverFacingAddress; 
        public bool first_initialization = false;
        private byte[] connectionSessionSalt;
        private byte connectionChatterLenth;
        private ulong connectionSessionTarget;
        private string lastServerNonce;
        private string currentServerNonce;
        private string lastClientNonce;
        private string currentClientNonce;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Init(object gameInstance)
        {



#if DEBUG
            // Allow the debugger some time to connect once the plugin assembly is loaded
            Thread.Sleep(100);
#endif


            MyGuiScreenMainMenuBase.OnOpened = menuOpenEvent;
            Instance = this;
            Instance.settingsGenerator = new SettingsGenerator();

            Log.Info("Loading");

            var configPath = Path.Combine(MyFileSystem.UserDataPath, ConfigFileName);
            config = PersistentConfig<PluginConfig>.Load(Log, configPath);

            var gameVersion = MyFinalBuildConstants.APP_VERSION_STRING.ToString();

            Common.SetPlugin(this, gameVersion, MyFileSystem.UserDataPath, "Clean Space", false, Logger);

          /*  if (!PatchHelpers.HarmonyPatchAll(Log, new Harmony(PluginName)))
            {
                failed = true;
                return;
            }*/
            Config.TokenValidTimeSeconds = TimeSpan.TicksPerSecond * 2;
            Log.Debug($"{PluginName} Loaded");
            init_events();
            SessionParameterFactory.RegisterProviders();
        }

        private void init_events()
        {
            Log.Info($"{PluginName}: Initializing events.");
            EventHub.CleanSpaceHelloReceived += EventHub_CleanSpaceHelloReceived;
            EventHub.ServerCleanSpaceRequested += EventHub_ServerCleanSpaceRequested;
            EventHub.ServerCleanSpaceFinalized += EventHub_ServerCleanSpaceFinalized;
            EventHub.CleanSpaceChatterReceived += EventHub_CleanSpaceChatterReceived;
            MyScreenManager.ScreenAdded += MyScreenManager_ScreenAdded; ;
            MySession.OnUnloaded += MySession_OnUnloaded;
        }

        private bool acceptingChatter = true;
        private void EventHub_CleanSpaceChatterReceived(object sender, CleanSpaceTargetedEventArgs e)
        {
            if (!acceptingChatter) return;
            object[] args = e.Args;
            try { MiscUtil.ArgsChecks<CleanSpaceChatterPacket>(e, 1); }
            catch (Exception ex)
            {
                Common.Logger.Error($"{Common.PluginName} ArgChecks ran into a problem handling chatter from the server." + ex.Message);
                return;
            }
            CleanSpaceChatterPacket r = (CleanSpaceChatterPacket)args[0];
           
            if (this.connectionChatterLenth > 0)
            {
                this.lastServerNonce = this.currentServerNonce;
                this.currentServerNonce = r.Nonce;

                byte[] challengeResponse = SessionParameterFactory.AnswerChallenge(r.sessionParameters, this.serverFacingAddress, Sync.MyId, this.connectionSessionTarget, AppDomain.CurrentDomain.GetAssemblies().Length);

                this.lastClientNonce = this.currentClientNonce;
                this.currentClientNonce = ValidationManager.RegisterNonceForPlayer(connectionSessionTarget, true);
                var message = new CleanSpaceChatterPacket
                {
                    SenderId = Sync.MyId,
                    TargetType = MessageTarget.Server,
                    Target = connectionSessionTarget,
                    UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Nonce = this.currentServerNonce,
                    sessionParameters = challengeResponse
                };

                PacketRegistry.Send(message, new EndpointId(connectionSessionTarget), this.currentClientNonce, this.lastServerNonce);
                Common.Logger.Info($"Replied to a chatter from server...");
                this.connectionChatterLenth -= 1;
            }
            else
            {
                acceptingChatter = false;
                PluginValidationRequest h = null;
                try
                {
                    using (var ms = new MemoryStream(r.chatterPayload))
                    {
                        Serializer.Deserialize<PluginValidationRequest>(ms);
                    }
                    EventHub.OnServerCleanSpaceRequested(this, r.SenderId, h);
                    return;
                }
                catch (Exception ex)
                {
                    Common.Logger.Error($"{Common.PluginName} Failed to aquire payload." + ex.Message);
                    return;
                }

                Common.Logger.Error($"{Common.PluginName} Failed to aquire payload.");
                return;
            }
        }

        private void ResetState()
        {
            this.connectionSessionSalt = null;
            this.connectionChatterLenth = 0;
            this.connectionSessionTarget = 0;
            this.lastServerNonce = null;
        }

        // Stage 1: Pending, Phase 1: Hello
        private void EventHub_CleanSpaceHelloReceived(object sender, CleanSpaceTargetedEventArgs e)
        {
            object[] args = e.Args;
            try {   MiscUtil.ArgsChecks<CleanSpaceHelloPacket>(e, 1);   }
            catch (Exception ex) {
                Common.Logger.Error($"{Common.PluginName} ArgChecks ran into a problem handling a hello message from the server." + ex.Message);
                return;
            }
            CleanSpaceHelloPacket r = (CleanSpaceHelloPacket)args[0];

            SessionParameters parametersIn = r.sessionParameters;
            this.connectionSessionSalt = parametersIn.sessionSalt;
            this.connectionChatterLenth = parametersIn.chatterLength;
            this.connectionSessionTarget = r.SenderId;
            this.lastServerNonce = null;
            this.currentServerNonce = r.Nonce;
            this.currentClientNonce = ValidationManager.RegisterNonceForPlayer(r.SenderId, true);
            this.serverFacingAddress = r.client_ip_echo;

            SessionParameters parametersOut =
                SessionParameterFactory.AnswerChallenge(parametersIn,
                                                        this.serverFacingAddress,
                                                        Sync.MyId,
                                                        this.connectionSessionTarget,
                                                        AppDomain.CurrentDomain.GetAssemblies().Length);

        
            var message = new CleanSpaceHelloPacket
            {
                SenderId = Sync.MyId,
                TargetType = MessageTarget.Server,
                Target = connectionSessionTarget,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Nonce = this.currentServerNonce,          
                sessionParameters = parametersOut
            };

            // ordinarily, we would send a message whose token is encrypted with last server nonce as the extra key, but we dont have one yet. Thats fine, if it is null,
            // then the wrapper will use the per-message salt as the extra key. Both sides do not care about an expected nonce yet. Or shouldn't.

            PacketRegistry.Send(message, new EndpointId(connectionSessionTarget), this.currentClientNonce, this.lastServerNonce);
            Common.Logger.Info($"Replied to a hello from server {connectionSessionTarget}. Let's talk turn-key.");
        }

        bool hasPendingMessage = false;
        MyGuiScreenMessageBox pendingMessageBox;
        private void EventHub_ServerCleanSpaceFinalized(object sender, CleanSpaceTargetedEventArgs e)
        {
            object[] args = e.Args;
            PluginValidationResult r = (PluginValidationResult)args[0];

            Log.Info($"{PluginName}: Received result code from server: " + r.Code.ToString());
            if (!r.Success)
            {
                var MsgBody = new StringBuilder("Could not join the server.\n\n");
                var Offenders = r.PluginList;

                var offenderPluginInfo = AssemblyScanner.GetRawPluginAssembliesData().Where((element)=>Offenders.Contains(element.Hash, StringComparer.OrdinalIgnoreCase));

                if(r.Code == Shared.Struct.ValidationResultCode.REJECTED_CLEANSPACE_HASH)
                {
                    MsgBody.AppendLine("Your version of Clean Space is not allowed by the server. Update your version of Clean Space or contact the server owner to identify the version being used if not latest.");
                }
                else if(r.Code == Shared.Struct.ValidationResultCode.REJECTED_MATCH)
                {
                    MsgBody.AppendLine("Some of your currently loaded plugins were disallowed by the server. Disable them before attempting to join. \n Plugins: \n");
                    offenderPluginInfo.ForEach((element) => MsgBody.AppendLine($"Plugin Name: {element.Name}"));
                    MsgBody.AppendLine("\n\nIf you feel that this is an error, contact a Clean Space developer with your logs.");
                }
                else if (r.Code == Shared.Struct.ValidationResultCode.EXPIRED_TOKEN)
                {
                    MsgBody.AppendLine("Clean Space rejected your connection due to an expired token. Perhaps network issues?");
                }
                else
                {
                    MsgBody.AppendLine("Clean Space rejected connection for unspecified reason. Contact a Clean Space dev with logs.");
                }
                pendingMessageBox = MyGuiSandbox.CreateMessageBox(
                      MyMessageBoxStyleEnum.Info,
                      canBeHidden: false,
                      canHideOthers: true,
                      buttonType: MyMessageBoxButtonsType.OK,
                      messageText: MsgBody,
                      messageCaption: new StringBuilder("Clean Space"),
                      size: new Vector2(0.6f, 0.5f)
                        );
                hasPendingMessage = true;
            }
        }

        private void MyScreenManager_ScreenAdded(MyGuiScreenBase obj)
        {
            if (obj is MyGuiScreenMainMenuBase)
            {
                Log.Info($"{PluginName}: Menu screen reached. Re-registering handlers.");
                PacketRegistry.InstallHandler();
                if (!first_initialization)
                {
                    first_initialization = true;                   
                    RegisterPackets();
                }

                if (hasPendingMessage)
                {
                    MyGuiSandbox.AddScreen(pendingMessageBox);
                    hasPendingMessage = false;
                }
            }
        }

        private void MySession_OnUnloaded()
        {
          
        }

        private void MySession_OnLoading()
        {           

           
        }

        /*
         * 
         *  attestationChallenge = EncryptionUtil.EncryptBytesWithIV(this.clientHasherBytes, newNonce, this.clientSalt, messageIV).Concat(messageIV).ToArray(),
                    attestationSignature = this.hasherSignature,
                    Nonce = newNonce,
                    Target = steamId,
                    TargetType = MessageTarget.Client,
                    UnixTimestamp = DateTime.Now.ToUnixTimestamp()
         * */
        private void EventHub_ServerCleanSpaceRequested(object sender, CleanSpaceTargetedEventArgs e)
        {
            object[] args = e.Args;
            try { MiscUtil.ArgsChecks<PluginValidationRequest>(e, 1); }
            catch (Exception ex)
            {
                Common.Logger.Error($"{Common.PluginName} ArgChecks ran into a problem handling a hello message from the server." + ex.Message);
                return;
            }
            PluginValidationRequest r = (PluginValidationRequest)args[0];


            byte[] IV = r.attestationChallenge.Range(r.attestationChallenge.Length - 12, r.attestationChallenge.Length).ToArray();
            byte[] attestationBytes = new byte[r.attestationChallenge.Length - 12];

            byte[] decryptedHasherBytes = EncryptionUtil.DecryptBytes(attestationBytes, this.currentServerNonce, this.connectionSessionSalt, IV);
            if (!TokenUtility.SignPayloadBytes(r.attestationChallenge, this.connectionSessionSalt).SequenceEqual(r.attestationSignature))
            {
                var m = $"{Common.PluginName} Security violation in clean space request hasher. Signature does not match.";
                Common.Logger.Error(m);
                // This one is a bit more serious so let's throw and hope it crashes the client - they are being targeted.
                throw new Exception(m);
                return;
            }

            this.lastServerNonce = this.currentServerNonce;
            this.currentServerNonce = r.Nonce;
            this.lastClientNonce = this.currentClientNonce;
            string newToken = ValidationManager.RegisterNonceForPlayer(r.SenderId, true);

            var message = new PluginValidationResponse
            {
                SenderId = Sync.MyId,
                TargetType = MessageTarget.Server,
                Target = r.SenderId,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Nonce = this.currentServerNonce,
                PluginHashes = AssemblyScanner.GetPluginAssemblies().Select<Assembly, string>((a) => AssemblyScanner.GetSecureAssemblyFingerprint(a, Encoding.UTF8.GetBytes(r.Nonce))).ToList(),
                attestationResponse = HasherRunner.ExecuteRunner(r.attestationChallenge)
            };
       
            PacketRegistry.Send(message, new EndpointId(r.SenderId), newToken, this.lastServerNonce);
            PacketRegistry.Logger.Info($"{PacketRegistry.PluginName}: Sending a response to validation request.");
        }

        private void menuOpenEvent()
        {                       
          
            MyGuiScreenMainMenuBase.OnOpened = null;

        }


        private void LogMessage(MessageBase obj)
        {

        }
     
        private void RegisterPackets()
        {

            PacketRegistry.Register<CleanSpaceHelloPacket>(
               107,
               () => new ProtoPacketData<CleanSpaceHelloPacket>()
           );

            PacketRegistry.Register<CleanSpaceChatterPacket>(
             108,
             () => new ProtoPacketData<CleanSpaceChatterPacket>(), SecretPacketFactory<CleanSpaceChatterPacket>.handler<ProtoPacketData<CleanSpaceChatterPacket>>
           );

            PacketRegistry.Register<PluginValidationRequest>(
                110,                 
                () => new ProtoPacketData<PluginValidationRequest>()
            );

            PacketRegistry.Register<PluginValidationResponse>(
               111,
               () => new ProtoPacketData<PluginValidationResponse>(), SecretPacketFactory<PluginValidationResponse>.handler<ProtoPacketData<PluginValidationResponse>>
            );

            PacketRegistry.Register<PluginValidationResult>(
              112,
              () => new ProtoPacketData<PluginValidationResult>()
            );
        }

        public void Dispose()
        {
            try
            {
               
                // TODO: Save state and close resources here, called when the game exists (not guaranteed!)
                // IMPORTANT: Do NOT call harmony.UnpatchAll() here! I t may break other plugins.
            }
            catch (Exception ex)
            {
                Log.Critical(ex, "Dispose failed");
            }

            Instance = null;
        }

        public void Update()
        {
            if (failed)
                return;

            try
            {
                CustomUpdate();
                Tick++;
            }
            catch (Exception ex)
            {
                Log.Critical(ex, "Update failed");
                failed = true;
            }
        }

        private void CustomUpdate()
        {
            // TODO: Put your update code here. It is called on every simulation frame!
            PatchHelpers.PatchUpdates();
        }

        // ReSharper disable once UnusedMember.Global
        public void OpenConfigDialog()
        {
            Instance.settingsGenerator.SetLayout<Simple>();
            MyGuiSandbox.AddScreen(Instance.settingsGenerator.Dialog);
        }
    }
}