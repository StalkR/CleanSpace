using CleanSpace;
using ProtoBuf;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Sandbox.Game.Multiplayer;
using CleanSpaceShared.Logging;
using CleanSpaceShared.Plugin;
using CleanSpaceShared.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using VRage;
using VRage.Collections;
using VRage.GameServices;
using VRage.Network;
using VRage.Utils;
using static Sandbox.Engine.Networking.MyNetworkWriter;


namespace CleanSpaceShared.Networking
{

    public static class SecretPacketFactory<T> where T : MessageBase
    {
        public static void handler<U>(IProtoPacketData packet, EndpointId sender) where U : ProtoPacketData<T>
        {
            T message;
            try
            {
                if (ValidationManager.NonceExistsForPlayer(sender.Value))
                {
                    var n = ValidationManager.GetNonceForPlayer(sender.Value);
                    Common.Logger.Debug($"Exist LEN: {n.Length}");
                    message = ((U)packet).GetMessage(n);

                    if (Common.IsServer)
                        message.ProcessServer(message);
                    else
                        message.ProcessClient(message);                   
                }
                else
                {
                    Common.Logger.Warning($"Received a packet from {sender.Value} with an unexpected nonce. Ignoring.");
                }
            }
            catch (Exception ex)
            {
                Common.Logger.Warning($"E19: Handler failed to unwrap a message from {sender.Value} with key. {ex.Message}");
                return;
            }

           
            ((U)packet).Return();
        }
    }

    public static class DefaultPacketFactory<T> where T: MessageBase
    {
        public static void handler<U>(IProtoPacketData packet, EndpointId sender) where U: ProtoPacketData<T>
        {
            T message;
            try
            {
                message = ((U)packet).GetMessage();
            }
            catch(Exception ex)
            {
                Common.Logger.Warning($"E18: Handler failed to unwrap a message from {sender.Value}. {ex.Message}");
                return;
            }
            
            if (Common.IsServer)
                message.ProcessServer(message);
            else
                message.ProcessClient(message);
            
            ((U)packet).Return();
        }
    }


    public static class PacketRegistry
    {
        private static bool IsServer => CleanSpaceShared.Plugin.Common.IsServer;
        public struct PacketInfo
        {            
            public Func<IProtoPacketData> Factory;
            public Action<IProtoPacketData, EndpointId> Handler;
        }

        public static string PluginName => Common.PluginName;
        public static IPluginLogger Logger => Common.Logger;

        private const byte CHANNEL = 225;

        private static readonly Dictionary<ushort, PacketInfo> packetInfos = new Dictionary<ushort, PacketInfo>();
        private static readonly Dictionary<Type, ushort> typeToId = new Dictionary<Type, ushort>();

        public static void Register<T>(ushort id, Func<IProtoPacketData> factory, Action<IProtoPacketData, EndpointId> handler = null) where T : MessageBase 
        {
            if (packetInfos.ContainsKey(id))
                throw new Exception($"Packet ID {id} is already registered");

            packetInfos[id] = new PacketInfo
            {
                Factory = factory,
                Handler = handler != null ? handler : DefaultPacketFactory<T>.handler<ProtoPacketData<T>>
            };

            Logger.Info($"Registered packet ID {id} with type {typeof(T).Name}");
            typeToId[typeof(T)] = id;            
        }

        public static void Send<T>(T message, EndpointId recipient, string token, string extraKey = null, MyP2PMessageEnum reliability = MyP2PMessageEnum.Reliable)
           where T : MessageBase
        {
            ushort id = GetPacketId<T>();
            message.UnixTimestamp = DateTime.UtcNow.ToUnixTimestamp();
            message.SenderId = Sync.MyId;
            message.Target = recipient.Value;
            var envelope = MessageFactory.Wrap(message, token, extraKey, message.should_compress, true);
            envelope.PacketId = id;
            var packet = new ProtoPacketData<T>(envelope);
            var np = InitSendStream(CHANNEL, recipient, reliability, IsServer ? MyMessageId.SERVER_DATA : MyMessageId.PLAYER_DATA);
            np.Data = packet;
            MyNetworkWriter.SendPacket(np);            
            Logger.Debug($"{PluginName}: Sent message id {envelope.PacketId} with token length {envelope.Key.Length}, payload length {envelope.Payload.Length} and total length {np.Data.Size} to destination {recipient}.");
        }

