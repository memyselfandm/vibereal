using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VibeReal.Voice
{
    public class ElevenLabsTTS : MonoBehaviour, ITTS
    {
        private string _apiKey;
        private string _voiceId;
        private string _modelId;
        private AudioSource _audioSource;
        private int _sampleRate = 16000;

        private Queue<SpeechItem> _speechQueue = new Queue<SpeechItem>();
        private string _currentUtteranceId;

        public event Action OnInitialized;
        public event Action<string> OnSpeechStart;
        public event Action<string> OnSpeechComplete;
        public event Action<string> OnError;

        public bool IsInitialized { get; private set; }
        public bool IsSpeaking { get; private set; }

        private struct SpeechItem
        {
            public string text;
            public SpeechPriority priority;
            public string utteranceId;
        }

        private void Start()
        {
            var config = Data.AppConfig.Load();
            _apiKey = config.elevenLabsApiKey;
            _voiceId = config.elevenLabsVoiceId;
            _modelId = config.elevenLabsModelId;
            Initialize();
        }

        public void Initialize()
        {
            if (IsInitialized) return;

            if (string.IsNullOrEmpty(_apiKey))
            {
                var config = Data.AppConfig.Load();
                _apiKey = config.elevenLabsApiKey;
                _voiceId = config.elevenLabsVoiceId;
                _modelId = config.elevenLabsModelId;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake = false;

            IsInitialized = true;
            Debug.Log("ElevenLabsTTS initialized");
            OnInitialized?.Invoke();
        }

        public void Speak(string text, SpeechPriority priority = SpeechPriority.Normal)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (!IsInitialized)
            {
                Debug.LogWarning("ElevenLabsTTS not initialized, queuing");
                QueueSpeak(text, priority);
                return;
            }

            if (priority >= SpeechPriority.High)
            {
                _speechQueue.Clear();
                if (_audioSource.isPlaying)
                {
                    _audioSource.Stop();
                }
            }

            var utteranceId = Guid.NewGuid().ToString();
            _currentUtteranceId = utteranceId;
            StartCoroutine(SynthesizeAndPlay(text, utteranceId));
        }

        public void QueueSpeak(string text, SpeechPriority priority = SpeechPriority.Normal)
        {
            _speechQueue.Enqueue(new SpeechItem
            {
                text = text,
                priority = priority,
                utteranceId = Guid.NewGuid().ToString()
            });

            if (!IsSpeaking && IsInitialized)
            {
                ProcessQueue();
            }
        }

        public void Stop()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
            StopAllCoroutines();
            _speechQueue.Clear();
            IsSpeaking = false;
        }

        public void ClearQueue()
        {
            _speechQueue.Clear();
        }

        private IEnumerator SynthesizeAndPlay(string text, string utteranceId)
        {
            IsSpeaking = true;
            OnSpeechStart?.Invoke(utteranceId);

            var url = $"https://api.elevenlabs.io/v1/text-to-speech/{_voiceId}?output_format=pcm_16000";

            var jsonBody = JsonUtility.ToJson(new TtsRequest
            {
                text = text,
                model_id = _modelId
            });

            var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", _apiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"ElevenLabsTTS error: {request.error} - {request.downloadHandler?.text}");
                IsSpeaking = false;
                OnError?.Invoke(request.error);
                request.Dispose();
                ProcessQueue();
                yield break;
            }

            byte[] pcmBytes = request.downloadHandler.data;
            request.Dispose();

            if (pcmBytes == null || pcmBytes.Length == 0)
            {
                Debug.LogError("ElevenLabsTTS: Empty audio response");
                IsSpeaking = false;
                OnError?.Invoke("Empty audio response");
                ProcessQueue();
                yield break;
            }

            // Convert 16-bit PCM bytes to float samples
            int sampleCount = pcmBytes.Length / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short value = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
                samples[i] = value / 32768f;
            }

            var clip = AudioClip.Create("ElevenLabsTTS", sampleCount, 1, _sampleRate, false);
            clip.SetData(samples, 0);

            _audioSource.clip = clip;
            _audioSource.Play();

            // Wait for playback to finish
            while (_audioSource.isPlaying)
            {
                yield return null;
            }

            IsSpeaking = false;
            OnSpeechComplete?.Invoke(utteranceId);

            // Clean up
            Destroy(clip);

            ProcessQueue();
        }

        private void ProcessQueue()
        {
            if (_speechQueue.Count > 0 && !IsSpeaking)
            {
                var item = _speechQueue.Dequeue();
                Speak(item.text, item.priority);
            }
        }

        [Serializable]
        private class TtsRequest
        {
            public string text;
            public string model_id;
        }
    }
}
