/**
 * WebSocket message types mirroring unity/Assets/Scripts/Data/HubMessages.cs
 * Field names must be exact camelCase matches for JsonUtility compatibility.
 */

// ==================== Base ====================

export interface HubMessage {
  type: string;
  requestId?: string;
}

// ==================== Session → Hub ====================

export interface RegisterMessage extends HubMessage {
  type: "register";
  sessionId: string;
  name: string;
  metadata?: Record<string, string>;
}

export interface StatusUpdateMessage extends HubMessage {
  type: "status_update";
  status: string;
  currentTask: string;
}

export interface ResponseChunkMessage extends HubMessage {
  type: "response_chunk";
  content: string;
  isComplete: boolean;
}

export interface ApprovalRequestMessage extends HubMessage {
  type: "approval_request";
  approvalId: string;
  toolName: string;
  description: string;
}

// ==================== Hub → Client ====================

export interface SessionInfo {
  id: string;
  name: string;
  type: string;
  status: string;
  lastActivity: string;
  currentTask: string;
}

export interface SessionListMessage extends HubMessage {
  type: "session_list";
  sessions: SessionInfo[];
}

export interface SessionUpdateMessage extends HubMessage {
  type: "session_update";
  sessionId: string;
  status: string;
  currentTask: string;
}

export interface ClaudeResponseMessage extends HubMessage {
  type: "claude_response";
  sessionId: string;
  content: string;
  isComplete: boolean;
}

export interface NotificationData {
  id: string;
  sessionId: string;
  type: string;
  priority: string;
  title: string;
  body: string;
  voiceText: string;
}

export interface NotificationMessage extends HubMessage {
  type: "notification";
  notification: NotificationData;
}

export interface ErrorMessage extends HubMessage {
  type: "error";
  code: string;
  message: string;
}

// ==================== Client → Hub ====================

export interface ListSessionsMessage extends HubMessage {
  type: "list_sessions";
}

export interface SendMessageMessage extends HubMessage {
  type: "send_message";
  sessionId: string;
  message: { role: string; content: string };
}

export interface ApprovalDecisionMessage extends HubMessage {
  type: "approval_decision";
  sessionId: string;
  approvalId: string;
  decision: string;
}

export interface SetFocusMessage extends HubMessage {
  type: "set_focus";
  sessionId: string;
}

// ==================== Internal ====================

export interface SessionRecord {
  id: string;
  name: string;
  type: string;
  status: string;
  currentTask: string;
  ws: import("ws").WebSocket | null;
  lastActivity: Date;
  pendingApprovals: PendingApproval[];
}

export interface PendingApproval {
  approvalId: string;
  toolName: string;
  description: string;
  timestamp: Date;
}

export interface ClientConnection {
  ws: import("ws").WebSocket;
  focusedSessionId: string | null;
}
