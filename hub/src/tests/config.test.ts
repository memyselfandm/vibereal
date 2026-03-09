/**
 * Tests for config.ts — verifies new process manager fields are present.
 */
import { describe, it, before, after } from "node:test";
import assert from "node:assert/strict";

describe("config", () => {
  it("exports port, apiKey, claudePath, processManager", async () => {
    // Import fresh after test env is set
    const { config } = await import("../config.ts");
    assert.ok(typeof config.port === "number", "port should be a number");
    assert.ok(typeof config.apiKey === "string", "apiKey should be a string");
    assert.ok(typeof config.claudePath === "string", "claudePath should be a string");
    assert.ok(typeof config.processManager === "boolean", "processManager should be a boolean");
  });

  it("claudePath defaults to 'claude'", async () => {
    const { config } = await import("../config.ts");
    // Unless CLAUDE_PATH env var is set, default is 'claude'
    const expected = process.env.CLAUDE_PATH || "claude";
    assert.equal(config.claudePath, expected);
  });

  it("processManager defaults to true when env var not set to 'false'", async () => {
    const { config } = await import("../config.ts");
    // Default is true unless PROCESS_MANAGER=false
    if (process.env.PROCESS_MANAGER !== "false") {
      assert.equal(config.processManager, true);
    } else {
      assert.equal(config.processManager, false);
    }
  });
});
