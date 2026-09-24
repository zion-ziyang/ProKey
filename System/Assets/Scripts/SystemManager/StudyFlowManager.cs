using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Collections;


public class StudyFlowManager : MonoBehaviour
{
    [Header("Participant ID")]
    [SerializeField] private string participantId = "P01";

    [Header("Module References")]
    [SerializeField] private KeyboardController keyboardController;
    [SerializeField] private CustomInputDisplay customInputDisplay;
    [SerializeField] private PredictionManager predictionManager;

    [Header("UI Elements")]
    [SerializeField] private TMP_Text targetTextField; // Target phrase display
    [SerializeField] private TMP_Text hintTextField;   // Hint info (e.g., "5 trials left")
    [SerializeField] private GameObject daySelectionPanel;
    [SerializeField] private GameObject studySelectionPanel;
    [Tooltip("Dominant hand selection (Left / Right): first step, the hand is kept until it is changed with the hand button of the study panel")]
    [SerializeField] private GameObject handSelectionPanel;
    [Tooltip("Label of the 'change hand' button on the study selection panel; shows the chosen dominant hand")]
    [SerializeField] private TMP_Text studyPanelHandLabel;
    [Header("Finger Tips")]
    [SerializeField] private List<GameObject> leftFingerTips;
    [SerializeField] private List<GameObject> rightFingerTips;
    [Tooltip("Index fingertip objects of the LEFT hand (trigger / collider / poker)")]
    [SerializeField] private List<GameObject> leftIndexTips;
    [Tooltip("Index fingertip objects of the RIGHT hand (trigger / collider / poker)")]
    [SerializeField] private List<GameObject> rightIndexTips;
    [Tooltip("Hand-anchored keyboard: only the dominant hand's fingertips are enabled (the other hand holds the keyboard). " +
             "Head-anchored keyboard: both hands can type. Disable to always enable both hands.")]
    [SerializeField] private bool restrictTypingToDominantHand = true;

    [Header("Backgrounds (Preview + Textboard)")]
    [SerializeField] private List<GameObject> smallBackgroundPanels;
    [SerializeField] private List<GameObject> largeBackgroundPanels;
    
    [Header("Control Panels")]
    [SerializeField] private GameObject controllerPanelStudy2;
    [SerializeField] private GameObject controllerPanelStudy1;
    [SerializeField] private GameObject controllerPanelStudy3;
    [Tooltip("Demo: Practice / Start Test / Back only")]
    [SerializeField] private GameObject controllerPanelDemo;
    [SerializeField] private PanelController panelController;
    [Tooltip("Uniform gap between neighbouring elements of the keyboard plane (metres): keyboard / preview / textboard / control panel. " +
             "At runtime the control panel is placed beside the active keyboard with this gap, whenever the keyboard or the dominant hand changes. " +
             "The keyboard, the preview and the textboard are laid out in the scene with the same gap.")]
    [SerializeField] private float layoutGap = 0.006f;
    [Tooltip("On: left-dominant participants (keyboard on the right hand) get the control panel on the LEFT of the keyboard, " +
             "so it does not end up under the hand that holds the keyboard. Off: always on the right.")]
    [SerializeField] private bool mirrorControlPanelForLeftDominant = true;

    [Header("Handedness")]
    [Tooltip("Panels that follow the keyboard-holding hand (e.g. HeadAdjustPanel)")]
    [SerializeField] private List<SizePanelHandFollower> handFollowers;

    [Header("Study Methods (index into KeyboardController.childKeyboards)")]
    [Tooltip("Study 1 (explore anchor): PokeKey")]
    [SerializeField] private int study1KeyboardIndex = 0;
    [Tooltip("Study 2 (compensation methods): Method A = PokeKey, Method B = PokeKey_Gap_L, Method C = PokeKey_Size_L")]
    [SerializeField] private int[] study2MethodKeyboards = { 0, 2, 1 };
    [Tooltip("Study 3: Method A = PokeKey, Method B = ProKey")]
    [SerializeField] private int[] study3MethodKeyboards = { 0, 3 };
    [Tooltip("Keyboards that need the Large backgrounds (the 'L' variants)")]
    [SerializeField] private int[] largeBackgroundKeyboards = { 1, 2 };
    [Tooltip("Allow Practice / Start again for a method whose blocks are all finished")]
    [SerializeField] private bool allowRepeatCompletedMethod = false;
    [Tooltip("Number of days in Study 3")]
    [SerializeField] private int studyDayCount = 3;
    [Tooltip("Demo: keyboard that is shown (ProKey, as Study 3 Method B)")]
    [SerializeField] private int demoKeyboardIndex = 3;

    // State variables
    private TestPhase currentPhase = TestPhase.Idle;
    private Queue<string> currentPhraseQueue;
    private string currentTargetPhrase;
    private int currentKeyboardIndex = -1;
    private int currentDay = 0; // 1, 2, or 3
    private int currentStudyNumber = 0; // 1, 2, 3 or DemoStudyNumber

    /// <summary>Internal study number of the demo (ProKey with the Study 3 pose and trial counts, logged as "DEMO")</summary>
    public const int DemoStudyNumber = 4;

    // Selection flow: Dominant hand (once) -> Study / Demo -> (Study 3 only: Day) -> study starts
    private enum SelectionStep { None, Study, Day, Hand }
    private int pendingStudyNumber = 0;
    private bool studyRunning = false;
    private bool handSelected = false;
    private DominantHand dominantHand = DominantHand.Right;

