using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;
using VibeReal.Data;

namespace VibeReal.Core
{
    /// <summary>
    /// WebSocket client for connecting to the Session Hub.
    /// Handles connection lifecycle, message sending/receiving, and auto-reconnection.
    /// </summary>
    public class WebSocketClient : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] private string hubUrl;
        [SerializeField] private string apiKey;

        [Header("Reconnection")]
        [SerializeField] private float reconnectDelay = 5f;
        [SerializeField] private float maxReconnectDelay = 60f;
        [SerializeField] private int maxReconnectAttempts = 10;

        // Events
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;
        public event Action<SessionListMessage> OnSessionList;
        public event Action<SessionUpdateMessage> OnSessionUpdate;
        public event Action<ClaudeResponseMessage> OnClaudeResponse;
        public event Action<NotificationMessage> OnNotification;
        public event Action<CommandAckMessage> OnCommandAck;
        public event Action<ErrorMessage> OnHubError;

        // State
        public bool IsConnected => _websocket?.State == WebSocketState.Open;
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        private WebSocket _websocket;
        private int _reconnectAttempts;
        private float _currentReconnectDelay;
        private Coroutine _reconnectCoroutine;
        private Queue<string> _messageQueue = new Queue<string>();

        public enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
            Reconnecting
        }

        private void Start()
        {
            // Load config
            var config = AppConfig.Load();
            if (string.IsNullOrEmpty(hubUrl))
                hubUrl = config.hubUrl;
            if (string.IsNullOrEmpty(apiKey))
                apiKey = config.apiKey;

            reconnectDelay = config.reconnectDelaySeconds;
            maxReconnectDelay = config.maxReconnectDelaySeconds;
            maxReconnectAttempts = config.maxReconnectAttempts;

            _currentReconnectDelay = reconnectDelay;
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _websocket?.DispatchMessageQueue();
#endif
        }

        private async void OnDestroy()
        {
            if (_websocket != null)
            {
                await _websocket.Close();
            }
        }

        /// <summary>
        /// Connect to the Session Hub
        /// </summary>
        public async void Connect()
        {
            if (State == ConnectionState.Connecting || State == ConnectionState.Connected)
            {
                Debug.LogWarning("WebSocket already connected or connecting");
                return;
            }

            State = ConnectionState.Connecting;
            Debug.Log($"Connecting to hub: {hubUrl}");

            try
            {
                var headers = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    headers.Add("Authorization", $"Bearer {apiKey}");
                }

                _websocket = new WebSocket(hubUrl, headers);

                _websocket.OnOpen += HandleOpen;
                _websocket.OnMessage += HandleMessage;
                _websocket.OnError += HandleError;
                _websocket.OnClose += HandleClose;

                await _websocket.Connect();
            }
            catch (Exception ex)
            {
                Debug.LogError($"WebSocket connection failed: {ex.Message}");
                State = ConnectionState.Disconnected;
                OnError?.Invoke(ex.Message);
                StartReconnect();
            }
        }

        /// <summary>
        /// Disconnect from the Session Hub
        /// </summary>
        public async void Disconnect()
        {
            StopReconnect();

            if (_websocket != null && _websocket.State == WebSocketState.Open)
            {
                await _websocket.Close();
            }

            State = ConnectionState.Disconnected;
        }

        /// <summary>
        /// Send a message to the hub
        /// </summary>
        public async void Send<T>(T message) where T : HubMessage
        {
            if (!IsConnected)
            {
                Debug.LogWarning("Cannot send message: not connected");
                // Queue message for later
                var json = JsonUtility.ToJson(message);
                _messageQueue.Enqueue(json);
                return;
            }

            try
            {
                var json = JsonUtility.ToJson(message);
                Debug.Log($"Sending: {json}");
                await _websocket.SendText(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send message: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Send a voice command to the hub
        /// </summary>
        public void SendVoiceCommand(string transcript, float confidence)
        {
            var message = new VoiceCommandMessage
            {
                transcript = transcript,
                confidence = confidence
            };
            Send(message);
        }

        /// <summary>
        /// Send an approval decision
        /// </summary>
        public void SendApprovalDecision(string sessionId, string approvalId, bool approved)
        {
            var message = new ApprovalDecisionMessage
            {
                sessionId = sessionId,
                approvalId = approvalId,
                decision = approved ? "approve" : "deny"
            };
            Send(message);
        }

        /// <summary>
        /// Request the list of sessions
        /// </summary>
        public void RequestSessionList()
        {
            Send(new ListSessionsMessage());
        }

        /// <summary>
        /// Set the focused session
        /// </summary>
        public void SetFocusedSession(string sessionId)
        {
            var message = new SetFocusMessage { sessionId = sessionId };
            Send(message);
        }

        // ==================== Event Handlers ====================

        private void HandleOpen()
        {
            Debug.Log("WebSocket connected");
            State = ConnectionState.Connected;
            _reconnectAttempts = 0;
            _currentReconnectDelay = reconnectDelay;

            // Send queued messages
            while (_messageQueue.Count > 0)
            {
                var json = _messageQueue.Dequeue();
                _websocket.SendText(json);
            }

            // Request initial session list
            RequestSessionList();

            OnConnected?.Invoke();
        }

        private void HandleMessage(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            Debug.Log($"Received: {json}");

            try
            {
                var messageType = HubMessageParser.GetMessageType(json);

                switch (messageType)
                {
                    case "session_list":
                        var sessionList = HubMessageParser.Parse<SessionListMessage>(json);
                        OnSessionList?.Invoke(sessionList);
                        break;

                    case "session_update":
                        var sessionUpdate = HubMessageParser.Parse<SessionUpdateMessage>(json);
                        OnSessionUpdate?.Invoke(sessionUpdate);
                        break;

                    case "claude_response":
                        var claudeResponse = HubMessageParser.Parse<ClaudeResponseMessage>(json);
                        OnClaudeResponse?.Invoke(claudeResponse);
                        break;

                    case "notification":
                        var notification = HubMessageParser.Parse<NotificationMessage>(json);
                        OnNotification?.Invoke(notification);
                        break;

                    case "command_ack":
                        var commandAck = HubMessageParser.Parse<CommandAckMessage>(json);
                        OnCommandAck?.Invoke(commandAck);
                        break;

                    case "error":
                        var error = HubMessageParser.Parse<ErrorMessage>(json);
                        OnHubError?.Invoke(error);
                        break;

                    default:
                        Debug.LogWarning($"Unknown message type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse message: {ex.Message}");
            }
        }

        private void HandleError(string error)
        {
            Debug.LogError($"WebSocket error: {error}");
            OnError?.Invoke(error);
        }

        private void HandleClose(WebSocketCloseCode closeCode)
        {
            Debug.Log($"WebSocket closed: {closeCode}");
            State = ConnectionState.Disconnected;
            OnDisconnected?.Invoke();

            // Auto-reconnect unless intentionally closed
            if (closeCode != WebSocketCloseCode.Normal)
            {
                StartReconnect();
            }
        }

        // ==================== Reconnection ====================

        private void StartReconnect()
        {
            if (_reconnectCoroutine != null || _reconnectAttempts >= maxReconnectAttempts)
            {
                if (_reconnectAttempts >= maxReconnectAttempts)
                {
                    Debug.LogError("Max reconnect attempts reached");
                    OnError?.Invoke("Max reconnect attempts reached");
                }
                return;
            }

            State = ConnectionState.Reconnecting;
            _reconnectCoroutine = StartCoroutine(ReconnectCoroutine());
        }

        private void StopReconnect()
        {
            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }
        }

        private IEnumerator ReconnectCoroutine()
        {
            _reconnectAttempts++;
            Debug.Log($"Reconnecting in {_currentReconnectDelay}s (attempt {_reconnectAttempts}/{maxReconnectAttempts})");

            yield return new WaitForSeconds(_currentReconnectDelay);

            // Exponential backoff
            _currentReconnectDelay = Mathf.Min(_currentReconnectDelay * 2, maxReconnectDelay);

            _reconnectCoroutine = null;
            Connect();
        }
    }
}
