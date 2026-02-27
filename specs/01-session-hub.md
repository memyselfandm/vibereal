# Session Hub Specification

## Overview

The Session Hub is the central server for VibeReal. It handles all core logic including:
- WebSocket connections to the XREAL app and Claude sessions
- Voice command processing (NLU/intent classification)
- Notification routing and prioritization
- Session lifecycle management
- External integrations API (webhooks for n8n, Home Assistant, etc.)

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                                 Session Hub                                      │
│                               (Node.js / Bun)                                    │
│                                                                                 │
│  ┌───────────────────────────────────────────────────────────────────────────┐  │
│  │                           WebSocket Layer                                  │  │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────┐    │  │
│  │  │ Client Handler  │  │ Session Handler │  │ Integration Handler     │    │  │
│  │  │ (XREAL App)     │  │ (Claude Servers)│  │ (External WS clients)   │    │  │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────────────┘    │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  ┌───────────────────────────────────────────────────────────────────────────┐  │
│  │                              Core Services                                 │  │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────┐    │  │
│  │  │ Voice Command   │  │ Session         │  │ Notification            │    │  │
│  │  │ Processor       │  │ Registry        │  │ Manager                 │    │  │
│  │  │ (NLU + Routing) │  │                 │  │                         │    │  │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────────────┘    │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  ┌───────────────────────────────────────────────────────────────────────────┐  │
│  │                            REST API Layer                                  │  │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────┐    │  │
│  │  │ /api/sessions   │  │ /api/webhooks   │  │ /api/integrations       │    │  │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────────────┘    │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────────┘
        │                           │                           │
        ▼                           ▼                           ▼
┌───────────────┐          ┌───────────────┐          ┌───────────────────┐
│  XREAL App    │          │ Claude        │          │ External Systems  │
│  (WebSocket)  │          │ Containers    │          │ (n8n, Home Asst,  │
└───────────────┘          └───────────────┘          │  Slack, etc.)     │
                                                      └───────────────────┘
```

## Core Components

### 1. Session Registry

In-memory store (with optional Redis persistence) tracking all connected Claude sessions.

```typescript
interface Session {
  id: string;
  type: 'container' | 'laptop' | 'remote';
  name: string;
  status: 'idle' | 'thinking' | 'waiting_input' | 'executing' | 'error';
  connectionType: 'websocket' | 'polling';
  connection?: WebSocket;
  pollingUrl?: string;
  lastActivity: Date;
  currentTask?: string;
  pendingApprovals: Approval[];
  metadata: Record<string, any>;
}

interface Approval {
  id: string;
  toolName: string;
  description: string;
  timestamp: Date;
}
```

### 2. Voice Command Processor

Handles NLU for voice commands from the XREAL app. Runs entirely on the hub for low latency.

```typescript
interface VoiceCommandProcessor {
  // Process raw transcript from XREAL app
  process(transcript: string, context: SessionContext): Promise<ProcessedCommand>;

  // Register custom command patterns
  registerPattern(intent: string, patterns: string[]): void;

  // Use Claude for complex/ambiguous commands (optional)
  enableClaudeFallback(apiKey: string): void;
}

interface ProcessedCommand {
  intent: Intent;
  targetSession: string | null;
  parameters: Record<string, any>;
  confidence: number;
  rawTranscript: string;
}

interface SessionContext {
  focusedSessionId: string;
  availableSessions: Session[];
  pendingApprovals: Approval[];
  lastCommand?: ProcessedCommand;
}
```

#### Supported Intents

| Intent | Patterns | Action |
|--------|----------|--------|
| `send_message` | "tell {session} to {message}", "ask {session} {message}", "{message}" | Forward message to session |
| `get_status` | "status", "what's happening", "update" | Return all session statuses |
| `approve` | "yes", "approve", "do it", "go ahead" | Approve pending action |
| `deny` | "no", "deny", "cancel", "don't" | Deny pending action |
| `switch_focus` | "focus {session}", "switch to {session}" | Change focused session |
| `list_sessions` | "list sessions", "what sessions" | Return session list |
| `interrupt` | "stop", "cancel", "abort" | Interrupt current operation |
| `repeat` | "repeat", "say again", "what" | Repeat last response |

#### Intent Classification

Two-tier approach:
1. **Fast pattern matching** - Regex patterns for common commands (<5ms)
2. **Claude fallback** - For ambiguous commands or complex messages (optional, ~500ms)

```typescript
class IntentClassifier {
  private patterns: Map<string, RegExp[]> = new Map();
  private claudeEnabled: boolean = false;

