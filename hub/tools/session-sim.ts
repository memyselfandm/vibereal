/**
 * Session Simulator — simulates a Claude Code session connecting to the hub.
 *
 * Usage:
 *   bun run sim                    # Auto-demo mode with default name
 *   bun run sim -- --name myapp    # Custom session name
 *   bun run sim -- --interactive   # Interactive stdin mode
 *   bun run sim -- --url ws://host:port/session/my-session
 */

const args = process.argv.slice(2);
const flagIdx = (f: string) => args.indexOf(f);

const name = args[flagIdx("--name") + 1] || "demo-session";
const isInteractive = args.includes("--interactive");
const port = process.env.PORT || "8080";
const baseUrl = args[flagIdx("--url") + 1] || `ws://localhost:${port}/session/${name}`;

console.log(`[sim] Connecting to ${baseUrl}`);

const ws = new WebSocket(baseUrl);

ws.onopen = () => {
  console.log("[sim] Connected");

  // Send register message
  ws.send(
    JSON.stringify({
      type: "register",
      sessionId: name,
      name: `Sim: ${name}`,
      metadata: { type: "claude_code" },
    })
  );
  console.log("[sim] Registered");

  if (isInteractive) {
    startInteractive();
  } else {
    startAutoDemo();
  }
};

ws.onmessage = (event) => {
  const msg = JSON.parse(event.data as string);
  console.log(`[sim] ← ${msg.type}:`, JSON.stringify(msg, null, 2));

  if (msg.type === "send_message") {
    console.log(`[sim] Received message: ${msg.message?.content}`);
    // Auto-respond in demo mode
    if (!isInteractive) {
      setTimeout(() => {
        ws.send(
          JSON.stringify({
            type: "response_chunk",
            content: `I received your message: "${msg.message?.content}". Processing...`,
            isComplete: false,
          })
        );
        setTimeout(() => {
          ws.send(
            JSON.stringify({
              type: "response_chunk",
              content: " Done!",
              isComplete: true,
            })
          );
          ws.send(
            JSON.stringify({
              type: "status_update",
              status: "idle",
              currentTask: "",
            })
          );
        }, 1000);
      }, 500);
    }
  }

  if (msg.type === "approval_decision") {
    console.log(`[sim] Approval ${msg.approvalId}: ${msg.decision}`);
    ws.send(
      JSON.stringify({
        type: "status_update",
        status: "executing",
        currentTask: msg.decision === "approve" ? "Running approved action" : "Action denied",
      })
    );
    setTimeout(() => {
      ws.send(
        JSON.stringify({
          type: "status_update",
          status: "idle",
          currentTask: "",
        })
      );
    }, 2000);
  }
};

ws.onerror = (event) => {
  console.error("[sim] Error:", event);
};

ws.onclose = (event) => {
  console.log(`[sim] Disconnected (code: ${event.code})`);
  process.exit(0);
};

// ==================== Auto Demo ====================

async function sleep(ms: number) {
  return new Promise((r) => setTimeout(r, ms));
}

async function startAutoDemo() {
  await sleep(1500);

  // Cycle through statuses
  const statuses = [
    { status: "thinking", task: "Analyzing project structure" },
    { status: "executing", task: "Reading package.json" },
    { status: "thinking", task: "Planning implementation" },
    { status: "executing", task: "Writing src/index.ts" },
  ];

  for (const s of statuses) {
    ws.send(JSON.stringify({ type: "status_update", ...s }));
    console.log(`[sim] → status: ${s.status} — ${s.task}`);
    await sleep(2000);
  }

  // Send response chunks
  const chunks = [
    "I've analyzed the project structure. ",
    "Found 3 source files and 2 config files. ",
    "Here's what I recommend:\n\n",
    "1. Refactor the auth module\n",
    "2. Add input validation\n",
    "3. Update the tests\n",
  ];

  for (let i = 0; i < chunks.length; i++) {
    ws.send(
      JSON.stringify({
        type: "response_chunk",
        content: chunks[i],
        isComplete: i === chunks.length - 1,
      })
    );
    await sleep(400);
  }

  await sleep(1000);

  // Trigger approval request
  ws.send(
    JSON.stringify({
      type: "approval_request",
      approvalId: "approval-001",
      toolName: "Bash",
      description: "git push origin main",
    })
  );
  console.log("[sim] → Approval request sent");

  // Wait for approval decision (handled in onmessage)
  console.log("[sim] Waiting for approval decision...");

  // Keep alive — the message handler will process the response
  await sleep(30000);

  // After timeout, go idle
  ws.send(JSON.stringify({ type: "status_update", status: "idle", currentTask: "" }));
  console.log("[sim] Demo cycle complete. Still connected — send messages from the UI.");

  // Keep the process running
  await sleep(600000);
}

// ==================== Interactive Mode ====================

function startInteractive() {
  console.log("\nInteractive mode. Commands:");
  console.log("  status <status> <task>     — Send status update");
  console.log('  respond <text>             — Send response chunk (use --done for isComplete)');
  console.log("  approve <tool> <desc>      — Trigger approval request");
  console.log("  quit                       — Disconnect\n");

  const reader = Bun.stdin.stream().getReader();
  const decoder = new TextDecoder();

  (async () => {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      const line = decoder.decode(value).trim();
      if (!line) continue;

      const parts = line.split(" ");
      const cmd = parts[0];

      switch (cmd) {
        case "status": {
          const status = parts[1] || "idle";
          const task = parts.slice(2).join(" ") || "";
          ws.send(JSON.stringify({ type: "status_update", status, currentTask: task }));
          break;
        }
        case "respond": {
          const isDone = parts.includes("--done");
          const text = parts
            .slice(1)
            .filter((p) => p !== "--done")
            .join(" ");
          ws.send(JSON.stringify({ type: "response_chunk", content: text, isComplete: isDone }));
          break;
        }
        case "approve": {
          const tool = parts[1] || "Bash";
          const desc = parts.slice(2).join(" ") || "Do something";
          ws.send(
            JSON.stringify({
              type: "approval_request",
              approvalId: `approval-${Date.now()}`,
              toolName: tool,
              description: desc,
            })
          );
          break;
        }
        case "quit":
          ws.close();
          process.exit(0);
        default:
          console.log(`Unknown command: ${cmd}`);
      }
    }
  })();
}
