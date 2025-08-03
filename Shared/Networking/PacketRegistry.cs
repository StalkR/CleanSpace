using ProtoBuf;

using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Networking;
using Shared.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using VRage;
using VRage.GameServices;
using VRage.Library.Collections;
using VRage.Network;
using VRage.Utils;


namespace CleanSpaceShared.Networking
{
    public static class PacketRegistry
    {

    private struct PacketInfo
    {
        public Func<IProtoPacketData> Factory;
        public Action<IProtoPacketData, EndpointId> Handler;
    }

        public static string PluginName;
        public static IPluginLogger Logger;

        private const byte CHANNEL = 225;

        private static readonly Dictionary<ushort, PacketInfo> packetInfos = new Dictionary<ushort, PacketInfo>();
        private static readonly Dictionary<Type, ushort> typeToId = new Dictionary<Type, ushort>();

        public static void Register<T>(ushort id, Func<IProtoPacketData> factory, Action<T, EndpointId> handler) where T : IProtoPacketData
        {
            if (packetInfos.ContainsKey(id))
                throw new Exception($"Packet ID {id} is already registered");

            packetInfos[id] = new PacketInfo
            {
                Factory = factory,
                Handler = (packet, sender) =>
                {
                    if (packet is T typed)
                        handler(typed, sender);
                    else
                        throw new InvalidCastException($"Packet ID {id} could not be cast to {typeof(T).Name}");
                }
            };

            // Store reverse mapping for MessageBase-derived payloads
            var innerType = typeof(T).IsGenericType ? typeof(T).GenericTypeArguments.First() : null;
            if (innerType != null && typeof(MessageBase).IsAssignableFrom(innerType))
                typeToId[innerType] = id;
        }

        public static void Send<T>(T message, EndpointId recipient, MyP2PMessageEnum reliability = MyP2PMessageEnum.Reliable)
           where T : MessageBase
        {
            ushort id = GetPacketId<T>();           
            var envelope = MessageFactory.Wrap(message,message.should_compress);
            envelope.PacketId = id;

            var packet = new ProtoPacketData<T>(envelope);
            var descriptor = new MyNetworkWriter.MyPacketDescriptor
            {
                Channel = CHANNEL,
                Data = packet,
                MsgType = reliability
            };

            descriptor.Recipients.Add(recipient);
            MyNetworkWriter.SendPacket(descriptor);
        }

        public static ushort GetPacketId<T>() where T : MessageBase
        {
            if (typeToId.TryGetValue(typeof(T), out var id))
                return id;

            throw new Exception($"{PluginName}: Packet type {typeof(T).Name} not registered");
        }

        public static void Init(IPluginLogger log, string pluginName)
        {
            Logger = log;
            PluginName = pluginName;

            var readerType = ResolveMyNetworkReaderType();
            if (readerType == null)
                throw new Exception("Could not find MyNetworkReader type");

            var setHandlerMethod = readerType.GetMethod("SetHandler", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (setHandlerMethod == null)
            {
                Logger.Error($"{PluginName}: Failed to find SetHandler method");
                return;
            }

            var delegateType = readerType.Assembly.GetType("Sandbox.Engine.Networking.NetworkMessageDelegate");
            if (delegateType == null)
            {
                Logger.Error($"{PluginName}: Failed to find delegate type");
                return;
            }

            var handlerMethod = typeof(PacketRegistry).GetMethod(nameof(OnPacketReceived), BindingFlags.Static | BindingFlags.NonPublic);
            if (handlerMethod == null)
            {
                Logger.Error($"{PluginName}: Failed to find OnPacketReceived");
                return;
            }

            var internalDelegate = Delegate.CreateDelegate(delegateType, handlerMethod);
            setHandlerMethod.Invoke(null, new object[] { CHANNEL, internalDelegate, null });

            Logger.Info($"{PluginName}: Custom packet handler registered on channel {CHANNEL}");
        }

        private static void OnPacketReceived(MyPacket packet)
        {
            try
            {
                Logger.Debug($"{PluginName}: OnPacketReceived started for packet from {packet.Sender.Id}");
                var stream = packet.ByteStream;
                var sender = packet.Sender.Id;

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

                NetworkEnvelope envelope;
                int envelopeLength;
                using (var ms = new MemoryStream(buffer))
                {
                    envelope = Serializer.Deserialize<NetworkEnvelope>(ms);
                    envelopeLength = (int)ms.Position;
                    Logger.Debug($"{PluginName}: Deserialized NetworkEnvelope with PacketId={envelope.PacketId}, IsCompressed={envelope.IsCompressed}, PayloadLength={envelope.Payload?.Length ?? 0}, EnvelopeBytes={envelopeLength}");
                }

                ushort id = envelope.PacketId;

                if (!packetInfos.TryGetValue(id, out var info))
                {
                    Logger.Warning($"{PluginName}: Unknown packet ID {id} from {sender}");
                    return;
                }

                Logger.Debug($"{PluginName}: Found PacketInfo for ID {envelope.PacketId}");
                var rawPacket = info.Factory();
                if (!(rawPacket is IProtoPacketData proto))
                {
                    Logger.Warning($"{PluginName}: Packet ID {id} is not a ProtoPacket");
                    return;
                }

                Logger.Debug($"{PluginName}: Created ProtoPacketData instance for packet ID {envelope.PacketId}");
                // Read proto body using ByteStream
                using (var protoStream = new VRage.ByteStream(buffer, envelopeLength))
                {
                    proto.Read(protoStream);
                    Logger.Debug($"{PluginName}: Successfully read proto data from inner buffer");
                }

                // Dispatch to handler
                info.Handler(proto, sender);
                Logger.Debug($"{PluginName}: Dispatched packet ID {envelope.PacketId} to handler for sender {sender}");
            }
            catch (Exception ex)
            {
                Logger.Error($"{PluginName}: Exception in OnPacketReceived: {ex}");
            }
        }

        private static Type ResolveMyNetworkReaderType()
        {
            const string TargetType = "Sandbox.Engine.Networking.MyNetworkReader";

            // Fast path
            var networkingAssembly = typeof(MyMultiplayer).Assembly;
            var type = networkingAssembly.GetType(TargetType);
            if (type != null) return type;

            // Slow path
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(TargetType);
                if (type != null)
                {
                    MyLog.Default.WriteLine($"Found MyNetworkReader in: {asm.FullName}");
                    return type;
                }
            }

            Logger?.Error($"{PluginName}: Could not resolve MyNetworkReader type.");
            return null;
        }

    }
}