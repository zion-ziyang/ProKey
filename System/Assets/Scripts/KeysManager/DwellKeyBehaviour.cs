using UnityEngine;
using System;
using System.Collections;
using TMPro;
using UnityEngine.Events;

public class DwellKeyBehaviour : BaseKey
{
    // --- Core References ---
    [Tooltip("Moving part of the key (child 'Collider'): carries the press collider, the face and the label")]
    public GameObject keyModel;
    [Tooltip("Renderer of the visible key face (child 'Face' of the key model)")]
    public MeshRenderer keyFace;

    [Header("Zone Visualization")]
    [Tooltip("Drag the GameObject with a Renderer in the Activation zone here")]
    [SerializeField] private Renderer activationZoneRenderer;

    [Header("Animation Mode Selection")]
    public AnimationMode mode = AnimationMode.Anticipate;

    [Header("General Animation Parameters")]
    public Vector3 risingTargetLocalPosition = new(0f, 0f, -0.135f);
    public float riseSpeed = 0.5f;
    public float returnSpeed = 0.4f;

    [Header("Mode-Specific Parameters")]
    [Tooltip("[Mode: Anticipate] Target local position when key retreats")]
    public Vector3 anticipationTargetLocalPosition = new(0f, 0f, -0.0001f);
    [Tooltip("[Mode: Anticipate] Retreat speed")]
    public float anticipationSpeed = 0.05f;

    [Header("Continuous Input Parameters")]
    [Tooltip("Whether to enable key repeat")]
    public bool enableRepeat = false;
    [Tooltip("Delay before first repeated input after initial press (seconds)")]
    public float repeatDelay = 0.5f;
    [Tooltip("Interval between subsequent repeated inputs (seconds)")]
    public float repeatInterval = 0.1f;

    [Header("Visuals & Audio")]
    public Material idleMaterial;
    public Material hoverMaterial;
    public Material pressedMaterial;
    [Space]
    public AudioClip hoverSound;
    public AudioClip pressSound;

    [Header("Interaction Parameters")]
    [Tooltip("Cooldown after a single press to prevent multiple triggers during one rise")]
    public float pressCooldown = 0.2f;

    [Header("Events")]
    public UnityEvent onPress;
    public UnityEvent onRelease;

    // --- Private State Variables ---
    private AudioSource audioSource;
    private Vector3 initialPosition;

    private KeyAnimationState currentState = KeyAnimationState.Idle;
    private Coroutine interactionCoroutine;
    private bool isHovering = false;
    private bool isCoolingDown = false;
    private bool wasPressedThisCycle = false;


    void Start()
    {
        if (keyModel == null || keyFace == null)
        {
            Debug.LogError($"Key '{keyValue}': keyModel or keyFace not specified!", this);
            return;
        }

        if (activationZoneRenderer != null)
        {
            activationZoneRenderer.enabled = false;
        }

        initialPosition = keyModel.transform.localPosition;

        audioSource = GetComponentInParent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        keyFace.material = idleMaterial;
        if (keyType == KeyType.Alphanumeric)
        {
            GetComponentInChildren<TMP_Text>().text = keyValue;
        }
    }

    // Called by KeyTrigger.cs
    public void OnFingerEnterZone(KeyZoneType zone)
    {
        if (zone == KeyZoneType.Hover)
        {
            // Entered hover zone: only show activation zone, do nothing else
            if (activationZoneRenderer != null)
            {
                activationZoneRenderer.enabled = true;
            }
        }
        else if (zone == KeyZoneType.Activation)
        {
            // Entered activation zone: trigger full interaction logic (same as previous OnFingerHoverStart)
            if (activationZoneRenderer != null)
            {
                activationZoneRenderer.enabled = true; // Ensure it is also shown
            }

            if (isHovering) return;
            isHovering = true; // isHovering now represents being in the activation zone

            if (interactionCoroutine != null) StopCoroutine(interactionCoroutine);
            interactionCoroutine = StartCoroutine(FullInteractionRoutine());
        }
    }

    // Called by KeyTrigger.cs
    public void OnFingerLeaveZone(KeyZoneType zone)
    {
        if (zone == KeyZoneType.Hover)
        {
            // Completely left all zones: hide activation zone and ensure interaction stops
            if (activationZoneRenderer != null)
            {
                activationZoneRenderer.enabled = false;
            }
            isHovering = false; // Ensure coroutine will stop
        }
        else if (zone == KeyZoneType.Activation)
        {
            // Left activation zone: trigger finger left logic (same as previous OnFingerHoverEnd)
            isHovering = false;
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case KeyAnimationState.Anticipating:
                MoveTowards(anticipationTargetLocalPosition, anticipationSpeed);
                if (IsAtPosition(anticipationTargetLocalPosition))
                {
                    currentState = KeyAnimationState.Rising;
                }
                break;

            case KeyAnimationState.Rising:
                MoveTowards(risingTargetLocalPosition, riseSpeed);
                // Collision detection is now handled by HandlePress; no need to check if target reached here
                break;

            case KeyAnimationState.Returning:
                if (!isCoolingDown) RemoveHoverEffect();
                MoveTowards(initialPosition, returnSpeed);
                if (IsAtPosition(initialPosition))
                {
                    currentState = KeyAnimationState.Idle;
                    // Subsequent behavior in Idle state is fully controlled by the coroutine
                }
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("collider"))
        {
            HandlePress();
        }
    }

