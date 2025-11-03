using UnityEngine;

/// <summary>
/// Manages render resolution scaling for performance optimization.
/// Renders the game at a lower resolution and upscales to display resolution.
/// </summary>
public class RenderScaleManager : MonoBehaviour
{
    [Header("Render Scale Settings")]
    [Tooltip("Resolution scale multiplier (0.5 = 50%, 1.0 = 100% native)")]
    [Range(0.5f, 1.0f)]
    [SerializeField] private float renderScale = 0.8f;
    
    [Tooltip("Apply the render scale on start")]
    [SerializeField] private bool applyOnStart = true;
    
    [Tooltip("Save and load the render scale setting")]
    [SerializeField] private bool saveSettings = true;
    
    private const string RENDER_SCALE_KEY = "RenderScale";
    
    private int originalWidth;
    private int originalHeight;
    private bool fullScreen;
    
    private void Awake()
    {
        // Store original resolution
        originalWidth = Screen.width;
        originalHeight = Screen.height;
        fullScreen = Screen.fullScreen;
        
        // Load saved setting if enabled
        if (saveSettings && PlayerPrefs.HasKey(RENDER_SCALE_KEY))
        {
            renderScale = PlayerPrefs.GetFloat(RENDER_SCALE_KEY);
        }
    }
    
    private void Start()
    {
        if (applyOnStart)
        {
            ApplyRenderScale();
        }
    }
    
    /// <summary>
    /// Apply the current render scale setting
    /// </summary>
    public void ApplyRenderScale()
    {
        // Calculate scaled resolution
        int scaledWidth = Mathf.RoundToInt(originalWidth * renderScale);
        int scaledHeight = Mathf.RoundToInt(originalHeight * renderScale);
        
        // Apply the new resolution
        Screen.SetResolution(scaledWidth, scaledHeight, fullScreen);
        
        Debug.Log($"Render Scale Applied: {renderScale:P0} ({scaledWidth}x{scaledHeight})");
        
        // Save setting if enabled
        if (saveSettings)
        {
            PlayerPrefs.SetFloat(RENDER_SCALE_KEY, renderScale);
            PlayerPrefs.Save();
        }
    }
    
    /// <summary>
    /// Set render scale to a specific value and apply it
    /// </summary>
    public void SetRenderScale(float scale)
    {
        renderScale = Mathf.Clamp(scale, 0.5f, 1.0f);
        ApplyRenderScale();
    }
    
    /// <summary>
    /// Reset to native resolution (100%)
    /// </summary>
    public void ResetToNative()
    {
        SetRenderScale(1.0f);
    }
    
    /// <summary>
    /// Preset: Performance mode (75%)
    /// </summary>
    public void SetPerformanceMode()
    {
        SetRenderScale(0.75f);
    }
    
    /// <summary>
    /// Preset: Balanced mode (85%)
    /// </summary>
    public void SetBalancedMode()
    {
        SetRenderScale(0.85f);
    }
    
    /// <summary>
    /// Preset: Quality mode (100%)
    /// </summary>
    public void SetQualityMode()
    {
        SetRenderScale(1.0f);
    }
    
    // Allow runtime adjustment in inspector
    private void OnValidate()
    {
        // Clamp the value when changed in inspector
        renderScale = Mathf.Clamp(renderScale, 0.5f, 1.0f);
    }
    
#if UNITY_EDITOR
    [ContextMenu("Apply Render Scale (Editor)")]
    private void ApplyRenderScaleEditor()
    {
        ApplyRenderScale();
    }
    
    [ContextMenu("Reset to Native (Editor)")]
    private void ResetToNativeEditor()
    {
        ResetToNative();
    }
#endif
}

