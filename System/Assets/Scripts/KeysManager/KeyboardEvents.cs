using System;

/// <summary>
/// A static event hub for handling all keyboard-related global events.
/// This acts as a neutral "broadcasting station": any component can call its methods to broadcast events,
/// and any component can subscribe to its events to receive broadcasts.
/// </summary>
public static class KeyboardEvents
{
    // --- Event Definitions ---
    public static event Action<string> OnKeyPressed;
    public static event Action OnDeleteCommandPressed;
    public static event Action OnNextCommandPressed;

    // --- Public Safe Broadcasting Methods ---

    /// <summary>
    /// Broadcasts an event indicating a key was pressed.
    /// </summary>
    /// <param name="keyValue">The key value that was pressed</param>
    public static void KeyPressed(string keyValue)
    {
        OnKeyPressed?.Invoke(keyValue);
    }

    /// <summary>
    /// Broadcasts an event indicating a delete command was pressed.
    /// </summary>
    public static void DeleteCommandPressed()
    {
        OnDeleteCommandPressed?.Invoke();
    }

    public static void NextCommandPressed()
    {
        OnNextCommandPressed?.Invoke();
    }
}