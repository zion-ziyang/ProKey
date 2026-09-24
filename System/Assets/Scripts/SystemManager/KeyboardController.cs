using UnityEngine;
using System.Collections.Generic;

public class KeyboardController : MonoBehaviour
{
    [Header("Core Objects")]
    [SerializeField] private GameObject keyboardSystem;
    [SerializeField] private Transform handAnchor;
    [SerializeField] private Transform headAnchor; // Head anchor (usually camera or center eye)

    [Header("Handedness")]
    [Tooltip("Anchor used when the keyboard is held by the RIGHT hand (left-handed participant). 'Hand Anchor' above is the left-hand default.")]
    [SerializeField] private Transform rightHandAnchor;
    [Tooltip("Fallback local X of the keyboard centre inside KeyboardSystem. Keeps the keyboard centred when the left-hand preset poses are mirrored to the right hand.")]
    [SerializeField] private float mirrorPivotLocalX = 0.015f;
    [Tooltip("Use the active child keyboard's local X as the mirror pivot (the small and large keyboards are centred differently)")]
    [SerializeField] private bool useActiveKeyboardAsMirrorPivot = true;

    [Header("World-Anchored Hand Setup (Study 1)")]
    [Tooltip("Distance in front of the head at which the keyboard is placed when a hand-anchor exploration trial starts (meters)")]
    [SerializeField] private float worldSetupDistance = 0.35f;
    [Tooltip("Height relative to the head at which the keyboard is placed when a hand-anchor exploration trial starts (meters, negative is below the eyes)")]
    [SerializeField] private float worldSetupHeight = -0.2f;
    [Tooltip("Confirm is rejected when the holding hand is farther away from the keyboard than this (hand not tracked). 0 = no check")]
    [SerializeField] private float maxConfirmHandDistance = 1.0f;

    [Header("Child Keyboard Management")]
    [SerializeField] private List<Keyboard> childKeyboards;

    [Header("Position Presets - Hand Anchored")]
    [SerializeField] private KeyboardPose besidePose;
    
    [Header("Position Presets - Hand Anchored (Study Specific)")]
    [Tooltip("Beside position configuration for Study 1")]
    [SerializeField] private KeyboardPose besidePoseStudy1;
    [Tooltip("Beside position configuration for Study 2")]
    [SerializeField] private KeyboardPose besidePoseStudy2;
    [Tooltip("Beside position configuration for Study 3")]
    [SerializeField] private KeyboardPose besidePoseStudy3;

    [Header("Position Presets - Head Anchored")]
    [Tooltip("Distance between keyboard and head (meters)")]
    [SerializeField] private float headAnchorDistance = 0.5f;
    [Tooltip("Keyboard tilt angle (degrees), 0 is horizontal, positive tilts upwards")]
    [SerializeField][Range(-90f, 90f)] private float headAnchorTiltAngle = 45f; // Default 45 deg
    [Tooltip("Keyboard height offset relative to head (meters), positive is upwards")]
    [SerializeField] private float headAnchorHeight = -0.2f;

    [Header("Position Smoothing")]
    [Tooltip("EMA smoothing factor (paper: alpha = 0.15), smaller is smoother but higher latency. Recommended between 0.1 and 0.3.")]
    [SerializeField][Range(0.01f, 1.0f)] private float smoothingFactor = 0.15f;

    // --- Private State Variables ---
    private bool isFollowing = false; // Whether following (hand or head)
    private AnchorMode currentAnchorMode = AnchorMode.Hand; // Current anchor mode (default Hand)
    private KeyboardPosition currentPose = KeyboardPosition.Beside; // Current keyboard position
    private Quaternion rotationOffset = Quaternion.identity;
    private Vector3 positionOffset = Vector3.zero;
    private bool hasCustomOffset = false; // True once a hand offset was captured (Confirm in the Study 1 hand exploration)
    private bool isFullHandMode = false;
    private int currentStudyNumber = 0; // Current study number (1, 2 or 3), 0 means unset
    public bool IsFullHandMode { get { return isFullHandMode; } }
    public void SetFullHandMode(bool isFullHandMode)
    {
        this.isFullHandMode = isFullHandMode;
    }

    private Vector3 previousPosition; // For smoothing calculation
    private bool isFirstFrame = true; // For smoothing initialization

    public Keyboard ActiveKeyboard { get; private set; }

