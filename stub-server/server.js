const { WebSocketServer } = require("ws");

const PORT = process.env.PORT || 8080;

const wss = new WebSocketServer({ port: PORT });

// --- Fake session data ---

const sessions = [
  {
    id: "container-1",
    name: "Container 1",
    type: "container",
    status: "idle",
    currentTask: null,
    lastActivity: new Date().toISOString(),
  },
  {
    id: "laptop-1",
    name: "Laptop",
    type: "laptop",
    status: "idle",
    currentTask: null,
    lastActivity: new Date().toISOString(),
  },
];

const statusCycle = ["idle", "thinking", "executing", "idle"];
const fakeTasks = [
  "Running pytest",
  "Analyzing codebase",
  "Writing tests",
  "Refactoring auth module",
  "Fixing lint errors",
  null,
];

const fakeConversation = [
  { role: "user", content: "Run the test suite and fix any failures" },
  { role: "assistant", content: "I'll run the tests now. Let me execute pytest..." },
  { role: "assistant", content: "Found 3 failing tests. Fixing the auth middleware test first." },
  { role: "user", content: "Great, also check the coverage report" },
  { role: "assistant", content: "Coverage is at 87%. The uncovered lines are in the error handling paths of api/routes.ts." },
];

let requestCounter = 0;

function uuid() {
  return `${Date.now()}-${++requestCounter}`;
}

