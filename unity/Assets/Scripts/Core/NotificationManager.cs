using System;
using System.Collections.Generic;
using UnityEngine;
using VibeReal.Data;

namespace VibeReal.Core
{
    /// <summary>
    /// Manages notifications from the Session Hub.
    /// Handles display, queuing, and TTS announcements.
    /// </summary>
    public class NotificationManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WebSocketClient webSocketClient;
        [SerializeField] private VoiceManager voiceManager;
        [SerializeField] private SessionManager sessionManager;

        [Header("Settings")]
        [SerializeField] private float toastDuration = 5f;
        [SerializeField] private int maxVisibleToasts = 3;
        [SerializeField] private bool speakCriticalNotifications = true;
        [SerializeField] private bool speakHighNotifications = true;

        // Events
        public event Action<NotificationData> OnNotificationReceived;
        public event Action<NotificationData> OnNotificationDismissed;
        public event Action<NotificationData> OnApprovalRequired;

        // State
        public IReadOnlyList<NotificationData> ActiveNotifications => _activeNotifications;
        public NotificationData CurrentApproval { get; private set; }

        private List<NotificationData> _activeNotifications = new List<NotificationData>();
        private Queue<NotificationData> _notificationQueue = new Queue<NotificationData>();

        private void Start()
        {
            if (webSocketClient == null)
                webSocketClient = FindObjectOfType<WebSocketClient>();
            if (voiceManager == null)
                voiceManager = FindObjectOfType<VoiceManager>();
            if (sessionManager == null)
                sessionManager = FindObjectOfType<SessionManager>();

            if (webSocketClient != null)
            {
                webSocketClient.OnNotification += HandleNotification;
            }
        }

        private void OnDestroy()
        {
            if (webSocketClient != null)
            {
                webSocketClient.OnNotification -= HandleNotification;
            }
        }

        /// <summary>
        /// Show a notification
        /// </summary>
        public void ShowNotification(NotificationData notification)
        {
            // Add to active list
            _activeNotifications.Add(notification);
            OnNotificationReceived?.Invoke(notification);

            // Speak if high priority
            if (ShouldSpeak(notification) && voiceManager != null)
            {
                voiceManager.SpeakNotification(notification);
            }

            // Auto-dismiss after duration (except approvals)
            if (notification.type != "approval_required")
            {
                StartCoroutine(AutoDismissCoroutine(notification));
            }

            // Limit visible toasts
            while (_activeNotifications.Count > maxVisibleToasts)
            {
                var oldest = _activeNotifications[0];
                if (oldest.type != "approval_required")
                {
                    DismissNotification(oldest.id);
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Show an approval dialog
        /// </summary>
        public void ShowApproval(NotificationData notification)
        {
            CurrentApproval = notification;
            OnApprovalRequired?.Invoke(notification);

            // Speak the approval
            if (voiceManager != null)
            {
                voiceManager.SpeakNotification(notification);
            }
        }

        /// <summary>
        /// Dismiss a notification by ID
        /// </summary>
        public void DismissNotification(string notificationId)
        {
            var notification = _activeNotifications.Find(n => n.id == notificationId);
            if (notification != null)
            {
                _activeNotifications.Remove(notification);
                OnNotificationDismissed?.Invoke(notification);
            }

            if (CurrentApproval?.id == notificationId)
            {
                CurrentApproval = null;
            }
        }

        /// <summary>
        /// Dismiss all notifications
        /// </summary>
        public void DismissAll()
        {
            var toRemove = new List<NotificationData>(_activeNotifications);
            foreach (var notification in toRemove)
            {
                DismissNotification(notification.id);
            }
        }

        /// <summary>
        /// Approve the current approval dialog
        /// </summary>
        public void ApproveCurrentApproval()
        {
            if (CurrentApproval != null && sessionManager != null)
            {
                sessionManager.Approve(CurrentApproval.sessionId, CurrentApproval.id);
                DismissNotification(CurrentApproval.id);

                if (voiceManager != null)
                {
                    voiceManager.Speak("Approved.");
                }
            }
        }

        /// <summary>
        /// Deny the current approval dialog
        /// </summary>
        public void DenyCurrentApproval()
        {
            if (CurrentApproval != null && sessionManager != null)
            {
                sessionManager.Deny(CurrentApproval.sessionId, CurrentApproval.id);
                DismissNotification(CurrentApproval.id);

                if (voiceManager != null)
                {
                    voiceManager.Speak("Denied.");
                }
            }
        }

        /// <summary>
        /// Create a local notification (not from hub)
        /// </summary>
        public void CreateLocalNotification(string title, string body,
            string priority = "normal", string type = "info")
        {
            var notification = new NotificationData
            {
                id = Guid.NewGuid().ToString(),
                sessionId = "",
                type = type,
                priority = priority,
                title = title,
                body = body,
                voiceText = title
            };

            ShowNotification(notification);
        }

        // ==================== Event Handlers ====================

        private void HandleNotification(NotificationMessage message)
        {
            var notification = message.notification;

            if (notification.type == "approval_required")
            {
                ShowApproval(notification);
            }
            else
            {
                ShowNotification(notification);
            }
        }

        // ==================== Helpers ====================

        private bool ShouldSpeak(NotificationData notification)
        {
            return notification.priority switch
            {
                "critical" => speakCriticalNotifications,
                "high" => speakHighNotifications,
                _ => false
            };
        }

        private System.Collections.IEnumerator AutoDismissCoroutine(NotificationData notification)
        {
            yield return new WaitForSeconds(toastDuration);

            if (_activeNotifications.Contains(notification))
            {
                DismissNotification(notification.id);
            }
        }
    }
}
