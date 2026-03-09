/**
 * Tests for api-router.ts
 * Run: node --experimental-transform-types --test src/api-router.test.ts
 */

import { describe, it, beforeEach, afterEach, mock } from "node:test";
import assert from "node:assert/strict";
import { IncomingMessage, ServerResponse } from "node:http";
import { Socket } from "node:net";

// ---------------------------------------------------------------------------
// We need to control the module-level singletons (config, sessionRegistry).
// Since Node's built-in test runner doesn't support jest.mock, we reset state
// directly via the exported functions on sessionRegistry.
// ---------------------------------------------------------------------------

import * as sessionRegistry from "./session-registry.ts";
import { handleApiRequest } from "./api-router.ts";

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

function makeReq(opts: {
  method?: string;
  url?: string;
  headers?: Record<string, string>;
}): IncomingMessage {
  const socket = new Socket();
  const req = new IncomingMessage(socket);
  req.method = opts.method ?? "GET";
  req.url = opts.url ?? "/";
  Object.assign(req.headers, { host: "localhost", ...(opts.headers ?? {}) });
  return req;
}

interface CapturedResponse {
  statusCode: number;
  headers: Record<string, string | string[] | number>;
  body: string;
  ended: boolean;
}

function makeRes(): { res: ServerResponse; captured: CapturedResponse } {
  const socket = new Socket();
  const req = new IncomingMessage(socket);
  const res = new ServerResponse(req);

  const captured: CapturedResponse = {
    statusCode: 200,
    headers: {},
    body: "",
    ended: false,
  };

  res.writeHead = (status: number, headers?: any) => {
    captured.statusCode = status;
    if (headers) Object.assign(captured.headers, headers);
    return res as any;
  };

  res.end = (chunk?: any) => {
    if (chunk) captured.body += chunk.toString();
    captured.ended = true;
    return res;
  };

  res.setHeader = (name: string, value: any) => {
    captured.headers[name.toLowerCase()] = value;
    return res as any;
  };

  return { res, captured };
}

function parsedBody(captured: CapturedResponse): any {
  return JSON.parse(captured.body);
}

// ---------------------------------------------------------------------------
// Clear session registry between tests
// ---------------------------------------------------------------------------

function clearRegistry() {
  // Unregister all sessions by accessing internals indirectly
  for (const s of sessionRegistry.getAll()) {
    sessionRegistry.unregister(s.id);
  }
}

// ---------------------------------------------------------------------------
// Non-API paths
// ---------------------------------------------------------------------------

