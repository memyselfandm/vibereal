# n8n Workflows Specification

## Overview

n8n serves as the orchestration layer for VibeReal, handling complex automation logic, integrations with external services, and providing a visual way to customize behavior without code changes.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              n8n Instance                                    │
│                                                                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────────┐  │
│  │ Voice Command   │  │ Session         │  │ Notification                │  │
│  │ Router          │  │ Lifecycle       │  │ Manager                     │  │
│  └────────┬────────┘  └────────┬────────┘  └──────────────┬──────────────┘  │
│           │                    │                          │                 │
│  ┌────────▼────────┐  ┌────────▼────────┐  ┌──────────────▼──────────────┐  │
│  │ Intent          │  │ Container       │  │ Priority                    │  │
│  │ Classifier      │  │ Orchestrator    │  │ Router                      │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────────────────┘  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    External Integrations                             │   │
│  │  [Slack] [Discord] [Email] [Calendar] [GitHub] [Linear] [Todoist]   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Workflows

### 1. Voice Command Router

**Trigger:** Webhook from Session Hub
**Purpose:** Parse voice commands and route to appropriate actions

```
┌──────────────┐    ┌────────────────┐    ┌─────────────────┐    ┌──────────────┐
│ Webhook      │───►│ Intent         │───►│ Route by        │───►│ Execute      │
│ (POST)       │    │ Classification │    │ Intent Type     │    │ Action       │
└──────────────┘    └────────────────┘    └─────────────────┘    └──────────────┘
```

#### Webhook Input
```json
{
  "transcript": "Hey JARVIS, ask container 2 to run the tests",
  "confidence": 0.95,
  "sessionContext": {
    "focusedSession": "container-1",
    "availableSessions": ["container-1", "container-2", "laptop-main"]
  }
}
```

#### Intent Classification (Claude Node)

System prompt for intent extraction:
```
You are a voice command parser for a developer assistant called JARVIS.
Parse the user's voice command and extract:
1. intent: The action to perform
2. target: Which Claude session to target (if mentioned)
3. parameters: Any additional parameters

Intents:
- send_message: Send a message to a Claude session
- get_status: Ask for status of sessions
- approve: Approve a pending action
- deny: Deny a pending action
- switch_focus: Change the focused session
- list_sessions: List all available sessions
- interrupt: Stop current Claude operation
- system_command: Non-Claude system commands

Output JSON only.
```

#### Intent Output
```json
{
  "intent": "send_message",
  "target": "container-2",
  "parameters": {
    "message": "run the tests"
  },
  "confidence": 0.92
}
```

#### Route Actions

| Intent | Action |
|--------|--------|
| `send_message` | Forward to Session Hub with target session |
| `get_status` | Query Session Hub for status, format for TTS |
| `approve` / `deny` | Send approval decision to Session Hub |
| `switch_focus` | Update focus in Session Hub |
| `list_sessions` | Query and format session list for TTS |
| `interrupt` | Send interrupt signal to target session |

### 2. Session Lifecycle Manager

**Trigger:** Webhook from Container Manager
**Purpose:** Handle session creation, updates, and cleanup

```
┌──────────────┐    ┌────────────────┐    ┌─────────────────┐    ┌──────────────┐
│ Webhook      │───►│ Event Type     │───►│ Process Event   │───►│ Notify       │
│ (Container)  │    │ Router         │    │                 │    │ Session Hub  │
└──────────────┘    └────────────────┘    └─────────────────┘    └──────────────┘
```

#### Events Handled

| Event | Action |
|-------|--------|
| `container_started` | Register new session with Hub |
| `container_stopped` | Unregister session, notify user |
| `container_error` | Alert user, attempt restart |
| `health_check_failed` | Attempt recovery, escalate if persistent |

### 3. Notification Manager

**Trigger:** Webhook from Session Hub
**Purpose:** Format and route notifications based on priority and context

```
┌──────────────┐    ┌────────────────┐    ┌─────────────────┐    ┌──────────────┐
│ Webhook      │───►│ Priority       │───►│ Format for      │───►│ Send to      │
│ (Hub Event)  │    │ Assessment     │    │ Output Channel  │    │ Channels     │
└──────────────┘    └────────────────┘    └─────────────────┘    └──────────────┘
```

#### Priority Levels

| Priority | Criteria | Notification Behavior |
|----------|----------|----------------------|
| `critical` | Approval required, errors | Immediate voice + visual |
| `high` | Task complete, waiting input | Voice if idle, always visual |
| `normal` | Status updates | Visual only |
| `low` | Background info | Batched, visual only |

#### Notification Formatting (Claude Node)

System prompt:
```
You are formatting notifications for voice output in an AR interface.
Keep messages concise (under 15 words for voice).
Use natural speech patterns.
Include session name when multiple sessions exist.

Input: Raw notification data
Output: JSON with voiceText and displayText
```

#### Output
```json
{
  "voiceText": "Container 2 needs approval to push to main branch",
  "displayText": "Container 2: Approval Required\ngit push origin main",
  "priority": "critical"
}
```

### 4. Smart Summary Generator

**Trigger:** Scheduled (every 30 min) or on-demand
**Purpose:** Generate summaries of all session activity