  classify(transcript: string, context: SessionContext): ClassificationResult {
    // Normalize
    const text = transcript.toLowerCase().trim();

    // Quick pattern match
    for (const [intent, regexes] of this.patterns) {
      for (const regex of regexes) {
        const match = text.match(regex);
        if (match) {
          return {
            intent,
            confidence: 0.95,
            entities: this.extractEntities(match, intent),
            usedFallback: false
          };
        }
      }
    }

    // Context-aware defaults
    if (context.pendingApprovals.length > 0) {
      if (/^(yes|yeah|yep|ok|okay|sure)$/i.test(text)) {
        return { intent: 'approve', confidence: 0.9, entities: {}, usedFallback: false };
      }
      if (/^(no|nope|nah|cancel)$/i.test(text)) {
        return { intent: 'deny', confidence: 0.9, entities: {}, usedFallback: false };
      }
    }

    // Claude fallback for complex commands
    if (this.claudeEnabled) {
      return this.classifyWithClaude(text, context);
    }

    // Default: send as message to focused session
    return {
      intent: 'send_message',
      confidence: 0.5,
      entities: { message: text },
      usedFallback: false
    };
  }
}
```

### 3. Notification Manager

Handles notification routing, prioritization, and delivery.

```typescript
interface NotificationManager {
  // Queue notification for delivery
  notify(notification: Notification): void;

  // Get pending notifications
  getPending(): Notification[];

  // Mark as delivered
  markDelivered(notificationId: string): void;

  // Subscribe to notifications (external integrations)
  subscribe(filter: NotificationFilter, callback: WebhookConfig): string;
  unsubscribe(subscriptionId: string): void;
}

interface Notification {
  id: string;
  sessionId: string;
  type: 'session_update' | 'approval_required' | 'task_complete' | 'error' | 'info';
  priority: 'low' | 'normal' | 'high' | 'critical';
  title: string;
  body: string;
  voiceText?: string;         // Optimized for TTS (shorter, natural speech)
  timestamp: Date;
  expiresAt?: Date;
  metadata?: Record<string, any>;
}

interface NotificationFilter {
  sessionIds?: string[];
  types?: string[];
  minPriority?: string;
}
```

#### Priority Handling

| Priority | Behavior |
|----------|----------|
| `critical` | Immediate push + voice alert, interrupts other TTS |
| `high` | Immediate push, voice if user idle |
| `normal` | Push to queue, voice on request |
| `low` | Queue only, batch with others |

#### Voice Text Generation

The hub generates TTS-optimized text for notifications:

```typescript
function generateVoiceText(notification: Notification): string {
  // Keep it short (<15 words for voice)
  switch (notification.type) {
    case 'approval_required':
      return `${notification.sessionId} needs approval for ${notification.metadata?.toolName}`;
    case 'task_complete':
      return `${notification.sessionId} finished: ${truncate(notification.title, 10)}`;
    case 'error':
      return `Error in ${notification.sessionId}`;
    default:
      return truncate(notification.title, 15);
  }
}
```

## WebSocket API

### Endpoints

| Endpoint | Purpose |
|----------|---------|
| `ws://host:8080/client` | XREAL app connection |
| `ws://host:8080/session/:sessionId` | Claude container connections |
| `ws://host:8080/integrations` | External system connections |

### Client (XREAL App) → Hub Messages

#### Voice Command
```json
{
  "type": "voice_command",
  "requestId": "uuid",
  "transcript": "tell container 1 to run the tests",
  "confidence": 0.95
}
```

#### Send Message (Direct)
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

#### Approve/Deny
```json
{
  "type": "approval_decision",
  "requestId": "uuid",
  "sessionId": "container-1",
  "approvalId": "approval-uuid",
  "decision": "approve"
}
```

