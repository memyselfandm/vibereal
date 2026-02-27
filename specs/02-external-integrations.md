# External Integrations Specification

## Overview

VibeReal exposes APIs for external systems to integrate with. This allows tools like n8n, Home Assistant, Slack, and custom scripts to:
- Receive notifications from VibeReal (webhooks)
- Send commands to Claude sessions
- Push notifications to the AR interface
- Query system status

The Session Hub handles all integrations directly - no middleware required.

## Integration Methods

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Session Hub                                     │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                      Integration Layer                               │   │
│  │                                                                      │   │
│  │  ┌──────────────┐   ┌──────────────┐   ┌──────────────────────┐     │   │
│  │  │ Webhooks     │   │ REST API     │   │ WebSocket            │     │   │
│  │  │ (Outbound)   │   │ (Inbound)    │   │ (Bidirectional)      │     │   │
│  │  └──────────────┘   └──────────────┘   └──────────────────────┘     │   │
│  │                                                                      │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
         │                        │                        │
         ▼                        ▼                        ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────────┐
│ n8n             │    │ Home Assistant  │    │ Custom Dashboard    │
│ Slack           │    │ Scripts/CLIs    │    │ Mobile App          │
│ Discord         │    │ GitHub Actions  │    │ Monitoring Tools    │
└─────────────────┘    └─────────────────┘    └─────────────────────┘
```

## Webhooks (Outbound)

Push notifications to external systems when events occur.

### Subscription Management

```
POST   /api/webhooks          # Create subscription
GET    /api/webhooks          # List subscriptions
GET    /api/webhooks/:id      # Get subscription details
PUT    /api/webhooks/:id      # Update subscription
DELETE /api/webhooks/:id      # Remove subscription
POST   /api/webhooks/:id/test # Send test payload
```

### Create Subscription

```http
POST /api/webhooks
Authorization: Bearer <api-key>
Content-Type: application/json

{
  "name": "Slack Notifications",
  "url": "https://hooks.slack.com/services/XXX/YYY/ZZZ",
  "events": ["approval_required", "task_complete", "error"],
  "sessions": ["container-1", "container-2"],
  "headers": {
    "X-Custom-Header": "value"
  },
  "secret": "webhook-signing-secret",
  "transform": "slack",
  "enabled": true
}
```

### Response

```json
{
  "id": "wh_abc123",
  "name": "Slack Notifications",
  "url": "https://hooks.slack.com/...",
  "events": ["approval_required", "task_complete", "error"],
  "sessions": ["container-1", "container-2"],
  "transform": "slack",
  "enabled": true,
  "createdAt": "2026-02-27T10:00:00Z",
  "stats": {
    "deliveries": 0,
    "failures": 0,
    "lastDelivery": null
  }
}
```

### Event Types

| Event | Description | When Triggered |
|-------|-------------|----------------|
| `session_connected` | Session came online | Container started, laptop connected |
| `session_disconnected` | Session went offline | Container stopped, connection lost |
| `status_change` | Session status changed | idle → thinking → executing |
| `approval_required` | Action needs approval | Claude wants to run a tool |
| `approval_resolved` | Approval was handled | User approved or denied |
| `task_complete` | Task finished | Claude completed work |
| `error` | Error occurred | Session error, connection error |
| `message` | Claude sent message | Response from Claude |

### Webhook Payload

```json
{
  "id": "evt_xyz789",
  "event": "approval_required",
  "timestamp": "2026-02-27T10:30:00Z",
  "session": {
    "id": "container-1",
    "name": "Container 1",
    "type": "container",
    "status": "waiting_input"
  },
  "data": {
    "approvalId": "apr_123",
    "toolName": "Bash",
    "description": "git push origin main",
    "requestedAt": "2026-02-27T10:30:00Z"
  }
}
```

### Signature Verification

All webhooks include an `X-VibeReal-Signature` header:

```
X-VibeReal-Signature: sha256=<HMAC-SHA256 of body using secret>
X-VibeReal-Timestamp: 1709034600
```

Verification (Node.js example):
```javascript
const crypto = require('crypto');

