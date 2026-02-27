# VibeReal

JARVIS-like AR interface for Claude Code sessions on XREAL glasses.

## Overview

VibeReal is a spatial computing application that provides a hands-free, voice-controlled interface to manage multiple Claude and Claude Code sessions through XREAL AR glasses. Talk to your AI coding assistants, approve actions, and monitor progress - all while keeping your hands on the keyboard.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                     XREAL One + Beam Pro                            │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Unity App (XREAL SDK 3.x)                                   │   │
│  │  - Spatial UI panels for each Claude session                 │   │
│  │  - Voice input (Android STT) + TTS output                    │   │
│  │  - Hand tracking for gesture fallback                        │   │
│  └───────────────────────┬──────────────────────────────────────┘   │
│                          │ WebSocket                                │
└──────────────────────────┼──────────────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────────────┐
│                         Session Hub                                  │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │  Core Server (Node.js / Bun)                                   │ │
│  │  - Voice command processing (NLU / intent classification)     │ │
│  │  - Session registry + message routing                         │ │
│  │  - Notification management + prioritization                   │ │
│  │  - External integrations API (webhooks, REST, WebSocket)      │ │
│  └─────────────────────┬─────────────────┬────────────────────────┘ │
│                        │                 │                          │
│  ┌─────────────────────▼─┐   ┌───────────▼────────────────────────┐ │
│  │  Claude Containers    │   │  External Systems                  │ │
│  │  (Docker-managed)     │   │  (n8n, Home Assistant, Slack...)   │ │
│  └───────────────────────┘   └────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

## Components

| Component | Description | Spec |
|-----------|-------------|------|
| **Session Hub** | Central server handling all logic: WebSocket connections, voice command NLU, notifications, and integrations API | [specs/01-session-hub.md](specs/01-session-hub.md) |
| **External Integrations** | REST/WebSocket/Webhook APIs for connecting n8n, Home Assistant, Slack, and other systems | [specs/02-external-integrations.md](specs/02-external-integrations.md) |
| **XREAL Unity App** | AR frontend with spatial UI, voice controls, and hand tracking | [specs/03-xreal-unity-app.md](specs/03-xreal-unity-app.md) |
| **Container Manager** | Docker orchestration for isolated Claude Code environments | [specs/04-container-manager.md](specs/04-container-manager.md) |
| **Voice Interface** | Wake word detection, STT/TTS, and conversation flow | [specs/05-voice-interface.md](specs/05-voice-interface.md) |

## Features

### Voice Commands
- **"Hey JARVIS"** - Wake word to activate
- **"Status"** - Get status of all sessions
- **"Container 1, run the tests"** - Send commands to specific sessions
- **"Approve" / "Deny"** - Respond to pending approvals
- **"Focus on laptop"** - Switch between sessions

### Spatial UI
- Floating panels for each Claude session
- Real-time conversation display
- Visual status indicators (idle, thinking, waiting)
- Approval dialogs with voice + gesture control

### Multi-Session Support
- Laptop Claude Code sessions (via Remote Control)
- Containerized Claude sessions (via Docker)
- Centralized notification system

### External Integrations
- **Webhooks** - Push events to n8n, Slack, Discord, etc.
- **REST API** - Send commands from Home Assistant, scripts, CI/CD
- **WebSocket** - Real-time bidirectional for custom dashboards

## Hardware Requirements

- XREAL One glasses
- XREAL Beam Pro
- Server with Docker (for containerized sessions)

## Getting Started

*Coming soon - implementation in progress*

## License

MIT
