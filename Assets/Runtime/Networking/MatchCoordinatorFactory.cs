using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Factory that provides either SignalR or HTTP coordinator based on availability.
    /// Allows seamless switching between real-time WebSocket and polling HTTP.
    /// </summary>
    public sealed class MatchCoordinatorFactory
    {
        public enum CoordinatorType
        {
            SignalR,
            HTTP
        }

        private static MatchCoordinatorFactory _instance;
        public static MatchCoordinatorFactory Instance => _instance ??= new MatchCoordinatorFactory();

        private CoordinatorType _preferredType = CoordinatorType.SignalR;
        private MatchSignalRCoordinator _signalRCoordinator;
        private MatchHttpCoordinator _httpCoordinator;

        public CoordinatorType CurrentType { get; private set; } = CoordinatorType.HTTP;

        /// <summary>
        /// Set preferred coordinator type. Will attempt SignalR first, fall back to HTTP.
        /// </summary>
        public void SetPreferredType(CoordinatorType type)
        {
            _preferredType = type;
        }

        /// <summary>
        /// Get active coordinator (SignalR if available, else HTTP).
        /// </summary>
        public IMatchCoordinator GetCoordinator()
        {
            if (_preferredType == CoordinatorType.SignalR && MatchSignalRCoordinator.Instance != null)
            {
                CurrentType = CoordinatorType.SignalR;
                return MatchSignalRCoordinator.Instance;
            }

            if (MatchHttpCoordinator.Instance != null)
            {
                CurrentType = CoordinatorType.HTTP;
                return MatchHttpCoordinator.Instance;
            }

            Debug.LogError("[MatchCoordinatorFactory] No coordinator available!");
            return null;
        }

        /// <summary>
        /// Check if SignalR is available.
        /// </summary>
        public bool IsSignalRAvailable => MatchSignalRCoordinator.Instance != null;

        /// <summary>
        /// Check if HTTP is available.
        /// </summary>
        public bool IsHttpAvailable => MatchHttpCoordinator.Instance != null;
    }

    /// <summary>
    /// Common interface for both SignalR and HTTP coordinators.
    /// Uses fire-and-forget async void pattern matching existing implementations.
    /// </summary>
    public interface IMatchCoordinator
    {
        // Note: These are async void to match existing MatchHttpCoordinator implementation
        void RequestPlayCard(string runtimeCardKey, int slotIndex);
        void RequestEndTurn();
        void RequestSetReady(bool isReady);
        void RequestForfeit();
    }
}
