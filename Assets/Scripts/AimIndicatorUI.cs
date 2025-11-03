using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Duck Hunt style aim indicator - shows a single red dot where you aimed
/// Only ONE dot exists at a time - new shots reposition the same dot
/// Attach this to your UI Canvas GameObject
/// AUTOMATICALLY DISABLED in multiplayer mode (split-screen doesn't work well with single cursor)
/// </summary>
public class AimIndicatorUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image aimDotImage; // Optional: Will auto-create if not assigned
    
    [Header("Indicator Settings")]
    [SerializeField] private float fadeDelay = 1f; // Time before fade starts
    [SerializeField] private float fadeOutTime = 0.3f; // How fast it fades once started
    [SerializeField] private float dotSize = 30f;
    [SerializeField] private Color dotColor = new Color(1f, 0f, 0f, 1f); // Red
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    [Header("Position Mode")]
    [SerializeField] private bool useScreenCenter = true;
    [Tooltip("If true, always shows at screen center. If false, shows at 3D world position")]
    
    private Camera mainCamera; // Auto-assigned from player's CameraModule
    
    private RectTransform dotRectTransform;
    private Coroutine fadeCoroutine;
    private bool isInitialized = false;
    private bool isMultiplayerMode = false;
    
    private void Awake()
    {
        // Check if multiplayer mode is active (2+ controllers)
        isMultiplayerMode = (Gamepad.all.Count >= 2);
        
        if (isMultiplayerMode)
        {
            // Multiplayer detected - disable aim indicator completely
            Debug.Log("<color=yellow>[AimIndicatorUI]</color> Multiplayer mode detected - aim cursor disabled");
            
            // Get canvas reference if needed
            if (canvas == null)
                canvas = GetComponent<Canvas>();
            
            // Hide the aim dot if it was already assigned in the inspector
            if (aimDotImage != null)
            {
                aimDotImage.gameObject.SetActive(false);
            }
            
            // Disable this component (no need to update)
            enabled = false;
            
            return;
        }
        
        if (canvas == null)
            canvas = GetComponent<Canvas>();
        
        InitializeDot();
    }
    
    /// <summary>
    /// Set the camera to use for world-to-screen conversions
    /// Called automatically by DefaultBehaviour
    /// </summary>
    public void SetCamera(Camera camera)
    {
        mainCamera = camera;
    }
    
    private void InitializeDot()
    {
        if (aimDotImage == null)
        {
            // Auto-create the dot
            CreateDot();
        }
        else
        {
            dotRectTransform = aimDotImage.GetComponent<RectTransform>();
        }
        
        // Initially hide the dot
        if (aimDotImage != null)
        {
            aimDotImage.color = new Color(dotColor.r, dotColor.g, dotColor.b, 0f);
            dotRectTransform.sizeDelta = new Vector2(dotSize, dotSize);
        }
        
        isInitialized = true;
    }
    
    private void CreateDot()
    {
        // Create a new UI Image for the dot
        GameObject dotObj = new GameObject("AimDot");
        dotObj.transform.SetParent(canvas.transform, false);
        
        aimDotImage = dotObj.AddComponent<Image>();
        
        // Create a simple filled circle sprite programmatically to avoid resource loading errors
        // Unity's built-in resources may not be available on all platforms
        Texture2D circleTexture = CreateCircleTexture((int)dotSize);
        aimDotImage.sprite = Sprite.Create(
            circleTexture,
            new Rect(0, 0, circleTexture.width, circleTexture.height),
            new Vector2(0.5f, 0.5f)
        );
        
        aimDotImage.color = dotColor;
        aimDotImage.raycastTarget = false; // Don't block mouse clicks
        
        dotRectTransform = dotObj.GetComponent<RectTransform>();
        dotRectTransform.sizeDelta = new Vector2(dotSize, dotSize);
        dotRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        dotRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dotRectTransform.pivot = new Vector2(0.5f, 0.5f);
        dotRectTransform.anchoredPosition = Vector2.zero;
        
        // Debug.Log("<color=green>[AimIndicatorUI]</color> Red dot aim indicator created automatically!");
    }
    
    /// <summary>
    /// Creates a circular texture programmatically
    /// </summary>
    private Texture2D CreateCircleTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        
        float center = size * 0.5f;
        float radiusSquared = (size * 0.5f) * (size * 0.5f);
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distanceSquared = dx * dx + dy * dy;
                
                // Create a smooth circle with anti-aliasing
                float distance = Mathf.Sqrt(distanceSquared);
                float alpha = 1f - Mathf.Clamp01((distance - (size * 0.4f)) / (size * 0.1f));
                
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        
        return texture;
    }
    
    /// <summary>
    /// Show aim indicator at screen center (simple version for screen-space aiming)
    /// Call this every time a shot is fired
    /// </summary>
    public void ShowAimIndicator()
    {
        // Don't show in multiplayer mode
        if (isMultiplayerMode) return;
        
        if (!isInitialized || aimDotImage == null) return;
        
        // Position at screen center (0,0 in anchored position)
        dotRectTransform.anchoredPosition = Vector2.zero;
        
        // Reset and show the dot
        ResetDot();
    }
    
    /// <summary>
    /// Show aim indicator at a specific 3D world position
    /// Call this every time a shot is fired with the aim point in world space
    /// </summary>
    public void ShowAimIndicator(Vector3 worldPosition)
    {
        // Don't show in multiplayer mode
        if (isMultiplayerMode) return;
        
        if (!isInitialized || aimDotImage == null) return;
        
        // Get camera (use assigned camera or fallback to Camera.main)
        Camera cam = mainCamera != null ? mainCamera : Camera.main;
        if (cam == null) return;
        
        // Convert 3D world position to 2D screen position
        Vector3 screenPos = cam.WorldToScreenPoint(worldPosition);
        
        // Check if point is in front of camera
        if (screenPos.z < 0) 
        {
            // Behind camera - hide dot
            HideDot();
            return;
        }
        
        // Convert screen position to canvas position
        Vector2 canvasPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out canvasPos
        );
        
        // Position the dot
        dotRectTransform.anchoredPosition = canvasPos;
        
        // Reset and show the dot
        ResetDot();
    }
    
    /// <summary>
    /// Resets the dot to full opacity and starts the fade timer
    /// </summary>
    private void ResetDot()
    {
        // Stop any existing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        
        // Reset to full opacity
        aimDotImage.color = new Color(dotColor.r, dotColor.g, dotColor.b, 1f);
        
        // Start new fade timer
        fadeCoroutine = StartCoroutine(FadeOutAfterDelay());
    }
    
    /// <summary>
    /// Immediately hide the dot
    /// </summary>
    private void HideDot()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        
        aimDotImage.color = new Color(dotColor.r, dotColor.g, dotColor.b, 0f);
    }
    
    /// <summary>
    /// Waits for the delay, then fades out the dot
    /// </summary>
    private IEnumerator FadeOutAfterDelay()
    {
        // Wait before starting fade
        yield return new WaitForSeconds(fadeDelay);
        
        // Fade out
        float elapsed = 0f;
        
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutTime;
            float alpha = fadeCurve.Evaluate(1f - t); // Inverse curve (1 to 0)
            
            aimDotImage.color = new Color(dotColor.r, dotColor.g, dotColor.b, alpha);
            
            yield return null;
        }
        
        // Ensure fully transparent at end
        aimDotImage.color = new Color(dotColor.r, dotColor.g, dotColor.b, 0f);
        fadeCoroutine = null;
    }
    
    /// <summary>
    /// Update dot size at runtime (optional)
    /// </summary>
    public void SetDotSize(float size)
    {
        dotSize = size;
        if (dotRectTransform != null)
        {
            dotRectTransform.sizeDelta = new Vector2(dotSize, dotSize);
        }
    }
    
    /// <summary>
    /// Update dot color at runtime (optional)
    /// </summary>
    public void SetDotColor(Color color)
    {
        dotColor = color;
        if (aimDotImage != null)
        {
            float currentAlpha = aimDotImage.color.a;
            aimDotImage.color = new Color(color.r, color.g, color.b, currentAlpha);
        }
    }
}

