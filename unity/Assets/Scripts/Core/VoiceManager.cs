using System;
using System.Text.RegularExpressions;
using UnityEngine;
using VibeReal.Data;
using VibeReal.Voice;

namespace VibeReal.Core
{
    /// <summary>
    /// Coordinates voice input (STT) and output (TTS) for the application.
    /// Implements push-to-talk state machine and response formatting.
    /// </summary>
    public class VoiceManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WebSocketClient webSocketClient;

        private ISTT stt;
        private ITTS tts;

        [Header("Settings")]
        [SerializeField] private float listeningTimeout = 10f;
        [SerializeField] private int maxTtsWords = 50;
        [SerializeField] private bool summarizeLongResponses = true;

        // Events
        public event Action OnListeningStarted;
        public event Action OnListeningStopped;
        public event Action<string> OnTranscriptReceived;
        public event Action<string> OnSpeakingStarted;
        public event Action OnSpeakingStopped;
        public event Action<VoiceState> OnStateChanged;
        public event Action<string> OnError;

        // State
        public VoiceState State { get; private set; } = VoiceState.Idle;
        public bool IsListening => State == VoiceState.Listening;
        public bool IsSpeaking => State == VoiceState.Speaking;

        private float _listeningStartTime;
        private string _lastTranscript;

        public enum VoiceState
        {
            Idle,
            Listening,
            Processing,
            WaitingForResponse,
            Speaking
        }

        private void Start()
        {
            var config = AppConfig.Load();
            listeningTimeout = config.listeningTimeoutSeconds;
            maxTtsWords = config.maxTtsWords;
            summarizeLongResponses = config.summarizeLongResponses;

            // Discover voice components — prefer ElevenLabs, fall back to Android native
            bool useElevenLabs = !string.IsNullOrEmpty(config.elevenLabsApiKey);

            if (stt == null) stt = GetComponent<ISTT>();
            if (stt == null)
            {
                if (useElevenLabs)
                    stt = gameObject.AddComponent<ElevenLabsSTT>();
                else
                    stt = gameObject.AddComponent<AndroidSTT>();
            }

            if (tts == null) tts = GetComponent<ITTS>();
            if (tts == null)
            {
                if (useElevenLabs)
                    tts = gameObject.AddComponent<ElevenLabsTTS>();
                else
                    tts = gameObject.AddComponent<AndroidTTS>();
            }

            if (webSocketClient == null) webSocketClient = FindObjectOfType<WebSocketClient>();

            // Subscribe to STT events
            stt.OnReadyForSpeech += HandleReadyForSpeech;
            stt.OnResult += HandleSpeechResult;
            stt.OnPartialResult += HandlePartialResult;
            stt.OnEndOfSpeech += HandleEndOfSpeech;
            stt.OnError += HandleSttError;

            // Subscribe to TTS events
            tts.OnSpeechStart += HandleTtsStart;
            tts.OnSpeechComplete += HandleTtsComplete;
            tts.OnError += HandleTtsError;

            // Subscribe to WebSocket events
            if (webSocketClient != null)
            {
                webSocketClient.OnClaudeResponse += HandleClaudeResponse;
                webSocketClient.OnCommandAck += HandleCommandAck;
            }
        }

        private void Update()
        {
            // Check for listening timeout
            if (State == VoiceState.Listening)
            {
                if (Time.time - _listeningStartTime > listeningTimeout)
                {
                    Debug.Log("Listening timeout");
                    StopListening();
                    Speak("I didn't hear anything. Try again.");
                }
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (stt != null)
            {
                stt.OnReadyForSpeech -= HandleReadyForSpeech;
                stt.OnResult -= HandleSpeechResult;
                stt.OnPartialResult -= HandlePartialResult;
                stt.OnEndOfSpeech -= HandleEndOfSpeech;
                stt.OnError -= HandleSttError;
            }

            if (tts != null)
            {
                tts.OnSpeechStart -= HandleTtsStart;
                tts.OnSpeechComplete -= HandleTtsComplete;
                tts.OnError -= HandleTtsError;
            }

            if (webSocketClient != null)
            {
                webSocketClient.OnClaudeResponse -= HandleClaudeResponse;
                webSocketClient.OnCommandAck -= HandleCommandAck;
            }
        }

        /// <summary>
        /// Start listening for voice input (push-to-talk)
        /// </summary>
        public void StartListening()
        {
            if (State == VoiceState.Listening)
            {
                Debug.LogWarning("Already listening");
                return;
            }

            // Stop any current TTS
            if (State == VoiceState.Speaking)
            {
                tts.Stop();
            }

            SetState(VoiceState.Listening);
            _listeningStartTime = Time.time;
            stt.StartListening();
            OnListeningStarted?.Invoke();
        }

        /// <summary>
        /// Stop listening and process the result
        /// </summary>
        public void StopListening()
        {
            if (State != VoiceState.Listening)
            {
                return;
            }

            stt.StopListening();
            OnListeningStopped?.Invoke();
        }

        /// <summary>
        /// Speak text using TTS
        /// </summary>
        public void Speak(string text, SpeechPriority priority = SpeechPriority.Normal)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Format for speech
            var formatted = FormatForSpeech(text);

            tts.Speak(formatted, priority);
        }

