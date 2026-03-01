using System;

namespace VibeReal.Data
{
    public enum NotificationType { SessionUpdate, ApprovalRequired, TaskComplete, Error, Info }

    public enum NotificationPriority { Low, Normal, High, Critical }

    [Serializable]
    public class NotificationData
    {
        public string id;
        public string sessionId;
        public string type;
        public string priority;
        public string title;
        public string body;
        public string voiceText;
        public string timestamp;
        public NotificationMetadata metadata;

        public NotificationType ParsedType
        {
            get
            {
                return type switch
                {
                    "approval_required" => NotificationType.ApprovalRequired,
                    "task_complete" => NotificationType.TaskComplete,
                    "error" => NotificationType.Error,
                    "info" => NotificationType.Info,
                    _ => NotificationType.SessionUpdate,
                };
            }
        }

        public NotificationPriority ParsedPriority
        {
            get
            {
                return priority switch
                {
                    "critical" => NotificationPriority.Critical,
                    "high" => NotificationPriority.High,
                    "low" => NotificationPriority.Low,
                    _ => NotificationPriority.Normal,
                };
            }
        }
    }

    [Serializable]
    public class NotificationMetadata
    {
        public string approvalId;
        public string toolName;
        public string description;
    }
}
