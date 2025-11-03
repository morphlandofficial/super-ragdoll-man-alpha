using UnityEngine;

[System.Serializable]
public class SphereBillboard : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Optional: Drag a GameObject or prefab here to look at. If empty, will auto-find camera.")]
    public Transform customTarget;
    
    [Header("Sphere Billboard Settings")]
    [Tooltip("The sprite/face that should always face the player (usually a child object)")]
    public Transform faceSprite;
    
    [Tooltip("Distance from sphere center to place the face sprite")]
    [Range(0.1f, 2.0f)]
    public float faceDistance = 0.5f;
    
    [Tooltip("How smoothly the eyeball rotates (higher = smoother)")]
    [Range(1f, 20f)]
    public float rotationSpeed = 8f;
    
    [Tooltip("Lock Y rotation to prevent tilting")]
    public bool lockY = true;
    
    [Tooltip("Show debug gizmos in scene view")]
    public bool showDebugGizmos = true;
    
    private Camera playerCamera;
    private Transform targetTransform;
    private Vector3 sphereCenter;
    
    void Start()
    {
        sphereCenter = transform.position;
        
        // Auto-find face sprite if not assigned
        if (faceSprite == null)
        {
            // Look for a child with "face" or "sprite" in the name
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child != transform && (child.name.ToLower().Contains("face") || child.name.ToLower().Contains("sprite")))
                {
                    faceSprite = child;
                    break;
                }
            }
        }
        
        if (faceSprite == null)
        {
            return;
        }
        
        // Priority 1: Use custom target if provided
        if (customTarget != null)
        {
            targetTransform = customTarget;
            return;
        }
        
        // Priority 2: Find the player camera
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
        if (targetTransform == null || faceSprite == null) return;
        
        // Update sphere center in case the parent moved
        sphereCenter = transform.position;
        
        // Calculate direction from sphere center to target
        Vector3 targetPosition = targetTransform.position;
        
        if (lockY)
        {
            // Keep the target at the same Y level as the sphere center
            targetPosition.y = sphereCenter.y;
        }
        
        Vector3 directionToTarget = (targetPosition - sphereCenter).normalized;
        
        // Only rotate if there's a meaningful direction
        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            // Calculate the position on the sphere surface facing the target
            Vector3 facePosition = sphereCenter + directionToTarget * faceDistance;
            
            // Position the face sprite
            faceSprite.position = Vector3.Lerp(faceSprite.position, facePosition, Time.deltaTime * rotationSpeed);
            
            // Rotate the face sprite to face the target
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            
            if (lockY)
            {
                // Only use the Y rotation component
                targetRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
            }
            
            faceSprite.rotation = Quaternion.Lerp(faceSprite.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Draw sphere outline
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, faceDistance);
        
        // Draw center point
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.05f);
        
        // Draw direction line from center to face sprite (pupil)
        if (faceSprite != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, faceSprite.position);
            
            // Draw pupil point
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(faceSprite.position, 0.03f);
            
            // Draw forward direction from pupil
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(faceSprite.position, faceSprite.forward * 0.3f);
        }
        
        // Draw target direction if available
        if (targetTransform != null)
        {
            Vector3 targetPos = targetTransform.position;
            if (lockY) targetPos.y = transform.position.y;
            
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, targetPos);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw additional debug info when selected
        if (!showDebugGizmos) return;
        
        // Draw sphere with face distance
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, faceDistance);
    }
}