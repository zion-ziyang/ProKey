using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Study 1 panels: the controller panel and the two adjust panels of the anchor exploration.
///
/// Study 1 Pipeline:
/// - Explore 1 button → Hand anchoring (HandAdjustPanel: Confirm / Configure again, finger mode)
/// - Explore 2 button → Head anchoring (HeadAdjustPanel: distance / height / tilt sliders, finger mode)
///
/// Closing an adjust panel brings the controller panel back.
///
/// Controller panel (two steps, like the method panels of Study 2 / 3):
/// - Step 1: header "Select an option" + Explore 1 + Explore 2 + End
/// - Step 2 (an option is chosen): header with the option + Practice + Start Test + Back
/// The chosen option is kept by the StudyFlowManager (SelectStudyOption / DeselectStudyOption).
///
/// Attach to KeyboardSystem or a manager object.
/// </summary>
public class PanelController : MonoBehaviour
{
    [Header("Adjust Panels")]
    [Tooltip("Hand anchoring panel of Explore 1")]
    [FormerlySerializedAs("handAdjustPanel1")]
    [SerializeField] private GameObject handAdjustPanel;

    [Tooltip("Head anchoring panel of Explore 2")]
    [FormerlySerializedAs("headAdjustPanel1")]
    [SerializeField] private GameObject headAdjustPanel;

    [Tooltip("Controller panel with Start/End/Explore buttons")]
    [SerializeField] private GameObject controllerPanel;

    [Header("HandAdjustPanel Buttons")]
    [Tooltip("Confirm / Configure again button of the hand exploration")]
    [FormerlySerializedAs("adjustHandButton1")]
    [SerializeField] private Toggle adjustHandButton;
    [Tooltip("Label text of adjustHandButton")]
    [FormerlySerializedAs("adjustHandButton1Label")]
    [SerializeField] private TMP_Text adjustHandButtonLabel;

    [Header("Adjust Button Label Settings")]
    [Tooltip("Label while the keyboard is world-anchored and waits for the participant to place the holding hand")]
    [SerializeField] private string handConfirmLabel = "Confirm";
    [Tooltip("Label after a trial was confirmed (keyboard follows the hand)")]
    [SerializeField] private string handConfigureAgainLabel = "Configure again";
    [Tooltip("Place the keyboard in front of the head when a hand exploration starts. Off: it freezes where it currently is")]
    [SerializeField] private bool placeKeyboardInFrontOnHandExplore = true;

    [Tooltip("Button to close the hand adjust panel (Explore 1)")]
    [FormerlySerializedAs("closeHandAdjustButton1")]
    [SerializeField] private Toggle closeHandAdjustButton;

    [Header("HeadAdjustPanel Buttons")]
    [Tooltip("Button to close the head adjust panel (Explore 2)")]
    [FormerlySerializedAs("closeHeadAdjustButton1")]
    [SerializeField] private Toggle closeHeadAdjustButton;

    [Header("Controller Panel Buttons")]
    [Tooltip("Button to start practice phase")]
    [SerializeField] private Toggle practiceButton;

    [Tooltip("Button to start real test phase")]
    [SerializeField] private Toggle startButton;

    [Tooltip("Button to end study and return to selection")]
    [SerializeField] private Toggle endButton;

    [Tooltip("Explore 1 button - switches to Hand Anchoring and shows the hand adjust panel")]
    [SerializeField] private Toggle explore1Button;

    [Tooltip("Explore 2 button - switches to Head Anchoring and shows the head adjust panel")]
    [SerializeField] private Toggle explore2Button;

    [Tooltip("Returns from Practice / Start Test to the Explore buttons")]
    [SerializeField] private Toggle backButton;

    [Tooltip("Header row of the controller panel: 'Select an option', then the chosen option")]
    [SerializeField] private TMP_Text optionHeader;

    [Header("Controller Panel Behaviour")]
    [SerializeField] private string stepOneHeaderText = "Select an option";
    [Tooltip("Presses are ignored for this long after the controller panel appears (or changes its buttons)")]
    [SerializeField] private float activationGuardSeconds = 0.4f;
    [Tooltip("End has to be pressed twice within this time (mistouch protection). 0 = a single press ends the study")]
    [SerializeField] private float endConfirmWindowSeconds = 3f;
    [SerializeField] private string endLabel = "End";
    [SerializeField] private string endConfirmLabel = "Press again to end";

