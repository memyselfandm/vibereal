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
│  │  - Voice recognition (Android SpeechRecognizer API)          │   │
│  │  - TTS for Claude responses (Android Native TTS)             │   │
│  │  - Hand tracking for fallback gestures                       │   │
│  └───────────────────────┬──────────────────────────────────────┘   │
│                          │ WebSocket                                │
└──────────────────────────┼──────────────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────────────┐
│                    Your Server (n8n + Docker)                        │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │  Session Hub (WebSocket Server)                                │ │
│  │  - Routes messages between AR app and Claude sessions          │ │
│  │  - Maintains session registry                                  │ │
│  │  - Pushes notifications to AR app                              │ │
│  └─────────────────────┬─────────────────┬────────────────────────┘ │
│                        │                 │                          │
│  ┌─────────────────────▼─┐   ┌───────────▼────────────────────────┐ │
│  │  n8n Workflows        │   │  Claude Container Manager          │ │
│  │  - Voice command NLU  │   │  - Manages Docker containers       │ │
│  │  - Notification logic │   │  - Each container = Claude session │ │
│  │  - Integrations       │   │                                    │ │
│  └───────────────────────┘   └────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

## Components

| Component | Description | Spec |
|-----------|-------------|------|
| **Session Hub** | WebSocket server routing messages between AR app and Claude sessions | [specs/01-session-hub.md](specs/01-session-hub.md) |
| **n8n Workflows** | Orchestration layer for voice command NLU, notifications, and integrations | [specs/02-n8n-workflows.md](specs/02-n8n-workflows.md) |
| **XREAL Unity App** | AR frontend running on Beam Pro with spatial UI and voice controls | [specs/03-xreal-unity-app.md](specs/03-xreal-unity-app.md) |
| **Container Manager** | Docker orchestration for isolated Claude Code environments | [specs/04-claude-container-manager.md](specs/04-claude-container-manager.md) |
| **Voice Interface** | Wake word detection, STT, NLU, and TTS systems | [specs/05-voice-interface.md](specs/05-voice-interface.md) |

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

## Hardware Requirements

- XREAL One glasses
- XREAL Beam Pro
- Server with Docker (for containerized sessions)
- n8n instance (can run on same server)

## Getting Started

*Coming soon - implementation in progress*

## License

MIT