    // Method state (Study 2 and 3): a method is finished once all its blocks (practice + test) are done
    private int currentMethodIndex = -1;
    private readonly HashSet<int> completedMethods = new HashSet<int>();
    private readonly Dictionary<int, int> completedBlocks = new Dictionary<int, int>(); // method index -> finished test blocks

    // Option state (Study 1): 0 = none, 1 = Explore 1 (hand anchor), 2 = Explore 2 (head anchor)
    private int currentStudyOption = 0;

    /// <summary>Raised when the phase, the selected method or the running study changes (used by the control panels)</summary>
    public event System.Action StateChanged;

    public TestPhase CurrentPhase { get { return currentPhase; } }
    public int CurrentMethodIndex { get { return currentMethodIndex; } }
    public int MethodCount { get { int[] m = GetMethodKeyboards(currentStudyNumber); return m != null ? m.Length : 0; } }
    public bool IsMethodCompleted(int methodIndex) { return completedMethods.Contains(methodIndex); }
    public bool AllowRepeatCompletedMethod { get { return allowRepeatCompletedMethod; } }
    public bool AllMethodsCompleted { get { return MethodCount > 0 && completedMethods.Count >= MethodCount; } }
    /// <summary>Blocks (practice + test) per method in the running study; 1 when the study has no methods</summary>
    public int BlockCount { get { return MethodCount > 0 ? Mathf.Max(1, GetCurrentStudyConfig().blocksPerMethod) : 1; } }
    /// <summary>1-based number of the block the current method is in (the next block to be tested)</summary>
    public int CurrentBlock { get { return Mathf.Min(GetCompletedBlocks(currentMethodIndex) + 1, BlockCount); } }
    /// <summary>Study 1: 0 = no option chosen yet, 1 = Explore 1 (hand anchor), 2 = Explore 2 (head anchor)</summary>
    public int CurrentStudyOption { get { return currentStudyOption; } }

    [System.Serializable]
    public class StudyConfig
    {
        [Header("Trial Count Configuration")]
        [Tooltip("Phrases per practice phase (Study 3: taken from the global set, so the day set is left to the test blocks)")]
        public int practiceTrials = 3;
        [Tooltip("Phrases per test block")]
        public int realTestTrials = 5;
        [Tooltip("Blocks (practice + test) per method; a method is finished after this many test blocks. Ignored in Study 1 and the demo")]
        public int blocksPerMethod = 1;
    }

    [Header("Study Configuration")]
    [Tooltip("Per anchor option: 1 practice block (3) + 1 test block (5)")]
    [SerializeField] private StudyConfig study1Config = new StudyConfig { practiceTrials = 3, realTestTrials = 5, blocksPerMethod = 1 };
    [Tooltip("Per method: 2 blocks x (3 practice + 5 test)")]
    [SerializeField] private StudyConfig study2Config = new StudyConfig { practiceTrials = 3, realTestTrials = 5, blocksPerMethod = 2 };
    [Tooltip("Per method and per day: 2 blocks x (3 practice + 5 test). 2 methods x 2 blocks x 5 test phrases = 20 phrases from the 26 of the day set")]
    [SerializeField] private StudyConfig study3Config = new StudyConfig { practiceTrials = 3, realTestTrials = 5, blocksPerMethod = 2 };

    void Start()
    {
        // Initialize app: show only the dominant hand selection, hide other panels
        ShowSelectionStep(SelectionStep.Hand);
        ShowControlPanelForStudy(0);
        // PanelController shows its ControllerPanel in its own Start(); hide it again once every Start() has run.
        // The keyboard system (preview + textboard) is hidden there as well: it only appears when a study starts.
        StartCoroutine(HideControlPanelsAfterFirstFrame());

        if (keyboardController != null) keyboardController.AnchorModeChanged += HandleAnchorModeChanged;
        
        // Initial state setup
        Debug.Log("System Start");

        // Subscribe to key events
        KeyboardEvents.OnKeyPressed += HandleKeyPress;
        KeyboardEvents.OnDeleteCommandPressed += HandleDeleteCommand;
        KeyboardEvents.OnNextCommandPressed += HandleNextCommand;
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        KeyboardEvents.OnKeyPressed -= HandleKeyPress;
        KeyboardEvents.OnDeleteCommandPressed -= HandleDeleteCommand;
        KeyboardEvents.OnNextCommandPressed -= HandleNextCommand;
        if (keyboardController != null) keyboardController.AnchorModeChanged -= HandleAnchorModeChanged;
    }

    private IEnumerator HideControlPanelsAfterFirstFrame()
    {
        yield return null;
        if (studyRunning) yield break;

        ShowControlPanelForStudy(0);
        // Not deactivated before the first frame: PanelController / KeyboardController have to run their Start() first
        if (keyboardController != null) keyboardController.ShowKeyboard(false);
    }

    private void HandleAnchorModeChanged(AnchorMode mode)
    {
        // Hand-anchored: only the dominant hand types. Head-anchored: both hands type.
        RefreshFingerTips();
    }