#### List Sessions
```json
{
  "type": "list_sessions",
  "requestId": "uuid"
}
```

#### Set Focus
```json
{
  "type": "set_focus",
  "requestId": "uuid",
  "sessionId": "container-1"
}
```

### Hub → Client Messages

#### Command Acknowledged
```json
{
  "type": "command_ack",
  "requestId": "uuid",
  "intent": "send_message",
  "targetSession": "container-1",
  "confidence": 0.95
}
```

#### Session List
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
      "lastActivity": "2026-02-27T10:30:00Z"
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
  "currentTask": "Running pytest"
}
```

#### Claude Response
```json
{
  "type": "claude_response",
  "sessionId": "container-1",
  "content": "I'll run the tests now...",
  "isComplete": false
}
```

#### Notification (Push)
```json
{
  "type": "notification",
  "notification": {
    "id": "notif-uuid",
    "sessionId": "container-1",
    "type": "approval_required",
    "priority": "critical",
    "title": "Approval Required",
    "body": "git push origin main",
    "voiceText": "Container 1 needs approval for git push"
  }
}
```

### Session (Claude Server) → Hub Messages

#### Register
```json
{
  "type": "register",
  "sessionId": "container-1",
  "name": "Container 1",
  "metadata": { "workingDirectory": "/app" }
}
```

#### Status Update
```json
{
  "type": "status_update",
  "status": "thinking",
  "currentTask": "Analyzing codebase"
}
```

#### Response Chunk
```json
{
  "type": "response_chunk",
  "content": "Found 3 files...",
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

## REST API

### Sessions

```
GET    /api/sessions                    # List all sessions
GET    /api/sessions/:id                # Get session details
POST   /api/sessions/:id/message        # Send message to session
POST   /api/sessions/:id/approve/:aid   # Approve action
POST   /api/sessions/:id/deny/:aid      # Deny action
POST   /api/sessions/:id/interrupt      # Interrupt session
```

### Webhooks (External Integrations)

```
GET    /api/webhooks                    # List webhook subscriptions
POST   /api/webhooks                    # Create webhook subscription
DELETE /api/webhooks/:id                # Remove subscription
POST   /api/webhooks/test/:id           # Send test notification
```

#### Webhook Subscription
```json
{
  "url": "https://n8n.example.com/webhook/vibereal",
  "events": ["approval_required", "task_complete", "error"],
  "sessions": ["container-1", "container-2"],
  "secret": "webhook-secret-for-signature"
}
```

#### Webhook Payload
```json
{
  "event": "approval_required",
  "timestamp": "2026-02-27T10:30:00Z",
  "session": {
    "id": "container-1",
    "name": "Container 1",
    "status": "waiting_input"
  },
  "data": {
    "approvalId": "uuid",
    "toolName": "Bash",
    "description": "git push origin main"
  }
}
```

Webhooks include an `X-VibeReal-Signature` header (HMAC-SHA256) for verification.

### Integrations API

Generic API for external systems to interact with VibeReal.

```
POST   /api/integrations/command        # Send voice-like command
POST   /api/integrations/notify         # Push notification to AR
GET    /api/integrations/status         # Get system status
```

#### Send Command (for Home Assistant, n8n, etc.)
```json
POST /api/integrations/command
{
  "command": "tell container 1 to deploy to staging",
  "source": "home-assistant"
}
```

#### Push Notification to AR
```json
POST /api/integrations/notify
{
  "title": "Deployment Complete",
  "body": "Staging deployment finished successfully",
  "voiceText": "Staging deployment complete",
  "priority": "high",
  "source": "github-actions"
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
  heartbeatInterval: 30000
  disconnectTimeout: 60000

notifications:
  queueMaxSize: 100
  ttl: 3600

voiceProcessor:
  claudeFallbackEnabled: false
  claudeApiKey: "${ANTHROPIC_API_KEY}"
  confidenceThreshold: 0.7

webhooks:
  maxPerUser: 10
  timeoutMs: 5000
  retryAttempts: 3

laptop:
  pollingInterval: 5000
  activePollingInterval: 1000

logging:
  level: "info"
  format: "json"
```

## Security

### Authentication

| Client Type | Auth Method |
|-------------|-------------|
| XREAL App | Pre-shared API key in header |
| Claude Containers | Per-session token |
| External Integrations | API key + optional IP allowlist |
| Webhooks | Signature verification (HMAC-SHA256) |

### Rate Limiting

| Endpoint | Limit |
|----------|-------|
| WebSocket messages | 100/minute per client |
| REST API | 60/minute per API key |
| Webhooks outbound | 1000/hour total |

## Error Handling

| Code | Description |
|------|-------------|
| `SESSION_NOT_FOUND` | Session does not exist |
| `SESSION_DISCONNECTED` | Session not connected |
| `UNAUTHORIZED` | Invalid authentication |
| `RATE_LIMITED` | Too many requests |
| `INVALID_MESSAGE` | Malformed payload |
| `APPROVAL_EXPIRED` | Approval timed out |
| `COMMAND_NOT_UNDERSTOOD` | NLU failed to classify |
| `WEBHOOK_FAILED` | Webhook delivery failed |

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

### Docker Compose

```yaml
version: "3.8"

services:
  session-hub:
    build: ./session-hub
    ports:
      - "8080:8080"
    environment:
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - API_KEY=${HUB_API_KEY}
      - REDIS_URL=redis://redis:6379
    depends_on:
      - redis
    restart: unless-stopped

  redis:
    image: redis:7-alpine
    volumes:
      - redis_data:/data
    restart: unless-stopped

volumes:
  redis_data:
```

### Environment Variables

```bash
PORT=8080
API_KEY=your-hub-api-key
ANTHROPIC_API_KEY=sk-ant-...      # Optional, for Claude NLU fallback
REDIS_URL=redis://localhost:6379  # Optional, for persistence
LOG_LEVEL=info
```

## Health & Monitoring

### Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /health` | Basic health check |
| `GET /health/sessions` | Session count and status |
| `GET /metrics` | Prometheus metrics |

### Metrics

```
# Sessions
vibereal_sessions_total{type="container",status="running"}
vibereal_sessions_total{type="laptop",status="idle"}

# Voice commands
vibereal_voice_commands_total{intent="send_message",success="true"}
vibereal_voice_command_latency_ms{quantile="p99"}

# Notifications
vibereal_notifications_sent_total{type="approval_required",priority="critical"}
vibereal_webhook_deliveries_total{status="success"}

# Connections
vibereal_websocket_connections{type="client"}
vibereal_websocket_connections{type="session"}
```

## Integration Examples

### n8n Workflow

Trigger n8n workflow when approval is required:

1. Create webhook in VibeReal:
```bash
curl -X POST http://hub:8080/api/webhooks \
  -H "Authorization: Bearer $API_KEY" \
  -d '{
    "url": "https://n8n.example.com/webhook/vibereal-approval",
    "events": ["approval_required"],
    "secret": "my-secret"
  }'
```

2. n8n workflow receives payload and can:
   - Send Slack notification
   - Trigger smart home action (flash lights)
   - Log to external system

### Home Assistant

Send voice commands from HA automations:

```yaml
# Home Assistant automation
automation:
  - alias: "Deploy on button press"
    trigger:
      - platform: state
        entity_id: binary_sensor.deploy_button
        to: "on"
    action:
      - service: rest_command.vibereal_command
        data:
          command: "tell container 1 to deploy to production"

rest_command:
  vibereal_command:
    url: "http://hub:8080/api/integrations/command"
    method: POST
    headers:
      Authorization: "Bearer {{ states('secret.vibereal_api_key') }}"
    payload: '{"command": "{{ command }}", "source": "home-assistant"}'
```

### Slack Bot

Push notifications to Slack:

```bash
curl -X POST http://hub:8080/api/webhooks \
  -H "Authorization: Bearer $API_KEY" \
  -d '{
    "url": "https://hooks.slack.com/services/XXX/YYY/ZZZ",
    "events": ["task_complete", "error"],
    "transform": "slack"
  }'
```

The hub can transform payloads to Slack block format when `"transform": "slack"` is specified.
