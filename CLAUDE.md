# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VibeReal is a JARVIS-like AR interface for managing Claude Code sessions through XREAL glasses. It provides voice-controlled, hands-free interaction with multiple Claude/Claude Code sessions via spatial computing.

## Architecture

The system has two main parts, with only the Unity app currently implemented:

1. **Session Hub** (Node.js/Bun, not yet implemented) - Central server handling WebSocket connections, voice command NLU, session registry, notification management, and external integrations (n8n, Home Assistant, Slack)
2. **XREAL Unity App** (`unity/`) - AR frontend with spatial UI panels, voice controls (Android STT/TTS via JNI), and WebSocket client that connects to the Session Hub

The Unity app communicates with the Session Hub over WebSocket using a JSON message protocol defined in `unity/Assets/Scripts/Data/HubMessages.cs`. Messages are typed via a `type` field and parsed by `HubMessageParser`.

## Unity App Structure

All C# code lives under `unity/Assets/Scripts/` in the `VibeReal` namespace (assembly: `VibeReal.asmdef`):

- **Core/** - `AppBootstrap` (entry point, auto-discovers managers via `FindObjectOfType`), `WebSocketClient` (connection lifecycle, auto-reconnect with exponential backoff, message routing via events), `SessionManager`, `VoiceManager`, `NotificationManager`, `InputHandler`
- **Voice/** - `AndroidSTT` / `AndroidTTS` - native Android speech via JNI bridge. STT must be initialized on Android UI thread. TTS `UtteranceProgressListener` cannot be proxied via `AndroidJavaProxy` (abstract class, not interface).
- **UI/** - `SessionPanelController`, `ApprovalDialogController`, `StatusIndicator` - world-space canvas controllers
- **Data/** - `Session`, `HubMessages` (WebSocket protocol types), `AppConfig` (loads from `Resources/config.json`)
- **Editor/** - `SceneSetup` (editor utilities for UI and XR setup, in `VibeReal.Editor` assembly)

## Scene Hierarchy (`vibereal.unity`)

```
XR Origin (VR)              - XR Interaction Toolkit origin with TrackedPoseDriver camera
XREAL Interaction Setup     - XREAL SDK prefab (session manager, tracking, event system, interaction manager)
Directional Light
Managers                    - AppBootstrap, WebSocketClient, SessionManager, VoiceManager, NotificationManager, InputHandler
Canvas                      - World Space, root-level (NOT parented to camera), TrackedDeviceGraphicRaycaster
  StatusBar                 - Connection status, session count, mic indicator, time
  SessionPanel              - Session name, status, conversation scroll, PTT indicator
  ApprovalDialog            - Hidden by default, shown for approval requests
```

## Development

### Unity Setup
- **Unity 6** (6000.3.x) with Android Build Support (includes SDK/NDK/JDK)
- **XREAL SDK 3.1.0** - imported from local tarball (`com.xreal.xr.tar.gz`) via Package Manager
- NativeWebSocket package auto-resolves from git URL in `Packages/manifest.json`
- Build target: Android (ARM64, IL2CPP, min API 29, target API 33)

### Critical XR Configuration
These settings are required for the app to render properly on XREAL glasses:

- **XR Plug-in Management** (Android tab): XREAL must be checked, auto-initialize enabled (`m_AutomaticLoading: 1`, `m_AutomaticRunning: 1` in `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`)
- **Graphics API**: Must be **OpenGLES3** only (XREAL doesn't support Vulkan). Set in Player Settings > Other Settings > Auto Graphics API = unchecked
- **Active Input Handling**: Set to "Input System Package (New)" (`activeInputHandler: 1`)
- **Camera**: Clear Flags = Solid Color, Background = black transparent (0,0,0,0)
- **Canvas**: Must be root-level World Space (not parented to camera), use `TrackedDeviceGraphicRaycaster` (not `GraphicRaycaster`)

### Building & Deploying
1. Open `unity/` folder in Unity Hub
2. Build And Run (or Build > `adb install -r VibeReal.apk`)
3. App launches automatically on Beam Pro; glasses display the AR UI

### Editor Utilities
- **VibeReal > Setup UI** - Creates the full UI hierarchy with wired serialized fields
- **VibeReal > Fix XR Setup** - Fixes Canvas, camera, raycaster, and graphics API settings
- **VibeReal > Add Scene To Build** - Registers active scene in build settings

### Configuration
Runtime config in `unity/Assets/Resources/config.json` - set `hubUrl` to Session Hub WebSocket endpoint.

## Specs

Detailed component specifications in `specs/`:
- `01-session-hub.md` - Hub server design
- `02-external-integrations.md` - REST/WebSocket/Webhook APIs
- `03-xreal-unity-app.md` - Unity app spec
- `04-container-manager.md` - Docker orchestration for Claude containers
- `05-voice-interface.md` - Wake word, STT/TTS, conversation flow

## Key Conventions

- C# namespace: `VibeReal` with sub-namespaces (`VibeReal.Core`, `VibeReal.Data`, etc.)
- WebSocket message protocol uses `JsonUtility` serialization with `[Serializable]` classes inheriting from `HubMessage`
- Managers are MonoBehaviours placed on a single "Managers" GameObject, auto-discovered by `AppBootstrap`
- Package name: `com.vibereal.app`

## Known Issues

- XREAL SDK 3.1.0 `XREALVirtualController` prefab has a missing script reference in Unity 6 (non-blocking warning)
- XREAL native `.so` plugins are not 16KB-aligned (warning on Android 15+, non-blocking)
- TTS `UtteranceProgressListener` cannot be proxied - speech completion callbacks not available
