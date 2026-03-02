using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VibeReal.Core;
using VibeReal.Data;

namespace VibeReal.UI
{
    /// <summary>
    /// Controls the main session panel UI.
    /// Displays the focused session's conversation and status.
    /// </summary>
    public class SessionPanelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private VoiceManager voiceManager;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI sessionNameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image statusIndicator;
        [SerializeField] private TextMeshProUGUI currentTaskText;
        [SerializeField] private ScrollRect conversationScrollRect;
        [SerializeField] private RectTransform conversationContent;
        [SerializeField] private TextMeshProUGUI conversationText;
        [SerializeField] private GameObject pttIndicator;
        [SerializeField] private TextMeshProUGUI transcriptText;

        [Header("Status Colors")]
        [SerializeField] private Color idleColor = Color.green;
        [SerializeField] private Color thinkingColor = Color.yellow;
        [SerializeField] private Color executingColor = Color.cyan;
        [SerializeField] private Color waitingColor = new Color(1f, 0.5f, 0f); // Orange
        [SerializeField] private Color errorColor = Color.red;
        [SerializeField] private Color disconnectedColor = Color.gray;

        [Header("Settings")]
        [SerializeField] private int maxDisplayedMessages = 20;

        private Session _currentSession;
        private List<string> _displayedMessages = new List<string>();

        private void Start()
        {
            if (sessionManager == null)
                sessionManager = FindObjectOfType<SessionManager>();
            if (voiceManager == null)
                voiceManager = FindObjectOfType<VoiceManager>();

            // Subscribe to events
            if (sessionManager != null)
            {
                sessionManager.OnFocusedSessionChanged += HandleFocusedSessionChanged;
                sessionManager.OnSessionStatusChanged += HandleSessionStatusChanged;
                sessionManager.OnClaudeResponse += HandleClaudeResponse;
            }

            if (voiceManager != null)
            {
                voiceManager.OnStateChanged += HandleVoiceStateChanged;
                voiceManager.OnTranscriptReceived += HandleTranscriptReceived;
            }

            // Initial state
            if (pttIndicator != null)
                pttIndicator.SetActive(false);

            UpdateUI();
        }

        private void OnDestroy()
        {
            if (sessionManager != null)
            {
                sessionManager.OnFocusedSessionChanged -= HandleFocusedSessionChanged;
                sessionManager.OnSessionStatusChanged -= HandleSessionStatusChanged;
                sessionManager.OnClaudeResponse -= HandleClaudeResponse;
            }

            if (voiceManager != null)
            {
                voiceManager.OnStateChanged -= HandleVoiceStateChanged;
                voiceManager.OnTranscriptReceived -= HandleTranscriptReceived;
            }
        }

        /// <summary>
        /// Add a user message to the conversation display
        /// </summary>
        public void AddUserMessage(string message)
        {
            AddMessage("You", message);
        }

        /// <summary>
        /// Add a Claude message to the conversation display
        /// </summary>
        public void AddClaudeMessage(string message)
        {
            AddMessage("Claude", message);
        }

        /// <summary>
        /// Clear the conversation display
        /// </summary>
        public void ClearConversation()
        {
            _displayedMessages.Clear();
            UpdateConversationDisplay();
        }

        // ==================== Event Handlers ====================

        private void HandleFocusedSessionChanged(Session session)
        {
            _currentSession = session;
            ClearConversation();

            // Load recent history
            if (session != null)
            {
                foreach (var msg in session.recentHistory)
                {
                    var sender = msg.role == "user" ? "You" : "Claude";
                    AddMessage(sender, msg.content, false);
                }
            }

            UpdateUI();
        }

        private void HandleSessionStatusChanged(Session session)
        {
            if (session == _currentSession)
            {
                UpdateUI();
            }
        }

        private void HandleClaudeResponse(Session session, string content)
        {
            if (session == _currentSession)
            {
                AddClaudeMessage(content);
            }
        }

        private void HandleVoiceStateChanged(VoiceManager.VoiceState state)
        {
            if (pttIndicator != null)
            {
                pttIndicator.SetActive(state == VoiceManager.VoiceState.Listening);
            }

            if (transcriptText != null)
            {
                transcriptText.gameObject.SetActive(
                    state == VoiceManager.VoiceState.Listening ||
                    state == VoiceManager.VoiceState.Processing);
            }
        }

        private void HandleTranscriptReceived(string transcript)
        {
            if (transcriptText != null)
            {
                transcriptText.text = transcript;
            }

            // Add user message when processing complete
            if (voiceManager != null && voiceManager.State == VoiceManager.VoiceState.Processing)
            {
                AddUserMessage(transcript);
            }
        }

        // ==================== UI Updates ====================

        private void UpdateUI()
        {
            if (_currentSession == null)
            {
                if (sessionNameText != null)
                    sessionNameText.text = "No Session";
                if (statusText != null)
                    statusText.text = "Disconnected";
                if (statusIndicator != null)
                    statusIndicator.color = disconnectedColor;
                if (currentTaskText != null)
                    currentTaskText.text = "";
                return;
            }

            // Session name
            if (sessionNameText != null)
            {
                sessionNameText.text = _currentSession.name;
            }

            // Status text and color
            var (statusString, statusColor) = GetStatusDisplay(_currentSession.status);

            if (statusText != null)
            {
                statusText.text = statusString;
            }

            if (statusIndicator != null)
            {
                statusIndicator.color = statusColor;
            }

            // Current task
            if (currentTaskText != null)
            {
                currentTaskText.text = _currentSession.currentTask ?? "";
                currentTaskText.gameObject.SetActive(!string.IsNullOrEmpty(_currentSession.currentTask));
            }
        }

        private (string, Color) GetStatusDisplay(SessionStatus status)
        {
            return status switch
            {
                SessionStatus.Idle => ("Idle", idleColor),
                SessionStatus.Thinking => ("Thinking...", thinkingColor),
                SessionStatus.Executing => ("Executing", executingColor),
                SessionStatus.WaitingInput => ("Waiting for Input", waitingColor),
                SessionStatus.Error => ("Error", errorColor),
                SessionStatus.Disconnected => ("Disconnected", disconnectedColor),
                _ => ("Unknown", disconnectedColor)
            };
        }

        private void AddMessage(string sender, string content, bool scroll = true)
        {
            var formatted = $"<b>{sender}:</b> {content}\n";
            _displayedMessages.Add(formatted);

            // Trim old messages
            while (_displayedMessages.Count > maxDisplayedMessages)
            {
                _displayedMessages.RemoveAt(0);
            }

            UpdateConversationDisplay();

            if (scroll)
            {
                ScrollToBottom();
            }
        }

        private void UpdateConversationDisplay()
        {
            if (conversationText != null)
            {
                conversationText.text = string.Join("\n", _displayedMessages);
            }
        }

        private void ScrollToBottom()
        {
            if (conversationScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                conversationScrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
