using System;
using System.Collections.Generic;

namespace VibeReal.Data
{
    // --- Incoming messages (Hub -> Client) ---

    [Serializable]
    public class HubMessageBase
    {
        public string type;
    }

    [Serializable]
    public class SessionListMessage
    {
        public string type;
        public string requestId;
        public List<SessionData> sessions;
    }

    [Serializable]
    public class SessionUpdateMessage
    {
        public string type;
        public string sessionId;
        public string status;
        public string currentTask;
    }

    [Serializable]
    public class ClaudeResponseMessage
    {
        public string type;
        public string sessionId;
        public string content;
        public bool isComplete;
    }

    [Serializable]
    public class NotificationMessage
    {
        public string type;
        public NotificationData notification;
    }

    [Serializable]
    public class CommandAckMessage
    {
        public string type;
        public string requestId;
        public string intent;
        public string targetSession;
        public float confidence;
    }

    [Serializable]
    public class ConversationHistoryMessage
    {
        public string type;
        public string sessionId;
        public List<ConversationMessage> messages;
    }

    // --- Outgoing messages (Client -> Hub) ---

    [Serializable]
    public class SendMessageRequest
    {
        public string type = "send_message";
        public string requestId;
        public string sessionId;
        public MessageContent message;
    }

    [Serializable]
    public class MessageContent
    {
        public string role = "user";
        public string content;
    }

    [Serializable]
    public class ApprovalDecisionRequest
    {
        public string type = "approval_decision";
        public string requestId;
        public string sessionId;
        public string approvalId;
        public string decision;
    }

    [Serializable]
    public class ListSessionsRequest
    {
        public string type = "list_sessions";
        public string requestId;
    }

    [Serializable]
    public class SetFocusRequest
    {
        public string type = "set_focus";
        public string requestId;
        public string sessionId;
    }
}
