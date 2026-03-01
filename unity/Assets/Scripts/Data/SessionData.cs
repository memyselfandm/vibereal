using System;
using System.Collections.Generic;

namespace VibeReal.Data
{
    public enum SessionType { Container, Laptop, Remote }

    public enum SessionStatus { Idle, Thinking, WaitingInput, Executing, Error }

    [Serializable]
    public class SessionData
    {
        public string id;
        public string name;
        public string type;
        public string status;
        public string currentTask;
        public string lastActivity;
        public List<ConversationMessage> conversation = new List<ConversationMessage>();
        public List<ApprovalData> pendingApprovals = new List<ApprovalData>();

        public SessionStatus ParsedStatus
        {
            get
            {
                return status switch
                {
                    "thinking" => SessionStatus.Thinking,
                    "waiting_input" => SessionStatus.WaitingInput,
                    "executing" => SessionStatus.Executing,
                    "error" => SessionStatus.Error,
                    _ => SessionStatus.Idle,
                };
            }
        }
    }

    [Serializable]
    public class ConversationMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class ApprovalData
    {
        public string approvalId;
        public string toolName;
        public string description;
        public string sessionId;
    }
}
