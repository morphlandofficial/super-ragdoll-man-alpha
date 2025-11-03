using UnityEngine;

public class AddMeshCollidersToChildren : MonoBehaviour
{
    [Header("Collider Settings")]
    [SerializeField] private bool convex = false;
    [SerializeField] private bool isTrigger = false;
    [SerializeField] private bool includeInactive = false;
    
    [Header("Filter Settings")]
    [SerializeField] private bool skipObjectsWithExistingColliders = true;
    [SerializeField] private string[] excludeNames = new string[0]; // Objects to exclude by name
    
    // [Header("Debug")]
    // [SerializeField] private bool showDebugLogs = false; // Unused after debug log cleanup // Set to false for release
    
    [ContextMenu("Add Mesh Colliders to All Children")]
    public void AddMeshCollidersToAllChildren()
    {
        int collidersAdded = 0;
        int objectsSkipped = 0;
        
        // Get all MeshFilter components in children (including this object)
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(includeInactive);
        
        foreach (MeshFilter meshFilter in meshFilters)
        {
            GameObject obj = meshFilter.gameObject;
            
            // Skip if object name is in exclude list
            if (ShouldExcludeObject(obj.name))
            {
                // if (showDebugLogs)

                objectsSkipped++;
                continue;
            }
            
            // Skip if object already has a collider and we're set to skip existing colliders
            if (skipObjectsWithExistingColliders && HasAnyCollider(obj))
            {
                // if (showDebugLogs)

                objectsSkipped++;
                continue;
            }
            
            // Add MeshCollider
            MeshCollider meshCollider = obj.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = obj.AddComponent<MeshCollider>();
            }
            
            // Configure the collider
            meshCollider.convex = convex;
            meshCollider.isTrigger = isTrigger;
            
            collidersAdded++;
            
            // if (showDebugLogs)

            
        }
        
    }
    
    private bool ShouldExcludeObject(string objectName)
    {
        foreach (string excludeName in excludeNames)
        {
            if (!string.IsNullOrEmpty(excludeName) && objectName.Contains(excludeName))
            {
                return true;
            }
        }
        return false;
    }
    
    private bool HasAnyCollider(GameObject obj)
    {
        return obj.GetComponent<Collider>() != null;
    }
    
    [ContextMenu("Remove All Mesh Colliders from Children")]
    public void RemoveAllMeshCollidersFromChildren()
    {
        int collidersRemoved = 0;
        
        MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>(includeInactive);
        
        foreach (MeshCollider meshCollider in meshColliders)
        {
            // if (showDebugLogs)

            
            DestroyImmediate(meshCollider);
            collidersRemoved++;
        }
        
    }
    
    [ContextMenu("Count Objects with MeshFilters")]
    public void CountObjectsWithMeshFilters()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(includeInactive);
        int withColliders = 0;
        int withoutColliders = 0;
        
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (HasAnyCollider(meshFilter.gameObject))
                withColliders++;
            else
                withoutColliders++;
        }
        
    }
}