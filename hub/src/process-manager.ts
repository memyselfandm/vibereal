import { spawn, execFileSync } from "node:child_process";
import { createInterface } from "node:readline";
import { randomUUID } from "node:crypto";
import { config } from "./config.ts";
import * as sessionRegistry from "./session-registry.ts";
import * as clients from "./client-manager.ts";
import type { ManagedProcess, CreateSessionRequest, CreateSessionResponse } from "./process-types.ts";

const managedProcesses = new Map<string, ManagedProcess>();
let enabled = true;
let cliAvailable = false;

export async function initialize(): Promise<void> {
  enabled = config.processManager;
  if (!enabled) {
    console.log("[process-manager] Disabled via PROCESS_MANAGER=false");
    return;
  }

  try {
    execFileSync("which", [config.claudePath], { stdio: "pipe" });
    cliAvailable = true;
    console.log(`[process-manager] Claude CLI found at: ${config.claudePath}`);
  } catch {
    cliAvailable = false;
    console.log(
      `[process-manager] Claude CLI not found (${config.claudePath}). Spawn disabled, relay-only mode.`
    );
  }
}

export function isEnabled(): boolean {
  return enabled && cliAvailable;
}

export function isManaged(sessionId: string): boolean {
  return managedProcesses.has(sessionId);
}

export function create(request: CreateSessionRequest): CreateSessionResponse {
  if (!isEnabled()) {
    throw new Error("Process manager is not available");
  }

  const sessionId = `managed-${randomUUID().slice(0, 8)}`;
  const name = request.name || `Session ${sessionId.slice(8)}`;

  const args = [
    "-p",
    "--output-format=stream-json",
    "--input-format=stream-json",
    "--verbose",
  ];

  if (request.allowedTools?.length) {
    args.push("--allowedTools", request.allowedTools.join(","));
  }

  if (request.permissionMode) {
    args.push("--permission-mode", request.permissionMode);
  }

  const child = spawn(config.claudePath, args, {
    cwd: request.workingDirectory || process.cwd(),
    stdio: ["pipe", "pipe", "pipe"],
    env: { ...process.env },
  });

  const managed: ManagedProcess = {
    id: sessionId,
    sessionId,
    process: child,
    createdAt: new Date(),
    prompt: request.prompt,
    workingDirectory: request.workingDirectory || process.cwd(),
  };

  managedProcesses.set(sessionId, managed);

  // Register in session registry (no WebSocket)
  sessionRegistry.registerManaged(sessionId, name);
  sessionRegistry.updateStatus(sessionId, "starting", "Initializing...");

  // Broadcast session update
  clients.broadcast(
    JSON.stringify({
      type: "session_update",
      sessionId,
      status: "starting",
      currentTask: "Initializing...",
    })
  );

  // Set up stdout parsing
  setupStdoutParser(managed);

  // Set up stderr logging
  if (child.stderr) {
    child.stderr.on("data", (data: Buffer) => {
      const text = data.toString("utf-8").trim();
      if (text) {
        console.log(`[process-manager] [${sessionId}] stderr: ${text}`);
      }
    });
  }

  // Handle process exit
  child.on("exit", (code, signal) => {
    console.log(
      `[process-manager] [${sessionId}] Process exited (code=${code}, signal=${signal})`
    );
    managedProcesses.delete(sessionId);
    sessionRegistry.updateStatus(sessionId, "completed", "");
    clients.broadcast(
      JSON.stringify({
        type: "session_update",
        sessionId,
        status: "completed",
        currentTask: "",
      })
    );
  });

  child.on("error", (err) => {
    console.error(`[process-manager] [${sessionId}] Process error:`, err.message);
    managedProcesses.delete(sessionId);
    sessionRegistry.updateStatus(sessionId, "error", err.message);
    clients.broadcast(
      JSON.stringify({
        type: "session_update",
        sessionId,
        status: "error",
        currentTask: err.message,
      })
    );
  });

  // Send the initial prompt via stdin
  sendToProcess(sessionId, {
    type: "user",
    message: {
      role: "user",
      content: [{ type: "text", text: request.prompt }],
    },
  });

  return { sessionId, name, status: "starting" };
}

function setupStdoutParser(managed: ManagedProcess): void {
  if (!managed.process.stdout) return;

  const rl = createInterface({ input: managed.process.stdout });
  const sessionId = managed.sessionId;

  rl.on("line", (line) => {
    if (!line.trim()) return;

    let event: any;
    try {
      event = JSON.parse(line);
    } catch {
      console.warn(
        `[process-manager] [${sessionId}] Non-JSON stdout: ${line.slice(0, 200)}`
      );
      return;
    }

    handleStreamEvent(sessionId, event);
  });
}

