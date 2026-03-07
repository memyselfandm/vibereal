using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

namespace VibeReal.Editor
{
    public static class SceneSetup
    {
        [MenuItem("VibeReal/Fix XR Setup")]
        public static void FixXRSetup()
        {
            // 1. Fix Canvas: unparent from Camera Offset, set to root
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                // Unparent to root
                canvas.transform.SetParent(null, false);
                canvas.transform.position = new Vector3(0, 1.3f, 2f);
                canvas.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
                canvas.transform.rotation = Quaternion.identity;

                // Ensure World Space
                canvas.renderMode = RenderMode.WorldSpace;

                // Set canvas size
                var rt = canvas.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(1200, 800);

                // Add CanvasScaler if missing
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                {
                    scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                }
                scaler.dynamicPixelsPerUnit = 1;
                scaler.referencePixelsPerUnit = 100;

                // Swap GraphicRaycaster for TrackedDeviceGraphicRaycaster
                var oldRaycaster = canvas.GetComponent<GraphicRaycaster>();
                if (oldRaycaster != null && canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                {
                    Object.DestroyImmediate(oldRaycaster);
                    canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                }

                // Set world camera to Main Camera
                var cam = Camera.main;
                if (cam != null)
                {
                    canvas.worldCamera = cam;
                }

                EditorUtility.SetDirty(canvas.gameObject);
            }

            // 2. Fix Main Camera
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = new Color(0, 0, 0, 0);
                EditorUtility.SetDirty(mainCam.gameObject);
            }

            // 3. Set Graphics API to OpenGLES3
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

            // Save scene
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            Debug.Log("XR Setup fixed: Canvas unparented + world space, TrackedDeviceGraphicRaycaster, Camera solid color black, Graphics API = OpenGLES3");
        }

