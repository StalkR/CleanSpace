using NLog;
using ProtoBuf;
using System;
using System.IO;
using System.Runtime.InteropServices;
using VRage;

namespace CleanSpaceShared.Networking
{
    public class ProtoPacketData<T> : IProtoPacketData where T : MessageBase
    {
        private byte[] _buffer;
        private GCHandle _handle;

        public ProtoPacketData() { }

        public ProtoPacketData(NetworkEnvelope envelope)
        {
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, envelope);
                _buffer = ms.ToArray();
            }

            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        }

        public byte[] Data => _buffer;
        public IntPtr Ptr => _handle.IsAllocated ? _handle.AddrOfPinnedObject() : IntPtr.Zero;
        public int Size => _buffer?.Length ?? 0;
        public int Offset => 0;

        public object MessageObject => GetMessage();

        public void Return()
        {
            if (_handle.IsAllocated)
                _handle.Free();
            _buffer = null;
        }
        public void Write(ByteStream stream)
        {
            byte[] bytes = _buffer ?? Array.Empty<byte>();
            stream.WriteUShort((ushort)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }
        public void Read(ByteStream stream)
        {

            int remaining = (int)(stream.Length - stream.Position);

            PacketRegistry.Logger.Debug($"{PacketRegistry.PluginName}: Stream length={stream.Length}, position={stream.Position}, remaining={remaining}");
            var buffer = new byte[remaining];
            int read = 0;
            while (read < remaining)
            {
                int bytesRead = stream.Read(buffer, read, remaining - read);
                PacketRegistry.Logger.Debug($"{PacketRegistry.PluginName}: Read {bytesRead} bytes from stream at offset {read}");
                if (bytesRead == 0)
                    throw new EndOfStreamException("Stream ended prematurely");
                read += bytesRead;
            }

            _buffer = buffer;
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        }

        public T GetMessage()
        {
            if (_buffer == null)
                throw new InvalidOperationException("No data to deserialize");

            NetworkEnvelope n = null;
            MemoryStream ms;
            using (ms = new MemoryStream(_buffer))
            {
                n = Serializer.Deserialize<NetworkEnvelope>(ms);
            }           
            return MessageFactory.Unwrap<T>(n);
        }
    }
}