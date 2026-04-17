using System;
using System.Security.Cryptography;
using System.Text;

namespace Flippy.CardDuelMobile.Core
{
    public static class RequestSigner
    {
        private const string SECRET_KEY = "your-secret-key-change-in-production";

        public static string SignRequest(string method, string path, string body = "")
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var nonce = Guid.NewGuid().ToString("N").Substring(0, 16);
            var payload = $"{method}:{path}:{body}:{timestamp}:{nonce}";

            var signature = ComputeHmacSha256(payload, SECRET_KEY);
            GameLogger.Debug("Signing", $"Signed {method} {path}");

            return signature;
        }

        public static bool VerifySignature(string method, string path, string body, string signature, string timestamp)
        {
            var payload = $"{method}:{path}:{body}:{timestamp}";
            var expectedSignature = ComputeHmacSha256(payload, SECRET_KEY);

            return signature == expectedSignature;
        }

        private static string ComputeHmacSha256(string message, string secret)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var messageBytes = Encoding.UTF8.GetBytes(message);

            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hashBytes = hmac.ComputeHash(messageBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
