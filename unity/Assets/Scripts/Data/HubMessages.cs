using System;
using System.Collections.Generic;

namespace VibeReal.Data
{
    /// <summary>
    /// Base class for all hub messages
    /// </summary>
    [Serializable]
    public class HubMessage
    {
        public string type;
        public string requestId;
    }

    // ==================== Client → Hub Messages ====================

    /// <summary>
    /// Send a voice command transcript to the hub for NLU processing
    /// </summary>
    [Serializable]
    public class VoiceCommandMessage : HubMessage
    {
        public string transcript;
        public float confidence;

        public VoiceCommandMessage()
        {
            type = "voice_command";
            requestId = Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// Send a direct message to a specific session
    /// </summary>
    [Serializable]
    public class SendMessageRequest : HubMessage
    {
        public string sessionId;
        public MessageContent message;

        public SendMessageRequest()
        {
            type = "send_message";
            requestId = Guid.NewGuid().ToString();
        }
    }

    [Serializable]
    public class MessageContent
    {
        public string role;
        public string content;
    }

    /// <summary>
    /// Approve or deny a pending action
    /// </summary>
    [Serializable]
    public class ApprovalDecisionMessage : HubMessage
    {
        public string sessionId;
        public string approvalId;
        public string decision; // "approve" or "deny"

        public ApprovalDecisionMessage()
        {
            type = "approval_decision";
            requestId = Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// Request list of all sessions
    /// </summary>
    [Serializable]
    public class ListSessionsMessage : HubMessage
    {
        public ListSessionsMessage()
        {
            type = "list_sessions";
            requestId = Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// Set the focused session
    /// </summary>
    [Serializable]
    public class SetFocusMessage : HubMessage
    {
        public string sessionId;

        public SetFocusMessage()
        {
            type = "set_focus";
            requestId = Guid.NewGuid().ToString();
        }
    }

    // ==================== Hub → Client Messages ====================

    /// <summary>
    /// Acknowledgment of a voice command
    /// </summary>
    [Serializable]
    public class CommandAckMessage : HubMessage
    {
        public string intent;
        public string targetSession;
        public float confidence;
    }

    /// <summary>
    /// List of sessions from the hub
    /// </summary>
    [Serializable]
    public class SessionListMessage : HubMessage
    {
        public List<SessionInfo> sessions;
    }

    [Serializable]
    public class SessionInfo
    {
        public string id;
        public string name;
        public string type;
        public string status;
        public string lastActivity;
        public string currentTask;
    }

    /// <summary>
    /// Session status update (push)
    /// </summary>
    [Serializable]
    public class SessionUpdateMessage : HubMessage
    {
        public string sessionId;
        public string status;
        public string currentTask;
    }

    /// <summary>
    /// Claude response content (may be streaming)
    /// </summary>
    [Serializable]
    public class ClaudeResponseMessage : HubMessage
    {
        public string sessionId;
        public string content;
        public bool isComplete;
    }

    /// <summary>
    /// Notification from the hub
    /// </summary>
    [Serializable]
    public class NotificationMessage : HubMessage
    {
        public NotificationData notification;
    }

    [Serializable]
    public class NotificationData
    {
        public string id;
        public string sessionId;
        public string type; // "approval_required", "task_complete", "error", "info"
        public string priority; // "low", "normal", "high", "critical"
        public string title;
        public string body;
        public string voiceText;
    }

    /// <summary>
    /// Error response from the hub
    /// </summary>
    [Serializable]
    public class ErrorMessage : HubMessage
    {
        public string code;
        public string message;
    }

    /// <summary>
    /// Helper class to parse incoming messages
    /// </summary>
    public static class HubMessageParser
    {
        public static string GetMessageType(string json)
        {
            // Simple extraction of type field
            var typeMessage = UnityEngine.JsonUtility.FromJson<HubMessage>(json);
            return typeMessage?.type;
        }

        public static T Parse<T>(string json) where T : HubMessage
        {
            return UnityEngine.JsonUtility.FromJson<T>(json);
        }
    }
}