    public bool IsFollowing { get { return isFollowing; } }
    public AnchorMode CurrentAnchorMode { get { return currentAnchorMode; } }
    /// <summary>True once a hand offset was captured ("Confirm" in the hand exploration), false while only the preset pose exists</summary>
    public bool HasCustomHandOffset { get { return hasCustomOffset; } }

    /// <summary>
    /// Raised whenever the anchor mode (Hand/Head) is set or toggled.
    /// </summary>
    public event System.Action<AnchorMode> AnchorModeChanged;

    // --- Handedness ---
    private bool keyboardOnRightHand = false; // false = left hand holds the keyboard (right-handed participant)

    /// <summary>
    /// The hand anchor the keyboard currently follows (non-dominant hand)
    /// </summary>
    public Transform ActiveHandAnchor
    {
        get { return (keyboardOnRightHand && rightHandAnchor != null) ? rightHandAnchor : handAnchor; }
    }

    /// <summary>
    /// Local X (in KeyboardSystem space) around which the left-hand presets are mirrored
    /// </summary>
    private float GetMirrorPivotLocalX()
    {
        if (useActiveKeyboardAsMirrorPivot && ActiveKeyboard != null && keyboardSystem != null
            && ActiveKeyboard.transform.parent == keyboardSystem.transform)
        {
            return ActiveKeyboard.transform.localPosition.x;
        }
        return mirrorPivotLocalX;
    }

    /// <summary>
    /// Selects which hand holds the keyboard. The preset poses are authored for the left hand
    /// and are mirrored when the keyboard moves to the right hand.
    /// Any custom offset is dropped because it was captured for the other hand.
    /// </summary>
    public void SetKeyboardHand(bool onRightHand)
    {
        if (onRightHand && rightHandAnchor == null)
        {
            Debug.LogWarning("SetKeyboardHand: rightHandAnchor not set, keyboard stays on the left hand", this);
        }

        keyboardOnRightHand = onRightHand;
        hasCustomOffset = false;
        isFirstFrame = true;

        if (currentAnchorMode == AnchorMode.Hand)
        {
            SetKeyboardPose(currentPose == KeyboardPosition.Idle ? KeyboardPosition.Beside : currentPose);
        }

        DataLogger.Instance.Log("KEYBOARD_HAND", onRightHand ? "Right" : "Left",
            ActiveHandAnchor != null ? ActiveHandAnchor.name : "null");
    }

    /// <summary>
    /// Clears per-session hand anchor state so a new study starts from the preset pose in Hand mode
    /// </summary>
    public void ResetHandAnchorState()
    {
        currentAnchorMode = AnchorMode.Hand;
        hasCustomOffset = false;
        isFirstFrame = true;
    }

    /// <summary>
    /// Sets current study number (1, 2 or 3) to select corresponding beside pose configuration
    /// </summary>
    public void SetStudyNumber(int studyNumber)
    {
        currentStudyNumber = studyNumber;
        DataLogger.Instance.Log("STUDY_NUMBER_SET", $"{studyNumber}", "KeyboardController");
    }

    void Start()
    {
        foreach (var kb in childKeyboards)
        {
            kb.gameObject.SetActive(false);
        }
        // keyboardSystem.SetActive(false);
    }

    void Update()
    {
        // Entire Update logic updated to include smoothing and visualization
        if (isFollowing)
        {
            Transform currentAnchor = null;

            // Select anchor according to current anchor mode
            if (currentAnchorMode == AnchorMode.Hand && ActiveHandAnchor != null)
            {
                currentAnchor = ActiveHandAnchor;
            }
            else if (currentAnchorMode == AnchorMode.Head && headAnchor != null)
            {
                currentAnchor = headAnchor;
            }

            if (currentAnchor != null)
            {
                currentAnchor.GetPositionAndRotation(out var anchorPosition, out var anchorRotation);

                // --- Position smoothing ---
                if (isFirstFrame)
                {
                    previousPosition = anchorPosition;
                    isFirstFrame = false;
                }
                // Lerp (linear interpolation) is common and efficient for smoothing
                Vector3 smoothedPosition = Vector3.Lerp(previousPosition, anchorPosition, smoothingFactor);
                previousPosition = smoothedPosition; // Use smoothed position as next frame's "previousPosition"

                // --- Keyboard position update ---
                if (currentAnchorMode == AnchorMode.Head)
                {
                    // Head anchoring: keyboard stays in view center using distance, angle, height parameters
                    UpdateHeadAnchoredPosition(smoothedPosition, anchorRotation);
                }
                else
                {
                    // SetKeyboardPose(KeyboardPosition.Beside);
                    // Hand anchoring: use traditional offset approach
                    keyboardSystem.transform.rotation = anchorRotation * rotationOffset;
                    Vector3 worldOffset = anchorRotation * positionOffset;
                    keyboardSystem.transform.position = smoothedPosition + worldOffset;
                }
            }
        }
    }

