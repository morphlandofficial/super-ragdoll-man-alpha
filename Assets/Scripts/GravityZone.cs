using UnityEngine;

/// <summary>
/// Creates a zone that changes gravity when objects enter/exit.
/// Useful for creating underwater zones, low-gravity areas, etc.
/// Requires a Collider set to "Is Trigger"
/// </summary>
[RequireComponent(typeof(Collider))]
public class GravityZone : MonoBehaviour
{
    [Header("Zone Gravity Settings")]
    [Tooltip("The gravity to apply when entering this zone")]
    [SerializeField] private Vector3 zoneGravity = new Vector3(0, -2.0f, 0);
    
    [Tooltip("Restore previous gravity when exiting the zone")]
    [SerializeField] private bool restoreOnExit = true;
    
    [Tooltip("Only affect objects with specific tags (leave empty to affect all)")]
    [SerializeField] private string[] affectedTags = new string[0];
    
    [Header("Transition")]
    [Tooltip("Smooth transition when entering/exiting")]
    [SerializeField] private bool smoothTransition = true;
    
    [Tooltip("Duration of gravity transition")]
    [SerializeField] private float transitionDuration = 1f;
    
    [Header("Visual Feedback")]
    [Tooltip("Show the zone boundaries in the Scene view")]
    [SerializeField] private bool showGizmo = true;
    
    [Tooltip("Color of the zone gizmo")]
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.6f, 1f, 0.3f);
    
    private Vector3 previousGravity;
    private GravityManager gravityManager;
    
    private void Awake()
    {
        // Ensure the collider is set to trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"GravityZone on {gameObject.name} requires a trigger collider. Setting isTrigger to true.");
            col.isTrigger = true;
        }
        
        // Try to find a GravityManager in the scene
        gravityManager = FindFirstObjectByType<GravityManager>();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Check if this object should be affected
        if (!ShouldAffectObject(other.gameObject))
            return;
        
        // Store the current gravity before changing it
        previousGravity = Physics.gravity;
        
        // Apply the zone gravity
        if (gravityManager != null)
        {
            // Use GravityManager if available for smooth transitions
            bool originalSmoothSetting = gravityManager.enabled;
            gravityManager.SetGravity(zoneGravity);
        }
        else
        {
            // Direct physics gravity change
            if (smoothTransition)
            {
                StartCoroutine(SmoothGravityTransition(previousGravity, zoneGravity, transitionDuration));
            }
            else
            {
                Physics.gravity = zoneGravity;
            }
        }
        
        Debug.Log($"{other.gameObject.name} entered gravity zone. Gravity: {zoneGravity}");
    }
    
    private void OnTriggerExit(Collider other)
    {
        // Check if this object should be affected
        if (!ShouldAffectObject(other.gameObject))
            return;
        
        if (!restoreOnExit)
            return;
        
        // Restore previous gravity
        if (gravityManager != null)
        {
            gravityManager.SetGravity(previousGravity);
        }
        else
        {
            if (smoothTransition)
            {
                StartCoroutine(SmoothGravityTransition(Physics.gravity, previousGravity, transitionDuration));
            }
            else
            {
                Physics.gravity = previousGravity;
            }
        }
        
        Debug.Log($"{other.gameObject.name} exited gravity zone. Gravity restored: {previousGravity}");
    }
    
    /// <summary>
    /// Check if an object should be affected by this zone based on tags
    /// </summary>
    private bool ShouldAffectObject(GameObject obj)
    {
        // If no tags specified, affect everything
        if (affectedTags.Length == 0)
            return true;
        
        // Check if object has any of the specified tags
        foreach (string tag in affectedTags)
        {
            if (obj.CompareTag(tag))
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Smoothly transition between two gravity values
    /// </summary>
    private System.Collections.IEnumerator SmoothGravityTransition(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0, 1, t);
            
            Physics.gravity = Vector3.Lerp(from, to, smoothT);
            
            yield return null;
        }
        
        Physics.gravity = to;
    }
    
    /// <summary>
    /// Manually set the gravity for this zone
    /// </summary>
    public void SetZoneGravity(Vector3 gravity)
    {
        zoneGravity = gravity;
    }
    
    /// <summary>
    /// Get the current zone gravity
    /// </summary>
    public Vector3 ZoneGravity => zoneGravity;
    
    private void OnDrawGizmos()
    {
        if (!showGizmo)
            return;
        
        Collider col = GetComponent<Collider>();
        if (col == null)
            return;
        
        // Set gizmo color
        Gizmos.color = gizmoColor;
        
        // Draw the zone based on collider type
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        if (col is BoxCollider boxCol)
        {
            Gizmos.DrawCube(boxCol.center, boxCol.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(boxCol.center, boxCol.size);
        }
        else if (col is SphereCollider sphereCol)
        {
            Gizmos.DrawSphere(sphereCol.center, sphereCol.radius);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireSphere(sphereCol.center, sphereCol.radius);
        }
        else if (col is CapsuleCollider capsuleCol)
        {
            // Simplified capsule drawing as wire sphere
            Gizmos.DrawWireSphere(capsuleCol.center, capsuleCol.radius);
        }
        
        Gizmos.matrix = oldMatrix;
        
        // Draw arrow indicating gravity direction in this zone
        Vector3 worldCenter = transform.TransformPoint(col.bounds.center);
        Vector3 gravityDirection = zoneGravity.normalized;
        float arrowLength = 2f;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(worldCenter, worldCenter + gravityDirection * arrowLength);
        
        // Draw arrow head
        Vector3 arrowTip = worldCenter + gravityDirection * arrowLength;
        Vector3 right = Vector3.Cross(gravityDirection, Vector3.forward).normalized * 0.3f;
        if (right.magnitude < 0.1f)
            right = Vector3.Cross(gravityDirection, Vector3.up).normalized * 0.3f;
        
        Gizmos.DrawLine(arrowTip, arrowTip - gravityDirection * 0.5f + right);
        Gizmos.DrawLine(arrowTip, arrowTip - gravityDirection * 0.5f - right);
    }
}




