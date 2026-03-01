using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VibeReal.Core;

namespace VibeReal.UI
{
    /// <summary>
    /// Always-visible status bar showing connection state, session count, and clock.
    /// MVP scope: connection dot, session count, time. No mic status yet.
    /// </summary>
    public class StatusBarUI : MonoBehaviour
    {
        [SerializeField] private WebSocketClient wsClient;
        [SerializeField] private SessionManager sessionManager;

        [Header("UI Elements")]
        [SerializeField] private Image connectionDot;
        [SerializeField] private TMP_Text connectionLabel;
        [SerializeField] private TMP_Text sessionCountText;
        [SerializeField] private TMP_Text clockText;

        private static readonly Color ColorConnected = new(0.3f, 0.9f, 0.3f);
        private static readonly Color ColorDisconnected = new(0.9f, 0.3f, 0.3f);
        private static readonly Color ColorConnecting = new(1f, 0.8f, 0.2f);

        private void OnEnable()
        {
            if (wsClient != null)
            {
                wsClient.OnConnected += RefreshConnection;
                wsClient.OnDisconnected += RefreshConnection;
            }

            if (sessionManager != null)
            {
                sessionManager.OnSessionAdded += _ => RefreshSessionCount();
                sessionManager.OnSessionRemoved += _ => RefreshSessionCount();
            }
        }

        private void OnDisable()
        {
            if (wsClient != null)
            {
                wsClient.OnConnected -= RefreshConnection;
                wsClient.OnDisconnected -= RefreshConnection;
            }
        }

        private void Update()
        {
            // Update clock every frame (cheap enough for MVP)
            if (clockText != null)
                clockText.text = System.DateTime.Now.ToString("h:mm tt");

            // Refresh connection state in case of reconnecting
            RefreshConnection();
        }

        private void RefreshConnection()
        {
            if (wsClient == null) return;

            var state = wsClient.State;

            if (connectionDot != null)
            {
                connectionDot.color = state switch
                {
                    ConnectionState.Connected => ColorConnected,
                    ConnectionState.Connecting or ConnectionState.Reconnecting => ColorConnecting,
                    _ => ColorDisconnected,
                };
            }

            if (connectionLabel != null)
            {
                connectionLabel.text = state switch
                {
                    ConnectionState.Connected => "Connected",
                    ConnectionState.Connecting => "Connecting...",
                    ConnectionState.Reconnecting => "Reconnecting...",
                    _ => "Disconnected",
                };
            }
        }

        private void RefreshSessionCount()
        {
            if (sessionCountText == null || sessionManager == null) return;

            int count = sessionManager.Sessions.Count;
            sessionCountText.text = count == 1 ? "1 Session" : $"{count} Sessions";
        }
    }
}
