using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bounce pad component that launches the player when touched and grants a time bonus.
/// Add this to any object with a Rigidbody and Collider to make it a bounce pad.
/// Supports both collision-based and trigger-based detection for rigidbody contact.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BouncePad : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField]
    [Tooltip("Use trigger detection instead of collision detection. Trigger mode allows passthrough while still detecting rigidbody contact.")]
    private bool useTriggerDetection = false;
    
    [Header("Bounce Settings")]
    [SerializeField] 
    [Tooltip("Force applied to launch the player upward")]
    private float bounceForce = 800f;
    
    [SerializeField]
    [Tooltip("Direction of the bounce force (Y=up, customize for angled launch pads)")]
    private Vector3 bounceDirection = Vector3.up;
    
    [SerializeField]
    [Tooltip("Should the bounce direction be relative to this object's rotation?")]
    private bool useLocalDirection = false;
    
    [SerializeField]
    [Tooltip("Require player to press jump while on platform to bounce (allows standing on platform)")]
    private bool requireJumpToBounce = false;
    
    [Header("Time Bonus")]
    [SerializeField]
    [Tooltip("Seconds added to the timer when player touches this pad")]
    private float timeBonus = 10f;
    
    [SerializeField]
    [Tooltip("Give time bonus every contact, or just once?")]
    private bool timeBonusEveryContact = false;
    
    [SerializeField]
    [Tooltip("Can the player use this bounce pad multiple times?")]
    private bool allowMultipleUses = true;
    
    [Header("Cooldown")]
    [SerializeField]
    [Tooltip("Cooldown between bounces (seconds). Set to 0 for no cooldown")]
    private float cooldownTime = 1f;
    
    [Header("Visual Feedback")]
    [SerializeField]
    [Tooltip("Material to temporarily apply when activated (optional)")]
    private Material activatedMaterial;
    
    [SerializeField]
    [Tooltip("Duration to show activated material (seconds)")]
    private float activatedMaterialDuration = 0.3f;
    
    [SerializeField]
    [Tooltip("Enable scale animation when activated")]
    private bool enableScaleAnimation = false;
    
    [SerializeField]
    [Tooltip("Scale punch effect when activated (1.0 = no effect)")]
    private float scaleMultiplier = 1.2f;
    
    [SerializeField]
    [Tooltip("Duration of scale animation (seconds)")]
    private float scaleAnimationDuration = 0.3f;
    
    [Header("Audio")]
    [SerializeField]
    [Tooltip("Sound to play when player bounces (optional)")]
    private AudioClip bounceSound;
    
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume of the bounce sound")]
    private float bounceSoundVolume = 0.8f;
    
    [Header("Debug")]
    [SerializeField]
    // private bool showDebugMessages = false; // Unused after debug log cleanup // Set to false for release
    
    // Internal state
    private bool hasBeenUsed = false;
    private bool timeBonusGiven = false;
    private float lastBounceTime = -1f;
    private TimerDisplayText timerReference;
    private Material originalMaterial;
    private Renderer objectRenderer;
    private Vector3 originalScale;
    private AudioSource audioSource;
    
    // Animation state
    private bool isAnimating = false;
    private float animationTimer = 0f;
    
    // Jump-to-bounce mode state
    private ActiveRagdoll.ActiveRagdoll playerOnPlatform = null;
    private ActiveRagdollActions inputActions;
    
    private void Awake()
    {
        // Initialize input actions for jump-to-bounce mode
        if (requireJumpToBounce)
        {
            inputActions = new ActiveRagdollActions();
        }
        
        // Ensure rigidbody is kinematic (so the pad doesn't fall)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.isKinematic = true;
            // if (showDebugMessages)

        }
        
        // Cache renderer and original material
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
        }
        
        // Store original scale
        originalScale = transform.localScale;
        
        // Setup audio source if bounce sound is assigned
        if (bounceSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.clip = bounceSound;
            audioSource.volume = bounceSoundVolume;
        }
        
        // Normalize bounce direction
        if (bounceDirection.magnitude > 0)
        {
            bounceDirection = bounceDirection.normalized;
        }
        else
        {
            // Debug.LogWarning($"BouncePad on {gameObject.name}: Bounce direction is zero! Defaulting to Vector3.up");
            bounceDirection = Vector3.up;
        }
    }
    
    private void OnEnable()
    {
        if (requireJumpToBounce && inputActions != null)
        {
            inputActions.Enable();
            inputActions.Player.Jump.performed += OnJumpPressed;
        }
    }
    
    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Jump.performed -= OnJumpPressed;
            inputActions.Disable();
        }
        
        // Clear player reference
        playerOnPlatform = null;
    }
    
    private void Start()
    {
        // Find timer in scene
        timerReference = FindFirstObjectByType<TimerDisplayText>();
        // if (timerReference == null && showDebugMessages)
        // {
        //     Debug.LogWarning($"BouncePad on {gameObject.name}: No TimerDisplayText found in scene. Time bonus will not work.");
        // }
    }
    
    private void Update()
    {
        // Handle scale animation
        if (isAnimating)
        {
            animationTimer += Time.deltaTime;
            float progress = animationTimer / scaleAnimationDuration;
            
            if (progress >= 1f)
            {
                // Animation complete
                transform.localScale = originalScale;
                isAnimating = false;
                animationTimer = 0f;
            }
            else
            {
                // Ping-pong scale animation
                float scale = Mathf.Lerp(1f, scaleMultiplier, Mathf.Sin(progress * Mathf.PI));
                transform.localScale = originalScale * scale;
            }
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Only use collision detection if trigger mode is disabled
        if (useTriggerDetection)
            return;
            
        HandleContact(collision.rigidbody);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Only use trigger detection if trigger mode is enabled
        if (!useTriggerDetection)
            return;
            
        HandleContact(other.attachedRigidbody);
    }
    
    private void HandleContact(Rigidbody collidedRigidbody)
    {
        // Check if already used (if single use)
        if (!allowMultipleUses && hasBeenUsed)
            return;
        
        // Check cooldown
        if (cooldownTime > 0 && Time.time - lastBounceTime < cooldownTime)
            return;
        
        // Try to find the player's torso rigidbody
        // The collision could be with any body part, so we need to check
        if (collidedRigidbody == null)
            return;
        
        // Check if this is part of the player's ragdoll
        // We'll look for the DefaultBehaviour component in parent hierarchy
        DefaultBehaviour playerBehaviour = collidedRigidbody.GetComponentInParent<DefaultBehaviour>();
        if (playerBehaviour == null)
        {
            // Not the player
            return;
        }
        
        // Find the player's physical torso
        ActiveRagdoll.ActiveRagdoll activeRagdoll = playerBehaviour.GetComponent<ActiveRagdoll.ActiveRagdoll>();
        if (activeRagdoll == null || activeRagdoll.PhysicalTorso == null)
        {
            // if (showDebugMessages)

            //     Debug.LogWarning($"BouncePad on {gameObject.name}: Could not find player's PhysicalTorso!");
            return;
        }
        
        // If jump-to-bounce mode is enabled, just track the player and wait for jump input
        if (requireJumpToBounce)
        {
            playerOnPlatform = activeRagdoll;
            return;
        }
        
        // AUTO-BOUNCE MODE: BOUNCE THE PLAYER immediately!
        PerformBounce(activeRagdoll);
    }
    
    private void OnCollisionStay(Collision collision)
    {
        // Only use collision detection if trigger mode is disabled
        if (useTriggerDetection)
            return;
            
        HandleContactStay(collision.rigidbody);
    }
    
    private void OnTriggerStay(Collider other)
    {
        // Only use trigger detection if trigger mode is enabled
        if (!useTriggerDetection)
            return;
            
        HandleContactStay(other.attachedRigidbody);
    }
    
    private void HandleContactStay(Rigidbody collidedRigidbody)
    {
        // Only needed for jump-to-bounce mode
        if (!requireJumpToBounce)
            return;
        
        // Keep tracking player while they're on the platform
        if (playerOnPlatform == null)
        {
            if (collidedRigidbody != null)
            {
                DefaultBehaviour playerBehaviour = collidedRigidbody.GetComponentInParent<DefaultBehaviour>();
                if (playerBehaviour != null)
                {
                    ActiveRagdoll.ActiveRagdoll activeRagdoll = playerBehaviour.GetComponent<ActiveRagdoll.ActiveRagdoll>();
                    if (activeRagdoll != null && activeRagdoll.PhysicalTorso != null)
                    {
                        playerOnPlatform = activeRagdoll;
                    }
                }
            }
        }
    }
    
    private void OnCollisionExit(Collision collision)
    {
        // Only use collision detection if trigger mode is disabled
        if (useTriggerDetection)
            return;
            
        HandleContactExit(collision.rigidbody);
    }
    
    private void OnTriggerExit(Collider other)
    {
        // Only use trigger detection if trigger mode is enabled
        if (!useTriggerDetection)
            return;
            
        HandleContactExit(other.attachedRigidbody);
    }
    
    private void HandleContactExit(Rigidbody collidedRigidbody)
    {
        // Only needed for jump-to-bounce mode
        if (!requireJumpToBounce)
            return;
        
        // Check if the player is leaving
        if (collidedRigidbody != null)
        {
            DefaultBehaviour playerBehaviour = collidedRigidbody.GetComponentInParent<DefaultBehaviour>();
            if (playerBehaviour != null)
            {
                ActiveRagdoll.ActiveRagdoll activeRagdoll = playerBehaviour.GetComponent<ActiveRagdoll.ActiveRagdoll>();
                if (activeRagdoll == playerOnPlatform)
                {
                    playerOnPlatform = null;
                }
            }
        }
    }
    
    private void OnJumpPressed(InputAction.CallbackContext context)
    {
        // Only bounce if player is on platform and jump-to-bounce is enabled
        if (playerOnPlatform != null && requireJumpToBounce)
        {
            // Check if already used (if single use)
            if (!allowMultipleUses && hasBeenUsed)
                return;
            
            // Check cooldown
            if (cooldownTime > 0 && Time.time - lastBounceTime < cooldownTime)
                return;
            
            // BOUNCE THE PLAYER!
            PerformBounce(playerOnPlatform);
        }
    }
    
    private void PerformBounce(ActiveRagdoll.ActiveRagdoll activeRagdoll)
    {
        if (activeRagdoll == null || activeRagdoll.PhysicalTorso == null)
            return;
        
        // Apply bounce force
        ApplyBounce(activeRagdoll.PhysicalTorso);
        
        // Add time bonus (if applicable)
        bool shouldGiveTimeBonus = timeBonusEveryContact || !timeBonusGiven;
        if (shouldGiveTimeBonus && timerReference != null && timeBonus != 0)
        {
            timerReference.AddTime(timeBonus);
            timeBonusGiven = true;
            // if (showDebugMessages)

        }
        
        // Visual feedback
        TriggerVisualFeedback();
        
        // Audio feedback
        if (audioSource != null)
        {
            audioSource.Play();
        }
        
        // Update state
        hasBeenUsed = true;
        lastBounceTime = Time.time;
        
        // if (showDebugMessages)

        
    }
    
    private void ApplyBounce(Rigidbody playerTorso)
    {
        // Calculate bounce direction (local or world space)
        Vector3 launchDirection = useLocalDirection ? transform.TransformDirection(bounceDirection) : bounceDirection;
        launchDirection = launchDirection.normalized;
        
        // Apply force to player's torso
        playerTorso.AddForce(launchDirection * bounceForce, ForceMode.Impulse);
    }
    
    private void TriggerVisualFeedback()
    {
        // Material change
        if (activatedMaterial != null && objectRenderer != null)
        {
            StartCoroutine(FlashMaterial());
        }
        
        // Scale animation
        if (enableScaleAnimation && scaleMultiplier != 1f)
        {
            isAnimating = true;
            animationTimer = 0f;
        }
    }
    
    private System.Collections.IEnumerator FlashMaterial()
    {
        if (objectRenderer != null)
        {
            objectRenderer.material = activatedMaterial;
            yield return new WaitForSeconds(activatedMaterialDuration);
            objectRenderer.material = originalMaterial;
        }
    }
    
    /// <summary>
    /// Manually reset the bounce pad to allow it to be used again
    /// </summary>
    public void ResetBouncePad()
    {
        hasBeenUsed = false;
        timeBonusGiven = false;
        lastBounceTime = -1f;
    }
    
    /// <summary>
    /// Draw the bounce direction in the editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 launchDirection = useLocalDirection ? transform.TransformDirection(bounceDirection) : bounceDirection;
        Vector3 startPos = transform.position;
        Gizmos.DrawRay(startPos, launchDirection * 2f);
        Gizmos.DrawWireSphere(startPos + launchDirection * 2f, 0.3f);
    }
}

