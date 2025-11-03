using UnityEngine;

public class CircularPath : MonoBehaviour
{
    public enum SpaceMode
    {
        WorldSpace,              // Circle center is fixed in world coordinates (ignores parent movement)
        LocalSpace,              // Circle is relative to parent transform (moves with parent)
        SelfSpace,               // Circle is relative to object's own initial world position (follows parent offset)
        LocalSpaceWorldAligned,  // Circle moves with parent but orientation stays world-aligned (no rotation inheritance)
        ParentTracking           // Orbit follows parent position/rotation, works with other scripts (BEST for Earth setup)
    }
    
    [Header("Circle Settings")]
    [SerializeField] private float radius = 10f;
    [SerializeField] private Vector3 centerOffset = Vector3.zero;
    [SerializeField] private Vector3 circleRotation = Vector3.zero;
    
    [Header("Movement")]
    [SerializeField] private float speed = 1f;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private bool startOnAwake = true;
    [SerializeField] private bool startsOnRandomPoint = false;
    
    [Header("Rotation Control")]
    [Tooltip("When enabled, object will rotate to face the direction of movement along the path")]
    [SerializeField] private bool controlRotation = false;
    [Tooltip("Additional rotation offset applied when controlling rotation (e.g., if forward isn't the front)")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;
    
    [Header("Space Settings")]
    [SerializeField] private SpaceMode spaceMode = SpaceMode.LocalSpace;
    [Tooltip("When enabled, rotation follows parent's rotation. When disabled, uses world-aligned rotation.")]
    [SerializeField] private bool inheritParentRotation = true;
    
    [Header("Vertical Oscillation")]
    [SerializeField] private bool enableVerticalOscillation = false;
    [SerializeField] private float oscillationHeight = 2f;
    [SerializeField] private float oscillationCycleSpeed = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool showCircle = true;
    [SerializeField] private Color circleColor = Color.yellow;
    [SerializeField] private int circleSegments = 64;
    
    // Private variables
    private bool isMoving = false;
    private float currentAngle = 0f;
    private float oscillationTime = 0f;
    private Vector3 storedCircleCenter;
    private Vector3 initialSelfPosition;
    private Vector3 initialLocalOffset;
    private Quaternion circleOrientation;
    private Transform parentTransform;
    
    private void Start()
    {
        // Store initial position for SelfSpace mode
        initialSelfPosition = transform.position;
        parentTransform = transform.parent;
        
        // Store initial local offset for ParentTracking mode
        initialLocalOffset = transform.localPosition;
        
        // Calculate circle center based on space mode
        switch (spaceMode)
        {
            case SpaceMode.WorldSpace:
                storedCircleCenter = transform.position + centerOffset;
                break;
            case SpaceMode.LocalSpace:
            case SpaceMode.LocalSpaceWorldAligned:
                storedCircleCenter = transform.localPosition + centerOffset;
                break;
            case SpaceMode.SelfSpace:
                storedCircleCenter = initialSelfPosition + centerOffset;
                break;
            case SpaceMode.ParentTracking:
                // Store the orbit center as a local offset
                storedCircleCenter = initialLocalOffset + centerOffset;
                break;
        }
        
        circleOrientation = Quaternion.Euler(circleRotation);
        
        if (startOnAwake)
        {
            StartMovement();
        }
    }
    
    private void Update()
    {
        if (!isMoving) return;
        
        // Update angle based on speed
        float angleSpeed = speed * (clockwise ? -1f : 1f);
        currentAngle += angleSpeed * Time.deltaTime;
        
        // Keep angle in 0-360 range
        if (currentAngle >= 360f) currentAngle -= 360f;
        if (currentAngle < 0f) currentAngle += 360f;
        
        // Update oscillation time
        if (enableVerticalOscillation)
        {
            oscillationTime += oscillationCycleSpeed * Time.deltaTime;
        }
        
        // Calculate position on circle
        float radians = currentAngle * Mathf.Deg2Rad;
        Vector3 circlePosition = new Vector3(
            Mathf.Cos(radians) * radius,
            0f,
            Mathf.Sin(radians) * radius
        );
        
        // Add vertical oscillation
        if (enableVerticalOscillation)
        {
            float verticalOffset = Mathf.Sin(oscillationTime) * oscillationHeight;
            circlePosition.y += verticalOffset;
        }
        
        // Get the appropriate rotation for the circle orientation
        Quaternion effectiveOrientation = GetEffectiveOrientation();
        
        // Apply circle orientation and center based on space mode
        switch (spaceMode)
        {
            case SpaceMode.WorldSpace:
                // Circle center is fixed in world space
                Vector3 worldPos = storedCircleCenter + (effectiveOrientation * circlePosition);
                transform.position = worldPos;
                break;
                
            case SpaceMode.LocalSpace:
                // Circle is relative to parent (inherits parent rotation)
                Vector3 localPos = storedCircleCenter + (circleOrientation * circlePosition);
                transform.localPosition = localPos;
                break;
                
            case SpaceMode.LocalSpaceWorldAligned:
                // Circle moves with parent but stays world-aligned (no rotation inheritance)
                Vector3 localPosWorldAligned = storedCircleCenter + (circleOrientation * circlePosition);
                transform.localPosition = localPosWorldAligned;
                break;
                
            case SpaceMode.SelfSpace:
                // Circle follows initial position but moves with parent
                Vector3 offset = transform.position - initialSelfPosition;
                Vector3 selfPos = storedCircleCenter + offset + (effectiveOrientation * circlePosition);
                transform.position = selfPos;
                break;
                
            case SpaceMode.ParentTracking:
                // Calculate orbit position in local space, then convert to world space
                // This makes it follow parent's position and rotation automatically
                Vector3 localOrbitPos = storedCircleCenter + (circleOrientation * circlePosition);
                
                if (parentTransform != null)
                {
                    // Convert local orbit position to world space using parent transform
                    Vector3 worldOrbitPos = parentTransform.TransformPoint(localOrbitPos);
                    transform.position = worldOrbitPos;
                }
                else
                {
                    // No parent, just use local position as world position
                    transform.position = localOrbitPos;
                }
                break;
        }
        
        // Control rotation to face movement direction
        if (controlRotation)
        {
            UpdateRotationToFaceMovement(radians);
        }
    }
    
    public void StartMovement()
    {
        isMoving = true;
        
        // Set initial angle (random or 0)
        currentAngle = startsOnRandomPoint ? Random.Range(0f, 360f) : 0f;
        
        // Set initial position on circle
        float radians = currentAngle * Mathf.Deg2Rad;
        Vector3 circlePosition = new Vector3(
            Mathf.Cos(radians) * radius,
            0f,
            Mathf.Sin(radians) * radius
        );
        
        // Get the appropriate rotation for the circle orientation
        Quaternion effectiveOrientation = GetEffectiveOrientation();
        
        // Set position based on space mode
        switch (spaceMode)
        {
            case SpaceMode.WorldSpace:
                Vector3 worldPos = storedCircleCenter + (effectiveOrientation * circlePosition);
                transform.position = worldPos;
                break;
                
            case SpaceMode.LocalSpace:
            case SpaceMode.LocalSpaceWorldAligned:
                Vector3 localPos = storedCircleCenter + (circleOrientation * circlePosition);
                transform.localPosition = localPos;
                break;
                
            case SpaceMode.SelfSpace:
                Vector3 offset = transform.position - initialSelfPosition;
                Vector3 selfPos = storedCircleCenter + offset + (effectiveOrientation * circlePosition);
                transform.position = selfPos;
                break;
                
            case SpaceMode.ParentTracking:
                Vector3 localOrbitPos = storedCircleCenter + (circleOrientation * circlePosition);
                
                if (parentTransform != null)
                {
                    Vector3 worldOrbitPos = parentTransform.TransformPoint(localOrbitPos);
                    transform.position = worldOrbitPos;
                }
                else
                {
                    transform.position = localOrbitPos;
                }
                break;
        }
        
        // Set initial rotation only if controlling rotation
        if (controlRotation)
        {
            UpdateRotationToFaceMovement(radians);
        }
        
        oscillationTime = 0f;
    }
    
    private void UpdateRotationToFaceMovement(float radians)
    {
        // Calculate tangent direction (perpendicular to radius)
        Vector3 tangentDirection = new Vector3(
            -Mathf.Sin(radians),
            0f,
            Mathf.Cos(radians)
        );
        
        if (!clockwise) tangentDirection = -tangentDirection;
        
        // Transform tangent direction based on space mode
        Vector3 worldDirection = Vector3.zero;
        
        switch (spaceMode)
        {
            case SpaceMode.WorldSpace:
            case SpaceMode.SelfSpace:
                // Use effective orientation (includes parent rotation if inheritParentRotation is true)
                worldDirection = GetEffectiveOrientation() * tangentDirection;
                break;
                
            case SpaceMode.LocalSpace:
                // Direction is in local space, so we need parent's rotation
                if (parentTransform != null)
                {
                    worldDirection = parentTransform.rotation * circleOrientation * tangentDirection;
                }
                else
                {
                    worldDirection = circleOrientation * tangentDirection;
                }
                break;
                
            case SpaceMode.LocalSpaceWorldAligned:
                // World-aligned, so only use circle orientation (no parent rotation)
                worldDirection = circleOrientation * tangentDirection;
                break;
                
            case SpaceMode.ParentTracking:
                // Follow parent's rotation plus circle orientation
                if (parentTransform != null)
                {
                    worldDirection = parentTransform.rotation * circleOrientation * tangentDirection;
                }
                else
                {
                    worldDirection = circleOrientation * tangentDirection;
                }
                break;
        }
        
        // Apply rotation to face the movement direction
        if (worldDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(worldDirection);
            
            // Apply rotation offset if specified
            if (rotationOffset != Vector3.zero)
            {
                targetRotation *= Quaternion.Euler(rotationOffset);
            }
            
            transform.rotation = targetRotation;
        }
    }
    
    private Quaternion GetEffectiveOrientation()
    {
        // Determine rotation based on space mode and parent rotation settings
        if (inheritParentRotation && transform.parent != null)
        {
            return transform.parent.rotation * circleOrientation;
        }
        return circleOrientation;
    }
    
    public void StopMovement()
    {
        isMoving = false;
    }
    
    public void SetRadius(float newRadius)
    {
        radius = Mathf.Max(0.1f, newRadius);
    }
    
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }
    
