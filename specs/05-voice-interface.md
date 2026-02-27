# Voice Interface Specification

## Overview

The Voice Interface is the primary interaction method for VibeReal, enabling hands-free control of Claude sessions. It handles wake word detection, speech-to-text, natural language understanding, and text-to-speech output.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Voice Interface                                    │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    Audio Input Pipeline                              │   │
│  │                                                                      │   │
│  │  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────────────┐  │   │
│  │  │ Mic      │──►│ VAD      │──►│ Wake     │──►│ Speech-to-Text   │  │   │
│  │  │ Capture  │   │ (Voice   │   │ Word     │   │ (Android ASR)    │  │   │
│  │  │          │   │ Activity)│   │ Detect   │   │                  │  │   │
│  │  └──────────┘   └──────────┘   └──────────┘   └────────┬─────────┘  │   │
│  │                                                        │            │   │
│  └────────────────────────────────────────────────────────┼────────────┘   │
│                                                           │                │
│  ┌────────────────────────────────────────────────────────▼────────────┐   │
│  │                    NLU Pipeline                                      │   │
│  │  ┌──────────────────┐   ┌──────────────────┐   ┌────────────────┐   │   │
│  │  │ Intent           │──►│ Entity           │──►│ Command        │   │   │
│  │  │ Classification   │   │ Extraction       │   │ Builder        │   │   │
│  │  └──────────────────┘   └──────────────────┘   └────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    Audio Output Pipeline                             │   │
│  │  ┌──────────────────┐   ┌──────────────────┐   ┌────────────────┐   │   │
│  │  │ Response         │──►│ Text-to-Speech   │──►│ Audio          │   │   │
│  │  │ Formatter        │   │ (Android TTS)    │   │ Output         │   │   │
│  │  └──────────────────┘   └──────────────────┘   └────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Audio Input Pipeline

### 1. Microphone Configuration

XREAL One glasses have 4 microphones for spatial audio capture.

```csharp
public class MicrophoneConfig
{
    // Use glasses microphones when available
    public AudioSource PreferredSource = AudioSource.Glasses;

    // Fallback to Beam Pro mic
    public AudioSource FallbackSource = AudioSource.Device;

    // Audio format
    public int SampleRate = 16000;
    public int Channels = 1;
    public int BitsPerSample = 16;

    // Buffer settings
    public int BufferLengthMs = 100;
    public int BufferCount = 3;
}

public enum AudioSource
{
    Glasses,      // XREAL One microphones
    Device,       // Beam Pro microphone
    Bluetooth     // Connected Bluetooth headset
}
```

### 2. Voice Activity Detection (VAD)

Detect when the user is speaking to avoid processing silence.

```csharp
public class VoiceActivityDetector
{
    // Configuration
    public float EnergyThreshold = 0.01f;         // Minimum audio energy
    public float SpeechStartDelay = 100f;         // ms before speech starts
    public float SpeechEndDelay = 500f;           // ms of silence to end
    public float MaxSpeechDuration = 30000f;      // ms max utterance

    // State
    public bool IsSpeechDetected { get; private set; }
    public float CurrentEnergy { get; private set; }

    // Events
    public event Action OnSpeechStart;
    public event Action OnSpeechEnd;
    public event Action<float[]> OnAudioFrame;

    // Methods
    public void ProcessAudioFrame(float[] samples);
    public void Reset();
}
```

### 3. Wake Word Detection

Two-stage wake word system for low-latency activation.

```csharp
public class WakeWordDetector
{
    // Configuration
    public string WakeWord = "jarvis";
    public string[] AlternativeWakeWords = { "hey jarvis", "ok jarvis" };
    public float Sensitivity = 0.5f;              // 0-1, higher = more sensitive

    // State
    public bool IsListening { get; private set; }
    public bool IsAwake { get; private set; }

    // Events
    public event Action OnWakeWordDetected;
    public event Action<float> OnWakeWordPartialMatch;

    // Methods
    public void StartListening();
    public void StopListening();
    public void ProcessAudio(float[] samples);
}
```

#### Wake Word Implementation Options

