using System;
using UnityEngine;

namespace VibeReal.Data
{
    /// <summary>
    /// Application configuration loaded from Resources/config.json
    /// </summary>
    [Serializable]
    public class AppConfig
    {
        // Hub connection
        public string hubUrl = "ws://192.168.1.100:8080/client";
        public string apiKey = "";

        // Voice settings
        public string languageCode = "en-US";
        public float speechRate = 1.1f;
        public float pitch = 1.0f;
        public int maxTtsWords = 50;
        public bool summarizeLongResponses = true;

        // Connection settings
        public float reconnectDelaySeconds = 5f;
        public float maxReconnectDelaySeconds = 60f;
        public int maxReconnectAttempts = 10;

        // UI settings
        public float panelDistance = 1.5f;
        public float panelWidth = 0.6f;
        public float panelHeight = 0.4f;

        // Timeouts
        public float listeningTimeoutSeconds = 10f;
        public float silenceThresholdSeconds = 1.5f;

        private static AppConfig _instance;

        /// <summary>
        /// Load config from Resources/config.json
        /// </summary>
        public static AppConfig Load()
        {
            if (_instance != null)
                return _instance;

            var configAsset = Resources.Load<TextAsset>("config");
            if (configAsset != null)
            {
                _instance = JsonUtility.FromJson<AppConfig>(configAsset.text);
            }
            else
            {
                Debug.LogWarning("config.json not found in Resources, using defaults");
                _instance = new AppConfig();
            }

            return _instance;
        }

        /// <summary>
        /// Reset cached instance (for testing)
        /// </summary>
        public static void Reset()
        {
            _instance = null;
        }
    }
}
