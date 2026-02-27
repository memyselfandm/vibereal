# XREAL Unity App Specification

## Overview

The XREAL Unity app is the AR frontend running on the Beam Pro, connected to XREAL One glasses. It provides a spatial interface for interacting with multiple Claude sessions through voice and gesture controls.

## Target Hardware

- **Glasses:** XREAL One
- **Compute Unit:** XREAL Beam Pro
- **SDK:** XREAL SDK 3.1.0+
- **Unity Version:** 2022.3 LTS or 2023.x

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Unity Application                                  │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                         UI Layer                                     │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐   │   │
│  │  │ Session      │  │ Notification │  │ Settings                 │   │   │
│  │  │ Panels       │  │ Toasts       │  │ Panel                    │   │   │
│  │  └──────────────┘  └──────────────┘  └──────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                       Core Services                                  │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐   │   │
│  │  │ Voice        │  │ WebSocket    │  │ Session                  │   │   │
│  │  │ Manager      │  │ Client       │  │ Manager                  │   │   │
│  │  └──────────────┘  └──────────────┘  └──────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                       XREAL SDK Layer                                │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐   │   │
│  │  │ Spatial      │  │ Hand         │  │ Controller               │   │   │
│  │  │ Tracking     │  │ Tracking     │  │ Input                    │   │   │
│  │  └──────────────┘  └──────────────┘  └──────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                       Platform Layer                                 │   │
│  │  ┌──────────────┐  ┌──────────────┐                                 │   │
│  │  │ Android      │  │ Android      │                                 │   │
│  │  │ TTS          │  │ STT          │                                 │   │
│  │  └──────────────┘  └──────────────┘                                 │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Scene Hierarchy

```
Scene: MainScene
├── XR Origin
│   ├── Camera Offset
│   │   └── Main Camera
│   ├── Left Hand Controller
│   └── Right Hand Controller
├── Spatial UI Root
│   ├── Session Panel Container
│   │   ├── Session Panel Prefab (x N)
│   │   └── Add Session Button
│   ├── Notification Container
│   │   └── Toast Prefab Pool
│   ├── Status Bar
│   │   ├── Clock
│   │   ├── Connection Status
│   │   └── Mic Status
│   └── Settings Panel
├── Managers
│   ├── VoiceManager
│   ├── SessionManager
│   ├── WebSocketClient
│   ├── NotificationManager
│   └── AudioManager
└── Environment
    └── Ambient Lighting
```

## Core Components

### 1. VoiceManager

Handles speech-to-text and text-to-speech using Android native APIs.

```csharp
public class VoiceManager : MonoBehaviour
{
    // Events
    public event Action<string> OnTranscriptionReceived;
    public event Action OnListeningStarted;
    public event Action OnListeningStopped;
    public event Action<VoiceError> OnError;

    // Configuration
    [SerializeField] private string wakeWord = "jarvis";
    [SerializeField] private float listeningTimeout = 10f;
    [SerializeField] private float confidenceThreshold = 0.7f;

    // TTS Settings
    [SerializeField] private float speechRate = 1.0f;
    [SerializeField] private float pitch = 1.0f;
    [SerializeField] private string languageCode = "en-US";

    // Public API
    public void StartListening();
    public void StopListening();
    public void Speak(string text, Priority priority = Priority.Normal);
    public void StopSpeaking();
    public bool IsListening { get; }
    public bool IsSpeaking { get; }

    // Wake word detection
    public void EnableWakeWordDetection();
    public void DisableWakeWordDetection();
}

public enum Priority
{
    Low,      // Can be interrupted
    Normal,   // Standard priority
    High      // Interrupts current speech
}
```

### 2. WebSocketClient

Manages connection to Session Hub.

```csharp
public class WebSocketClient : MonoBehaviour
{
    // Events
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<HubMessage> OnMessageReceived;
    public event Action<string> OnError;

    // Configuration
    [SerializeField] private string hubUrl;
    [SerializeField] private string apiKey;
    [SerializeField] private float reconnectDelay = 5f;
    [SerializeField] private int maxReconnectAttempts = 10;

    // Public API
    public void Connect();
    public void Disconnect();
    public void Send(HubMessage message);
    public ConnectionState State { get; }

    // Message helpers
    public void SendUserMessage(string sessionId, string content);
    public void SendApproval(string sessionId, string approvalId, bool approved);
    public void RequestSessionList();
    public void SetFocusSession(string sessionId);
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}
```

### 3. SessionManager

Tracks and manages Claude sessions.

```csharp
public class SessionManager : MonoBehaviour
{
    // Events
    public event Action<Session> OnSessionAdded;
    public event Action<Session> OnSessionRemoved;
    public event Action<Session> OnSessionUpdated;
    public event Action<Session> OnFocusChanged;

    // Public API
    public IReadOnlyList<Session> Sessions { get; }
    public Session FocusedSession { get; }

    public void SetFocus(string sessionId);
    public Session GetSession(string sessionId);
    public Session FindSessionByName(string name);  // Fuzzy match for voice
}

[Serializable]
public class Session
{
    public string Id;
    public string Name;
    public SessionType Type;
    public SessionStatus Status;
    public string CurrentTask;
    public DateTime LastActivity;
    public List<Approval> PendingApprovals;
    public List<ConversationMessage> RecentHistory;
}

public enum SessionType { Container, Laptop, Remote }
public enum SessionStatus { Idle, Thinking, WaitingInput, Executing, Error }
```

