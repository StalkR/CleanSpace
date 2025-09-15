using CleanSpace;
using CleanSpaceShared.Networking;
using CleanSpaceShared.Scanner;
using ClientPlugin;
using Sandbox.Engine.Networking;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens;
using Sandbox.Graphics.GUI;
using Shared.Config;
using Shared.Events;
using Shared.Hasher;
using Shared.Logging;
using Shared.Plugin;
using Shared.Struct;
using Shared.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using VRage.FileSystem;
using VRage.Game;
using VRage.GameServices;
using VRage.Network;
using VRage.Plugins;
using VRageMath;

namespace CleanSpaceShared
{

    // ReSharper disable once UnusedType.Global
    public class CleanSpaceClientPlugin : IPlugin, ICommonPlugin
    {
        public const string PluginName = "Clean Space";
        public static CleanSpaceClientPlugin Instance { get; private set; }
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
        static CleanSpaceClientPlugin()
        {
            if (!IsInValidAppDomain())
            {               
                Common.Logger.Error($"{Common.PluginName}: Loaded outside valid app domain. Aborting...");
                throw new InvalidOperationException($"{Common.PluginName} invalid app domain.");
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool IsInValidAppDomain()
        {
            try
            {
                var found = AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => a.FullName?.IndexOf("Sandbox.Game", StringComparison.OrdinalIgnoreCase) >= 0
                           || a.FullName?.IndexOf("Sandbox.Engine", StringComparison.OrdinalIgnoreCase) >= 0);
                if (found) return true;

                var t = Type.GetType("Sandbox.Game.World.MySession, Sandbox.Game", throwOnError: false);
                if (t != null) return true;

                var procName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                if (procName.IndexOf("SpaceEngineers", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                var thisAsm = typeof(CleanSpaceClientPlugin).Assembly;
                var thisName = thisAsm.GetName().Name;

                var duplicates = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a =>
                    {
                        try { return string.Equals(a.GetName().Name, thisName, StringComparison.OrdinalIgnoreCase); }
                        catch { return false; }
                    })
                    .ToList();

                if (duplicates.Count > 1)
                {                   
                    return false;
                }
            }
            catch
            {
            }

            return false;
        }
        public static bool Ensure() => true; 

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Init(object gameInstance)
        {
            Ensure();
#if DEBUG
            // Allow the debugger some time to connect once the plugin assembly is loaded
            Thread.Sleep(100);
#endif

            MyGuiScreenMainMenuBase.OnOpened = menuOpenEvent;
            Instance = this;

            Log.Info("Loading");

            var configPath = Path.Combine(MyFileSystem.UserDataPath, ConfigFileName);
            config = PersistentConfig<PluginConfig>.Load(Log, configPath);

            var gameVersion = MyFinalBuildConstants.APP_VERSION_STRING.ToString();
            Common.SetPlugin(this, gameVersion, MyFileSystem.UserDataPath, "Clean Space", false, Logger);

            Config.TokenValidTimeSeconds = TimeSpan.TicksPerSecond * 2;
            Log.Debug($"{PluginName} Loaded");
            init_events();
            ClientSessionParameterProviders.RegisterProviders();
        }

        private void init_events()
        {
            Log.Info($"{PluginName}: Initializing events.");
            EventHub.CleanSpaceHelloReceived += EventHub_CleanSpaceHelloReceived;
            EventHub.ServerCleanSpaceRequested += EventHub_ServerCleanSpaceRequested;
            EventHub.ServerCleanSpaceFinalized += EventHub_ServerCleanSpaceFinalized;
            EventHub.CleanSpaceChatterReceived += EventHub_CleanSpaceChatterReceived;
            MyScreenManager.ScreenAdded += MyScreenManager_ScreenAdded; ;
        }

        private bool acceptingChatter = true;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void EventHub_CleanSpaceChatterReceived(object sender, CleanSpaceTargetedEventArgs e)
        {
            if (!acceptingChatter) {
                    Common.Logger.Debug("Received chatter, but not accepting it. Probably an echo.");
                return; 
            }

            object[] args = e.Args;
            try { MiscUtil.ArgsChecks<CleanSpaceChatterPacket>(e, 1); }
            catch (Exception ex) {
                Common.Logger.Error($"{Common.PluginName} ArgChecks ran into a problem handling chatter from the server." + ex.Message);
                return;
            }

            CleanSpaceChatterPacket r = (CleanSpaceChatterPacket)args[0];
           
            if (this.connectionChatterLenth > 1)
            {
                this.lastServerNonce = (string)this.currentServerNonce.Clone();
                this.currentServerNonce = r.NonceS;

                this.lastClientNonce = this.currentClientNonce;
                this.currentClientNonce = ValidationManager.RegisterNonceForPlayer(connectionSessionTarget, true);

                var message = new CleanSpaceChatterPacket
                {
                    SenderId = Sync.MyId,
                    TargetType = MessageTarget.Server,
                    Target = connectionSessionTarget,
                    UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    NonceS = this.currentServerNonce,
                    NonceC = this.currentClientNonce,
                    chatterParameters = SessionParameterFactory.AnswerChallenge(r.chatterParameters, r.NonceS, this.connectionSessionSalt)
                };

                PacketRegistry.Send(message, new EndpointId(connectionSessionTarget), this.currentClientNonce, this.currentServerNonce);
                Common.Logger.Info($"Replied to a chatter from server...");
                this.connectionChatterLenth -= 1;
            }
            else
            {
                acceptingChatter = false;
                PluginValidationRequest h = null;
                try
                {
                    h = ProtoUtil.Deserialize<PluginValidationRequest>(r.chatterPayload);

                    if (h != null)
                    {
                        EventHub.OnServerCleanSpaceRequested(this, r.SenderId, h);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Common.Logger.Error($"{Common.PluginName} Failed to aquire payload. [MSG:{ex.Message}] [STACK:{ex.StackTrace}]");
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
            this.acceptingChatter = true;
            this.acceptingHello = true;
        }

        private bool acceptingHello = true;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void EventHub_CleanSpaceHelloReceived(object sender, CleanSpaceTargetedEventArgs e)
        {
            object[] args = e.Args;
            try {   MiscUtil.ArgsChecks<CleanSpaceHelloPacket>(e, 1, destVerify: Sync.MyId);   }
            catch (Exception ex) {
                Common.Logger.Error($"{Common.PluginName} ArgChecks ran into a problem handling a hello message from the server." + ex.Message);
                return;
            }
            CleanSpaceHelloPacket r = (CleanSpaceHelloPacket)args[0];

            if (!acceptingHello) return;
            acceptingHello = false;

            SessionParameters parametersIn = ProtoUtil.Deserialize<SessionParameters>(r.sessionParameters);
            this.connectionSessionSalt = parametersIn.sessionSalt;
            this.connectionChatterLenth = parametersIn.chatterLength;
            this.connectionSessionTarget = r.SenderId;

            this.lastServerNonce = String.Empty;
            this.currentServerNonce = r.NonceS;

            this.lastClientNonce = String.Empty;
            this.currentClientNonce = ValidationManager.RegisterNonceForPlayer(r.SenderId, true);
            
            this.serverFacingAddress = r.client_ip_echo;

            Common.Logger.Debug("Received params: " + Convert.ToBase64String(parametersIn));
            byte[] parametersOut =
                SessionParameterFactory.AnswerChallenge(parametersIn, r.NonceS, this.connectionSessionSalt);

            Common.Logger.Debug("Generated params: " + Convert.ToBase64String(parametersOut));
            var message = new CleanSpaceHelloPacket
            {
                SenderId = Sync.MyId,
                TargetType = MessageTarget.Server,
                Target = connectionSessionTarget,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                NonceS = this.currentServerNonce,          
                NonceC = this.currentClientNonce,
                sessionParameters = parametersOut
            };

            // ordinarily, we would send a message whose token is encrypted with last server nonce as the extra key, but we dont have one yet. Thats fine, if it is null,
            // then the wrapper will use the per-message salt as the extra key. Both sides do not care about an expected nonce yet. Or shouldn't.

            PacketRegistry.Send(message, new EndpointId(connectionSessionTarget), this.currentClientNonce, this.currentServerNonce);
            Common.Logger.Info($"Replied to a hello from server {connectionSessionTarget}. Let's talk turn-key.");
        }

        bool hasPendingMessage = false;
        MyGuiScreenMessageBox pendingMessageBox;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]

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

                ResetState();
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void EventHub_ServerCleanSpaceRequested(object sender, CleanSpaceTargetedEventArgs e)
        {           
            object[] args = e.Args;
            try
            {
                MiscUtil.ArgsChecks<PluginValidationRequest>(e, 1);
            }
            catch (Exception ex)
            {
                Common.Logger.Error($"{Common.PluginName} ArgChecks failed handling a hello message from the server: {ex}");
                return;
            }

            PluginValidationRequest r = args[0] as PluginValidationRequest;

            if (r.attestationChallenge == null)
            {
                Common.Logger.Error($"{Common.PluginName} attestationChallenge was null.");
                return;
            }

            PacketRegistry.Logger.Debug($"{PacketRegistry.PluginName}: Unpacking attestation (Challenge length: {r.attestationChallenge?.Length}).");

            byte[] IV = r.attestationChallenge.Range(r.attestationChallenge.Length - 16, r.attestationChallenge.Length).ToArray();
            byte[] attestationBytes = r.attestationChallenge.Range(0, r.attestationChallenge.Length - 16).ToArray();

            this.lastServerNonce = currentServerNonce;
            this.currentServerNonce = r.NonceS;
            byte[] decryptedHasherBytes = EncryptionUtil.DecryptBytes(attestationBytes, currentServerNonce, this.connectionSessionSalt, IV);

            if (r.attestationSignature == null)
            {
                Common.Logger.Error($"{Common.PluginName} attestationSignature was null.");
                return;
            }

            if (!TokenUtility.SignPayloadBytes(decryptedHasherBytes, this.connectionSessionSalt).SequenceEqual(r.attestationSignature))
            {
                var m = $"{Common.PluginName} Security violation in clean space request hasher. Signature does not match.";
                Common.Logger.Error(m);
                throw new Exception(m);
            }

            HasherRunner.ValidateHasherRunnerBytes(decryptedHasherBytes);
            
            this.lastClientNonce = this.currentClientNonce;
            this.currentClientNonce = ValidationManager.RegisterNonceForPlayer(r.SenderId, true);

            if (r.NonceS == null)
                Common.Logger.Warning($"{Common.PluginName} r.NonceS was null.");

            var pluginHashes = AssemblyScanner.GetPluginAssemblies()
                ?.Select<Assembly, string>((a) =>{
                    try
                    {
                        return AssemblyScanner.GetSecureAssemblyFingerprint(a, Encoding.UTF8.GetBytes(r.NonceS));
                    }
                    catch (Exception ex)
                    {
                        Common.Logger.Error($"{Common.PluginName} Failed to fingerprint assembly {a.FullName}: {ex}");
                        return null;
                    }
                })
                .Where(h => h != null)
                .ToList();

            byte[] newSteamToken = new byte[1024];
            if(!MyGameService.GetAuthSessionTicket(out var _, newSteamToken, out var length))
            {
                Common.Logger.Error($"{Common.PluginName} Couldn't get a new steam auth ticket...");
            }

            var message = new PluginValidationResponse
            {
                SenderId = Sync.MyId,
                TargetType = MessageTarget.Server,
                Target = connectionSessionTarget,
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                NonceS = this.currentServerNonce,
                NonceC = currentClientNonce,
                PluginHashes = pluginHashes,
                attestationResponse = HasherRunner.ExecuteRunner(decryptedHasherBytes),
                newSteamToken = newSteamToken.Range(0, (int)length).ToArray(),
            };

            if (message.attestationResponse == null)
                Common.Logger.Warning($"{Common.PluginName} attestationResponse came back null.");

            PacketRegistry.Send(message, new EndpointId(connectionSessionTarget), currentClientNonce, currentServerNonce);
            PacketRegistry.Logger.Info($"{PacketRegistry.PluginName}: Sent response to validation request.");
        }


        private void menuOpenEvent()
        {                       
          
            MyGuiScreenMainMenuBase.OnOpened = null;

        }


        private void LogMessage(MessageBase obj)
        {
            byte[] array = new byte[1024];
           
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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
               () => new ProtoPacketData<PluginValidationResponse>()
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
                Tick++;
            }
            catch (Exception ex)
            {
                Log.Critical(ex, "Update failed");
                failed = true;
            }
        }

    }
}