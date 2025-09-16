using System;
using System.Security.Cryptography;
using System.Text;

namespace CleanSpaceShared.Util
{
    internal static class TokenUtility
    {
        private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(5);
        internal static string GenerateToken(string sharedSecret, DateTimeOffset expiryTime, string purpose = "default")
        {
            long expiryUnix = expiryTime.ToUnixTimeSeconds();
            byte[] nonceBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonceBytes);
            }
            string nonce = Convert.ToBase64String(nonceBytes);
            string payload = $"{expiryUnix}|{purpose}|{nonce}";
            byte[] key = DeriveKey(sharedSecret, purpose);
            string signature = SignPayload(payload, key);
            string combined = $"{expiryUnix}|{purpose}|{nonce}|{signature}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(combined));
        }

        internal static bool IsTokenValid(string token, string sharedSecret, string expectedPurpose = "default")
        {
            string payload;
            try
            {
                payload = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            }
            catch
            {
                return false; // malformed token
            }

            var parts = payload.Split('|');
            if (parts.Length != 4)
                return false;

            if (!long.TryParse(parts[0], out long expiryUnix))
                return false;

            string purpose = parts[1];
            string nonce = parts[2];
            string receivedSignature = parts[3];

            if (!string.Equals(purpose, expectedPurpose, StringComparison.Ordinal))
                return false;

            DateTimeOffset expiryTime = DateTimeOffset.FromUnixTimeSeconds(expiryUnix);
            if (DateTimeOffset.UtcNow - ClockSkew > expiryTime)
                return false;

            string unsignedPayload = $"{expiryUnix}|{purpose}|{nonce}";
            byte[] key = DeriveKey(sharedSecret, expectedPurpose);

            string expectedSignature;
            try
            {
                expectedSignature = SignPayload(unsignedPayload, key);
            }
            catch
            {
                return false;
            }

            try
            {
                if (!FixedTimeEquals(
                        Convert.FromBase64String(receivedSignature),
                        Convert.FromBase64String(expectedSignature)))
                    return false;
            }
            catch
            {
                return false;
            }

            return true;
        }

        internal static string SignPayload(string payload, byte[] key)
        {
            var hmac = new HMACSHA512(key);
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(signature);
        }

        internal static byte[] SignPayloadBytes(byte[] payload, byte[] key)
        {
            var hmac = new HMACSHA512(key);
            return hmac.ComputeHash(payload);
        }

        private static byte[] DeriveKey(string seed, string purpose)
        {
            using (var sha = SHA256.Create())
            {
                string combined = seed + ":" + purpose;
                byte[] data = Encoding.UTF8.GetBytes(combined);
                return sha.ComputeHash(data);
            }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }
    }
}
