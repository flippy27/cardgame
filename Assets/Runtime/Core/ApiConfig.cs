namespace Flippy.CardDuelMobile.Core
{
    /// <summary>
    /// API configuration. Set via environment or override in code.
    /// Dev: http://localhost:5000
    /// Prod: https://api.cardduel.com (set via env var API_BASE_URL)
    /// </summary>
    public static class ApiConfig
    {
#if UNITY_EDITOR
        private const string DEFAULT_BASE_URL = "http://localhost:5000";
#else
        private const string DEFAULT_BASE_URL = "https://api.cardduel.com";
#endif

        public static string BaseUrl { get; set; } = GetUrlFromEnvironment() ?? DEFAULT_BASE_URL;
        public static int TimeoutSeconds { get; set; } = 30;
        public static int MaxRetries { get; set; } = 3;
        public static int RetryDelayMs { get; set; } = 500;

        private static string GetUrlFromEnvironment()
        {
            // Check environment variable (set via build config or runtime)
            var envUrl = System.Environment.GetEnvironmentVariable("API_BASE_URL");
            return !string.IsNullOrWhiteSpace(envUrl) ? envUrl.TrimEnd('/') : null;
        }

        public static void SetUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ValidationException("API URL cannot be empty");
            BaseUrl = url.TrimEnd('/');
        }
    }
}
