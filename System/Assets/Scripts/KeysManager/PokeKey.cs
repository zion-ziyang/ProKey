using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.Events;

public class PokeKey : BaseKey
{
    [Tooltip("Moving part of the key (child 'Collider'): carries the press collider, the face and the label")]
    public GameObject keyModel;
    [Tooltip("Renderer of the visible key face (child 'Face' of the key model)")]
    public MeshRenderer keyFace;

    [Header("Feedback Effects")]
    public Material pressedMaterial;
    public Material idleMaterial;
    public Material hoverMaterial;
    public AudioClip pressSound;
    [Tooltip("Depth of key depression")]
    public float pressDepth = -0.01f;
    [Tooltip("Key animation speed")]
    public float animationSpeed = 20f;

    [Header("Continuous Input Parameters")]
    [Tooltip("Whether to enable key repeat")]
    public bool enableRepeat = false;
    [Tooltip("[If repeat enabled] Delay before first repeated input after initial press (seconds)")]
    public float repeatDelay = 0.5f;
    [Tooltip("[If repeat enabled] Interval between subsequent repeated inputs (seconds)")]
    public float repeatInterval = 0.1f;
    [Header("Events")]
    public UnityEvent onPress;
    public UnityEvent onRelease;
    // --- Private Variables ---
    private AudioSource audioSource;
    private Vector3 initialPosition;
    private Vector3 pressedPosition;

    private bool isPressed = false;
    private Coroutine repeatCoroutine;
    private Coroutine animationCoroutine;

    void Start()
    {
        if (keyModel == null || keyFace == null)
        {
            Debug.LogError($"PokeKey '{keyValue}': keyModel or keyFace not specified!", this);
            return;
        }

        if (idleMaterial != null)
        {
            keyFace.material = idleMaterial;
        }

        initialPosition = keyModel.transform.localPosition;
        // Use local Z-axis to define depression direction for greater generality
        pressedPosition = initialPosition - new Vector3(0, 0, pressDepth);

        audioSource = GetComponentInParent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        GetComponentInChildren<TMP_Text>().text = keyValue;
    }

    #region --- Core Trigger Logic ---

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("poker") && !isPressed)
        {
            isPressed = true;

            // Trigger first press event
            TriggerKeyPress();

            // Start press animation
            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
            animationCoroutine = StartCoroutine(AnimatePressDown());

            if (enableRepeat)
            {
                if (repeatCoroutine != null) StopCoroutine(repeatCoroutine);
                repeatCoroutine = StartCoroutine(RepeatInputRoutine());
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("poker") && isPressed)
        {
            isPressed = false;

            // Stop repeat coroutine (if running)
            if (repeatCoroutine != null)
            {
                StopCoroutine(repeatCoroutine);
                repeatCoroutine = null;
            }

            // Start rebound animation
            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
            animationCoroutine = StartCoroutine(AnimateReleaseUp());

            onRelease?.Invoke();
        }
    }

    public void OnFingerEnterZone()
    {
        keyFace.material = hoverMaterial;
    }

    public void OnFingerLeaveZone()
    {
        keyFace.material = idleMaterial;
    }

    #endregion

    #region --- Coroutines ---

    // Press animation
    private IEnumerator AnimatePressDown()
    {
        if (pressedMaterial != null)
        {
            keyFace.material = pressedMaterial;
        }
        yield return MoveToPosition(pressedPosition);
    }

    // Rebound animation
    private IEnumerator AnimateReleaseUp()
    {
        yield return MoveToPosition(initialPosition);
        if (idleMaterial != null)
        {
            keyFace.material = idleMaterial;
        }
    }

    // Key repeat coroutine
    private IEnumerator RepeatInputRoutine()
    {
        // First input already triggered in OnTriggerEnter; wait briefly here before looping
        yield return new WaitForSeconds(repeatDelay);

        while (true)
        {
            TriggerKeyPress(); // Repeatedly trigger input
            yield return new WaitForSeconds(repeatInterval);
        }
    }

    #endregion

    #region --- Helper Methods ---

    private void TriggerKeyPress()
    {
        switch (keyType)
        {
            case KeyType.Alphanumeric:
                KeyboardEvents.KeyPressed(keyValue);
                break;
            case KeyType.DeleteCommand:
                KeyboardEvents.DeleteCommandPressed();
                break;
            case KeyType.NextCommand: KeyboardEvents.NextCommandPressed(); break;
        }

        onPress?.Invoke();

        if (pressSound != null)
        {
            audioSource.PlayOneShot(pressSound);
        }
    }

    private IEnumerator MoveToPosition(Vector3 targetPosition)
    {
        // Use Vector3.MoveTowards for uniform motion, resulting in a more stable effect
        while (Vector3.Distance(keyModel.transform.localPosition, targetPosition) > 0.001f)
        {
            keyModel.transform.localPosition = Vector3.MoveTowards(
                keyModel.transform.localPosition,
                targetPosition,
                animationSpeed * Time.deltaTime
            );
            yield return null;
        }
        keyModel.transform.localPosition = targetPosition;
    }

    #endregion

    #region --- Interface Implementation ---

    public override bool IsIdle()
    {
        return !isPressed;
    }

    public override void ResetKey()
    {
        StopAllCoroutines();
        keyModel.transform.localPosition = initialPosition;
        if (keyFace != null && idleMaterial != null)
        {
            keyFace.material = idleMaterial;
        }
        isPressed = false;
        repeatCoroutine = null;
        animationCoroutine = null;
    }

    #endregion
}