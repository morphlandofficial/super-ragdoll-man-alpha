using UnityEngine;

/// <summary>
/// Procedurally generates prefab instances in a defined box volume at runtime.
/// Place this on an empty GameObject, configure the box size and generation parameters,
/// then position/rotate/scale the GameObject to place the spawn volume where you want it.
/// Works with any prefab - collectibles, obstacles, decorations, etc.
/// </summary>
public class Generator : MonoBehaviour
{
    [Header("Prefab Reference")]
    [SerializeField]
    [Tooltip("The prefab to spawn")]
    private GameObject prefabToSpawn;
    
    [Header("Spawn Volume")]
    [SerializeField]
    [Tooltip("Size of the box volume where objects will spawn")]
    private Vector3 boxSize = new Vector3(10f, 10f, 10f);
    
    [Header("Generation Settings")]
    [SerializeField]
    [Tooltip("Number of objects to generate")]
    [Range(1, 500)]
    private int spawnCount = 20;
    
    [SerializeField]
    [Tooltip("Enable random size variation for spawned objects")]
    private bool enableSizeVariation = true;
    
    [SerializeField]
    [Tooltip("Minimum scale multiplier for size variation")]
    [Range(0.1f, 2f)]
    private float minSizeMultiplier = 0.7f;
    
    [SerializeField]
    [Tooltip("Maximum scale multiplier for size variation")]
    [Range(0.1f, 2f)]
    private float maxSizeMultiplier = 1.3f;
    
    [Header("Optional Settings")]
    [SerializeField]
    [Tooltip("Parent spawned objects to this generator (keeps hierarchy clean)")]
    private bool parentToGenerator = true;
    
    [SerializeField]
    [Tooltip("Random seed for reproducible generation (0 = random each time)")]
    private int randomSeed = 0;
    
    [SerializeField]
    [Tooltip("Enable collision checking to prevent overlapping spawns (slower but cleaner)")]
    private bool preventOverlaps = false;
    
    [SerializeField]
    [Tooltip("Minimum distance between spawned objects when preventing overlaps")]
    private float minDistanceBetweenObjects = 2f;
    
    [SerializeField]
    [Tooltip("Maximum attempts to find a non-overlapping position")]
    private int maxPlacementAttempts = 30;
    
    [Header("Gizmo Visualization")]
    [SerializeField]
    [Tooltip("Color of the box gizmo in the editor")]
    private Color gizmoColor = new Color(0f, 1f, 1f, 0.3f);
    
    [SerializeField]
    [Tooltip("Show wireframe outline of the box")]
    private bool showWireframe = true;
    
    [Header("Debug")]
    [SerializeField]
    // private bool showDebugMessages = false; // Unused after debug log cleanup // Set to false for release
    
    // Internal state
    private GameObject spawnedObjectsContainer;
    
    private void Start()
    {
        Generate();
    }
    
    /// <summary>
    /// Generates all objects at runtime.
    /// </summary>
    public void Generate()
    {
        if (prefabToSpawn == null)
        {
// Debug.LogError($"Generator on {gameObject.name}: No prefab assigned! Please assign a prefab to spawn.");
            return;
        }
        
        // Initialize random seed if specified
        if (randomSeed != 0)
        {
            Random.InitState(randomSeed);
        }
        
        // Create container for organization
        if (parentToGenerator)
        {
            spawnedObjectsContainer = new GameObject("Generated Objects");
            spawnedObjectsContainer.transform.SetParent(transform);
            spawnedObjectsContainer.transform.localPosition = Vector3.zero;
            spawnedObjectsContainer.transform.localRotation = Quaternion.identity;
            spawnedObjectsContainer.transform.localScale = Vector3.one;
        }
        
        // Store spawned positions for overlap prevention
        Vector3[] spawnedPositions = new Vector3[spawnCount];
        
        // Generate objects
        int successfulSpawns = 0;
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 localPosition = Vector3.zero;
            bool foundValidPosition = false;
            
            // Try to find a valid position
            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                // Generate random position within the box
                localPosition = new Vector3(
                    Random.Range(-boxSize.x / 2f, boxSize.x / 2f),
                    Random.Range(-boxSize.y / 2f, boxSize.y / 2f),
                    Random.Range(-boxSize.z / 2f, boxSize.z / 2f)
                );
                
                // Check for overlaps if enabled
                if (preventOverlaps)
                {
                    bool tooClose = false;
                    for (int j = 0; j < successfulSpawns; j++)
                    {
                        float distance = Vector3.Distance(localPosition, spawnedPositions[j]);
                        if (distance < minDistanceBetweenObjects)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    
                    if (!tooClose)
                    {
                        foundValidPosition = true;
                        break;
                    }
                }
                else
                {
                    foundValidPosition = true;
                    break;
                }
            }
            
            // Skip this spawn if we couldn't find a valid position
            if (!foundValidPosition && preventOverlaps)
            {
                if (false) // showDebugMessages
                {
// Debug.LogWarning($"Generator: Could not find valid position for object {i + 1} after {maxPlacementAttempts} attempts");
                }
                continue;
            }
            
            // Convert local position to world position
            Vector3 worldPosition = transform.TransformPoint(localPosition);
            
            // Spawn the object
            GameObject spawnedObject = Instantiate(prefabToSpawn, worldPosition, Quaternion.identity);
            
            // Apply size variation if enabled
            if (enableSizeVariation)
            {
                float sizeMultiplier = Random.Range(minSizeMultiplier, maxSizeMultiplier);
                spawnedObject.transform.localScale = prefabToSpawn.transform.localScale * sizeMultiplier;
            }
            
            // Parent to container if needed
            if (parentToGenerator && spawnedObjectsContainer != null)
            {
                spawnedObject.transform.SetParent(spawnedObjectsContainer.transform);
            }
            
            // Store position for overlap checking
            if (preventOverlaps)
            {
                spawnedPositions[successfulSpawns] = localPosition;
            }
            
            successfulSpawns++;
        }
        
        if (false) // showDebugMessages
        {
        }
    }
    
    /// <summary>
    /// Clears all generated objects (useful for testing in editor).
    /// </summary>
    public void ClearGeneratedObjects()
    {
        if (spawnedObjectsContainer != null)
        {
            DestroyImmediate(spawnedObjectsContainer);
            spawnedObjectsContainer = null;
        }
        else
        {
            // Fallback: find and destroy all instances of the prefab
            if (prefabToSpawn != null)
            {
                GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (GameObject obj in allObjects)
                {
                    if (obj.name.StartsWith(prefabToSpawn.name))
                    {
                        DestroyImmediate(obj);
                    }
                }
            }
        }
        
        if (false) // showDebugMessages
        {
        }
    }
    
    /// <summary>
    /// Draws the spawn volume gizmo in the editor.
    /// </summary>
    private void OnDrawGizmos()
    {
        // Set gizmo color
        Gizmos.color = gizmoColor;
        
        // Draw filled box
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, boxSize);
        
        // Draw wireframe if enabled
        if (showWireframe)
        {
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(Vector3.zero, boxSize);
        }
    }
    
    /// <summary>
    /// Draws the gizmo when this object is selected (brighter).
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Brighter version when selected
        Color selectedColor = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.6f);
        Gizmos.color = selectedColor;
        
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, boxSize);
        
        if (showWireframe)
        {
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(Vector3.zero, boxSize);
        }
    }
}

