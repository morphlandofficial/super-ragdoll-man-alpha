using UnityEngine;
using TMPro; // Or use UnityEngine.UI.Text if not using TextMeshPro

/// <summary>
/// Manages the UI display for the RagdollPointsSystem.
/// This component lives in the scene permanently and updates UI based on the persistent points system.
/// </summary>
public class PointsUIManager : MonoBehaviour
{
    [Header("--- REFERENCES ---")]
    [SerializeField] private RagdollPointsSystem pointsSystem;
    
    [Header("--- UI ELEMENTS ---")]
    [SerializeField] private TMP_Text currentPointsText;
    [SerializeField] private TMP_Text pointsPerSecondText;
    [SerializeField] private TMP_Text totalPointsText;
    
    [Header("--- DISPLAY SETTINGS ---")]
    [SerializeField] private bool showCurrentPoints = true;
    [SerializeField] private bool showPointsPerSecond = true;
    [SerializeField] private bool showTotalPoints = false;
    
    [Header("--- FORMAT SETTINGS ---")]
    [SerializeField] private string currentPointsFormat = "Points: {0:F0}";
    [SerializeField] private string pointsPerSecondFormat = "{0:F1} pts/sec";
    [SerializeField] private string totalPointsFormat = "Total: {0:F0}";
    
    [Header("--- COLOR CODING ---")]
    [SerializeField] private bool useColorCoding = true;
    [SerializeField] private Color positiveColor = Color.green;
    [SerializeField] private Color negativeColor = Color.red;
    [SerializeField] private Color neutralColor = Color.white;
    
    private void Awake()
    {
        // Use singleton instead of expensive FindObjectOfType
        if (pointsSystem == null)
        {
            pointsSystem = RagdollPointsSystem.Instance;
            if (pointsSystem == null)
            {
// Debug.LogError("PointsUIManager: No RagdollPointsSystem instance found in scene!");
            }
        }
    }
    
    private void Update()
    {
        if (pointsSystem == null) return;
        
        // Update current points display
        if (showCurrentPoints && currentPointsText != null)
        {
            float points = pointsSystem.CurrentPoints;
            currentPointsText.text = string.Format(currentPointsFormat, points);
            
            // Apply color coding
            if (useColorCoding)
            {
                if (points > 0)
                    currentPointsText.color = positiveColor;
                else if (points < 0)
                    currentPointsText.color = negativeColor;
                else
                    currentPointsText.color = neutralColor;
            }
        }
        
        // Update points per second display
        if (showPointsPerSecond && pointsPerSecondText != null)
        {
            float pps = pointsSystem.SmoothedPointsPerSecond;
            pointsPerSecondText.text = string.Format(pointsPerSecondFormat, pps);
            
            // Apply color coding
            if (useColorCoding)
            {
                if (pps > 0)
                    pointsPerSecondText.color = positiveColor;
                else if (pps < 0)
                    pointsPerSecondText.color = negativeColor;
                else
                    pointsPerSecondText.color = neutralColor;
            }
        }
        
        // Update total points display
        if (showTotalPoints && totalPointsText != null)
        {
            totalPointsText.text = string.Format(totalPointsFormat, pointsSystem.TotalPointsEarned);
        }
    }
}

