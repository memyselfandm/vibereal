using System;

namespace VibeReal.Voice
{
    public enum SpeechError
    {
        Unknown = 0,
        NetworkTimeout = 1,
        Network = 2,
        Audio = 3,
        Server = 4,
        Client = 5,
        SpeechTimeout = 6,
        NoMatch = 7,
        RecognizerBusy = 8,
        InsufficientPermissions = 9
    }

    public interface ISTT
    {
        event Action OnReadyForSpeech;
        event Action OnBeginningOfSpeech;
        event Action OnEndOfSpeech;
        event Action<string> OnPartialResult;
        event Action<string, float> OnResult;
        event Action<SpeechError> OnError;

        bool IsListening { get; }
        bool IsInitialized { get; }

        void Initialize();
        void StartListening();
        void StopListening();
        void Cancel();
    }
}
