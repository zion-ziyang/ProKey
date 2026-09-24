using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Control panel of Study 2 (Method A / B / C), Study 3 (Method A / B) and the demo (no methods):
/// method buttons + Practice + Start Test (+ End). Buttons that would be a mistouch in the current state are disabled.
/// Two-step layout (Study 2 / 3): step 1 shows the method buttons + End; once a method is chosen only
/// the method header, Practice, Start Test and Back stay visible. Back returns to the method buttons.
/// Demo: no method buttons, single-step, and the End button is a single-press "Back" to the study selection.
/// </summary>
public class MethodPanelBindings : MonoBehaviour
{
    [SerializeField] private StudyFlowManager studyFlowManager;
    [Tooltip("Element 0 = Method A, element 1 = Method B, ...")]
    [SerializeField] private List<Toggle> methodButtons = new List<Toggle>();
    [SerializeField] private Toggle practiceButton;
    [SerializeField] private Toggle startButton;
    [SerializeField] private Toggle endButton;
    [Tooltip("Presses are ignored for this long after the panel appears (or changes its buttons)")]
    [SerializeField] private float activationGuardSeconds = 0.4f;
    [Tooltip("End has to be pressed twice within this time (mistouch protection). 0 = a single press ends the study")]
    [SerializeField] private float endConfirmWindowSeconds = 3f;
    [SerializeField] private string endLabel = "End";
    [SerializeField] private string endConfirmLabel = "Press again to end";

    [Header("Two-step layout")]
    [Tooltip("On: choosing a method hides the method buttons and End; only the header, Practice, Start and Back stay visible. " +
             "Off: all buttons are always visible (Back and the header are hidden).")]
    [SerializeField] private bool twoStepLayout = false;
    [Tooltip("Returns from Practice / Start to the method buttons (two-step layout only)")]
    [SerializeField] private Toggle backButton;
    [Tooltip("Row that shows the chosen method above Practice / Start / Back (two-step layout only)")]
    [SerializeField] private TMP_Text methodHeader;
    [Tooltip("Two-step layout: header text while the method buttons are shown (e.g. \"Select a method\"), " +
             "so both steps have the same number of rows. Empty = no header in step 1")]
    [SerializeField] private string stepOneHeader = "";

    private const string LabelPath = "Content/Background/Elements/Text/Label";

    private readonly List<UnityAction<bool>> methodListeners = new List<UnityAction<bool>>();
    private readonly Dictionary<Toggle, TMP_Text> labels = new Dictionary<Toggle, TMP_Text>();
    private float enabledTime;
    private float endArmedTime = float.NegativeInfinity;
    private bool subscribed;
    private bool? actionStepShown; // null = layout not applied yet

    private void Awake()
    {
        if (studyFlowManager == null) studyFlowManager = FindObjectOfType<StudyFlowManager>();

        for (int i = 0; i < methodButtons.Count; i++)
        {
            int methodIndex = i;
            UnityAction<bool> listener = _ => OnMethodClicked(methodIndex);
            methodListeners.Add(listener);
            if (methodButtons[i] != null) methodButtons[i].onValueChanged.AddListener(listener);
        }

        if (practiceButton != null) practiceButton.onValueChanged.AddListener(OnPracticeClicked);
        if (startButton != null) startButton.onValueChanged.AddListener(OnStartClicked);
        if (endButton != null) endButton.onValueChanged.AddListener(OnEndClicked);
        if (backButton != null) backButton.onValueChanged.AddListener(OnBackClicked);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < methodButtons.Count && i < methodListeners.Count; i++)
        {
            if (methodButtons[i] != null) methodButtons[i].onValueChanged.RemoveListener(methodListeners[i]);
        }

