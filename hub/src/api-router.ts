import type { IncomingMessage, ServerResponse } from "node:http";
import { config } from "./config.ts";
import { sendJson, sendError, checkAuth } from "./api-helpers.ts";
import * as sessionRegistry from "./session-registry.ts";

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

  // POST /api/sessions — stub (process manager not yet wired)
  if (path === "/api/sessions" && method === "POST") {
    sendError(res, 501, "NOT_IMPLEMENTED", "Process manager not yet available");
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

  // DELETE /api/sessions/:id — stub
  if (sessionDetailMatch && method === "DELETE") {
    sendError(res, 501, "NOT_IMPLEMENTED", "Process manager not yet available");
    return true;
  }

  // Unknown /api/ route
  sendError(res, 404, "NOT_FOUND", `Unknown API endpoint: ${method} ${path}`);
  return true;
}
