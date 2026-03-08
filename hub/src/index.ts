import { createServer } from "node:http";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { WebSocketServer, type WebSocket } from "ws";
import { config } from "./config.ts";
import * as sessionRegistry from "./session-registry.ts";
import * as clientManager from "./client-manager.ts";
import { onSessionOpen, onSessionMessage, onSessionClose } from "./handlers/session-handler.ts";
import { onClientOpen, onClientMessage, onClientClose } from "./handlers/client-handler.ts";

const __dirname = dirname(fileURLToPath(import.meta.url));
const indexHtml = readFileSync(join(__dirname, "..", "public", "index.html"), "utf-8");

// HTTP server
const server = createServer((req, res) => {
  const url = new URL(req.url || "/", `http://${req.headers.host}`);

  // Health check
  if (url.pathname === "/health") {
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({
      status: "ok",
      sessions: sessionRegistry.count(),
      clients: clientManager.count(),
    }));
    return;
  }

  // Serve web UI
  if (url.pathname === "/" || url.pathname === "/index.html") {
    res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
    res.end(indexHtml);
    return;
  }

  res.writeHead(404);
  res.end("Not Found");
});

// WebSocket server
const wss = new WebSocketServer({ noServer: true });

server.on("upgrade", (req, socket, head) => {
  const url = new URL(req.url || "/", `http://${req.headers.host}`);

  // Route: /client
  if (url.pathname === "/client") {
    // Validate API key if configured
    if (config.apiKey) {
      const auth = req.headers["authorization"];
      const headerToken = auth?.startsWith("Bearer ") ? auth.slice(7) : null;
      const queryToken = url.searchParams.get("apiKey");
      const token = headerToken || queryToken;
      if (token !== config.apiKey) {
        socket.write("HTTP/1.1 401 Unauthorized\r\n\r\n");
        socket.destroy();
        return;
      }
    }

    wss.handleUpgrade(req, socket, head, (ws) => {
      setupClientWs(ws);
    });
    return;
  }

  // Route: /session/:id
  const sessionMatch = url.pathname.match(/^\/session\/(.+)$/);
  if (sessionMatch) {
    const sessionId = decodeURIComponent(sessionMatch[1]);
    wss.handleUpgrade(req, socket, head, (ws) => {
      setupSessionWs(ws, sessionId);
    });
    return;
  }

  socket.write("HTTP/1.1 404 Not Found\r\n\r\n");
  socket.destroy();
});

function setupClientWs(ws: WebSocket) {
  onClientOpen(ws);

  ws.on("message", (data) => {
    const text = typeof data === "string" ? data : data.toString("utf-8");
    onClientMessage(ws, text);
  });

  ws.on("close", () => {
    onClientClose(ws);
  });
}

function setupSessionWs(ws: WebSocket, sessionId: string) {
  onSessionOpen(ws, sessionId);

  ws.on("message", (data) => {
    const text = typeof data === "string" ? data : data.toString("utf-8");
    onSessionMessage(ws, sessionId, text);
  });

  ws.on("close", () => {
    onSessionClose(ws, sessionId);
  });
}

server.listen(config.port, () => {
  console.log(`VibeReal Session Hub running on http://localhost:${config.port}`);
  console.log(`  Web UI:  http://localhost:${config.port}/`);
  console.log(`  Health:  http://localhost:${config.port}/health`);
  console.log(`  Client:  ws://localhost:${config.port}/client`);
  console.log(`  Session: ws://localhost:${config.port}/session/{id}`);
  if (!config.apiKey) {
    console.log(`  Warning: No HUB_API_KEY set — client auth disabled`);
  }
});