        /// <summary>
        /// Speak a notification
        /// </summary>
        public void SpeakNotification(NotificationData notification)
        {
            var priority = notification.priority switch
            {
                "critical" => SpeechPriority.Urgent,
                "high" => SpeechPriority.High,
                _ => SpeechPriority.Normal
            };

            var text = !string.IsNullOrEmpty(notification.voiceText)
                ? notification.voiceText
                : notification.title;

            Speak(text, priority);
        }

        /// <summary>
        /// Stop all voice activity
        /// </summary>
        public void StopAll()
        {
            stt.Cancel();
            tts.Stop();
            SetState(VoiceState.Idle);
        }

        // ==================== Event Handlers ====================

        private void HandleReadyForSpeech()
        {
            Debug.Log("Ready for speech");
        }

        private void HandlePartialResult(string text)
        {
            _lastTranscript = text;
            OnTranscriptReceived?.Invoke(text);
        }

        private void HandleSpeechResult(string text, float confidence)
        {
            Debug.Log($"Speech result: {text} (confidence: {confidence})");
            _lastTranscript = text;
            OnTranscriptReceived?.Invoke(text);

            SetState(VoiceState.Processing);

            // Send to hub
            if (webSocketClient != null && webSocketClient.IsConnected)
            {
                webSocketClient.SendVoiceCommand(text, confidence);
                SetState(VoiceState.WaitingForResponse);
            }
            else
            {
                Speak("I'm not connected to the server.");
                SetState(VoiceState.Idle);
            }
        }

        private void HandleEndOfSpeech()
        {
            Debug.Log("End of speech");
            OnListeningStopped?.Invoke();
        }

        private void HandleSttError(SpeechError error)
        {
            Debug.LogError($"STT error: {error}");

            string errorMessage = error switch
            {
                SpeechError.NoMatch => "I didn't catch that. Try again.",
                SpeechError.Network or SpeechError.NetworkTimeout =>
                    "Network error. Check your connection.",
                SpeechError.Audio => "Audio error. Check microphone.",
                SpeechError.SpeechTimeout => "I didn't hear anything.",
                _ => "Voice recognition error."
            };

            OnError?.Invoke(errorMessage);
            SetState(VoiceState.Idle);

            // Speak error if not a timeout
            if (error != SpeechError.SpeechTimeout &&
                error != SpeechError.NoMatch)
            {
                Speak(errorMessage);
            }
        }

        private void HandleTtsStart(string utteranceId)
        {
            SetState(VoiceState.Speaking);
            OnSpeakingStarted?.Invoke(utteranceId);
        }

        private void HandleTtsComplete(string utteranceId)
        {
            if (State == VoiceState.Speaking)
            {
                SetState(VoiceState.Idle);
            }
            OnSpeakingStopped?.Invoke();
        }

        private void HandleTtsError(string error)
        {
            Debug.LogError($"TTS error: {error}");
            OnError?.Invoke(error);
            SetState(VoiceState.Idle);
        }

        private void HandleClaudeResponse(ClaudeResponseMessage response)
        {
            if (response.isComplete)
            {
                // Speak the response
                Speak(response.content);
            }
        }

        private void HandleCommandAck(CommandAckMessage ack)
        {
            Debug.Log($"Command acknowledged: {ack.intent} -> {ack.targetSession}");
            // Could provide auditory feedback here
        }

        // ==================== Helpers ====================

        private void SetState(VoiceState newState)
        {
            if (State != newState)
            {
                Debug.Log($"Voice state: {State} -> {newState}");
                State = newState;
                OnStateChanged?.Invoke(newState);
            }
        }

        /// <summary>
        /// Format text for TTS output (strip markdown, truncate, etc.)
        /// </summary>
        private string FormatForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Remove markdown code blocks
            text = Regex.Replace(text, @"```[\s\S]*?```", "[code block]");
            text = Regex.Replace(text, @"`[^`]+`", "");

            // Remove markdown formatting
            text = Regex.Replace(text, @"\*\*([^*]+)\*\*", "$1"); // Bold
            text = Regex.Replace(text, @"\*([^*]+)\*", "$1");     // Italic
            text = Regex.Replace(text, @"__([^_]+)__", "$1");     // Bold
            text = Regex.Replace(text, @"_([^_]+)_", "$1");       // Italic
            text = Regex.Replace(text, @"#+\s*", "");             // Headers
            text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1"); // Links

            // Remove bullet points
            text = Regex.Replace(text, @"^\s*[-*]\s+", "", RegexOptions.Multiline);

            // Collapse whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();

            // Truncate if too long
            if (summarizeLongResponses)
            {
                var words = text.Split(' ');
                if (words.Length > maxTtsWords)
                {
                    text = string.Join(" ", words, 0, maxTtsWords) + ". See the display for more.";
                }
            }

            return text;
        }
    }
}
