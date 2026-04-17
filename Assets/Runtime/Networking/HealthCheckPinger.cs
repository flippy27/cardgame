using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    public class HealthCheckPinger : MonoBehaviour
    {
        private string _healthUrl;
        private int _failureCount;
        private int _maxFailures = 3;
        private float _pingIntervalSeconds = 30f;
        private bool _isHealthy = true;

        public bool IsServerHealthy => _isHealthy;
        public int FailureCount => _failureCount;

        public void Initialize(string baseUrl)
        {
            _healthUrl = $"{baseUrl.TrimEnd('/')}/api/v1/health";
            StartCoroutine(PingLoop());
        }

        private IEnumerator PingLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(_pingIntervalSeconds);
                yield return StartCoroutine(Ping());
            }
        }

        private IEnumerator Ping()
        {
            using var req = UnityWebRequest.Get(_healthUrl);
            req.timeout = 5;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[Health] Server OK");
                _failureCount = 0;
                if (!_isHealthy)
                {
                    _isHealthy = true;
                    GameEvents.RaiseConnected();
                }
            }
            else
            {
                _failureCount++;
                Debug.LogWarning($"[Health] Ping failed ({_failureCount}/{_maxFailures}): {req.error}");

                if (_failureCount >= _maxFailures && _isHealthy)
                {
                    _isHealthy = false;
                    GameEvents.RaiseDisconnected();
                }
            }
        }

        public void Stop()
        {
            StopAllCoroutines();
        }
    }
}