    /// <summary>
    /// Updates keyboard position in head anchored mode
    /// Keyboard stays in view center using distance, tilt angle, and height parameters
    /// </summary>
    private void UpdateHeadAnchoredPosition(Vector3 headPosition, Quaternion headRotation)
    {
        // 1. Calculate keyboard position: specified distance in front of head with height offset applied
        // Head forward vector (usually camera forward)
        Vector3 forward = headRotation * Vector3.forward;
        Vector3 up = headRotation * Vector3.up;

        // Keyboard position = head position + forward distance + height offset
        Vector3 keyboardPosition = headPosition + forward * headAnchorDistance + up * headAnchorHeight;

        // 2. Calculate keyboard rotation: keyboard faces user, then tilts up/down around horizontal right axis
        // Get horizontal forward of head (yaw only, ignoring pitch)
        Vector3 headForward = headRotation * Vector3.forward;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(headForward, Vector3.up).normalized;
        if (horizontalForward.magnitude < 0.01f)
        {
            // If head is almost vertically up or down, use default forward
            horizontalForward = Vector3.forward;
        }
        
        // Calculate horizontal right axis (always horizontal, for up/down tilt)
        Vector3 horizontalRight = Vector3.Cross(Vector3.up, horizontalForward).normalized;
        
        // Calculate horizontal rotation of head first (yaw only)
        Quaternion horizontalRotation = Quaternion.LookRotation(horizontalForward, Vector3.up);
        
        // Rotate tilt angle around horizontal right axis (pitch tilt)
        // This rotation happens in world space around horizontal right axis
        Quaternion tiltRotation = Quaternion.AngleAxis(headAnchorTiltAngle, horizontalRight);
        
        // Tilt around the horizontal right axis in horizontal space (so the tilt does not turn with the head), then apply the head orientation
        Quaternion tiltInHorizontalSpace = Quaternion.Inverse(horizontalRotation) * tiltRotation * horizontalRotation;
        Quaternion keyboardRotation = headRotation * tiltInHorizontalSpace;

        // 3. Apply smoothed position and calculated rotation
        keyboardSystem.transform.position = keyboardPosition;
        keyboardSystem.transform.rotation = keyboardRotation;
    }

    public void ShowKeyboard(bool show)
    {
        keyboardSystem.SetActive(show);
    }

    /// <summary>
    /// Sets whether to follow anchor
    /// </summary>
    public void SetFollow(bool follow)
    {
        isFollowing = follow;
        if (follow)
        {
            isFirstFrame = true; // Reset smoothing state each time following begins
        }
        // Log follow status change
        string details = "";
        if (currentAnchorMode == AnchorMode.Hand && follow)
        {
            details = FormatHandAnchorData(positionOffset, rotationOffset);
        }
        else if (currentAnchorMode == AnchorMode.Head && follow)
        {
            details = $"Distance:{headAnchorDistance:F3}m, Tilt:{headAnchorTiltAngle:F2}deg, Height:{headAnchorHeight:F3}m";
        }
        DataLogger.Instance.Log("FOLLOW_STATUS", $"{follow}", details);
    }

    /// <summary>
    /// Sets anchor mode (Hand or Head)
    /// </summary>
    public void SetAnchorMode(AnchorMode mode)
    {
        currentAnchorMode = mode;
        isFirstFrame = true; // Reset smoothing state when switching anchor mode

        // Log anchor mode setting
        string details = "";
        if (mode == AnchorMode.Hand)
        {
            // If hand anchored, only reapply preset pose offset if there is no custom offset
            // This preserves an offset captured with Confirm in the Study 1 hand exploration
            if (!hasCustomOffset)
            {
                SetKeyboardPose(currentPose);
            }
            details = FormatHandAnchorData(positionOffset, rotationOffset);
        }
        else
        {
            // Head anchor does not require pose; uses distance, angle, height parameters
            details = $"Distance:{headAnchorDistance:F3}m, Tilt:{headAnchorTiltAngle:F2}deg, Height:{headAnchorHeight:F3}m";
        }
        DataLogger.Instance.Log("ANCHOR_MODE_SET", $"{mode}", details);
        AnchorModeChanged?.Invoke(currentAnchorMode);
    }

