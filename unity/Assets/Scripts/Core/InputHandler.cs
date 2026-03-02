using UnityEngine;
using UnityEngine.InputSystem;
using VibeReal.Core;

namespace VibeReal.Core
{
    /// <summary>
    /// Handles controller input for push-to-talk and other interactions.
    /// Uses Unity's Input System for XREAL Beam Pro controller.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VoiceManager voiceManager;
        [SerializeField] private NotificationManager notificationManager;
        [SerializeField] private SessionManager sessionManager;

        [Header("Input Actions")]
        [SerializeField] private InputActionReference pushToTalkAction;
        [SerializeField] private InputActionReference approveAction;
        [SerializeField] private InputActionReference denyAction;
        [SerializeField] private InputActionReference menuAction;

        [Header("Keyboard Fallbacks (Editor)")]
        [SerializeField] private KeyCode pttKey = KeyCode.Space;
        [SerializeField] private KeyCode approveKey = KeyCode.Y;
        [SerializeField] private KeyCode denyKey = KeyCode.N;

        private bool _isPttPressed;

        private void Start()
        {
            if (voiceManager == null)
                voiceManager = FindObjectOfType<VoiceManager>();
            if (notificationManager == null)
                notificationManager = FindObjectOfType<NotificationManager>();
            if (sessionManager == null)
                sessionManager = FindObjectOfType<SessionManager>();

            // Set up input actions if available
            SetupInputActions();
        }

        private void OnEnable()
        {
            EnableInputActions();
        }

        private void OnDisable()
        {
            DisableInputActions();
        }

        private void Update()
        {
            // Keyboard fallbacks for editor testing
#if UNITY_EDITOR
            HandleKeyboardInput();
#endif
        }

        private void SetupInputActions()
        {
            if (pushToTalkAction != null && pushToTalkAction.action != null)
            {
                pushToTalkAction.action.started += OnPttStarted;
                pushToTalkAction.action.canceled += OnPttCanceled;
            }

            if (approveAction != null && approveAction.action != null)
            {
                approveAction.action.performed += OnApprovePerformed;
            }

            if (denyAction != null && denyAction.action != null)
            {
                denyAction.action.performed += OnDenyPerformed;
            }

            if (menuAction != null && menuAction.action != null)
            {
                menuAction.action.performed += OnMenuPerformed;
            }
        }

        private void EnableInputActions()
        {
            pushToTalkAction?.action?.Enable();
            approveAction?.action?.Enable();
            denyAction?.action?.Enable();
            menuAction?.action?.Enable();
        }

        private void DisableInputActions()
        {
            pushToTalkAction?.action?.Disable();
            approveAction?.action?.Disable();
            denyAction?.action?.Disable();
            menuAction?.action?.Disable();
        }

        // ==================== Input Action Callbacks ====================

        private void OnPttStarted(InputAction.CallbackContext context)
        {
            if (voiceManager != null && !_isPttPressed)
            {
                _isPttPressed = true;
                voiceManager.StartListening();
            }
        }

        private void OnPttCanceled(InputAction.CallbackContext context)
        {
            if (voiceManager != null && _isPttPressed)
            {
                _isPttPressed = false;
                voiceManager.StopListening();
            }
        }

        private void OnApprovePerformed(InputAction.CallbackContext context)
        {
            if (notificationManager != null && notificationManager.CurrentApproval != null)
            {
                notificationManager.ApproveCurrentApproval();
            }
        }

        private void OnDenyPerformed(InputAction.CallbackContext context)
        {
            if (notificationManager != null && notificationManager.CurrentApproval != null)
            {
                notificationManager.DenyCurrentApproval();
            }
        }

        private void OnMenuPerformed(InputAction.CallbackContext context)
        {
            // Could open settings or session list
            Debug.Log("Menu button pressed");
        }

        // ==================== Keyboard Fallbacks (Editor) ====================

#if UNITY_EDITOR
        private void HandleKeyboardInput()
        {
            // Push-to-talk
            if (Input.GetKeyDown(pttKey))
            {
                if (voiceManager != null && !_isPttPressed)
                {
                    _isPttPressed = true;
                    voiceManager.StartListening();
                }
            }

            if (Input.GetKeyUp(pttKey))
            {
                if (voiceManager != null && _isPttPressed)
                {
                    _isPttPressed = false;
                    voiceManager.StopListening();
                }
            }

            // Approve (when approval dialog is showing)
            if (Input.GetKeyDown(approveKey))
            {
                if (notificationManager != null && notificationManager.CurrentApproval != null)
                {
                    notificationManager.ApproveCurrentApproval();
                }
            }

            // Deny
            if (Input.GetKeyDown(denyKey))
            {
                if (notificationManager != null && notificationManager.CurrentApproval != null)
                {
                    notificationManager.DenyCurrentApproval();
                }
            }
        }
#endif
    }
}
