namespace Flippy.CardDuelMobile.Core
{
    /// <summary>
    /// API configuration — single source of truth for the server endpoint.
    /// At runtime the value comes from Resources/config.json (ConfigManager pushes it
    /// here via SetUrl). The constant below is only the fallback when config.json is
    /// missing/invalid, and the API_BASE_URL env var overrides everything.
    /// All paths now point at the Raspberry Pi server (192.168.1.87:5000).
    /// (Use the IP, NOT localhost — localhost resolves to IPv6 ::1 and the Docker port-forward hangs on it.)
    /// </summary>
    public static class ApiConfig
    {
        // Same fallback in editor and player builds: the Pi. Keeps editor, device and
        // config.json from diverging onto different hosts.
        private const string DEFAULT_BASE_URL = "http://192.168.1.87:5000";

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
