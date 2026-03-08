using System;
using UnityEngine;

namespace VibeReal.Voice
{
    /// <summary>
    /// Android Speech-to-Text wrapper using native SpeechRecognizer API.
    /// Provides push-to-talk voice recognition.
    /// </summary>
    public class AndroidSTT : MonoBehaviour, ISTT
    {
        [Header("Settings")]
        [SerializeField] private string languageCode = "en-US";
        [SerializeField] private int maxAlternatives = 3;

        // Events
        public event Action OnReadyForSpeech;
        public event Action OnBeginningOfSpeech;
        public event Action OnEndOfSpeech;
        public event Action<string> OnPartialResult;
        public event Action<string, float> OnResult;
        public event Action<SpeechError> OnError;

        // State
        public bool IsListening { get; private set; }
        public bool IsInitialized { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _speechRecognizer;
        private AndroidJavaObject _recognizerIntent;
        private AndroidJavaObject _activity;
#endif

        private void Start()
        {
            var config = Data.AppConfig.Load();
            languageCode = config.languageCode;

            Initialize();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        /// <summary>
        /// Initialize the speech recognizer
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    _activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }

                // SpeechRecognizer must be created on the Android UI thread
                _activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        using (var speechRecognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer"))
                        {
                            bool isAvailable = speechRecognizerClass.CallStatic<bool>("isRecognitionAvailable", _activity);
                            if (!isAvailable)
                            {
                                Debug.LogError("Speech recognition not available on this device");
                                MainThreadDispatcher.Enqueue(() => OnError?.Invoke(SpeechError.Client));
                                return;
                            }

                            _speechRecognizer = speechRecognizerClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", _activity);
                        }

                        var listener = new SpeechRecognitionListener(this);
                        _speechRecognizer.Call("setRecognitionListener", listener);

                        using (var recognizerIntentClass = new AndroidJavaClass("android.speech.RecognizerIntent"))
                        {
                            string actionRecognizeSpeech = recognizerIntentClass.GetStatic<string>("ACTION_RECOGNIZE_SPEECH");
                            _recognizerIntent = new AndroidJavaObject("android.content.Intent", actionRecognizeSpeech);

                            string extraLanguageModel = recognizerIntentClass.GetStatic<string>("EXTRA_LANGUAGE_MODEL");
                            string languageModelFreeForm = recognizerIntentClass.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM");
                            _recognizerIntent.Call<AndroidJavaObject>("putExtra", extraLanguageModel, languageModelFreeForm);

                            string extraLanguage = recognizerIntentClass.GetStatic<string>("EXTRA_LANGUAGE");
                            _recognizerIntent.Call<AndroidJavaObject>("putExtra", extraLanguage, languageCode);

                            string extraMaxResults = recognizerIntentClass.GetStatic<string>("EXTRA_MAX_RESULTS");
                            _recognizerIntent.Call<AndroidJavaObject>("putExtra", extraMaxResults, maxAlternatives);

                            string extraPartialResults = recognizerIntentClass.GetStatic<string>("EXTRA_PARTIAL_RESULTS");
                            _recognizerIntent.Call<AndroidJavaObject>("putExtra", extraPartialResults, true);
                        }

                        IsInitialized = true;
                        Debug.Log("AndroidSTT initialized");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to initialize speech recognizer on UI thread: {ex.Message}");
                        MainThreadDispatcher.Enqueue(() => OnError?.Invoke(SpeechError.Client));
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize speech recognizer: {ex.Message}");
                OnError?.Invoke(SpeechError.Client);
            }
#else
            // Editor/non-Android fallback
            IsInitialized = true;
            Debug.Log("AndroidSTT initialized (mock mode)");
#endif
        }

        /// <summary>
        /// Start listening for speech
        /// </summary>
        public void StartListening()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("Speech recognizer not initialized");
                OnError?.Invoke(SpeechError.Client);
                return;
            }

            if (IsListening)
            {
                Debug.LogWarning("Already listening");
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    _speechRecognizer.Call("startListening", _recognizerIntent);
                }));
                IsListening = true;
                Debug.Log("Started listening");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to start listening: {ex.Message}");
                OnError?.Invoke(SpeechError.Client);
            }
#else
            // Editor mock
            IsListening = true;
            Debug.Log("Started listening (mock mode)");
            OnReadyForSpeech?.Invoke();
#endif
        }

        /// <summary>
        /// Stop listening for speech
        /// </summary>
        public void StopListening()
        {
            if (!IsListening) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    _speechRecognizer.Call("stopListening");
                }));
                IsListening = false;
                Debug.Log("Stopped listening");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to stop listening: {ex.Message}");
            }
