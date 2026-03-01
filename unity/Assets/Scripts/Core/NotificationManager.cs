using System;
using System.Collections.Generic;
using UnityEngine;
using VibeReal.Data;

namespace VibeReal.Core
{
    /// <summary>
    /// Receives notifications from the hub, maintains a queue, and exposes events
    /// for the UI layer to display toasts and approval dialogs.
    /// MVP scope: queue notifications, fire events, track pending approvals.
    /// </summary>
    public class NotificationManager : MonoBehaviour
    {
        [SerializeField] private int maxQueueSize = 20;

        public event Action<NotificationData> OnNotificationReceived;
        public event Action<ApprovalData> OnApprovalRequired;
        public event Action<NotificationData> OnNotificationDismissed;

        private readonly Queue<NotificationData> _queue = new();
        private readonly Dictionary<string, ApprovalData> _pendingApprovals = new();

        [SerializeField] private WebSocketClient wsClient;

        public IReadOnlyDictionary<string, ApprovalData> PendingApprovals => _pendingApprovals;

        private void OnEnable()
        {
            if (wsClient == null)
                wsClient = FindFirstObjectByType<WebSocketClient>();

            wsClient.OnNotification += HandleNotification;
        }

        private void OnDisable()
        {
            if (wsClient != null)
                wsClient.OnNotification -= HandleNotification;
        }

        public void Dismiss(string notificationId)
        {
            // Remove from pending approvals if it was one
            _pendingApprovals.Remove(notificationId);
        }

        public void RespondToApproval(string approvalId, string sessionId, bool approved)
        {
            wsClient.SendApproval(sessionId, approvalId, approved);
            _pendingApprovals.Remove(approvalId);
        }

        private void HandleNotification(NotificationMessage msg)
        {
            var notification = msg.notification;
            if (notification == null) return;

            // Enforce max queue
            while (_queue.Count >= maxQueueSize)
                _queue.Dequeue();

            _queue.Enqueue(notification);
            OnNotificationReceived?.Invoke(notification);

            // Track approval requests
            if (notification.ParsedType == NotificationType.ApprovalRequired && notification.metadata != null)
            {
                var approval = new ApprovalData
                {
                    approvalId = notification.metadata.approvalId,
                    toolName = notification.metadata.toolName,
                    description = notification.metadata.description,
                    sessionId = notification.sessionId,
                };
                _pendingApprovals[approval.approvalId] = approval;
                OnApprovalRequired?.Invoke(approval);
            }
        }
    }
}