    /// <summary>
    /// Captures the keyboard pose relative to the active hand anchor (from their current world poses)
    /// and starts following the hand with that offset.
    /// </summary>
    private bool CaptureHandOffsetAndFollow(string logEvent, string logValue)
    {
        Transform anchor = ActiveHandAnchor;
        if (anchor == null || keyboardSystem == null)
        {
            Debug.LogWarning("CaptureHandOffsetAndFollow: hand anchor or keyboardSystem not set");
            return false;
        }

        anchor.GetPositionAndRotation(out var handPos, out var handRot);
        keyboardSystem.transform.GetPositionAndRotation(out var kbPos, out var kbRot);

        // Rotation offset: difference between hand rotation and keyboard rotation
        rotationOffset = Quaternion.Inverse(handRot) * kbRot;

        // Position offset: transform world position delta into hand local space
        Vector3 worldDelta = kbPos - handPos;
        positionOffset = Quaternion.Inverse(handRot) * worldDelta;

        // Mark as using custom offset
        hasCustomOffset = true;

        // Resume following. Seed the smoothing state so the keyboard does not jump on the first frame.
        previousPosition = handPos;
        isFirstFrame = false;
        isFollowing = true;
        currentPose = KeyboardPosition.Beside; // Logically still considered beside
        
        // Log recalculated offset value and world coordinates for verification
        string offsetData = FormatHandAnchorData(positionOffset, rotationOffset);
        string worldData = FormatWorldCoordinates(handPos, handRot, kbPos, kbRot);
        string details = $"Following|Hand:{(keyboardOnRightHand ? "Right" : "Left")}|Offset:{offsetData}|World:{worldData}";
        DataLogger.Instance.Log(logEvent, logValue, details);
        DataLogger.Instance.Log("HAND_ANCHOR_STATUS", $"{isFollowing}", offsetData);
        return true;
    }

    /// <summary>
    /// Study 1 hand exploration, step 1: the keyboard stops following and stays fixed in the world
    /// so the participant can position the holding hand relative to it.
    /// </summary>
    /// <param name="placeInFrontOfHead">true: re-place the keyboard in front of the user first; false: freeze it where it is</param>
    public void BeginWorldAnchoredHandSetup(bool placeInFrontOfHead)
    {
        currentAnchorMode = AnchorMode.Hand;
        isFollowing = false;

        if (placeInFrontOfHead && headAnchor != null && keyboardSystem != null)
        {
            // Yaw-only placement so the keyboard stays upright regardless of head pitch
            Vector3 forward = Vector3.ProjectOnPlane(headAnchor.forward, Vector3.up);
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;

            Vector3 position = headAnchor.position + forward * worldSetupDistance + Vector3.up * worldSetupHeight;
            keyboardSystem.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        }

        keyboardSystem.transform.GetPositionAndRotation(out var kbPos, out var kbRot);
        Vector3 kbEuler = kbRot.eulerAngles;
        DataLogger.Instance.Log("HAND_ANCHOR_WORLD_LOCK", placeInFrontOfHead ? "PlacedInFrontOfHead" : "FrozenInPlace",
            $"KbPos({kbPos.x:F3},{kbPos.y:F3},{kbPos.z:F3})|KbRot({kbEuler.x:F2},{kbEuler.y:F2},{kbEuler.z:F2})");
        AnchorModeChanged?.Invoke(currentAnchorMode);
    }

    /// <summary>
    /// Study 1 hand exploration, step 2 ("confirm"): anchors the keyboard to the holding hand,
    /// keeping the current relative pose between hand and keyboard.
    /// </summary>
    public bool ConfirmHandAnchorFromCurrentPose()
    {
        // A hand that is not tracked reports a stale / origin pose: refuse to anchor to it
        Transform anchor = ActiveHandAnchor;
        if (anchor != null && keyboardSystem != null && maxConfirmHandDistance > 0f)
        {
            float distance = Vector3.Distance(anchor.position, keyboardSystem.transform.position);
            if (distance > maxConfirmHandDistance)
            {
                DataLogger.Instance.Log("HAND_ANCHOR_CONFIRM", "Rejected", $"HandTooFar:{distance:F3}m|Max:{maxConfirmHandDistance:F3}m");
                Debug.LogWarning($"ConfirmHandAnchorFromCurrentPose: holding hand is {distance:F2} m away from the keyboard (not tracked?)");
                return false;
            }
        }

        currentAnchorMode = AnchorMode.Hand;
        return CaptureHandOffsetAndFollow("HAND_ANCHOR_CONFIRM", "Confirmed");
    }

