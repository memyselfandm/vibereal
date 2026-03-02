using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VibeReal.Core;
using VibeReal.Data;

namespace VibeReal.UI
{
    /// <summary>
    /// Controls the approval dialog UI.
    /// Shows when Claude needs permission to execute an action.
    /// </summary>
    public class ApprovalDialogController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NotificationManager notificationManager;
        [SerializeField] private SessionManager sessionManager;

        [Header("UI Elements")]
        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private TextMeshProUGUI sessionNameText;
        [SerializeField] private TextMeshProUGUI toolNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Button approveButton;
        [SerializeField] private Button denyButton;
        [SerializeField] private TextMeshProUGUI hintText;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float pulseScale = 1.05f;
        [SerializeField] private float pulseSpeed = 2f;

        private NotificationData _currentApproval;
        private CanvasGroup _canvasGroup;
        private Vector3 _originalScale;
        private bool _isPulsing;

        private void Awake()
        {
            _canvasGroup = dialogPanel?.GetComponent<CanvasGroup>();
            if (dialogPanel != null)
            {
                _originalScale = dialogPanel.transform.localScale;
            }
        }

        private void Start()
        {
            if (notificationManager == null)
                notificationManager = FindObjectOfType<NotificationManager>();
            if (sessionManager == null)
                sessionManager = FindObjectOfType<SessionManager>();

            // Subscribe to events
            if (notificationManager != null)
            {
                notificationManager.OnApprovalRequired += HandleApprovalRequired;
                notificationManager.OnNotificationDismissed += HandleNotificationDismissed;
            }

            // Set up button listeners
            if (approveButton != null)
            {
                approveButton.onClick.AddListener(OnApproveClicked);
            }

            if (denyButton != null)
            {
                denyButton.onClick.AddListener(OnDenyClicked);
            }

            // Initial state
            Hide();
        }

        private void OnDestroy()
        {
            if (notificationManager != null)
            {
                notificationManager.OnApprovalRequired -= HandleApprovalRequired;
                notificationManager.OnNotificationDismissed -= HandleNotificationDismissed;
            }

            if (approveButton != null)
            {
                approveButton.onClick.RemoveListener(OnApproveClicked);
            }

            if (denyButton != null)
            {
                denyButton.onClick.RemoveListener(OnDenyClicked);
            }
        }

        private void Update()
        {
            // Pulse animation when visible
            if (_isPulsing && dialogPanel != null)
            {
                float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * (pulseScale - 1f) * 0.5f;
                dialogPanel.transform.localScale = _originalScale * scale;
            }
        }

        /// <summary>
        /// Show the approval dialog
        /// </summary>
        public void Show(NotificationData approval)
        {
            _currentApproval = approval;

            // Get session info
            var session = sessionManager?.GetSession(approval.sessionId);

            // Update UI
            if (sessionNameText != null)
            {
                sessionNameText.text = session?.name ?? approval.sessionId;
            }

            if (toolNameText != null)
            {
                toolNameText.text = approval.title;
            }

            if (descriptionText != null)
            {
                descriptionText.text = approval.body;
            }

            if (hintText != null)
            {
                hintText.text = "Say \"Approve\" or \"Deny\", or use buttons";
            }

            // Show panel
            if (dialogPanel != null)
            {
                dialogPanel.SetActive(true);
                dialogPanel.transform.localScale = _originalScale;
            }

            // Fade in
            if (_canvasGroup != null)
            {
                StartCoroutine(FadeIn());
            }

            _isPulsing = true;
        }

        /// <summary>
        /// Hide the approval dialog
        /// </summary>
        public void Hide()
        {
            _currentApproval = null;
            _isPulsing = false;

            if (dialogPanel != null)
            {
                dialogPanel.SetActive(false);
                dialogPanel.transform.localScale = _originalScale;
            }
        }

        /// <summary>
        /// Approve via voice command
        /// </summary>
        public void ApproveViaVoice()
        {
            if (_currentApproval != null)
            {
                OnApproveClicked();
            }
        }

        /// <summary>
        /// Deny via voice command
        /// </summary>
        public void DenyViaVoice()
        {
            if (_currentApproval != null)
            {
                OnDenyClicked();
            }
        }

        // ==================== Event Handlers ====================

        private void HandleApprovalRequired(NotificationData approval)
        {
            Show(approval);
        }

        private void HandleNotificationDismissed(NotificationData notification)
        {
            if (_currentApproval != null && notification.id == _currentApproval.id)
            {
                Hide();
            }
        }

        private void OnApproveClicked()
        {
            if (notificationManager != null)
            {
                notificationManager.ApproveCurrentApproval();
            }
            Hide();
        }

        private void OnDenyClicked()
        {
            if (notificationManager != null)
            {
                notificationManager.DenyCurrentApproval();
            }
            Hide();
        }

        // ==================== Animation ====================

        private System.Collections.IEnumerator FadeIn()
        {
            float elapsed = 0f;
            _canvasGroup.alpha = 0f;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
        }
    }
}
