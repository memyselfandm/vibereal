using System;
using System.Collections.Generic;
using UnityEngine;

namespace VibeReal.Voice
{
    /// <summary>
    /// Android Text-to-Speech wrapper using native TextToSpeech API.
    /// Supports queuing, priority, and speech rate/pitch control.
    /// </summary>
    public class AndroidTTS : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string languageCode = "en-US";
        [SerializeField] private float speechRate = 1.1f;
        [SerializeField] private float pitch = 1.0f;

        // Events
        public event Action OnInitialized;
        public event Action<string> OnSpeechStart;
        public event Action<string> OnSpeechComplete;
        public event Action<string> OnError;

        // State
        public bool IsInitialized { get; private set; }
        public bool IsSpeaking { get; private set; }

        private Queue<SpeechItem> _speechQueue = new Queue<SpeechItem>();
        private string _currentUtteranceId;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _tts;
        private AndroidJavaObject _activity;
#endif

        public enum Priority
        {
            Low,
            Normal,
            High,
            Urgent
        }

        private struct SpeechItem
        {
            public string text;
            public Priority priority;
            public string utteranceId;
        }

        private void Start()
        {
            var config = Data.AppConfig.Load();
            languageCode = config.languageCode;
            speechRate = config.speechRate;
            pitch = config.pitch;

            Initialize();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        /// <summary>
        /// Initialize the TTS engine
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Get Unity activity
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    _activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }

                // Create TTS with init listener
                var initListener = new TTSInitListener(this);
                _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", _activity, initListener);

                Debug.Log("AndroidTTS initialization started");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize TTS: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
#else
            // Editor mock
            IsInitialized = true;
            Debug.Log("AndroidTTS initialized (mock mode)");
            OnInitialized?.Invoke();
#endif
        }

        internal void HandleInitComplete(bool success)
        {
            if (success)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                // Set language
                try
                {
                    using (var locale = new AndroidJavaObject("java.util.Locale", languageCode.Split('-')[0],
                        languageCode.Contains("-") ? languageCode.Split('-')[1] : ""))
                    {
                        _tts.Call<int>("setLanguage", locale);
                    }

                    // Set speech rate and pitch
                    _tts.Call<int>("setSpeechRate", speechRate);
                    _tts.Call<int>("setPitch", pitch);

                    // Set utterance progress listener
                    var progressListener = new TTSProgressListener(this);
                    _tts.Call<int>("setOnUtteranceProgressListener", progressListener);

                    IsInitialized = true;
                    Debug.Log("AndroidTTS initialized successfully");
                    MainThreadDispatcher.Enqueue(() => OnInitialized?.Invoke());
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to configure TTS: {ex.Message}");
                    MainThreadDispatcher.Enqueue(() => OnError?.Invoke(ex.Message));
                }
#endif
            }
            else
            {
                Debug.LogError("TTS initialization failed");
                MainThreadDispatcher.Enqueue(() => OnError?.Invoke("TTS initialization failed"));
            }
        }

        /// <summary>
        /// Speak text immediately (flushes queue for High/Urgent priority)
        /// </summary>
        public void Speak(string text, Priority priority = Priority.Normal)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (!IsInitialized)
            {
                Debug.LogWarning("TTS not initialized, queuing speech");
                QueueSpeak(text, priority);
                return;
            }

            var utteranceId = Guid.NewGuid().ToString();

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                int queueMode = (priority >= Priority.High) ? 0 : 1; // QUEUE_FLUSH = 0, QUEUE_ADD = 1

                using (var bundle = new AndroidJavaObject("android.os.Bundle"))
                {
                    _tts.Call<int>("speak", text, queueMode, bundle, utteranceId);
                }

                if (priority >= Priority.High)
                {
                    _speechQueue.Clear();
                }

                _currentUtteranceId = utteranceId;
                IsSpeaking = true;
                Debug.Log($"Speaking: {text.Substring(0, Math.Min(50, text.Length))}...");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to speak: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
#else
            // Editor mock
            IsSpeaking = true;
            _currentUtteranceId = utteranceId;
            Debug.Log($"Speaking (mock): {text}");
            OnSpeechStart?.Invoke(utteranceId);

            // Simulate speech duration
            StartCoroutine(MockSpeechCoroutine(text, utteranceId));
#endif
        }

