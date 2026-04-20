using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Fachada de Unity Multiplayer Services para sesiones 1v1.
    /// </summary>
    public sealed class MpsGameSessionService : MonoBehaviour
    {
        public string environmentName = "production";
        public MatchmakerConfig matchmakerConfig;

        public ISession CurrentSession { get; private set; }
        public bool IsInitialized { get; private set; }
        public string LastJoinCode { get; private set; }
        public string LocalPlayerId => AuthenticationService.Instance?.PlayerId ?? string.Empty;

        public event Action<ISession> SessionChanged;
        public event Action<string> JoinCodeChanged;
        public event Action<string> ErrorRaised;

        public async Task EnsureInitializedAsync()
        {
            if (IsInitialized)
            {
                Debug.Log("[MpsGameSessionService] Already initialized");
                return;
            }

            try
            {
                Debug.Log("[MpsGameSessionService] Initializing UnityServices...");
                var options = new InitializationOptions();
                if (!string.IsNullOrWhiteSpace(environmentName))
                {
                    options.SetEnvironmentName(environmentName);
                }

                await UnityServices.InitializeAsync(options);
                Debug.Log("[MpsGameSessionService] UnityServices initialized");

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("[MpsGameSessionService] Signing in anonymously...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log("[MpsGameSessionService] Signed in");
                }

                IsInitialized = true;
                Debug.Log("[MpsGameSessionService] Ready");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MpsGameSessionService] Init failed: {exception.Message}\n{exception.StackTrace}");
                ErrorRaised?.Invoke(exception.Message);
                throw;
            }
        }

        public async Task QuickMatchAsync()
        {
            Debug.Log("[MpsGameSessionService] QuickMatchAsync started");
            await EnsureInitializedAsync();

            try
            {
                Debug.Log("[MpsGameSessionService] Building QuickJoin options...");
                var quickJoin = new QuickJoinOptions
                {
                    CreateSession = matchmakerConfig == null || matchmakerConfig.createSessionIfQuickJoinFails,
                    Timeout = TimeSpan.FromSeconds(matchmakerConfig != null ? matchmakerConfig.quickJoinTimeoutSeconds : 5f)
                };

                Debug.Log($"[MpsGameSessionService] Calling MatchmakeSessionAsync (createIfFails={quickJoin.CreateSession}, timeout={quickJoin.Timeout.TotalSeconds}s)...");
                CurrentSession = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, BuildBaseSessionOptions(false));
                Debug.Log($"[MpsGameSessionService] Got session: {CurrentSession?.Name}");
                WireSession(CurrentSession);
                Debug.Log("[MpsGameSessionService] Session wired");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MpsGameSessionService] QuickMatch failed: {exception.Message}\n{exception.StackTrace}");
                ErrorRaised?.Invoke(exception.Message);
            }
        }

        public async Task AdvancedMatchmakerAsync(CancellationToken cancellationToken)
        {
            await EnsureInitializedAsync();

            try
            {
                var options = new MatchmakerOptions
                {
                    QueueName = matchmakerConfig != null && !string.IsNullOrWhiteSpace(matchmakerConfig.advancedQueueName)
                        ? matchmakerConfig.advancedQueueName
                        : "Friendly"
                };

                CurrentSession = await MultiplayerService.Instance.MatchmakeSessionAsync(options, BuildBaseSessionOptions(false), cancellationToken);
                WireSession(CurrentSession);
            }
            catch (Exception exception)
            {
                ErrorRaised?.Invoke(exception.Message);
            }
        }

        public async Task CreatePrivateMatchAsync(string sessionName)
        {
            await EnsureInitializedAsync();

            try
            {
                var options = BuildBaseSessionOptions(true);
                options.Name = string.IsNullOrWhiteSpace(sessionName) ? "Private Match" : sessionName;
                CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                WireSession(CurrentSession);
            }
            catch (Exception exception)
            {
                ErrorRaised?.Invoke(exception.Message);
            }
        }

        public async Task JoinByCodeAsync(string joinCode)
        {
            await EnsureInitializedAsync();

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                ErrorRaised?.Invoke("Join code vacío.");
                return;
            }

            try
            {
                CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim().ToUpperInvariant());
                WireSession(CurrentSession);
            }
            catch (Exception exception)
            {
                ErrorRaised?.Invoke(exception.Message);
            }
        }

        public async Task LeaveAsync()
        {
            if (CurrentSession == null)
            {
                return;
            }

            try
            {
                await CurrentSession.LeaveAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                CurrentSession = null;
                LastJoinCode = string.Empty;
                SessionChanged?.Invoke(null);
                JoinCodeChanged?.Invoke(string.Empty);
            }
        }

        public void CopyJoinCodeToClipboard()
        {
            if (string.IsNullOrWhiteSpace(LastJoinCode))
            {
                ErrorRaised?.Invoke("No join code disponible.");
                return;
            }

            GUIUtility.systemCopyBuffer = LastJoinCode;
        }

        private SessionOptions BuildBaseSessionOptions(bool isPrivate)
        {
            return new SessionOptions
            {
                MaxPlayers = 2,
                Type = matchmakerConfig != null && !string.IsNullOrWhiteSpace(matchmakerConfig.sessionType)
                    ? matchmakerConfig.sessionType
                    : "CardDuel",
                IsPrivate = isPrivate,
                Name = "Card Duel"
            }.WithRelayNetwork();
        }

        private void WireSession(ISession session)
        {
            if (session == null)
            {
                ErrorRaised?.Invoke("No se pudo obtener sesión.");
                return;
            }

            LastJoinCode = session.Code;
            SessionChanged?.Invoke(session);
            JoinCodeChanged?.Invoke(LastJoinCode);

            session.PlayerJoined += _ => SessionChanged?.Invoke(session);
            session.PlayerLeaving += _ => SessionChanged?.Invoke(session);
            session.StateChanged += _ => SessionChanged?.Invoke(session);
            session.RemovedFromSession += () => SessionChanged?.Invoke(null);
        }
    }
}
