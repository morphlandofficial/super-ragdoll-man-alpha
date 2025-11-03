using UnityEngine;

/// <summary>
/// Simple component that constrains movement to X-axis only by zeroing out Z-axis velocity.
/// Just add this to any character to restrict their movement to left/right only.
/// </summary>
public class XAxisMovementConstraint : MonoBehaviour
{
    [Header("Constraint Settings")]
    [SerializeField] private bool constrainToXAxisOnly = true;
    [SerializeField] private bool preserveYMovement = true; // Allow jumping/falling
    
    private Rigidbody _rigidbody;
    private Vector3 _initialZPosition;
    private bool _hasInitialPosition = false;
    
    void Start()
    {
        // Try to get rigidbody from this object or its children
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            // Look for rigidbody in children (for ragdoll systems)
            _rigidbody = GetComponentInChildren<Rigidbody>();
        }
        
        if (_rigidbody == null)
        {
// Debug.LogWarning("XAxisMovementConstraint: No Rigidbody found. Will use Transform constraint instead.");
        }
        
        // Store initial Z position
        _initialZPosition = transform.position;
        _hasInitialPosition = true;
    }
    
    void FixedUpdate()
    {
        if (!constrainToXAxisOnly) return;
        
        if (_rigidbody != null)
        {
            // Method 1: Zero out Z velocity on the main rigidbody
            Vector3 velocity = _rigidbody.linearVelocity;
            velocity.z = 0f;
            if (!preserveYMovement)
                velocity.y = 0f;
            _rigidbody.linearVelocity = velocity;
        }
        else if (_hasInitialPosition)
        {
            // Method 2: Constrain transform position if no rigidbody
            Vector3 pos = transform.position;
            pos.z = _initialZPosition.z;
            if (!preserveYMovement)
                pos.y = _initialZPosition.y;
            transform.position = pos;
        }
    }
    
    void LateUpdate()
    {
        if (!constrainToXAxisOnly) return;
        
        // Additional constraint for ragdoll systems - constrain all child rigidbodies
        Rigidbody[] allRigidbodies = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in allRigidbodies)
        {
            if (rb != null)
            {
                Vector3 velocity = rb.linearVelocity;
                velocity.z = 0f;
                rb.linearVelocity = velocity;
            }
        }
    }
    
    /// <summary>
    /// Enable or disable the X-axis constraint at runtime
    /// </summary>
    public void SetConstraintEnabled(bool enabled)
    {
        constrainToXAxisOnly = enabled;
    }
    
    /// <summary>
    /// Reset the initial Z position (useful if you want to change the constraint line)
    /// </summary>
    public void ResetInitialPosition()
    {
        _initialZPosition = transform.position;
        _hasInitialPosition = true;
    }
}