    [Header("Controller Reference")]
    [SerializeField] private KeyboardController keyboardController;
    [SerializeField] private StudyFlowManager studyFlowManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindComponents = true;
    [Tooltip("Name of explore 1 button in ControllerPanel")]
    [SerializeField] private string explore1ButtonName = "Explore_1_button";
    [Tooltip("Name of explore 2 button in ControllerPanel")]
    [SerializeField] private string explore2ButtonName = "Explore_2_button";

    [Header("Background References")]
    [SerializeField] private GameObject previewBackgroundS;
    [SerializeField] private GameObject previewBackgroundL;
    [SerializeField] private GameObject textboardBackgroundS;
    [SerializeField] private GameObject textboardBackgroundL;

    // Flag to track if backgrounds have been initialized for this instance
    private bool backgroundsInitialized = false;

    // Track if hand adjust mode is active (for label toggling)
    // true: keyboard is world-anchored and waits for "Confirm". false: keyboard follows the hand ("Configure again")
    private bool isHandAdjustModeActive = false;
    private int handExploreTrialCount = 0;

    // Controller panel (two-step layout)
    private const string ButtonLabelPath = "Content/Background/Elements/Text/Label";
    private float controllerPanelShownTime;
    private float endArmedTime = float.NegativeInfinity;
    private bool? actionStepShown; // null = layout not applied yet
    private bool subscribedToStudyFlow;

    private void Awake()
    {
        ResolveRequiredReferences();
        if (autoFindComponents)
        {
            AutoFindComponents();
        }
    }

    private void ResolveRequiredReferences()
    {
        if (keyboardController == null)
        {
            keyboardController = GetComponent<KeyboardController>();
            if (keyboardController == null)
            {
                keyboardController = GetComponentInParent<KeyboardController>();
            }
            if (keyboardController == null)
            {
                keyboardController = FindFirstObjectByType<KeyboardController>();
            }
        }

        if (studyFlowManager == null)
        {
            studyFlowManager = FindFirstObjectByType<StudyFlowManager>();
        }
    }

    /// <summary>
    /// Auto-finds button and panel references if not assigned in Inspector
    /// </summary>
    private void AutoFindComponents()
    {
        // Find ControllerPanel if not assigned
        if (controllerPanel == null)
        {
            Transform panel = FindChildByName(transform, "ControllerPanel_study_1");
            if (panel != null) controllerPanel = panel.gameObject;
        }

        // Find the buttons in ControllerPanel
        if (controllerPanel != null)
        {
            if (explore1Button == null) explore1Button = FindToggle(controllerPanel.transform, explore1ButtonName);
            if (explore2Button == null) explore2Button = FindToggle(controllerPanel.transform, explore2ButtonName);
            if (practiceButton == null) practiceButton = FindToggle(controllerPanel.transform, "Practice_button");
            if (startButton == null) startButton = FindToggle(controllerPanel.transform, "Start_button");
            if (endButton == null) endButton = FindToggle(controllerPanel.transform, "End_button");
            if (backButton == null) backButton = FindToggle(controllerPanel.transform, "Back_button");

            if (optionHeader == null)
            {
                Transform header = FindChildByName(controllerPanel.transform, "OptionHeader");
                if (header != null) optionHeader = header.GetComponentInChildren<TMP_Text>(true);
            }
        }

        // Find adjust panels
        if (handAdjustPanel == null)
        {
            Transform panel = FindChildByName(transform, "HandAdjustPanel");
            if (panel != null) handAdjustPanel = panel.gameObject;
        }

        if (headAdjustPanel == null)
        {
            Transform panel = FindChildByName(transform, "HeadAdjustPanel");
            if (panel != null) headAdjustPanel = panel.gameObject;
        }

        // Find the buttons in HandAdjustPanel
        if (handAdjustPanel != null)
        {
            if (adjustHandButton == null) adjustHandButton = FindToggle(handAdjustPanel.transform, "adjust_hand_button");
            if (closeHandAdjustButton == null) closeHandAdjustButton = FindToggle(handAdjustPanel.transform, "close_hand_adjust_button");
        }

        // Find the buttons in HeadAdjustPanel
        if (headAdjustPanel != null)
        {
            if (closeHeadAdjustButton == null) closeHeadAdjustButton = FindToggle(headAdjustPanel.transform, "close_head_adjust_button");
        }

        Debug.Log($"[PanelController] AutoFind Results:\n" +
                  $"  ControllerPanel: {controllerPanel != null}, explore1: {explore1Button != null}, explore2: {explore2Button != null}\n" +
                  $"  HandAdjustPanel: {handAdjustPanel != null}, adjust: {adjustHandButton != null}, close: {closeHandAdjustButton != null}\n" +
                  $"  HeadAdjustPanel: {headAdjustPanel != null}, close: {closeHeadAdjustButton != null}");
    }

