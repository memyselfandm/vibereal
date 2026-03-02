# VibeReal Unity App

JARVIS-like AR interface for Claude Code sessions on XREAL glasses.

## Prerequisites

- **Unity 2022.3 LTS** (recommended) or Unity 2023.x
- **XREAL SDK 3.1.0+** - Download from [developer.xreal.com](https://developer.xreal.com/download/)
- **Android Build Support** module for Unity
- **XREAL Beam Pro** device for testing

## Quick Start

### 1. Open Project in Unity

1. Open Unity Hub
2. Click "Add" and select this `unity` folder
3. Open with Unity 2022.3 LTS
4. Wait for package resolution (NativeWebSocket will be downloaded automatically)

### 2. Import XREAL SDK

1. Download XREAL SDK 3.1.0+ from the developer portal
2. In Unity: **Window > Package Manager**
3. Click **+** > **Add package from tarball...**
4. Select the downloaded XREAL SDK `.tgz` file
5. Import the package

### 3. Configure XR Settings

1. Go to **Edit > Project Settings > XR Plug-in Management**
2. Select the **Android** tab
3. Check **XREAL** (or install it if not visible)
4. Go to **XREAL** settings and configure as needed

### 4. Create the Scene

Since Unity scenes can't be fully scaffolded outside the editor, create the main scene:

1. **File > New Scene** (Basic 3D)
2. Save as `Assets/Scenes/MainScene.unity`

3. **Add XR Origin:**
   - If using XREAL samples: drag the XR Origin prefab from XREAL SDK samples
   - Or: **GameObject > XR > XR Origin (Action-based)**

4. **Create Managers GameObject:**
   - Create empty GameObject named "Managers"
   - Add these components:
     - `WebSocketClient`
     - `SessionManager`
     - `VoiceManager`
     - `NotificationManager`

5. **Create UI Canvas:**
   - **GameObject > UI > Canvas**
   - Set **Render Mode** to **World Space**
   - Position at (0, 0, 1.5) - 1.5m in front of camera
   - Scale to (0.001, 0.001, 0.001)
   - Add `Canvas Scaler` component

6. **Create Session Panel:**
   - Right-click Canvas > **UI > Panel**
   - Add `SessionPanelController` component
   - Add child objects:
     - Header (TextMeshPro): Session name
     - Status indicator (Image)
     - Conversation scroll view
     - PTT indicator

7. **Create Approval Dialog:**
   - Create another Panel (hidden by default)
   - Add `ApprovalDialogController` component
   - Add Approve/Deny buttons

8. **Create Status Bar:**
   - Create Panel at top of canvas
   - Add `StatusIndicator` component

### 5. Configure the App

Edit `Assets/Resources/config.json`:

```json
{
  "hubUrl": "ws://YOUR_SERVER_IP:8080/client",
  "apiKey": "your-api-key-if-required"
}
```

### 6. Set Up Input

For push-to-talk on XREAL Beam Pro controller:

1. **Edit > Project Settings > Input System Package**
2. Create Input Action for "PushToTalk" bound to controller button (e.g., A button)
3. In `VoiceManager`, add input handling:

```csharp
// In a separate InputHandler script
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [SerializeField] private VoiceManager voiceManager;
    [SerializeField] private InputActionReference pttAction;

    private void OnEnable()
    {
        pttAction.action.started += OnPttPressed;
        pttAction.action.canceled += OnPttReleased;
    }

    private void OnDisable()
    {
        pttAction.action.started -= OnPttPressed;
        pttAction.action.canceled -= OnPttReleased;
    }

    private void OnPttPressed(InputAction.CallbackContext ctx) => voiceManager.StartListening();
    private void OnPttReleased(InputAction.CallbackContext ctx) => voiceManager.StopListening();
}
```

### 7. Build APK

1. **File > Build Settings**
2. Switch platform to **Android**
3. Add `MainScene` to build
4. Click **Player Settings** and verify:
   - Package Name: `com.vibereal.app`
   - Minimum API Level: 29
   - Target API Level: 33
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64 only
5. Click **Build** and choose output location

### 8. Install on Device

```bash
adb install -r VibeReal.apk
```

Or use **Build And Run** in Unity with device connected.

## Project Structure

```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── WebSocketClient.cs     # Hub connection
│   │   ├── SessionManager.cs      # Session state
│   │   ├── VoiceManager.cs        # STT/TTS coordination
│   │   └── NotificationManager.cs # Notifications & approvals
│   ├── Voice/
│   │   ├── AndroidSTT.cs          # Speech-to-text
│   │   └── AndroidTTS.cs          # Text-to-speech
│   ├── UI/
│   │   ├── SessionPanelController.cs
│   │   ├── ApprovalDialogController.cs
│   │   └── StatusIndicator.cs
│   └── Data/
│       ├── Session.cs             # Session model
│       ├── HubMessages.cs         # WebSocket messages
│       └── AppConfig.cs           # Configuration
├── Plugins/Android/
│   └── AndroidManifest.xml        # Permissions
├── Resources/
│   └── config.json                # Runtime config
└── Scenes/
    └── MainScene.unity            # Main scene (create in Editor)
```

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `hubUrl` | Session Hub WebSocket URL | `ws://192.168.1.100:8080/client` |
| `apiKey` | API key for authentication | (empty) |
| `languageCode` | Speech recognition language | `en-US` |
| `speechRate` | TTS speech rate (0.5-2.0) | `1.1` |
| `maxTtsWords` | Max words before truncation | `50` |
| `reconnectDelaySeconds` | Initial reconnect delay | `5` |
| `panelDistance` | UI panel distance (meters) | `1.5` |

## Voice Commands

When connected to the Session Hub, these voice commands are processed:

| Command | Action |
|---------|--------|
| "Status" / "What's happening" | Get status of all sessions |
| "Tell [session] to [message]" | Send command to session |
| "Approve" / "Yes" | Approve pending action |
| "Deny" / "No" | Deny pending action |

## Troubleshooting

### WebSocket won't connect
- Check `hubUrl` in config.json
- Verify Session Hub is running
- Check firewall/network settings
- Verify Beam Pro has network access

### Speech recognition not working
- Check RECORD_AUDIO permission is granted
- Verify microphone access in Android settings
- Try offline speech recognition if network is slow

### App crashes on start
- Check Android logcat: `adb logcat -s Unity`
- Verify IL2CPP build completed successfully
- Check for missing references in scene

### UI not visible
- Verify Canvas is set to World Space
- Check Canvas position (should be ~1.5m in front)
- Verify XR Origin is set up correctly

## Dependencies

| Package | Version | Source |
|---------|---------|--------|
| NativeWebSocket | latest | Git URL |
| XR Interaction Toolkit | 2.5.4 | Unity Registry |
| XR Management | 4.4.0 | Unity Registry |
| TextMeshPro | 3.0.6 | Unity Registry |
| Input System | 1.7.0 | Unity Registry |
| XREAL SDK | 3.1.0+ | Manual import |

## License

MIT
