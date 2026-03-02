using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VibeReal.Core;

namespace VibeReal.UI
{
    /// <summary>
    /// Displays connection and system status.
    /// Shows in the status bar at the top of the view.
    /// </summary>
    public class StatusIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WebSocketClient webSocketClient;
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private VoiceManager voiceManager;

        [Header("UI Elements")]
        [SerializeField] private Image connectionIndicator;
        [SerializeField] private TextMeshProUGUI connectionText;
        [SerializeField] private TextMeshProUGUI sessionCountText;
        [SerializeField] private Image micIndicator;
        [SerializeField] private TextMeshProUGUI timeText;

        [Header("Colors")]
        [SerializeField] private Color connectedColor = Color.green;
        [SerializeField] private Color connectingColor = Color.yellow;
        [SerializeField] private Color disconnectedColor = Color.red;
        [SerializeField] private Color micActiveColor = Color.red;
        [SerializeField] private Color micInactiveColor = Color.gray;

        private void Start()
        {
            if (webSocketClient == null)
                webSocketClient = FindObjectOfType<WebSocketClient>();
            if (sessionManager == null)
                sessionManager = FindObjectOfType<SessionManager>();
            if (voiceManager == null)
                voiceManager = FindObjectOfType<VoiceManager>();

            // Subscribe to events
            if (webSocketClient != null)
            {
                webSocketClient.OnConnected += UpdateConnectionStatus;
                webSocketClient.OnDisconnected += UpdateConnectionStatus;
            }

            if (sessionManager != null)
            {
                sessionManager.OnSessionsUpdated += HandleSessionsUpdated;
            }

            if (voiceManager != null)
            {
                voiceManager.OnStateChanged += HandleVoiceStateChanged;
            }

            // Initial update
            UpdateConnectionStatus();
            UpdateMicStatus();
        }

        private void OnDestroy()
        {
            if (webSocketClient != null)
            {
                webSocketClient.OnConnected -= UpdateConnectionStatus;
                webSocketClient.OnDisconnected -= UpdateConnectionStatus;
            }

            if (sessionManager != null)
            {
                sessionManager.OnSessionsUpdated -= HandleSessionsUpdated;
            }

            if (voiceManager != null)
            {
                voiceManager.OnStateChanged -= HandleVoiceStateChanged;
            }
        }

        private void Update()
        {
            // Update time
            if (timeText != null)
            {
                timeText.text = System.DateTime.Now.ToString("h:mm tt");
            }
        }

        private void UpdateConnectionStatus()
        {
            if (webSocketClient == null) return;

            var state = webSocketClient.State;

            if (connectionIndicator != null)
            {
                connectionIndicator.color = state switch
                {
                    WebSocketClient.ConnectionState.Connected => connectedColor,
                    WebSocketClient.ConnectionState.Connecting or
                    WebSocketClient.ConnectionState.Reconnecting => connectingColor,
                    _ => disconnectedColor
                };
            }

            if (connectionText != null)
            {
                connectionText.text = state switch
                {
                    WebSocketClient.ConnectionState.Connected => "Connected",
                    WebSocketClient.ConnectionState.Connecting => "Connecting...",
                    WebSocketClient.ConnectionState.Reconnecting => "Reconnecting...",
                    _ => "Disconnected"
                };
            }
        }

        private void HandleSessionsUpdated(System.Collections.Generic.List<Data.Session> sessions)
        {
            if (sessionCountText != null)
            {
                int count = sessions?.Count ?? 0;
                sessionCountText.text = count == 1 ? "1 Session" : $"{count} Sessions";
            }
        }

        private void HandleVoiceStateChanged(VoiceManager.VoiceState state)
        {
            UpdateMicStatus();
        }

        private void UpdateMicStatus()
        {
            if (micIndicator == null) return;

            bool isListening = voiceManager != null && voiceManager.IsListening;
            micIndicator.color = isListening ? micActiveColor : micInactiveColor;
        }
    }
}
