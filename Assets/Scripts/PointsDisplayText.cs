using UnityEngine;
using TMPro; // Remove this line if using regular UI.Text

/// <summary>
/// Attach this to a TextMeshPro component to display points from RagdollPointsSystem.
/// Automatically finds the RagdollPointsSystem in the scene.
/// </summary>
[RequireComponent(typeof(TMP_Text))] // Change to Text if using regular UI
public class PointsDisplayText : MonoBehaviour
{
    [Header("--- AUTO SETUP ---")]
    [Tooltip("Leave empty to auto-find in scene")]
    [SerializeField] private RagdollPointsSystem pointsSystem;
    
    [Header("--- DISPLAY FORMAT ---")]
    [SerializeField] private string displayFormat = "Points: {0:F0}";
    [Tooltip("Use {0} for the points value. F0 = no decimals, F1 = 1 decimal, F2 = 2 decimals")]
    
    private TMP_Text textComponent; // Change to Text if using regular UI
    
    private void Awake()
    {
        // Get the text component on this GameObject
        textComponent = GetComponent<TMP_Text>(); // Change to GetComponent<Text>() if using regular UI
        
        // Auto-find the points system if not assigned
        if (pointsSystem == null)
        {
            pointsSystem = FindFirstObjectByType<RagdollPointsSystem>();
            
            if (pointsSystem == null)
            {
// Debug.LogError("PointsDisplayText: Could not find RagdollPointsSystem in scene!");
            }
            else
            {
            }
        }
    }
    
    private void Update()
    {
        // Update the text every frame
        if (pointsSystem != null && textComponent != null)
        {
            float points = pointsSystem.CurrentPoints;
            textComponent.text = string.Format(displayFormat, points);
        }
    }
}



