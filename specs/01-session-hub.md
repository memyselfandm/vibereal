# Session Hub Specification

## Overview

The Session Hub is a WebSocket server that acts as the central nervous system of VibeReal. It maintains connections to all Claude/Claude Code sessions (laptop and containerized), routes messages between the XREAL AR app and those sessions, and pushes real-time notifications.

## Architecture

```
                              ┌─────────────────────────────┐
                              │        Session Hub          │
                              │     (Node.js / Bun)         │
┌──────────────┐              │                             │              ┌──────────────────┐
│  XREAL App   │◄────────────►│  ┌─────────────────────┐    │◄────────────►│ Claude Container │
│  (Client)    │   WebSocket  │  │   Session Registry  │    │   WebSocket  │     Server 1     │
└──────────────┘              │  │                     │    │              └──────────────────┘
                              │  │  - session_id       │    │
                              │  │  - type (container/ │    │              ┌──────────────────┐
                              │  │    laptop/remote)   │    │◄────────────►│ Claude Container │
                              │  │  - status           │    │   WebSocket  │     Server 2     │
                              │  │  - last_activity    │    │              └──────────────────┘
                              │  │  - connection       │    │
                              │  └─────────────────────┘    │              ┌──────────────────┐
                              │                             │◄────────────►│  Laptop Claude   │
                              │  ┌─────────────────────┐    │    HTTP      │  (Remote Ctrl)   │
                              │  │  Notification Queue │    │              └──────────────────┘
                              │  └─────────────────────┘    │
                              └─────────────────────────────┘
```

## Core Components

### 1. Session Registry

In-memory store (with optional Redis persistence) tracking all connected Claude sessions.

```typescript
interface Session {
  id: string;                          // Unique session identifier
  type: 'container' | 'laptop' | 'remote';
  name: string;                        // Human-readable name ("Container 1", "Laptop Main")
  status: 'idle' | 'thinking' | 'waiting_input' | 'executing' | 'error';
  connectionType: 'websocket' | 'polling';
  connection?: WebSocket;              // For container sessions
  pollingUrl?: string;                 // For laptop remote control
  lastActivity: Date;
  currentTask?: string;                // Brief description of current work
  pendingApprovals: Approval[];        // Tool calls waiting for user approval
  metadata: Record<string, any>;
}

interface Approval {
  id: string;
  toolName: string;
  description: string;
  timestamp: Date;
}
```

### 2. WebSocket Server

Handles bidirectional communication with XREAL app and Claude container servers.

**Port:** 8080 (configurable)

**Endpoints:**
- `ws://host:8080/client` - XREAL app connection
- `ws://host:8080/session/:sessionId` - Claude container server connections

### 3. Notification Queue

Buffers notifications when the XREAL app is temporarily disconnected (e.g., glasses off).

## API Specification

### Client (XREAL App) → Hub Messages

#### List Sessions
```json
{
  "type": "list_sessions",
  "requestId": "uuid"
}
```

#### Send Message to Session
```json
{
  "type": "send_message",
  "requestId": "uuid",
  "sessionId": "container-1",
  "message": {
    "role": "user",
    "content": "Run the test suite"
  }
}
```

#### Approve Tool Call
```json
{
  "type": "approve",
  "requestId": "uuid",
  "sessionId": "container-1",
  "approvalId": "approval-uuid",
  "decision": "approve" | "deny"
}
```

#### Set Focus Session
```json
{
  "type": "set_focus",
  "requestId": "uuid",
  "sessionId": "container-1"
}
```

#### Get Session History
```json
{
  "type": "get_history",
  "requestId": "uuid",
  "sessionId": "container-1",
  "limit": 50
}
```

### Hub → Client (XREAL App) Messages

#### Session List Response
```json
{
  "type": "session_list",
  "requestId": "uuid",
  "sessions": [
    {
      "id": "container-1",
      "name": "Container 1",
      "type": "container",
      "status": "idle",
      "lastActivity": "2026-02-27T10:30:00Z",
      "currentTask": null
    }
  ]
}
```

#### Session Update (Push)
```json
{
  "type": "session_update",
  "sessionId": "container-1",
  "status": "thinking",
  "currentTask": "Running pytest on /app/tests"
}
```

#### Claude Response (Streaming)
```json
{
  "type": "claude_response",
  "sessionId": "container-1",
  "content": "I'll run the tests now...",
  "isComplete": false
}
```

