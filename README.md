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

## Unity App (MVP)

The `unity/` folder contains a complete scaffolded Unity project for the XREAL AR app.

### What's Implemented

| Component | Scripts | Description |
|-----------|---------|-------------|
| **Core** | `WebSocketClient.cs`, `VoiceManager.cs`, `SessionManager.cs`, `NotificationManager.cs`, `AppBootstrap.cs`, `InputHandler.cs` | Business logic and coordination |
| **Voice** | `AndroidSTT.cs`, `AndroidTTS.cs` | Native Android speech recognition and text-to-speech via JNI |
| **UI** | `SessionPanelController.cs`, `ApprovalDialogController.cs`, `StatusIndicator.cs` | UI controllers for spatial panels |
| **Data** | `Session.cs`, `HubMessages.cs`, `AppConfig.cs` | Data models matching hub WebSocket protocol |

### Key Features
- Push-to-talk voice input using Android SpeechRecognizer
- TTS output with priority queue and markdown stripping
- WebSocket connection with auto-reconnect
- Session state tracking and approval workflow
- Controller input handling for Beam Pro

### Quick Start

1. Open `unity/` in Unity 2022.3 LTS
2. Import XREAL SDK 3.1.0+ from [developer.xreal.com](https://developer.xreal.com/download/)
3. Configure XR Plug-in Management → Enable XREAL
4. Create scene following `unity/README.md` instructions
5. Build → Android → APK
6. `adb install VibeReal.apk`

See [unity/README.md](unity/README.md) for detailed setup instructions.

### Configuration

Edit `unity/Assets/Resources/config.json`:
```json
{
  "hubUrl": "ws://YOUR_SERVER_IP:8080/client",
  "apiKey": "your-api-key"
}
```

## Session Hub (MVP)

The `hub/` folder contains a lightweight WebSocket relay server with a web test UI.

### Quick Start

```bash
cd hub && npm install
npm run dev                      # Start hub (default port 8080)
# Open http://localhost:8080     # Web test UI
npm run sim                      # Run demo simulator in another terminal
```

Set `PORT=8090` to use a different port. Set `HUB_API_KEY=secret` to enable client auth.

### How It Works

- Sessions (Claude Code instances) connect to `ws://host:port/session/{id}` and send status updates
- Clients (Unity app or web UI) connect to `ws://host:port/client` and receive relayed events
- Hub routes messages between them — session status, responses, and approval requests

## Project Structure

```
vibereal/
├── hub/                      # Session Hub server (Node.js)
│   ├── src/                  # TypeScript source
│   ├── public/               # Web test UI
│   └── tools/                # Session simulator
├── specs/                    # Detailed specifications
│   ├── 01-session-hub.md
│   ├── 02-external-integrations.md
│   ├── 03-xreal-unity-app.md
│   ├── 04-container-manager.md
│   └── 05-voice-interface.md
└── unity/                    # Unity project (XREAL AR app)
    ├── Assets/
    │   ├── Scripts/          # C# source code
    │   ├── Plugins/Android/  # Android manifest
    │   └── Resources/        # Runtime config
    ├── Packages/             # Package manifest
    └── ProjectSettings/      # Unity settings
```

## License

MIT
