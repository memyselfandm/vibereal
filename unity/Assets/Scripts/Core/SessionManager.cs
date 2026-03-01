using System;
using System.Collections.Generic;
using UnityEngine;
using VibeReal.Data;

namespace VibeReal.Core
{
    /// <summary>
    /// Tracks Claude sessions received from the hub.
    /// MVP scope: maintain session list, track focused session, update state.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public event Action<SessionData> OnSessionAdded;
        public event Action<SessionData> OnSessionRemoved;
        public event Action<SessionData> OnSessionUpdated;
        public event Action<SessionData> OnFocusChanged;
        public event Action<string, ConversationMessage> OnNewMessage;

        private readonly Dictionary<string, SessionData> _sessions = new();
        private string _focusedSessionId;

        public IReadOnlyDictionary<string, SessionData> Sessions => _sessions;

        public SessionData FocusedSession =>
            _focusedSessionId != null && _sessions.TryGetValue(_focusedSessionId, out var s) ? s : null;

        [SerializeField] private WebSocketClient wsClient;

        private void OnEnable()
        {
            if (wsClient == null)
                wsClient = FindFirstObjectByType<WebSocketClient>();

            wsClient.OnSessionList += HandleSessionList;
            wsClient.OnSessionUpdate += HandleSessionUpdate;
            wsClient.OnClaudeResponse += HandleClaudeResponse;
            wsClient.OnConversationHistory += HandleConversationHistory;
        }

        private void OnDisable()
        {
            if (wsClient != null)
            {
                wsClient.OnSessionList -= HandleSessionList;
                wsClient.OnSessionUpdate -= HandleSessionUpdate;
                wsClient.OnClaudeResponse -= HandleClaudeResponse;
                wsClient.OnConversationHistory -= HandleConversationHistory;
            }
        }

        public void SetFocus(string sessionId)
        {
            if (!_sessions.ContainsKey(sessionId)) return;
            _focusedSessionId = sessionId;
            OnFocusChanged?.Invoke(_sessions[sessionId]);
        }

        public SessionData GetSession(string sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var s) ? s : null;
        }

        // --- Hub message handlers ---

        private void HandleSessionList(SessionListMessage msg)
        {
            // Clear and rebuild
            var existingIds = new HashSet<string>(_sessions.Keys);

            foreach (var session in msg.sessions)
            {
                if (_sessions.ContainsKey(session.id))
                {
                    _sessions[session.id] = session;
                    OnSessionUpdated?.Invoke(session);
                }
                else
                {
                    _sessions[session.id] = session;
                    OnSessionAdded?.Invoke(session);
                }
                existingIds.Remove(session.id);
            }

            // Remove sessions no longer present
            foreach (var removedId in existingIds)
            {
                var removed = _sessions[removedId];
                _sessions.Remove(removedId);
                OnSessionRemoved?.Invoke(removed);
            }

            // Auto-focus first session if none focused
            if (_focusedSessionId == null && _sessions.Count > 0)
            {
                foreach (var kv in _sessions)
                {
                    SetFocus(kv.Key);
                    break;
                }
            }
        }

        private void HandleSessionUpdate(SessionUpdateMessage msg)
        {
            if (!_sessions.TryGetValue(msg.sessionId, out var session)) return;

            session.status = msg.status;
            session.currentTask = msg.currentTask;
            session.lastActivity = DateTime.UtcNow.ToString("o");
            OnSessionUpdated?.Invoke(session);
        }

        private void HandleClaudeResponse(ClaudeResponseMessage msg)
        {
            if (!_sessions.TryGetValue(msg.sessionId, out var session)) return;

            var message = new ConversationMessage
            {
                role = "assistant",
                content = msg.content
            };
            session.conversation.Add(message);
            OnNewMessage?.Invoke(msg.sessionId, message);
        }

        private void HandleConversationHistory(ConversationHistoryMessage msg)
        {
            if (!_sessions.TryGetValue(msg.sessionId, out var session)) return;

            session.conversation.Clear();
            session.conversation.AddRange(msg.messages);
            OnSessionUpdated?.Invoke(session);
        }
    }
}
