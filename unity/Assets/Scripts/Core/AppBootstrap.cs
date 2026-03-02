using UnityEngine;
using VibeReal.Data;

namespace VibeReal.Core
{
    /// <summary>
    /// Application bootstrap - initializes all managers and starts connection.
    /// Attach this to a GameObject in the scene to auto-start the app.
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        [Header("Auto-Connect")]
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private float connectDelay = 1f;

        [Header("References (Auto-found if null)")]
        [SerializeField] private WebSocketClient webSocketClient;
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private VoiceManager voiceManager;
        [SerializeField] private NotificationManager notificationManager;

        private void Awake()
        {
            // Ensure config is loaded early
            var config = AppConfig.Load();
            Debug.Log($"VibeReal starting - Hub URL: {config.hubUrl}");
        }

        private void Start()
        {
            // Find references if not set
            if (webSocketClient == null)
                webSocketClient = FindObjectOfType<WebSocketClient>();
            if (sessionManager == null)
                sessionManager = FindObjectOfType<SessionManager>();
            if (voiceManager == null)
                voiceManager = FindObjectOfType<VoiceManager>();
            if (notificationManager == null)
                notificationManager = FindObjectOfType<NotificationManager>();

            // Subscribe to connection events for logging
            if (webSocketClient != null)
            {
                webSocketClient.OnConnected += HandleConnected;
                webSocketClient.OnDisconnected += HandleDisconnected;
                webSocketClient.OnError += HandleError;
            }

            // Auto-connect
            if (autoConnectOnStart)
            {
                Invoke(nameof(Connect), connectDelay);
            }
        }

        private void OnDestroy()
        {
            if (webSocketClient != null)
            {
                webSocketClient.OnConnected -= HandleConnected;
                webSocketClient.OnDisconnected -= HandleDisconnected;
                webSocketClient.OnError -= HandleError;
            }
        }

        /// <summary>
        /// Connect to the Session Hub
        /// </summary>
        public void Connect()
        {
            if (webSocketClient != null)
            {
                Debug.Log("Connecting to Session Hub...");
                webSocketClient.Connect();
            }
            else
            {
                Debug.LogError("WebSocketClient not found!");
            }
        }

        /// <summary>
        /// Disconnect from the Session Hub
        /// </summary>
        public void Disconnect()
        {
            if (webSocketClient != null)
            {
                webSocketClient.Disconnect();
            }
        }

        private void HandleConnected()
        {
            Debug.Log("Connected to Session Hub");

            // Show notification
            if (notificationManager != null)
            {
                notificationManager.CreateLocalNotification(
                    "Connected",
                    "Connected to Session Hub",
                    "normal",
                    "info"
                );
            }

            // Speak confirmation
            if (voiceManager != null)
            {
                voiceManager.Speak("Connected to session hub.");
            }
        }

        private void HandleDisconnected()
        {
            Debug.Log("Disconnected from Session Hub");

            if (notificationManager != null)
            {
                notificationManager.CreateLocalNotification(
                    "Disconnected",
                    "Lost connection to Session Hub",
                    "high",
                    "error"
                );
            }
        }

        private void HandleError(string error)
        {
            Debug.LogError($"Connection error: {error}");

            if (notificationManager != null)
            {
                notificationManager.CreateLocalNotification(
                    "Connection Error",
                    error,
                    "high",
                    "error"
                );
            }
        }
    }
}
