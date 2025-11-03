using UnityEngine;

/// <summary>
/// Manages gravity settings for the scene. Attach this to a GameObject to control gravity.
/// Perfect for underwater environments, low-gravity zones, or any custom gravity scenarios.
/// </summary>
public class GravityManager : MonoBehaviour
{
    [Header("Gravity Settings")]
    [Tooltip("Apply gravity settings when the scene starts")]
    [SerializeField] private bool applyOnStart = true;
    
    [Tooltip("Gravity preset to use")]
    [SerializeField] private GravityPreset gravityPreset = GravityPreset.Custom;
    
    [Tooltip("Custom gravity value (used when preset is 'Custom')")]
    [SerializeField] private Vector3 customGravity = new Vector3(0, -9.81f, 0);
    
    [Header("Transition Settings")]
    [Tooltip("Enable smooth transition when changing gravity")]
    [SerializeField] private bool smoothTransition = true;
    
    [Tooltip("Duration of gravity transition in seconds")]
    [SerializeField] private float transitionDuration = 2f;
    
    [Header("Info")]
    [Tooltip("Shows the current gravity value (read-only)")]
    [SerializeField] private Vector3 currentGravity;
    
    // Private variables for smooth transitions
    private Vector3 targetGravity;
    private Vector3 startGravity;
    private float transitionTimer = 0f;
    private bool isTransitioning = false;
    
    // Store the original gravity to restore when needed
    private Vector3 originalGravity;
    
    public enum GravityPreset
    {
        Earth,          // -9.81 m/s²
        Underwater,     // -2.0 m/s² (reduced gravity for underwater feel)
        Moon,           // -1.62 m/s²
        Mars,           // -3.71 m/s²
        LowGravity,     // -4.0 m/s²
        ZeroGravity,    // 0 m/s²
        HighGravity,    // -20.0 m/s²
        Custom          // User-defined
    }
    
    private void Awake()
    {
        // Store the original gravity value
        originalGravity = Physics.gravity;
        currentGravity = originalGravity;
    }
    
    private void Start()
    {
        if (applyOnStart)
        {
            ApplyGravity();
        }
    }
    
    private void Update()
    {
        // Handle smooth gravity transitions
        if (isTransitioning)
        {
            transitionTimer += Time.deltaTime;
            float t = Mathf.Clamp01(transitionTimer / transitionDuration);
            
            // Smooth interpolation
            float smoothT = Mathf.SmoothStep(0, 1, t);
            Physics.gravity = Vector3.Lerp(startGravity, targetGravity, smoothT);
            currentGravity = Physics.gravity;
            
            if (t >= 1f)
            {
                isTransitioning = false;
            }
        }
        else
        {
            currentGravity = Physics.gravity;
        }
    }
    
    /// <summary>
    /// Apply the selected gravity preset or custom gravity
    /// </summary>
    public void ApplyGravity()
    {
        Vector3 newGravity = GetGravityFromPreset(gravityPreset);
        SetGravity(newGravity);
    }
    
    /// <summary>
    /// Set gravity to a specific value
    /// </summary>
    /// <param name="gravity">The new gravity vector</param>
    public void SetGravity(Vector3 gravity)
    {
        if (smoothTransition && Application.isPlaying)
        {
            StartGravityTransition(gravity);
        }
        else
        {
            Physics.gravity = gravity;
            currentGravity = gravity;
        }
    }
    
    /// <summary>
    /// Set gravity using a preset
    /// </summary>
    /// <param name="preset">The gravity preset to apply</param>
    public void SetGravityPreset(GravityPreset preset)
    {
        gravityPreset = preset;
        ApplyGravity();
    }
    
    /// <summary>
    /// Restore the original gravity that was present when the scene loaded
    /// </summary>
    public void RestoreOriginalGravity()
    {
        SetGravity(originalGravity);
    }
    
    /// <summary>
    /// Get the gravity vector for a specific preset
    /// </summary>
    private Vector3 GetGravityFromPreset(GravityPreset preset)
    {
        switch (preset)
        {
            case GravityPreset.Earth:
                return new Vector3(0, -9.81f, 0);
            
            case GravityPreset.Underwater:
                // Reduced gravity for underwater feeling (buoyancy simulation)
                return new Vector3(0, -2.0f, 0);
            
            case GravityPreset.Moon:
                return new Vector3(0, -1.62f, 0);
            
            case GravityPreset.Mars:
                return new Vector3(0, -3.71f, 0);
            
            case GravityPreset.LowGravity:
                return new Vector3(0, -4.0f, 0);
            
            case GravityPreset.ZeroGravity:
                return Vector3.zero;
            
            case GravityPreset.HighGravity:
                return new Vector3(0, -20.0f, 0);
            
            case GravityPreset.Custom:
                return customGravity;
            
            default:
                return new Vector3(0, -9.81f, 0);
        }
    }
    
    /// <summary>
    /// Start a smooth transition to new gravity
    /// </summary>
    private void StartGravityTransition(Vector3 newGravity)
    {
        startGravity = Physics.gravity;
        targetGravity = newGravity;
        transitionTimer = 0f;
        isTransitioning = true;
    }
    
    /// <summary>
    /// Immediately set gravity without transition
    /// </summary>
    public void SetGravityImmediate(Vector3 gravity)
    {
        Physics.gravity = gravity;
        currentGravity = gravity;
        isTransitioning = false;
    }
    
    /// <summary>
    /// Toggle between zero gravity and the current preset
    /// </summary>
    public void ToggleZeroGravity()
    {
        if (Physics.gravity.magnitude < 0.1f)
        {
            // Currently zero gravity, restore preset
            ApplyGravity();
        }
        else
        {
            // Not zero gravity, set to zero
            SetGravity(Vector3.zero);
        }
    }
    
    /// <summary>
    /// Multiply current gravity by a factor
    /// </summary>
    public void MultiplyGravity(float multiplier)
    {
        SetGravity(Physics.gravity * multiplier);
    }
    
    /// <summary>
    /// Add a value to current gravity
    /// </summary>
    public void AddGravity(Vector3 additionalGravity)
    {
        SetGravity(Physics.gravity + additionalGravity);
    }
    
    private void OnDestroy()
    {
        // Restore original gravity when this component is destroyed
        // This prevents gravity changes from persisting across scenes
        if (Application.isPlaying)
        {
            Physics.gravity = originalGravity;
        }
    }
    
    private void OnValidate()
    {
        // Update the display of current gravity in the inspector
        if (Application.isPlaying)
        {
            currentGravity = Physics.gravity;
        }
    }
    
    // Public getters
    public Vector3 CurrentGravity => Physics.gravity;
    public GravityPreset CurrentPreset => gravityPreset;
    public bool IsTransitioning => isTransitioning;
}




