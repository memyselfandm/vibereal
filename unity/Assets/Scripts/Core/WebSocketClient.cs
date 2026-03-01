using System;
using System.Collections;
using UnityEngine;
using VibeReal.Data;

namespace VibeReal.Core
{
    public enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting }

    /// <summary>
    /// Minimal WebSocket client for MVP. Uses Unity's built-in networking
    /// via a coroutine-based approach. For production, swap in NativeWebSocket.
    ///
    /// MVP scope: connect, send JSON, receive JSON, basic reconnect.
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

        private WebSocket _ws;
        private int _reconnectCount;
        private int _requestCounter;

        public string NextRequestId() => $"req-{++_requestCounter}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        // --- Lifecycle ---

        private void Start()
        {
            Connect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        // --- Public API ---

        public void Connect()
        {
            if (State == ConnectionState.Connected || State == ConnectionState.Connecting)
                return;

            State = ConnectionState.Connecting;
            StartCoroutine(ConnectCoroutine());
        }

        public void Disconnect()
        {
            StopAllCoroutines();

            if (_ws != null)
            {
                _ws.Close();
                _ws = null;
            }

            State = ConnectionState.Disconnected;
            OnDisconnected?.Invoke();
        }

        public void Send(string json)
        {
            if (State != ConnectionState.Connected || _ws == null)
            {
                Debug.LogWarning("[WebSocketClient] Cannot send - not connected");
                return;
            }

            _ws.SendString(json);
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

        // --- Connection coroutine ---

        private IEnumerator ConnectCoroutine()
        {
            Debug.Log($"[WebSocketClient] Connecting to {hubUrl}...");

            _ws = new WebSocket(new Uri(hubUrl));
            yield return StartCoroutine(_ws.Connect());

            if (_ws.error != null)
            {
                Debug.LogError($"[WebSocketClient] Connection failed: {_ws.error}");
                OnError?.Invoke(_ws.error);
                State = ConnectionState.Disconnected;

                if (_reconnectCount < maxReconnectAttempts)
                {
                    _reconnectCount++;
                    State = ConnectionState.Reconnecting;
                    yield return new WaitForSeconds(reconnectDelay);
                    StartCoroutine(ConnectCoroutine());
                }
                yield break;
            }

            State = ConnectionState.Connected;
            _reconnectCount = 0;
            Debug.Log("[WebSocketClient] Connected!");
            OnConnected?.Invoke();

            // Message receive loop
            while (_ws != null && _ws.error == null)
            {
                string reply = _ws.RecvString();
                if (reply != null)
                {
                    HandleMessage(reply);
                }
                yield return null;
            }

            // Connection lost
            Debug.Log("[WebSocketClient] Connection lost");
            State = ConnectionState.Disconnected;
            OnDisconnected?.Invoke();

            if (_reconnectCount < maxReconnectAttempts)
            {
                _reconnectCount++;
                State = ConnectionState.Reconnecting;
                yield return new WaitForSeconds(reconnectDelay);
                StartCoroutine(ConnectCoroutine());
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