function verifyWebhook(body, signature, timestamp, secret) {
  const payload = `${timestamp}.${body}`;
  const expected = crypto
    .createHmac('sha256', secret)
    .update(payload)
    .digest('hex');
  return signature === `sha256=${expected}`;
}
```

### Payload Transforms

Pre-built transforms for common platforms:

| Transform | Target Platform |
|-----------|-----------------|
| `raw` | No transformation (default) |
| `slack` | Slack Block Kit format |
| `discord` | Discord embed format |
| `teams` | Microsoft Teams card |
| `generic` | Simple text format |

#### Slack Transform Example

Input event:
```json
{
  "event": "approval_required",
  "session": { "name": "Container 1" },
  "data": { "toolName": "Bash", "description": "git push origin main" }
}
```

Transformed output:
```json
{
  "blocks": [
    {
      "type": "header",
      "text": { "type": "plain_text", "text": "⚠️ Approval Required" }
    },
    {
      "type": "section",
      "text": {
        "type": "mrkdwn",
        "text": "*Container 1* wants to execute:\n```git push origin main```"
      }
    },
    {
      "type": "actions",
      "elements": [
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "Approve" },
          "style": "primary",
          "action_id": "approve_apr_123"
        },
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "Deny" },
          "style": "danger",
          "action_id": "deny_apr_123"
        }
      ]
    }
  ]
}
```

### Retry Policy

Failed deliveries are retried with exponential backoff:
- Attempt 1: Immediate
- Attempt 2: 10 seconds
- Attempt 3: 1 minute
- Attempt 4: 5 minutes
- Attempt 5: 30 minutes (final)

After 5 failures, the webhook is marked as failing and an alert is generated.

## REST API (Inbound)

### Send Command

Send a voice-like command to the system.

```http
POST /api/integrations/command
Authorization: Bearer <api-key>
Content-Type: application/json

{
  "command": "tell container 1 to run the tests",
  "source": "home-assistant",
  "metadata": {
    "trigger": "button_press"
  }
}
```

Response:
```json
{
  "id": "cmd_abc123",
  "status": "processed",
  "intent": "send_message",
  "targetSession": "container-1",
  "message": "run the tests",
  "timestamp": "2026-02-27T10:30:00Z"
}
```

### Push Notification

Push a notification to the AR interface.

```http
POST /api/integrations/notify
Authorization: Bearer <api-key>
Content-Type: application/json

{
  "title": "Deployment Complete",
  "body": "Production deployment finished successfully",
  "voiceText": "Production deployment complete",
  "priority": "high",
  "source": "github-actions",
  "metadata": {
    "deploymentId": "deploy-123",
    "environment": "production"
  },
  "expiresIn": 300
}
```

Response:
```json
{
  "id": "notif_xyz789",
  "status": "delivered",
  "deliveredAt": "2026-02-27T10:30:00Z"
}
```

If the AR client is disconnected, notifications are queued:
```json
{
  "id": "notif_xyz789",
  "status": "queued",
  "queuedAt": "2026-02-27T10:30:00Z",
  "expiresAt": "2026-02-27T10:35:00Z"
}
```

### Approve/Deny Actions

External systems can approve or deny pending actions.

```http
POST /api/integrations/approve
Authorization: Bearer <api-key>
Content-Type: application/json

{
  "sessionId": "container-1",
  "approvalId": "apr_123",
  "decision": "approve",
  "source": "slack-bot"
}
```

### Get System Status

```http
GET /api/integrations/status
Authorization: Bearer <api-key>
```

Response:
```json
{
  "status": "healthy",
  "arClient": {
    "connected": true,
    "lastSeen": "2026-02-27T10:30:00Z"
  },
  "sessions": {
    "total": 3,
    "connected": 2,
    "byStatus": {
      "idle": 1,
      "thinking": 1,
      "disconnected": 1
    }
  },
  "pendingApprovals": [
    {
      "id": "apr_123",
      "sessionId": "container-1",
      "toolName": "Bash",
      "description": "git push origin main",
      "requestedAt": "2026-02-27T10:29:00Z"
    }
  ],
  "queuedNotifications": 0
}
```

### List Sessions

```http
GET /api/integrations/sessions
Authorization: Bearer <api-key>
```

Response:
```json
{
  "sessions": [
    {
      "id": "container-1",
      "name": "Container 1",
      "type": "container",
      "status": "idle",
      "currentTask": null,
      "lastActivity": "2026-02-27T10:25:00Z",
      "pendingApprovals": []
    },
    {
      "id": "laptop-main",
      "name": "Laptop",
      "type": "laptop",
      "status": "thinking",
      "currentTask": "Analyzing codebase",
      "lastActivity": "2026-02-27T10:30:00Z",
      "pendingApprovals": []
    }
  ]
}
```

### Send Message to Session

```http
POST /api/integrations/sessions/:sessionId/message
Authorization: Bearer <api-key>
Content-Type: application/json