    public void SetCenterOffset(Vector3 offset)
    {
        centerOffset = offset;
        
        switch (spaceMode)
        {
            case SpaceMode.WorldSpace:
                storedCircleCenter = transform.position + centerOffset;
                break;
            case SpaceMode.LocalSpace:
            case SpaceMode.LocalSpaceWorldAligned:
                storedCircleCenter = transform.localPosition + centerOffset;
                break;
            case SpaceMode.SelfSpace:
                storedCircleCenter = initialSelfPosition + centerOffset;
                break;
            case SpaceMode.ParentTracking:
                storedCircleCenter = initialLocalOffset + centerOffset;
                break;
        }
    }
    
    public void SetCircleRotation(Vector3 rotation)
    {
        circleRotation = rotation;
        circleOrientation = Quaternion.Euler(circleRotation);
    }
    
    public void SetClockwise(bool isClockwise)
    {
        clockwise = isClockwise;
    }
    
    public void SetVerticalOscillation(bool enable)
    {
        enableVerticalOscillation = enable;
    }
    
    public void SetOscillationHeight(float height)
    {
        oscillationHeight = Mathf.Max(0f, height);
    }
    
    public void SetOscillationCycleSpeed(float cycleSpeed)
    {
        oscillationCycleSpeed = cycleSpeed;
    }
    
