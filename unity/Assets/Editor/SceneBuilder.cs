using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace VibeReal.Editor
{
    /// <summary>
    /// Editor menu tool that builds the entire MVP scene hierarchy in one click.
    /// Run via: VibeReal > Build MVP Scene
    /// </summary>
    public static class SceneBuilder
    {
        [MenuItem("VibeReal/Build MVP Scene")]
        public static void BuildScene()
        {
            // --- Managers ---
            var managers = new GameObject("Managers");

            var wsClientGO = new GameObject("WebSocketClient");
            wsClientGO.transform.SetParent(managers.transform);
            var wsClient = wsClientGO.AddComponent<Core.WebSocketClient>();

            var sessionMgrGO = new GameObject("SessionManager");
            sessionMgrGO.transform.SetParent(managers.transform);
            var sessionMgr = sessionMgrGO.AddComponent<Core.SessionManager>();

            var notifMgrGO = new GameObject("NotificationManager");
            notifMgrGO.transform.SetParent(managers.transform);
            var notifMgr = notifMgrGO.AddComponent<Core.NotificationManager>();

            // --- Canvas (Screen Space for testing, switch to World Space for AR) ---
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // --- Status Bar (top) ---
            var statusBar = CreatePanel(canvasGO.transform, "StatusBar", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -30), new Vector2(0, 0), 60f);
            SetColor(statusBar, new Color(0.1f, 0.1f, 0.15f, 0.9f));

            var connDot = CreateImage(statusBar.transform, "ConnectionDot",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(16, 16));
            connDot.GetComponent<Image>().color = Color.red;

            var connLabel = CreateTMPText(statusBar.transform, "ConnectionLabel", "Disconnected",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(55, 0), new Vector2(150, 30), 16);

            var sessCount = CreateTMPText(statusBar.transform, "SessionCount", "0 Sessions",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 30), 16);

            var clock = CreateTMPText(statusBar.transform, "Clock", "12:00 PM",
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(120, 30), 16);
            clock.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Right;

            var statusBarUI = statusBar.AddComponent<UI.StatusBarUI>();
            SetPrivateField(statusBarUI, "wsClient", wsClient);
            SetPrivateField(statusBarUI, "sessionManager", sessionMgr);
            SetPrivateField(statusBarUI, "connectionDot", connDot.GetComponent<Image>());
            SetPrivateField(statusBarUI, "connectionLabel", connLabel.GetComponent<TMP_Text>());
            SetPrivateField(statusBarUI, "sessionCountText", sessCount.GetComponent<TMP_Text>());
            SetPrivateField(statusBarUI, "clockText", clock.GetComponent<TMP_Text>());

            // --- Session Panel (center) ---
            var sessionPanel = CreatePanel(canvasGO.transform, "SessionPanel",
                new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.92f), Vector2.zero, Vector2.zero, 0);

            SetColor(sessionPanel, new Color(0.12f, 0.12f, 0.18f, 0.95f));

            // Header
            var header = CreatePanel(sessionPanel.transform, "Header",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -20), new Vector2(0, 0), 40f);
            SetColor(header, new Color(0.08f, 0.08f, 0.12f, 1f));

            var statusDotImg = CreateImage(header.transform, "StatusDot",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(15, 0), new Vector2(12, 12));
            statusDotImg.GetComponent<Image>().color = new Color(0.4f, 0.8f, 0.4f);

            var sessionName = CreateTMPText(header.transform, "SessionName", "Container 1",
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(35, 0), new Vector2(200, 30), 18);

            var statusLabel = CreateTMPText(header.transform, "StatusLabel", "Idle",
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-15, 0), new Vector2(150, 30), 16);
            statusLabel.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Right;
            statusLabel.GetComponent<TMP_Text>().fontStyle = FontStyles.Italic;

            // Conversation area
            var convArea = CreatePanel(sessionPanel.transform, "ConversationArea",
                new Vector2(0, 0.08f), new Vector2(1, 0.92f), new Vector2(0, 5), new Vector2(0, -50), 0);
            SetColor(convArea, new Color(0.05f, 0.05f, 0.08f, 1f));

            var scrollRect = convArea.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(convArea.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 0);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.offsetMin = new Vector2(10, 10);
            contentRT.offsetMax = new Vector2(-10, -10);

            var contentSizeFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var convText = contentGO.AddComponent<TextMeshProUGUI>();
            convText.fontSize = 14;
            convText.color = new Color(0.85f, 0.85f, 0.9f);
            convText.enableWordWrapping = true;
            convText.richText = true;
            convText.text = "<b>Claude:</b> Waiting for connection...";

            scrollRect.content = contentRT;
            scrollRect.viewport = convArea.GetComponent<RectTransform>();
            convArea.AddComponent<RectMask2D>();

            // Input area
            var inputArea = CreatePanel(sessionPanel.transform, "InputArea",
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 5), new Vector2(0, 0), 45f);
            SetColor(inputArea, new Color(0.08f, 0.08f, 0.12f, 1f));

            // Input field
            var inputFieldGO = new GameObject("MessageInput");
            inputFieldGO.transform.SetParent(inputArea.transform, false);
            var inputRT = inputFieldGO.AddComponent<RectTransform>();
            inputRT.anchorMin = new Vector2(0, 0);
            inputRT.anchorMax = new Vector2(0.8f, 1);
            inputRT.offsetMin = new Vector2(10, 5);
            inputRT.offsetMax = new Vector2(-5, -5);

            var inputField = inputFieldGO.AddComponent<TMP_InputField>();

            // Input field text area & text
            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(inputFieldGO.transform, false);
            var textAreaRT = textAreaGO.AddComponent<RectTransform>();
            textAreaRT.anchorMin = Vector2.zero;
            textAreaRT.anchorMax = Vector2.one;
            textAreaRT.offsetMin = new Vector2(5, 0);
            textAreaRT.offsetMax = new Vector2(-5, 0);
            textAreaGO.AddComponent<RectMask2D>();

            var inputTextGO = new GameObject("Text");
            inputTextGO.transform.SetParent(textAreaGO.transform, false);
            var inputTextRT = inputTextGO.AddComponent<RectTransform>();
            inputTextRT.anchorMin = Vector2.zero;
            inputTextRT.anchorMax = Vector2.one;
            inputTextRT.offsetMin = Vector2.zero;
            inputTextRT.offsetMax = Vector2.zero;
            var inputTMP = inputTextGO.AddComponent<TextMeshProUGUI>();
            inputTMP.fontSize = 14;
            inputTMP.color = Color.white;

            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var placeholderRT = placeholderGO.AddComponent<RectTransform>();
            placeholderRT.anchorMin = Vector2.zero;
            placeholderRT.anchorMax = Vector2.one;
            placeholderRT.offsetMin = Vector2.zero;
            placeholderRT.offsetMax = Vector2.zero;
            var placeholderTMP = placeholderGO.AddComponent<TextMeshProUGUI>();
            placeholderTMP.text = "Type a message...";
            placeholderTMP.fontSize = 14;
            placeholderTMP.fontStyle = FontStyles.Italic;
            placeholderTMP.color = new Color(0.5f, 0.5f, 0.5f);

            inputField.textViewport = textAreaRT;
            inputField.textComponent = inputTMP;
            inputField.placeholder = placeholderTMP;

            // Background for input field
            var inputBG = inputFieldGO.AddComponent<Image>();
            inputBG.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            inputField.targetGraphic = inputBG;

            // Send button
            var sendBtnGO = CreateButton(inputArea.transform, "SendButton", "Send",
                new Vector2(0.82f, 0), new Vector2(1, 1), new Vector2(0, 5), new Vector2(-10, -5),
                new Color(0.2f, 0.5f, 1f));

            // Approval overlay (hidden by default)
            var approvalOverlay = CreatePanel(sessionPanel.transform, "ApprovalOverlay",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 0);
            SetColor(approvalOverlay, new Color(0, 0, 0, 0.85f));
            approvalOverlay.SetActive(false);

            var approvalTitle = CreateTMPText(approvalOverlay.transform, "ApprovalTitle", "Approval Required",
                new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(400, 40), 22);
            approvalTitle.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
            approvalTitle.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            approvalTitle.GetComponent<TMP_Text>().color = new Color(1f, 0.8f, 0.2f);

            var approvalBody = CreateTMPText(approvalOverlay.transform, "ApprovalBody", "Command details here",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 100), 16);
            approvalBody.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

            var approveBtn = CreateButton(approvalOverlay.transform, "ApproveButton", "Approve",
                new Vector2(0.2f, 0.2f), new Vector2(0.45f, 0.2f), new Vector2(0, 0), new Vector2(0, 40),
                new Color(0.2f, 0.7f, 0.3f));

            var denyBtn = CreateButton(approvalOverlay.transform, "DenyButton", "Deny",
                new Vector2(0.55f, 0.2f), new Vector2(0.8f, 0.2f), new Vector2(0, 0), new Vector2(0, 40),
                new Color(0.8f, 0.2f, 0.2f));

            // Wire SessionPanelUI
            var panelUI = sessionPanel.AddComponent<UI.SessionPanelUI>();
            SetPrivateField(panelUI, "sessionManager", sessionMgr);
            SetPrivateField(panelUI, "notificationManager", notifMgr);
            SetPrivateField(panelUI, "wsClient", wsClient);
            SetPrivateField(panelUI, "statusDot", statusDotImg.GetComponent<Image>());
            SetPrivateField(panelUI, "sessionNameText", sessionName.GetComponent<TMP_Text>());
            SetPrivateField(panelUI, "statusLabelText", statusLabel.GetComponent<TMP_Text>());
            SetPrivateField(panelUI, "conversationText", convText);
            SetPrivateField(panelUI, "scrollRect", scrollRect);
            SetPrivateField(panelUI, "messageInput", inputField);
            SetPrivateField(panelUI, "sendButton", sendBtnGO.GetComponent<Button>());
            SetPrivateField(panelUI, "approvalOverlay", approvalOverlay);
            SetPrivateField(panelUI, "approvalBodyText", approvalBody.GetComponent<TMP_Text>());
            SetPrivateField(panelUI, "approveButton", approveBtn.GetComponent<Button>());
            SetPrivateField(panelUI, "denyButton", denyBtn.GetComponent<Button>());

            // --- Notification Toast Area (top right) ---
            var notifArea = CreatePanel(canvasGO.transform, "NotificationArea",
                new Vector2(0.6f, 0.75f), new Vector2(0.98f, 0.92f), Vector2.zero, Vector2.zero, 0);
            // Transparent container
            var notifImg = notifArea.GetComponent<Image>();
            if (notifImg != null) Object.DestroyImmediate(notifImg);

            var toastRoot = CreatePanel(notifArea.transform, "ToastRoot",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 0);
            SetColor(toastRoot, new Color(0.15f, 0.15f, 0.2f, 0.95f));
            toastRoot.SetActive(false);

            // Add an outline to the toast
            var toastOutline = toastRoot.AddComponent<Outline>();
            toastOutline.effectColor = new Color(0.3f, 0.6f, 1f, 0.5f);
            toastOutline.effectDistance = new Vector2(1, -1);

            var toastSession = CreateTMPText(toastRoot.transform, "ToastSession", "Container 1",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -5), new Vector2(-10, 0), 12);
            toastSession.GetComponent<TMP_Text>().color = new Color(0.5f, 0.7f, 1f);
            var tsRT = toastSession.GetComponent<RectTransform>();
            tsRT.sizeDelta = new Vector2(0, 18);

            var toastTitle = CreateTMPText(toastRoot.transform, "ToastTitle", "Notification",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -25), new Vector2(-10, 0), 14);
            toastTitle.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            var ttRT = toastTitle.GetComponent<RectTransform>();
            ttRT.sizeDelta = new Vector2(0, 20);

            var toastBody = CreateTMPText(toastRoot.transform, "ToastBody", "Details here",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 5), new Vector2(-10, -50), 13);

            var toastUI = notifArea.AddComponent<UI.NotificationToastUI>();
            SetPrivateField(toastUI, "notificationManager", notifMgr);
            SetPrivateField(toastUI, "toastRoot", toastRoot);
            SetPrivateField(toastUI, "toastTitle", toastTitle.GetComponent<TMP_Text>());
            SetPrivateField(toastUI, "toastBody", toastBody.GetComponent<TMP_Text>());
            SetPrivateField(toastUI, "toastSession", toastSession.GetComponent<TMP_Text>());

            // --- Directional Light ---
            var light = new GameObject("Directional Light");
            var lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            // --- EventSystem (required for UI interaction) ---
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Wire SerializeField references on managers
            SetPrivateField(sessionMgr, "wsClient", wsClient);
            SetPrivateField(notifMgr, "wsClient", wsClient);

            Debug.Log("[VibeReal] MVP Scene built! Enter Play mode to test.");
            EditorUtility.DisplayDialog("VibeReal", "MVP scene built successfully.\n\n1. Start the stub server: cd stub-server && npm start\n2. Enter Play mode in Unity\n3. The app will connect to ws://localhost:8080", "OK");
        }

        // --- Helper methods ---

        private static GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            if (height > 0)
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            }
            go.AddComponent<Image>();
            return go;
        }

        private static void SetColor(GameObject go, Color color)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.color = color;
        }

        private static GameObject CreateImage(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.AddComponent<Image>();
            return go;
        }

        private static GameObject CreateTMPText(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = new Color(0.85f, 0.85f, 0.9f);
            tmp.enableWordWrapping = true;
            return go;
        }

        private static GameObject CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return go;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(target, value);
                EditorUtility.SetDirty(target as Object);
            }
            else
            {
                Debug.LogWarning($"[SceneBuilder] Could not find field '{fieldName}' on {type.Name}");
            }
        }
    }
}