{
  "content": "What's the status of the tests?",
  "source": "api-client"
}
```

## WebSocket (Bidirectional)

For real-time integrations that need to both send and receive.

### Connect

```
ws://hub:8080/integrations
Authorization: Bearer <api-key>
```

### Subscribe to Events

```json
{
  "type": "subscribe",
  "events": ["approval_required", "task_complete"],
  "sessions": ["container-1"]
}
```

### Receive Events

```json
{
  "type": "event",
  "event": "approval_required",
  "timestamp": "2026-02-27T10:30:00Z",
  "session": { "id": "container-1", "name": "Container 1" },
  "data": { "approvalId": "apr_123", "toolName": "Bash" }
}
```

### Send Commands

```json
{
  "type": "command",
  "command": "tell container 1 to run tests",
  "requestId": "req-123"
}
```

### Response

```json
{
  "type": "command_result",
  "requestId": "req-123",
  "status": "processed",
  "intent": "send_message"
}
```

## Integration Examples

### n8n

#### Receive Notifications (Webhook)

1. Create a Webhook node in n8n to receive events
2. Register the webhook URL with VibeReal:

```bash
curl -X POST http://hub:8080/api/webhooks \
  -H "Authorization: Bearer $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "n8n Workflow",
    "url": "https://n8n.example.com/webhook/vibereal",
    "events": ["approval_required", "task_complete", "error"],
    "secret": "n8n-webhook-secret"
  }'
```

3. n8n workflow processes the payload:
```
[Webhook] → [Switch on event type] → [Slack/Email/Database]
```

#### Send Commands (HTTP Request)

```
[Schedule/Trigger] → [HTTP Request to VibeReal API] → [Process Response]
```

n8n HTTP Request node config:
- Method: POST
- URL: `http://hub:8080/api/integrations/command`
- Headers: `Authorization: Bearer {{ $credentials.vibeRealApiKey }}`
- Body: `{ "command": "{{ $json.command }}", "source": "n8n" }`

### Home Assistant

#### Automation: Notify on Motion

```yaml
automation:
  - alias: "Notify VibeReal on Office Motion"
    trigger:
      - platform: state
        entity_id: binary_sensor.office_motion
        to: "on"
    condition:
      - condition: state
        entity_id: input_boolean.working_mode
        state: "on"
    action:
      - service: rest_command.vibereal_notify
        data:
          title: "Motion Detected"
          body: "Someone entered the office"
          priority: "low"

rest_command:
  vibereal_notify:
    url: "http://hub:8080/api/integrations/notify"
    method: POST
    headers:
      Authorization: !secret vibereal_api_key
      Content-Type: application/json
    payload: >
      {
        "title": "{{ title }}",
        "body": "{{ body }}",
        "priority": "{{ priority }}",
        "source": "home-assistant"
      }
```

#### Automation: Send Command on Button Press

```yaml
automation:
  - alias: "Deploy on Button Press"
    trigger:
      - platform: state
        entity_id: binary_sensor.deploy_button
        to: "on"
    action:
      - service: rest_command.vibereal_command
        data:
          command: "tell container 1 to deploy to staging"

rest_command:
  vibereal_command:
    url: "http://hub:8080/api/integrations/command"
    method: POST
    headers:
      Authorization: !secret vibereal_api_key
      Content-Type: application/json
    payload: '{"command": "{{ command }}", "source": "home-assistant"}'
```

#### Sensor: VibeReal Status

```yaml
sensor:
  - platform: rest
    name: "VibeReal Status"
    resource: "http://hub:8080/api/integrations/status"
    headers:
      Authorization: !secret vibereal_api_key
    value_template: "{{ value_json.sessions.total }}"
    json_attributes:
      - sessions
      - pendingApprovals
      - arClient
    scan_interval: 30
```

### Slack Bot

#### Interactive Approvals

1. Set up webhook with Slack transform:
```bash
curl -X POST http://hub:8080/api/webhooks \
  -H "Authorization: Bearer $API_KEY" \
  -d '{
    "url": "https://hooks.slack.com/services/XXX/YYY/ZZZ",
    "events": ["approval_required"],
    "transform": "slack"
  }'
```

2. Set up Slack interactivity endpoint to receive button clicks
3. When user clicks Approve/Deny, call VibeReal API:

