using System;

namespace Flippy.CardDuelMobile.Core
{
    public static class RequestValidator
    {
        public static void ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ValidationException("Email required");
            if (!email.Contains("@") || email.Length < 3)
                throw new ValidationException("Invalid email format");
        }

        public static void ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 3)
                throw new ValidationException("Password must be 3+ chars");
        }
    }
}
