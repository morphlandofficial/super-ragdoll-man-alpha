using UnityEngine;
using TMPro;

/// <summary>
/// Attach this to a TextMeshPro component to display remaining lives from Battle Royale Manager.
/// Automatically finds the Battle Royale Manager and hides when not present or lives are infinite.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LivesDisplayText : MonoBehaviour
{
    [Header("--- DISPLAY FORMAT ---")]
    [SerializeField] private string displayFormat = "Lives: {0}";
    [Tooltip("Use {0} for the lives count. Example: 'Lives: {0}' or '♥ {0}'")]
    
    [Header("--- LOW LIVES WARNING ---")]
    [SerializeField] private bool enableLowLivesWarning = true;
    [SerializeField] private int lowLivesThreshold = 1;
    [Tooltip("Lives count at which text turns to warning color (default: 1 = last life)")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowLivesColor = Color.red;
    
    [Header("--- AUTO HIDE ---")]
    [SerializeField] private bool hideWhenNoManager = true;
    [Tooltip("If true, hides the text when no Battle Royale Manager found in scene")]
    [SerializeField] private bool hideWhenInfiniteLives = true;
    [Tooltip("If true, hides the text when lives mode is infinite")]
    
    private TMP_Text textComponent;
    private BattleRoyaleManager battleRoyaleManager;
    private Color originalColor;
    private RectTransform rectTransform;
    
    private void Awake()
    {
        // Get the text component on this GameObject
        textComponent = GetComponent<TMP_Text>();
        originalColor = textComponent.color;
        
        if (textComponent == null)
        {
            Debug.LogError("[LivesDisplayText] No TextMeshPro component found!");
        }
        
        // Get RectTransform and position in bottom-right corner (top of stack)
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Set anchor to bottom-right
            rectTransform.anchorMin = new Vector2(1, 0); // Bottom-right
            rectTransform.anchorMax = new Vector2(1, 0); // Bottom-right
            rectTransform.pivot = new Vector2(1, 0); // Pivot at bottom-right
            
            // Position at top of stack with proper spacing
            rectTransform.anchoredPosition = new Vector2(-20, 120); // 20 pixels from right, 120 pixels from bottom
            
            // Set text alignment to right
            if (textComponent != null)
            {
                textComponent.alignment = TMPro.TextAlignmentOptions.BottomRight;
                textComponent.enableWordWrapping = false; // Prevent text wrapping
                textComponent.overflowMode = TMPro.TextOverflowModes.Overflow; // Allow overflow instead of wrapping
            }
        }
    }
    
    private void Update()
    {
        // Find manager if not found yet (lazy loading)
        if (battleRoyaleManager == null)
        {
            FindBattleRoyaleManager();
            
            if (battleRoyaleManager == null)
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
        
        // Check if lives mode is infinite
        BattleRoyaleManager.LivesMode livesMode = battleRoyaleManager.GetLivesMode();
        
        // Hide/show based on lives mode
        if (hideWhenInfiniteLives && livesMode == BattleRoyaleManager.LivesMode.Infinite)
        {
            // Infinite lives - hide the display
            if (textComponent.enabled)
            {
                textComponent.enabled = false;
            }
            return;
        }
        
        // Show the display
        if (!textComponent.enabled)
        {
            textComponent.enabled = true;
        }
        
        // Get current lives
        int currentLives = battleRoyaleManager.GetCurrentLives();
        
        // Display lives count
        textComponent.text = string.Format(displayFormat, currentLives);
        
        // Apply color coding based on lives level
        if (enableLowLivesWarning && currentLives <= lowLivesThreshold)
        {
            textComponent.color = lowLivesColor;
        }
        else
        {
            textComponent.color = normalColor;
        }
    }
    
    /// <summary>
    /// Find the Battle Royale Manager in the scene
    /// </summary>
    private void FindBattleRoyaleManager()
    {
        battleRoyaleManager = FindFirstObjectByType<BattleRoyaleManager>();
        
        if (battleRoyaleManager == null && !hideWhenNoManager)
        {
            // Debug.LogWarning("[LivesDisplayText] No Battle Royale Manager found in scene!");
        }
    }
}