### 4. NotificationManager

Handles incoming notifications and displays them appropriately.

```csharp
public class NotificationManager : MonoBehaviour
{
    // Configuration
    [SerializeField] private float toastDuration = 5f;
    [SerializeField] private int maxVisibleToasts = 3;
    [SerializeField] private AudioClip notificationSound;
    [SerializeField] private AudioClip approvalSound;

    // Public API
    public void ShowNotification(Notification notification);
    public void DismissNotification(string notificationId);
    public void DismissAll();

    // Voice integration
    public void SpeakNotification(Notification notification);
}

[Serializable]
public class Notification
{
    public string Id;
    public string SessionId;
    public NotificationType Type;
    public NotificationPriority Priority;
    public string Title;
    public string Body;
    public string VoiceText;
    public DateTime Timestamp;
}

public enum NotificationType
{
    SessionUpdate,
    ApprovalRequired,
    TaskComplete,
    Error,
    Info
}

public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}
```

## UI Components

### Session Panel

A floating spatial panel displaying a single Claude session.

```
┌─────────────────────────────────────────────────────────────────┐
│ ● Container 1                                    [Thinking...] │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  User: Run the test suite and fix any failures                  │
│                                                                 │
│  Claude: I'll run the tests now...                              │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ $ pytest tests/ -v                                       │   │
│  │ ================================= test session starts    │   │
│  │ collected 45 items                                       │   │
│  │ ...                                                      │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│  [🎤 Speak]  [⌨️ Type]  [⏹️ Stop]  [📌 Pin]  [✕ Close]          │
└─────────────────────────────────────────────────────────────────┘
```

**Prefab Structure:**
```
SessionPanel
├── Header
│   ├── StatusIndicator (colored dot)
│   ├── SessionName (TextMeshPro)
│   └── StatusLabel (TextMeshPro)
├── ContentArea
│   ├── ScrollView
│   │   └── ConversationContent
│   │       ├── MessageBubble (prefab pool)
│   │       └── CodeBlock (prefab pool)
│   └── ScrollBar
├── Footer
│   ├── SpeakButton
│   ├── TypeButton
│   ├── StopButton
│   ├── PinButton
│   └── CloseButton
└── ApprovalOverlay (hidden by default)
    ├── ApprovalMessage
    ├── ApproveButton
    └── DenyButton
```

### Approval Dialog

Overlays the session panel when approval is needed.

```
┌─────────────────────────────────────────────────────────────────┐
│                    ⚠️ Approval Required                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Claude wants to execute:                                       │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ git push origin main                                     │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  Say "Approve" or "Deny", or use buttons below                  │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│         [✓ Approve]                    [✕ Deny]                 │
└─────────────────────────────────────────────────────────────────┘
```

### Status Bar

Always-visible bar at the top of view.

```
┌─────────────────────────────────────────────────────────────────┐
│ 🟢 Connected │ 3 Sessions │ 🎤 Listening │              10:42 AM │
└─────────────────────────────────────────────────────────────────┘
```

### Notification Toast

Temporary notifications that appear and fade.

```
┌─────────────────────────────────────────────────────────────┐
│ Container 2                                           [✕]   │
│ ✅ Tests completed: 45 passed, 0 failed                     │
└─────────────────────────────────────────────────────────────┘
```

## Interaction Design

### Voice Commands

| Command | Action |
|---------|--------|
| "Hey JARVIS" | Activate listening mode |
| "Status" / "What's happening" | Read status of all sessions |
| "Focus [session name]" | Switch focus to named session |
| "Send [message]" | Send message to focused session |
| "[Session name], [message]" | Send message to specific session |
| "Approve" / "Yes" | Approve pending action |
| "Deny" / "No" | Deny pending action |
| "Stop" / "Cancel" | Interrupt current Claude operation |
| "Read that again" | Repeat last notification |
| "Show sessions" | Display session overview |

### Hand Gestures (XREAL SDK)

| Gesture | Action |
|---------|--------|
| Pinch | Select / Confirm |
| Grab + Drag | Move panel |
| Pinch + Drag | Resize panel |
| Palm Open | Open menu |
| Swipe Left/Right | Navigate between sessions |

### Controller Input (Beam Pro)

| Input | Action |
|-------|--------|
| Trigger | Select |
| Grip | Grab / Move |
| Thumbstick | Scroll content |
| A Button | Quick speak (push-to-talk) |
| B Button | Open settings |
| Menu | Toggle all panels |

## Spatial Layout

### Default Layout

Sessions arranged in an arc in front of the user:

```
                    User's View (top-down)

                         [Session 2]
                        /           \
              [Session 1]           [Session 3]
                        \           /
                         \         /
                          \       /
                           [USER]
                              ▲
                           (facing)
```