```javascript
// Slack interactivity handler
app.post('/slack/interactions', async (req, res) => {
  const payload = JSON.parse(req.body.payload);
  const action = payload.actions[0];

  if (action.action_id.startsWith('approve_') || action.action_id.startsWith('deny_')) {
    const [decision, approvalId] = action.action_id.split('_');

    await fetch('http://hub:8080/api/integrations/approve', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${process.env.VIBEREAL_API_KEY}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        approvalId,
        decision,
        source: 'slack-bot'
      })
    });

    res.json({ text: `Action ${decision}d!` });
  }
});
```

### GitHub Actions

#### Notify on Workflow Completion

```yaml
# .github/workflows/deploy.yml
name: Deploy

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Deploy
        run: ./deploy.sh

      - name: Notify VibeReal
        if: always()
        run: |
          curl -X POST ${{ secrets.VIBEREAL_HUB_URL }}/api/integrations/notify \
            -H "Authorization: Bearer ${{ secrets.VIBEREAL_API_KEY }}" \
            -H "Content-Type: application/json" \
            -d '{
              "title": "Deployment ${{ job.status }}",
              "body": "Commit: ${{ github.sha }}",
              "voiceText": "Deployment ${{ job.status }}",
              "priority": "${{ job.status == 'success' && 'normal' || 'high' }}",
              "source": "github-actions"
            }'
```

### Custom CLI Tool

Simple bash script to interact with VibeReal:

```bash
#!/bin/bash
# vibereal-cli

HUB_URL="${VIBEREAL_HUB_URL:-http://localhost:8080}"
API_KEY="${VIBEREAL_API_KEY}"

case "$1" in
  status)
    curl -s -H "Authorization: Bearer $API_KEY" \
      "$HUB_URL/api/integrations/status" | jq
    ;;

  sessions)
    curl -s -H "Authorization: Bearer $API_KEY" \
      "$HUB_URL/api/integrations/sessions" | jq
    ;;

  say)
    shift
    curl -s -X POST -H "Authorization: Bearer $API_KEY" \
      -H "Content-Type: application/json" \
      "$HUB_URL/api/integrations/command" \
      -d "{\"command\": \"$*\", \"source\": \"cli\"}" | jq
    ;;

  notify)
    curl -s -X POST -H "Authorization: Bearer $API_KEY" \
      -H "Content-Type: application/json" \
      "$HUB_URL/api/integrations/notify" \
      -d "{\"title\": \"$2\", \"body\": \"$3\", \"priority\": \"${4:-normal}\", \"source\": \"cli\"}" | jq
    ;;

  *)
    echo "Usage: vibereal-cli {status|sessions|say <command>|notify <title> <body> [priority]}"
    ;;
esac
```

Usage:
```bash
vibereal-cli status
vibereal-cli sessions
vibereal-cli say "tell container 1 to run tests"
vibereal-cli notify "Build Complete" "All tests passed" high
```

## Authentication

### API Keys

Generate API keys for external integrations:

```http
POST /api/keys
Authorization: Bearer <admin-key>
Content-Type: application/json

{
  "name": "Home Assistant",
  "permissions": ["read", "command", "notify"],
  "expiresAt": null
}
```

Response:
```json
{
  "id": "key_abc123",
  "name": "Home Assistant",
  "key": "vr_live_xxxxxxxxxxxxx",
  "permissions": ["read", "command", "notify"],
  "createdAt": "2026-02-27T10:00:00Z"
}
```

### Permission Levels

| Permission | Allows |
|------------|--------|
| `read` | Get status, list sessions |
| `command` | Send commands, messages |
| `notify` | Push notifications to AR |
| `approve` | Approve/deny actions |
| `webhooks` | Manage webhook subscriptions |
| `admin` | All permissions + key management |

## Rate Limits

| Endpoint | Limit |
|----------|-------|
| REST API (read) | 120/minute |
| REST API (write) | 60/minute |
| WebSocket messages | 100/minute |
| Webhook subscriptions | 10 per key |

## Error Responses

```json
{
  "error": {
    "code": "RATE_LIMITED",
    "message": "Too many requests",
    "retryAfter": 30
  }
}
```

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `UNAUTHORIZED` | 401 | Invalid or missing API key |
| `FORBIDDEN` | 403 | Insufficient permissions |
| `NOT_FOUND` | 404 | Resource not found |
| `RATE_LIMITED` | 429 | Too many requests |
| `INVALID_REQUEST` | 400 | Malformed request |
| `SESSION_UNAVAILABLE` | 503 | Session not connected |
