using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls cycling through multiple planet/Earth objects using L3 (left stick click) or J key.
/// Attach this to the parent object that contains all planet children.
/// Automatically detects spacing and animates smooth transitions between planets.
/// </summary>
public class EarthCarouselController : MonoBehaviour
{
    [Header("Transition Settings")]
    [SerializeField] 
    [Tooltip("Speed of transition between planets (higher = faster)")]
    private float transitionSpeed = 2f;
    
    [SerializeField]
    [Tooltip("Animation curve for smooth transitions (optional)")]
    private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [SerializeField]
    [Tooltip("Invert the movement direction if planets are moving the wrong way")]
    private bool invertDirection = false;
    
    [Header("Input Settings")]
    [SerializeField]
    [Tooltip("Enable keyboard input for testing (J key)")]
    private bool enableKeyboardInput = true;
    
    [SerializeField]
    [Tooltip("Keyboard key to cycle planets")]
    private Key cycleKey = Key.J;
    
    [Header("Audio")]
    [SerializeField]
    [Tooltip("Sound to play when cycling to next planet")]
    private AudioClip cycleSound;
    
    [SerializeField]
    [Range(0f, 1f)]
    private float cycleSoundVolume = 0.7f;
    
    [Header("Reference Position (Editor Only)")]
    [SerializeField]
    [Tooltip("The world position where planets should appear when centered. Auto-set on initialization.")]
    private Vector3 storedReferencePosition;
    
    [SerializeField]
    [Tooltip("Has the reference position been set? (Auto-managed)")]
    private bool hasStoredReference = false;
    
    [Header("Debug")]
    [SerializeField]
    private bool showDebugInfo = false;
    
    // Private state
    private ActiveRagdollActions inputActions;
    private Transform[] planetChildren;
    private int currentPlanetIndex = 0;
    private float spacing = 0f;
    private Vector3 firstPlanetWorldPosition; // Store the first planet's world position as reference
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float transitionProgress = 1f; // 1 = complete, 0 = just started
    private AudioSource audioSource;
    
    private void Awake()
    {
        // Initialize input system
        inputActions = new ActiveRagdollActions();
        
        // Setup audio source for cycle sounds
        if (cycleSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }
        
        // Discover planet children
        DiscoverPlanets();
    }
    
    private void OnEnable()
    {
        inputActions.Enable();
    }
    
    private void OnDisable()
    {
        inputActions.Disable();
    }
    
    private void DiscoverPlanets()
    {
        int childCount = transform.childCount;
        
        if (childCount == 0)
        {
            Debug.LogError($"[EarthCarouselController] No planet children found on {gameObject.name}!");
            return;
        }
        
        // Store all children in order
        planetChildren = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            planetChildren[i] = transform.GetChild(i);
        }
        
        // Calculate spacing between planets
        if (childCount > 1)
        {
            // Get spacing from first two children's X positions
            spacing = Mathf.Abs(planetChildren[1].localPosition.x - planetChildren[0].localPosition.x);
        }
        
        // Store the FIRST planet's world position as the reference "center" position
        if (childCount > 0 && planetChildren[0] != null)
        {
            // Use stored reference if available, otherwise capture current position
            if (!hasStoredReference)
            {
                storedReferencePosition = planetChildren[0].position;
                hasStoredReference = true;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[EarthCarouselController] Initialized reference position from first planet '{planetChildren[0].name}': {storedReferencePosition}");
                }
            }
            
            firstPlanetWorldPosition = storedReferencePosition;
            
