using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Typed text of the preview. One TextMeshPro slot per visible character (the content0..N objects of
/// Preview/UserInput/MessageList, each with its own caret object), laid out with a measured width per character.
/// The text itself is not limited. When it is wider than the active preview background, the line scrolls to the left
/// so that its tail and the caret stay visible, like a single-line text field; the hidden head is still part of GetCurrentText().
/// </summary>
public class CustomInputDisplay : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("TextMeshPro components of the Content objects (one character each)")]
    [SerializeField] private List<TMP_Text> charDisplaySlots;
    [Tooltip("Caret objects, one per Content object (a child of the slot, so it moves with it)")]
    [SerializeField] private List<GameObject> charIndicators;

    [Header("Layout Parameters")]
    [Tooltip("Left edge of the text in the space of the slots' parent (metres)")]
    [SerializeField] private float startX = -0.13f;
    [Tooltip("Preview backgrounds (S and L). The active one bounds the visible text: the right margin equals the left margin, " +
             "i.e. the distance from the background's left edge to startX.")]
    [SerializeField] private List<Transform> boardBackgrounds;
    [Tooltip("Visible width in metres when no background is active or assigned (the small background minus the margins)")]
    [SerializeField] private float fallbackVisibleWidth = 0.307f;

    // --- Private State Variables ---
    private Dictionary<char, float> charWidthMap;
    private readonly List<char> typedChars = new List<char>();
    // Left edge of each character relative to startX, before scrolling (= width of the characters before it)
    private readonly List<float> charOffsets = new List<float>();
    private float currentTotalWidth = 0.0f;
    private bool warnedNoBackground;

    void Start()
    {
        InitializeCharWidthMap();
        Clear();
    }

    private void InitializeCharWidthMap()
    {
        charWidthMap = new Dictionary<char, float>
        {
             {'a', 0.006f}, {'b', 0.0062f}, {'c', 0.00575f}, {'d', 0.0062f},
             {'e', 0.0059f}, {'f', 0.0039f}, {'g', 0.0063f}, {'h', 0.0061f},
             {'i', 0.0028f}, {'j', 0.0028f}, {'k', 0.00575f}, {'l', 0.0028f},
             {'m', 0.0096f}, {'n', 0.0061f}, {'o', 0.0063f}, {'p', 0.0062f},
             {'q', 0.0062f}, {'r', 0.0039f}, {'s', 0.00565f}, {'t', 0.0037f},
             {'u', 0.0061f}, {'v', 0.00545f}, {'w', 0.0082f}, {'x', 0.0055f},
             {'y', 0.00535f}, {'z', 0.0055f}, {' ', 0.004f}
        };
    }

    private float GetCharWidth(char c)
    {
        if (charWidthMap == null) InitializeCharWidthMap();
        return charWidthMap.ContainsKey(char.ToLower(c)) ? charWidthMap[char.ToLower(c)] : 0.004f;
    }

    public void AddChar(char c)
    {
        charOffsets.Add(currentTotalWidth);
        typedChars.Add(c);
        currentTotalWidth += GetCharWidth(c);
        Relayout();
    }

    public void DeleteLastChar()
    {
        if (typedChars.Count == 0) return;

        int last = typedChars.Count - 1;
        currentTotalWidth = charOffsets[last];
        typedChars.RemoveAt(last);
        charOffsets.RemoveAt(last);
        Relayout();
    }

    public void Clear()
    {
        typedChars.Clear();
        charOffsets.Clear();
        currentTotalWidth = 0.0f;
        Relayout();
    }

    public string GetCurrentText()
    {
        return new string(typedChars.ToArray());
    }

    /// <summary>
    /// Fills the slots from the typed text. Slot 0 shows the first visible character, the slot after the last
    /// character carries the caret, every other slot is empty and parked at startX.
    /// </summary>
    private void Relayout()
    {
        if (charDisplaySlots == null || charDisplaySlots.Count == 0) return;

        float visibleWidth = GetVisibleWidth();
        // > 0: the text is wider than the board, show its tail
        float scroll = Mathf.Max(0f, currentTotalWidth - visibleWidth);

        // First character that is completely inside the visible window
        int first = 0;
        while (first < typedChars.Count && charOffsets[first] < scroll - 0.0001f) first++;

        // One slot stays free for the caret
        int maxVisible = charDisplaySlots.Count - 1;
        if (typedChars.Count - first > maxVisible) first = typedChars.Count - maxVisible;

        int slot = 0;
        for (int i = first; i < typedChars.Count; i++, slot++)
        {
            SetSlot(slot, typedChars[i].ToString(), startX + charOffsets[i] - scroll);
        }

        int caretSlot = slot;
        SetSlot(caretSlot, "", startX + currentTotalWidth - scroll);
        for (slot = caretSlot + 1; slot < charDisplaySlots.Count; slot++)
        {
            SetSlot(slot, "", startX);
        }

        UpdateActiveIndicator(caretSlot);
    }

    private void SetSlot(int index, string text, float x)
    {
        TMP_Text slot = charDisplaySlots[index];
        if (slot == null) return;

        if (slot.text != text) slot.text = text;

        RectTransform rect = slot.rectTransform;
        if (rect != null && !Mathf.Approximately(rect.anchoredPosition.x, x))
        {
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
        }
    }

    /// <summary>
    /// Width available for the text: from startX to the right edge of the active background, minus the same margin
    /// the text keeps to the left edge. Measured in the space of the slots' parent, so it is valid in any keyboard pose.
    /// </summary>
    private float GetVisibleWidth()
    {
        Transform board = ActiveBackground();
        if (board == null)
        {
            if (!warnedNoBackground)
            {
                warnedNoBackground = true;
                Debug.LogWarning($"CustomInputDisplay: no active preview background assigned, using {fallbackVisibleWidth:F3} m of visible width", this);
            }
            return fallbackVisibleWidth;
        }

        Transform space = charDisplaySlots[0] != null && charDisplaySlots[0].transform.parent != null
            ? charDisplaySlots[0].transform.parent
            : transform;

        MeshFilter filter = board.GetComponent<MeshFilter>();
        Bounds mesh = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        float left = space.InverseTransformPoint(board.TransformPoint(new Vector3(mesh.min.x, mesh.center.y, mesh.center.z))).x;
        float right = space.InverseTransformPoint(board.TransformPoint(new Vector3(mesh.max.x, mesh.center.y, mesh.center.z))).x;

        float margin = Mathf.Max(0f, startX - left);
        return Mathf.Max(0.01f, right - margin - startX);
    }

    private Transform ActiveBackground()
    {
        if (boardBackgrounds == null) return null;
        foreach (Transform background in boardBackgrounds)
        {
            // activeSelf: the whole keyboard system may be hidden while the backgrounds are switched
            if (background != null && background.gameObject.activeSelf) return background;
        }
        return null;
    }

    private void UpdateActiveIndicator(int caretSlot)
    {
        if (charIndicators == null) return;
        for (int i = 0; i < charIndicators.Count; i++)
        {
            var indicator = charIndicators[i];
            if (indicator != null)
            {
                var renderer = indicator.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = (i == caretSlot);
                else indicator.SetActive(i == caretSlot); // Fallback option
            }
        }
    }
}
