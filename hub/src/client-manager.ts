import type WebSocket from "ws";
import type { ClientConnection } from "./types.ts";

const clients = new Set<ClientConnection>();

export function add(ws: WebSocket): ClientConnection {
  const conn: ClientConnection = { ws, focusedSessionId: null };
  clients.add(conn);
  return conn;
}

export function remove(ws: WebSocket): void {
  for (const client of clients) {
    if (client.ws === ws) {
      clients.delete(client);
      return;
    }
  }
}

export function find(ws: WebSocket): ClientConnection | undefined {
  for (const client of clients) {
    if (client.ws === ws) return client;
  }
  return undefined;
}

export function setFocus(ws: WebSocket, sessionId: string | null): void {
  const client = find(ws);
  if (client) {
    client.focusedSessionId = sessionId;
  }
}

export function broadcast(message: string): void {
  for (const client of clients) {
    client.ws.send(message);
  }
}

export function count(): number {
  return clients.size;
}
