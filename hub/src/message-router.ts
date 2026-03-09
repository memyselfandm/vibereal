import type WebSocket from "ws";
import type {
  HubMessage,
  RegisterMessage,
  StatusUpdateMessage,
  ResponseChunkMessage,
  ApprovalRequestMessage,
  ListSessionsMessage,
  SendMessageMessage,
  ApprovalDecisionMessage,
  SetFocusMessage,
} from "./types.ts";
import * as registry from "./session-registry.ts";
import * as clients from "./client-manager.ts";

/**
 * Route a message from a session WebSocket.
 */
export function handleSessionMessage(ws: WebSocket, sessionId: string, raw: string): void {
  let msg: HubMessage;
  try {
    msg = JSON.parse(raw);
  } catch {
    console.error(`[router] Invalid JSON from session ${sessionId}`);
    return;
  }

  switch (msg.type) {
    case "register": {
      const m = msg as RegisterMessage;
      registry.register(m.sessionId || sessionId, m.name, ws, m.metadata);
      console.log(`[router] Session registered: ${sessionId} (${m.name})`);
      clients.broadcast(
        JSON.stringify({
          type: "session_update",
          sessionId,
          status: "idle",
          currentTask: "",
        })
      );
      break;
    }

    case "status_update": {
      const m = msg as StatusUpdateMessage;
      registry.updateStatus(sessionId, m.status, m.currentTask);
      clients.broadcast(
        JSON.stringify({
          type: "session_update",
          sessionId,
          status: m.status,
          currentTask: m.currentTask,
        })
      );
      break;
    }

    case "response_chunk": {
      const m = msg as ResponseChunkMessage;
      clients.broadcast(
        JSON.stringify({
          type: "claude_response",
          sessionId,
          content: m.content,
          isComplete: m.isComplete,
        })
      );
      break;
    }

    case "approval_request": {
      const m = msg as ApprovalRequestMessage;
      registry.addApproval(sessionId, {
        approvalId: m.approvalId,
        toolName: m.toolName,
        description: m.description,
        timestamp: new Date(),
      });
      registry.updateStatus(sessionId, "waiting_input", `Approval: ${m.toolName}`);
      const notification = {
        type: "notification",
        notification: {
          id: m.approvalId,
          sessionId,
          type: "approval_required",
          priority: "high",
          title: `Approval: ${m.toolName}`,
          body: m.description,
          voiceText: `${m.toolName} requires approval: ${m.description}`,
        },
      };
      clients.broadcast(JSON.stringify(notification));
      // Also send session_update so clients update status
      clients.broadcast(
        JSON.stringify({
          type: "session_update",
          sessionId,
          status: "waiting_input",
          currentTask: `Approval: ${m.toolName}`,
        })
      );
      break;
    }

    default:
      console.warn(`[router] Unknown session message type: ${msg.type}`);
  }
}

/**
 * Route a message from a client WebSocket.
 */
export function handleClientMessage(ws: WebSocket, raw: string): void {
  let msg: HubMessage;
  try {
    msg = JSON.parse(raw);
  } catch {
    ws.send(JSON.stringify({ type: "error", code: "INVALID_JSON", message: "Invalid JSON" }));
    return;
  }

  switch (msg.type) {
    case "list_sessions": {
      const m = msg as ListSessionsMessage;
      const sessions = registry.getAllInfo();
      ws.send(
        JSON.stringify({
          type: "session_list",
          requestId: m.requestId,
          sessions,
        })
      );
      break;
    }

    case "send_message": {
      const m = msg as SendMessageMessage;
      const session = registry.get(m.sessionId);
      if (!session) {
        ws.send(
          JSON.stringify({
            type: "error",
            requestId: m.requestId,
            code: "SESSION_NOT_FOUND",
            message: `Session ${m.sessionId} not found`,
          })
        );
        return;
      }
      // Try managed process first, then WebSocket
      if (!session.ws) {
        // Lazy import to avoid circular dependency at module level
        import("./process-manager.ts").then((pm) => {
          if (pm.isManaged(m.sessionId)) {
            pm.sendMessage(m.sessionId, m.message);
          } else {
            ws.send(
              JSON.stringify({
                type: "error",
                requestId: m.requestId,
                code: "SESSION_DISCONNECTED",
                message: `Session ${m.sessionId} is disconnected`,
              })
            );
          }
        });
        return;
      }
      session.ws.send(
        JSON.stringify({
          type: "send_message",
          requestId: m.requestId,
          message: m.message,
        })
      );
      break;
    }

    case "approval_decision": {
      const m = msg as ApprovalDecisionMessage;
      const session = registry.get(m.sessionId);
      if (!session) {
        ws.send(
          JSON.stringify({
            type: "error",
            requestId: m.requestId,
            code: "SESSION_NOT_FOUND",
            message: `Session ${m.sessionId} not found`,
          })
        );
        return;
      }
      // Try managed process first, then WebSocket
      if (!session.ws) {
        import("./process-manager.ts").then((pm) => {
          if (pm.isManaged(m.sessionId)) {
            pm.sendApprovalDecision(m.sessionId, m.approvalId, m.decision);
          } else {
            ws.send(
              JSON.stringify({
                type: "error",
                requestId: m.requestId,
                code: "SESSION_DISCONNECTED",
                message: `Session ${m.sessionId} is disconnected`,
              })
            );
          }
        });
        return;
      }
      registry.removeApproval(m.sessionId, m.approvalId);
      session.ws.send(
        JSON.stringify({
          type: "approval_decision",
          requestId: m.requestId,
          approvalId: m.approvalId,
          decision: m.decision,
        })
      );
      break;
    }

    case "set_focus": {
      const m = msg as SetFocusMessage;
      clients.setFocus(ws, m.sessionId);
      break;
    }

    default:
      ws.send(
        JSON.stringify({
          type: "error",
          code: "UNKNOWN_TYPE",
          message: `Unknown message type: ${msg.type}`,
        })
      );
  }
}
