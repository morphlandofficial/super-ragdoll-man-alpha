using UnityEngine;

public class ContinuousRotation : MonoBehaviour
{
    [Header("Rotation Axes")]
    [Tooltip("Rotate around X axis")]
    public bool rotateX = false;
    
    [Tooltip("Rotate around Y axis")]
    public bool rotateY = true;
    
    [Tooltip("Rotate around Z axis")]
    public bool rotateZ = false;
    
    [Header("Spin Settings")]
    [Tooltip("Reverse the rotation direction")]
    public bool reverseDirection = false;
    
    [Tooltip("Rotation speed in degrees per second")]
    [Range(0f, 360f)]
    public float rotationSpeed = 90f;
    
    [Tooltip("Randomize starting rotation angle")]
    public bool randomizeStartRotation = false;
    
    [Tooltip("Use local space (relative to parent) or world space")]
    public Space rotationSpace = Space.Self;
    
    [Header("Oscillation Settings")]
    [Tooltip("Enable back and forth rotation instead of continuous spin")]
    public bool rotateBackAndForth = false;
    
    [Tooltip("Maximum angle range for oscillation (degrees from center)")]
    [Range(0f, 180f)]
    public float oscillationRange = 45f;
    
    [Tooltip("Speed of oscillation (how fast it swings back and forth)")]
    [Range(0.1f, 10f)]
    public float oscillationSpeed = 1f;
    
    [Header("Anchor Point Settings")]
    [Tooltip("Use a manual anchor point for rotation instead of object's pivot")]
    public bool useManualAnchorPoint = false;
    
    [Tooltip("The world position to rotate around (editable with gizmo in scene view)")]
    public Vector3 manualAnchorPoint = Vector3.zero;
    
    private Quaternion initialRotation;
    private bool initialized = false;
    private float timeOffset = 0f; // For oscillation randomization
    
    private void Start()
    {
        // Store the initial rotation when the object starts
        initialRotation = transform.rotation;
        initialized = true;
        
        // Apply random start rotation if enabled
        if (randomizeStartRotation)
        {
            if (rotateBackAndForth)
            {
                // For oscillation, randomize the time offset (phase of the sine wave)
                timeOffset = Random.Range(0f, Mathf.PI * 2f);
            }
            else
            {
                // For continuous rotation, apply a random initial rotation
                float randomAngle = Random.Range(0f, 360f);
                
                Vector3 randomRotation = new Vector3(
                    rotateX ? randomAngle : 0f,
                    rotateY ? randomAngle : 0f,
                    rotateZ ? randomAngle : 0f
                );
                
                transform.Rotate(randomRotation, rotationSpace);
            }
        }
    }
    
    private void Update()
    {
        if (rotateBackAndForth)
        {
            RotateBackAndForth();
        }
        else
        {
            RotateContinuously();
        }
    }
    
    private void RotateContinuously()
    {
        // Calculate rotation for this frame
        float rotationAmount = rotationSpeed * Time.deltaTime;
        
        // Apply reverse direction if enabled
        if (reverseDirection)
        {
            rotationAmount = -rotationAmount;
        }
        
        // Build rotation vector based on enabled axes
        Vector3 rotation = new Vector3(
            rotateX ? rotationAmount : 0f,
            rotateY ? rotationAmount : 0f,
            rotateZ ? rotationAmount : 0f
        );
        
        // Apply rotation
        if (useManualAnchorPoint)
        {
            // Rotate around the manual anchor point
            if (rotateX) transform.RotateAround(manualAnchorPoint, Vector3.right, rotation.x);
            if (rotateY) transform.RotateAround(manualAnchorPoint, Vector3.up, rotation.y);
            if (rotateZ) transform.RotateAround(manualAnchorPoint, Vector3.forward, rotation.z);
        }
        else
        {
            transform.Rotate(rotation, rotationSpace);
        }
    }
    
    private void RotateBackAndForth()
    {
        if (!initialized)
        {
            initialRotation = transform.rotation;
            initialized = true;
        }
        
        // Calculate oscillating angle using sine wave for smooth motion
        // Apply time offset for randomization
        float time = (Time.time * oscillationSpeed) + timeOffset;
        float angle = Mathf.Sin(time) * oscillationRange;
        
        // Apply reverse direction if enabled
        if (reverseDirection)
        {
            angle = -angle;
        }
        
        // Build rotation based on enabled axes
        Vector3 eulerRotation = new Vector3(
            rotateX ? angle : 0f,
            rotateY ? angle : 0f,
            rotateZ ? angle : 0f
        );
        
        // Apply rotation relative to initial rotation
        if (rotationSpace == Space.Self)
        {
            transform.localRotation = initialRotation * Quaternion.Euler(eulerRotation);
        }
        else
        {
            transform.rotation = initialRotation * Quaternion.Euler(eulerRotation);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw gizmo for manual anchor point when enabled
        if (useManualAnchorPoint)
        {
            // Draw a sphere at the anchor point
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(manualAnchorPoint, 0.1f);
            
            // Draw a line from object to anchor point
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, manualAnchorPoint);
            
            // Draw axes at the anchor point
            Gizmos.color = Color.red;
            Gizmos.DrawLine(manualAnchorPoint, manualAnchorPoint + Vector3.right * 0.5f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(manualAnchorPoint, manualAnchorPoint + Vector3.up * 0.5f);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(manualAnchorPoint, manualAnchorPoint + Vector3.forward * 0.5f);
        }
    }
}