        [MenuItem("VibeReal/Setup UI")]
        public static void SetupUI()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("No Canvas found in scene");
                return;
            }

            var canvasRT = canvas.GetComponent<RectTransform>();

            // Set canvas size
            canvasRT.sizeDelta = new Vector2(1200, 800);

            // Status Bar
            var statusBar = CreateUIObject("StatusBar", canvasRT);
            var statusRT = statusBar.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0, 1);
            statusRT.anchorMax = new Vector2(1, 1);
            statusRT.pivot = new Vector2(0.5f, 1);
            statusRT.sizeDelta = new Vector2(0, 60);
            statusRT.anchoredPosition = Vector2.zero;

            var statusBg = statusBar.AddComponent<Image>();
            statusBg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            var statusIndicator = statusBar.AddComponent<VibeReal.UI.StatusIndicator>();

            // Connection indicator
            var connDot = CreateUIObject("ConnectionIndicator", statusRT);
            var connDotRT = connDot.GetComponent<RectTransform>();
            connDotRT.anchorMin = new Vector2(0, 0.5f);
            connDotRT.anchorMax = new Vector2(0, 0.5f);
            connDotRT.pivot = new Vector2(0, 0.5f);
            connDotRT.anchoredPosition = new Vector2(20, 0);
            connDotRT.sizeDelta = new Vector2(16, 16);
            var connImg = connDot.AddComponent<Image>();
            connImg.color = Color.red;

            // Connection text
            var connText = CreateTMPText("ConnectionText", statusRT, "Disconnected", 20);
            var connTextRT = connText.GetComponent<RectTransform>();
            connTextRT.anchorMin = new Vector2(0, 0);
            connTextRT.anchorMax = new Vector2(0.4f, 1);
            connTextRT.offsetMin = new Vector2(46, 0);
            connTextRT.offsetMax = Vector2.zero;

            // Session count
            var sessCount = CreateTMPText("SessionCountText", statusRT, "0 Sessions", 20);
            var sessCountRT = sessCount.GetComponent<RectTransform>();
            sessCountRT.anchorMin = new Vector2(0.4f, 0);
            sessCountRT.anchorMax = new Vector2(0.6f, 1);
            sessCountRT.offsetMin = Vector2.zero;
            sessCountRT.offsetMax = Vector2.zero;
            sessCount.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            // Mic indicator
            var micDot = CreateUIObject("MicIndicator", statusRT);
            var micDotRT = micDot.GetComponent<RectTransform>();
            micDotRT.anchorMin = new Vector2(1, 0.5f);
            micDotRT.anchorMax = new Vector2(1, 0.5f);
            micDotRT.pivot = new Vector2(1, 0.5f);
            micDotRT.anchoredPosition = new Vector2(-80, 0);
            micDotRT.sizeDelta = new Vector2(16, 16);
            var micImg = micDot.AddComponent<Image>();
            micImg.color = Color.gray;

            // Time text
            var timeText = CreateTMPText("TimeText", statusRT, "12:00 PM", 20);
            var timeTextRT = timeText.GetComponent<RectTransform>();
            timeTextRT.anchorMin = new Vector2(0.8f, 0);
            timeTextRT.anchorMax = new Vector2(1, 1);
            timeTextRT.offsetMin = Vector2.zero;
            timeTextRT.offsetMax = new Vector2(-10, 0);
            timeText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

            // Wire StatusIndicator fields via SerializedObject
            var so = new SerializedObject(statusIndicator);
            so.FindProperty("connectionIndicator").objectReferenceValue = connImg;
            so.FindProperty("connectionText").objectReferenceValue = connText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("sessionCountText").objectReferenceValue = sessCount.GetComponent<TextMeshProUGUI>();
            so.FindProperty("micIndicator").objectReferenceValue = micImg;
            so.FindProperty("timeText").objectReferenceValue = timeText.GetComponent<TextMeshProUGUI>();
            so.ApplyModifiedProperties();

            // Session Panel
            var sessionPanel = CreateUIObject("SessionPanel", canvasRT);
            var panelRT = sessionPanel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.05f, 0.05f);
            panelRT.anchorMax = new Vector2(0.95f, 0.9f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            var panelBg = sessionPanel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            var panelCtrl = sessionPanel.AddComponent<VibeReal.UI.SessionPanelController>();

            // Session name header
            var sessName = CreateTMPText("SessionName", panelRT, "No Session", 28);
            var sessNameRT = sessName.GetComponent<RectTransform>();
            sessNameRT.anchorMin = new Vector2(0, 1);
            sessNameRT.anchorMax = new Vector2(0.7f, 1);
            sessNameRT.pivot = new Vector2(0, 1);
            sessNameRT.sizeDelta = new Vector2(0, 50);
            sessNameRT.anchoredPosition = new Vector2(20, -10);
            sessName.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

            // Status text
            var statusTxt = CreateTMPText("StatusText", panelRT, "Disconnected", 22);
            var statusTxtRT = statusTxt.GetComponent<RectTransform>();
            statusTxtRT.anchorMin = new Vector2(0.7f, 1);
            statusTxtRT.anchorMax = new Vector2(1, 1);
            statusTxtRT.pivot = new Vector2(1, 1);
            statusTxtRT.sizeDelta = new Vector2(0, 50);
            statusTxtRT.anchoredPosition = new Vector2(-20, -10);
            statusTxt.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

            // Status indicator dot
            var statusDot = CreateUIObject("StatusDot", panelRT);
            var statusDotRT = statusDot.GetComponent<RectTransform>();
            statusDotRT.anchorMin = new Vector2(0.7f, 1);
            statusDotRT.anchorMax = new Vector2(0.7f, 1);
            statusDotRT.pivot = new Vector2(1, 1);
            statusDotRT.anchoredPosition = new Vector2(-5, -25);
            statusDotRT.sizeDelta = new Vector2(12, 12);
            var statusDotImg = statusDot.AddComponent<Image>();
            statusDotImg.color = Color.gray;

            // Current task
            var taskText = CreateTMPText("CurrentTask", panelRT, "", 18);
            var taskTextRT = taskText.GetComponent<RectTransform>();
            taskTextRT.anchorMin = new Vector2(0, 1);
            taskTextRT.anchorMax = new Vector2(1, 1);
            taskTextRT.pivot = new Vector2(0, 1);
            taskTextRT.sizeDelta = new Vector2(0, 30);
            taskTextRT.anchoredPosition = new Vector2(20, -65);
            taskText.GetComponent<TextMeshProUGUI>().color = new Color(0.7f, 0.7f, 0.7f);

            // Conversation scroll view
            var scrollView = new GameObject("ConversationScroll");
            scrollView.transform.SetParent(panelRT, false);
            var scrollRT = scrollView.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0.1f);
            scrollRT.anchorMax = new Vector2(1, 0.85f);
            scrollRT.offsetMin = new Vector2(15, 0);
            scrollRT.offsetMax = new Vector2(-15, 0);

            var scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            var scrollMask = scrollView.AddComponent<Mask>();
            var scrollMaskImg = scrollView.AddComponent<Image>();
            scrollMaskImg.color = new Color(0, 0, 0, 0.01f);

            // Content container
            var content = new GameObject("Content");
            content.transform.SetParent(scrollRT, false);
            var contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 0);
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;
            scrollRect.viewport = scrollRT;

            // Conversation text
            var convText = CreateTMPText("ConversationText", contentRT, "Waiting for connection...", 20);
            var convTextRT = convText.GetComponent<RectTransform>();
            convTextRT.anchorMin = Vector2.zero;
            convTextRT.anchorMax = new Vector2(1, 1);
            convTextRT.offsetMin = Vector2.zero;
            convTextRT.offsetMax = Vector2.zero;
            var convTMP = convText.GetComponent<TextMeshProUGUI>();
            convTMP.alignment = TextAlignmentOptions.TopLeft;
            convTMP.overflowMode = TextOverflowModes.Overflow;
            convTMP.richText = true;

            // PTT Indicator
            var pttObj = CreateUIObject("PTTIndicator", panelRT);
            var pttRT = pttObj.GetComponent<RectTransform>();
            pttRT.anchorMin = new Vector2(0.3f, 0);
            pttRT.anchorMax = new Vector2(0.7f, 0.1f);
            pttRT.offsetMin = Vector2.zero;
            pttRT.offsetMax = Vector2.zero;
            var pttBg = pttObj.AddComponent<Image>();
            pttBg.color = new Color(0.8f, 0.1f, 0.1f, 0.8f);
            pttObj.SetActive(false);

            var pttText = CreateTMPText("TranscriptText", pttRT, "Listening...", 20);
            var pttTextTMP = pttText.GetComponent<TextMeshProUGUI>();
            pttTextTMP.alignment = TextAlignmentOptions.Center;
            var pttTextRT = pttText.GetComponent<RectTransform>();
            pttTextRT.anchorMin = Vector2.zero;
            pttTextRT.anchorMax = Vector2.one;
            pttTextRT.offsetMin = new Vector2(10, 5);
            pttTextRT.offsetMax = new Vector2(-10, -5);

            // Wire SessionPanelController fields
            var panelSO = new SerializedObject(panelCtrl);
            panelSO.FindProperty("sessionNameText").objectReferenceValue = sessName.GetComponent<TextMeshProUGUI>();
            panelSO.FindProperty("statusText").objectReferenceValue = statusTxt.GetComponent<TextMeshProUGUI>();
            panelSO.FindProperty("statusIndicator").objectReferenceValue = statusDotImg;
            panelSO.FindProperty("currentTaskText").objectReferenceValue = taskText.GetComponent<TextMeshProUGUI>();
            panelSO.FindProperty("conversationScrollRect").objectReferenceValue = scrollRect;
            panelSO.FindProperty("conversationContent").objectReferenceValue = contentRT;
            panelSO.FindProperty("conversationText").objectReferenceValue = convTMP;
            panelSO.FindProperty("pttIndicator").objectReferenceValue = pttObj;
            panelSO.FindProperty("transcriptText").objectReferenceValue = pttTextTMP;
            panelSO.ApplyModifiedProperties();

            // Approval Dialog (hidden by default)
            var approvalDialog = CreateUIObject("ApprovalDialog", canvasRT);
            var approvalRT = approvalDialog.GetComponent<RectTransform>();
            approvalRT.anchorMin = new Vector2(0.2f, 0.3f);
            approvalRT.anchorMax = new Vector2(0.8f, 0.7f);
            approvalRT.offsetMin = Vector2.zero;
            approvalRT.offsetMax = Vector2.zero;

            var approvalBg = approvalDialog.AddComponent<Image>();
            approvalBg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            var approvalCtrl = approvalDialog.AddComponent<VibeReal.UI.ApprovalDialogController>();
            approvalDialog.SetActive(false);

            // Approval title
            var approvalTitle = CreateTMPText("Title", approvalRT, "Approval Required", 26);
            var approvalTitleRT = approvalTitle.GetComponent<RectTransform>();
            approvalTitleRT.anchorMin = new Vector2(0, 0.7f);
            approvalTitleRT.anchorMax = new Vector2(1, 1);
            approvalTitleRT.offsetMin = new Vector2(20, 0);
            approvalTitleRT.offsetMax = new Vector2(-20, -10);
            approvalTitle.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

            // Approval body
            var approvalBody = CreateTMPText("Body", approvalRT, "", 20);
            var approvalBodyRT = approvalBody.GetComponent<RectTransform>();
            approvalBodyRT.anchorMin = new Vector2(0, 0.25f);
            approvalBodyRT.anchorMax = new Vector2(1, 0.7f);
            approvalBodyRT.offsetMin = new Vector2(20, 0);
            approvalBodyRT.offsetMax = new Vector2(-20, 0);

            Debug.Log("VibeReal UI setup complete!");
            EditorUtility.SetDirty(canvas.gameObject);

            // Save scene
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        [MenuItem("VibeReal/Add Scene To Build")]
        public static void AddSceneToBuild()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Check if already added
            foreach (var s in scenes)
            {
                if (s.path == scene.path)
                {
                    Debug.Log($"Scene '{scene.path}' already in build settings.");
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scene.path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"Added '{scene.path}' to build settings at index {scenes.Count - 1}.");
        }

        private static GameObject CreateUIObject(string name, RectTransform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static GameObject CreateTMPText(string name, RectTransform parent, string text, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            return go;
        }
    }
}