    // Core input processing function
    private void HandleKeyPress(string key)
    {
        if (key.Length > 1 || key == " ")
        {
            HandleWordOrSpaceInput(key);
        }
        else
        {
            customInputDisplay.AddChar(key[0]);
            DataLogger.Instance.Log("KEY_PRESS", key);
        }
    }

    // Dedicated handler method for delete command
    private void HandleDeleteCommand()
    {
        customInputDisplay.DeleteLastChar();

        // Record
        DataLogger.Instance.Log("KEY_PRESS", "delete");
        Debug.Log("DELETE");
    }

    private void HandleNextCommand()
    {
        OnNextPhraseButtonPressed();
    }

    // Handle words and spaces
    private void HandleWordOrSpaceInput(string input)
    {
        if (input == " ")
        {
            customInputDisplay.AddChar(' ');
            DataLogger.Instance.Log("KEY_PRESS", " ");
            return;
        }

        // 1. Get incomplete word currently in the input field
        string currentFullText = customInputDisplay.GetCurrentText();
        string[] words = currentFullText.Split(' ');
        string incompleteWord = words.LastOrDefault();

        // 2. Delete that incomplete word
        if (!string.IsNullOrEmpty(incompleteWord))
        {
            for (int i = 0; i < incompleteWord.Length; i++)
            {
                customInputDisplay.DeleteLastChar();
            }
        }

        // 3. Append the selected full word or space
        foreach (char c in input)
        {
            customInputDisplay.AddChar(c);
        }

        // 4. If predicted word, automatically append a space at the end
        customInputDisplay.AddChar(' ');
        DataLogger.Instance.Log("PREDICTION_SELECT", input + " ");
    }

    #region --- Public Methods Called by Buttons ---

    /// <summary>
    /// Step 2 of the selection flow (the dominant hand is already known).
    /// Study 3 continues with the day selection, Study 1 and 2 start right away.
    /// </summary>
    public void OnStudySelected(int studyNumber)
    {
        if (studyNumber < 1 || studyNumber > 3)
        {
            Debug.LogWarning($"OnStudySelected: unknown study number {studyNumber}", this);
            return;
        }

        if (!handSelected)
        {
            Debug.LogWarning("OnStudySelected: no dominant hand selected yet", this);
            hintTextField.text = "Dominant Hand Selection";
            ShowSelectionStep(SelectionStep.Hand);
            return;
        }

        pendingStudyNumber = studyNumber;
        if (studyNumber == 3)
        {
            hintTextField.text = "Day Selection";
            ShowSelectionStep(SelectionStep.Day);
        }
        else
        {
            ShowSelectionStep(SelectionStep.None);
            BeginStudy(studyNumber);
        }
    }

    /// <summary>
    /// "Demo" on the study selection panel: shows ProKey right away (Study 3 pose and trial counts, no day, no methods).
    /// Its control panel only has Practice, Start Test and Back.
    /// </summary>
    public void OnDemoSelected()
    {
        if (studyRunning) return;

        if (!handSelected)
        {
            Debug.LogWarning("OnDemoSelected: no dominant hand selected yet", this);
            hintTextField.text = "Dominant Hand Selection";
            ShowSelectionStep(SelectionStep.Hand);
            return;
        }

        pendingStudyNumber = 0;
        ShowSelectionStep(SelectionStep.None);
        BeginStudy(DemoStudyNumber);
    }

    /// <summary>
    /// Step 3 (Study 3 only): day selection, starts Study 3.
    /// </summary>
    public void OnDaySelected(int day)
    {
        if (day < 1 || day > studyDayCount)
        {
            Debug.LogWarning($"OnDaySelected: Day {day} is not part of Study 3 (Day 1-{studyDayCount})", this);
            return;
        }

        if (!handSelected)
        {
            Debug.LogWarning("OnDaySelected: no dominant hand selected yet", this);
            hintTextField.text = "Dominant Hand Selection";
            ShowSelectionStep(SelectionStep.Hand);
            return;
        }

        pendingStudyNumber = 3;
        currentDay = day;
        ShowSelectionStep(SelectionStep.None);
        BeginStudy(3);
    }

    /// <summary>
    /// Step 1 of the selection flow: stores the dominant hand and continues with the study selection.
    /// The hand is kept when a study ends, so it only has to be chosen once per participant.
    /// </summary>
    public void OnDominantHandSelected(DominantHand hand)
    {
        if (studyRunning)
        {
            Debug.LogWarning("OnDominantHandSelected: ignored, a study is running", this);
            return;
        }

        dominantHand = hand;
        handSelected = true;
        pendingStudyNumber = 0;
        UpdateStudyPanelHandLabel();
        Debug.Log($"Dominant hand: {hand}");

        hintTextField.text = "Study Selection";
        ShowSelectionStep(SelectionStep.Study);
    }

    private void UpdateStudyPanelHandLabel()
    {
        if (studyPanelHandLabel != null) studyPanelHandLabel.text = $"Hand: {dominantHand} (change)";
    }

    public void OnLeftHandSelected()
    {
        OnDominantHandSelected(DominantHand.Left);
    }

    public void OnRightHandSelected()
    {
        OnDominantHandSelected(DominantHand.Right);
    }

