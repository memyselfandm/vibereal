/**
 * Tests for hub-registry.ts and hub-registry-client.ts
 *
 * Uses Node.js built-in test runner (node:test).
 * Run: node --experimental-transform-types --test test/hub-registry.test.ts
 */

import { test, describe, afterEach } from "node:test";
import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import { createServer, type IncomingMessage, type ServerResponse } from "node:http";
import type { AddressInfo } from "node:net";

// ---------------------------------------------------------------------------
// Helpers: minimal fake IncomingMessage / ServerResponse for unit tests
// ---------------------------------------------------------------------------

function makeReq(
  method: string,
  path: string,
  body = "",
  headers: Record<string, string> = {},
): IncomingMessage {
  const req = new EventEmitter() as IncomingMessage;
  req.method = method;
  req.url = path;
  req.headers = { host: "localhost", ...headers };

  // Simulate body streaming on next tick
  setImmediate(() => {
    if (body) {
      req.emit("data", Buffer.from(body));
    }
    req.emit("end");
  });

  return req;
}

function makeRes(): { res: ServerResponse; status: () => number; body: () => string } {
  let statusCode = 200;
  let responseBody = "";

  const res = {
    writeHead(code: number) {
      statusCode = code;
    },
    end(data?: string) {
      if (data) responseBody = data;
    },
    setHeader() {},
  } as unknown as ServerResponse;

  return {
    res,
    status: () => statusCode,
    body: () => responseBody,
  };
}

// ---------------------------------------------------------------------------
// api-helpers tests
// ---------------------------------------------------------------------------

describe("api-helpers", async () => {
  const { sendJson, sendError, checkAuth } = await import("../src/api-helpers.ts");

  test("sendJson writes status and JSON body", () => {
    const { res, status, body } = makeRes();
    sendJson(res, 201, { hello: "world" });
    assert.equal(status(), 201);
    assert.deepEqual(JSON.parse(body()), { hello: "world" });
  });

  test("sendError wraps in error envelope", () => {
    const { res, body } = makeRes();
    sendError(res, 400, "BAD_REQUEST", "missing field");
    const parsed = JSON.parse(body());
    assert.equal(parsed.error.code, "BAD_REQUEST");
    assert.equal(parsed.error.message, "missing field");
  });

  test("checkAuth returns true when no apiKey configured", () => {
    const req = makeReq("GET", "/");
    assert.equal(checkAuth(req, ""), true);
  });

  test("checkAuth accepts valid Bearer token", () => {
    const req = makeReq("GET", "/", "", { authorization: "Bearer secret123" });
    assert.equal(checkAuth(req, "secret123"), true);
  });

  test("checkAuth rejects wrong Bearer token", () => {
    const req = makeReq("GET", "/", "", { authorization: "Bearer wrong" });
    assert.equal(checkAuth(req, "secret123"), false);
  });

  test("checkAuth accepts apiKey query param", () => {
    const req = makeReq("GET", "/?apiKey=secret123");
    assert.equal(checkAuth(req, "secret123"), true);
  });
});

// ---------------------------------------------------------------------------
// hub-registry tests
// ---------------------------------------------------------------------------

