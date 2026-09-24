using UnityEngine;

/// <summary>
/// Script allowing SizePanel to independently follow hand movement
/// Independent of the keyboard system; does not move along with the keyboard
/// </summary>
public class SizePanelHandFollower : MonoBehaviour
{
    [Header("Hand Anchor")]
    [Tooltip("Hand tracking anchor (usually Palm or Wrist Transform)")]
    [SerializeField] private Transform handAnchor;

    [Tooltip("Anchor used for left-handed participants (panel follows the RIGHT hand). Offsets below are authored for 'Hand Anchor' and get mirrored.")]
    [SerializeField] private Transform rightHandAnchor;

    [Header("Position Offset")]
    [Tooltip("Position offset relative to hand (in hand local coordinate space)")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0.15f, 0.1f, 0.05f);
    
    [Tooltip("Rotation offset relative to hand (Euler angles)")]
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 0f, 0f);

    [Header("Follow Settings")]
    [Tooltip("Whether to enable following")]
    [SerializeField] private bool isFollowing = true;
    
    [Tooltip("Position smoothing factor, smaller is smoother but higher latency. Recommended between 0.1 and 0.3.")]
    [SerializeField][Range(0.01f, 1.0f)] private float smoothingFactor = 0.15f;

    [Header("Debug Options")]
    [Tooltip("Display debug information in Scene view")]
    [SerializeField] private bool showDebugGizmos = true;

    // Private variables
    private Vector3 previousPosition;
    private bool isFirstFrame = true;
    private Quaternion rotationOffset;
    private bool followRightHand = false;

    private Transform ActiveHandAnchor
    {
        get { return (followRightHand && rightHandAnchor != null) ? rightHandAnchor : handAnchor; }
    }

    /// <summary>
    /// Selects which hand the panel follows. When following the right hand, the offsets
    /// (authored for the left hand) are mirrored across the hand's YZ plane.
    /// </summary>
    public void SetFollowRightHand(bool useRightHand)
    {
        if (useRightHand && rightHandAnchor == null)
        {
            Debug.LogWarning("SetFollowRightHand: rightHandAnchor not set, panel stays on the default hand", this);
        }

        followRightHand = useRightHand;
        isFirstFrame = true;
    }

    void Start()
    {
        // Convert Euler angles to Quaternion
        rotationOffset = Quaternion.Euler(rotationOffsetEuler);
        
        // Initialize smoothing state
        if (handAnchor != null && isFollowing)
        {
            handAnchor.GetPositionAndRotation(out var anchorPosition, out var anchorRotation);
            previousPosition = anchorPosition;
        }
    }

    /// <summary>
    /// Using LateUpdate to ensure this runs AFTER KeyboardController's Update
    /// This is crucial because HeadAnchorPanel is a child of KeyboardSystem,
    /// and we need to set the world position after KeyboardSystem has moved.
    /// </summary>
    void LateUpdate()
    {
        Transform anchor = ActiveHandAnchor;
        if (!isFollowing || anchor == null)
        {
            return;
        }

        // Get current position and rotation of the hand
        anchor.GetPositionAndRotation(out var anchorPosition, out var anchorRotation);

        // Offsets are authored for the left hand; mirror them when following the right hand
        bool mirrored = followRightHand && rightHandAnchor != null;
        Vector3 activePositionOffset = mirrored ? HandMirror.MirrorPosition(positionOffset) : positionOffset;
        Quaternion activeRotationOffset = mirrored ? HandMirror.MirrorRotation(rotationOffset) : rotationOffset;

        // Position smoothing
        if (isFirstFrame)
        {
            previousPosition = anchorPosition;
            isFirstFrame = false;
        }
        
        Vector3 smoothedPosition = Vector3.Lerp(previousPosition, anchorPosition, smoothingFactor);
        previousPosition = smoothedPosition;

        // Calculate final position and rotation
        // Position: hand position + offset in hand local coordinates transformed to world coordinates
        Vector3 worldOffset = anchorRotation * activePositionOffset;
        transform.position = smoothedPosition + worldOffset;

        // Rotation: hand rotation * rotation offset
        transform.rotation = anchorRotation * activeRotationOffset;
    }

    void OnValidate()
    {
        // Update rotation offset when Inspector values change
        rotationOffset = Quaternion.Euler(rotationOffsetEuler);
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos || handAnchor == null)
        {
            return;
        }

        // Draw hand position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(handAnchor.position, 0.02f);

        // Draw panel position
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.02f);

        // Draw connecting line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(handAnchor.position, transform.position);

        // Draw hand forward direction
        Gizmos.color = Color.red;
        Gizmos.DrawRay(handAnchor.position, handAnchor.forward * 0.1f);
    }
}
