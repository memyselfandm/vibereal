using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VibeReal.Voice
{
    public class ElevenLabsSTT : MonoBehaviour, ISTT
    {
        private string _apiKey;
        private string _deviceName;
        private AudioClip _recordingClip;
        private bool _isRecording;
        private int _sampleRate = 16000;

        public event Action OnReadyForSpeech;
        public event Action OnBeginningOfSpeech;
        public event Action OnEndOfSpeech;
        public event Action<string> OnPartialResult;
        public event Action<string, float> OnResult;
        public event Action<SpeechError> OnError;

        public bool IsListening { get; private set; }
        public bool IsInitialized { get; private set; }

        private void Start()
        {
            var config = Data.AppConfig.Load();
            _apiKey = config.elevenLabsApiKey;
            Initialize();
        }

        public void Initialize()
        {
            if (IsInitialized) return;

            if (string.IsNullOrEmpty(_apiKey))
            {
                var config = Data.AppConfig.Load();
                _apiKey = config.elevenLabsApiKey;
            }

            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("ElevenLabsSTT: No microphone found");
                OnError?.Invoke(SpeechError.Audio);
                return;
            }

            _deviceName = Microphone.devices[0];
            IsInitialized = true;
            Debug.Log($"ElevenLabsSTT initialized with mic: {_deviceName}");
        }

        public void StartListening()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("ElevenLabsSTT not initialized");
                OnError?.Invoke(SpeechError.Client);
                return;
            }

            if (IsListening) return;

            _recordingClip = Microphone.Start(_deviceName, false, 30, _sampleRate);
            _isRecording = true;
            IsListening = true;

            OnReadyForSpeech?.Invoke();
            OnBeginningOfSpeech?.Invoke();
            Debug.Log("ElevenLabsSTT: Recording started");
        }

        public void StopListening()
        {
            if (!IsListening) return;

            int lastSample = Microphone.GetPosition(_deviceName);
            Microphone.End(_deviceName);
            _isRecording = false;
            IsListening = false;

            OnEndOfSpeech?.Invoke();

            if (lastSample <= 0)
            {
                Debug.LogWarning("ElevenLabsSTT: No audio recorded");
                OnError?.Invoke(SpeechError.NoMatch);
                return;
            }

            float[] samples = new float[lastSample * _recordingClip.channels];
            _recordingClip.GetData(samples, 0);

            byte[] wavBytes = EncodeToWav(samples, _recordingClip.channels, _sampleRate);
            StartCoroutine(TranscribeAudio(wavBytes));
        }

        public void Cancel()
        {
            if (_isRecording)
            {
                Microphone.End(_deviceName);
                _isRecording = false;
            }
            IsListening = false;
        }

        private IEnumerator TranscribeAudio(byte[] wavBytes)
        {
            var url = "https://api.elevenlabs.io/v1/speech-to-text";

            // Build multipart form data
            var boundary = "----UnityBoundary" + DateTime.Now.Ticks.ToString("x");
            var bodyBuilder = new System.Collections.Generic.List<byte>();

            // Add model_id field
            var fieldHeader = $"--{boundary}\r\nContent-Disposition: form-data; name=\"model_id\"\r\n\r\nscribe_v1\r\n";
            bodyBuilder.AddRange(Encoding.UTF8.GetBytes(fieldHeader));

            // Add file field
            var fileHeader = $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"audio.wav\"\r\nContent-Type: audio/wav\r\n\r\n";
            bodyBuilder.AddRange(Encoding.UTF8.GetBytes(fileHeader));
            bodyBuilder.AddRange(wavBytes);
            bodyBuilder.AddRange(Encoding.UTF8.GetBytes("\r\n"));

            // End boundary
            bodyBuilder.AddRange(Encoding.UTF8.GetBytes($"--{boundary}--\r\n"));

            byte[] bodyBytes = bodyBuilder.ToArray();

            var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", $"multipart/form-data; boundary={boundary}");
            request.SetRequestHeader("xi-api-key", _apiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"ElevenLabsSTT error: {request.error} - {request.downloadHandler?.text}");
                OnError?.Invoke(SpeechError.Network);
                yield break;
            }

            var responseText = request.downloadHandler.text;
            Debug.Log($"ElevenLabsSTT response: {responseText}");

            // Parse JSON response { "text": "..." }
            var response = JsonUtility.FromJson<SttResponse>(responseText);
            if (!string.IsNullOrEmpty(response.text))
            {
                OnResult?.Invoke(response.text, 1.0f);
            }
            else
            {
                OnError?.Invoke(SpeechError.NoMatch);
            }

            request.Dispose();
        }

        private static byte[] EncodeToWav(float[] samples, int channels, int sampleRate)
        {
            int sampleCount = samples.Length;
            int byteRate = sampleRate * channels * 2; // 16-bit = 2 bytes per sample
            int dataSize = sampleCount * 2;
            int fileSize = 44 + dataSize;

            byte[] wav = new byte[fileSize];
            int pos = 0;

            // RIFF header
            WriteString(wav, ref pos, "RIFF");
            WriteInt32(wav, ref pos, fileSize - 8);
            WriteString(wav, ref pos, "WAVE");

            // fmt chunk
            WriteString(wav, ref pos, "fmt ");
            WriteInt32(wav, ref pos, 16); // chunk size
            WriteInt16(wav, ref pos, 1);  // PCM format
            WriteInt16(wav, ref pos, (short)channels);
            WriteInt32(wav, ref pos, sampleRate);
            WriteInt32(wav, ref pos, byteRate);
            WriteInt16(wav, ref pos, (short)(channels * 2)); // block align
            WriteInt16(wav, ref pos, 16); // bits per sample

            // data chunk
            WriteString(wav, ref pos, "data");
            WriteInt32(wav, ref pos, dataSize);

            // Convert float samples to 16-bit PCM
            for (int i = 0; i < sampleCount; i++)
            {
                float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                short value = (short)(clamped * 32767f);
                wav[pos++] = (byte)(value & 0xFF);
                wav[pos++] = (byte)((value >> 8) & 0xFF);
            }

            return wav;
        }

        private static void WriteString(byte[] buffer, ref int pos, string value)
        {
            foreach (char c in value)
                buffer[pos++] = (byte)c;
        }

        private static void WriteInt32(byte[] buffer, ref int pos, int value)
        {
            buffer[pos++] = (byte)(value & 0xFF);
            buffer[pos++] = (byte)((value >> 8) & 0xFF);
            buffer[pos++] = (byte)((value >> 16) & 0xFF);
            buffer[pos++] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteInt16(byte[] buffer, ref int pos, short value)
        {
            buffer[pos++] = (byte)(value & 0xFF);
            buffer[pos++] = (byte)((value >> 8) & 0xFF);
        }

        [Serializable]
        private class SttResponse
        {
            public string text;
        }
    }
}