describe("hub-registry", async () => {
  const registry = await import("../src/hub-registry.ts");

  afterEach(() => {
    registry.stopPruning();
  });

  test("handleRegistryRequest returns false for non-registry paths", () => {
    const req = makeReq("GET", "/health");
    const { res } = makeRes();
    const handled = registry.handleRegistryRequest(req, res);
    assert.equal(handled, false);
  });

  test("OPTIONS request returns 204", () => {
    const req = makeReq("OPTIONS", "/api/registry/hubs");
    const { res, status } = makeRes();
    registry.handleRegistryRequest(req, res);
    assert.equal(status(), 204);
  });

  test("GET /api/registry/hubs returns hub list", () => {
    const req = makeReq("GET", "/api/registry/hubs");
    const { res, status, body } = makeRes();
    registry.handleRegistryRequest(req, res);
    assert.equal(status(), 200);
    const parsed = JSON.parse(body());
    assert.ok(Array.isArray(parsed.hubs));
  });

  test("POST /api/registry/hubs registers a new hub", async () => {
    const payload = JSON.stringify({ url: "http://hub1.local:8080", name: "hub1", capabilities: ["relay"] });
    const req = makeReq("POST", "/api/registry/hubs", payload, { "content-type": "application/json" });
    const { res, status, body } = makeRes();

    registry.handleRegistryRequest(req, res);

    // Wait for async body reading
    await new Promise((r) => setTimeout(r, 50));

    assert.equal(status(), 201);
    const parsed = JSON.parse(body());
    assert.ok(parsed.id);
    assert.equal(parsed.status, "registered");
    assert.ok(registry.count() >= 1);
  });

  test("POST /api/registry/hubs with same URL updates existing hub", async () => {
    const url = "http://hub-update-test.local:8080";

    const req1 = makeReq("POST", "/api/registry/hubs", JSON.stringify({ url, name: "hub-orig" }));
    const { res: res1 } = makeRes();
    registry.handleRegistryRequest(req1, res1);
    await new Promise((r) => setTimeout(r, 50));

    const req2 = makeReq("POST", "/api/registry/hubs", JSON.stringify({ url, name: "hub-updated" }));
    const { res: res2, status: status2, body: body2 } = makeRes();
    registry.handleRegistryRequest(req2, res2);
    await new Promise((r) => setTimeout(r, 50));

    assert.equal(status2(), 200);
    const parsed2 = JSON.parse(body2());
    assert.equal(parsed2.status, "updated");
  });

  test("POST /api/registry/hubs returns 400 when url or name missing", async () => {
    const req = makeReq("POST", "/api/registry/hubs", JSON.stringify({ name: "no-url" }));
    const { res, status, body } = makeRes();
    registry.handleRegistryRequest(req, res);
    await new Promise((r) => setTimeout(r, 50));

    assert.equal(status(), 400);
    const parsed = JSON.parse(body());
    assert.equal(parsed.error.code, "MISSING_FIELDS");
  });

  test("POST /api/registry/hubs returns 400 on invalid JSON", async () => {
    const req = makeReq("POST", "/api/registry/hubs", "not-json");
    const { res, status, body } = makeRes();
    registry.handleRegistryRequest(req, res);
    await new Promise((r) => setTimeout(r, 50));

    assert.equal(status(), 400);
    const parsed = JSON.parse(body());
    assert.equal(parsed.error.code, "INVALID_JSON");
  });

  test("DELETE /api/registry/hubs/:id removes a hub", async () => {
    // Register first
    const payload = JSON.stringify({ url: "http://delete-me.local:9000", name: "delete-me" });
    const req1 = makeReq("POST", "/api/registry/hubs", payload);
    const { res: res1, body: body1 } = makeRes();
    registry.handleRegistryRequest(req1, res1);
    await new Promise((r) => setTimeout(r, 50));
    const { id } = JSON.parse(body1());

    // Then delete
    const req2 = makeReq("DELETE", `/api/registry/hubs/${id}`);
    const { res: res2, status: status2, body: body2 } = makeRes();
    registry.handleRegistryRequest(req2, res2);
    // DELETE is synchronous (no body read needed)
    await new Promise((r) => setTimeout(r, 10));

    assert.equal(status2(), 200);
    const parsed = JSON.parse(body2());
    assert.equal(parsed.status, "removed");
  });

  test("DELETE /api/registry/hubs/:id returns 404 for unknown id", () => {
    const req = makeReq("DELETE", "/api/registry/hubs/nonexistent-id");
    const { res, status, body } = makeRes();
    registry.handleRegistryRequest(req, res);

    assert.equal(status(), 404);
    const parsed = JSON.parse(body());
    assert.equal(parsed.error.code, "NOT_FOUND");
  });

  test("GET /api/registry/hubs/:id returns hub details", async () => {
    const payload = JSON.stringify({ url: "http://detail-hub.local:8080", name: "detail-hub" });
    const req1 = makeReq("POST", "/api/registry/hubs", payload);
    const { res: res1, body: body1 } = makeRes();
    registry.handleRegistryRequest(req1, res1);
    await new Promise((r) => setTimeout(r, 50));
    const { id } = JSON.parse(body1());

    const req2 = makeReq("GET", `/api/registry/hubs/${id}`);
    const { res: res2, status: status2, body: body2 } = makeRes();
    registry.handleRegistryRequest(req2, res2);

    assert.equal(status2(), 200);
    const parsed = JSON.parse(body2());
    assert.equal(parsed.name, "detail-hub");
    assert.equal(parsed.status, "healthy");
  });

  test("GET /api/registry/hubs/:id returns 404 for unknown id", () => {
    const req = makeReq("GET", "/api/registry/hubs/no-such-hub");
    const { res, status, body } = makeRes();
    registry.handleRegistryRequest(req, res);

    assert.equal(status(), 404);
    const parsed = JSON.parse(body());
    assert.equal(parsed.error.code, "NOT_FOUND");
  });

  test("unknown registry endpoint returns 404", () => {
    const req = makeReq("GET", "/api/registry/unknown");
    const { res, status, body } = makeRes();
    registry.handleRegistryRequest(req, res);

    assert.equal(status(), 404);
    const parsed = JSON.parse(body());
    assert.equal(parsed.error.code, "NOT_FOUND");
  });

  test("startPruning and stopPruning do not throw", () => {
    registry.startPruning();
    registry.startPruning(); // calling twice should be safe (no-op)
    registry.stopPruning();
    registry.stopPruning(); // calling twice should be safe (no-op)
  });

  test("count() returns a number", () => {
    assert.ok(typeof registry.count() === "number");
  });
});