    public void SetRotationOffset(Vector3 offset)
    {
        rotationOffset = offset;
    }
    
    public void SetControlRotation(bool control)
    {
        controlRotation = control;
    }
    
    public float GetRadius() => radius;
    public float GetSpeed() => speed;
    public Vector3 GetCenterOffset() => centerOffset;
    public Vector3 GetCircleRotation() => circleRotation;
    public bool IsClockwise() => clockwise;
    public bool IsMoving() => isMoving;
    public bool IsVerticalOscillationEnabled() => enableVerticalOscillation;
    public float GetOscillationHeight() => oscillationHeight;
    public float GetOscillationCycleSpeed() => oscillationCycleSpeed;
    public bool GetControlRotation() => controlRotation;
    public Vector3 GetRotationOffset() => rotationOffset;
    
    private void OnDrawGizmos()
    {
        if (!showCircle) return;
        
        // Calculate circle center and orientation based on space mode
        Vector3 center;
        Quaternion orientation;
        
        if (Application.isPlaying)
        {
            // Runtime: use stored values and current mode
            switch (spaceMode)
            {
                case SpaceMode.WorldSpace:
                    center = storedCircleCenter;
                    orientation = GetEffectiveOrientation();
                    break;
                    
                case SpaceMode.LocalSpace:
                    center = transform.parent != null ? transform.parent.TransformPoint(storedCircleCenter) : storedCircleCenter;
                    orientation = circleOrientation;
                    break;
                    
                case SpaceMode.LocalSpaceWorldAligned:
                    center = transform.parent != null ? transform.parent.TransformPoint(storedCircleCenter) : storedCircleCenter;
                    orientation = circleOrientation;  // World-aligned, no parent rotation
                    break;
                    
                case SpaceMode.ParentTracking:
                    center = parentTransform != null ? parentTransform.TransformPoint(storedCircleCenter) : storedCircleCenter;
                    orientation = parentTransform != null ? parentTransform.rotation * circleOrientation : circleOrientation;
                    break;
                    
                case SpaceMode.SelfSpace:
                    Vector3 offset = transform.position - initialSelfPosition;
                    center = storedCircleCenter + offset;
                    orientation = GetEffectiveOrientation();
                    break;
                    
                default:
                    center = transform.position;
                    orientation = Quaternion.identity;
                    break;
            }
        }
        else
        {
            // Editor: preview based on current settings
            switch (spaceMode)
            {
                case SpaceMode.WorldSpace:
                    center = transform.position + centerOffset;
                    orientation = Quaternion.Euler(circleRotation);
                    break;
                    
                case SpaceMode.LocalSpace:
                    Vector3 localCenter = transform.localPosition + centerOffset;
                    center = transform.parent != null ? transform.parent.TransformPoint(localCenter) : localCenter;
                    orientation = Quaternion.Euler(circleRotation);
                    break;
                    
                case SpaceMode.LocalSpaceWorldAligned:
                    Vector3 localCenterWorldAligned = transform.localPosition + centerOffset;
                    center = transform.parent != null ? transform.parent.TransformPoint(localCenterWorldAligned) : localCenterWorldAligned;
                    orientation = Quaternion.Euler(circleRotation);  // World-aligned
                    break;
                    
                case SpaceMode.ParentTracking:
                    Vector3 localCenterTracking = transform.localPosition + centerOffset;
                    center = transform.parent != null ? transform.parent.TransformPoint(localCenterTracking) : localCenterTracking;
                    orientation = transform.parent != null ? 
                        transform.parent.rotation * Quaternion.Euler(circleRotation) : 
                        Quaternion.Euler(circleRotation);
                    break;
                    
                case SpaceMode.SelfSpace:
                    center = transform.position + centerOffset;
                    orientation = inheritParentRotation && transform.parent != null ? 
                        transform.parent.rotation * Quaternion.Euler(circleRotation) : 
                        Quaternion.Euler(circleRotation);
                    break;
                    
                default:
                    center = transform.position + centerOffset;
                    orientation = Quaternion.Euler(circleRotation);
                    break;
            }
        }
        
        // Draw circle
        Gizmos.color = circleColor;
        Vector3 prevPoint = Vector3.zero;
        
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (i / (float)circleSegments) * 360f * Mathf.Deg2Rad;
            Vector3 localPoint = new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );
            
