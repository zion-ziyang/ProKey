using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A step of the theta/tilt slider: the angle shown to the user and the tilt applied to the keyboard system
/// </summary>
[System.Serializable]
public struct ThetaPreset
{
    public string name;
    [Tooltip("Display angle shown to user (e.g., 0° for flat, 90° for perpendicular)")]
    public float displayAngle;
    public float realAngle;
}

/// <summary>
/// Bindings of the Study 1 adjust panels (HandAdjustPanel / HeadAdjustPanel): the head-anchor sliders
/// (distance, height, tilt; head panel only) and the 2-finger / 10-finger toggles.
/// </summary>
public class SizePanelBindings : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KeyboardController keyboardController;
    [SerializeField] private StudyFlowManager studyFlowManager;
    [SerializeField] private Slider distanceSlider;
    [SerializeField] private Slider heightSlider;
    [SerializeField] private Slider thetaSlider;
    [SerializeField] private Toggle finger10Toggle;
    [SerializeField] private Toggle finger2Toggle;

    [Header("Value Display Texts")]
    [SerializeField] private TMP_Text distanceValueText;
    [SerializeField] private TMP_Text heightValueText;
    [SerializeField] private TMP_Text thetaValueText;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindComponents = true;
    [SerializeField] private string distanceControlPath = "DistanceControl";
    [SerializeField] private string heightControlPath = "HeightControl";
    [SerializeField] private string thetaControlPath = "ThetaControl";

    [Header("Slider Settings")]
    [Tooltip("Distance slider: 3-7, default 5 (maps to 0.3-0.7, step 0.1, center 0.5)")]
    [SerializeField] private float distanceBaseValue = 0.5f;
    [SerializeField] private float distanceStep = 0.1f;
    [SerializeField] private int distanceSliderCenter = 5;

    [Tooltip("Height slider: -1 to 3, default 0 (maps to -0.25 to -0.05, step 0.1, center -0.2)")]
    [SerializeField] private float heightBaseValue = -0.15f;
    [SerializeField] private float heightStep = 0.05f;
    [SerializeField] private int heightSliderCenter = 0;

    [Header("Theta/Tilt Presets")]
    [Tooltip("Theta presets: slider 0=flat (under preview), slider 2=default (45°), slider 5=perpendicular (90°)")]
    [SerializeField] private ThetaPreset[] thetaPresets = new ThetaPreset[]
    {
        new ThetaPreset { name = "Flat (0°)", displayAngle = 15f, realAngle = -30f },
        new ThetaPreset { name = "30°", displayAngle = 30f, realAngle = -15f },
        new ThetaPreset { name = "45° (Default)", displayAngle = 45f, realAngle = 0f },
        new ThetaPreset { name = "60°", displayAngle = 60f, realAngle = 15f },
        new ThetaPreset { name = "Perpendicular (90°)", displayAngle = 75f, realAngle = 30f }
    };
    [SerializeField] private int thetaSliderDefault = 2;

    // Toggle group for radio button behavior
    private ToggleGroup fingerToggleGroup;

    // Flag to track if defaults have been set (only set once on first enable)
    private bool defaultsInitialized = false;

    private void Awake()
    {
        if (autoFindComponents)
        {
            AutoFindComponents();
        }

        // Create toggle group for radio button behavior
        SetupToggleGroups();
    }

    private void AutoFindComponents()
    {
        // Find sliders and value texts by searching in child transforms
        Transform distanceControl = FindChildByName(transform, distanceControlPath);
        if (distanceControl != null)
        {
            if (distanceSlider == null)
            {
                distanceSlider = distanceControl.GetComponentInChildren<Slider>(true);
            }
            if (distanceValueText == null)
            {
                Transform valueObj = distanceControl.Find("Value");
                if (valueObj != null)
                {
                    distanceValueText = valueObj.GetComponent<TMP_Text>();
                }
            }
        }

        Transform heightControl = FindChildByName(transform, heightControlPath);
        if (heightControl != null)
        {
            if (heightSlider == null)
            {
                heightSlider = heightControl.GetComponentInChildren<Slider>(true);
            }
            if (heightValueText == null)
            {
                Transform valueObj = heightControl.Find("Value");
                if (valueObj != null)
                {
                    heightValueText = valueObj.GetComponent<TMP_Text>();
                }
            }
        }

        Transform thetaControl = FindChildByName(transform, thetaControlPath);
        if (thetaControl != null)
        {
            if (thetaSlider == null)
            {
                thetaSlider = thetaControl.GetComponentInChildren<Slider>(true);
            }
            if (thetaValueText == null)
            {
                Transform valueObj = thetaControl.Find("Value");
                if (valueObj != null)
                {
                    thetaValueText = valueObj.GetComponent<TMP_Text>();
                }
            }
        }

        // Find finger toggles
        if (finger10Toggle == null)
        {
            Transform finger10 = FindChildByName(transform, "Toggle_10_finger");
            if (finger10 != null)
            {
                finger10Toggle = finger10.GetComponentInChildren<Toggle>(true);
            }
        }

        if (finger2Toggle == null)
        {
            Transform finger2 = FindChildByName(transform, "Toggle_2_finger");
            if (finger2 != null)
            {
                finger2Toggle = finger2.GetComponentInChildren<Toggle>(true);
            }
        }
    }

    private Transform FindChildByName(Transform parent, string name)
    {
        // Search recursively through all children
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }
        return null;
    }

    private void SetupToggleGroups()
    {
        // Create the toggle group on a child GameObject (it needs to be on a GameObject)
        GameObject toggleGroupHolder = new GameObject("ToggleGroups");
        toggleGroupHolder.transform.SetParent(transform, false);

        // Finger toggle group
        GameObject fingerGroupObj = new GameObject("FingerToggleGroup");
        fingerGroupObj.transform.SetParent(toggleGroupHolder.transform, false);
        fingerToggleGroup = fingerGroupObj.AddComponent<ToggleGroup>();
        fingerToggleGroup.allowSwitchOff = false; // Radio behavior - one must always be selected

        // Assign toggles to their group
        if (finger10Toggle != null) finger10Toggle.group = fingerToggleGroup;
        if (finger2Toggle != null) finger2Toggle.group = fingerToggleGroup;
    }

    private void OnEnable()
    {
        // Register slider listeners
        if (distanceSlider != null)
        {
            distanceSlider.onValueChanged.AddListener(HandleDistanceChanged);
        }

        if (heightSlider != null)
        {
            heightSlider.onValueChanged.AddListener(HandleHeightChanged);
        }

        if (thetaSlider != null)
        {
            thetaSlider.onValueChanged.AddListener(HandleThetaChanged);
        }

        // Register finger toggle listeners
        if (finger10Toggle != null)
        {
            finger10Toggle.onValueChanged.AddListener(HandleFinger10Toggle);
        }

        if (finger2Toggle != null)
        {
            finger2Toggle.onValueChanged.AddListener(HandleFinger2Toggle);
        }

        // Sync sliders only if in head anchor mode (to avoid conflicts with hand anchor)
        if (keyboardController != null && keyboardController.CurrentAnchorMode == AnchorMode.Head)
        {
            SyncSlidersFromController();
        }

        // Update value displays to show initial values
        UpdateAllValueDisplays();

        // Set default toggles only once on first enable
        if (!defaultsInitialized)
        {
            SetDefaultToggles();
            defaultsInitialized = true;
        }
    }

    private void OnDisable()
    {
        // Remove slider listeners
        if (distanceSlider != null)
        {
            distanceSlider.onValueChanged.RemoveListener(HandleDistanceChanged);
        }

        if (heightSlider != null)
        {
            heightSlider.onValueChanged.RemoveListener(HandleHeightChanged);
        }

        if (thetaSlider != null)
        {
            thetaSlider.onValueChanged.RemoveListener(HandleThetaChanged);
        }

        // Remove finger toggle listeners
        if (finger10Toggle != null)
        {
            finger10Toggle.onValueChanged.RemoveAllListeners();
        }

        if (finger2Toggle != null)
        {
            finger2Toggle.onValueChanged.RemoveAllListeners();
        }
    }

    private void SyncSlidersFromController()
    {
        if (keyboardController == null)
        {
            return;
        }

        // Sync distance slider
        if (distanceSlider != null)
        {
            float distance = keyboardController.GetHeadAnchorDistance();
            int sliderValue = DistanceToSliderValue(distance);
            distanceSlider.SetValueWithoutNotify(sliderValue);
        }

        // Sync height slider
        if (heightSlider != null)
        {
            float height = keyboardController.GetHeadAnchorHeight();
            int sliderValue = HeightToSliderValue(height);
            heightSlider.SetValueWithoutNotify(sliderValue);
        }

        // Sync theta slider - just set the slider value, don't apply transform presets on sync
        // Transform presets are only applied when user actively changes the slider
        // This prevents resetting keyboard angle when panel first opens
        if (thetaSlider != null)
        {
            thetaSlider.SetValueWithoutNotify(thetaSliderDefault);
            // Update the display only, don't apply transforms
            if (thetaPresets.Length > thetaSliderDefault)
            {
                UpdateThetaValueDisplay(thetaPresets[thetaSliderDefault].displayAngle);
            }
        }
    }

    // Distance mapping: slider value (3-7) to actual distance
    private float SliderToDistance(float sliderValue)
    {
        // slider 5 = 0.5, each step = 0.02
        return distanceBaseValue + (sliderValue - distanceSliderCenter) * distanceStep;
    }

    private int DistanceToSliderValue(float distance)
    {
        // Reverse mapping: distance to slider value
        return Mathf.RoundToInt((distance - distanceBaseValue) / distanceStep + distanceSliderCenter);
    }

    // Height mapping: slider value (-2 to 2) to actual height
    private float SliderToHeight(float sliderValue)
    {
        // slider 0 = -0.2, each step = 0.02
        return heightBaseValue + (sliderValue - heightSliderCenter) * heightStep;
    }

    private int HeightToSliderValue(float height)
    {
        // Reverse mapping: height to slider value
        return Mathf.RoundToInt((height - heightBaseValue) / heightStep + heightSliderCenter);
    }

    // Theta mapping: slider value (0-5) to preset index
    private int SliderToPresetIndex(float sliderValue)
    {
        return Mathf.Clamp(Mathf.RoundToInt(sliderValue), 0, thetaPresets.Length - 1);
    }

    private ThetaPreset GetThetaPreset(float sliderValue)
    {
        int index = SliderToPresetIndex(sliderValue);
        return thetaPresets[index];
    }

    private void SetDefaultToggles()
    {
        // Default: finger = 2-finger
        //
        // IMPORTANT: We must use .isOn = true (not SetIsOnWithoutNotify) for the selected toggle
        // so the ToggleGroup properly handles mutual exclusion. But first turn all OFF to avoid
        // multiple toggles being ON simultaneously.
        if (finger10Toggle != null) finger10Toggle.SetIsOnWithoutNotify(false);
        if (finger2Toggle != null) finger2Toggle.SetIsOnWithoutNotify(false);
        // Then turn on the default (this properly notifies the toggle group)
        if (finger2Toggle != null) finger2Toggle.isOn = true;

        // Also actually set the finger mode to 2-finger (hide extra finger tips)
        if (studyFlowManager != null)
        {
            studyFlowManager.SetFingerMode(false); // 2-finger mode (index fingers only)
        }
        else if (keyboardController != null)
        {
            keyboardController.SetFullHandMode(false);
        }

        Debug.Log("[SizePanelBindings] Default toggles set: Finger=2");
    }

    private void HandleDistanceChanged(float sliderValue)
    {
        float distance = SliderToDistance(sliderValue);
        UpdateDistanceValueDisplay(distance);

        if (keyboardController == null)
        {
            return;
        }

        keyboardController.SetHeadAnchorDistance(distance);
        Debug.Log($"[SizePanelBindings] Distance slider: {sliderValue} → {distance:F3}m");
    }

    private void HandleHeightChanged(float sliderValue)
    {
        float height = SliderToHeight(sliderValue);
        UpdateHeightValueDisplay(height);

        if (keyboardController == null)
        {
            return;
        }

        keyboardController.SetHeadAnchorHeight(height);
        Debug.Log($"[SizePanelBindings] Height slider: {sliderValue} → {height:F3}m");
    }

    private void HandleThetaChanged(float sliderValue)
    {
        ThetaPreset preset = GetThetaPreset(sliderValue);
        UpdateThetaValueDisplay(preset.displayAngle);

        // Use whole-system rotation via headAnchorTiltAngle
        // This rotates the entire KeyboardSystem (Textboard, Preview, and Keyboard together)
        if (keyboardController != null)
        {
            keyboardController.SetHeadAnchorTilt(preset.realAngle);
        }

        Debug.Log($"[SizePanelBindings] Theta slider: {sliderValue} → {preset.name} ({preset.displayAngle}°) - whole system rotation");
    }

    // Value display update methods
    private void UpdateDistanceValueDisplay(float distance)
    {
        if (distanceValueText != null)
        {
            distanceValueText.text = $"{distance:F2}m";
        }
    }

    private void UpdateHeightValueDisplay(float height)
    {
        if (heightValueText != null)
        {
            heightValueText.text = $"{height:F2}m";
        }
    }

    private void UpdateThetaValueDisplay(float displayAngle)
    {
        if (thetaValueText != null)
        {
            // displayAngle is already the user-facing angle (0° to 90°)
            thetaValueText.text = $"{displayAngle:F0}°";
        }
    }

    private void UpdateAllValueDisplays()
    {
        if (distanceSlider != null)
        {
            UpdateDistanceValueDisplay(SliderToDistance(distanceSlider.value));
        }
        if (heightSlider != null)
        {
            UpdateHeightValueDisplay(SliderToHeight(heightSlider.value));
        }
        if (thetaSlider != null)
        {
            ThetaPreset preset = GetThetaPreset(thetaSlider.value);
            UpdateThetaValueDisplay(preset.displayAngle);
        }
    }

    // Finger toggle handlers - uses StudyFlowManager to also toggle finger tip visibility
    // Explicitly deselect other finger toggle to ensure proper mutual exclusion
    private void HandleFinger10Toggle(bool isOn)
    {
        if (!isOn) return;

        // Explicitly turn off other finger toggle
        if (finger2Toggle != null) finger2Toggle.SetIsOnWithoutNotify(false);

        // Use StudyFlowManager if available (handles both mode and finger tip visibility)
        if (studyFlowManager != null)
        {
            studyFlowManager.SetFingerMode(true); // 10-finger mode
        }
        else if (keyboardController != null)
        {
            // Fallback to just setting the mode
            keyboardController.SetFullHandMode(true);
        }
        Debug.Log("[SizePanelBindings] Finger mode changed to: 10-finger");
    }

    private void HandleFinger2Toggle(bool isOn)
    {
        if (!isOn) return;

        // Explicitly turn off other finger toggle
        if (finger10Toggle != null) finger10Toggle.SetIsOnWithoutNotify(false);

        // Use StudyFlowManager if available (handles both mode and finger tip visibility)
        if (studyFlowManager != null)
        {
            studyFlowManager.SetFingerMode(false); // 2-finger mode
        }
        else if (keyboardController != null)
        {
            // Fallback to just setting the mode
            keyboardController.SetFullHandMode(false);
        }
        Debug.Log("[SizePanelBindings] Finger mode changed to: 2-finger");
    }
}
