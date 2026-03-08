import type WebSocket from "ws";
import * as registry from "../session-registry.ts";
import * as clients from "../client-manager.ts";
import { handleSessionMessage } from "../message-router.ts";

/**
 * Handle a new session WebSocket connection.
 * Session ID is extracted from the URL path: /session/:id
 */
export function onSessionOpen(ws: WebSocket, sessionId: string): void {
  console.log(`[session] Connected: ${sessionId}`);
}

export function onSessionMessage(ws: WebSocket, sessionId: string, message: string): void {
  handleSessionMessage(ws, sessionId, message);
}

export function onSessionClose(ws: WebSocket, sessionId: string): void {
  console.log(`[session] Disconnected: ${sessionId}`);
  registry.markDisconnected(sessionId);
  clients.broadcast(
    JSON.stringify({
      type: "session_update",
      sessionId,
      status: "disconnected",
      currentTask: "",
    })
  );
}
