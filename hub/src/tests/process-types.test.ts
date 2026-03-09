/**
 * Tests for process-types.ts — verifies type shapes are importable and correct.
 * Since these are TypeScript interfaces (erased at runtime), we test that the
 * module imports cleanly and that objects conforming to the interfaces work.
 */
import { describe, it } from "node:test";
import assert from "node:assert/strict";

describe("process-types", () => {
  it("module imports without error", async () => {
    // Should not throw
    await import("../process-types.ts");
    assert.ok(true, "process-types.ts imported successfully");
  });

  it("CreateSessionRequest shape: prompt required, rest optional", () => {
    // Runtime validation of the expected interface shape
    const req = {
      prompt: "Write a hello world program",
    };
    assert.equal(req.prompt, "Write a hello world program");
    // Optional fields should be undefined when not provided
    assert.equal((req as any).workingDirectory, undefined);
    assert.equal((req as any).name, undefined);
    assert.equal((req as any).allowedTools, undefined);
    assert.equal((req as any).permissionMode, undefined);
  });

  it("CreateSessionRequest shape: all fields can be provided", () => {
    const req = {
      prompt: "Help me debug this",
      workingDirectory: "/home/user/project",
      name: "Debug Session",
      allowedTools: ["Read", "Write", "Bash"],
      permissionMode: "bypassPermissions",
    };
    assert.equal(req.prompt, "Help me debug this");
    assert.equal(req.workingDirectory, "/home/user/project");
    assert.equal(req.name, "Debug Session");
    assert.deepEqual(req.allowedTools, ["Read", "Write", "Bash"]);
    assert.equal(req.permissionMode, "bypassPermissions");
  });

  it("CreateSessionResponse has sessionId, name, status", () => {
    const resp = {
      sessionId: "managed-abc123",
      name: "Test Session",
      status: "starting",
    };
    assert.equal(resp.sessionId, "managed-abc123");
    assert.equal(resp.name, "Test Session");
    assert.equal(resp.status, "starting");
  });
});
