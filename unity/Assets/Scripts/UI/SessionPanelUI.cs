using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VibeReal.Core;
using VibeReal.Data;

namespace VibeReal.UI
{
    /// <summary>
    /// Displays a single Claude session: header with status, scrollable
    /// conversation, and an approval overlay when actions need approval.
    ///
    /// MVP scope: one panel, text-only conversation, basic approval UI.
    /// Attach to a Canvas with the required child objects wired up in the inspector.
    /// </summary>
    public class SessionPanelUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private NotificationManager notificationManager;
        [SerializeField] private WebSocketClient wsClient;

        [Header("Header")]
        [SerializeField] private Image statusDot;
        [SerializeField] private TMP_Text sessionNameText;
        [SerializeField] private TMP_Text statusLabelText;

        [Header("Conversation")]
        [SerializeField] private TMP_Text conversationText;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Input")]
        [SerializeField] private TMP_InputField messageInput;
        [SerializeField] private Button sendButton;

        [Header("Approval Overlay")]
        [SerializeField] private GameObject approvalOverlay;
        [SerializeField] private TMP_Text approvalBodyText;
        [SerializeField] private Button approveButton;
        [SerializeField] private Button denyButton;

        private string _currentSessionId;
        private ApprovalData _currentApproval;

        // Status colors
        private static readonly Color ColorIdle = new(0.4f, 0.8f, 0.4f);
        private static readonly Color ColorThinking = new(0.3f, 0.6f, 1f);
        private static readonly Color ColorExecuting = new(1f, 0.8f, 0.2f);
        private static readonly Color ColorError = new(1f, 0.3f, 0.3f);
        private static readonly Color ColorWaiting = new(0.9f, 0.5f, 0.1f);

        private void Start()
        {
            if (approvalOverlay != null)
                approvalOverlay.SetActive(false);

            // Wire button clicks
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendClicked);

            if (approveButton != null)
                approveButton.onClick.AddListener(() => OnApprovalDecision(true));

            if (denyButton != null)
                denyButton.onClick.AddListener(() => OnApprovalDecision(false));

            if (messageInput != null)
                messageInput.onSubmit.AddListener(_ => OnSendClicked());
        }

        private void OnEnable()
        {
            if (sessionManager != null)
            {
                sessionManager.OnSessionUpdated += HandleSessionUpdated;
                sessionManager.OnNewMessage += HandleNewMessage;
                sessionManager.OnFocusChanged += HandleFocusChanged;
            }

            if (notificationManager != null)
                notificationManager.OnApprovalRequired += HandleApprovalRequired;
        }

        private void OnDisable()
        {
            if (sessionManager != null)
            {
                sessionManager.OnSessionUpdated -= HandleSessionUpdated;
                sessionManager.OnNewMessage -= HandleNewMessage;
                sessionManager.OnFocusChanged -= HandleFocusChanged;
            }

            if (notificationManager != null)
                notificationManager.OnApprovalRequired -= HandleApprovalRequired;
        }

        // --- Event handlers ---

        private void HandleFocusChanged(SessionData session)
        {
            _currentSessionId = session.id;
            RefreshPanel(session);
        }

        private void HandleSessionUpdated(SessionData session)
        {
            if (session.id != _currentSessionId) return;
            RefreshHeader(session);
            RefreshConversation(session);
        }

        private void HandleNewMessage(string sessionId, ConversationMessage msg)
        {
            if (sessionId != _currentSessionId) return;

            var session = sessionManager.GetSession(sessionId);
            if (session != null)
                RefreshConversation(session);
        }

        private void HandleApprovalRequired(ApprovalData approval)
        {
            if (approval.sessionId != _currentSessionId) return;
            ShowApproval(approval);
        }

        // --- UI updates ---

        private void RefreshPanel(SessionData session)
        {
            RefreshHeader(session);
            RefreshConversation(session);
        }

        private void RefreshHeader(SessionData session)
        {
            if (sessionNameText != null)
                sessionNameText.text = session.name;

            if (statusLabelText != null)
                statusLabelText.text = FormatStatus(session.ParsedStatus);

            if (statusDot != null)
                statusDot.color = StatusColor(session.ParsedStatus);
        }

        private void RefreshConversation(SessionData session)
        {
            if (conversationText == null) return;

            var sb = new System.Text.StringBuilder();
            foreach (var msg in session.conversation)
            {
                string label = msg.role == "user" ? "<b>You:</b>" : "<b>Claude:</b>";
                sb.AppendLine($"{label} {msg.content}");
                sb.AppendLine();
            }

            conversationText.text = sb.ToString();

            // Auto-scroll to bottom
            if (scrollRect != null)
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
        }

        private void ShowApproval(ApprovalData approval)
        {
            _currentApproval = approval;
            if (approvalOverlay != null)
                approvalOverlay.SetActive(true);

            if (approvalBodyText != null)
                approvalBodyText.text = $"<b>{approval.toolName}</b>\n{approval.description}";
        }

        private void HideApproval()
        {
            _currentApproval = null;
            if (approvalOverlay != null)
                approvalOverlay.SetActive(false);
        }

        // --- User actions ---

        private void OnSendClicked()
        {
            if (messageInput == null || string.IsNullOrWhiteSpace(messageInput.text)) return;
            if (_currentSessionId == null) return;

            string content = messageInput.text.Trim();
            wsClient.SendMessage(_currentSessionId, content);

            // Add to local conversation immediately
            var session = sessionManager.GetSession(_currentSessionId);
            if (session != null)
            {
                session.conversation.Add(new ConversationMessage { role = "user", content = content });
                RefreshConversation(session);
            }

            messageInput.text = "";
            messageInput.ActivateInputField();
        }

        private void OnApprovalDecision(bool approved)
        {
            if (_currentApproval == null) return;
            notificationManager.RespondToApproval(_currentApproval.approvalId, _currentApproval.sessionId, approved);
            HideApproval();
        }

        // --- Helpers ---

        private static string FormatStatus(SessionStatus status)
        {
            return status switch
            {
                SessionStatus.Thinking => "Thinking...",
                SessionStatus.Executing => "Executing",
                SessionStatus.WaitingInput => "Waiting for input",
                SessionStatus.Error => "Error",
                _ => "Idle",
            };
        }

        private static Color StatusColor(SessionStatus status)
        {
            return status switch
            {
                SessionStatus.Thinking => ColorThinking,
                SessionStatus.Executing => ColorExecuting,
                SessionStatus.WaitingInput => ColorWaiting,
                SessionStatus.Error => ColorError,
                _ => ColorIdle,
            };
        }
    }
}
