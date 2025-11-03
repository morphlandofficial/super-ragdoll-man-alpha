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
    
    [Header("Debug")]
    [SerializeField]
    private bool showDebugInfo = false;
    
    // Private state
    private ActiveRagdollActions inputActions;
    private Transform[] planetChildren;
    private int currentPlanetIndex = 0;
    private float spacing = 0f;
    private Vector3 originalParentPosition; // Store the initial parent position
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
        
        // Store the original parent position (this is where planet 0 is centered)
        originalParentPosition = transform.position;
        
        // Set initial position (should already be showing first planet)
        startPosition = originalParentPosition;
        targetPosition = originalParentPosition;
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
        // We need to move the parent so the selected child appears at the same world position as child 0 originally did
        
        // Get the offset between the target planet and the first planet
        Vector3 firstChildLocalPos = planetChildren[0].localPosition;
        Vector3 targetChildLocalPos = planetChildren[planetIndex].localPosition;
        float offsetFromFirst = targetChildLocalPos.x - firstChildLocalPos.x;
        
        // Store current position as start of animation
        startPosition = transform.position;
        
        // Target position: move parent in opposite direction of the child's offset from first
        // If child is +444 units to the right of child 0, move parent -444 to the left
        targetPosition = originalParentPosition;
        if (invertDirection)
            targetPosition.x = originalParentPosition.x + offsetFromFirst;
        else
            targetPosition.x = originalParentPosition.x - offsetFromFirst;
        
        // Reset transition
        transitionProgress = 0f;
        
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

