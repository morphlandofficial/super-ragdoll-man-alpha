using UnityEngine;
using TMPro; // Remove this line if using regular UI.Text

/// <summary>
/// Displays the final score when the level is completed.
/// Attach this to a UI GameObject (can be the text itself or a parent panel).
/// </summary>
public class LevelFinishUI : MonoBehaviour
{
    [Header("--- UI REFERENCES ---")]
    [SerializeField] private TMP_Text finalScoreText;
    [Tooltip("The text component to display final score. Leave empty to use component on this GameObject")]
    
    [Header("--- DISPLAY FORMAT ---")]
    [SerializeField] private string displayFormat = "FINAL SCORE: {0:F0}";
    [Tooltip("Use {0} for total score")]
    
    [SerializeField] private bool showBreakdown = false;
    [SerializeField] private string breakdownFormat = "FINAL SCORE: {0:F0}\nPhysics: {1:F0}\nTime Bonus: {2:F0}";
    [Tooltip("Use {0} for total, {1} for physics points, {2} for time bonus")]
    
    [Header("--- VISIBILITY ---")]
    [SerializeField] private bool hideOnStart = true;
    [Tooltip("Hide the UI element until level is complete")]
    
    [Header("--- COUNT-UP ANIMATION ---")]
    [SerializeField] private bool animateCountUp = true;
    [SerializeField] private float countUpDuration = 2f;
    [Tooltip("How long the count-up animation takes in seconds")]
    
    // Animation state
    private bool isAnimating = false;
    private float animationStartTime;
    private float targetScore;
    private float targetPhysicsPoints;
    private float targetTimeBonus;
    private float currentDisplayScore = 0f;
    
    private void Start()
    {
        // Auto-find text component if not assigned
        if (finalScoreText == null)
        {
            finalScoreText = GetComponent<TMP_Text>();
            
            // If still null, try to find in children
            if (finalScoreText == null)
            {
                finalScoreText = GetComponentInChildren<TMP_Text>();
            }
        }
        
        // Force center alignment
        if (finalScoreText != null)
        {
            finalScoreText.alignment = TMPro.TextAlignmentOptions.Center;
            
            // Also center the RectTransform
            RectTransform rectTransform = finalScoreText.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Set anchors to center
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
        
        // Always reset animation state
        isAnimating = false;
        
        // Hide on start if enabled
        if (hideOnStart && finalScoreText != null)
        {
            finalScoreText.text = "";
            finalScoreText.enabled = false;
        }
    }
    
    private void Update()
    {
        if (isAnimating)
        {
            float elapsedTime = Time.time - animationStartTime;
            float progress = Mathf.Clamp01(elapsedTime / countUpDuration);
            
            // Smooth easing (ease out)
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            
            currentDisplayScore = Mathf.Lerp(0f, targetScore, easedProgress);
            
            // Update text
            if (showBreakdown)
            {
                float displayPhysics = Mathf.Lerp(0f, targetPhysicsPoints, easedProgress);
                float displayTimeBonus = Mathf.Lerp(0f, targetTimeBonus, easedProgress);
                finalScoreText.text = string.Format(breakdownFormat, currentDisplayScore, displayPhysics, displayTimeBonus);
            }
            else
            {
                finalScoreText.text = string.Format(displayFormat, currentDisplayScore);
            }
            
            // Stop animating when complete
            if (progress >= 1f)
            {
                isAnimating = false;
            }
        }
    }
    
    /// <summary>
    /// Display the final score
    /// </summary>
    public void ShowFinalScore(float totalScore, float physicsPoints, float timeBonus)
    {
        if (finalScoreText != null)
        {
            // MULTIPLAYER FIX: Ensure the entire GameObject hierarchy is active
            // Walk up the parent chain and activate all GameObjects up to the root
            EnsureHierarchyActive(gameObject);
            
            // Also ensure the text's GameObject is active (in case it's on a child)
            if (finalScoreText.gameObject != gameObject)
            {
                EnsureHierarchyActive(finalScoreText.gameObject);
            }
            
            // Show the UI
            finalScoreText.enabled = true;
            
            Debug.Log($"<color=cyan>[LevelFinishUI]</color> Showing final score: {totalScore:F0} (Physics: {physicsPoints:F0}, Time: {timeBonus:F0})");
            
            if (animateCountUp)
            {
                // Start count-up animation
                targetScore = totalScore;
                targetPhysicsPoints = physicsPoints;
                targetTimeBonus = timeBonus;
                currentDisplayScore = 0f;
                animationStartTime = Time.time;
                isAnimating = true;
            }
            else
            {
                // Show immediately without animation
                if (showBreakdown)
                {
                    finalScoreText.text = string.Format(breakdownFormat, totalScore, physicsPoints, timeBonus);
                }
                else
                {
                    finalScoreText.text = string.Format(displayFormat, totalScore);
                }
            }
        }
        else
        {
            Debug.LogWarning("<color=red>[LevelFinishUI]</color> No text component assigned!");
        }
    }
    
    /// <summary>
    /// Ensure a GameObject and all its parents are active
    /// </summary>
    private void EnsureHierarchyActive(GameObject obj)
    {
        if (obj == null) return;
        
        // Activate this GameObject
        if (!obj.activeSelf)
        {
            obj.SetActive(true);
            Debug.Log($"<color=yellow>[LevelFinishUI]</color> Activated '{obj.name}' in hierarchy");
        }
        
        // Recursively activate parent
        if (obj.transform.parent != null)
        {
            EnsureHierarchyActive(obj.transform.parent.gameObject);
        }
    }
}

