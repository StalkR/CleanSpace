using System;
using System.IO;
using System.Security.Cryptography;

public static class EncryptionUtil
{  
    private static void DebugLog(string message)
    {
        CleanSpaceShared.Plugin.Common.Logger.Debug($"{CleanSpaceShared.Plugin.Common.PluginName}: {message}");
    }

    private static string ToHex(byte[] data)
    {
        return BitConverter.ToString(data ?? Array.Empty<byte>()).Replace("-", "");
    }

    public static byte[] GenerateSalt(int saltLengthBytes = 16)
    {
        byte[] salt = new byte[saltLengthBytes];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(salt);
        return salt;
    }

    private static Aes CreateConfiguredAes(string seed, byte[] salt, byte[] IV = null)
    {
        DebugLog($"Creating AES with seed length {seed?.Length}, salt {ToHex(salt)}");

        var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Padding = PaddingMode.PKCS7;
        aes.Mode = CipherMode.CBC;

        using (var keyDeriver = new Rfc2898DeriveBytes(seed, salt, 100))
            aes.Key = keyDeriver.GetBytes(aes.KeySize / 8);

        if (IV == null)
            aes.GenerateIV();
        else
            aes.IV = (byte[])IV.Clone();
        DebugLog($"AES configured: Key length {aes.Key.Length}, IV length {aes.IV.Length}, IV {ToHex(aes.IV)}");
        return aes;
    }

    public static byte[] EncryptBytes(byte[] data, string seed, byte[] salt, out byte[] IV)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        DebugLog($"EncryptBytes: input length {data.Length}, salt {ToHex(salt)}");

        using (var aes = CreateConfiguredAes(seed, salt))
        {
            IV = (byte[])aes.IV.Clone();

            using (var ms = new MemoryStream())
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
                byte[] encrypted = ms.ToArray();
                return encrypted;
            }
        }
    }

    public static byte[] EncryptBytesWithIV(byte[] data, string seed, byte[] salt, byte[] IV)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        using (var aes = CreateConfiguredAes(seed, salt, IV))
        using (var ms = new MemoryStream())
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();
            byte[] encrypted = ms.ToArray();
            return encrypted;
        }
    }

    public static byte[] DecryptBytes(byte[] encryptedData, string seed, byte[] salt, byte[] IV)
    {
        if (encryptedData == null) throw new ArgumentNullException(nameof(encryptedData));
        using (var aes = CreateConfiguredAes(seed, salt, IV))
        using (var ms = new MemoryStream(encryptedData))
        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
        using (var reader = new MemoryStream())
        {          
            cs.CopyTo(reader);
            byte[] result = reader.ToArray();
            return result;
        }
    }

}
