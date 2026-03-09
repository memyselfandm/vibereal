import type WebSocket from "ws";
import type { SessionRecord, SessionInfo, PendingApproval } from "./types.ts";

const sessions = new Map<string, SessionRecord>();

export function register(
  id: string,
  name: string,
  ws: WebSocket,
  metadata?: Record<string, string>
): SessionRecord {
  const record: SessionRecord = {
    id,
    name,
    type: metadata?.type || "claude_code",
    status: "idle",
    currentTask: "",
    ws,
    lastActivity: new Date(),
    pendingApprovals: [],
  };
  sessions.set(id, record);
  return record;
}

export function registerManaged(id: string, name: string): SessionRecord {
  const record: SessionRecord = {
    id,
    name,
    type: "managed",
    status: "starting",
    currentTask: "",
    ws: null,
    lastActivity: new Date(),
    pendingApprovals: [],
  };
  sessions.set(id, record);
  return record;
}

export function unregister(id: string): void {
  sessions.delete(id);
}

export function get(id: string): SessionRecord | undefined {
  return sessions.get(id);
}

export function getAll(): SessionRecord[] {
  return Array.from(sessions.values());
}

export function getAllInfo(): SessionInfo[] {
  return getAll().map((s) => ({
    id: s.id,
    name: s.name,
    type: s.type,
    status: s.status,
    lastActivity: s.lastActivity.toISOString(),
    currentTask: s.currentTask,
  }));
}

export function updateStatus(id: string, status: string, currentTask: string): void {
  const session = sessions.get(id);
  if (session) {
    session.status = status;
    session.currentTask = currentTask;
    session.lastActivity = new Date();
  }
}

export function markDisconnected(id: string): void {
  const session = sessions.get(id);
  if (session) {
    session.status = "disconnected";
    session.ws = null;
    session.lastActivity = new Date();
  }
}

export function addApproval(id: string, approval: PendingApproval): void {
  const session = sessions.get(id);
  if (session) {
    session.pendingApprovals.push(approval);
    session.lastActivity = new Date();
  }
}

export function removeApproval(id: string, approvalId: string): PendingApproval | undefined {
  const session = sessions.get(id);
  if (!session) return undefined;
  const idx = session.pendingApprovals.findIndex((a) => a.approvalId === approvalId);
  if (idx === -1) return undefined;
  return session.pendingApprovals.splice(idx, 1)[0];
}

export function count(): number {
  return sessions.size;
}