    /// <summary>
    /// "Back" on the day panel returns to the study selection,
    /// the hand button on the study panel returns to the dominant hand selection.
    /// </summary>
    public void OnSelectionBack()
    {
        if (studyRunning) return;

        bool dayStepVisible = daySelectionPanel != null && daySelectionPanel.activeSelf;
        pendingStudyNumber = 0;
        if (dayStepVisible && handSelected)
        {
            hintTextField.text = "Study Selection";
            ShowSelectionStep(SelectionStep.Study);
        }
        else
        {
            hintTextField.text = "Dominant Hand Selection";
            ShowSelectionStep(SelectionStep.Hand);
        }
    }

    private void ShowSelectionStep(SelectionStep step)
    {
        if (studySelectionPanel != null) studySelectionPanel.SetActive(step == SelectionStep.Study);
        if (daySelectionPanel != null) daySelectionPanel.SetActive(step == SelectionStep.Day);
        if (handSelectionPanel != null) handSelectionPanel.SetActive(step == SelectionStep.Hand);
    }

    private void ShowControlPanelForStudy(int studyNumber)
    {
        if (controllerPanelStudy1 != null) controllerPanelStudy1.SetActive(studyNumber == 1);
        if (controllerPanelStudy2 != null) controllerPanelStudy2.SetActive(studyNumber == 2);
        if (controllerPanelStudy3 != null) controllerPanelStudy3.SetActive(studyNumber == 3);
        if (controllerPanelDemo != null) controllerPanelDemo.SetActive(studyNumber == DemoStudyNumber);
    }

    /// <summary>
    /// Starts a study once study number, (day) and dominant hand are known.
    /// Study 1: explore anchor with PokeKey. Study 2 / 3: methods are picked on the control panel.
    /// Demo: ProKey with the Study 3 pose, no methods.
    /// </summary>
    private void BeginStudy(int studyNumber)
    {
        bool demo = studyNumber == DemoStudyNumber;
        currentStudyNumber = studyNumber;
        studyRunning = true;
        currentMethodIndex = -1;
        currentStudyOption = 0;
        completedMethods.Clear();
        completedBlocks.Clear();

        string condition = demo ? "DEMO" : (studyNumber == 3 ? $"STUDY3_Day{currentDay}" : $"STUDY{studyNumber}");
        DataLogger.Instance.InitializeNewLogFile(participantId, condition);
        DataLogger.Instance.Log("STUDY_NUM", demo ? "Demo" : $"{studyNumber}");
        if (studyNumber == 3) DataLogger.Instance.Log("SELECT_DAY", $"{currentDay}");
        DataLogger.Instance.Log("DOMINANT_HAND", dominantHand.ToString(),
            $"KeyboardHand:{(dominantHand == DominantHand.Left ? "Right" : "Left")}");

        // Phrase set: Study 3 uses one set per day, Study 1 and 2 use the global set
        if (studyNumber == 3) PhraseProvider.Instance.SetDay(currentDay);
        else PhraseProvider.Instance.UseGlobalPhraseSet();

        keyboardController.ShowKeyboard(true);
        keyboardController.ResetHandAnchorState();
        // The demo uses the beside pose of Study 3
        keyboardController.SetStudyNumber(demo ? 3 : currentStudyNumber);

        // Study 1 uses PokeKey. Study 2 / 3 show Method A until a method button is pressed. The demo shows ProKey.
        int[] methods = GetMethodKeyboards(studyNumber);
        int startKeyboard = (methods != null && methods.Length > 0) ? methods[0] : (demo ? demoKeyboardIndex : study1KeyboardIndex);
        ActivateKeyboard(startKeyboard);

        // Handedness: which hand holds the keyboard, which fingertips type, where the side panels sit
        keyboardController.SetFullHandMode(false);
        ApplyDominantHand();

        // Apply the beside pose of this study (mirrored for left-handed participants)
        keyboardController.SetKeyboardPose(KeyboardPosition.Beside);
        if (studyNumber != 1)
        {
            // Study 2 / 3: the keyboard follows the holding hand right away
            SetKeyboardAnchorToHand();
        }

        ShowControlPanelForStudy(studyNumber);

        // Reinitialize PanelController to reset all toggles (fixes double-click issue)
        if (panelController != null)
        {
            panelController.ReinitializeForStudy();
        }

        if (studyNumber == 1)
        {
            // Study 1 starts in world coordinates, in front of the participant.
            // The keyboard only follows the hand after "Confirm" in the hand exploration (or the head in the head exploration).
            keyboardController.BeginWorldAnchoredHandSetup(true);
            Debug.Log("Anchor Mode: World (until confirmed)");
        }

        DataLogger.Instance.Log("STUDY_START", demo ? "Demo" : $"Study{studyNumber}", $"Keyboard: {keyboardController.ActiveKeyboard.name}");

        targetTextField.text = "the quick brown fox jumps over the lazy dog"; // Sample text for free practice
        customInputDisplay.Clear();
        if (demo) hintTextField.text = "Demo | press Practice or Start Test";
        else hintTextField.text = studyNumber == 1 ? "Select an option" : "Select a method";

        currentPhase = TestPhase.FreePractice;
        NotifyStateChanged();
    }

    private int[] GetMethodKeyboards(int studyNumber)
    {
        switch (studyNumber)
        {
            case 2: return study2MethodKeyboards;
            case 3: return study3MethodKeyboards;
            default: return null;
        }
    }

    public static string GetMethodLabel(int methodIndex)
    {
        return methodIndex < 0 ? "-" : ((char)('A' + methodIndex)).ToString();
    }

