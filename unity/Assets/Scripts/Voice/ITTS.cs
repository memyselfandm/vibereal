using System;

namespace VibeReal.Voice
{
    public enum SpeechPriority
    {
        Low,
        Normal,
        High,
        Urgent
    }

    public interface ITTS
    {
        event Action OnInitialized;
        event Action<string> OnSpeechStart;
        event Action<string> OnSpeechComplete;
        event Action<string> OnError;

        bool IsInitialized { get; }
        bool IsSpeaking { get; }

        void Initialize();
        void Speak(string text, SpeechPriority priority = SpeechPriority.Normal);
        void QueueSpeak(string text, SpeechPriority priority = SpeechPriority.Normal);
        void Stop();
        void ClearQueue();
    }
}
