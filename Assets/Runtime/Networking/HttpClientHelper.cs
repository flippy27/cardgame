using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Centralized HTTP request handler. All HTTP operations (GET, POST, PUT, DELETE) go through here.
    /// Handles: retry logic, circuit breaker, authorization headers, error handling, logging.
    /// </summary>
    public static class HttpClientHelper
    {
        private static CircuitBreaker _circuitBreaker = new CircuitBreaker(failureThreshold: 5, resetTimeoutSeconds: 60);

        public static int TimeoutSeconds { get; set; } = 30;
        public static int MaxRetries { get; set; } = 3;
        public static int RetryDelayMs { get; set; } = 500;

        /// <summary>
        /// GET request with retry, authorization header, and error handling.
        /// </summary>
        public static async Task<string> GetAsync(string url)
        {
            return await ExecuteWithRetry(url, "GET", null, retryAttempt: 0);
        }

        /// <summary>
        /// POST request with JSON body, retry, and authorization header.
        /// </summary>
        public static async Task<string> PostAsync(string url, string jsonBody)
        {
            return await ExecutePostWithRetry(url, jsonBody, retryAttempt: 0);
        }

        /// <summary>
        /// PUT request with JSON body.
        /// </summary>
        public static async Task<string> PutAsync(string url, string jsonBody)
        {
            return await ExecuteWithRetry(url, "PUT", jsonBody, retryAttempt: 0);
        }

        /// <summary>
        /// DELETE request.
        /// </summary>
        public static async Task<string> DeleteAsync(string url)
        {
            return await ExecuteWithRetry(url, "DELETE", null, retryAttempt: 0);
        }

        private static async Task<string> ExecuteWithRetry(string url, string method, string jsonBody = null, int retryAttempt = 0)
        {
            if (_circuitBreaker.IsOpen())
            {
                throw new InvalidOperationException($"Circuit breaker OPEN. Server unavailable ({_circuitBreaker.FailureCount} failures)");
            }

            var startTime = Time.realtimeSinceStartup;
            try
            {
                Debug.Log($"[HTTP] {method} {url} (attempt {retryAttempt + 1}/{MaxRetries + 1})");

                using var request = new UnityWebRequest(url, method);
                request.downloadHandler = new DownloadHandlerBuffer();

                // Add request body if provided (for PUT, PATCH, etc.)
                if (!string.IsNullOrEmpty(jsonBody))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                // Add authorization header if token available
                var token = SecureTokenStorage.GetToken();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {token}");
                }

                request.timeout = TimeoutSeconds;

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Delay(10);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var statusCode = request.responseCode;
                    var errorMsg = request.error ?? "Unknown error";

                    Debug.LogError($"[HTTP] {method} failed: HTTP {statusCode} - {errorMsg}");
                    _circuitBreaker.RecordFailure();

                    if (statusCode == 401 || statusCode == 403)
                        throw new UnauthorizedAccessException($"Unauthorized: {url}");

                    if (statusCode == 404)
                        throw new InvalidOperationException($"Resource not found: {url}");

                    if (statusCode >= 500 && retryAttempt < MaxRetries)
                    {
                        var delay = (int)Math.Pow(2, retryAttempt) * RetryDelayMs;
                        await Task.Delay(delay);
                        return await ExecuteWithRetry(url, method, jsonBody, retryAttempt + 1);
                    }

                    throw new InvalidOperationException($"HTTP {statusCode}: {errorMsg}");
                }

                var durationMs = (Time.realtimeSinceStartup - startTime) * 1000f;
                Debug.Log($"[HTTP] {method} success: {url} ({request.downloadHandler.text.Length} bytes) in {durationMs:F2}ms");
                _circuitBreaker.RecordSuccess();
                MetricsCollector.RecordRequest(url, method, (int)request.responseCode, durationMs);

                return request.downloadHandler.text;
            }
            catch (TaskCanceledException)
            {
                Debug.LogError($"[HTTP] Timeout on {method} {url}");
                _circuitBreaker.RecordFailure();
                throw new InvalidOperationException($"Request timeout: {url}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HTTP] Unexpected error on {method} {url}: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }

        private static async Task<string> ExecutePostWithRetry(string url, string jsonBody, int retryAttempt = 0)
        {
            if (_circuitBreaker.IsOpen())
            {
                throw new InvalidOperationException($"Circuit breaker OPEN. Server unavailable ({_circuitBreaker.FailureCount} failures)");
            }

            var startTime = Time.realtimeSinceStartup;
            try
            {
                Debug.Log($"[HTTP] POST {url} (attempt {retryAttempt + 1}/{MaxRetries + 1})");

                using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                // Add authorization header if token available
                var token = SecureTokenStorage.GetToken();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {token}");
                }

                request.timeout = TimeoutSeconds;

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Delay(10);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var statusCode = request.responseCode;
                    var errorMsg = request.error ?? "Unknown error";

                    Debug.LogError($"[HTTP] POST failed: HTTP {statusCode} - {errorMsg}");
                    _circuitBreaker.RecordFailure();

                    if (statusCode == 401 || statusCode == 403)
                        throw new UnauthorizedAccessException($"Unauthorized: {url}");

                    if (statusCode == 404)
                        throw new InvalidOperationException($"Resource not found: {url}");

                    if (statusCode >= 500 && retryAttempt < MaxRetries)
                    {
                        var delay = (int)Math.Pow(2, retryAttempt) * RetryDelayMs;
                        await Task.Delay(delay);
                        return await ExecutePostWithRetry(url, jsonBody, retryAttempt + 1);
                    }

                    throw new InvalidOperationException($"HTTP {statusCode}: {errorMsg}");
                }

                var durationMs = (Time.realtimeSinceStartup - startTime) * 1000f;
                Debug.Log($"[HTTP] POST success: {url} ({request.downloadHandler.text.Length} bytes) in {durationMs:F2}ms");
                _circuitBreaker.RecordSuccess();
                MetricsCollector.RecordRequest(url, "POST", (int)request.responseCode, durationMs);

                return request.downloadHandler.text;
            }
            catch (TaskCanceledException)
            {
                Debug.LogError($"[HTTP] Timeout on POST {url}");
                _circuitBreaker.RecordFailure();
                throw new InvalidOperationException($"Request timeout: {url}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HTTP] Unexpected error on POST {url}: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }
    }
}
