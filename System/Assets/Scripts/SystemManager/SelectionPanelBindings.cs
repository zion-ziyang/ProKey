using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Connects the toggle-based buttons (ControllerPanel style) of a selection panel to the StudyFlowManager:
/// study selection, day selection (Study 3) and dominant hand selection.
/// </summary>
public class SelectionPanelBindings : MonoBehaviour
{
    public enum SelectionAction
    {
        SelectStudy,     // value = study number (1-3)
        SelectDay,       // value = day (1-3)
        SelectLeftHand,  // dominant hand = left  -> keyboard on the right hand
        SelectRightHand, // dominant hand = right -> keyboard on the left hand
        Back,            // previous selection step
        StartDemo        // ProKey demo (appended last: the scene stores these values by index)
    }

    [Serializable]
    public class Entry
    {
        public Toggle button;
        public SelectionAction action;
        public int value;
    }

    [SerializeField] private StudyFlowManager studyFlowManager;
    [SerializeField] private List<Entry> entries = new List<Entry>();
    [Tooltip("Presses are ignored for this long after the panel appears, so the finger that pressed " +
             "the previous panel does not immediately press a button of this one")]
    [SerializeField] private float activationGuardSeconds = 0.4f;

    private readonly List<UnityAction<bool>> listeners = new List<UnityAction<bool>>();
    private float enabledTime;

    private void Awake()
    {
        if (studyFlowManager == null) studyFlowManager = FindObjectOfType<StudyFlowManager>();

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            UnityAction<bool> listener = _ => OnButtonChanged(entry);
            listeners.Add(listener);
            if (entry.button != null) entry.button.onValueChanged.AddListener(listener);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < entries.Count && i < listeners.Count; i++)
        {
            if (entries[i].button != null) entries[i].button.onValueChanged.RemoveListener(listeners[i]);
        }
    }

    private void OnEnable()
    {
        enabledTime = Time.unscaledTime;
        ResetToggles();
    }

    private void ResetToggles()
    {
        foreach (var entry in entries)
        {
            if (entry.button != null) entry.button.SetIsOnWithoutNotify(false);
        }
    }

    private void OnButtonChanged(Entry entry)
    {
        // The toggles are used as push buttons: every value change is a click, and the toggle never stays on
        if (entry.button != null) entry.button.SetIsOnWithoutNotify(false);

        if (Time.unscaledTime - enabledTime < activationGuardSeconds) return;
        if (studyFlowManager == null)
        {
            Debug.LogError("[SelectionPanelBindings] StudyFlowManager is not assigned", this);
            return;
        }

        switch (entry.action)
        {
            case SelectionAction.SelectStudy:
                studyFlowManager.OnStudySelected(entry.value);
                break;
            case SelectionAction.SelectDay:
                studyFlowManager.OnDaySelected(entry.value);
                break;
            case SelectionAction.SelectLeftHand:
                studyFlowManager.OnLeftHandSelected();
                break;
            case SelectionAction.SelectRightHand:
                studyFlowManager.OnRightHandSelected();
                break;
            case SelectionAction.Back:
                studyFlowManager.OnSelectionBack();
                break;
            case SelectionAction.StartDemo:
                studyFlowManager.OnDemoSelected();
                break;
        }
    }
}
