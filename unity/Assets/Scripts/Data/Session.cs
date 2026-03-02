using System;
using System.Collections.Generic;

namespace VibeReal.Data
{
    /// <summary>
    /// Represents a Claude session (container or laptop)
    /// </summary>
    [Serializable]
    public class Session
    {
        public string id;
        public string name;
        public SessionType type;
        public SessionStatus status;
        public string currentTask;
        public DateTime lastActivity;
        public List<Approval> pendingApprovals = new List<Approval>();
        public List<ConversationMessage> recentHistory = new List<ConversationMessage>();
    }

    public enum SessionType
    {
        Container,
        Laptop,
        Remote
    }

    public enum SessionStatus
    {
        Idle,
        Thinking,
        WaitingInput,
        Executing,
        Error,
        Disconnected
    }

    /// <summary>
    /// Pending approval for a tool call
    /// </summary>
    [Serializable]
    public class Approval
    {
        public string id;
        public string toolName;
        public string description;
        public DateTime timestamp;
    }

    /// <summary>
    /// A message in the conversation history
    /// </summary>
    [Serializable]
    public class ConversationMessage
    {
        public string role; // "user" or "assistant"
        public string content;
        public DateTime timestamp;
        public bool isComplete;
    }
}