            if (showDebugInfo)
            {
                Debug.Log($"[EarthCarouselController] Using reference position: {firstPlanetWorldPosition}");
            }
        }
        
        // Set initial position (should already be showing first planet)
        startPosition = transform.position;
        targetPosition = transform.position;
        transitionProgress = 1f;
        
        if (showDebugInfo)
        {
            Debug.Log($"[EarthCarouselController] Initialized with {childCount} planets, spacing: {spacing}");
            for (int i = 0; i < planetChildren.Length; i++)
            {
                Debug.Log($"  Planet {i}: {planetChildren[i].name} at local position {planetChildren[i].localPosition}");
            }
        }
    }
    
    private void Update()
    {
        // Handle keyboard input for testing
        if (enableKeyboardInput && Keyboard.current != null)
        {
            if (Keyboard.current[cycleKey].wasPressedThisFrame)
            {
                CycleToNextPlanet();
            }
        }
        
        // Handle gamepad L3 button (left stick click)
        if (Gamepad.current != null && Gamepad.current.leftStickButton.wasPressedThisFrame)
        {
            CycleToNextPlanet();
        }
        
        // Animate transition if in progress
        if (transitionProgress < 1f)
        {
            AnimateTransition();
        }
    }
    
    /// <summary>
    /// Cycles to the next planet in sequence (loops back to first after last)
    /// </summary>
    public void CycleToNextPlanet()
    {
        if (planetChildren == null || planetChildren.Length == 0)
            return;
        
        // Don't allow cycling if already transitioning
        if (transitionProgress < 1f)
            return;
        
        // Move to next planet (wrap around)
        currentPlanetIndex = (currentPlanetIndex + 1) % planetChildren.Length;
        
        // Start transition
        StartTransitionToPlanet(currentPlanetIndex);
        
        if (showDebugInfo)
        {
            Debug.Log($"[EarthCarouselController] Cycling to planet {currentPlanetIndex} ({planetChildren[currentPlanetIndex].name})");
        }
    }
    
    /// <summary>
    /// Cycles to the previous planet in sequence (loops back to last from first)
    /// </summary>
    public void CycleToPreviousPlanet()
    {
        if (planetChildren == null || planetChildren.Length == 0)
            return;
        
        // Don't allow cycling if already transitioning
        if (transitionProgress < 1f)
            return;
        
        // Move to previous planet (wrap around)
        currentPlanetIndex--;
        if (currentPlanetIndex < 0)
            currentPlanetIndex = planetChildren.Length - 1;
        
        // Start transition
        StartTransitionToPlanet(currentPlanetIndex);
        
        if (showDebugInfo)
        {
            Debug.Log($"[EarthCarouselController] Cycling to planet {currentPlanetIndex} ({planetChildren[currentPlanetIndex].name})");
        }
    }
    
    /// <summary>
    /// Jump directly to a specific planet by index
    /// </summary>
    public void GoToPlanet(int planetIndex)
    {
        if (planetChildren == null || planetChildren.Length == 0)
            return;
        
        if (planetIndex < 0 || planetIndex >= planetChildren.Length)
        {
            Debug.LogWarning($"[EarthCarouselController] Invalid planet index: {planetIndex}");
            return;
        }
        
        currentPlanetIndex = planetIndex;
        StartTransitionToPlanet(currentPlanetIndex);
    }
    
    private void StartTransitionToPlanet(int planetIndex)
    {
        // Calculate target position to center this planet
        // We move the parent so the selected planet appears at firstPlanetWorldPosition
        
        Transform targetPlanet = planetChildren[planetIndex];
        
        // Current world position of the target planet
        Vector3 currentTargetWorldPos = targetPlanet.position;
        
        // How far is the target planet from where it should be?
        Vector3 offset = currentTargetWorldPos - firstPlanetWorldPosition;
        
        // Store current position as start of animation
        startPosition = transform.position;
        
        // Target position: move parent by the negative offset
        targetPosition = transform.position - offset;
        
        // Apply invert direction if needed (for compatibility)
        if (invertDirection)
        {
            // Invert the X offset only
            float originalX = transform.position.x;
            float calculatedX = targetPosition.x;
            float difference = calculatedX - originalX;
            targetPosition.x = originalX - difference;
        }
        
        // Reset transition
        transitionProgress = 0f;
        
        if (showDebugInfo)
        {
            Debug.Log($"[EarthCarouselController] Transitioning to planet {planetIndex} ({targetPlanet.name})");
            Debug.Log($"[EarthCarouselController] First planet reference: {firstPlanetWorldPosition}");
            Debug.Log($"[EarthCarouselController] Target planet current position: {currentTargetWorldPos}");
            Debug.Log($"[EarthCarouselController] Offset: {offset}");
            Debug.Log($"[EarthCarouselController] Target parent position: {targetPosition}");
        }
        
        // Play cycle sound
        if (cycleSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f); // Slight pitch variation
            audioSource.PlayOneShot(cycleSound, cycleSoundVolume);
        }
    }
    
    private void AnimateTransition()
    {
        // Advance transition
        transitionProgress += Time.deltaTime * transitionSpeed;
        transitionProgress = Mathf.Clamp01(transitionProgress);
        
        // Apply curve for smooth easing
        float curvedProgress = transitionCurve.Evaluate(transitionProgress);
        
        // Lerp position
        transform.position = Vector3.Lerp(startPosition, targetPosition, curvedProgress);
    }
    
    /// <summary>
    /// Get the current active planet index
    /// </summary>
    public int GetCurrentPlanetIndex()
    {
        return currentPlanetIndex;
    }
    
    /// <summary>
    /// Get the current active planet transform
    /// </summary>
    public Transform GetCurrentPlanet()
    {
        if (planetChildren == null || currentPlanetIndex >= planetChildren.Length)
            return null;
        
        return planetChildren[currentPlanetIndex];
    }
    
    /// <summary>
    /// Get total number of planets
    /// </summary>
    public int GetPlanetCount()
    {
        return planetChildren?.Length ?? 0;
    }
    
    private void OnDestroy()
    {
        inputActions?.Dispose();
    }
    
    // Draw gizmos in editor to visualize planet positions
    private void OnDrawGizmos()
    {
        if (!showDebugInfo || !Application.isPlaying)
            return;
        
        if (planetChildren == null)
            return;
        
        // Draw lines connecting planets
        Gizmos.color = Color.cyan;
        for (int i = 0; i < planetChildren.Length - 1; i++)
        {
            if (planetChildren[i] != null && planetChildren[i + 1] != null)
            {
                Gizmos.DrawLine(planetChildren[i].position, planetChildren[i + 1].position);
            }
        }
        
        // Highlight current planet
        if (planetChildren[currentPlanetIndex] != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(planetChildren[currentPlanetIndex].position, 5f);
        }
    }
}