            Vector3 worldPoint = center + (orientation * localPoint);
            
            if (i > 0)
            {
                Gizmos.DrawLine(prevPoint, worldPoint);
            }
            
            prevPoint = worldPoint;
        }
        
        // Draw vertical oscillation range if enabled
        if (enableVerticalOscillation)
        {
            Gizmos.color = Color.cyan;
            
            // Draw upper and lower circles to show oscillation range
            for (int i = 0; i <= circleSegments; i++)
            {
                float angle = (i / (float)circleSegments) * 360f * Mathf.Deg2Rad;
                Vector3 localPoint = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );
                
                // Upper oscillation circle
                Vector3 upperLocalPoint = localPoint + Vector3.up * oscillationHeight;
                Vector3 upperWorldPoint = center + (orientation * upperLocalPoint);
                
                // Lower oscillation circle
                Vector3 lowerLocalPoint = localPoint + Vector3.down * oscillationHeight;
                Vector3 lowerWorldPoint = center + (orientation * lowerLocalPoint);
                
                if (i > 0)
                {
                    // Draw upper circle
                    float prevAngle = ((i - 1) / (float)circleSegments) * 360f * Mathf.Deg2Rad;
                    Vector3 prevUpperLocal = new Vector3(
                        Mathf.Cos(prevAngle) * radius,
                        oscillationHeight,
                        Mathf.Sin(prevAngle) * radius
                    );
                    Vector3 prevUpperWorld = center + (orientation * prevUpperLocal);
                    Gizmos.DrawLine(prevUpperWorld, upperWorldPoint);
                    
                    // Draw lower circle
                    Vector3 prevLowerLocal = new Vector3(
                        Mathf.Cos(prevAngle) * radius,
                        -oscillationHeight,
                        Mathf.Sin(prevAngle) * radius
                    );
                    Vector3 prevLowerWorld = center + (orientation * prevLowerLocal);
                    Gizmos.DrawLine(prevLowerWorld, lowerWorldPoint);
                }
                
                // Draw vertical lines connecting upper and lower circles every few segments
                if (i % 8 == 0)
                {
                    Gizmos.color = Color.cyan * 0.5f;
                    Gizmos.DrawLine(upperWorldPoint, lowerWorldPoint);
                    Gizmos.color = Color.cyan;
                }
            }
        }
        
        // Draw center point
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, 0.5f);
        
        // Draw current position if moving
        if (isMoving && Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 1f);
            
            // Draw direction arrow
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 3f);
        }
    }
}