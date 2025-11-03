using UnityEngine;
using TMPro;

/// <summary>
/// Attach this to a TextMeshPro component to display collectible progress from CollectibleFinishManager.
/// Shows "Collectibles: X/Y" format with color coding based on progress.
/// Automatically finds the CollectibleFinishManager and hides if not present.
/// MULTIPLAYER SUPPORT: Works with MultiplayerUIManager - will be duplicated per player.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class CollectibleCounterUI : MonoBehaviour
{
    [Header("--- DISPLAY FORMAT ---")]
    [SerializeField] private string displayFormat = "⭐ {0} out of {1}";
    [Tooltip("Format string. {0} = collected count, {1} = TOTAL count. Example: '⭐ {0} out of {1}' or 'Collectibles: {0}/{1}'")]
    
    [Header("--- COLOR CODING ---")]
    [SerializeField] private bool enableColorCoding = true;
    [SerializeField] private Color belowRequiredColor = Color.white;
    [Tooltip("Color when collected < required amount (can't finish level yet)")]
    [SerializeField] private Color requiredMetColor = Color.yellow;
    [Tooltip("Color when collected >= required amount (level can be finished)")]
    [SerializeField] private Color allCollectedColor = Color.green;
    [Tooltip("Color when ALL collectibles are collected (including optional ones)")]
    
    [Header("--- AUTO HIDE ---")]
    [SerializeField] private bool hideWhenNoManager = true;
    [Tooltip("If true, hides when no CollectibleFinishManager is found in scene")]
    
    private TMP_Text textComponent;
    private CollectibleFinishManager collectibleManager;
    private bool hasManager = false;
    private RectTransform rectTransform;
    
    private void Awake()
    {
        // Get the text component on this GameObject
        textComponent = GetComponent<TMP_Text>();
        
        if (textComponent == null)
        {
            Debug.LogError("[CollectibleCounterUI] No TextMeshPro component found!");
        }
        
        // Get RectTransform and position in bottom-left corner
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Set anchor to bottom-left
            rectTransform.anchorMin = new Vector2(0, 0); // Bottom-left
            rectTransform.anchorMax = new Vector2(0, 0); // Bottom-left
            rectTransform.pivot = new Vector2(0, 0); // Pivot at bottom-left
            
            // Position at bottom-left corner (lowest position in stack)
            rectTransform.anchoredPosition = new Vector2(20, 20); // 20 pixels from left, 20 pixels from bottom
            
            // Set text alignment to left
            if (textComponent != null)
            {
                textComponent.alignment = TMPro.TextAlignmentOptions.BottomLeft;
                textComponent.enableWordWrapping = false; // Prevent text wrapping
                textComponent.overflowMode = TMPro.TextOverflowModes.Overflow; // Allow overflow instead of wrapping
            }
        }
    }
    
    private void Start()
    {
        // Find Collectible Finish Manager
        FindCollectibleManager();
    }
    
    private void Update()
    {
        // Find manager if not found yet (lazy loading)
        if (!hasManager)
        {
            FindCollectibleManager();
            
            if (!hasManager)
            {
                // No manager found - hide text if enabled
                if (hideWhenNoManager && textComponent != null)
                {
                    if (textComponent.enabled)
                    {
                        textComponent.enabled = false;
                    }
                }
                return;
            }
        }
        
        // Show the display
        if (!textComponent.enabled)
        {
            textComponent.enabled = true;
        }
        
        // Update display
        UpdateDisplay();
    }
    
    /// <summary>
    /// Find the CollectibleFinishManager in the scene
    /// </summary>
    private void FindCollectibleManager()
    {
        collectibleManager = FindFirstObjectByType<CollectibleFinishManager>();
        hasManager = (collectibleManager != null);
        
        if (!hasManager && !hideWhenNoManager)
        {
            Debug.LogWarning("[CollectibleCounterUI] No CollectibleFinishManager found in scene!");
        }
    }
    
    /// <summary>
    /// Update the text display based on current collection progress
    /// </summary>
    private void UpdateDisplay()
    {
        if (collectibleManager == null || textComponent == null) return;
        
        // Get collection data from the manager
        int collected = collectibleManager.GetCollectedCount();
        int required = collectibleManager.GetRequiredCount();
        int total = collectibleManager.GetTotalCount();
        
        // Update text - show collected out of TOTAL (not required)
        textComponent.text = string.Format(displayFormat, collected, total);
        
        // Apply color coding based on progress
        if (enableColorCoding)
        {
            if (collected >= total)
            {
                // ALL collectibles collected (including optional ones)
                textComponent.color = allCollectedColor;
            }
            else if (collected >= required)
            {
                // Required amount met - level can be finished (but optional ones remain)
                textComponent.color = requiredMetColor;
            }
            else
            {
                // Below required amount - can't finish level yet
                textComponent.color = belowRequiredColor;
            }
        }
    }
}

