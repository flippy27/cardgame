using System;

namespace Flippy.CardDuelMobile.Networking
{
    public enum CircuitState { Closed, Open, HalfOpen }

    public class CircuitBreaker
    {
        private CircuitState _state = CircuitState.Closed;
        private int _failureCount;
        private DateTime _lastFailureTime;
        private readonly int _failureThreshold;
        private readonly int _resetTimeoutSeconds;

        public CircuitState State => _state;
        public int FailureCount => _failureCount;

        public CircuitBreaker(int failureThreshold = 5, int resetTimeoutSeconds = 60)
        {
            _failureThreshold = failureThreshold;
            _resetTimeoutSeconds = resetTimeoutSeconds;
        }

        public void RecordSuccess()
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }

        public void RecordFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                UnityEngine.Debug.LogWarning($"[Circuit] OPEN after {_failureCount} failures");
            }
        }

        public bool IsOpen()
        {
            if (_state != CircuitState.Open)
                return false;

            var timeSinceLastFailure = (DateTime.UtcNow - _lastFailureTime).TotalSeconds;
            if (timeSinceLastFailure > _resetTimeoutSeconds)
            {
                _state = CircuitState.HalfOpen;
                _failureCount = 0;
                UnityEngine.Debug.Log("[Circuit] HALF-OPEN: testing recovery");
                return false;
            }

            return true;
        }

        public void Reset()
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }
}