    /// <summary>
    /// Study 2 / 3: switches to the keyboard of the given method (0 = Method A, 1 = Method B, ...).
    /// Not allowed while the real test is running. A running practice is ended.
    /// </summary>
    public bool SelectMethod(int methodIndex)
    {
        int[] methods = GetMethodKeyboards(currentStudyNumber);
        if (!studyRunning || methods == null || methodIndex < 0 || methodIndex >= methods.Length)
        {
            Debug.LogWarning($"SelectMethod: method {methodIndex} is not available in study {currentStudyNumber}", this);
            return false;
        }

        if (currentPhase == TestPhase.Real)
        {
            Debug.LogWarning("SelectMethod: ignored, the real test is still running", this);
            return false;
        }

        if (currentPhase == TestPhase.Practice)
        {
            DataLogger.Instance.Log("PHASE_END", currentPhase.ToString(), "Interrupted:MethodChanged");
        }

        currentMethodIndex = methodIndex;
        predictionManager.ResetPredictions();
        ActivateKeyboard(methods[methodIndex]);

        DataLogger.Instance.Log("METHOD_SELECT", GetMethodLabel(methodIndex),
            $"Keyboard: {keyboardController.ActiveKeyboard.name}|Index:{methods[methodIndex]}");
        Debug.Log($"Method {GetMethodLabel(methodIndex)}: {keyboardController.ActiveKeyboard.name}");

        currentPhraseQueue = null;
        currentPhase = TestPhase.FreePractice;
        targetTextField.text = "the quick brown fox jumps over the lazy dog"; // Sample text for free practice
        customInputDisplay.Clear();
        hintTextField.text = completedMethods.Contains(methodIndex)
            ? $"Method {GetMethodLabel(methodIndex)} (finished)"
            : HintLine($"Method {GetMethodLabel(methodIndex)}", GetBlockLabel(), "press Practice or Start Test");

        NotifyStateChanged();
        return true;
    }

    private int GetCompletedBlocks(int methodIndex)
    {
        int blocks;
        return completedBlocks.TryGetValue(methodIndex, out blocks) ? blocks : 0;
    }

    /// <summary>"Block 1/2" for the current method; empty for studies without methods or with a single block</summary>
    private string GetBlockLabel()
    {
        return MethodCount > 0 && BlockCount > 1 ? $"Block {CurrentBlock}/{BlockCount}" : "";
    }

    /// <summary>"Method A" for the studies with methods; empty otherwise</summary>
    private string GetMethodPart()
    {
        return MethodCount > 0 ? $"Method {GetMethodLabel(currentMethodIndex)}" : "";
    }

    /// <summary>
    /// One hint line from its non-empty parts, "Method A | Block 1/2 | ...". The text board shows the hint on ONE line
    /// (it shrinks a little, then is cut with an ellipsis), and about 60 characters fit the small board at full size:
    /// keep every part short.
    /// </summary>
    private static string HintLine(params string[] parts)
    {
        return string.Join(" | ", parts.Where(part => !string.IsNullOrEmpty(part)));
    }

    /// <summary>Phase name shown to the participant (the log keeps the enum names)</summary>
    private static string PhaseLabel(TestPhase phase)
    {
        return phase == TestPhase.Real ? "Test" : phase.ToString();
    }

    /// <summary>
    /// Study 2 / 3: "Back" on the method panel. Leaves the chosen method so another one can be selected.
    /// Not allowed while the real test is running. A running practice is ended. The keyboard stays as it is.
    /// </summary>
    public bool DeselectMethod()
    {
        if (!studyRunning || MethodCount == 0) return false;

        if (currentPhase == TestPhase.Real)
        {
            Debug.LogWarning("DeselectMethod: ignored, the real test is still running", this);
            return false;
        }

        if (currentMethodIndex < 0) return true;

        if (currentPhase == TestPhase.Practice)
        {
            DataLogger.Instance.Log("PHASE_END", currentPhase.ToString(), "Interrupted:MethodDeselected");
        }

        DataLogger.Instance.Log("METHOD_DESELECT", GetMethodLabel(currentMethodIndex),
            $"Keyboard: {keyboardController.ActiveKeyboard.name}");

        currentMethodIndex = -1;
        predictionManager.ResetPredictions();

        currentPhraseQueue = null;
        currentPhase = TestPhase.FreePractice;
        targetTextField.text = "the quick brown fox jumps over the lazy dog"; // Sample text for free practice
        customInputDisplay.Clear();
        hintTextField.text = AllMethodsCompleted ? "All Test Finished!" : "Select a method";

        NotifyStateChanged();
        return true;
    }

    public static string GetStudyOptionLabel(int option)
    {
        return option > 0 ? $"Explore{option}" : "-";
    }