    /// <summary>
    /// Finds a child transform by name recursively
    /// </summary>
    private Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }
        return null;
    }

    private Toggle FindToggle(Transform parent, string name)
    {
        Transform child = FindChildByName(parent, name);
        return child != null ? child.GetComponent<Toggle>() : null;
    }

    private void OnEnable()
    {
        // KeyboardSystem (this object) is hidden between studies: follow the study state only while it is visible
        if (studyFlowManager != null && !subscribedToStudyFlow)
        {
            studyFlowManager.StateChanged += RefreshControllerPanel;
            subscribedToStudyFlow = true;
        }
    }

    private void OnDisable()
    {
        if (studyFlowManager != null && subscribedToStudyFlow)
        {
            studyFlowManager.StateChanged -= RefreshControllerPanel;
            subscribedToStudyFlow = false;
        }
    }

    private void Update()
    {
        if (!float.IsNegativeInfinity(endArmedTime) && Time.unscaledTime - endArmedTime > endConfirmWindowSeconds)
        {
            DisarmEnd();
        }
    }

    private void Start()
    {
        // Initialize to hand anchor mode on launch
        InitializePanels();
        SetupButtonListeners();

        Debug.Log($"[PanelController] Start completed - explore1Button={explore1Button != null}, explore2Button={explore2Button != null}");
    }


    private void InitializePanels()
    {
        // On launch: hide the adjust panels - the Explore buttons show them
        HideAdjustPanels();
        ShowControllerPanel(true);

        // Reset all toggles to OFF state so they work on first click
        ResetAllPanelToggles();

        // Initialize backgrounds to Small (default) on launch
        if (!backgroundsInitialized)
        {
            SetBackgroundSize(BackgroundSize.Small);
            backgroundsInitialized = true;
        }

        // Ensure keyboard is in hand anchor mode
        if (keyboardController != null)
        {
            keyboardController.SetAnchorMode(AnchorMode.Hand);
            Debug.Log("[PanelController] Initialized with Hand Anchor mode (panels hidden)");
        }
    }

    /// <summary>
    /// Resets all toggle buttons in all panels to OFF state
    /// </summary>
    private void ResetAllPanelToggles()
    {
        // Controller panel buttons - ensure they're interactable and reset to OFF
        ResetControllerToggle(practiceButton);
        ResetControllerToggle(startButton);
        ResetControllerToggle(endButton);
        ResetControllerToggle(explore1Button);
        ResetControllerToggle(explore2Button);
        ResetControllerToggle(backButton);

        // Adjust panel buttons
        if (adjustHandButton != null) adjustHandButton.SetIsOnWithoutNotify(false);
        if (closeHandAdjustButton != null) closeHandAdjustButton.SetIsOnWithoutNotify(false);
        if (closeHeadAdjustButton != null) closeHeadAdjustButton.SetIsOnWithoutNotify(false);

        Debug.Log("[PanelController] All panel toggles reset to OFF state");
    }

    private static void ResetControllerToggle(Toggle toggle)
    {
        if (toggle == null) return;
        toggle.interactable = true;
        toggle.SetIsOnWithoutNotify(false);
    }

    private enum BackgroundSize { Small, Large }

    private void SetBackgroundSize(BackgroundSize size)
    {
        // Update Preview backgrounds
        if (previewBackgroundS != null) previewBackgroundS.SetActive(size == BackgroundSize.Small);
        if (previewBackgroundL != null) previewBackgroundL.SetActive(size == BackgroundSize.Large);

        // Update Textboard backgrounds
        if (textboardBackgroundS != null) textboardBackgroundS.SetActive(size == BackgroundSize.Small);
        if (textboardBackgroundL != null) textboardBackgroundL.SetActive(size == BackgroundSize.Large);
    }

    private void SetupButtonListeners()
    {
        // Controller Panel buttons
        if (practiceButton != null) practiceButton.onValueChanged.AddListener(OnPracticeButtonClicked);
        if (startButton != null) startButton.onValueChanged.AddListener(OnStartButtonClicked);
        if (endButton != null) endButton.onValueChanged.AddListener(OnEndButtonClicked);
        if (explore1Button != null) explore1Button.onValueChanged.AddListener(OnExplore1ButtonClicked);
        if (explore2Button != null) explore2Button.onValueChanged.AddListener(OnExplore2ButtonClicked);
        if (backButton != null) backButton.onValueChanged.AddListener(OnBackButtonClicked);

        // Adjust panel buttons
        if (adjustHandButton != null) adjustHandButton.onValueChanged.AddListener(OnAdjustHandButtonClicked);
        if (closeHandAdjustButton != null) closeHandAdjustButton.onValueChanged.AddListener(OnCloseAdjustButtonClicked);
        if (closeHeadAdjustButton != null) closeHeadAdjustButton.onValueChanged.AddListener(OnCloseAdjustButtonClicked);
    }

    private void OnDestroy()
    {
        // Remove listeners
        if (practiceButton != null) practiceButton.onValueChanged.RemoveListener(OnPracticeButtonClicked);
        if (startButton != null) startButton.onValueChanged.RemoveListener(OnStartButtonClicked);
        if (endButton != null) endButton.onValueChanged.RemoveListener(OnEndButtonClicked);
        if (explore1Button != null) explore1Button.onValueChanged.RemoveListener(OnExplore1ButtonClicked);
        if (explore2Button != null) explore2Button.onValueChanged.RemoveListener(OnExplore2ButtonClicked);
        if (backButton != null) backButton.onValueChanged.RemoveListener(OnBackButtonClicked);
        if (adjustHandButton != null) adjustHandButton.onValueChanged.RemoveListener(OnAdjustHandButtonClicked);
        if (closeHandAdjustButton != null) closeHandAdjustButton.onValueChanged.RemoveListener(OnCloseAdjustButtonClicked);
        if (closeHeadAdjustButton != null) closeHeadAdjustButton.onValueChanged.RemoveListener(OnCloseAdjustButtonClicked);
    }

    /// <summary>
    /// The controller panel toggles are used as push buttons: every press arrives as isOn = true and the toggle never stays on.
    /// Returns false for presses that have to be ignored (right after the panel appeared or changed its buttons).
    /// </summary>
    private bool AcceptControllerPress(Toggle toggle, bool isOn)
    {
        if (toggle != null) toggle.SetIsOnWithoutNotify(false);
        if (!isOn) return false;
        return Time.unscaledTime - controllerPanelShownTime >= activationGuardSeconds;
    }

    /// <summary>
    /// Called when Practice button is clicked - starts the Practice phase
    /// </summary>
    private void OnPracticeButtonClicked(bool isOn)
    {
        if (!AcceptControllerPress(practiceButton, isOn)) return;

        HideAdjustPanels();

        if (studyFlowManager != null)
        {
            studyFlowManager.StartPracticePhase();
            Debug.Log("[PanelController] Practice button clicked - StartPracticePhase called");
        }
        else
        {
            Debug.LogWarning("[PanelController] StudyFlowManager not found!");
        }
    }

    private void OnStartButtonClicked(bool isOn)
    {
        if (!AcceptControllerPress(startButton, isOn)) return;

        HideAdjustPanels();
        // Note: Keeping ControllerPanel visible when Start is clicked (not hiding it)

        if (studyFlowManager != null)
        {
            studyFlowManager.StartRealTestPhase();
            Debug.Log("[PanelController] Start button clicked - StartRealTestPhase called");
        }
        else
        {
            Debug.LogWarning("[PanelController] StudyFlowManager not found!");
        }
    }

    private void OnEndButtonClicked(bool isOn)
    {
        if (!AcceptControllerPress(endButton, isOn)) return;

        bool armed = Time.unscaledTime - endArmedTime <= endConfirmWindowSeconds;
        if (endConfirmWindowSeconds > 0f && !armed)
        {
            // First press only arms the button
            endArmedTime = Time.unscaledTime;
            SetButtonLabel(endButton, endConfirmLabel);
            return;
        }

        DisarmEnd();

        if (studyFlowManager != null)
        {
            // Hides KeyboardSystem (this object): nothing that needs an active object (coroutines) may follow
            studyFlowManager.BackToStudySelection();
            Debug.Log("[PanelController] End button clicked - BackToStudySelection called");
        }
        else
        {
            Debug.LogWarning("[PanelController] StudyFlowManager not found!");
        }
    }

    /// <summary>
    /// "Back" of step 2: returns to the Explore buttons. Not possible while the real test is running.
    /// </summary>
    private void OnBackButtonClicked(bool isOn)
    {
        if (!AcceptControllerPress(backButton, isOn)) return;

        if (studyFlowManager != null) studyFlowManager.DeselectStudyOption();
    }

    private void DisarmEnd()
    {
        endArmedTime = float.NegativeInfinity;
        if (endButton != null) SetButtonLabel(endButton, endLabel);
    }

    /// <summary>
    /// Called when Explore 1 button is clicked - switches to Hand Anchoring and shows the HandAdjustPanel
    /// </summary>
    private void OnExplore1ButtonClicked(bool isOn)
    {
        Debug.Log($"[PanelController] OnExplore1ButtonClicked called with isOn={isOn}");
        if (!AcceptControllerPress(explore1Button, isOn)) return;

        // Option 1 of Study 1: Practice / Start Test appear once the hand panel is closed
        if (studyFlowManager != null && !studyFlowManager.SelectStudyOption(1)) return;

        // Switch to hand anchoring mode
        if (keyboardController != null)
        {
            keyboardController.SetAnchorMode(AnchorMode.Hand);
            Debug.Log("[PanelController] Set anchor mode to Hand");
        }
        else
        {
            Debug.LogWarning("[PanelController] keyboardController is null!");
        }

        OpenAdjustPanel(handAdjustPanel);

        // A hand exploration trial starts world-anchored: the participant places the holding hand, then presses "Confirm"
        BeginHandSetup(placeKeyboardInFrontOnHandExplore);

        Debug.Log("[PanelController] Explore 1 clicked - Hand anchoring");
    }

    /// <summary>
    /// Called when Explore 2 button is clicked - switches to Head Anchoring and shows the HeadAdjustPanel
    /// </summary>
    private void OnExplore2ButtonClicked(bool isOn)
    {
        if (!AcceptControllerPress(explore2Button, isOn)) return;

        // Option 2 of Study 1: Practice / Start Test appear once the head panel is closed
        if (studyFlowManager != null && !studyFlowManager.SelectStudyOption(2)) return;

        // Switch to head anchoring mode
        if (keyboardController != null)
        {
            keyboardController.SetAnchorMode(AnchorMode.Head);
            if (!keyboardController.IsFollowing) keyboardController.SetFollow(true);
        }

        OpenAdjustPanel(headAdjustPanel);

        Debug.Log("[PanelController] Explore 2 clicked - Head anchoring");
    }

    /// <summary>
    /// Two-step layout of the controller panel, driven by the option stored in the StudyFlowManager:
    /// no option -> header + Explore 1 + Explore 2 + End, option chosen -> header + Practice + Start Test + Back.
    /// Buttons that would be a mistouch in the current state are disabled.
    /// </summary>
    private void RefreshControllerPanel()
    {
        if (studyFlowManager == null) return;

        TestPhase phase = studyFlowManager.CurrentPhase;
        int option = studyFlowManager.CurrentStudyOption;
        bool realTestRunning = phase == TestPhase.Real;
        bool actionStep = option > 0;

        if (optionHeader != null)
        {
            optionHeader.text = actionStep ? GetOptionTitle(option) : stepOneHeaderText;
        }

        if (!actionStepShown.HasValue || actionStepShown.Value != actionStep)
        {
            bool firstLayout = !actionStepShown.HasValue;
            actionStepShown = actionStep;

            SetButtonVisible(explore1Button, !actionStep);
            SetButtonVisible(explore2Button, !actionStep);
            SetButtonVisible(endButton, !actionStep);
            SetButtonVisible(practiceButton, actionStep);
            SetButtonVisible(startButton, actionStep);
            SetButtonVisible(backButton, actionStep);

            if (!firstLayout)
            {
                // The buttons under the finger have just been replaced: ignore presses for a moment, and never keep End armed
                controllerPanelShownTime = Time.unscaledTime;
                DisarmEnd();

                // A hidden button must not stay the selected UI element, otherwise it reappears highlighted
                EventSystem eventSystem = EventSystem.current;
                GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
                if (selected != null && controllerPanel != null && selected.transform.IsChildOf(controllerPanel.transform))
                {
                    eventSystem.SetSelectedGameObject(null);
                }

                RectTransform column = practiceButton != null ? practiceButton.transform.parent as RectTransform : null;
                if (column != null) LayoutRebuilder.MarkLayoutForRebuild(column);
            }
        }

        if (practiceButton != null) practiceButton.interactable = actionStep && !realTestRunning && phase != TestPhase.Practice;
        if (startButton != null) startButton.interactable = actionStep && !realTestRunning;
        // The real test cannot be left with Back
        if (backButton != null) backButton.interactable = !realTestRunning;
    }

    private string GetOptionTitle(int option)
    {
        Toggle button = option == 1 ? explore1Button : (option == 2 ? explore2Button : null);
        TMP_Text label = GetButtonLabel(button);
        return label != null && !string.IsNullOrEmpty(label.text) ? label.text : $"Explore {option}";
    }

    private static void SetButtonVisible(Toggle button, bool visible)
    {
        if (button != null && button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
    }

    private static TMP_Text GetButtonLabel(Toggle button)
    {
        if (button == null) return null;
        Transform labelTransform = button.transform.Find(ButtonLabelPath);
        return labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : button.GetComponentInChildren<TMP_Text>(true);
    }

    private static void SetButtonLabel(Toggle button, string text)
    {
        TMP_Text label = GetButtonLabel(button);
        if (label != null) label.text = text;
    }

    /// <summary>
    /// Shows one adjust panel instead of the controller panel, with its toggles reset
    /// </summary>
    private void OpenAdjustPanel(GameObject panel)
    {
        HideAdjustPanels();
        ShowControllerPanel(false);

        if (adjustHandButton != null) adjustHandButton.SetIsOnWithoutNotify(false);
        if (closeHandAdjustButton != null) closeHandAdjustButton.SetIsOnWithoutNotify(false);
        if (closeHeadAdjustButton != null) closeHeadAdjustButton.SetIsOnWithoutNotify(false);

        if (panel != null)
        {
            panel.SetActive(true);
            Debug.Log($"[PanelController] Showing {panel.name}");
        }
        else
        {
            Debug.LogError("[PanelController] adjust panel is NULL - cannot show panel!");
        }
    }

    /// <summary>
    /// Starts a hand exploration trial: the keyboard is anchored in the world and the button shows "Confirm"
    /// </summary>
    private void BeginHandSetup(bool placeInFrontOfHead)
    {
        if (keyboardController != null)
        {
            keyboardController.BeginWorldAnchoredHandSetup(placeInFrontOfHead);
        }

        isHandAdjustModeActive = true;
        UpdateAdjustHandButtonLabel();
    }

    /// <summary>
    /// Called when the Confirm / Configure again button is clicked.
    /// "Confirm": computes the keyboard pose relative to the holding hand and anchors the keyboard to that hand (one exploration trial).
    /// "Configure again": anchors the keyboard in the world again, where it currently is.
    /// </summary>
    private void OnAdjustHandButtonClicked(bool isOn)
    {
        // Only react when toggle turns ON (button pressed)
        if (!isOn) return;

        if (isHandAdjustModeActive)
        {
            bool confirmed = keyboardController != null && keyboardController.ConfirmHandAnchorFromCurrentPose();
            if (confirmed)
            {
                handExploreTrialCount++;
                DataLogger.Instance.Log("HAND_EXPLORE_TRIAL", $"{handExploreTrialCount}", "Confirmed");
                isHandAdjustModeActive = false;
                UpdateAdjustHandButtonLabel();
            }
            else
            {
                // Hand not tracked / too far away: stay world-anchored and keep showing "Confirm"
                Debug.Log("Confirm failed: holding hand not found");
            }
        }
        else
        {
            DataLogger.Instance.Log("HAND_EXPLORE_TRIAL", $"{handExploreTrialCount + 1}", "ConfigureAgain");
            BeginHandSetup(false);
        }

        Debug.Log($"[PanelController] Confirm / Configure again clicked - waiting for confirm: {isHandAdjustModeActive}, trials: {handExploreTrialCount}");

        if (adjustHandButton != null) StartCoroutine(ResetToggleNextFrame(adjustHandButton));
    }

    /// <summary>
    /// Updates the adjust hand button label based on current mode
    /// </summary>
    private void UpdateAdjustHandButtonLabel()
    {
        string newLabel = isHandAdjustModeActive ? handConfirmLabel : handConfigureAgainLabel;

        if (adjustHandButtonLabel != null)
        {
            adjustHandButtonLabel.text = newLabel;
        }
        else
        {
            // Try to find the label in children if not assigned
            TMP_Text label = adjustHandButton?.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = newLabel;
        }
    }

    private void OnCloseAdjustButtonClicked(bool isOn)
    {
        if (!isOn) return;

        HideAdjustPanels();
        ShowControllerPanel(true);

        // Reset adjust mode state and labels when closing panels
        ResetAdjustHandMode();

        // Closed without "Confirm": follow the hand again with the last confirmed offset.
        // Nothing confirmed yet (start of Study 1): the keyboard stays where it is, in world coordinates.
        if (keyboardController != null && keyboardController.CurrentAnchorMode == AnchorMode.Hand
            && !keyboardController.IsFollowing && keyboardController.HasCustomHandOffset)
        {
            keyboardController.SetFollow(true);
        }

        // Reset explore button toggles so they can be clicked again
        if (explore1Button != null) explore1Button.SetIsOnWithoutNotify(false);
        if (explore2Button != null) explore2Button.SetIsOnWithoutNotify(false);

        // Reset both close button toggles (only one is visible at a time)
        if (closeHandAdjustButton != null) StartCoroutine(ResetToggleNextFrame(closeHandAdjustButton));
        if (closeHeadAdjustButton != null) StartCoroutine(ResetToggleNextFrame(closeHeadAdjustButton));
    }

    /// <summary>
    /// Resets the hand adjust mode state and updates button labels to default
    /// </summary>
    private void ResetAdjustHandMode()
    {
        isHandAdjustModeActive = false;
        UpdateAdjustHandButtonLabel();
    }

    /// <summary>
    /// Resets a toggle to OFF state after one frame
    /// This allows button-like behavior with toggles
    /// </summary>
    private System.Collections.IEnumerator ResetToggleNextFrame(Toggle toggle)
    {
        yield return null; // Wait one frame
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(false);
        }
    }

    private void HideAdjustPanels()
    {
        if (handAdjustPanel != null) handAdjustPanel.SetActive(false);
        if (headAdjustPanel != null) headAdjustPanel.SetActive(false);
    }

    private void ShowControllerPanel(bool visible)
    {
        if (controllerPanel != null) controllerPanel.SetActive(visible);
        if (!visible) return;

        // The panel has just appeared under the finger: ignore presses for a moment, and never keep End armed
        controllerPanelShownTime = Time.unscaledTime;
        DisarmEnd();
        RefreshControllerPanel();
    }

    /// <summary>
    /// Reinitializes the panel controller for a new study session.
    /// Call this when entering Study 1 to ensure all toggles are properly reset.
    /// </summary>
    public void ReinitializeForStudy()
    {
        Debug.Log("[PanelController] ReinitializeForStudy called");

        // Reset state
        handExploreTrialCount = 0;
        ResetAdjustHandMode();

        // Hide all adjust panels
        HideAdjustPanels();

        // Reset all toggles to OFF state
        ResetAllPanelToggles();

        // Controller panel: back to the option buttons, with the activation guard of a panel that has just appeared
        actionStepShown = null;
        controllerPanelShownTime = Time.unscaledTime;
        DisarmEnd();
        RefreshControllerPanel();

        // Ensure keyboard is in hand anchor mode
        if (keyboardController != null)
        {
            keyboardController.SetAnchorMode(AnchorMode.Hand);
        }

        Debug.Log("[PanelController] Reinitialized for new study session");
    }
}