    public float GetHeadAnchorDistance()
    {
        return headAnchorDistance;
    }

    public float GetHeadAnchorHeight()
    {
        return headAnchorHeight;
    }

    public void SetHeadAnchorDistance(float distance)
    {
        headAnchorDistance = Mathf.Max(0.05f, distance);
        DataLogger.Instance.Log("HEAD_ANCHOR_SET", "Distance", $"NewValue:{headAnchorDistance:F3}m");
    }

    public void SetHeadAnchorHeight(float height)
    {
        headAnchorHeight = height;
        DataLogger.Instance.Log("HEAD_ANCHOR_SET", "Height", $"NewValue:{headAnchorHeight:F3}m");
    }

    public void SetHeadAnchorTilt(float tilt)
    {
        headAnchorTiltAngle = Mathf.Clamp(tilt, -90f, 90f);
        DataLogger.Instance.Log("HEAD_ANCHOR_SET", "Tilt", $"NewValue:{headAnchorTiltAngle:F2}deg");
    }

    /// <summary>
    /// Sets local tilt angle of currently active keyboard (rotates keyboard only, without affecting entire system)
    /// Positive tilts upwards, negative tilts downwards
    /// </summary>
    /// <param name="localTilt">Local tilt angle (degrees)</param>
    public void SetActiveKeyboardLocalTilt(float localTilt)
    {
        if (ActiveKeyboard == null)
        {
            Debug.LogWarning("SetActiveKeyboardLocalTilt: No active keyboard");
            return;
        }

        // Rotate the active keyboard around its local X axis
        Transform keyboardTransform = ActiveKeyboard.transform;
        Vector3 currentEuler = keyboardTransform.localEulerAngles;
        keyboardTransform.localEulerAngles = new Vector3(localTilt, currentEuler.y, currentEuler.z);
        
        DataLogger.Instance.Log("KEYBOARD_LOCAL_TILT", "Set", $"Tilt:{localTilt:F2}deg");
    }

    /// <summary>
    /// Sets keyboard position pose (valid for hand anchor only)
    /// Head anchor uses distance, angle, height parameters and is unaffected by this method
    /// </summary>
    public void SetKeyboardPose(KeyboardPosition pose)
    {
        // Head anchor does not use pose; return directly
        if (currentAnchorMode == AnchorMode.Head)
        {
            Debug.LogWarning("SetKeyboardPose is invalid in head anchor mode. Please use headAnchorDistance, headAnchorTiltAngle, and headAnchorHeight parameters to adjust position.", this);
            return;
        }

        // Hand anchor: the beside pose of the running study, or the default beside pose
        KeyboardPose targetPose;
        if (currentStudyNumber == 1 && besidePoseStudy1 != null)
        {
            targetPose = besidePoseStudy1;
        }
        else if (currentStudyNumber == 2 && besidePoseStudy2 != null)
        {
            targetPose = besidePoseStudy2;
        }
        else if (currentStudyNumber == 3 && besidePoseStudy3 != null)
        {
            targetPose = besidePoseStudy3;
        }
        else
        {
            targetPose = besidePose;
        }

        if (targetPose != null)
        {
            positionOffset = targetPose.positionOffset;
            rotationOffset = Quaternion.Euler(targetPose.eulerRotationOffset);
            if (keyboardOnRightHand)
            {
                // Presets are authored for the left hand: mirror them across the hand's YZ plane,
                // then shift along the keyboard's own X so its centre lands on the mirrored spot
                rotationOffset = HandMirror.MirrorRotation(rotationOffset);
                positionOffset = HandMirror.MirrorPosition(positionOffset)
                                 + rotationOffset * new Vector3(-2f * GetMirrorPivotLocalX(), 0f, 0f);
            }
            currentPose = pose; // Record current pose
            hasCustomOffset = false; // Using preset pose, not custom offset
            
            // Log keyboard pose switch
            string offsetData = FormatHandAnchorData(positionOffset, rotationOffset);
            DataLogger.Instance.Log("KEYBOARD_POSE", $"{pose}", offsetData);
        }
    }

