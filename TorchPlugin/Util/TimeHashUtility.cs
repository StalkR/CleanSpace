using System;
using System.Text;
using System.Security.Cryptography;

namespace CleanSpace
{
    public static class HKDF
    {
        public static byte[] DeriveKey(byte[] ikm, byte[] salt, byte[] info, int outputLength)
        {
            var hmac = new HMACSHA512(salt ?? new byte[64]);
            byte[] prk = hmac.ComputeHash(ikm);
            var result = new byte[outputLength];
            byte[] previousBlock = Array.Empty<byte>();
            byte counter = 1;
            int bytesWritten = 0;
            var hmacExpand = new HMACSHA512(prk);
            while (bytesWritten < outputLength)
            {
                var data = new byte[previousBlock.Length + info.Length + 1];
                Buffer.BlockCopy(previousBlock, 0, data, 0, previousBlock.Length);
                Buffer.BlockCopy(info, 0, data, previousBlock.Length, info.Length);
                data[data.Length - 1] = counter++;

                previousBlock = hmacExpand.ComputeHash(data);
                int toCopy = Math.Min(previousBlock.Length, outputLength - bytesWritten);
                Buffer.BlockCopy(previousBlock, 0, result, bytesWritten, toCopy);
                bytesWritten += toCopy;
            }

            return result;
        }
    }

    public static class TimeHashUtility
    {
        public static string SharedSecret => ((ViewModelConfig)CleanSpaceTorchPlugin.Instance.Config).Secret;
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
        public static string GenerateToken(string sharedSecret, TimeSpan validFor, string purpose = "default")
        {
            long unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            byte[] buffer = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }
            string nonce = Convert.ToBase64String(buffer);
            string payload = $"{unixTime}|{purpose}|{nonce}";

            var key = DeriveKey(sharedSecret, purpose);
            string signature = SignPayload(payload, key);
            return $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))}.{signature}";
        }

        public static bool IsTokenValid(string token, string sharedSecret, TimeSpan maxAge, string expectedPurpose = "default")
        {
            var parts = token.Split('.');
            if (parts.Length != 2)
                return false;

            string encodedPayload = parts[0];
            string receivedSignature = parts[1];

            string payload;
            try
            {
                payload = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPayload));
            }
            catch
            {
                return false;
            }

            var key = DeriveKey(sharedSecret, expectedPurpose);
            string expectedSignature = SignPayload(payload, key);

            if (!FixedTimeEquals(
                    Convert.FromBase64String(receivedSignature),
                    Convert.FromBase64String(expectedSignature)))
            {
                return false;
            }

            var payloadParts = payload.Split('|');
            if (payloadParts.Length != 3)
                return false;

            if (!long.TryParse(payloadParts[0], out long tokenTime))
                return false;

            string purpose = payloadParts[1];
            if (purpose != expectedPurpose)
                return false;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return now - tokenTime <= maxAge.TotalSeconds;
        }

        private static string SignPayload(string payload, byte[] key)
        {
            var hmac = new HMACSHA512(key);
            byte[] sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(sig);
        }

        private static byte[] DeriveKey(string seed, string purpose)
        {
            byte[] ikm = Encoding.UTF8.GetBytes(seed);
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] info = Encoding.UTF8.GetBytes(purpose);
            return HKDF.DeriveKey(ikm, salt, info, 64);
        }

    }
}