#if UNITY_EDITOR || !UNITY_ANDROID
        private System.Collections.IEnumerator MockSpeechCoroutine(string text, string utteranceId)
        {
            // Estimate speech duration (roughly 150 words per minute)
            float duration = (text.Split(' ').Length / 150f) * 60f;
            duration = Mathf.Max(0.5f, duration);

            yield return new WaitForSeconds(duration);

            IsSpeaking = false;
            OnSpeechComplete?.Invoke(utteranceId);
            ProcessQueue();
        }
#endif

        /// <summary>
        /// Queue text to speak after current speech
        /// </summary>
        public void QueueSpeak(string text, Priority priority = Priority.Normal)
        {
            _speechQueue.Enqueue(new SpeechItem
            {
                text = text,
                priority = priority,
                utteranceId = Guid.NewGuid().ToString()
            });

            // If not speaking, start immediately
            if (!IsSpeaking && IsInitialized)
            {
                ProcessQueue();
            }
        }

        /// <summary>
        /// Stop current speech and clear queue
        /// </summary>
        public void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _tts?.Call<int>("stop");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to stop TTS: {ex.Message}");
            }
#endif
            _speechQueue.Clear();
            IsSpeaking = false;
        }

        /// <summary>
        /// Clear the speech queue (doesn't stop current speech)
        /// </summary>
        public void ClearQueue()
        {
            _speechQueue.Clear();
        }

        /// <summary>
        /// Set speech rate (0.5 to 2.0, default 1.0)
        /// </summary>
        public void SetSpeechRate(float rate)
        {
            speechRate = Mathf.Clamp(rate, 0.5f, 2.0f);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsInitialized)
            {
                _tts?.Call<int>("setSpeechRate", speechRate);
            }
#endif
        }

        /// <summary>
        /// Set pitch (0.5 to 2.0, default 1.0)
        /// </summary>
        public void SetPitch(float newPitch)
        {
            pitch = Mathf.Clamp(newPitch, 0.5f, 2.0f);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsInitialized)
            {
                _tts?.Call<int>("setPitch", pitch);
            }
#endif
        }

        private void ProcessQueue()
        {
            if (_speechQueue.Count > 0 && !IsSpeaking)
            {
                var item = _speechQueue.Dequeue();
                Speak(item.text, item.priority);
            }
        }

        internal void HandleSpeechStart(string utteranceId)
        {
            IsSpeaking = true;
            MainThreadDispatcher.Enqueue(() => OnSpeechStart?.Invoke(utteranceId));
        }

        internal void HandleSpeechComplete(string utteranceId)
        {
            IsSpeaking = false;
            MainThreadDispatcher.Enqueue(() =>
            {
                OnSpeechComplete?.Invoke(utteranceId);
                ProcessQueue();
            });
        }

        internal void HandleSpeechError(string utteranceId, string error)
        {
            IsSpeaking = false;
            MainThreadDispatcher.Enqueue(() =>
            {
                OnError?.Invoke(error);
                ProcessQueue();
            });
        }

        private void Cleanup()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_tts != null)
            {
                try
                {
                    _tts.Call("stop");
                    _tts.Call("shutdown");
                    _tts.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to cleanup TTS: {ex.Message}");
                }
            }
#endif
            IsInitialized = false;
            IsSpeaking = false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Native callback for TTS initialization
        /// </summary>
        private class TTSInitListener : AndroidJavaProxy
        {
            private readonly AndroidTTS _tts;

            public TTSInitListener(AndroidTTS tts)
                : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                _tts = tts;
            }

            public void onInit(int status)
            {
                _tts.HandleInitComplete(status == 0); // SUCCESS = 0
            }
        }

        /// <summary>
        /// Native callback for TTS utterance progress
        /// </summary>
        private class TTSProgressListener : AndroidJavaProxy
        {
            private readonly AndroidTTS _tts;

            public TTSProgressListener(AndroidTTS tts)
                : base("android.speech.tts.UtteranceProgressListener")
            {
                _tts = tts;
            }

            public void onStart(string utteranceId)
            {
                _tts.HandleSpeechStart(utteranceId);
            }

            public void onDone(string utteranceId)
            {
                _tts.HandleSpeechComplete(utteranceId);
            }

            public void onError(string utteranceId)
            {
                _tts.HandleSpeechError(utteranceId, "TTS error");
            }

            // Required for API 21+
            public void onError(string utteranceId, int errorCode)
            {
                _tts.HandleSpeechError(utteranceId, $"TTS error: {errorCode}");
            }

            public void onStop(string utteranceId, bool interrupted)
            {
                _tts.HandleSpeechComplete(utteranceId);
            }
        }
#endif
    }
}