function handleStreamEvent(sessionId: string, event: any): void {
  const type = event.type;

  switch (type) {
    case "system": {
      // Initial system message - session is ready
      sessionRegistry.updateStatus(sessionId, "idle", "");
      clients.broadcast(
        JSON.stringify({
          type: "session_update",
          sessionId,
          status: "idle",
          currentTask: "",
        })
      );
      break;
    }

    case "assistant": {
      // Full assistant message with content blocks
      const content = event.message?.content;
      if (Array.isArray(content)) {
        for (const block of content) {
          if (block.type === "text" && block.text) {
            clients.broadcast(
              JSON.stringify({
                type: "claude_response",
                sessionId,
                content: block.text,
                isComplete: false,
              })
            );
          }
          if (block.type === "tool_use") {
            sessionRegistry.updateStatus(sessionId, "executing", block.name || "tool");
            clients.broadcast(
              JSON.stringify({
                type: "session_update",
                sessionId,
                status: "executing",
                currentTask: block.name || "tool",
              })
            );
          }
        }
      }
      break;
    }

    case "content_block_delta": {
      // Streaming text delta
      const delta = event.delta;
      if (delta?.type === "text_delta" && delta.text) {
        clients.broadcast(
          JSON.stringify({
            type: "claude_response",
            sessionId,
            content: delta.text,
            isComplete: false,
          })
        );
      }
      break;
    }

    case "message_stop": {
      // Message complete
      clients.broadcast(
        JSON.stringify({
          type: "claude_response",
          sessionId,
          content: "",
          isComplete: true,
        })
      );
      sessionRegistry.updateStatus(sessionId, "idle", "");
      clients.broadcast(
        JSON.stringify({
          type: "session_update",
          sessionId,
          status: "idle",
          currentTask: "",
        })
      );
      break;
    }

    case "result": {
      // Final result message
      const resultText = event.result || "";
      if (resultText) {
        clients.broadcast(
          JSON.stringify({
            type: "claude_response",
            sessionId,
            content: resultText,
            isComplete: true,
          })
        );
      }
      sessionRegistry.updateStatus(sessionId, "completed", "");
      clients.broadcast(
        JSON.stringify({
          type: "session_update",
          sessionId,
          status: "completed",
          currentTask: "",
        })
      );
      break;
    }

    default: {
      console.log(`[process-manager] [${sessionId}] Unhandled event type: ${type}`);
    }
  }
}

export function sendMessage(
  sessionId: string,
  message: { role: string; content: string }
): boolean {
  const managed = managedProcesses.get(sessionId);
  if (!managed?.process.stdin?.writable) return false;

  sendToProcess(sessionId, {
    type: "user",
    message: {
      role: "user",
      content: [{ type: "text", text: message.content }],
    },
  });

  sessionRegistry.updateStatus(sessionId, "busy", "Processing...");
  clients.broadcast(
    JSON.stringify({
      type: "session_update",
      sessionId,
      status: "busy",
      currentTask: "Processing...",
    })
  );

  return true;
}

export function sendApprovalDecision(
  sessionId: string,
  approvalId: string,
  decision: string
): boolean {
  // Approval handling for managed processes would require the permission-prompt-tool
  // protocol. For now this is a placeholder that logs the decision.
  console.log(
    `[process-manager] [${sessionId}] Approval ${approvalId}: ${decision} (not yet implemented for managed processes)`
  );
  return false;
}

function sendToProcess(sessionId: string, message: unknown): void {
  const managed = managedProcesses.get(sessionId);
  if (!managed?.process.stdin?.writable) {
    console.warn(`[process-manager] [${sessionId}] Cannot write to stdin`);
    return;
  }
  managed.process.stdin.write(JSON.stringify(message) + "\n");
}

export function kill(sessionId: string): boolean {
  const managed = managedProcesses.get(sessionId);
  if (!managed) return false;

  console.log(`[process-manager] [${sessionId}] Killing process`);
  managed.process.kill("SIGTERM");

  // Force kill after 5 seconds if still running
  setTimeout(() => {
    if (managedProcesses.has(sessionId)) {
      console.log(`[process-manager] [${sessionId}] Force killing process`);
      managed.process.kill("SIGKILL");
      managedProcesses.delete(sessionId);
    }
  }, 5000);

  return true;
}

export function getAll(): ManagedProcess[] {
  return Array.from(managedProcesses.values());
}

export function shutdownAll(): void {
  console.log(`[process-manager] Shutting down ${managedProcesses.size} managed processes`);
  for (const [id] of managedProcesses) {
    kill(id);
  }
}
