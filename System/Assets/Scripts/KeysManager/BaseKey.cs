using UnityEngine;

/// <summary>
/// Key animation mode.
/// </summary>
public enum AnimationMode
{
    Direct,     // Direct rising
    Anticipate, // Anticipation (sinks/retreats first, then rises)
}

public enum KeyType
{
    Alphanumeric, // For letters, numbers, space, and predicted words
    DeleteCommand, // For backspace/delete commands
    NextCommand
}

/// <summary>
/// Key internal animation state machine.
/// </summary>
public enum KeyAnimationState
{
    Idle,             // Idle
    WaitingForRepeat, // Waiting for next repeat input
    Anticipating,     // Anticipating (moving back)
    Rising,           // Rising
    Returning         // Returning to idle
}

/// <summary>
/// Zones above a ProKey key.
/// </summary>
public enum KeyZoneType
{
    Hover,       // Shows the activation zone
    Activation   // Starts the key motion
}

public abstract class BaseKey : MonoBehaviour
{
    [Header("Basic Configuration")]
    public string keyValue = "q";
    public KeyType keyType = KeyType.Alphanumeric;
    /// <summary>
    /// Resets the key to its initial, completely idle state.
    /// </summary>
    public abstract void ResetKey();

    /// <summary>
    /// Determines whether the key is currently in a completely idle state.
    /// </summary>
    public abstract bool IsIdle();
}