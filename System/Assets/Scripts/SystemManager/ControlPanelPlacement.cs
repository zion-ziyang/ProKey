using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places a control panel beside a keyboard, in the plane of the keyboard:
/// fixed gap between the keyboard's background plate and the panel's backplate, top edges aligned, same depth.
/// Used by StudyFlowManager every time the keyboard or the dominant hand changes; static so that
/// editor tooling can lay out the scene the same way.
/// </summary>
public static class ControlPanelPlacement
{
    /// <summary>Child of a keyboard that holds the background plate (the visible outline of the keyboard)</summary>
    public const string KeyboardPlateName = "Background";
    /// <summary>Child of a panel that holds the world-space canvas (its rect is the visible backplate)</summary>
    public const string PanelCanvasName = "CanvasRoot";

    private static readonly Vector3[] RectCorners = new Vector3[4];
    private static readonly List<MeshFilter> MeshFilters = new List<MeshFilter>();

    /// <summary>
    /// Moves (and rotates) the panel so that it sits beside the keyboard.
    /// leftSide = false: panel to the right of the keyboard. leftSide = true: panel to the left.
    /// Works on inactive objects. Returns false when the keyboard or the panel cannot be measured.
    /// </summary>
    public static bool Place(Transform panel, Transform keyboard, float gap, bool leftSide)
    {
        if (panel == null || keyboard == null) return false;

        // Keyboard plane: x = right, y = up along the keyboard, z = plane normal
        Quaternion plane = keyboard.rotation;
        panel.rotation = plane;

        if (!TryGetKeyboardExtents(keyboard, plane, out Bounds keyboardExtents)) return false;
        if (!TryGetPanelExtents(panel, plane, out Bounds panelExtents)) return false;

        Vector3 delta = Vector3.zero;
        delta.x = leftSide
            ? (keyboardExtents.min.x - gap) - panelExtents.max.x
            : (keyboardExtents.max.x + gap) - panelExtents.min.x;
        delta.y = keyboardExtents.max.y - panelExtents.max.y;
        delta.z = keyboardExtents.center.z - panelExtents.center.z;

        panel.position += plane * delta;
        return true;
    }

    /// <summary>
    /// Extents of the keyboard's background plate in plane coordinates (world space rotated by the inverse of 'plane').
    /// Falls back to every rendered mesh of the keyboard when it has no background plate.
    /// </summary>
    public static bool TryGetKeyboardExtents(Transform keyboard, Quaternion plane, out Bounds extents)
    {
        Transform plate = keyboard.Find(KeyboardPlateName);
        if (plate != null && TryGetMeshExtents(plate, plane, out extents)) return true;
        return TryGetMeshExtents(keyboard, plane, out extents);
    }

    /// <summary>
    /// Extents of the panel's canvas rect in plane coordinates
    /// </summary>
    public static bool TryGetPanelExtents(Transform panel, Quaternion plane, out Bounds extents)
    {
        extents = new Bounds();
        RectTransform canvas = panel.Find(PanelCanvasName) as RectTransform;
        if (canvas == null)
        {
            Canvas found = panel.GetComponentInChildren<Canvas>(true);
            canvas = found != null ? found.transform as RectTransform : null;
        }

        if (canvas == null) return false;

        Quaternion toPlane = Quaternion.Inverse(plane);
        canvas.GetWorldCorners(RectCorners);
        extents = new Bounds(toPlane * RectCorners[0], Vector3.zero);
        for (int i = 1; i < RectCorners.Length; i++) extents.Encapsulate(toPlane * RectCorners[i]);
        return true;
    }

    private static bool TryGetMeshExtents(Transform root, Quaternion plane, out Bounds extents)
    {
        extents = new Bounds();
        Quaternion toPlane = Quaternion.Inverse(plane);
        bool any = false;

        root.GetComponentsInChildren(true, MeshFilters);
        foreach (MeshFilter filter in MeshFilters)
        {
            if (filter.sharedMesh == null || filter.GetComponent<Renderer>() == null) continue;

            Bounds mesh = filter.sharedMesh.bounds;
            Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 point = toPlane * toWorld.MultiplyPoint3x4(corner);
                if (any) extents.Encapsulate(point);
                else extents = new Bounds(point, Vector3.zero);
                any = true;
            }
        }

        MeshFilters.Clear();
        return any;
    }
}
