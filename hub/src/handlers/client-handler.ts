import type WebSocket from "ws";
import * as clientManager from "../client-manager.ts";
import { handleClientMessage } from "../message-router.ts";

/**
 * Handle a new client WebSocket connection.
 * Clients connect on /client with optional API key auth.
 */
export function onClientOpen(ws: WebSocket): void {
  clientManager.add(ws);
  console.log(`[client] Connected (total: ${clientManager.count()})`);
}

export function onClientMessage(ws: WebSocket, message: string): void {
  handleClientMessage(ws, message);
}

export function onClientClose(ws: WebSocket): void {
  clientManager.remove(ws);
  console.log(`[client] Disconnected (total: ${clientManager.count()})`);
}