#else
            IsListening = false;
            Debug.Log("Stopped listening (mock mode)");
#endif
        }

        /// <summary>
        /// Cancel current recognition
        /// </summary>
        public void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    _speechRecognizer.Call("cancel");
                }));
                IsListening = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to cancel: {ex.Message}");
            }
#else
            IsListening = false;
#endif
        }

        private void Cleanup()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_speechRecognizer != null)
            {
                try
                {
                    _speechRecognizer.Call("destroy");
                    _speechRecognizer.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to cleanup: {ex.Message}");
                }
            }
#endif
            IsInitialized = false;
            IsListening = false;
        }

        // Called from native listener
        internal void HandleReadyForSpeech()
        {
            MainThreadDispatcher.Enqueue(() => OnReadyForSpeech?.Invoke());
        }

        internal void HandleBeginningOfSpeech()
        {
            MainThreadDispatcher.Enqueue(() => OnBeginningOfSpeech?.Invoke());
        }

        internal void HandleEndOfSpeech()
        {
            IsListening = false;
            MainThreadDispatcher.Enqueue(() => OnEndOfSpeech?.Invoke());
        }

        internal void HandlePartialResults(string text)
        {
            MainThreadDispatcher.Enqueue(() => OnPartialResult?.Invoke(text));
        }

        internal void HandleResults(string text, float confidence)
        {
            IsListening = false;
            MainThreadDispatcher.Enqueue(() => OnResult?.Invoke(text, confidence));
        }

        internal void HandleError(int errorCode)
        {
            IsListening = false;
            MainThreadDispatcher.Enqueue(() => OnError?.Invoke((SpeechError)errorCode));
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Native callback listener for Android SpeechRecognizer
        /// </summary>
        private class SpeechRecognitionListener : AndroidJavaProxy
        {
            private readonly AndroidSTT _stt;

            public SpeechRecognitionListener(AndroidSTT stt)
                : base("android.speech.RecognitionListener")
            {
                _stt = stt;
            }

            public void onReadyForSpeech(AndroidJavaObject @params)
            {
                _stt.HandleReadyForSpeech();
            }

            public void onBeginningOfSpeech()
            {
                _stt.HandleBeginningOfSpeech();
            }

            public void onRmsChanged(float rmsdB) { }

            public void onBufferReceived(byte[] buffer) { }

            public void onEndOfSpeech()
            {
                _stt.HandleEndOfSpeech();
            }

            public void onError(int error)
            {
                _stt.HandleError(error);
            }

            public void onResults(AndroidJavaObject results)
            {
                try
                {
                    using (var list = results.Call<AndroidJavaObject>("getStringArrayList", "results_recognition"))
                    {
                        if (list != null)
                        {
                            int size = list.Call<int>("size");
                            if (size > 0)
                            {
                                string text = list.Call<string>("get", 0);

                                // Try to get confidence
                                float confidence = 1.0f;
                                using (var confidenceScores = results.Call<AndroidJavaObject>("getFloatArray", "confidence_scores"))
                                {
                                    if (confidenceScores != null)
                                    {
                                        float[] scores = AndroidJNIHelper.ConvertFromJNIArray<float[]>(confidenceScores.GetRawObject());
                                        if (scores != null && scores.Length > 0)
                                        {
                                            confidence = scores[0];
                                        }
                                    }
                                }

                                _stt.HandleResults(text, confidence);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing results: {ex.Message}");
                }
            }

            public void onPartialResults(AndroidJavaObject partialResults)
            {
                try
                {
                    using (var list = partialResults.Call<AndroidJavaObject>("getStringArrayList", "results_recognition"))
                    {
                        if (list != null)
                        {
                            int size = list.Call<int>("size");
                            if (size > 0)
                            {
                                string text = list.Call<string>("get", 0);
                                _stt.HandlePartialResults(text);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing partial results: {ex.Message}");
                }
            }

            public void onEvent(int eventType, AndroidJavaObject @params) { }
        }
#endif
    }

    /// <summary>
    /// Helper to dispatch callbacks to Unity main thread
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly System.Collections.Generic.Queue<Action> _actions =
            new System.Collections.Generic.Queue<Action>();
        private static bool _initialized;

        public static void Enqueue(Action action)
        {
            lock (_actions)
            {
                _actions.Enqueue(action);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var go = new GameObject("MainThreadDispatcher");
            go.AddComponent<MainThreadDispatcherBehaviour>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private class MainThreadDispatcherBehaviour : MonoBehaviour
        {
            private void Update()
            {
                lock (_actions)
                {
                    while (_actions.Count > 0)
                    {
                        var action = _actions.Dequeue();
                        action?.Invoke();
                    }
                }
            }
        }
    }
}
