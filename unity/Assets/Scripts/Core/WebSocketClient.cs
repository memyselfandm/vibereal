using System;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;
using VibeReal.Data;

namespace VibeReal.Core
{
    public enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting }

    /// <summary>
    /// WebSocket client using NativeWebSocket (github.com/endel/NativeWebSocket).
    /// Connects to the Session Hub, dispatches typed events for each message type.
    /// </summary>
    public class WebSocketClient : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] private string hubUrl = "ws://localhost:8080";
        [SerializeField] private float reconnectDelay = 3f;
        [SerializeField] private int maxReconnectAttempts = 5;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        // Events
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnRawMessageReceived;
        public event Action<string> OnError;

        // Parsed message events (dispatched by type)
        public event Action<SessionListMessage> OnSessionList;
        public event Action<SessionUpdateMessage> OnSessionUpdate;
        public event Action<ClaudeResponseMessage> OnClaudeResponse;
        public event Action<NotificationMessage> OnNotification;
        public event Action<CommandAckMessage> OnCommandAck;
        public event Action<ConversationHistoryMessage> OnConversationHistory;

        private NativeWebSocket.WebSocket _ws;
        private int _reconnectCount;
        private int _requestCounter;

        public string NextRequestId() => $"req-{++_requestCounter}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        // --- Lifecycle ---

        private void Start()
        {
            Connect();
        }

        private void Update()
        {
            // NativeWebSocket requires dispatching messages on the main thread
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif
        }

        private async void OnDestroy()
        {
            await Disconnect();
        }

        private void OnApplicationQuit()
        {
            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                _ws.Close().Wait(1000);
            }
        }

        // --- Public API ---

        public async void Connect()
        {
            if (State == ConnectionState.Connected || State == ConnectionState.Connecting)
                return;

            State = ConnectionState.Connecting;
            await ConnectAsync();
        }

        public async Task Disconnect()
        {
            if (_ws == null) return;

            if (_ws.State == WebSocketState.Open)
            {
                await _ws.Close();
            }
            _ws = null;
            State = ConnectionState.Disconnected;
            OnDisconnected?.Invoke();
        }

        public async void Send(string json)
        {
            if (State != ConnectionState.Connected || _ws == null)
            {
                Debug.LogWarning("[WebSocketClient] Cannot send - not connected");
                return;
            }

            await _ws.SendText(json);
        }

        // Typed send helpers

        public void SendMessage(string sessionId, string content)
        {
            var msg = new SendMessageRequest
            {
                requestId = NextRequestId(),
                sessionId = sessionId,
                message = new MessageContent { content = content }
            };
            Send(JsonUtility.ToJson(msg));
        }

        public void SendApproval(string sessionId, string approvalId, bool approved)
        {
            var msg = new ApprovalDecisionRequest
            {
                requestId = NextRequestId(),
                sessionId = sessionId,
                approvalId = approvalId,
                decision = approved ? "approve" : "deny"
            };
            Send(JsonUtility.ToJson(msg));
        }

        public void RequestSessionList()
        {
            var msg = new ListSessionsRequest { requestId = NextRequestId() };
            Send(JsonUtility.ToJson(msg));
        }

        public void SetFocusSession(string sessionId)
        {
            var msg = new SetFocusRequest
            {
                requestId = NextRequestId(),
                sessionId = sessionId
            };
            Send(JsonUtility.ToJson(msg));
        }

        // --- Connection ---

        private async Task ConnectAsync()
        {
            Debug.Log($"[WebSocketClient] Connecting to {hubUrl}...");

            _ws = new NativeWebSocket.WebSocket(hubUrl);

            _ws.OnOpen += () =>
            {
                Debug.Log("[WebSocketClient] Connected!");
                State = ConnectionState.Connected;
                _reconnectCount = 0;
                OnConnected?.Invoke();
            };

            _ws.OnMessage += (bytes) =>
            {
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                HandleMessage(json);
            };

            _ws.OnError += (error) =>
            {
                Debug.LogError($"[WebSocketClient] Error: {error}");
                OnError?.Invoke(error);
            };

            _ws.OnClose += (code) =>
            {
                Debug.Log($"[WebSocketClient] Closed with code {code}");
                State = ConnectionState.Disconnected;
                OnDisconnected?.Invoke();
                TryReconnect();
            };

            try
            {
                await _ws.Connect();
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocketClient] Connection failed: {e.Message}");
                OnError?.Invoke(e.Message);
                State = ConnectionState.Disconnected;
                TryReconnect();
            }
        }

        private async void TryReconnect()
        {
            if (_reconnectCount >= maxReconnectAttempts)
            {
                Debug.LogWarning("[WebSocketClient] Max reconnect attempts reached");
                return;
            }

            _reconnectCount++;
            State = ConnectionState.Reconnecting;
            Debug.Log($"[WebSocketClient] Reconnecting in {reconnectDelay}s (attempt {_reconnectCount}/{maxReconnectAttempts})...");

            await Task.Delay((int)(reconnectDelay * 1000));

            if (this != null && State == ConnectionState.Reconnecting)
            {
                await ConnectAsync();
            }
        }

        // --- Message dispatch ---

        private void HandleMessage(string json)
        {
            OnRawMessageReceived?.Invoke(json);

            try
            {
                var baseMsg = JsonUtility.FromJson<HubMessageBase>(json);

                switch (baseMsg.type)
                {
                    case "session_list":
                        OnSessionList?.Invoke(JsonUtility.FromJson<SessionListMessage>(json));
                        break;

                    case "session_update":
                        OnSessionUpdate?.Invoke(JsonUtility.FromJson<SessionUpdateMessage>(json));
                        break;

                    case "claude_response":
                        OnClaudeResponse?.Invoke(JsonUtility.FromJson<ClaudeResponseMessage>(json));
                        break;

                    case "notification":
                        OnNotification?.Invoke(JsonUtility.FromJson<NotificationMessage>(json));
                        break;

                    case "command_ack":
                        OnCommandAck?.Invoke(JsonUtility.FromJson<CommandAckMessage>(json));
                        break;

                    case "conversation_history":
                        OnConversationHistory?.Invoke(JsonUtility.FromJson<ConversationHistoryMessage>(json));
                        break;

                    default:
                        Debug.Log($"[WebSocketClient] Unknown message type: {baseMsg.type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocketClient] Failed to parse message: {e.Message}");
            }
        }
    }
}
