using UnityEngine;
using ActiveRagdoll;

/// <summary>
/// Runtime costume swapper for Active Ragdoll characters.
/// Allows swapping between different costume hierarchies by activating/deactivating them.
/// </summary>
public class CostumeSwapper : MonoBehaviour
{
    [Header("Costume References")]
    [Tooltip("List all costume hierarchies as children of this GameObject")]
    [SerializeField] private GameObject[] costumes;
    
    [Header("Settings")]
    [Tooltip("Index of the costume to use at start (0-based)")]
    [SerializeField] private int defaultCostumeIndex = 0;
    
    private int currentCostumeIndex = 0;
    private ActiveRagdoll.ActiveRagdoll activeRagdoll;
    
    private void Start()
    {
        activeRagdoll = GetComponent<ActiveRagdoll.ActiveRagdoll>();
        
        if (activeRagdoll == null)
        {
            Debug.LogError("CostumeSwapper: No ActiveRagdoll component found!");
            enabled = false;
            return;
        }
        
        // Auto-find costumes if not assigned
        if (costumes == null || costumes.Length == 0)
        {
            AutoFindCostumes();
        }
        
        // Activate default costume
        if (costumes.Length > 0)
        {
            SwapToCostume(defaultCostumeIndex);
        }
    }
    
    /// <summary>
    /// Automatically finds all costume hierarchies as direct children.
    /// Looks for children that have "Animated" and "Physical" grandchildren.
    /// </summary>
    private void AutoFindCostumes()
    {
        var potentialCostumes = new System.Collections.Generic.List<GameObject>();
        
        foreach (Transform child in transform)
        {
            // Skip camera and other systems
            if (child.name.Contains("Camera") || child.name.Contains("System"))
                continue;
            
            // Check if it has Animated/Physical structure
            bool hasAnimated = child.Find("Animated") != null;
            bool hasPhysical = child.Find("Physical") != null;
            
            if (hasAnimated && hasPhysical)
            {
                potentialCostumes.Add(child.gameObject);
            }
        }
        
        costumes = potentialCostumes.ToArray();
        Debug.Log($"CostumeSwapper: Auto-found {costumes.Length} costumes");
    }
    
    /// <summary>
    /// Swaps to a specific costume by index.
    /// </summary>
    /// <param name="index">Index of the costume to activate</param>
    public void SwapToCostume(int index)
    {
        if (costumes == null || costumes.Length == 0)
        {
            Debug.LogError("CostumeSwapper: No costumes assigned!");
            return;
        }
        
        if (index < 0 || index >= costumes.Length)
        {
            Debug.LogError($"CostumeSwapper: Invalid costume index {index}. Valid range: 0-{costumes.Length - 1}");
            return;
        }
        
        // Deactivate all costumes
        foreach (var costume in costumes)
        {
            if (costume != null)
                costume.SetActive(false);
        }
        
        // Activate selected costume
        costumes[index].SetActive(true);
        currentCostumeIndex = index;
        
        // Reinitialize ActiveRagdoll to pick up new references
        RefreshActiveRagdoll();
        
        Debug.Log($"CostumeSwapper: Switched to costume '{costumes[index].name}'");
    }
    
    /// <summary>
    /// Cycles to the next costume in the list.
    /// </summary>
    public void CycleToNextCostume()
    {
        if (costumes == null || costumes.Length <= 1)
            return;
        
        int nextIndex = (currentCostumeIndex + 1) % costumes.Length;
        SwapToCostume(nextIndex);
    }
    
    /// <summary>
    /// Swaps to a costume by name.
    /// </summary>
    /// <param name="costumeName">Name of the costume GameObject</param>
    public void SwapToCostumeByName(string costumeName)
    {
        for (int i = 0; i < costumes.Length; i++)
        {
            if (costumes[i].name == costumeName)
            {
                SwapToCostume(i);
                return;
            }
        }
        
        Debug.LogError($"CostumeSwapper: Costume '{costumeName}' not found!");
    }
    
    /// <summary>
    /// Forces ActiveRagdoll to reinitialize and pick up references from the new active costume.
    /// </summary>
    private void RefreshActiveRagdoll()
    {
        if (activeRagdoll == null)
            return;
        
        // Use the new RefreshCostumeReferences method for clean costume swapping
        activeRagdoll.RefreshCostumeReferences();
        
        // Also refresh camera module's lookPoint
        var cameraModule = GetComponent<ActiveRagdoll.CameraModule>();
        if (cameraModule != null)
        {
            // CameraModule will auto-refresh on next Start, but we can help it
            cameraModule.enabled = false;
            cameraModule.enabled = true;
        }
    }
    
    /// <summary>
    /// Gets the name of the currently active costume.
    /// </summary>
    public string GetCurrentCostumeName()
    {
        if (costumes != null && currentCostumeIndex < costumes.Length)
            return costumes[currentCostumeIndex].name;
        return "None";
    }
    
    /// <summary>
    /// Gets the index of the currently active costume.
    /// </summary>
    public int GetCurrentCostumeIndex()
    {
        return currentCostumeIndex;
    }
}