        internal static ushort GetPacketId<T>() where T : MessageBase
        {
            if (typeToId.TryGetValue(typeof(T), out var id))
                return id;

            throw new Exception($"{PluginName}: Packet type {typeof(T).Name} not registered");
        }

        private static Type _NetworkReaderType = null;
        public static Type NetworkReaderType => _NetworkReaderType ?? (_NetworkReaderType = ResolveMyNetworkReaderType());

        private static Type _NetworkMessageDelegateType = null;

        public static Type NetworkMessageDelegateType = _NetworkMessageDelegateType ?? (_NetworkMessageDelegateType = NetworkReaderType.Assembly.GetType("Sandbox.Engine.Networking.NetworkMessageDelegate"));
        public static void InstallHandler()
        {  

            if (NetworkReaderType == null)
                throw new Exception("Could not find MyNetworkReader type");
           
            var handlerMethod = typeof(PacketRegistry).GetMethod(nameof(OnPacketReceived), BindingFlags.Static | BindingFlags.NonPublic);
            if (handlerMethod == null)
            {
                Logger.Error($"{PluginName}: Failed to find OnPacketReceived");
                return;
            }
            RegisterHandlerOn(CHANNEL, handlerMethod);
        }


        private static MyPacketDescriptor PullDescriptorFromGamePool()
        {
            Type t = typeof(MyNetworkWriter);        
            FieldInfo f = t.GetField("m_descriptorPool", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null)
                Logger.Error("Could not find field 'm_descriptorPool' on MyNetworkWriter.");

            var pool = (MyConcurrentPool<MyPacketDescriptor>)f.GetValue(null);
            if (pool == null)
                Logger.Error("The field 'm_descriptorPool' was null.");

            return pool.Get();
        }

        private static MyPacketDescriptor GetPacketDescriptor(EndpointId userId, MyP2PMessageEnum msgType, int channel)
        {
            MyPacketDescriptor myPacketDescriptor = PullDescriptorFromGamePool();
            myPacketDescriptor.MsgType = msgType;
            myPacketDescriptor.Channel = channel;
            if (userId.IsValid)
            {
                myPacketDescriptor.Recipients.Add(userId);
            }
            return myPacketDescriptor;
        }

        private static MyNetworkWriter.MyPacketDescriptor InitSendStream(int m_channel, EndpointId endpoint, MyP2PMessageEnum msgType, MyMessageId msgId, byte index = 0)
        {
            MyNetworkWriter.MyPacketDescriptor packetDescriptor = GetPacketDescriptor(endpoint, msgType, m_channel);
           // packetDescriptor.Header.WriteByte((byte)msgId);
            //packetDescriptor.Header.WriteByte(index);
            return packetDescriptor;
        }


        public static void RegisterHandlerOn(int channel, MethodInfo handler, Action<ulong> disconnectPeerOnError = null)
        {
            if (NetworkReaderType == null)
                throw new Exception("Could not find MyNetworkReader type");

            var setHandlerMethod = NetworkReaderType.GetMethod("SetHandler", BindingFlags.Static | BindingFlags.Public);
            if (setHandlerMethod == null)
            {
                Logger.Error($"{PluginName}: Failed to find SetHandler method");
                return;
            }

            if (NetworkMessageDelegateType == null)
            {
                Logger.Error($"{PluginName}: Failed to find delegate type");
                return;
            }

            var internalDelegate = Delegate.CreateDelegate(NetworkMessageDelegateType, handler);
            setHandlerMethod.Invoke(null, new object[] { channel, internalDelegate, disconnectPeerOnError });

            Logger.Info($"{PluginName}: Packet handler registered on channel {channel}");
        }