    /// <summary>
    /// Study 1: an option (1 = Explore 1 / hand anchor, 2 = Explore 2 / head anchor) has to be chosen
    /// before Practice and Start Test become available. The anchor itself is set up by the PanelController.
    /// Not allowed while the real test is running. A running practice is ended.
    /// </summary>
    public bool SelectStudyOption(int option)
    {
        if (!studyRunning || currentStudyNumber != 1 || option < 1)
        {
            Debug.LogWarning($"SelectStudyOption: option {option} is not available in study {currentStudyNumber}", this);
            return false;
        }

        if (currentPhase == TestPhase.Real)
        {
            Debug.LogWarning("SelectStudyOption: ignored, the real test is still running", this);
            return false;
        }

        if (currentPhase == TestPhase.Practice)
        {
            DataLogger.Instance.Log("PHASE_END", currentPhase.ToString(), "Interrupted:OptionChanged");
        }

        currentStudyOption = option;
        predictionManager.ResetPredictions();
        DataLogger.Instance.Log("OPTION_SELECT", GetStudyOptionLabel(option), $"Keyboard: {keyboardController.ActiveKeyboard.name}");

        currentPhraseQueue = null;
        currentPhase = TestPhase.FreePractice;
        targetTextField.text = "the quick brown fox jumps over the lazy dog"; // Sample text for free practice
        customInputDisplay.Clear();
        hintTextField.text = $"Explore {option} | press Practice or Start Test";

        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Study 1: "Back" on the control panel. Leaves the chosen option so the other one can be selected.
    /// Not allowed while the real test is running. A running practice is ended. Keyboard and anchor stay as they are.
    /// </summary>
    public bool DeselectStudyOption()
    {
        if (!studyRunning || currentStudyNumber != 1) return false;

        if (currentPhase == TestPhase.Real)
        {
            Debug.LogWarning("DeselectStudyOption: ignored, the real test is still running", this);
            return false;
        }

        if (currentStudyOption == 0) return true;

        if (currentPhase == TestPhase.Practice)
        {
            DataLogger.Instance.Log("PHASE_END", currentPhase.ToString(), "Interrupted:OptionDeselected");
        }

        DataLogger.Instance.Log("OPTION_DESELECT", GetStudyOptionLabel(currentStudyOption),
            $"Keyboard: {keyboardController.ActiveKeyboard.name}");

        currentStudyOption = 0;
        predictionManager.ResetPredictions();

        currentPhraseQueue = null;
        currentPhase = TestPhase.FreePractice;
        targetTextField.text = "the quick brown fox jumps over the lazy dog"; // Sample text for free practice
        customInputDisplay.Clear();
        hintTextField.text = "Select an option";

        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Activates a child keyboard together with its prediction UI and the matching Preview / Textboard backgrounds
    /// </summary>
    private void ActivateKeyboard(int keyboardIndex)
    {
        currentKeyboardIndex = keyboardIndex;
        keyboardController.ActivateChildKeyboard(keyboardIndex);

        if (keyboardController.ActiveKeyboard != null)
        {
            predictionManager.RegisterPredictionUI(keyboardController.ActiveKeyboard.predictionContainer, keyboardController.ActiveKeyboard.predictionKeys);
        }

        ApplyBackgroundForKeyboard(keyboardIndex);
        PositionControlPanels();
    }

    /// <summary>
    /// Keeps every control panel beside the active keyboard with the same gap, whatever its width (S / L).
    /// Right of the keyboard; left of it for left-dominant participants (the right hand holds the keyboard there).
    /// </summary>
    private void PositionControlPanels()
    {
        if (keyboardController == null || keyboardController.ActiveKeyboard == null) return;

        Transform keyboard = keyboardController.ActiveKeyboard.transform;
        bool leftSide = mirrorControlPanelForLeftDominant && dominantHand == DominantHand.Left;

        PlaceControlPanel(controllerPanelStudy1, keyboard, leftSide);
        PlaceControlPanel(controllerPanelStudy2, keyboard, leftSide);
        PlaceControlPanel(controllerPanelStudy3, keyboard, leftSide);
        PlaceControlPanel(controllerPanelDemo, keyboard, leftSide);
    }

    private void PlaceControlPanel(GameObject panel, Transform keyboard, bool leftSide)
    {
        if (panel == null) return;
        if (!ControlPanelPlacement.Place(panel.transform, keyboard, layoutGap, leftSide))
        {
            Debug.LogWarning($"PositionControlPanels: could not measure '{panel.name}' or '{keyboard.name}', the panel keeps its position", this);
        }
    }

    /// <summary>
    /// The 'L' keyboards are wider than the default ones: switch Preview and Textboard to Background_L (and back to _S)
    /// </summary>
    private void ApplyBackgroundForKeyboard(int keyboardIndex)
    {
        bool large = largeBackgroundKeyboards != null && System.Array.IndexOf(largeBackgroundKeyboards, keyboardIndex) >= 0;

        SetActiveAll(smallBackgroundPanels, !large);
        SetActiveAll(largeBackgroundPanels, large);

        DataLogger.Instance.Log("BACKGROUND_SIZE", large ? "L" : "S", $"KeyboardIndex:{keyboardIndex}");
    }

    private static void SetActiveAll(List<GameObject> objects, bool active)
    {
        if (objects == null) return;
        foreach (var obj in objects)
        {
            if (obj != null) obj.SetActive(active);
        }
    }

    /// <summary>
    /// Applies the selected dominant hand: the keyboard (and the hand-following panels) move to the other hand,
    /// the side panels swap sides, and only the dominant hand's fingertips stay enabled while hand-anchored.
    /// </summary>
    private void ApplyDominantHand()
    {
        bool keyboardOnRightHand = dominantHand == DominantHand.Left;

        keyboardController.SetKeyboardHand(keyboardOnRightHand);

        if (handFollowers != null)
        {
            foreach (var follower in handFollowers)
            {
                if (follower != null) follower.SetFollowRightHand(keyboardOnRightHand);
            }
        }

        // The control panel swaps to the other side of the keyboard for left-dominant participants
        PositionControlPanels();

        RefreshFingerTips();
    }

    /// <summary>
    /// Enables / disables the fingertip colliders according to dominant hand, anchor mode and finger mode
    /// </summary>
    private void RefreshFingerTips()
    {
        bool fullHandMode = keyboardController.IsFullHandMode;
        bool bothHands = !studyRunning || !restrictTypingToDominantHand || keyboardController.CurrentAnchorMode == AnchorMode.Head;
        bool leftTypes = bothHands || dominantHand == DominantHand.Left;
        bool rightTypes = bothHands || dominantHand == DominantHand.Right;

        SetActiveAll(leftIndexTips, leftTypes);
        SetActiveAll(rightIndexTips, rightTypes);
        SetActiveAll(leftFingerTips, leftTypes && fullHandMode);
        SetActiveAll(rightFingerTips, rightTypes && fullHandMode);
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }


    public void BackToStudySelection()
    {
        // Cleanup
        hintTextField.text = "Study Selection";
        predictionManager.ResetPredictions();

        // End pressed during the real test: close the phase in the log
        if (currentPhase == TestPhase.Real)
        {
            DataLogger.Instance.Log("PHASE_END", currentPhase.ToString(), "Interrupted:StudyEnd");
        }

        string studyName = currentStudyNumber == DemoStudyNumber ? "Demo" : currentStudyNumber.ToString();
        DataLogger.Instance.Log("STUDY_END", "StudyEnd",
            $"Study:{studyName}|Day:{currentDay}|MethodsCompleted:{completedMethods.Count}/{MethodCount}");
        currentPhase = TestPhase.Idle;
        currentPhraseQueue = null;
        currentMethodIndex = -1;
        currentStudyOption = 0;
        completedMethods.Clear();
        completedBlocks.Clear();
        studyRunning = false;
        pendingStudyNumber = 0;
        customInputDisplay.Clear();

        keyboardController.ShowKeyboard(false);

        // Hide the control panels when returning to study selection.
        // The dominant hand is kept: it is only asked again when the hand button of the study panel is pressed.
        ShowControlPanelForStudy(0);
        UpdateStudyPanelHandLabel();
        ShowSelectionStep(handSelected ? SelectionStep.Study : SelectionStep.Hand);

        NotifyStateChanged();
    }

    /// <summary>
    /// Sets the finger mode to a specific state (10-finger or 2-finger)
    /// </summary>
    /// <param name="fullHandMode">true for 10-finger mode, false for 2-finger mode</param>
    public void SetFingerMode(bool fullHandMode)
    {
        keyboardController.SetFullHandMode(fullHandMode);

        // Fingertips also depend on the dominant hand and the anchor mode
        RefreshFingerTips();
        
        DataLogger.Instance?.Log("FINGER_MODE", fullHandMode ? "10-finger" : "2-finger");
    }

    /// <summary>
    /// Sets keyboard anchor mode to Hand
    /// </summary>
    public void SetKeyboardAnchorToHand()
    {
        keyboardController.SetAnchorMode(AnchorMode.Hand);
        keyboardController.SetFollow(true);
        DataLogger.Instance.Log("ANCHOR_MODE_SET", "Hand");
        Debug.Log("Anchor Mode: Hand");
    }

    #endregion

    #region --- Test Phase Control ---

    /// <summary>
    /// Gets current Study configuration
    /// </summary>
    private StudyConfig GetCurrentStudyConfig()
    {
        switch (currentStudyNumber)
        {
            case 1: return study1Config;
            case 3: return study3Config;
            case DemoStudyNumber: return study3Config; // The demo is ProKey as in Study 3
            default: return study2Config;
        }
    }

    /// <summary>
    /// Study 1 needs a selected option, Study 2 / 3 a selected, not yet finished method before a phase can start.
    /// The demo has neither.
    /// </summary>
    private bool CanStartPhase(string phaseName)
    {
        if (!studyRunning)
        {
            Debug.LogWarning($"{phaseName}: no study is running", this);
            return false;
        }

        if (currentPhase == TestPhase.Real)
        {
            Debug.LogWarning($"{phaseName}: ignored, the real test is still running", this);
            return false;
        }

        if (currentStudyNumber == 1 && currentStudyOption == 0)
        {
            hintTextField.text = "Select an option first";
            return false;
        }

        if (MethodCount == 0) return true; // Study 1 and the demo

        if (currentMethodIndex < 0)
        {
            hintTextField.text = "Select a method first";
            return false;
        }

        if (completedMethods.Contains(currentMethodIndex) && !allowRepeatCompletedMethod)
        {
            hintTextField.text = $"Method {GetMethodLabel(currentMethodIndex)} is already finished";
            return false;
        }

        return true;
    }

    private string GetPhaseDetails()
    {
        if (currentStudyNumber == 1)
        {
            return $"Option:{GetStudyOptionLabel(currentStudyOption)}|Anchor:{keyboardController.CurrentAnchorMode}|Keyboard:{keyboardController.ActiveKeyboard.name}";
        }

        if (MethodCount == 0) return keyboardController.ActiveKeyboard != null ? $"Keyboard:{keyboardController.ActiveKeyboard.name}" : "";
        return $"Method:{GetMethodLabel(currentMethodIndex)}|Block:{CurrentBlock}/{BlockCount}|Keyboard:{keyboardController.ActiveKeyboard.name}";
    }

    public void StartPracticePhase()
    {
        if (!CanStartPhase("StartPracticePhase")) return;
        if (currentPhase == TestPhase.Practice) return; // Already running (double trigger)

        predictionManager.ResetPredictions();

        currentPhase = TestPhase.Practice;
        DataLogger.Instance.Log("PHASE_START", "Practice", GetPhaseDetails());

        // Practice phrases come from the global set, so a Study 3 day set is used by the test blocks only
        StudyConfig config = GetCurrentStudyConfig();
        currentPhraseQueue = PhraseProvider.Instance.GetPracticePhraseQueue(config.practiceTrials);

        // Load first phrase
        LoadNextPhrase();
        NotifyStateChanged();
    }

    public void StartRealTestPhase()
    {
        if (!CanStartPhase("StartRealTestPhase")) return;

        if (currentPhase == TestPhase.Practice)
        {
            DataLogger.Instance.Log("PHASE_END", currentPhase.ToString(), "Interrupted:RealTestStarted");
        }

        predictionManager.ResetPredictions();

        currentPhase = TestPhase.Real;
        DataLogger.Instance.Log("PHASE_START", "RealTest", GetPhaseDetails());

        StudyConfig config = GetCurrentStudyConfig();
        currentPhraseQueue = PhraseProvider.Instance.GetNextPhraseQueue(config.realTestTrials);
        LoadNextPhrase();
        NotifyStateChanged();
    }

    #endregion

    #region --- Phrase Processing ---

    public void OnNextPhraseButtonPressed()
    {
        // Called when user clicks "Next" or finishes a sentence
        string userInput = customInputDisplay.GetCurrentText();

        predictionManager.ResetPredictions();

        // 1. Record data of previous sentence
        DataLogger.Instance.Log("PHRASE_SUBMIT", userInput, $"Target: {currentTargetPhrase}");

        if (currentPhase == TestPhase.FreePractice || currentPhase == TestPhase.Idle || currentPhraseQueue == null)
        {
            customInputDisplay.Clear();
            return;
        }

        // 2. Check if queue is empty
        if (currentPhraseQueue.Count == 0)
        {
            EndCurrentPhase();
        }
        else
        {
            LoadNextPhrase();
        }
    }

    private void LoadNextPhrase()
    {
        if (currentPhraseQueue == null || currentPhraseQueue.Count == 0) return;

        currentTargetPhrase = currentPhraseQueue.Dequeue();

        // Update UI
        targetTextField.text = currentTargetPhrase;
        customInputDisplay.Clear();
        UpdateHint();

        // Record event
        DataLogger.Instance.Log("PHRASE_LOAD", currentTargetPhrase);    }

    #endregion

    #region --- Phase End & UI Management ---

    private void EndCurrentPhase()
    {
        DataLogger.Instance.Log("PHASE_END", currentPhase.ToString(), GetPhaseDetails());

        if (currentPhase == TestPhase.Practice)
        {
            hintTextField.text = HintLine(GetMethodPart(), GetBlockLabel(), "Practice done", "press Start Test");
        }
        else if (currentPhase == TestPhase.Real)
        {
            if (MethodCount > 0)
            {
                // One test block done; the method is finished after its last block
                int blocksDone = GetCompletedBlocks(currentMethodIndex) + 1;
                completedBlocks[currentMethodIndex] = blocksDone;
                DataLogger.Instance.Log("BLOCK_COMPLETE", GetMethodLabel(currentMethodIndex), $"Block:{blocksDone}/{BlockCount}");

                if (blocksDone >= BlockCount)
                {
                    completedMethods.Add(currentMethodIndex);
                    DataLogger.Instance.Log("METHOD_COMPLETE", GetMethodLabel(currentMethodIndex),
                        $"Completed:{completedMethods.Count}/{MethodCount}");
                    hintTextField.text = AllMethodsCompleted
                        ? "All Test Finished!"
                        : $"Method {GetMethodLabel(currentMethodIndex)} finished | select the next method";
                }
                else
                {
                    hintTextField.text = HintLine(GetMethodPart(), $"Block {blocksDone}/{BlockCount} done", "press Practice or Start Test");
                }
            }
            else if (currentStudyNumber == 1)
            {
                // Back to the option buttons (Explore 1 / Explore 2 / End)
                DataLogger.Instance.Log("OPTION_COMPLETE", GetStudyOptionLabel(currentStudyOption));
                hintTextField.text = $"Explore {currentStudyOption} finished | select an option or End";
                currentStudyOption = 0;
            }
            else
            {
                hintTextField.text = "Test Finished!";
            }
        }

        targetTextField.text = "";
        customInputDisplay.Clear();
        currentPhase = TestPhase.Idle;
        NotifyStateChanged();
    }

    private void UpdateHint()
    {
        if (currentPhraseQueue != null && currentPhraseQueue.Count >= 0)
        {
            int left = currentPhraseQueue.Count + 1;
            hintTextField.text = HintLine(GetMethodPart(), GetBlockLabel(), PhaseLabel(currentPhase), $"{left} {(left == 1 ? "phrase" : "phrases")} left");
        }
    }
    #endregion
}