function randomItem(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

// --- WebSocket handling ---

wss.on("connection", (ws) => {
  console.log("[stub] Client connected");

  // Send session list on connect
  ws.send(JSON.stringify({
    type: "session_list",
    requestId: uuid(),
    sessions: sessions,
  }));

  // Send initial conversation history for container-1
  ws.send(JSON.stringify({
    type: "conversation_history",
    sessionId: "container-1",
    messages: fakeConversation,
  }));

  // Handle incoming messages
  ws.on("message", (raw) => {
    let msg;
    try {
      msg = JSON.parse(raw.toString());
    } catch {
      console.log("[stub] Invalid JSON received");
      return;
    }

    console.log("[stub] Received:", msg.type, msg);

    switch (msg.type) {
      case "list_sessions":
        ws.send(JSON.stringify({
          type: "session_list",
          requestId: msg.requestId,
          sessions: sessions,
        }));
        break;

      case "send_message":
        handleSendMessage(ws, msg);
        break;

      case "voice_command":
        handleVoiceCommand(ws, msg);
        break;

      case "approval_decision":
        handleApproval(ws, msg);
        break;

      case "set_focus":
        ws.send(JSON.stringify({
          type: "command_ack",
          requestId: msg.requestId,
          intent: "set_focus",
          targetSession: msg.sessionId,
          confidence: 1.0,
        }));
        break;

      default:
        console.log("[stub] Unknown message type:", msg.type);
    }
  });

  ws.on("close", () => {
    console.log("[stub] Client disconnected");
  });

  // --- Periodic fake events ---

  // Status changes every 8 seconds
  const statusInterval = setInterval(() => {
    const session = randomItem(sessions);
    const newStatus = randomItem(statusCycle);
    session.status = newStatus;
    session.currentTask = newStatus === "idle" ? null : randomItem(fakeTasks);
    session.lastActivity = new Date().toISOString();

    ws.send(JSON.stringify({
      type: "session_update",
      sessionId: session.id,
      status: session.status,
      currentTask: session.currentTask,
    }));
  }, 8000);

  // Fake approval request every 20 seconds
  const approvalInterval = setInterval(() => {
    const session = randomItem(sessions);
    const tools = [
      { tool: "Bash", desc: "git push origin main" },
      { tool: "Bash", desc: "rm -rf node_modules && npm install" },
      { tool: "Write", desc: "Write to src/config.ts" },
      { tool: "Bash", desc: "docker compose up -d" },
    ];
    const action = randomItem(tools);

    ws.send(JSON.stringify({
      type: "notification",
      notification: {
        id: uuid(),
        sessionId: session.id,
        type: "approval_required",
        priority: "critical",
        title: "Approval Required",
        body: `${action.tool}: ${action.desc}`,
        voiceText: `${session.name} needs approval for ${action.desc}`,
        timestamp: new Date().toISOString(),
        metadata: {
          approvalId: uuid(),
          toolName: action.tool,
          description: action.desc,
        },
      },
    }));
  }, 20000);

  // Fake info notification every 30 seconds
  const infoInterval = setInterval(() => {
    const notifications = [
      { title: "Tests Passed", body: "45 passed, 0 failed", voice: "All tests passed", priority: "normal", nType: "task_complete" },
      { title: "Build Complete", body: "Production build succeeded in 42s", voice: "Build complete", priority: "normal", nType: "task_complete" },
      { title: "Lint Error", body: "3 lint errors in src/auth.ts", voice: "Lint errors found", priority: "high", nType: "error" },
    ];
    const n = randomItem(notifications);
    const session = randomItem(sessions);

    ws.send(JSON.stringify({
      type: "notification",
      notification: {
        id: uuid(),
        sessionId: session.id,
        type: n.nType,
        priority: n.priority,
        title: n.title,
        body: n.body,
        voiceText: `${session.name}: ${n.voice}`,
        timestamp: new Date().toISOString(),
      },
    }));
  }, 30000);

  ws.on("close", () => {
    clearInterval(statusInterval);
    clearInterval(approvalInterval);
    clearInterval(infoInterval);
  });
});

// --- Message handlers ---

function handleSendMessage(ws, msg) {
  // Ack
  ws.send(JSON.stringify({
    type: "command_ack",
    requestId: msg.requestId,
    intent: "send_message",
    targetSession: msg.sessionId,
    confidence: 1.0,
  }));

  // Simulate "thinking"
  const sessionId = msg.sessionId || "container-1";
  ws.send(JSON.stringify({
    type: "session_update",
    sessionId: sessionId,
    status: "thinking",
    currentTask: "Processing your message",
  }));

  // Simulate streamed response after a delay
  setTimeout(() => {
    ws.send(JSON.stringify({
      type: "claude_response",
      sessionId: sessionId,
      content: "Sure, I'll work on that. Let me analyze the codebase first...",
      isComplete: false,
    }));
  }, 1500);

  setTimeout(() => {
    ws.send(JSON.stringify({
      type: "claude_response",
      sessionId: sessionId,
      content: "Done! I've made the changes. Here's what I did:\n- Updated the auth middleware\n- Fixed the test assertions\n- Added error handling for edge cases",
      isComplete: true,
    }));

    ws.send(JSON.stringify({
      type: "session_update",
      sessionId: sessionId,
      status: "idle",
      currentTask: null,
    }));
  }, 4000);
}

function handleVoiceCommand(ws, msg) {
  const transcript = (msg.transcript || "").toLowerCase().trim();

  // Simple intent detection for the stub
  let intent = "send_message";
  let targetSession = "container-1";

  if (transcript.match(/status|what'?s happening|update/)) {
    intent = "get_status";
  } else if (transcript.match(/^(yes|approve|do it|go ahead)$/)) {
    intent = "approve";
  } else if (transcript.match(/^(no|deny|cancel|don'?t)$/)) {
    intent = "deny";
  } else if (transcript.match(/^(stop|abort)$/)) {
    intent = "interrupt";
  }

  ws.send(JSON.stringify({
    type: "command_ack",
    requestId: msg.requestId,
    intent: intent,
    targetSession: targetSession,
    confidence: 0.95,
  }));

  // If status request, send a summary
  if (intent === "get_status") {
    ws.send(JSON.stringify({
      type: "claude_response",
      sessionId: "system",
      content: sessions.map((s) => `${s.name}: ${s.status}${s.currentTask ? ` - ${s.currentTask}` : ""}`).join("\n"),
      isComplete: true,
    }));
  }
}

function handleApproval(ws, msg) {
  ws.send(JSON.stringify({
    type: "command_ack",
    requestId: msg.requestId,
    intent: "approval_decision",
    targetSession: msg.sessionId,
    confidence: 1.0,
  }));

  ws.send(JSON.stringify({
    type: "notification",
    notification: {
      id: uuid(),
      sessionId: msg.sessionId,
      type: "info",
      priority: "normal",
      title: msg.decision === "approve" ? "Action Approved" : "Action Denied",
      body: msg.decision === "approve" ? "Executing approved action..." : "Action was denied.",
      voiceText: msg.decision === "approve" ? "Approved" : "Denied",
      timestamp: new Date().toISOString(),
    },
  }));
}

console.log(`[VibeReal Stub Server] Running on ws://localhost:${PORT}`);
console.log("[VibeReal Stub Server] Waiting for XREAL client connection...");
console.log("[VibeReal Stub Server] Fake events: status updates (8s), approvals (20s), notifications (30s)");