#### Approval Required (Push)
```json
{
  "type": "approval_required",
  "sessionId": "container-1",
  "approval": {
    "id": "approval-uuid",
    "toolName": "Bash",
    "description": "Run: npm run build",
    "timestamp": "2026-02-27T10:31:00Z"
  }
}
```

#### Task Complete (Push)
```json
{
  "type": "task_complete",
  "sessionId": "container-1",
  "summary": "Tests completed: 45 passed, 2 failed",
  "priority": "normal" | "high"
}
```

#### Error
```json
{
  "type": "error",
  "requestId": "uuid",
  "code": "SESSION_NOT_FOUND",
  "message": "Session container-3 does not exist"
}
```

### Session (Claude Server) → Hub Messages

#### Register Session
```json
{
  "type": "register",
  "sessionId": "container-1",
  "name": "Container 1",
  "metadata": {
    "workingDirectory": "/app",
    "model": "claude-sonnet-4-20250514"
  }
}
```

#### Status Update
```json
{
  "type": "status_update",
  "status": "thinking" | "idle" | "waiting_input" | "executing",
  "currentTask": "Analyzing codebase structure"
}
```

#### Response Chunk
```json
{
  "type": "response_chunk",
  "content": "I found 3 files that match...",
  "isComplete": false
}
```

#### Approval Request
```json
{
  "type": "approval_request",
  "approvalId": "uuid",
  "toolName": "Bash",
  "description": "Execute: git push origin main"
}
```

### Hub → Session Messages

#### User Message
```json
{
  "type": "user_message",
  "message": {
    "role": "user",
    "content": "What's the status of the build?"
  }
}
```

#### Approval Decision
```json
{
  "type": "approval_decision",
  "approvalId": "uuid",
  "decision": "approve" | "deny"
}
```

#### Interrupt
```json
{
  "type": "interrupt"
}
```

## Laptop Remote Control Integration

For Claude Code sessions running on the laptop (not in containers), the hub uses HTTP polling to track status via Claude's Remote Control feature.

### Polling Strategy
- Poll every 5 seconds when session is idle
- Poll every 1 second when session is active
- Webhook callback for immediate notifications (if available)

### Remote Control API
```typescript
interface LaptopSessionConfig {
  sessionId: string;
  remoteControlUrl: string;  // claude.ai/code remote control endpoint
  authToken: string;         // Session auth token
}
```

## Configuration

```yaml
# config.yaml
server:
  port: 8080
  host: "0.0.0.0"

redis:
  enabled: false
  url: "redis://localhost:6379"

sessions:
  maxPerClient: 10
  heartbeatInterval: 30000  # ms
  disconnectTimeout: 60000  # ms

notifications:
  queueMaxSize: 100
  ttl: 3600  # seconds

laptop:
  pollingInterval: 5000  # ms
  activePollingInterval: 1000  # ms

logging:
  level: "info"
  format: "json"
```

## Security

### Authentication
- XREAL app authenticates with a pre-shared API key
- Claude container servers authenticate with per-session tokens
- All connections must use WSS (WebSocket Secure) in production

### Authorization
- XREAL app can only interact with sessions registered to its user
- Rate limiting: 100 messages/minute per client

## Error Handling

| Error Code | Description |
|------------|-------------|
| `SESSION_NOT_FOUND` | Requested session does not exist |
| `SESSION_DISCONNECTED` | Session is registered but not connected |
| `UNAUTHORIZED` | Invalid or missing authentication |
| `RATE_LIMITED` | Too many requests |
| `INVALID_MESSAGE` | Malformed message payload |
| `APPROVAL_EXPIRED` | Approval request has timed out |

## Deployment

### Docker
```dockerfile
FROM oven/bun:1.0
WORKDIR /app
COPY package.json bun.lockb ./
RUN bun install --frozen-lockfile
COPY . .
EXPOSE 8080
CMD ["bun", "run", "start"]
```

### Environment Variables
```bash
PORT=8080
REDIS_URL=redis://redis:6379
API_KEY=your-secret-key
LOG_LEVEL=info
```

## Health Checks

- `GET /health` - Returns 200 if server is running
- `GET /health/sessions` - Returns count of active sessions
- `GET /metrics` - Prometheus-compatible metrics

## Future Considerations

1. **Multi-user support** - Multiple XREAL users with isolated sessions
2. **Session persistence** - Reconnect to sessions after hub restart
3. **Clustering** - Multiple hub instances with Redis pub/sub
4. **Voice command preprocessing** - Move NLU parsing to hub for lower latency
