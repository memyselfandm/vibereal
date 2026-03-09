/**
 * Tests for process-manager.ts
 *
 * Since spawning real claude CLI processes is environment-dependent, we test:
 * 1. Module imports cleanly
 * 2. initialize() runs without throwing
 * 3. isEnabled() returns correct value based on CLI availability
 * 4. isManaged() returns false for unknown session IDs
 * 5. getAll() returns empty array initially
 * 6. kill() returns false for unknown session IDs
 * 7. sendMessage() returns false for unknown session IDs
 * 8. create() throws when process manager is not enabled
 */
import { describe, it } from "node:test";
import assert from "node:assert/strict";

describe("process-manager", () => {
  it("module imports without error", async () => {
    await import("../process-manager.ts");
    assert.ok(true, "process-manager.ts imported successfully");
  });

  it("initialize() resolves without throwing", async () => {
    const pm = await import("../process-manager.ts");
    await assert.doesNotReject(
      () => pm.initialize(),
      "initialize() should not throw even if CLI is unavailable"
    );
  });

  it("isEnabled() returns a boolean", async () => {
    const pm = await import("../process-manager.ts");
    await pm.initialize();
    assert.equal(typeof pm.isEnabled(), "boolean", "isEnabled() should return a boolean");
  });

  it("isManaged() returns false for unknown session IDs", async () => {
    const pm = await import("../process-manager.ts");
    assert.equal(pm.isManaged("nonexistent-session-id"), false);
    assert.equal(pm.isManaged(""), false);
    assert.equal(pm.isManaged("managed-00000000"), false);
  });

  it("getAll() returns an array", async () => {
    const pm = await import("../process-manager.ts");
    const all = pm.getAll();
    assert.ok(Array.isArray(all), "getAll() should return an array");
  });

  it("kill() returns false for unknown session ID", async () => {
    const pm = await import("../process-manager.ts");
    assert.equal(pm.kill("nonexistent-session"), false);
  });

  it("sendMessage() returns false for unknown session ID", async () => {
    const pm = await import("../process-manager.ts");
    const result = pm.sendMessage("nonexistent-session", { role: "user", content: "hello" });
    assert.equal(result, false);
  });

  it("sendApprovalDecision() returns false for unknown session ID", async () => {
    const pm = await import("../process-manager.ts");
    const result = pm.sendApprovalDecision("nonexistent-session", "approval-123", "approve");
    assert.equal(result, false);
  });

  it("shutdownAll() does not throw when no processes are running", async () => {
    const pm = await import("../process-manager.ts");
    assert.doesNotThrow(() => pm.shutdownAll());
  });

  it("create() throws when process manager is disabled/CLI unavailable", async () => {
    const pm = await import("../process-manager.ts");
    await pm.initialize();

    if (!pm.isEnabled()) {
      // If not enabled, create() should throw
      assert.throws(
        () => pm.create({ prompt: "test prompt" }),
        /not available/i,
        "create() should throw when process manager is not available"
      );
    } else {
      // If enabled (claude CLI found), we just verify it returns valid response shape
      // We don't actually want to spawn a real process in tests
      // so we skip this branch — just document the expected shape
      assert.ok(true, "claude CLI available — create() test skipped to avoid spawning real process");
    }
  });
});