    private void HandlePress()
    {
        // Must be an active key and not cooling down
        if (isCoolingDown && isHovering)
        {
            return;
        }

        // Trigger events and effects
        switch (keyType)
        {
            case KeyType.Alphanumeric: KeyboardEvents.KeyPressed(keyValue); break;
            case KeyType.DeleteCommand: KeyboardEvents.DeleteCommandPressed(); break;
            case KeyType.NextCommand: KeyboardEvents.NextCommandPressed(); break;
        }
        ApplyPressEffect();
        StartCoroutine(PressCooldownRoutine());

        // Only set the flag; the running coroutine reacts to it
        wasPressedThisCycle = true;
    }

    // --- Helper Methods ---

    private void InitiateRise()
    {
        switch (mode)
        {
            case AnimationMode.Direct:
                currentState = KeyAnimationState.Rising;
                break;
            case AnimationMode.Anticipate:
                currentState = KeyAnimationState.Anticipating;
                break;
        }
    }

    private void MoveTowards(Vector3 target, float speed)
    {
        keyModel.transform.localPosition = Vector3.MoveTowards(keyModel.transform.localPosition, target, speed * Time.deltaTime);
    }

    private bool IsAtPosition(Vector3 target)
    {
        return Vector3.Distance(keyModel.transform.localPosition, target) < 0.0001f;
    }

    private void ApplyHoverEffect()
    {
        if (!isCoolingDown)
        {
            keyFace.material = hoverMaterial;
            if (hoverSound != null) audioSource.PlayOneShot(hoverSound);
        }
    }

    private void RemoveHoverEffect()
    {
        keyFace.material = idleMaterial;
    }

    private void ApplyPressEffect()
    {
        keyFace.material = pressedMaterial;
        if (pressSound != null) audioSource.PlayOneShot(pressSound);
        onPress?.Invoke();
    }

    // --- Coroutines ---

    private IEnumerator PressCooldownRoutine()
    {
        isCoolingDown = true;
        yield return new WaitForSeconds(pressCooldown);
        isCoolingDown = false;

        // After cooldown ends, if the main interaction flow is still active (i.e. finger is still hovering),
        // and the key is not currently returning or idle, restore hover material.
        // This prevents overriding the final Idle material.
        if (isHovering)
        {
            // Set material directly to avoid replaying the hover sound
            keyFace.material = hoverMaterial;
        }
    }

    private IEnumerator FullInteractionRoutine()
    {
        // Initial hover effect
        ApplyHoverEffect();
        bool isFirstPress = true;

        // === Core Interaction Loop ===
        // As long as the finger is in the activation zone, this loop continues
        while (isHovering)
        {
            wasPressedThisCycle = false; // Reset the "pressed this cycle" signal
            InitiateRise();

            // Wait until key is pressed or finger leaves midway
            yield return new WaitUntil(() => wasPressedThisCycle || !isHovering);

            // If awakened because finger left, break out of loop and enter final cleanup
            if (!isHovering)
            {
                break;
            }

            // --- Key has been pressed ---
            currentState = KeyAnimationState.Returning;
            yield return new WaitUntil(() => currentState == KeyAnimationState.Idle);

            // --- Check if repeat is needed ---
            if (enableRepeat)
            {
                // Enter waiting for repeat state
                currentState = KeyAnimationState.WaitingForRepeat;
                float delay = isFirstPress ? repeatDelay : repeatInterval;
                isFirstPress = false;

                // While waiting, constantly check if finger has left
                float waitTimer = 0f;
                while (waitTimer < delay)
                {
                    if (!isHovering) break; // If finger leaves, immediately interrupt wait
                    waitTimer += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                // Repeat disabled: stay in the coroutine until the finger leaves the activation zone
                yield return new WaitUntil(() => !isHovering);
            }
        }

        // === Final Cleanup Phase ===
        // Only reached when isHovering becomes false (regardless of current phase).
        // Ensure key fully returns to initial position
        if (currentState != KeyAnimationState.Idle)
        {
            currentState = KeyAnimationState.Returning;
            yield return new WaitUntil(() => currentState == KeyAnimationState.Idle);
        }

        // Remove all visual and audio effects, restoring to initial state
        RemoveHoverEffect();
        onRelease?.Invoke();

        // Mark coroutine as finished naturally
        interactionCoroutine = null;
    }


    public override void ResetKey()
    {
        // 1. Stop all running coroutines on this script (e.g. FullInteractionRoutine, PressCooldownRoutine)
        // This is the most crucial step to prevent stale coroutines from interfering when the key is reused.
        StopAllCoroutines();

        // 2. Manually reset all state variables
        currentState = KeyAnimationState.Idle;
        isHovering = false;
        isCoolingDown = false;
        wasPressedThisCycle = false;
        interactionCoroutine = null;

        // 3. Force restore visual state
        if (keyModel != null)
        {
            keyModel.transform.localPosition = initialPosition;
        }
        if (keyFace != null)
        {
            keyFace.material = idleMaterial;
        }
        if (activationZoneRenderer != null)
        {
            activationZoneRenderer.enabled = false;
        }
    }

    public override bool IsIdle()
    {
        return this.currentState == KeyAnimationState.Idle;
    }
}