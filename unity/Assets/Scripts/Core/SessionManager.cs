using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VibeReal.Data;

namespace VibeReal.Core
{
    /// <summary>
    /// Manages Claude session state received from the Session Hub.
    /// Tracks focused session and pending approvals.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WebSocketClient webSocketClient;

        // Events
        public event Action<List<Session>> OnSessionsUpdated;
        public event Action<Session> OnSessionAdded;
        public event Action<Session> OnSessionRemoved;
        public event Action<Session> OnSessionStatusChanged;
        public event Action<Session> OnFocusedSessionChanged;
        public event Action<Session, Approval> OnApprovalRequired;
        public event Action<Session, string> OnClaudeResponse;

        // State
        public IReadOnlyList<Session> Sessions => _sessions;
        public Session FocusedSession { get; private set; }
        public bool HasPendingApprovals => _sessions.Any(s => s.pendingApprovals.Count > 0);

        private List<Session> _sessions = new List<Session>();
        private Dictionary<string, Session> _sessionLookup = new Dictionary<string, Session>();

        private void Start()
        {
            if (webSocketClient == null)
            {
                webSocketClient = FindObjectOfType<WebSocketClient>();
            }

            if (webSocketClient != null)
            {
                webSocketClient.OnSessionList += HandleSessionList;
                webSocketClient.OnSessionUpdate += HandleSessionUpdate;
                webSocketClient.OnClaudeResponse += HandleClaudeResponse;
                webSocketClient.OnNotification += HandleNotification;
                webSocketClient.OnConnected += HandleConnected;
            }
        }

        private void OnDestroy()
        {
            if (webSocketClient != null)
            {
                webSocketClient.OnSessionList -= HandleSessionList;
                webSocketClient.OnSessionUpdate -= HandleSessionUpdate;
                webSocketClient.OnClaudeResponse -= HandleClaudeResponse;
                webSocketClient.OnNotification -= HandleNotification;
                webSocketClient.OnConnected -= HandleConnected;
            }
        }

        /// <summary>
        /// Set the focused session for voice commands
        /// </summary>
        public void SetFocusedSession(string sessionId)
        {
            if (_sessionLookup.TryGetValue(sessionId, out var session))
            {
                SetFocusedSession(session);
            }
            else
            {
                Debug.LogWarning($"Session not found: {sessionId}");
            }
        }

        /// <summary>
        /// Set the focused session
        /// </summary>
        public void SetFocusedSession(Session session)
        {
            if (FocusedSession != session)
            {
                FocusedSession = session;
                Debug.Log($"Focused session: {session?.name ?? "none"}");
                OnFocusedSessionChanged?.Invoke(session);

                // Notify hub
                if (session != null && webSocketClient != null)
                {
                    webSocketClient.SetFocusedSession(session.id);
                }
            }
        }

        /// <summary>
        /// Get session by ID
        /// </summary>
        public Session GetSession(string sessionId)
        {
            _sessionLookup.TryGetValue(sessionId, out var session);
            return session;
        }

        /// <summary>
        /// Find session by name (fuzzy match for voice commands)
        /// </summary>
        public Session FindSessionByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            name = name.ToLowerInvariant().Trim();

            // Exact match first
            var exact = _sessions.FirstOrDefault(s =>
                s.name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // Contains match
            var contains = _sessions.FirstOrDefault(s =>
                s.name.ToLowerInvariant().Contains(name));
            if (contains != null) return contains;

            // Number match ("container 1", "1", "first")
            var numberMatch = ExtractNumber(name);
            if (numberMatch > 0 && numberMatch <= _sessions.Count)
            {
                return _sessions[numberMatch - 1];
            }

            return null;
        }

        /// <summary>
        /// Approve a pending action
        /// </summary>
        public void Approve(string sessionId, string approvalId)
        {
            webSocketClient?.SendApprovalDecision(sessionId, approvalId, true);
            RemoveApproval(sessionId, approvalId);
        }

        /// <summary>
        /// Deny a pending action
        /// </summary>
        public void Deny(string sessionId, string approvalId)
        {
            webSocketClient?.SendApprovalDecision(sessionId, approvalId, false);
            RemoveApproval(sessionId, approvalId);
        }

        /// <summary>
        /// Get summary of all session statuses (for voice readout)
        /// </summary>
        public string GetStatusSummary()
        {
            if (_sessions.Count == 0)
            {
                return "No sessions connected.";
            }

            var summaries = new List<string>();

            foreach (var session in _sessions)
            {
                var status = session.status switch
                {
                    SessionStatus.Idle => "is idle",
                    SessionStatus.Thinking => "is thinking",
                    SessionStatus.Executing => "is executing",
                    SessionStatus.WaitingInput => "is waiting for input",
                    SessionStatus.Error => "has an error",
                    SessionStatus.Disconnected => "is disconnected",
                    _ => "status unknown"
                };

                var summary = $"{session.name} {status}";

                if (!string.IsNullOrEmpty(session.currentTask))
                {
                    summary += $": {session.currentTask}";
                }

                if (session.pendingApprovals.Count > 0)
                {
                    summary += $". Has {session.pendingApprovals.Count} pending approval.";
                }

                summaries.Add(summary);
            }

            return string.Join(". ", summaries);
        }

