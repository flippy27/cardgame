using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// SignalR-based match coordinator for real-time gameplay.
    /// Uses a direct WebSocket SignalR connection and falls back to HTTP polling if needed.
    /// </summary>
    public sealed class MatchSignalRCoordinator : MonoBehaviour, IMatchCoordinator
    {
        private const char RecordSeparator = '\u001e';
        private const int HandshakeTimeoutMs = 5000;
        private const int InvocationTimeoutMs = 10000;
        private const int PingIntervalMs = 10000;
        private const string MatchSnapshotTarget = "MatchSnapshot";

        [Header("Match")]
        public string matchId;
        public string playerId;
        public string reconnectToken;
        public int seatIndex;

        private readonly object _mainThreadLock = new();
        private readonly Queue<Action> _mainThreadQueue = new();
        private readonly object _pendingLock = new();
        private readonly Dictionary<string, TaskCompletionSource<MatchSnapshot>> _pendingInvocations = new();

        private MatchHttpCoordinator _httpCoordinator;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _connectionCts;
        private Task _receiveLoopTask;
        private Task _pingLoopTask;
        private TaskCompletionSource<bool> _handshakeCompletionSource;
        private int _invocationCounter;
        private bool _usingHttpFallback;
        private MatchSnapshot _currentSnapshot;

        public static MatchSignalRCoordinator Instance { get; private set; }
        public bool IsConnected => _usingHttpFallback
            ? _httpCoordinator != null
            : _webSocket != null && _webSocket.State == WebSocketState.Open;
        public bool IsUsingHttpFallback => _usingHttpFallback;
        public MatchSnapshot CurrentSnapshot => _currentSnapshot;

        public event Action<MatchSnapshot> SnapshotChanged;
        public event Action<string> ErrorOccurred;
        public event Action ConnectionEstablished;
        public event Action ConnectionLost;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            FlushMainThreadQueue();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            _ = DisconnectAsync();
        }

        public async Task<bool> ConnectAsync(string matchId, string playerId, string reconnectToken, int seatIndex)
        {
            this.matchId = matchId;
            this.playerId = playerId;
            this.reconnectToken = reconnectToken;
            this.seatIndex = seatIndex;

            await DisconnectAsync();

            try
            {
                GameLogger.Info("SignalR", $"Connecting to match {matchId} as player {playerId}");

                await ConnectSignalRInternalAsync();
                EnqueueOnMainThread(() => ConnectionEstablished?.Invoke());

                GameLogger.Info("SignalR", "Connected using SignalR WebSocket");
                return true;
            }
            catch (Exception ex)
            {
                GameLogger.Warning("SignalR", $"SignalR connection failed, using HTTP fallback: {ex.Message}");
                EnqueueOnMainThread(() => ErrorOccurred?.Invoke($"SignalR unavailable, using HTTP fallback: {ex.Message}"));

                try
                {
                    await StartHttpFallbackAsync();
                    EnqueueOnMainThread(() => ConnectionEstablished?.Invoke());
                    return true;
                }
                catch (Exception fallbackEx)
                {
                    GameLogger.Error("SignalR", $"Fallback connection failed: {fallbackEx.Message}");
                    EnqueueOnMainThread(() => ErrorOccurred?.Invoke(fallbackEx.Message));
                    return false;
                }
            }
        }

        public async Task SetReadyAsync(bool isReady)
        {
            try
            {
                if (_usingHttpFallback && _httpCoordinator != null)
                {
                    _httpCoordinator.RequestSetReady(isReady);
                    return;
                }

                EnsureSignalRConnected();
                await InvokeAsync("SetReady", new SetReadyRequestDto
                {
                    matchId = matchId,
                    playerId = playerId,
                    isReady = isReady
                });
            }
            catch (Exception ex)
            {
                ReportActionError("SetReady", ex);
            }
        }

        public async Task PlayCardAsync(string runtimeHandKey, int slotIndex)
        {
            try
            {
                if (_usingHttpFallback && _httpCoordinator != null)
                {
                    _httpCoordinator.RequestPlayCard(runtimeHandKey, slotIndex);
                    return;
                }

                EnsureSignalRConnected();
                await InvokeAsync("PlayCard", new PlayCardRequestDto
                {
                    matchId = matchId,
                    playerId = playerId,
                    runtimeHandKey = runtimeHandKey,
                    slotIndex = slotIndex
                });
            }
            catch (Exception ex)
            {
                ReportActionError("PlayCard", ex);
            }
        }

        public async Task EndTurnAsync()
        {
            try
            {
                if (_usingHttpFallback && _httpCoordinator != null)
                {
                    _httpCoordinator.RequestEndTurn();
                    return;
                }

                EnsureSignalRConnected();
                await InvokeAsync("EndTurn", new EndTurnRequestDto
                {
                    matchId = matchId,
                    playerId = playerId
                });
            }
            catch (Exception ex)
            {
                ReportActionError("EndTurn", ex);
            }
        }

        public async Task ForfeitAsync()
        {
            try
            {
                if (_usingHttpFallback && _httpCoordinator != null)
                {
                    _httpCoordinator.RequestForfeit();
                    return;
                }

                EnsureSignalRConnected();
                await InvokeAsync("Forfeit", new ForfeitRequestDto
                {
                    matchId = matchId,
                    playerId = playerId
                });
            }
            catch (Exception ex)
            {
                ReportActionError("Forfeit", ex);
            }
        }

        public async Task DisconnectAsync()
        {
            await StopHttpFallbackAsync();
            await StopSignalRAsync();
            _currentSnapshot = null;
        }

        void IMatchCoordinator.RequestPlayCard(string runtimeCardKey, int slotIndex)
        {
            _ = PlayCardAsync(runtimeCardKey, slotIndex);
        }

        void IMatchCoordinator.RequestEndTurn()
        {
            _ = EndTurnAsync();
        }

        void IMatchCoordinator.RequestSetReady(bool isReady)
        {
            _ = SetReadyAsync(isReady);
        }

        void IMatchCoordinator.RequestForfeit()
        {
            _ = ForfeitAsync();
        }

        private async Task ConnectSignalRInternalAsync()
        {
            _usingHttpFallback = false;
            _connectionCts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();
            _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            var hubUri = BuildHubUri(ConfigManager.GetApiBaseUrl(), matchId, SecureTokenStorage.GetToken());
            await _webSocket.ConnectAsync(hubUri, _connectionCts.Token);

            _handshakeCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _receiveLoopTask = ReceiveLoopAsync(_webSocket, _connectionCts.Token);

            await SendFrameAsync("{\"protocol\":\"json\",\"version\":1}" + RecordSeparator, _connectionCts.Token);
            await WaitWithTimeout(_handshakeCompletionSource.Task, HandshakeTimeoutMs, "SignalR handshake timed out.");

            _pingLoopTask = PingLoopAsync(_connectionCts.Token);

            var snapshot = await InvokeAsync("ConnectToMatch", new ConnectMatchRequestDto
            {
                matchId = matchId,
                playerId = playerId,
                reconnectToken = reconnectToken
            });

            if (_currentSnapshot == null && snapshot != null)
            {
                EnqueueOnMainThread(() => ProcessSnapshot(snapshot));
            }
        }

        private async Task<MatchSnapshot> InvokeAsync(string target, object payload)
        {
            if (_webSocket == null || _connectionCts == null)
            {
                throw new InvalidOperationException("SignalR socket is not initialized.");
            }

            var invocationId = Interlocked.Increment(ref _invocationCounter).ToString(CultureInfo.InvariantCulture);
            var completion = new TaskCompletionSource<MatchSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pendingLock)
            {
                _pendingInvocations[invocationId] = completion;
            }

            var payloadJson = JsonUtility.ToJson(payload);
            var frame = $"{{\"type\":1,\"invocationId\":\"{EscapeJson(invocationId)}\",\"target\":\"{EscapeJson(target)}\",\"arguments\":[{payloadJson}]}}{RecordSeparator}";

            await SendFrameAsync(frame, _connectionCts.Token);

            try
            {
                return await WaitWithTimeout(completion.Task, InvocationTimeoutMs, $"{target} timed out.");
            }
            finally
            {
                lock (_pendingLock)
                {
                    _pendingInvocations.Remove(invocationId);
                }
            }
        }

        private async Task SendFrameAsync(string frame, CancellationToken cancellationToken)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("SignalR socket is not connected.");
            }

            var bytes = Encoding.UTF8.GetBytes(frame);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            var frameBuffer = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var messageBuilder = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await HandleSocketClosedAsync("SignalR server closed the connection.");
                            return;
                        }

                        messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    frameBuffer.Append(messageBuilder);
                    ProcessSignalRFrames(frameBuffer);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during disconnect.
            }
            catch (Exception ex)
            {
                await HandleSocketClosedAsync($"SignalR receive loop failed: {ex.Message}");
            }
        }

        private async Task PingLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await Task.Delay(PingIntervalMs, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await SendFrameAsync("{\"type\":6}" + RecordSeparator, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during disconnect.
            }
            catch (Exception ex)
            {
                await HandleSocketClosedAsync($"SignalR ping failed: {ex.Message}");
            }
        }

        private void ProcessSignalRFrames(StringBuilder frameBuffer)
        {
            var bufferedText = frameBuffer.ToString();
            var lastSeparator = bufferedText.LastIndexOf(RecordSeparator);
            if (lastSeparator < 0)
            {
                return;
            }

            var completedFrames = bufferedText.Substring(0, lastSeparator + 1);
            frameBuffer.Clear();
            frameBuffer.Append(bufferedText.Substring(lastSeparator + 1));

            var frames = completedFrames.Split(RecordSeparator);
            foreach (var rawFrame in frames)
            {
                var frame = rawFrame.Trim();
                if (string.IsNullOrEmpty(frame))
                {
                    continue;
                }

                ProcessSignalRMessage(frame);
            }
        }

        private void ProcessSignalRMessage(string frame)
        {
            if (frame == "{}")
            {
                _handshakeCompletionSource?.TrySetResult(true);
                return;
            }

            if (!TryExtractIntProperty(frame, "type", out var messageType))
            {
                return;
            }

            switch (messageType)
            {
                case 1:
                    ProcessInvocationMessage(frame);
                    break;
                case 3:
                    ProcessCompletionMessage(frame);
                    break;
                case 6:
                    break;
                case 7:
                    var closeMessage = TryExtractStringProperty(frame, "error", out var error)
                        ? $"SignalR closed: {error}"
                        : "SignalR connection closed by server.";
                    _ = HandleSocketClosedAsync(closeMessage);
                    break;
            }
        }

        private void ProcessInvocationMessage(string frame)
        {
            if (!TryExtractStringProperty(frame, "target", out var target) ||
                !string.Equals(target, MatchSnapshotTarget, StringComparison.Ordinal))
            {
                return;
            }

            if (!TryExtractFirstArrayElementJson(frame, "arguments", out var snapshotJson))
            {
                return;
            }

            var snapshot = JsonUtility.FromJson<MatchSnapshot>(snapshotJson);
            if (snapshot == null)
            {
                return;
            }

            EnqueueOnMainThread(() => ProcessSnapshot(snapshot));
        }

        private void ProcessCompletionMessage(string frame)
        {
            if (!TryExtractStringProperty(frame, "invocationId", out var invocationId))
            {
                return;
            }

            TaskCompletionSource<MatchSnapshot> completion = null;
            lock (_pendingLock)
            {
                _pendingInvocations.TryGetValue(invocationId, out completion);
            }

            if (completion == null)
            {
                return;
            }

            if (TryExtractStringProperty(frame, "error", out var error))
            {
                completion.TrySetException(new InvalidOperationException(error));
                return;
            }

            if (!TryExtractJsonProperty(frame, "result", out var resultJson))
            {
                completion.TrySetResult(null);
                return;
            }

            var snapshot = JsonUtility.FromJson<MatchSnapshot>(resultJson);
            completion.TrySetResult(snapshot);
        }

        private async Task HandleSocketClosedAsync(string reason)
        {
            if (_usingHttpFallback)
            {
                return;
            }

            GameLogger.Warning("SignalR", reason);
            EnqueueOnMainThread(() => ConnectionLost?.Invoke());
            EnqueueOnMainThread(() => ErrorOccurred?.Invoke(reason));

            try
            {
                await StartHttpFallbackAsync();
            }
            catch (Exception ex)
            {
                GameLogger.Error("SignalR", $"HTTP fallback failed after disconnect: {ex.Message}");
                EnqueueOnMainThread(() => ErrorOccurred?.Invoke($"HTTP fallback failed: {ex.Message}"));
            }
        }

        private async Task StartHttpFallbackAsync()
        {
            _usingHttpFallback = true;
            await StopSignalRAsync();

            var coordinatorGo = new GameObject($"HttpCoordinator_{matchId}");
            DontDestroyOnLoad(coordinatorGo);
            _httpCoordinator = coordinatorGo.AddComponent<MatchHttpCoordinator>();
            _httpCoordinator.SnapshotChanged += OnHttpSnapshotChanged;
            _httpCoordinator.ErrorOccurred += OnHttpError;
            _httpCoordinator.Initialize(matchId, playerId, seatIndex);
        }

        private async Task StopHttpFallbackAsync()
        {
            if (_httpCoordinator == null)
            {
                return;
            }

            _httpCoordinator.SnapshotChanged -= OnHttpSnapshotChanged;
            _httpCoordinator.ErrorOccurred -= OnHttpError;
            _httpCoordinator.StopPolling();
            Destroy(_httpCoordinator.gameObject);
            _httpCoordinator = null;
            _usingHttpFallback = false;
            await Task.CompletedTask;
        }

        private async Task StopSignalRAsync()
        {
            CancelPendingInvocations();

            if (_connectionCts != null)
            {
                _connectionCts.Cancel();
                _connectionCts.Dispose();
                _connectionCts = null;
            }

            var socket = _webSocket;
            _webSocket = null;
            _handshakeCompletionSource = null;

            if (socket != null)
            {
                try
                {
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
                    }
                }
                catch
                {
                    // Best effort only.
                }
                finally
                {
                    socket.Dispose();
                }
            }

            _receiveLoopTask = null;
            _pingLoopTask = null;
        }

        private void CancelPendingInvocations()
        {
            lock (_pendingLock)
            {
                foreach (var pending in _pendingInvocations.Values)
                {
                    pending.TrySetCanceled();
                }

                _pendingInvocations.Clear();
            }
        }

        private void ProcessSnapshot(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _currentSnapshot = snapshot;

            if (snapshot.localSeatIndex >= 0)
            {
                seatIndex = snapshot.localSeatIndex;
            }

            GameLogger.Info("SignalR", $"Snapshot received: phase={snapshot.phase}, turn={snapshot.turnNumber}, localSeat={seatIndex}");

            var duelSnapshot = SnapshotConverter.Convert(snapshot, seatIndex);
            if (duelSnapshot != null)
            {
                var json = JsonUtility.ToJson(duelSnapshot);
                BattleSnapshotBus.Publish(json);
            }

            SnapshotChanged?.Invoke(snapshot);
        }

        private void OnHttpSnapshotChanged(MatchSnapshot snapshot)
        {
            ProcessSnapshot(snapshot);
        }

        private void OnHttpError(string message)
        {
            GameLogger.Error("SignalR", $"HTTP fallback error: {message}");
            ErrorOccurred?.Invoke(message);
        }

        private void EnsureSignalRConnected()
        {
            if (_usingHttpFallback)
            {
                if (_httpCoordinator == null)
                {
                    throw new InvalidOperationException("HTTP fallback coordinator not available.");
                }

                return;
            }

            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("Not connected to SignalR match.");
            }
        }

        private void ReportActionError(string actionName, Exception ex)
        {
            GameLogger.Error("SignalR", $"{actionName} failed: {ex.Message}");
            ErrorOccurred?.Invoke($"{actionName} error: {ex.Message}");
        }

        private void EnqueueOnMainThread(Action action)
        {
            lock (_mainThreadLock)
            {
                _mainThreadQueue.Enqueue(action);
            }
        }

        private void FlushMainThreadQueue()
        {
            while (true)
            {
                Action action = null;
                lock (_mainThreadLock)
                {
                    if (_mainThreadQueue.Count > 0)
                    {
                        action = _mainThreadQueue.Dequeue();
                    }
                }

                if (action == null)
                {
                    break;
                }

                action.Invoke();
            }
        }

        private static async Task<T> WaitWithTimeout<T>(Task<T> task, int timeoutMs, string timeoutMessage)
        {
            var timeoutTask = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(task, timeoutTask);
            if (completed == timeoutTask)
            {
                throw new TimeoutException(timeoutMessage);
            }

            return await task;
        }

        private static Uri BuildHubUri(string baseUrl, string matchId, string token)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("API base URL is not configured.");
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Authentication token is missing.");
            }

            var uriBuilder = new UriBuilder(baseUrl)
            {
                Scheme = baseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
                Path = CombinePath(new Uri(baseUrl).AbsolutePath, "hubs/match"),
                Query = $"matchId={Uri.EscapeDataString(matchId)}&access_token={Uri.EscapeDataString(token)}"
            };

            return uriBuilder.Uri;
        }

        private static string CombinePath(string basePath, string relativePath)
        {
            var trimmedBase = (basePath ?? string.Empty).TrimEnd('/');
            var trimmedRelative = relativePath.TrimStart('/');
            return string.IsNullOrEmpty(trimmedBase)
                ? "/" + trimmedRelative
                : $"{trimmedBase}/{trimmedRelative}";
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static bool TryExtractIntProperty(string json, string propertyName, out int value)
        {
            value = 0;
            if (!TryExtractJsonProperty(json, propertyName, out var rawValue))
            {
                return false;
            }

            return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryExtractStringProperty(string json, string propertyName, out string value)
        {
            value = null;
            if (!TryExtractJsonProperty(json, propertyName, out var rawValue))
            {
                return false;
            }

            if (rawValue.Length < 2 || rawValue[0] != '"' || rawValue[^1] != '"')
            {
                return false;
            }

            value = UnescapeJsonString(rawValue.Substring(1, rawValue.Length - 2));
            return true;
        }

        private static bool TryExtractFirstArrayElementJson(string json, string propertyName, out string elementJson)
        {
            elementJson = null;
            if (!TryExtractJsonProperty(json, propertyName, out var arrayJson) ||
                string.IsNullOrEmpty(arrayJson) ||
                arrayJson[0] != '[')
            {
                return false;
            }

            var startIndex = 1;
            while (startIndex < arrayJson.Length && char.IsWhiteSpace(arrayJson[startIndex]))
            {
                startIndex++;
            }

            if (startIndex >= arrayJson.Length || arrayJson[startIndex] == ']')
            {
                return false;
            }

            return TrySliceJsonValue(arrayJson, startIndex, out elementJson, out _);
        }

        private static bool TryExtractJsonProperty(string json, string propertyName, out string valueJson)
        {
            valueJson = null;
            var search = $"\"{propertyName}\"";
            var propertyIndex = json.IndexOf(search, StringComparison.Ordinal);
            if (propertyIndex < 0)
            {
                return false;
            }

            var colonIndex = json.IndexOf(':', propertyIndex + search.Length);
            if (colonIndex < 0)
            {
                return false;
            }

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
            {
                valueStart++;
            }

            return TrySliceJsonValue(json, valueStart, out valueJson, out _);
        }

        private static bool TrySliceJsonValue(string json, int startIndex, out string valueJson, out int nextIndex)
        {
            valueJson = null;
            nextIndex = startIndex;
            if (startIndex < 0 || startIndex >= json.Length)
            {
                return false;
            }

            var firstChar = json[startIndex];
            if (firstChar == '"' && TryReadJsonString(json, startIndex, out valueJson, out nextIndex))
            {
                return true;
            }

            if (firstChar == '{' || firstChar == '[')
            {
                var depth = 0;
                var inString = false;
                var escaping = false;
                for (var i = startIndex; i < json.Length; i++)
                {
                    var current = json[i];
                    if (inString)
                    {
                        if (escaping)
                        {
                            escaping = false;
                            continue;
                        }

                        if (current == '\\')
                        {
                            escaping = true;
                        }
                        else if (current == '"')
                        {
                            inString = false;
                        }

                        continue;
                    }

                    if (current == '"')
                    {
                        inString = true;
                        continue;
                    }

                    if (current == '{' || current == '[')
                    {
                        depth++;
                    }
                    else if (current == '}' || current == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            valueJson = json.Substring(startIndex, i - startIndex + 1);
                            nextIndex = i + 1;
                            return true;
                        }
                    }
                }

                return false;
            }

            var endIndex = startIndex;
            while (endIndex < json.Length &&
                   json[endIndex] != ',' &&
                   json[endIndex] != '}' &&
                   json[endIndex] != ']')
            {
                endIndex++;
            }

            valueJson = json.Substring(startIndex, endIndex - startIndex).Trim();
            nextIndex = endIndex;
            return !string.IsNullOrEmpty(valueJson);
        }

        private static bool TryReadJsonString(string json, int startIndex, out string valueJson, out int nextIndex)
        {
            valueJson = null;
            nextIndex = startIndex;

            var escaping = false;
            for (var i = startIndex + 1; i < json.Length; i++)
            {
                var current = json[i];
                if (escaping)
                {
                    escaping = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (current == '"')
                {
                    valueJson = json.Substring(startIndex, i - startIndex + 1);
                    nextIndex = i + 1;
                    return true;
                }
            }

            return false;
        }

        private static string UnescapeJsonString(string value)
        {
            return value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\/", "/")
                .Replace("\\b", "\b")
                .Replace("\\f", "\f")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
        }
    }
}