| Option | Pros | Cons |
|--------|------|------|
| **Picovoice Porcupine** | Custom wake words, on-device, low latency | Requires license for commercial |
| **Snowboy** | Open source, custom wake words | Deprecated, less accurate |
| **Keyword spotting in ASR** | No extra library, simple | Higher latency, less reliable |
| **Always listening** | No wake word needed | Battery drain, privacy concerns |

**Recommendation:** Use Picovoice Porcupine for custom "JARVIS" wake word with on-device processing.

### 4. Speech-to-Text (ASR)

Using Android's SpeechRecognizer API for on-device and cloud STT.

```csharp
public class SpeechToText
{
    // Configuration
    public string LanguageCode = "en-US";
    public bool PreferOffline = true;             // Use offline when available
    public bool EnablePartialResults = true;
    public int MaxAlternatives = 3;

    // State
    public bool IsListening { get; private set; }
    public string CurrentTranscript { get; private set; }

    // Events
    public event Action<string> OnPartialResult;
    public event Action<TranscriptionResult> OnFinalResult;
    public event Action<SpeechError> OnError;

    // Methods
    public void StartListening();
    public void StopListening();
    public void Cancel();
}

public class TranscriptionResult
{
    public string Text;
    public float Confidence;
    public string[] Alternatives;
    public bool IsFinal;
}

public enum SpeechError
{
    NoMatch,
    NetworkError,
    AudioError,
    ServerError,
    Timeout,
    NoSpeechInput
}
```

#### Android Integration

```csharp
// Unity plugin interface for Android SpeechRecognizer
public interface IAndroidSpeechRecognizer
{
    void Initialize(string languageCode);
    void StartListening();
    void StopListening();
    void Destroy();

    // Callbacks (invoked on Unity thread)
    event Action OnReadyForSpeech;
    event Action OnBeginningOfSpeech;
    event Action OnEndOfSpeech;
    event Action<string[]> OnPartialResults;
    event Action<string[], float[]> OnResults;
    event Action<int> OnError;
}
```

## NLU Pipeline

### Intent Classification

Process transcribed speech to extract user intent.

```typescript
// Intent definitions
interface Intent {
  name: string;
  confidence: number;
  entities: Entity[];
  rawText: string;
}

interface Entity {
  type: string;           // "session", "command", "approval", etc.
  value: string;
  startIndex: number;
  endIndex: number;
  confidence: number;
}
```

### Supported Intents

| Intent | Description | Example Utterances |
|--------|-------------|-------------------|
| `send_message` | Send message to Claude session | "tell container 1 to run tests", "ask Claude to fix the bug" |
| `get_status` | Query session status | "what's happening", "status report", "what are you working on" |
| `approve` | Approve pending action | "approve", "yes", "do it", "go ahead" |
| `deny` | Deny pending action | "deny", "no", "cancel that", "don't do it" |
| `switch_focus` | Change focused session | "focus on container 2", "switch to laptop" |
| `list_sessions` | List all sessions | "show sessions", "what sessions are active" |
| `interrupt` | Stop current operation | "stop", "cancel", "abort" |
| `repeat` | Repeat last response | "say that again", "repeat", "what did you say" |
| `help` | Get help | "help", "what can you do", "commands" |
| `system` | System commands | "volume up", "mute", "settings" |

### Entity Types

| Entity | Description | Examples |
|--------|-------------|----------|
| `session_name` | Claude session identifier | "container 1", "laptop", "the second one" |
| `command` | Action to perform | "run tests", "fix the bug", "deploy" |
| `file_path` | File reference | "the main file", "index.js" |
| `approval_id` | Specific approval | "that one", "the git push" |

### Intent Classification Implementation

#### Option A: Local Rule-Based (Low Latency)