        // ==================== Event Handlers ====================

        private void HandleConnected()
        {
            // Request session list on connect
            webSocketClient.RequestSessionList();
        }

        private void HandleSessionList(SessionListMessage message)
        {
            var newSessions = new List<Session>();
            var newLookup = new Dictionary<string, Session>();

            foreach (var info in message.sessions)
            {
                var session = ConvertSessionInfo(info);

                // Preserve existing state if we have it
                if (_sessionLookup.TryGetValue(info.id, out var existing))
                {
                    session.pendingApprovals = existing.pendingApprovals;
                    session.recentHistory = existing.recentHistory;
                }

                newSessions.Add(session);
                newLookup[session.id] = session;
            }

            // Check for removed sessions
            foreach (var oldSession in _sessions)
            {
                if (!newLookup.ContainsKey(oldSession.id))
                {
                    OnSessionRemoved?.Invoke(oldSession);
                }
            }

            // Check for added sessions
            foreach (var newSession in newSessions)
            {
                if (!_sessionLookup.ContainsKey(newSession.id))
                {
                    OnSessionAdded?.Invoke(newSession);
                }
            }

            _sessions = newSessions;
            _sessionLookup = newLookup;

            // Auto-focus first session if none focused
            if (FocusedSession == null && _sessions.Count > 0)
            {
                SetFocusedSession(_sessions[0]);
            }
            else if (FocusedSession != null && !_sessionLookup.ContainsKey(FocusedSession.id))
            {
                // Focused session was removed
                SetFocusedSession(_sessions.Count > 0 ? _sessions[0] : null);
            }

            OnSessionsUpdated?.Invoke(_sessions);
        }

        private void HandleSessionUpdate(SessionUpdateMessage message)
        {
            if (_sessionLookup.TryGetValue(message.sessionId, out var session))
            {
                var oldStatus = session.status;

                session.status = ParseStatus(message.status);
                session.currentTask = message.currentTask;
                session.lastActivity = DateTime.UtcNow;

                if (oldStatus != session.status)
                {
                    OnSessionStatusChanged?.Invoke(session);
                }
            }
        }

        private void HandleClaudeResponse(ClaudeResponseMessage message)
        {
            if (_sessionLookup.TryGetValue(message.sessionId, out var session))
            {
                // Add to history
                if (message.isComplete)
                {
                    session.recentHistory.Add(new ConversationMessage
                    {
                        role = "assistant",
                        content = message.content,
                        timestamp = DateTime.UtcNow,
                        isComplete = true
                    });

                    // Trim history
                    while (session.recentHistory.Count > 20)
                    {
                        session.recentHistory.RemoveAt(0);
                    }
                }

                OnClaudeResponse?.Invoke(session, message.content);
            }
        }

        private void HandleNotification(NotificationMessage message)
        {
            var notification = message.notification;

            if (notification.type == "approval_required")
            {
                if (_sessionLookup.TryGetValue(notification.sessionId, out var session))
                {
                    var approval = new Approval
                    {
                        id = notification.id,
                        toolName = "Unknown", // Could parse from body
                        description = notification.body,
                        timestamp = DateTime.UtcNow
                    };

                    session.pendingApprovals.Add(approval);
                    OnApprovalRequired?.Invoke(session, approval);
                }
            }
        }

        // ==================== Helpers ====================

        private void RemoveApproval(string sessionId, string approvalId)
        {
            if (_sessionLookup.TryGetValue(sessionId, out var session))
            {
                session.pendingApprovals.RemoveAll(a => a.id == approvalId);
            }
        }

        private Session ConvertSessionInfo(SessionInfo info)
        {
            return new Session
            {
                id = info.id,
                name = info.name,
                type = ParseType(info.type),
                status = ParseStatus(info.status),
                currentTask = info.currentTask,
                lastActivity = DateTime.TryParse(info.lastActivity, out var dt) ? dt : DateTime.UtcNow
            };
        }

        private SessionType ParseType(string type)
        {
            return type?.ToLowerInvariant() switch
            {
                "container" => SessionType.Container,
                "laptop" => SessionType.Laptop,
                "remote" => SessionType.Remote,
                _ => SessionType.Container
            };
        }

        private SessionStatus ParseStatus(string status)
        {
            return status?.ToLowerInvariant() switch
            {
                "idle" => SessionStatus.Idle,
                "thinking" => SessionStatus.Thinking,
                "executing" => SessionStatus.Executing,
                "waiting_input" => SessionStatus.WaitingInput,
                "error" => SessionStatus.Error,
                "disconnected" => SessionStatus.Disconnected,
                _ => SessionStatus.Idle
            };
        }

        private int ExtractNumber(string text)
        {
            // "container 1" -> 1
            // "first" -> 1
            // "second" -> 2

            var ordinals = new Dictionary<string, int>
            {
                {"first", 1}, {"second", 2}, {"third", 3},
                {"fourth", 4}, {"fifth", 5}, {"one", 1},
                {"two", 2}, {"three", 3}, {"four", 4}, {"five", 5}
            };

            foreach (var kvp in ordinals)
            {
                if (text.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            // Try to extract digit
            foreach (var word in text.Split(' '))
            {
                if (int.TryParse(word, out int num))
                {
                    return num;
                }
            }

            return 0;
        }
    }
}
