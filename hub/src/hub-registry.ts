import type { IncomingMessage, ServerResponse } from "node:http";
import { sendJson, sendError, checkAuth, readBody } from "./api-helpers.ts";
import { config } from "./config.ts";

export interface HubRecord {
  id: string;
  url: string;
  name: string;
  capabilities: string[];
  lastHeartbeat: Date;
  registeredAt: Date;
  status: "healthy" | "degraded" | "unhealthy";
}

const hubs = new Map<string, HubRecord>();
let pruneInterval: ReturnType<typeof setInterval> | null = null;

/**
 * Handle /api/registry/* requests. Returns true if handled.
 */
export function handleRegistryRequest(req: IncomingMessage, res: ServerResponse): boolean {
  const url = new URL(req.url || "/", `http://${req.headers.host}`);
  const path = url.pathname;
  const method = req.method || "GET";

  if (!path.startsWith("/api/registry/")) return false;

  // CORS
  res.setHeader("Access-Control-Allow-Origin", "*");
  res.setHeader("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

  if (method === "OPTIONS") {
    res.writeHead(204);
    res.end();
    return true;
  }

  // Auth check
  if (!checkAuth(req, config.apiKey)) {
    sendError(res, 401, "UNAUTHORIZED", "Invalid or missing API key");
    return true;
  }

  // GET /api/registry/hubs — list all hubs
  if (path === "/api/registry/hubs" && method === "GET") {
    const hubList = Array.from(hubs.values()).map((h) => ({
      ...h,
      status: computeStatus(h),
      lastHeartbeat: h.lastHeartbeat.toISOString(),
      registeredAt: h.registeredAt.toISOString(),
    }));
    sendJson(res, 200, { hubs: hubList });
    return true;
  }

  // POST /api/registry/hubs — register or heartbeat
  if (path === "/api/registry/hubs" && method === "POST") {
    readBody(req)
      .then((body) => {
        let data: any;
        try {
          data = JSON.parse(body);
        } catch {
          sendError(res, 400, "INVALID_JSON", "Request body must be valid JSON");
          return;
        }

        if (!data.url || !data.name) {
          sendError(res, 400, "MISSING_FIELDS", "url and name are required");
          return;
        }

        // Upsert by URL
        let existing: HubRecord | undefined;
        for (const h of hubs.values()) {
          if (h.url === data.url) {
            existing = h;
            break;
          }
        }

        if (existing) {
          existing.name = data.name;
          existing.capabilities = data.capabilities || existing.capabilities;
          existing.lastHeartbeat = new Date();
          existing.status = "healthy";
          sendJson(res, 200, { id: existing.id, status: "updated" });
        } else {
          const id = `hub-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 6)}`;
          const record: HubRecord = {
            id,
            url: data.url,
            name: data.name,
            capabilities: data.capabilities || ["relay"],
            lastHeartbeat: new Date(),
            registeredAt: new Date(),
            status: "healthy",
          };
          hubs.set(id, record);
          console.log(`[registry] Hub registered: ${record.name} (${record.url})`);
          sendJson(res, 201, { id, status: "registered" });
        }
      })
      .catch((err) => {
        sendError(res, 500, "INTERNAL_ERROR", err.message);
      });
    return true;
  }

  // Specific hub routes: /api/registry/hubs/:id
  const hubIdMatch = path.match(/^\/api\/registry\/hubs\/([^/]+)$/);
  if (hubIdMatch) {
    const id = decodeURIComponent(hubIdMatch[1]);

    // DELETE /api/registry/hubs/:id
    if (method === "DELETE") {
      if (hubs.has(id)) {
        const hub = hubs.get(id)!;
        hubs.delete(id);
        console.log(`[registry] Hub removed: ${hub.name} (${hub.url})`);
        sendJson(res, 200, { status: "removed" });
      } else {
        sendError(res, 404, "NOT_FOUND", `Hub ${id} not found`);
      }
      return true;
    }

    // GET /api/registry/hubs/:id
    if (method === "GET") {
      const hub = hubs.get(id);
      if (!hub) {
        sendError(res, 404, "NOT_FOUND", `Hub ${id} not found`);
        return true;
      }
      sendJson(res, 200, {
        ...hub,
        status: computeStatus(hub),
        lastHeartbeat: hub.lastHeartbeat.toISOString(),
        registeredAt: hub.registeredAt.toISOString(),
      });
      return true;
    }
  }

  sendError(res, 404, "NOT_FOUND", `Unknown registry endpoint: ${method} ${path}`);
  return true;
}

function computeStatus(hub: HubRecord): "healthy" | "degraded" | "unhealthy" {
  const elapsed = Date.now() - hub.lastHeartbeat.getTime();
  if (elapsed < 45_000) return "healthy";
  if (elapsed < 90_000) return "degraded";
  return "unhealthy";
}

export function startPruning(): void {
  if (pruneInterval) return;
  pruneInterval = setInterval(() => {
    const now = Date.now();
    for (const [id, hub] of hubs) {
      const elapsed = now - hub.lastHeartbeat.getTime();
      if (elapsed > 180_000) {
        console.log(
          `[registry] Pruning hub: ${hub.name} (${hub.url}) — no heartbeat for ${Math.round(elapsed / 1000)}s`,
        );
        hubs.delete(id);
      }
    }
  }, 30_000);
  // Don't block process exit
  pruneInterval.unref();
}

export function stopPruning(): void {
  if (pruneInterval) {
    clearInterval(pruneInterval);
    pruneInterval = null;
  }
}

export function count(): number {
  return hubs.size;
}