// ---------------------------------------------------------------------------
// hub-registry-client tests
// ---------------------------------------------------------------------------

describe("hub-registry-client", async () => {
  const registryClient = await import("../src/hub-registry-client.ts");

  test("start() does nothing when REGISTRY_URL is not set", () => {
    // Ensure env is cleared
    delete process.env.REGISTRY_URL;
    assert.doesNotThrow(() => registryClient.start());
  });

  test("stop() resolves without error when not started", async () => {
    await assert.doesNotReject(() => registryClient.stop());
  });

  test("client registers and sends heartbeats to a live HTTP server", async () => {
    const received: any[] = [];

    const server = createServer((req: IncomingMessage, res: ServerResponse) => {
      let body = "";
      req.on("data", (chunk: Buffer) => (body += chunk.toString()));
      req.on("end", () => {
        received.push({ method: req.method, url: req.url, body: body ? JSON.parse(body) : null });
        res.writeHead(req.method === "POST" ? 201 : 200, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ id: "test-hub-id", status: "registered" }));
      });
    });

    await new Promise<void>((r) => server.listen(0, "127.0.0.1", r));
    const { port } = server.address() as AddressInfo;

    // Set env before calling start (config is read at call time via getHubInfo)
    process.env.REGISTRY_URL = `http://127.0.0.1:${port}`;
    process.env.HUB_URL = "http://hub-under-test.local:8080";
    process.env.HUB_NAME = "test-hub";

    registryClient.start();

    // Wait for initial registration request
    await new Promise((r) => setTimeout(r, 300));

    await registryClient.stop();

    // Restore env
    delete process.env.REGISTRY_URL;
    delete process.env.HUB_URL;
    delete process.env.HUB_NAME;

    await new Promise<void>((r) => server.close(() => r()));

    // Should have at least one POST (registration) and one DELETE (deregister on stop)
    const posts = received.filter((r) => r.method === "POST");
    assert.ok(posts.length >= 1, `Expected at least 1 POST, got ${posts.length}`);
    assert.equal(posts[0].body.name, "test-hub");
    assert.equal(posts[0].body.url, "http://hub-under-test.local:8080");
    assert.ok(Array.isArray(posts[0].body.capabilities));
  });
});
