using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A simple component attached to the root object of each keyboard Prefab,
/// used to provide references to keyboard-specific components (such as prediction keys).
/// </summary>
public class Keyboard : MonoBehaviour
{
    [Tooltip("Parent container for this keyboard's word prediction keys")]
    public GameObject predictionContainer;

    [Tooltip("Drag all prediction keys for this keyboard here")]
    public List<BaseKey> predictionKeys;
}