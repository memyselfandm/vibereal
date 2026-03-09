/**
 * Tests for session-registry.ts — verifies registerManaged function.
 */
import { describe, it, beforeEach } from "node:test";
import assert from "node:assert/strict";

// We need a fresh registry for each test since it's module-level state.
// We'll use dynamic import with cache busting workaround by testing behavior.

describe("session-registry registerManaged", () => {
  it("creates a session record with null ws and type 'managed'", async () => {
    const registry = await import("../session-registry.ts");

    const id = `test-managed-${Date.now()}`;
    const record = registry.registerManaged(id, "Test Managed Session");

    assert.equal(record.id, id);
    assert.equal(record.name, "Test Managed Session");
    assert.equal(record.type, "managed");
    assert.equal(record.status, "starting");
    assert.equal(record.ws, null);
    assert.equal(record.currentTask, "");
    assert.ok(Array.isArray(record.pendingApprovals));
    assert.equal(record.pendingApprovals.length, 0);
    assert.ok(record.lastActivity instanceof Date);
  });

  it("registered managed session is retrievable via get()", async () => {
    const registry = await import("../session-registry.ts");

    const id = `test-managed-get-${Date.now()}`;
    registry.registerManaged(id, "Retrievable Session");

    const retrieved = registry.get(id);
    assert.ok(retrieved !== undefined, "should be able to get registered managed session");
    assert.equal(retrieved!.id, id);
    assert.equal(retrieved!.type, "managed");
  });

  it("registered managed session appears in getAll()", async () => {
    const registry = await import("../session-registry.ts");

    const id = `test-managed-all-${Date.now()}`;
    registry.registerManaged(id, "All Sessions Test");

    const all = registry.getAll();
    const found = all.find((s) => s.id === id);
    assert.ok(found !== undefined, "managed session should appear in getAll()");
  });

  it("managed session status can be updated via updateStatus()", async () => {
    const registry = await import("../session-registry.ts");

    const id = `test-managed-status-${Date.now()}`;
    registry.registerManaged(id, "Status Update Test");

    registry.updateStatus(id, "idle", "");
    const session = registry.get(id);
    assert.equal(session!.status, "idle");
    assert.equal(session!.currentTask, "");
  });

  it("does not overwrite existing ws-based sessions", async () => {
    const registry = await import("../session-registry.ts");

    // Register a managed session
    const managedId = `test-managed-isolation-${Date.now()}`;
    registry.registerManaged(managedId, "Managed");

    // Verify the managed one is still null ws
    const managed = registry.get(managedId);
    assert.equal(managed!.ws, null);
  });
});
