import type { IncomingMessage, ServerResponse } from "node:http";
import { config } from "./config.ts";
import { readBody, sendJson, sendError, checkAuth } from "./api-helpers.ts";
import * as sessionRegistry from "./session-registry.ts";
import * as _processManager from "./process-manager.ts";
import type { CreateSessionRequest } from "./process-types.ts";

/**
 * Dependency seam for process manager — replaceable in tests.
 */
export const deps = {
  processManager: _processManager as {
    isEnabled(): boolean;
    isManaged(sessionId: string): boolean;
    create(request: CreateSessionRequest): { sessionId: string; name: string; status: string };
    kill(sessionId: string): boolean;
  },
};

/**
 * Handle all /api/* requests. Returns true if the request was handled.
 */
export function handleApiRequest(req: IncomingMessage, res: ServerResponse): boolean {
  const url = new URL(req.url || "/", `http://${req.headers.host || "localhost"}`);
  if (!url.pathname.startsWith("/api/")) return false;
  if (url.pathname.startsWith("/api/registry/")) return false;

  // CORS headers
  res.setHeader("Access-Control-Allow-Origin", "*");
  res.setHeader("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

  if (req.method === "OPTIONS") {
    res.writeHead(204);
    res.end();
    return true;
  }

  // Auth check
  if (!checkAuth(req, config.apiKey)) {
    sendError(res, 401, "UNAUTHORIZED", "Invalid or missing API key");
    return true;
  }

  const path = url.pathname;
  const method = req.method || "GET";

  // GET /api/sessions
  if (path === "/api/sessions" && method === "GET") {
    const sessions = sessionRegistry.getAllInfo();
    sendJson(res, 200, { sessions });
    return true;
  }

  // POST /api/sessions
  if (path === "/api/sessions" && method === "POST") {
    handleCreateSession(req, res);
    return true;
  }

  // Routes with session id: /api/sessions/:id
  const sessionDetailMatch = path.match(/^\/api\/sessions\/([^/]+)$/);

  // GET /api/sessions/:id
  if (sessionDetailMatch && method === "GET") {
    const id = decodeURIComponent(sessionDetailMatch[1]);
    const session = sessionRegistry.get(id);
    if (!session) {
      sendError(res, 404, "NOT_FOUND", `Session ${id} not found`);
      return true;
    }
    sendJson(res, 200, {
      id: session.id,
      name: session.name,
      type: session.type,
      status: session.status,
      lastActivity: session.lastActivity.toISOString(),
      currentTask: session.currentTask,
      pendingApprovals: session.pendingApprovals.map((a) => ({
        approvalId: a.approvalId,
        toolName: a.toolName,
        description: a.description,
        timestamp: a.timestamp.toISOString(),
      })),
    });
    return true;
  }

  // DELETE /api/sessions/:id
  if (sessionDetailMatch && method === "DELETE") {
    const id = decodeURIComponent(sessionDetailMatch[1]);
    handleDeleteSession(id, res);
    return true;
  }

  // Unknown /api/ route
  sendError(res, 404, "NOT_FOUND", `Unknown API endpoint: ${method} ${path}`);
  return true;
}

async function handleCreateSession(req: IncomingMessage, res: ServerResponse): Promise<void> {
  let body: string;
  try {
    body = await readBody(req);
  } catch {
    sendError(res, 400, "BAD_REQUEST", "Failed to read request body");
    return;
  }

  let parsed: Partial<CreateSessionRequest>;
  try {
    parsed = JSON.parse(body || "{}");
  } catch {
    sendError(res, 400, "BAD_REQUEST", "Invalid JSON in request body");
    return;
  }

  if (!parsed.prompt) {
    sendError(res, 400, "BAD_REQUEST", "Missing required field: prompt");
    return;
  }

  if (!deps.processManager.isEnabled()) {
    sendError(res, 503, "SERVICE_UNAVAILABLE", "Process manager is not available");
    return;
  }

  try {
    const request: CreateSessionRequest = {
      prompt: parsed.prompt,
      workingDirectory: parsed.workingDirectory,
      name: parsed.name,
      allowedTools: parsed.allowedTools,
      permissionMode: parsed.permissionMode,
    };
    const result = deps.processManager.create(request);
    sendJson(res, 201, result);
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    sendError(res, 500, "INTERNAL_ERROR", message);
  }
}

function handleDeleteSession(id: string, res: ServerResponse): void {
  if (deps.processManager.isManaged(id)) {
    deps.processManager.kill(id);
    sessionRegistry.unregister(id);
    sendJson(res, 200, { status: "killed", sessionId: id });
    return;
  }

  const session = sessionRegistry.get(id);
  if (session) {
    sendError(res, 400, "BAD_REQUEST", "Cannot kill external session");
    return;
  }

  sendError(res, 404, "NOT_FOUND", `Session ${id} not found`);
}
