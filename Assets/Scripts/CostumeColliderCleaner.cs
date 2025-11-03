using UnityEngine;

/// <summary>
/// Removes colliders from visual-only costume elements that shouldn't have physics.
/// Attach this to the root character GameObject and it will clean up all costumes on Awake.
/// </summary>
public class CostumeColliderCleaner : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Object names that should have their colliders removed (visual-only elements)")]
    [SerializeField] private string[] visualOnlyObjects = new string[]
    {
        "PROJECTION PLANE",  // TV screen effect
        "Buttcrack",         // Decorative plane
        "TV_with_cord",      // TV visual elements
        // Add more visual-only element names here as needed
    };
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    private void Awake()
    {
        CleanAllCostumes();
    }
    
    /// <summary>
    /// Clean colliders from all costume hierarchies (including inactive ones)
    /// </summary>
    private void CleanAllCostumes()
    {
        int totalRemoved = 0;
        
        // Find all costume hierarchies (check for Animated/Physical structure)
        foreach (Transform child in transform)
        {
            // Skip camera and other systems
            if (child.name.Contains("Camera") || child.name.Contains("System"))
                continue;
            
            // Check if it has Animated/Physical structure (costume hierarchy)
            bool hasAnimated = child.Find("Animated") != null;
            bool hasPhysical = child.Find("Physical") != null;
            
            if (hasAnimated && hasPhysical)
            {
                // Clean this costume's visual-only objects
                int removed = CleanCostume(child.gameObject);
                totalRemoved += removed;
                
                if (showDebugLogs && removed > 0)
                {
                    Debug.Log($"<color=green>[CostumeColliderCleaner]</color> Removed {removed} colliders from costume: {child.name}");
                }
            }
        }
        
        if (totalRemoved > 0 && showDebugLogs)
        {
            Debug.Log($"<color=green>[CostumeColliderCleaner]</color> ✅ Total colliders removed from visual elements: {totalRemoved}");
        }
    }
    
    /// <summary>
    /// Clean colliders from visual-only objects in a specific costume
    /// </summary>
    private int CleanCostume(GameObject costume)
    {
        int removed = 0;
        
        // Get ALL colliders in this costume (including inactive)
        Collider[] colliders = costume.GetComponentsInChildren<Collider>(true);
        
        foreach (Collider collider in colliders)
        {
            // Check if this object is in the visual-only list
            if (IsVisualOnlyObject(collider.gameObject.name))
            {
                if (showDebugLogs)
                {
                    Debug.Log($"<color=yellow>[CostumeColliderCleaner]</color> Removing {collider.GetType().Name} from: {GetGameObjectPath(collider.gameObject)}");
                }
                
                // Check if it's a rigidbody (shouldn't be on visual elements)
                Rigidbody rb = collider.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Debug.LogWarning($"<color=orange>[CostumeColliderCleaner]</color> ⚠️ Visual element has Rigidbody! Removing from: {collider.gameObject.name}");
                    Destroy(rb);
                }
                
                Destroy(collider);
                removed++;
            }
        }
        
        return removed;
    }
    
    /// <summary>
    /// Check if an object name matches our visual-only list
    /// </summary>
    private bool IsVisualOnlyObject(string objectName)
    {
        foreach (string visualName in visualOnlyObjects)
        {
            if (!string.IsNullOrEmpty(visualName) && objectName.Contains(visualName))
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get the full hierarchy path of a GameObject (for debugging)
    /// </summary>
    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null && parent != transform)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
    
    /// <summary>
    /// Manually trigger cleaning (useful for runtime costume swaps)
    /// </summary>
    [ContextMenu("Clean All Costumes Now")]
    public void CleanNow()
    {
        CleanAllCostumes();
    }
}