    /// <summary>
    /// Formats hand anchor data into a reportable string format
    /// </summary>
    private string FormatHandAnchorData(Vector3 posOffset, Quaternion rotOffset)
    {
        Vector3 euler = rotOffset.eulerAngles;
        // Calculate offset distance (for quick understanding of offset magnitude)
        float offsetDistance = posOffset.magnitude;
        return $"PosOffset({posOffset.x:F3},{posOffset.y:F3},{posOffset.z:F3})m|" +
               $"RotOffset({euler.x:F2},{euler.y:F2},{euler.z:F2})deg|" +
               $"OffsetDistance:{offsetDistance:F3}m";
    }

    /// <summary>
    /// Formats world coordinate data (for verification and reproduction)
    /// </summary>
    private string FormatWorldCoordinates(Vector3 handPos, Quaternion handRot, Vector3 kbPos, Quaternion kbRot)
    {
        Vector3 handEuler = handRot.eulerAngles;
        Vector3 kbEuler = kbRot.eulerAngles;
        float distance = Vector3.Distance(handPos, kbPos);
        return $"HandPos({handPos.x:F3},{handPos.y:F3},{handPos.z:F3})|" +
               $"HandRot({handEuler.x:F2},{handEuler.y:F2},{handEuler.z:F2})|" +
               $"KbPos({kbPos.x:F3},{kbPos.y:F3},{kbPos.z:F3})|" +
               $"KbRot({kbEuler.x:F2},{kbEuler.y:F2},{kbEuler.z:F2})|" +
               $"WorldDistance:{distance:F3}m";
    }

    /// <summary>
    /// Activates specified child keyboard and deactivates all other child keyboards.
    /// Preserves all current states (anchor mode, follow status, position offset, etc.) across keyboard switches.
    /// </summary>
    /// <param name="keyboardIndex">Index of keyboard to activate in childKeyboards list</param>
    public void ActivateChildKeyboard(int keyboardIndex)
    {
        if (keyboardIndex < 0 || keyboardIndex >= childKeyboards.Count)
        {
            Debug.LogError($"Invalid keyboard index: {keyboardIndex}", this);
            return;
        }

        // Iterate through all child keyboards: activate target, deactivate others
        for (int i = 0; i < childKeyboards.Count; i++)
        {
            bool shouldBeActive = (i == keyboardIndex);
            childKeyboards[i].gameObject.SetActive(shouldBeActive);
        }

        // Update reference to currently active keyboard
        ActiveKeyboard = childKeyboards[keyboardIndex];

        // The mirrored preset depends on where the active keyboard is centred, so re-apply it
        if (keyboardOnRightHand && currentAnchorMode == AnchorMode.Hand && !hasCustomOffset)
        {
            SetKeyboardPose(currentPose);
        }
        
        // Reset smoothing state to ensure new keyboard position is immediately calculated correctly
        // This ensures the new keyboard gets the correct position based on current state in next Update frame
        isFirstFrame = true;
        
        // If currently following, update position immediately once to ensure new keyboard gets correct position and rotation
        // This prevents the new keyboard from being in the wrong position before the next frame
        if (isFollowing)
        {
            Transform currentAnchor = null;
            if (currentAnchorMode == AnchorMode.Hand && ActiveHandAnchor != null)
            {
                currentAnchor = ActiveHandAnchor;
            }
            else if (currentAnchorMode == AnchorMode.Head && headAnchor != null)
            {
                currentAnchor = headAnchor;
            }
            
            if (currentAnchor != null)
            {
                currentAnchor.GetPositionAndRotation(out var anchorPosition, out var anchorRotation);
                previousPosition = anchorPosition;
                
                // Immediately apply position and rotation to ensure new keyboard gets correct state right away
                if (currentAnchorMode == AnchorMode.Head)
                {
                    UpdateHeadAnchoredPosition(anchorPosition, anchorRotation);
                }
                else
                {
                    keyboardSystem.transform.rotation = anchorRotation * rotationOffset;
                    Vector3 worldOffset = anchorRotation * positionOffset;
                    keyboardSystem.transform.position = anchorPosition + worldOffset;
                }
            }
        }
    }
}