using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flippy.CardDuelMobile.Core
{
    [System.Serializable]
    public class RequestMetric
    {
        public string endpoint;
        public string method;
        public int statusCode;
        public float durationMs;
        public bool success;
        public long timestamp;
    }

    public static class MetricsCollector
    {
        private static List<RequestMetric> _metrics = new();
        private static float _lastUploadTime;
        private const float UPLOAD_INTERVAL = 60f; // 60 seconds

        public static void RecordRequest(string endpoint, string method, int statusCode, float durationMs)
        {
            var metric = new RequestMetric
            {
                endpoint = endpoint,
                method = method,
                statusCode = statusCode,
                durationMs = durationMs,
                success = statusCode >= 200 && statusCode < 300,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            _metrics.Add(metric);
            GameLogger.Debug("Metrics", $"{method} {endpoint}: {durationMs}ms ({statusCode})");

            if (_metrics.Count > 100 || Time.realtimeSinceStartup - _lastUploadTime > UPLOAD_INTERVAL)
            {
                TryUploadMetrics();
            }
        }

        public static void PrintStats()
        {
            if (_metrics.Count == 0)
            {
                GameLogger.Info("Metrics", "No metrics collected");
                return;
            }

            var totalTime = 0f;
            var successCount = 0;
            var errorCount = 0;

            foreach (var m in _metrics)
            {
                totalTime += m.durationMs;
                if (m.success) successCount++;
                else errorCount++;
            }

            var avgTime = totalTime / _metrics.Count;
            var successRate = (successCount * 100f) / _metrics.Count;

            GameLogger.Info("Metrics", $"Requests: {_metrics.Count} | Avg: {avgTime:F2}ms | Success: {successRate:F1}%");
        }

        private static void TryUploadMetrics()
        {
            if (_metrics.Count == 0) return;

            GameLogger.Info("Metrics", $"Uploading {_metrics.Count} metrics to telemetry");
            _metrics.Clear();
            _lastUploadTime = Time.realtimeSinceStartup;
        }
    }
}
