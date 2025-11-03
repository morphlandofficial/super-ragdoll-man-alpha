using UnityEngine;
using System.Collections;

/// <summary>
/// Handles animated scaling for objects that should grow into existence when activated.
/// This component should be attached to objects that are toggled by ProximitySceneLoader.
/// </summary>
public class ProximityScaleAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float growDuration = 0.3f;
    [SerializeField] private AnimationCurve growCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float startScale = 0.001f; // Very small but not zero to avoid issues
    
    // Public properties for external configuration
    public float GrowDuration 
    { 
        get => growDuration; 
        set => growDuration = Mathf.Max(0.1f, value); 
    }
    
    public AnimationCurve GrowCurve 
    { 
        get => growCurve; 
        set => growCurve = value ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); 
    }
    
    public float StartScale 
    { 
        get => startScale; 
        set => startScale = Mathf.Max(0.001f, value); 
    }
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false; // Set to false for release
    
    private Vector3 originalScale;
    private Coroutine currentAnimation;
    private bool isInitialized = false;
    
    private void Awake()
    {
        Initialize();
    }
    
    private void Initialize()
    {
        if (isInitialized) return;
        
        // Store the original scale
        originalScale = transform.localScale;
        
        // If the object starts inactive, set it to tiny scale
        if (!gameObject.activeSelf)
        {
            transform.localScale = Vector3.one * startScale;
        }
        
        isInitialized = true;
        
        if (debugMode)
        {
        }
    }
    
    private void OnEnable()
    {
        // When the object becomes active, start the grow animation
        if (isInitialized)
        {
            StartGrowAnimation();
        }
    }
    
    private void OnDisable()
    {
        // When disabled, stop any running animation and reset to tiny scale
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
        
        // Reset to tiny scale for next activation
        if (isInitialized)
        {
            transform.localScale = Vector3.one * startScale;
        }
    }
    
    /// <summary>
    /// Starts the grow animation from tiny to original size
    /// </summary>
    public void StartGrowAnimation()
    {
        if (!isInitialized)
        {
            Initialize();
        }
        
        // Stop any existing animation
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }
        
        // Start the grow animation
        currentAnimation = StartCoroutine(GrowAnimationCoroutine());
        
        if (debugMode)
        {
        }
    }
    
    /// <summary>
    /// Instantly shrinks the object to tiny size (for immediate deactivation)
    /// </summary>
    public void InstantShrink()
    {
        if (!isInitialized)
        {
            Initialize();
        }
        
        // Stop any running animation
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
        
        // Set to tiny scale
        transform.localScale = Vector3.one * startScale;
        
        if (debugMode)
        {
        }
    }
    
    private IEnumerator GrowAnimationCoroutine()
    {
        float elapsedTime = 0f;
        Vector3 startScaleVec = Vector3.one * startScale;
        
        // Ensure we start from tiny scale
        transform.localScale = startScaleVec;
        
        while (elapsedTime < growDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / growDuration;
            
            // Apply the animation curve
            float curveValue = growCurve.Evaluate(progress);
            
            // Interpolate between tiny scale and original scale
            Vector3 currentScale = Vector3.Lerp(startScaleVec, originalScale, curveValue);
            transform.localScale = currentScale;
            
            yield return null;
        }
        
        // Ensure we end at exactly the original scale
        transform.localScale = originalScale;
        currentAnimation = null;
        
        if (debugMode)
        {
        }
    }
    
    /// <summary>
    /// Manually set the original scale (useful if scale changes after initialization)
    /// </summary>
    public void SetOriginalScale(Vector3 newOriginalScale)
    {
        originalScale = newOriginalScale;
        
        if (debugMode)
        {
        }
    }
    
    /// <summary>
    /// Get the current original scale
    /// </summary>
    public Vector3 GetOriginalScale()
    {
        return originalScale;
    }
    
    #if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure grow duration is positive
        if (growDuration <= 0f)
        {
            growDuration = 0.1f;
        }
        
        // Ensure start scale is very small but not zero
        if (startScale <= 0f)
        {
            startScale = 0.001f;
        }
    }
    #endif
}