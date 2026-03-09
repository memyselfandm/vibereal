/**
 * Tests for api-helpers.ts
 * Run: node --experimental-transform-types --test src/api-helpers.test.ts
 */

import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { IncomingMessage, ServerResponse } from "node:http";
import { Socket } from "node:net";
import { readBody, sendJson, sendError, checkAuth } from "./api-helpers.ts";

// ---------------------------------------------------------------------------
// Helpers to build minimal IncomingMessage / ServerResponse mocks
// ---------------------------------------------------------------------------

function makeReq(opts: {
  url?: string;
  headers?: Record<string, string>;
  body?: string;
}): IncomingMessage {
  const socket = new Socket();
  const req = new IncomingMessage(socket);
  req.url = opts.url ?? "/";
  Object.assign(req.headers, opts.headers ?? {});

  // If a body string is given, push it through the readable stream
  if (opts.body !== undefined) {
    // Schedule emission after current tick so listeners can attach
    setImmediate(() => {
      req.push(opts.body);
      req.push(null);
    });
  }

  return req;
}

interface CapturedResponse {
  statusCode: number;
  headers: Record<string, string | string[]>;
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

  // Intercept writeHead — capture status and headers passed inline
  const origWriteHead = res.writeHead.bind(res);
  res.writeHead = (status: number, headers?: any) => {
    captured.statusCode = status;
    if (headers) {
      // Normalize header names to lowercase for consistent assertion
      for (const [k, v] of Object.entries(headers)) {
        captured.headers[k.toLowerCase()] = v as string | string[];
      }
    }
    return origWriteHead(status, headers);
  };

  // Intercept end
  res.end = (chunk?: any) => {
    if (chunk) captured.body += chunk.toString();
    captured.ended = true;
    return res;
  };

  // Intercept setHeader
  const origSetHeader = res.setHeader.bind(res);
  res.setHeader = (name: string, value: any) => {
    captured.headers[name.toLowerCase()] = value;
    return origSetHeader(name, value);
  };

  return { res, captured };
}

// ---------------------------------------------------------------------------
// readBody
// ---------------------------------------------------------------------------

describe("readBody", () => {
  it("resolves with the full request body string", async () => {
    const req = makeReq({ body: '{"hello":"world"}' });
    const body = await readBody(req);
    assert.equal(body, '{"hello":"world"}');
  });

  it("resolves with empty string for an empty body", async () => {
    const req = makeReq({ body: "" });
    const body = await readBody(req);
    assert.equal(body, "");
  });

  it("rejects when the stream emits an error", async () => {
    const socket = new Socket();
    const req = new IncomingMessage(socket);
    setImmediate(() => req.destroy(new Error("stream failure")));

    await assert.rejects(readBody(req), /stream failure/);
  });
});

// ---------------------------------------------------------------------------
// sendJson
// ---------------------------------------------------------------------------

describe("sendJson", () => {
  it("writes the correct status code", () => {
    const { res, captured } = makeRes();
    sendJson(res, 200, { ok: true });
    assert.equal(captured.statusCode, 200);
  });

  it("sets Content-Type to application/json", () => {
    const { res, captured } = makeRes();
    sendJson(res, 200, { ok: true });
    assert.equal(captured.headers["content-type"], "application/json");
  });

  it("serializes the data as JSON in the body", () => {
    const { res, captured } = makeRes();
    sendJson(res, 200, { sessions: [] });
    assert.equal(captured.body, JSON.stringify({ sessions: [] }));
  });

  it("sets Content-Length matching the body byte length", () => {
    const { res, captured } = makeRes();
    const data = { key: "value" };
    sendJson(res, 201, data);
    const expected = Buffer.byteLength(JSON.stringify(data));
    assert.equal(Number(captured.headers["content-length"]), expected);
  });

  it("ends the response", () => {
    const { res, captured } = makeRes();
    sendJson(res, 200, {});
    assert.ok(captured.ended);
  });
});

// ---------------------------------------------------------------------------
// sendError
// ---------------------------------------------------------------------------

describe("sendError", () => {
  it("writes the correct HTTP status", () => {
    const { res, captured } = makeRes();
    sendError(res, 404, "NOT_FOUND", "Session not found");
    assert.equal(captured.statusCode, 404);
  });

  it("wraps error in { error: { code, message } } envelope", () => {
    const { res, captured } = makeRes();
    sendError(res, 400, "BAD_REQUEST", "Invalid input");
    const parsed = JSON.parse(captured.body);
    assert.deepEqual(parsed, {
      error: { code: "BAD_REQUEST", message: "Invalid input" },
    });
  });

  it("ends the response", () => {
    const { res, captured } = makeRes();
    sendError(res, 500, "SERVER_ERROR", "oops");
    assert.ok(captured.ended);
  });
});

// ---------------------------------------------------------------------------
// checkAuth
// ---------------------------------------------------------------------------

describe("checkAuth", () => {
  it("returns true when apiKey is empty (auth disabled)", () => {
    const req = makeReq({});
    assert.ok(checkAuth(req, ""));
  });

  it("returns true when Bearer token matches apiKey", () => {
    const req = makeReq({ headers: { authorization: "Bearer secret123" }, url: "/" });
    assert.ok(checkAuth(req, "secret123"));
  });

  it("returns false when Bearer token does not match", () => {
    const req = makeReq({ headers: { authorization: "Bearer wrong" }, url: "/" });
    assert.equal(checkAuth(req, "secret123"), false);
  });

  it("returns true when apiKey query param matches", () => {
    const req = makeReq({ url: "/api/sessions?apiKey=secret123", headers: { host: "localhost" } });
    assert.ok(checkAuth(req, "secret123"));
  });

  it("returns false when query param does not match", () => {
    const req = makeReq({ url: "/api/sessions?apiKey=bad", headers: { host: "localhost" } });
    assert.equal(checkAuth(req, "secret123"), false);
  });

  it("prefers Bearer header over query param when both present", () => {
    const req = makeReq({
      url: "/api/sessions?apiKey=bad",
      headers: { authorization: "Bearer secret123", host: "localhost" },
    });
    assert.ok(checkAuth(req, "secret123"));
  });

  it("returns false when neither header nor query param provided and key is set", () => {
    const req = makeReq({ url: "/", headers: { host: "localhost" } });
    assert.equal(checkAuth(req, "secret123"), false);
  });
});
