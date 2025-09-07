using CleanSpaceShared.Util;
using ProtoBuf;
using System;
using System.IO;
using System.Text;

namespace CleanSpaceShared.Networking
{
    public static class MessageFactory
    {
        
        public static Envelope Wrap<T>(T message, string token, string extraKey, bool compress = false, bool encrypt = true) where T : MessageBase
        {            
            ushort packetId = PacketRegistry.GetPacketId<T>();
            byte[] payload;
            byte[] salt = EncryptionUtil.GenerateSalt(16);
            using (var ms = new MemoryStream())
            {                
                Serializer.Serialize(ms, message);
                payload = ms.ToArray();
            }

            if (compress)
                payload = CompressionUtil.Compress(payload);

            byte[] IV_in = null;
        
            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            byte[] key = EncryptionUtil.EncryptBytes(tokenBytes, extraKey ?? Convert.ToBase64String(salt), salt, out IV_in);

            if (encrypt)
                payload = EncryptionUtil.EncryptBytesWithIV(payload, token, salt, IV_in);

            return new Envelope
            {
                PacketId = packetId,
                IsCompressed = compress,
                IsEncrypted = encrypt,
                Payload = payload,
                Key = key,
                Salt = salt,
                IV = IV_in
            };
        }

        public static T Unwrap<T>(Envelope envelope, string extraKey) where T : MessageBase
        {          
            byte[] data = (byte[])envelope.Payload.Clone();
            byte[] key = (byte[])envelope.Key.Clone();

            string skey = null;
            try
            {
               skey = Encoding.UTF8.GetString(EncryptionUtil.DecryptBytes(key, extraKey ?? Convert.ToBase64String(envelope.Salt), envelope.Salt, envelope.IV));
            }
            catch (Exception ex)
            {
                Shared.Plugin.Common.Logger.Error(ex, "UNWRAP: " + ex.Message);
            }

            Shared.Plugin.Common.Logger.Debug( "K: " + skey);
            if (envelope.IsEncrypted)
            {
                try
                {
                    data = EncryptionUtil.DecryptBytes(data, skey, envelope.Salt, envelope.IV);                
                }
                catch (Exception ex)
                {
                    Shared.Plugin.Common.Logger.Error(ex, "UNWRAP2: " + ex.Message);
                }
            }

            if (envelope.IsCompressed)
                data = CompressionUtil.Decompress(data);
          
            using (var ms = new MemoryStream(data))
            {
                return Serializer.Deserialize<T>(ms);
            }
        }

    }
}