        if (practiceButton != null) practiceButton.onValueChanged.RemoveListener(OnPracticeClicked);
        if (startButton != null) startButton.onValueChanged.RemoveListener(OnStartClicked);
        if (endButton != null) endButton.onValueChanged.RemoveListener(OnEndClicked);
        if (backButton != null) backButton.onValueChanged.RemoveListener(OnBackClicked);
    }

    private void OnEnable()
    {
        enabledTime = Time.unscaledTime;
        actionStepShown = null;
        DisarmEnd();
        if (studyFlowManager != null && !subscribed)
        {
            studyFlowManager.StateChanged += Refresh;
            subscribed = true;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (studyFlowManager != null && subscribed)
        {
            studyFlowManager.StateChanged -= Refresh;
            subscribed = false;
        }
    }

    private bool IsGuarded(Toggle toggle)
    {
        // The toggles are used as push buttons: every value change is a click, and the toggle never stays on
        if (toggle != null) toggle.SetIsOnWithoutNotify(false);
        return studyFlowManager == null || Time.unscaledTime - enabledTime < activationGuardSeconds;
    }

    private void OnMethodClicked(int methodIndex)
    {
        if (IsGuarded(methodButtons[methodIndex])) return;
        studyFlowManager.SelectMethod(methodIndex);
    }

    private void OnPracticeClicked(bool _)
    {
        if (IsGuarded(practiceButton)) return;
        studyFlowManager.StartPracticePhase();
    }

    private void OnStartClicked(bool _)
    {
        if (IsGuarded(startButton)) return;
        studyFlowManager.StartRealTestPhase();
    }

    private void OnBackClicked(bool _)
    {
        if (IsGuarded(backButton)) return;
        studyFlowManager.DeselectMethod();
    }

    private void OnEndClicked(bool _)
    {
        if (IsGuarded(endButton)) return;

        bool armed = Time.unscaledTime - endArmedTime <= endConfirmWindowSeconds;
        if (endConfirmWindowSeconds > 0f && !armed)
        {
            // First press only arms the button
            endArmedTime = Time.unscaledTime;
            SetLabel(endButton, endConfirmLabel);
            return;
        }

        DisarmEnd();
        studyFlowManager.BackToStudySelection();
    }

    private void DisarmEnd()
    {
        endArmedTime = float.NegativeInfinity;
        if (endButton != null) SetLabel(endButton, endLabel);
    }

    private void Update()
    {
        if (!float.IsNegativeInfinity(endArmedTime) && Time.unscaledTime - endArmedTime > endConfirmWindowSeconds)
        {
            DisarmEnd();
        }
    }

    /// <summary>
    /// Updates labels, which buttons are visible and which can be pressed from the state of the StudyFlowManager
    /// </summary>
    private void Refresh()
    {
        if (studyFlowManager == null) return;

        TestPhase phase = studyFlowManager.CurrentPhase;
        int current = studyFlowManager.CurrentMethodIndex;
        bool realTestRunning = phase == TestPhase.Real;
        // A panel without method buttons (demo) is ready as long as the running study has no methods either
        bool methodReady = methodButtons.Count == 0
            ? studyFlowManager.MethodCount == 0
            : current >= 0 && (!studyFlowManager.IsMethodCompleted(current) || studyFlowManager.AllowRepeatCompletedMethod);

        // Two-step layout: the second step is shown while a method is chosen.
        // A method whose real test has just finished returns to the method buttons.
        bool justFinished = current >= 0 && studyFlowManager.IsMethodCompleted(current) && phase == TestPhase.Idle;
        bool actionStep = twoStepLayout && current >= 0 && !justFinished;
        ApplyLayout(actionStep, current);

        for (int i = 0; i < methodButtons.Count; i++)
        {
            Toggle button = methodButtons[i];
            if (button == null) continue;

            bool completed = studyFlowManager.IsMethodCompleted(i);
            string label = $"Method {StudyFlowManager.GetMethodLabel(i)}";
            if (completed) label += " (done)";
            else if (i == current && !twoStepLayout) label += " (active)";
            SetLabel(button, label);

            // Methods cannot be switched while the real test is running.
            // Two-step layout: a finished method leads to a dead end (nothing to start), so it cannot be opened again.
            bool deadEnd = twoStepLayout && completed && !studyFlowManager.AllowRepeatCompletedMethod;
            button.interactable = !realTestRunning && !deadEnd;
            button.SetIsOnWithoutNotify(false);
        }

        if (practiceButton != null)
        {
            practiceButton.interactable = methodReady && !realTestRunning && phase != TestPhase.Practice;
            practiceButton.SetIsOnWithoutNotify(false);
        }

        if (startButton != null)
        {
            startButton.interactable = methodReady && !realTestRunning;
            startButton.SetIsOnWithoutNotify(false);
        }

        if (backButton != null)
        {
            // The real test cannot be left with Back
            backButton.interactable = !realTestRunning;
            backButton.SetIsOnWithoutNotify(false);
        }

        if (endButton != null) endButton.SetIsOnWithoutNotify(false);
    }

    /// <summary>
    /// Shows the buttons of the current step. Without the two-step layout every button except Back is visible.
    /// </summary>
    private void ApplyLayout(bool actionStep, int currentMethod)
    {
        bool showStepOneHeader = twoStepLayout && !actionStep && !string.IsNullOrEmpty(stepOneHeader);
        if (methodHeader != null)
        {
            if (actionStep) methodHeader.text = $"Method {StudyFlowManager.GetMethodLabel(currentMethod)}";
            else if (showStepOneHeader) methodHeader.text = stepOneHeader;
        }

        if (actionStepShown.HasValue && actionStepShown.Value == actionStep) return;
        bool firstLayout = !actionStepShown.HasValue;
        actionStepShown = actionStep;

        bool showMethods = !twoStepLayout || !actionStep;
        bool showActions = !twoStepLayout || actionStep;

        foreach (Toggle button in methodButtons) SetVisible(button, showMethods);
        SetVisible(endButton, showMethods);
        SetVisible(practiceButton, showActions);
        SetVisible(startButton, showActions);
        SetVisible(backButton, twoStepLayout && actionStep);
        if (methodHeader != null) methodHeader.gameObject.SetActive((twoStepLayout && actionStep) || showStepOneHeader);

        if (firstLayout) return;

        // The buttons under the finger have just been replaced: ignore presses for a moment, and never keep End armed
        enabledTime = Time.unscaledTime;
        DisarmEnd();

        // A hidden button must not stay the selected UI element, otherwise it reappears highlighted
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(transform)) eventSystem.SetSelectedGameObject(null);

        RectTransform column = practiceButton != null ? practiceButton.transform.parent as RectTransform : null;
        if (column != null) LayoutRebuilder.MarkLayoutForRebuild(column);
    }

    private static void SetVisible(Toggle button, bool visible)
    {
        if (button != null && button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
    }

    private void SetLabel(Toggle button, string text)
    {
        if (!labels.TryGetValue(button, out TMP_Text label))
        {
            Transform labelTransform = button.transform.Find(LabelPath);
            label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : button.GetComponentInChildren<TMP_Text>(true);
            labels[button] = label;
        }

        if (label != null) label.text = text;
    }
}
