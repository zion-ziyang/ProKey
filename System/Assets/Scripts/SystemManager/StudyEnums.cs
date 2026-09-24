public enum TestPhase
{
    Idle,
    FreePractice,
    Practice,
    Real
}

public enum KeyboardPosition
{
    Idle,
    Beside
}

public enum AnchorMode
{
    Hand,
    Head
}

/// <summary>
/// The participant's dominant (typing) hand.
/// Right: keyboard is anchored to the LEFT hand and the right index types (original behaviour).
/// Left:  keyboard is anchored to the RIGHT hand and the left index types.
/// </summary>
public enum DominantHand
{
    Right,
    Left
}

/// <summary>
/// Helpers to mirror a hand-local pose across the hand's YZ plane (left hand <-> right hand).
/// Valid because the OpenXR hand skeleton frames of both hands are mirror images of each other.
/// </summary>
public static class HandMirror
{
    public static UnityEngine.Vector3 MirrorPosition(UnityEngine.Vector3 p)
    {
        return new UnityEngine.Vector3(-p.x, p.y, p.z);
    }

    public static UnityEngine.Quaternion MirrorRotation(UnityEngine.Quaternion q)
    {
        return new UnityEngine.Quaternion(q.x, -q.y, -q.z, q.w);
    }
}

[System.Serializable]
public class KeyboardPose
{
    public UnityEngine.Vector3 positionOffset;
    public UnityEngine.Vector3 eulerRotationOffset;
}