```csharp
public class LocalIntentClassifier
{
    private Dictionary<string, IntentPattern[]> patterns;

    public Intent Classify(string text)
    {
        // Normalize text
        text = text.ToLower().Trim();

        // Check patterns in priority order
        foreach (var (intentName, intentPatterns) in patterns)
        {
            foreach (var pattern in intentPatterns)
            {
                if (pattern.Matches(text))
                {
                    return new Intent
                    {
                        Name = intentName,
                        Confidence = pattern.Confidence,
                        Entities = pattern.ExtractEntities(text),
                        RawText = text
                    };
                }
            }
        }

        // Default: treat as message to focused session
        return new Intent
        {
            Name = "send_message",
            Confidence = 0.5f,
            Entities = new[] { new Entity { Type = "command", Value = text } },
            RawText = text
        };
    }
}

// Pattern definitions
var patterns = new Dictionary<string, IntentPattern[]>
{
    ["approve"] = new[]
    {
        new IntentPattern(@"^(yes|approve|do it|go ahead|confirmed?)$", 0.95f),
        new IntentPattern(@"^(ok|okay|sure|yep|yeah)$", 0.85f),
    },
    ["deny"] = new[]
    {
        new IntentPattern(@"^(no|deny|cancel|don'?t|stop|abort)$", 0.95f),
        new IntentPattern(@"^(nope|nah|negative)$", 0.85f),
    },
    ["get_status"] = new[]
    {
        new IntentPattern(@"(status|what'?s happening|update|progress)", 0.9f),
        new IntentPattern(@"(what are you|are you) (doing|working)", 0.85f),
    },
    // ... more patterns
};
```

#### Option B: Claude-Based (Higher Accuracy)

For complex commands, send to Claude for NLU:

```typescript
// n8n workflow or direct API call
const systemPrompt = `
You are parsing voice commands for a developer assistant.
Extract the intent and entities from the user's speech.

Respond with JSON only:
{
  "intent": "intent_name",
  "confidence": 0.0-1.0,
  "entities": [
    {"type": "entity_type", "value": "extracted_value"}
  ]
}

Intents: send_message, get_status, approve, deny, switch_focus,
         list_sessions, interrupt, repeat, help, system, unknown

Entity types: session_name, command, file_path, approval_id
`;

// Example
// Input: "ask container 2 to run the integration tests"
// Output: {
//   "intent": "send_message",
//   "confidence": 0.95,
//   "entities": [
//     {"type": "session_name", "value": "container 2"},
//     {"type": "command", "value": "run the integration tests"}
//   ]
// }
```

### Command Builder

Translate intents into hub commands.

```csharp
public class CommandBuilder
{
    public HubCommand Build(Intent intent, SessionContext context)
    {
        return intent.Name switch
        {
            "send_message" => BuildSendMessage(intent, context),
            "approve" => BuildApproval(intent, context, approved: true),
            "deny" => BuildApproval(intent, context, approved: false),
            "switch_focus" => BuildFocusSwitch(intent, context),
            "get_status" => BuildStatusRequest(intent, context),
            "interrupt" => BuildInterrupt(intent, context),
            _ => null
        };
    }

    private HubCommand BuildSendMessage(Intent intent, SessionContext context)
    {
        var sessionEntity = intent.Entities.FirstOrDefault(e => e.Type == "session_name");
        var commandEntity = intent.Entities.FirstOrDefault(e => e.Type == "command");

        string targetSession = sessionEntity?.Value != null
            ? ResolveSessionName(sessionEntity.Value, context)
            : context.FocusedSessionId;

        return new HubCommand
        {
            Type = "send_message",
            SessionId = targetSession,
            Payload = new { content = commandEntity?.Value ?? intent.RawText }
        };
    }

    private string ResolveSessionName(string name, SessionContext context)
    {
        // Fuzzy match "container 2" -> "container-2"
        // "laptop" -> "laptop-main"
        // "the second one" -> context.Sessions[1].Id
        return context.Sessions
            .OrderByDescending(s => FuzzyMatch(s.Name, name))
            .FirstOrDefault()?.Id;
    }
}
```

## Audio Output Pipeline

### 1. Response Formatter

Prepare Claude responses for speech output.

```csharp
public class ResponseFormatter
{
    // Configuration
    public int MaxWordsForVoice = 50;
    public bool SummarizeLongResponses = true;
    public bool ReadCodeBlocks = false;
    public bool SpellOutAcronyms = true;

    public FormattedResponse Format(ClaudeResponse response)
    {
        var text = response.Content;

        // Remove markdown formatting
        text = StripMarkdown(text);

        // Handle code blocks
        if (response.HasCodeBlocks && !ReadCodeBlocks)
        {
            text = ReplaceCodeBlocks(text, "[code block on screen]");
        }

        // Summarize if too long
        if (CountWords(text) > MaxWordsForVoice && SummarizeLongResponses)
        {
            text = Summarize(text);
        }

        // Expand abbreviations
        text = ExpandAbbreviations(text);

        return new FormattedResponse
        {
            VoiceText = text,
            DisplayText = response.Content,
            Priority = DeterminePriority(response)
        };
    }

    private string Summarize(string text)
    {
        // Extract first sentence + key points
        // Or call Claude for summary
    }
}
```

