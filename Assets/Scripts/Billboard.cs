using UnityEngine;

public class Billboard : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Optional: Drag a GameObject or prefab here to look at. If empty, will auto-find camera.")]
    public Transform customTarget;
    
    [Header("Billboard Settings")]
    public bool lockY = true; // Lock Y rotation to prevent tilting
    public bool reverseDirection = false; // Reverse the facing direction if needed
    
    private Camera playerCamera;
    private Transform targetTransform;
    
    void Start()
    {
        // Priority 1: Use custom target if provided
        if (customTarget != null)
        {
            targetTransform = customTarget;
            return;
        }
        
        // Priority 2: Find the player camera - first try to find it by tag, then use main camera
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerCamera = player.GetComponentInChildren<Camera>();
        }
        
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        
        if (playerCamera != null)
        {
            targetTransform = playerCamera.transform;
        }
    }
    
    void LateUpdate()
    {
        if (targetTransform == null) return;
        
        Vector3 targetPosition = targetTransform.position;
        
        if (lockY)
        {
            // Keep the billboard at the same Y level, only rotate on Y axis
            targetPosition.y = transform.position.y;
        }
        
        // Calculate direction to camera
        Vector3 direction = targetPosition - transform.position;
        
        if (reverseDirection)
        {
            direction = -direction;
        }
        
        // Only rotate if there's a meaningful direction
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            if (lockY)
            {
                // Only use the Y rotation component
                targetRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
            }
            
            transform.rotation = targetRotation;
        }
    }
}