### Layout Configuration

```csharp
[Serializable]
public class LayoutConfig
{
    public float panelDistance = 1.5f;      // meters from user
    public float panelWidth = 0.6f;         // meters
    public float panelHeight = 0.4f;        // meters
    public float arcAngle = 30f;            // degrees between panels
    public float verticalOffset = -0.1f;    // meters below eye level
    public bool followHead = true;          // panels follow head rotation
    public float followSmoothness = 5f;     // lower = smoother
}
```

### Pinned Panels

Users can pin panels to world space or relative positions:
- **World Pin:** Panel stays at fixed world position
- **Follow Pin:** Panel maintains relative position to head
- **Dock:** Panel minimizes to status bar

## Audio Design

### Sound Effects

| Event | Sound |
|-------|-------|
| Wake word detected | Soft chime |
| Listening started | Rising tone |
| Listening stopped | Falling tone |
| Message sent | Soft click |
| Notification (normal) | Gentle ping |
| Notification (approval) | Attention tone |
| Approval confirmed | Success chime |
| Error | Warning tone |

### TTS Voice Settings

```csharp
[Serializable]
public class TTSConfig
{
    public string voiceName = "en-US-Neural2-J";  // Or system default
    public float rate = 1.1f;                      // Slightly faster
    public float pitch = 1.0f;
    public float volume = 0.8f;

    // Summarization
    public int maxWordsBeforeSummary = 50;
    public bool summarizeLongResponses = true;
}
```

## Networking

### Connection State Machine

```
    ┌─────────────┐
    │ Disconnected│◄─────────────────────────────┐
    └──────┬──────┘                              │
           │ Connect()                           │
           ▼                                     │
    ┌─────────────┐                              │
    │ Connecting  │──── Timeout ─────────────────┤
    └──────┬──────┘                              │
           │ Success                             │
           ▼                                     │
    ┌─────────────┐                              │
    │  Connected  │──── Connection Lost ─────────┤
    └──────┬──────┘                              │
           │                                     │
           ▼                                     │
    ┌─────────────┐                              │
    │ Reconnecting│──── Max Retries ─────────────┘
    └─────────────┘
           │
           │ Success
           ▼
    ┌─────────────┐
    │  Connected  │
    └─────────────┘
```

### Offline Mode

When disconnected:
- Show cached session states (stale indicator)
- Queue voice commands for when reconnected
- Display reconnection status

## Performance Considerations

### Rendering
- Use XREAL's single-pass stereo rendering
- Pool UI elements (message bubbles, toasts)
- Limit visible conversation history (last 20 messages)
- Lazy load older messages on scroll

### Memory
- Max 5 session panels at once
- Compress stored conversation history
- Unload inactive panel resources

### Battery
- Reduce update frequency when glasses are off
- Pause voice detection when not in use
- Use efficient WebSocket heartbeats

## Build Configuration

### Player Settings
```
Company Name: YourCompany
Product Name: VibeReal
Package Name: com.yourcompany.vibereal
Minimum API Level: 29 (Android 10)
Target API Level: 33 (Android 13)
Scripting Backend: IL2CPP
Target Architectures: ARM64
```

### Required Permissions
```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.RECORD_AUDIO" />
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
```

### Dependencies
- XREAL SDK 3.1.0+
- NativeWebSocket (Unity WebSocket)
- TextMeshPro
- Android Speech Recognition Plugin
- Android TTS Plugin

## Testing

### Unit Tests
- VoiceManager command parsing
- SessionManager state transitions
- WebSocket message serialization

### Integration Tests
- End-to-end voice command flow (mock STT)
- Session panel UI updates on state change
- Notification display and dismissal

### Device Testing
- XREAL One + Beam Pro hardware
- Latency measurements (voice → response)
- Battery drain profiling
- Thermal monitoring

## File Structure

```
Assets/
├── Scenes/
│   └── MainScene.unity
├── Scripts/
│   ├── Core/
│   │   ├── VoiceManager.cs
│   │   ├── WebSocketClient.cs
│   │   ├── SessionManager.cs
│   │   └── NotificationManager.cs
│   ├── UI/
│   │   ├── SessionPanel.cs
│   │   ├── SessionPanelManager.cs
│   │   ├── NotificationToast.cs
│   │   ├── StatusBar.cs
│   │   └── ApprovalDialog.cs
│   ├── Input/
│   │   ├── HandGestureHandler.cs
│   │   └── ControllerInputHandler.cs
│   ├── Data/
│   │   ├── Session.cs
│   │   ├── Notification.cs
│   │   └── HubMessage.cs
│   └── Utils/
│       ├── AudioPool.cs
│       └── UIPool.cs
├── Prefabs/
│   ├── SessionPanel.prefab
│   ├── NotificationToast.prefab
│   ├── MessageBubble.prefab
│   └── CodeBlock.prefab
├── Audio/
│   ├── SFX/
│   └── Voice/
├── Materials/
├── Fonts/
└── Plugins/
    ├── XREAL/
    ├── AndroidSpeech/
    └── NativeWebSocket/
```