### 2. Text-to-Speech

Using Android's TextToSpeech API.

```csharp
public class TextToSpeechManager
{
    // Configuration
    public string LanguageCode = "en-US";
    public float SpeechRate = 1.1f;               // Slightly faster
    public float Pitch = 1.0f;
    public float Volume = 0.9f;

    // State
    public bool IsSpeaking { get; private set; }

    // Events
    public event Action OnSpeechStart;
    public event Action OnSpeechComplete;
    public event Action<SpeechError> OnError;

    // Methods
    public void Speak(string text, Priority priority = Priority.Normal);
    public void Stop();
    public void Pause();
    public void Resume();

    // Queue management
    public void QueueSpeak(string text, Priority priority);
    public void ClearQueue();
}

public enum Priority
{
    Low,          // Can be interrupted, queued
    Normal,       // Standard priority
    High,         // Interrupts low/normal
    Urgent        // Interrupts everything, immediate
}
```

#### Android TTS Integration

```csharp
public interface IAndroidTTS
{
    void Initialize(string languageCode, Action<bool> onReady);
    void SetSpeechRate(float rate);
    void SetPitch(float pitch);
    int Speak(string text, int queueMode);  // QUEUE_FLUSH or QUEUE_ADD
    void Stop();
    bool IsSpeaking();
    void Shutdown();

    event Action<string> OnStart;
    event Action<string> OnDone;
    event Action<string> OnError;
}
```

### 3. Audio Output Routing

Manage where audio is played.

```csharp
public class AudioRouter
{
    public AudioOutput CurrentOutput { get; private set; }

    // Available outputs
    public enum AudioOutput
    {
        GlassesSpeaker,      // Built into XREAL One
        BeamProSpeaker,      // Beam Pro speaker
        BluetoothHeadset,    // Connected headset
        WiredHeadphones      // 3.5mm or USB-C
    }

    public void SetOutput(AudioOutput output);
    public AudioOutput[] GetAvailableOutputs();
    public void SetVolume(float volume);
}
```

## Conversation Flow

### State Machine

```
                    ┌───────────────────┐
                    │      Idle         │
                    │ (Wake word        │
                    │  detection ON)    │
                    └─────────┬─────────┘
                              │ Wake word detected
                              ▼
                    ┌───────────────────┐
         Timeout    │    Listening      │
        ┌──────────►│ (STT active)      │◄──────────┐
        │           └─────────┬─────────┘           │
        │                     │ Speech ended        │
        │                     ▼                     │
        │           ┌───────────────────┐           │
        │           │   Processing      │           │
        │           │ (NLU + Command)   │           │
        │           └─────────┬─────────┘           │
        │                     │ Command sent        │
        │                     ▼                     │
        │           ┌───────────────────┐           │
        │           │   Waiting         │           │
        │           │ (For Claude)      │           │
        │           └─────────┬─────────┘           │
        │                     │ Response received   │
        │                     ▼                     │
        │           ┌───────────────────┐           │
        │           │   Speaking        │           │
        │           │ (TTS active)      │───────────┘
        │           └─────────┬─────────┘  Follow-up
        │                     │ Speech complete     detected
        │                     ▼
        │           ┌───────────────────┐
        └───────────│   Cooldown        │
                    │ (Brief pause)     │
                    └─────────┬─────────┘
                              │ Timeout
                              ▼
                    ┌───────────────────┐
                    │      Idle         │
                    └───────────────────┘
```

### State Configuration

```csharp
public class ConversationFlowConfig
{
    // Listening
    public float ListeningTimeout = 10.0f;        // seconds
    public float SilenceThreshold = 1.5f;         // seconds to end utterance

    // Cooldown
    public float CooldownDuration = 0.5f;         // seconds after TTS

    // Follow-up
    public bool EnableFollowUp = true;
    public float FollowUpWindow = 5.0f;           // seconds to detect follow-up
    public string[] FollowUpIndicators = { "and", "also", "then", "what about" };

    // Interruption
    public bool AllowInterruption = true;         // Interrupt TTS with new command
    public string[] InterruptionWords = { "stop", "cancel", "wait", "jarvis" };
}
```

