# AR UI MVP Specification

## Scope

The absolute minimum to prove the AR interface concept works end-to-end: connect to a hub, display a session, and interact with it.

## What's In

### Stub Server (`stub-server/`)
- Node.js WebSocket server on port 8080
- Serves fake session data (2 sessions) on connect
- Responds to `send_message`, `voice_command`, `approval_decision`, `list_sessions`, `set_focus`
- Periodically emits fake events: status updates (8s), approval requests (20s), info notifications (30s)
- Simulates Claude response flow: ack → thinking → streamed response → idle

### Unity AR App (`unity/Assets/Scripts/`)

**Data layer** — Serializable models matching the hub's JSON protocol:
- `SessionData`, `ConversationMessage`, `ApprovalData`
- `NotificationData`, `NotificationMetadata`
- `HubMessage` types (incoming + outgoing)

**Core services** — MonoBehaviours wired via events:
- `WebSocketClient` — connect, reconnect, send/receive JSON, dispatch typed events
- `SessionManager` — track sessions, focused session, conversation history
- `NotificationManager` — queue notifications, track pending approvals, respond

**UI components** — MonoBehaviours bound to Unity UI elements in the inspector:
- `SessionPanelUI` — header (status dot + name + label), scrollable conversation, text input + send, approval overlay
- `StatusBarUI` — connection dot + label, session count, clock
- `NotificationToastUI` — auto-dismissing toast queue for non-approval notifications

## What's Out

- Voice input / TTS (no mic, no wake word, no speech)
- Hand gestures / spatial interaction
- Multiple simultaneous panels (one panel, switchable)
- Panel movement / pinning / docking
- Sound effects
- Settings UI
- Offline mode / caching
- XREAL SDK integration (runs on any Unity target for now)
- Authentication / API keys

## How to Run

### Stub server
```bash
cd stub-server
npm install
npm start
# Listening on ws://localhost:8080
```

### Unity app
1. Open `unity/` as a Unity project (2022.3 LTS+)
2. Import TextMeshPro (Window → TextMeshPro → Import TMP Essential Resources)
3. Build the scene hierarchy (see below) and wire MonoBehaviour references in the inspector
4. Set `WebSocketClient.hubUrl` to `ws://<your-machine-ip>:8080`
5. Enter Play mode

### Scene Setup

```
MainScene
├── Managers (Empty GameObject)
│   ├── WebSocketClient      [attach WebSocketClient.cs]
│   ├── SessionManager       [attach SessionManager.cs, ref WebSocketClient]
│   └── NotificationManager  [attach NotificationManager.cs, ref WebSocketClient]
├── Canvas (World Space for AR, Screen Space for testing)
│   ├── SessionPanel         [attach SessionPanelUI.cs]
│   │   ├── Header
│   │   │   ├── StatusDot (Image)
│   │   │   ├── SessionName (TMP_Text)
│   │   │   └── StatusLabel (TMP_Text)
│   │   ├── ConversationArea (ScrollRect)
│   │   │   └── Content
│   │   │       └── ConversationText (TMP_Text)
│   │   ├── InputArea
│   │   │   ├── MessageInput (TMP_InputField)
│   │   │   └── SendButton (Button)
│   │   └── ApprovalOverlay (inactive by default)
│   │       ├── ApprovalBody (TMP_Text)
│   │       ├── ApproveButton (Button)
│   │       └── DenyButton (Button)
│   ├── StatusBar            [attach StatusBarUI.cs]
│   │   ├── ConnectionDot (Image)
│   │   ├── ConnectionLabel (TMP_Text)
│   │   ├── SessionCount (TMP_Text)
│   │   └── Clock (TMP_Text)
│   └── NotificationArea     [attach NotificationToastUI.cs]
│       └── ToastRoot (inactive by default)
│           ├── ToastSession (TMP_Text)
│           ├── ToastTitle (TMP_Text)
│           └── ToastBody (TMP_Text)
└── Directional Light
```

## Success Criteria

1. Start stub server, enter Play mode in Unity
2. Status bar shows "Connected" with green dot
3. Session panel displays "Container 1" with conversation history
4. Type a message and send — see Claude's simulated response appear
5. After ~20s, approval overlay appears — click Approve or Deny
6. Notification toasts appear and auto-dismiss for info/error events
7. Status dot changes color as session status cycles