```
┌──────────────┐    ┌────────────────┐    ┌─────────────────┐    ┌──────────────┐
│ Schedule /   │───►│ Gather Session │───►│ Generate        │───►│ Store /      │
│ Manual Trig  │    │ Activity       │    │ Summary         │    │ Notify       │
└──────────────┘    └────────────────┘    └─────────────────┘    └──────────────┘
```

#### Summary Output
```json
{
  "period": "last_30_minutes",
  "summary": "Container 1 completed the API refactor with 3 commits. Container 2 is running integration tests (15 min remaining). Laptop session is idle.",
  "highlights": [
    {"session": "container-1", "event": "Completed API refactor"},
    {"session": "container-2", "event": "Running integration tests"}
  ],
  "pendingActions": 0
}
```

### 5. External Integration Router

**Trigger:** Session Hub events
**Purpose:** Push notifications to external services

#### Slack Integration
```
┌──────────────┐    ┌────────────────┐    ┌─────────────────┐
│ Hub Event    │───►│ Filter by      │───►│ Slack API       │
│              │    │ Importance     │    │ Post Message    │
└──────────────┘    └────────────────┘    └─────────────────┘
```

#### GitHub Integration
```
┌──────────────┐    ┌────────────────┐    ┌─────────────────┐
│ PR/Issue     │───►│ Route to       │───►│ Session Hub     │
│ Webhook      │    │ Relevant       │    │ (Notify User)   │
│              │    │ Session        │    │                 │
└──────────────┘    └────────────────┘    └─────────────────┘
```

### 6. Context Enrichment Pipeline

**Trigger:** Before sending user message to Claude
**Purpose:** Enrich messages with relevant context

```
┌──────────────┐    ┌────────────────┐    ┌─────────────────┐    ┌──────────────┐
│ User Message │───►│ Detect Context │───►│ Fetch Relevant  │───►│ Augmented    │
│              │    │ Needs          │    │ Data            │    │ Message      │
└──────────────┘    └────────────────┘    └─────────────────┘    └──────────────┘
```

#### Context Sources
- Recent git activity (commits, branches)
- Open PRs and issues
- Calendar events (meetings coming up)
- Previous session summaries

## Webhook Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/webhook/voice-command` | POST | Process voice commands |
| `/webhook/container-event` | POST | Container lifecycle events |
| `/webhook/hub-notification` | POST | Session Hub notifications |
| `/webhook/github` | POST | GitHub webhooks |
| `/webhook/slack` | POST | Slack slash commands |

## Environment Variables

```bash
# Session Hub
SESSION_HUB_URL=ws://session-hub:8080
SESSION_HUB_API_KEY=your-key

# Claude API (for intent classification)
ANTHROPIC_API_KEY=your-key

# External Services
SLACK_BOT_TOKEN=xoxb-...
SLACK_CHANNEL_ID=C...
GITHUB_TOKEN=ghp_...
LINEAR_API_KEY=lin_...

# Feature Flags
ENABLE_SLACK_NOTIFICATIONS=true
ENABLE_GITHUB_INTEGRATION=true
VOICE_CONFIDENCE_THRESHOLD=0.7
```

## Workflow Templates

### Template: Voice Command Handler
```json
{
  "name": "Voice Command Router",
  "nodes": [
    {
      "name": "Webhook",
      "type": "n8n-nodes-base.webhook",
      "parameters": {
        "path": "voice-command",
        "method": "POST"
      }
    },
    {
      "name": "Intent Classifier",
      "type": "@n8n/n8n-nodes-langchain.lmChatAnthropic",
      "parameters": {
        "model": "claude-haiku-3-5-20241022",
        "systemMessage": "...",
        "maxTokens": 200
      }
    },
    {
      "name": "Route Intent",
      "type": "n8n-nodes-base.switch",
      "parameters": {
        "rules": [
          {"value": "send_message", "output": 0},
          {"value": "get_status", "output": 1},
          {"value": "approve", "output": 2}
        ]
      }
    }
  ]
}
```

## Error Handling

### Retry Strategy
- Intent classification: 2 retries with exponential backoff
- External API calls: 3 retries with 5s, 15s, 30s delays
- Session Hub calls: Immediate retry, then queue for later

### Fallback Responses
```json
{
  "low_confidence": "I didn't quite catch that. Could you repeat?",
  "no_sessions": "No Claude sessions are currently active.",
  "session_not_found": "I couldn't find a session called {name}.",
  "hub_unavailable": "Session Hub is temporarily unavailable. Please try again."
}
```

## Monitoring

### Execution Logging
- All workflow executions logged with input/output
- Failed executions trigger alerts

### Metrics
- Voice command success rate
- Intent classification accuracy
- Average response latency
- Notification delivery rate

## Deployment

### Docker Compose Integration
```yaml
services:
  n8n:
    image: n8nio/n8n:latest
    ports:
      - "5678:5678"
    environment:
      - N8N_BASIC_AUTH_ACTIVE=true
      - N8N_BASIC_AUTH_USER=admin
      - N8N_BASIC_AUTH_PASSWORD=${N8N_PASSWORD}
      - WEBHOOK_URL=https://n8n.yourdomain.com
    volumes:
      - n8n_data:/home/node/.n8n
      - ./workflows:/home/node/workflows
```

### Workflow Import
Workflows are exported as JSON and can be imported via:
1. n8n UI: Settings > Import from File
2. API: `POST /workflows` with JSON body
3. CLI: `n8n import:workflow --input=workflow.json`