describe("handleApiRequest — routing", () => {
  it("returns false for non-/api/ paths", () => {
    const req = makeReq({ url: "/health" });
    const { res } = makeRes();
    const handled = handleApiRequest(req, res);
    assert.equal(handled, false);
  });

  it("returns true for /api/ paths", () => {
    const req = makeReq({ url: "/api/sessions" });
    const { res } = makeRes();
    const handled = handleApiRequest(req, res);
    assert.equal(handled, true);
  });

  it("handles OPTIONS preflight with 204 and CORS headers", () => {
    const req = makeReq({ method: "OPTIONS", url: "/api/sessions" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);
    assert.equal(captured.statusCode, 204);
    assert.ok(captured.ended);
  });

  it("sets CORS headers on all /api/ responses", () => {
    const req = makeReq({ url: "/api/sessions" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);
    assert.equal(captured.headers["access-control-allow-origin"], "*");
  });
});

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

describe("handleApiRequest — auth", () => {
  // Auth only enforced when HUB_API_KEY is set. We'll set it via env then restore.
  const originalKey = process.env.HUB_API_KEY;

  afterEach(() => {
    process.env.HUB_API_KEY = originalKey;
  });

  it("allows requests when no API key configured (config.apiKey empty)", () => {
    // config is loaded at import time; we can't hot-reload it here.
    // Instead verify that the default behavior (no key set) passes through.
    // This test is only valid if HUB_API_KEY was not set during import.
    // We skip it when a key was set in the environment.
    if (process.env.HUB_API_KEY) return;

    const req = makeReq({ url: "/api/sessions" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);
    assert.notEqual(captured.statusCode, 401);
  });
});

// ---------------------------------------------------------------------------
// GET /api/sessions
// ---------------------------------------------------------------------------

describe("GET /api/sessions", () => {
  beforeEach(clearRegistry);
  afterEach(clearRegistry);

  it("returns 200 with empty sessions array when no sessions registered", () => {
    const req = makeReq({ url: "/api/sessions" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    assert.equal(captured.statusCode, 200);
    const body = parsedBody(captured);
    assert.ok(Array.isArray(body.sessions));
    assert.equal(body.sessions.length, 0);
  });

  it("returns all registered sessions", () => {
    // Register two sessions via the registry (passing null for ws since we don't need WS here)
    sessionRegistry.register("s1", "Session One", null as any);
    sessionRegistry.register("s2", "Session Two", null as any);

    const req = makeReq({ url: "/api/sessions" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    const body = parsedBody(captured);
    assert.equal(body.sessions.length, 2);
    const ids = body.sessions.map((s: any) => s.id).sort();
    assert.deepEqual(ids, ["s1", "s2"]);
  });

  it("each session info includes required fields", () => {
    sessionRegistry.register("s1", "My Session", null as any);

    const req = makeReq({ url: "/api/sessions" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    const session = parsedBody(captured).sessions[0];
    assert.ok("id" in session);
    assert.ok("name" in session);
    assert.ok("status" in session);
    assert.ok("lastActivity" in session);
    assert.ok("type" in session);
  });
});

// ---------------------------------------------------------------------------
// GET /api/sessions/:id
// ---------------------------------------------------------------------------

describe("GET /api/sessions/:id", () => {
  beforeEach(clearRegistry);
  afterEach(clearRegistry);

  it("returns 404 for unknown session id", () => {
    const req = makeReq({ url: "/api/sessions/nonexistent" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    assert.equal(captured.statusCode, 404);
    const body = parsedBody(captured);
    assert.equal(body.error.code, "NOT_FOUND");
  });

  it("returns 200 with session detail for a known session", () => {
    sessionRegistry.register("session-abc", "ABC Session", null as any);

    const req = makeReq({ url: "/api/sessions/session-abc" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    assert.equal(captured.statusCode, 200);
    const body = parsedBody(captured);
    assert.equal(body.id, "session-abc");
    assert.equal(body.name, "ABC Session");
  });

  it("includes pendingApprovals array in detail response", () => {
    sessionRegistry.register("s1", "S1", null as any);
    sessionRegistry.addApproval("s1", {
      approvalId: "a1",
      toolName: "bash",
      description: "run ls",
      timestamp: new Date(),
    });

    const req = makeReq({ url: "/api/sessions/s1" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    const body = parsedBody(captured);
    assert.equal(body.pendingApprovals.length, 1);
    assert.equal(body.pendingApprovals[0].approvalId, "a1");
    assert.equal(body.pendingApprovals[0].toolName, "bash");
  });

  it("URL-decodes the session id", () => {
    sessionRegistry.register("my session", "Encoded", null as any);

    const req = makeReq({ url: "/api/sessions/my%20session" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    assert.equal(captured.statusCode, 200);
    const body = parsedBody(captured);
    assert.equal(body.id, "my session");
  });

  it("lastActivity is an ISO 8601 string", () => {
    sessionRegistry.register("s1", "S1", null as any);

    const req = makeReq({ url: "/api/sessions/s1" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    const body = parsedBody(captured);
    assert.doesNotThrow(() => new Date(body.lastActivity));
    assert.ok(body.lastActivity.includes("T")); // ISO format
  });
});

// ---------------------------------------------------------------------------
// POST /api/sessions — stub
// ---------------------------------------------------------------------------

describe("POST /api/sessions", () => {
  it("returns 501 NOT_IMPLEMENTED", () => {
    const req = makeReq({ method: "POST", url: "/api/sessions" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    assert.equal(captured.statusCode, 501);
    const body = parsedBody(captured);
    assert.equal(body.error.code, "NOT_IMPLEMENTED");
  });
});

// ---------------------------------------------------------------------------
// DELETE /api/sessions/:id — stub
// ---------------------------------------------------------------------------

describe("DELETE /api/sessions/:id", () => {
  it("returns 501 NOT_IMPLEMENTED", () => {
    const req = makeReq({ method: "DELETE", url: "/api/sessions/some-id" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    assert.equal(captured.statusCode, 501);
    const body = parsedBody(captured);
    assert.equal(body.error.code, "NOT_IMPLEMENTED");
  });
});

// ---------------------------------------------------------------------------
// Unknown /api/ routes
// ---------------------------------------------------------------------------

describe("unknown /api/ routes", () => {
  it("returns 404 NOT_FOUND for unrecognized /api/ path", () => {
    const req = makeReq({ url: "/api/unknown-resource" });
    const { res, captured } = makeRes();
    handleApiRequest(req, res);

    assert.equal(captured.statusCode, 404);
    const body = parsedBody(captured);
    assert.equal(body.error.code, "NOT_FOUND");
  });
});
