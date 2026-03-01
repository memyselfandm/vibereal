using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using VibeReal.Core;
using VibeReal.Data;

namespace VibeReal.UI
{
    /// <summary>
    /// Displays notification toasts that auto-dismiss.
    /// MVP scope: show/hide a single toast prefab, queue overflow.
    /// In production, pool multiple toast GameObjects for stacking.
    /// </summary>
    public class NotificationToastUI : MonoBehaviour
    {
        [SerializeField] private NotificationManager notificationManager;

        [Header("Toast UI")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private TMP_Text toastTitle;
        [SerializeField] private TMP_Text toastBody;
        [SerializeField] private TMP_Text toastSession;

        [Header("Settings")]
        [SerializeField] private float displayDuration = 5f;
        [SerializeField] private int maxQueuedToasts = 5;

        private readonly Queue<NotificationData> _toastQueue = new();
        private bool _showing;

        private void OnEnable()
        {
            if (notificationManager != null)
                notificationManager.OnNotificationReceived += HandleNotification;

            if (toastRoot != null)
                toastRoot.SetActive(false);
        }

        private void OnDisable()
        {
            if (notificationManager != null)
                notificationManager.OnNotificationReceived -= HandleNotification;
        }

        private void HandleNotification(NotificationData notification)
        {
            // Skip approval_required — handled by the panel's approval overlay
            if (notification.ParsedType == NotificationType.ApprovalRequired)
                return;

            if (_toastQueue.Count >= maxQueuedToasts)
                _toastQueue.Dequeue();

            _toastQueue.Enqueue(notification);

            if (!_showing)
                StartCoroutine(ShowNextToast());
        }

        private IEnumerator ShowNextToast()
        {
            _showing = true;

            while (_toastQueue.Count > 0)
            {
                var notification = _toastQueue.Dequeue();
                DisplayToast(notification);
                yield return new WaitForSeconds(displayDuration);
                HideToast();
                yield return new WaitForSeconds(0.3f); // Brief gap between toasts
            }

            _showing = false;
        }

        private void DisplayToast(NotificationData notification)
        {
            if (toastRoot != null)
                toastRoot.SetActive(true);

            if (toastTitle != null)
                toastTitle.text = notification.title;

            if (toastBody != null)
                toastBody.text = notification.body;

            if (toastSession != null)
                toastSession.text = notification.sessionId;
        }

        private void HideToast()
        {
            if (toastRoot != null)
                toastRoot.SetActive(false);
        }
    }
}