## Error Handling

### Speech Recognition Errors

| Error | User Feedback | Recovery |
|-------|---------------|----------|
| No match | "I didn't catch that" | Return to listening |
| Network error | "I'm having trouble connecting" | Retry or use offline |
| Audio error | "I can't hear you" | Check mic, return to idle |
| Timeout | "I didn't hear anything" | Return to idle |

### TTS Errors

| Error | Recovery |
|-------|----------|
| Initialization failed | Fall back to system TTS |
| Language unavailable | Use default language |
| Audio output error | Try alternative output |

### Error Responses

```csharp
public static class ErrorResponses
{
    public static string NoMatch = "I didn't quite catch that. Could you say that again?";
    public static string NetworkError = "I'm having trouble with the connection. Try again in a moment.";
    public static string SessionNotFound = "I couldn't find a session called {0}.";
    public static string NoActiveSession = "There's no active session to talk to.";
    public static string ApprovalExpired = "That approval request has expired.";
    public static string HubDisconnected = "I've lost connection to the session hub.";
    public static string LowConfidence = "I'm not sure I understood. Did you mean {0}?";
}
```

## Accessibility

### Voice Feedback Levels

```csharp
public enum VerbosityLevel
{
    Minimal,      // Only critical info
    Normal,       // Standard feedback
    Verbose,      // Detailed explanations
    Debug         // All events + debug info
}
```

### Audio Cues

| Event | Sound |
|-------|-------|
| Wake word detected | Rising tone (do-re) |
| Listening started | Soft chime |
| Listening ended | Descending tone |
| Command understood | Confirmation beep |
| Command not understood | Question tone |
| Error | Warning tone |
| Notification received | Attention ping |
| Approval required | Urgent ping |

## Privacy

### Data Handling

```csharp
public class PrivacyConfig
{
    // Audio data
    public bool StoreAudioLocally = false;
    public bool SendAudioToCloud = true;          // Required for cloud STT
    public int AudioRetentionSeconds = 0;         // Don't retain

    // Transcripts
    public bool StoreTranscripts = false;
    public bool SendTranscriptsToAnalytics = false;

    // Wake word
    public bool AlwaysListeningForWakeWord = true;
    public bool ProcessAudioBeforeWakeWord = false;
}
```

### Indicators

Visual and audio indicators when listening:
- Status bar shows microphone icon
- Optional LED indicator on glasses (if available)
- Subtle audio cue when listening starts/stops

## Performance Metrics

### Key Metrics

| Metric | Target |
|--------|--------|
| Wake word latency | < 300ms |
| STT latency (first word) | < 500ms |
| End-to-end latency (speech → action) | < 2s |
| Intent classification accuracy | > 95% |
| Word error rate (STT) | < 5% |

### Monitoring

```csharp
public class VoiceMetrics
{
    public void RecordWakeWordLatency(float ms);
    public void RecordSTTLatency(float ms);
    public void RecordIntentAccuracy(bool correct, string expected, string actual);
    public void RecordCommandSuccess(bool success, string command);

    public VoiceMetricsReport GetReport(TimeSpan period);
}
```

## Testing

### Unit Tests

- Wake word detection accuracy
- Intent classification patterns
- Entity extraction
- Response formatting

### Integration Tests

- End-to-end voice command flow (mock audio)
- Hub communication
- Error recovery

### User Testing

- Voice command recognition in various environments
- TTS intelligibility
- Conversation flow naturalness
- Latency perception

## Dependencies

### Android Libraries

- `android.speech.SpeechRecognizer` - Speech-to-text
- `android.speech.tts.TextToSpeech` - Text-to-speech
- Picovoice Porcupine (optional) - Wake word detection

### Unity Packages

- [UnityAndroidSpeechRecognizer](https://github.com/EricBatlle/UnityAndroidSpeechRecognizer)
- [Android Native TTS](https://github.com/HoseinPorazar/Android-Native-TTS-plugin-for-Unity-3d)

### Server-Side (Optional)

- n8n workflow for complex NLU
- Claude API for summarization