        public static void ClearHandlerOn(int channel)
        {
            if (NetworkReaderType == null)
                throw new Exception("Could not find MyNetworkReader type");

            var setHandlerMethod = NetworkReaderType.GetMethod("ClearHandler", BindingFlags.Static | BindingFlags.Public);
            setHandlerMethod.Invoke(null, new object[] { channel });

            Logger.Info($"{PluginName}: Packet handler cleared for channel {CHANNEL}");
        }


        private static void OnPacketReceived(MyPacket packet)
        {
            try
            {
             
                Logger.Debug($"{PluginName}: OnPacketReceived started for packet from {packet.Sender.Id.Value} received.");
                var stream = packet.ByteStream;
                var sender = packet.Sender.Id;
                var receivedTime = packet.ReceivedTime;

                //stream.Position = 10;
                int remaining = (int)(stream.Length - stream.Position);

                Logger.Debug($"{PluginName}: Stream length={stream.Length}, position={stream.Position}, remaining={remaining}");
                var buffer = new byte[remaining];
                int read = 0;
                while (read < remaining)
                {
                    int bytesRead = stream.Read(buffer, read, remaining - read);
                    Logger.Debug($"{PluginName}: Read {bytesRead} bytes from stream at offset {read}");
                    if (bytesRead == 0)
                        throw new EndOfStreamException("Stream ended prematurely");
                    read += bytesRead;
                }

                Envelope envelope;
                int envelopeLength;
                using (var ms = new MemoryStream(buffer))
                {
                    envelope = Serializer.Deserialize<Envelope>(ms);
                    envelopeLength = (int)ms.Position;
                    Logger.Debug($"{PluginName}: Deserialized NetworkEnvelope with PacketId={envelope.PacketId}, IsEncrypted={envelope.IsEncrypted}, IsCompressed={envelope.IsCompressed}, PayloadLength={envelope.Payload?.Length ?? 0}, KeyLength={envelope.Key?.Length ?? 0}, EnvelopeBytes={envelopeLength}");
                }

                ushort id = envelope.PacketId;
                if (!packetInfos.TryGetValue(id, out var info))
                {
                    Logger.Warning($"{PluginName}: Unknown packet ID {id} from {sender}");
                    return;
                }

                var rawPacket = info.Factory();
                if (!(rawPacket is IProtoPacketData proto))
                {
                    Logger.Warning($"{PluginName}: Packet ID {id} is not a ProtoPacket");
                    return;
                }
                
                Logger.Debug($"{PluginName}: Created ProtoPacketData instance for packet ID {id}");
                using (var protoStream = new VRage.ByteStream(buffer, envelopeLength))
                {
                    proto.Read(protoStream);
                    Logger.Debug($"{PluginName}: Successfully read proto data with length {protoStream.Length} from inner buffer");
                }

                info.Handler(proto, sender);
                Logger.Debug($"{PluginName}: Dispatched packet ID {id} to handler for sender {sender}");
                
                
            }
            catch (Exception ex)
            {
                Logger.Error($"{PluginName}: Exception in OnPacketReceived: {ex}");
            }
        }

        private static Type ResolveMyNetworkReaderType()
        {
            const string TargetType = "Sandbox.Engine.Networking.MyNetworkReader";

            // Fast path, the one we all know and love.
            var networkingAssembly = typeof(MyMultiplayer).Assembly;
            var type = networkingAssembly.GetType(TargetType);
            if (type != null) return type;

            // Slow path, in case keen changes something.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(TargetType);
                if (type != null)
                {
                    Logger.Warning($"Found MyNetworkReader in: {asm.FullName}. Hey. I had to do this the slow way. Let CS Dev know something has changed.");
                    return type;
                }
            }

            Logger?.Error($"{PluginName}: Could not resolve MyNetworkReader type.");
            return null;
        }

    }
}