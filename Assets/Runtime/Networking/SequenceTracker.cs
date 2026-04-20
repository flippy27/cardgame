using System;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Global sequence number tracker for all messages sent to API.
    /// Used to detect packet loss and order actions correctly.
    /// </summary>
    public sealed class SequenceTracker
    {
        private static int _globalSequence = 0;
        private static readonly object _lock = new object();

        public static int NextSequence()
        {
            lock (_lock)
            {
                return ++_globalSequence;
            }
        }

        public static int CurrentSequence()
        {
            lock (_lock)
            {
                return _globalSequence;
            }
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _globalSequence = 0;
            }
        }
    }
}
