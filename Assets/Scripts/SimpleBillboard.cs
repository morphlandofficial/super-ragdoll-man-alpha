using UnityEngine;

/// <summary>
/// Simple billboard that rotates the GameObject to look at a target (usually the player)
/// Shows a clear gizmo indicating the looking direction
/// </summary>
public class SimpleBillboard : MonoBehaviour
{
    /// <summary>
    /// Rotation constraint modes for billboard
    /// </summary>
    public enum RotationMode
    {
        FreeRotate,     // Rotate on all axes (full 3D tracking)
        LockX,          // Lock X axis (prevent up/down tilt on X)
        LockY,          // Lock Y axis (prevent left/right rotation on Y) - Most common for vertical billboards
        LockZ,          // Lock Z axis (prevent roll on Z)
        LockXY,         // Lock both X and Y (only Z rotation)
        LockXZ,         // Lock both X and Z (only Y rotation) - Traditional billboard
        LockYZ          // Lock both Y and Z (only X rotation)
    }
    
    [Header("Target Settings")]
    [Tooltip("Auto-finds any object with DefaultBehaviour component. Leave unchecked to use Camera instead.")]
    public bool trackPlayer = true;
    
    [Header("Billboard Settings")]
    [Tooltip("Rotation constraint mode - controls which axes can rotate")]
    public RotationMode rotationMode = RotationMode.LockXZ;
    
    [Tooltip("How smoothly the billboard rotates (higher = smoother)")]
    [Range(1f, 20f)]
    public float rotationSpeed = 8f;
    
    [Header("Pivot Point (Optional)")]
    [Tooltip("If set, this object will rotate AROUND this pivot point instead of rotating in place")]
    public Transform customPivot;
    
    [Tooltip("If no Transform is set, use this local offset as the pivot point")]
    public Vector3 pivotOffset = Vector3.zero;
    
    [Tooltip("Use pivot offset relative to parent (if false, uses world space)")]
    public bool pivotRelativeToParent = true;
    
    [Header("Gizmo Visualization")]
    [Tooltip("Show debug gizmos in scene view")]
    public bool showDebugGizmos = true;
    
    [Tooltip("Show ray to target (what billboard is looking at)")]
    public bool showLookingRay = true;
    
    [Tooltip("Length of the looking direction ray")]
    [Range(0.5f, 10f)]
    public float rayLength = 2f;
    
    [Tooltip("Color of the looking ray")]
    public Color rayColor = Color.green;
    
    [Tooltip("Show target connection line")]
    public bool showTargetLine = true;
    
    [Header("Debug")]
    [Tooltip("Show continuous debug info in console")]
    public bool debugMode = false;
    
    private Transform targetTransform;
    private Vector3 lastPosition;
    private Vector3 lastTargetPosition;
    private float lastDebugTime = 0f;
    
    void Start()
    {
        lastPosition = transform.position;
        FindTarget();
    }
    
    void LateUpdate()
    {
        // If no target, try to find one
        if (targetTransform == null)
        {
            FindTarget();
            if (targetTransform == null)
            {
                return; // Still no target
            }
        }
        
        // DEBUG: Track target position changes (only if debug mode enabled)
        if (debugMode && Time.time - lastDebugTime > 2f)
        {
            Vector3 currentTargetPos = targetTransform.position;
            float distanceMoved = Vector3.Distance(currentTargetPos, lastTargetPosition);
            
            lastTargetPosition = currentTargetPos;
            lastDebugTime = Time.time;
        }
        
        // Determine if we're using a custom pivot point
        bool usePivot = (customPivot != null) || (pivotOffset != Vector3.zero);
        
        if (usePivot)
        {
            RotateAroundPivot();
        }
        else
        {
            RotateInPlace();
        }
    }
    
    /// <summary>
    /// Simple target finding - just looks for DefaultBehaviour or Camera
    /// </summary>
    void FindTarget()
    {
        if (trackPlayer)
        {
            // Find any active DefaultBehaviour in the scene
            DefaultBehaviour playerBehaviour = FindFirstObjectByType<DefaultBehaviour>();
            if (playerBehaviour != null && playerBehaviour.gameObject.activeInHierarchy)
            {
                // Try to find Torso child (center of character) - search recursively
                // Try multiple common torso names
                Transform torso = FindChildRecursive(playerBehaviour.transform, "Torso");
                if (torso == null)
                    torso = FindChildRecursive(playerBehaviour.transform, "PhysicalTorso");
                if (torso == null)
                    torso = FindChildWithPartialName(playerBehaviour.transform, "Torso");
                
                if (torso != null)
                {
                    targetTransform = torso;
                    lastTargetPosition = targetTransform.position;
                }
                else
                {
                    // Fallback to root if no torso found
                    targetTransform = playerBehaviour.transform;
                    lastTargetPosition = targetTransform.position;
                    if (debugMode)
                    {
                        // Debug.LogWarning($"[Billboard {gameObject.name}] No torso found, using root");
                    }
                }
                return;
            }
        }
        
        // Fallback to Camera
        if (Camera.main != null)
        {
            targetTransform = Camera.main.transform;
            lastTargetPosition = targetTransform.position;
        }
    }
    
    Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;
            
            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }
    
    Transform FindChildWithPartialName(Transform parent, string partialName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Contains(partialName))
                return child;
            
            Transform found = FindChildWithPartialName(child, partialName);
            if (found != null)
                return found;
        }
        return null;
    }
    
    Vector3 GetPivotPoint()
    {
        if (customPivot != null)
        {
            // Use the custom pivot transform's position
            return customPivot.position;
        }
        else if (pivotOffset != Vector3.zero)
        {
            // Use the pivot offset
            if (pivotRelativeToParent && transform.parent != null)
            {
                // Transform the offset by parent's rotation and scale, then add to parent's position
                return transform.parent.TransformPoint(pivotOffset);
            }
            else
            {
                // Offset is in world space
                return pivotOffset;
            }
        }
        
        // Default: return object's own position
        return transform.position;
    }
    
    void RotateInPlace()
    {
        if (targetTransform == null)
            return;
        
        // Calculate direction to target from this object's position
        Vector3 myPosition = transform.position;
        Vector3 targetPosition = targetTransform.position;
        
        lastPosition = myPosition;
        
        // Apply rotation mode constraints to target position
        targetPosition = ApplyRotationModeConstraints(myPosition, targetPosition);
        
        Vector3 directionToTarget = targetPosition - myPosition;
        
        // Only rotate if there's a meaningful direction
        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            
            // Apply rotation mode constraints to the rotation
            targetRotation = ApplyRotationConstraints(targetRotation);
            
            // Store position before rotation to ensure it doesn't change
            Vector3 positionBeforeRotation = transform.position;
            
            // Apply rotation directly to transform
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            
            // Ensure position hasn't changed due to rotation
            if (Vector3.Distance(transform.position, positionBeforeRotation) > 0.001f)
            {
                transform.position = positionBeforeRotation;
            }
        }
    }
    
    void RotateAroundPivot()
    {
        // Calculate the pivot point in world space
        Vector3 pivotPoint = GetPivotPoint();
        
        // Calculate direction from pivot to target
        Vector3 targetPosition = targetTransform.position;
        
        // Apply rotation mode constraints to target position
        targetPosition = ApplyRotationModeConstraints(pivotPoint, targetPosition);
        
        Vector3 directionToTarget = targetPosition - pivotPoint;
        
        // Only rotate if there's a meaningful direction
        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            // Calculate the desired rotation
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            
            // Apply rotation mode constraints to the rotation
            targetRotation = ApplyRotationConstraints(targetRotation);
            
            // Store the current offset from pivot (in world space)
            Vector3 currentOffset = transform.position - pivotPoint;
            float distanceFromPivot = currentOffset.magnitude;
            
            // Smoothly interpolate rotation
            Quaternion smoothRotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            
            // Apply rotation
            transform.rotation = smoothRotation;
            
            // Maintain the same distance from pivot point
            // Position = pivot + (forward direction * distance)
            Vector3 newPosition = pivotPoint + smoothRotation * Vector3.forward * distanceFromPivot;
            transform.position = newPosition;
        }
    }
    
    /// <summary>
    /// Apply rotation mode constraints to target position
    /// </summary>
    Vector3 ApplyRotationModeConstraints(Vector3 fromPosition, Vector3 targetPosition)
    {
        Vector3 constrainedTarget = targetPosition;
        
        switch (rotationMode)
        {
            case RotationMode.FreeRotate:
                // No constraints - use target as-is
                break;
                
            case RotationMode.LockX:
            case RotationMode.LockXY:
            case RotationMode.LockXZ:
                // Lock X = keep target at same Y level
                constrainedTarget.y = fromPosition.y;
                break;
                
            case RotationMode.LockY:
            case RotationMode.LockYZ:
                // Lock Y = traditional billboard (no X tilt, no Z roll)
                constrainedTarget.y = fromPosition.y;
                break;
                
            case RotationMode.LockZ:
                // Lock Z = prevent roll
                constrainedTarget.y = fromPosition.y;
                break;
        }
        
        return constrainedTarget;
    }
    
    /// <summary>
    /// Apply rotation mode constraints to the final rotation
    /// </summary>
    Quaternion ApplyRotationConstraints(Quaternion targetRotation)
    {
        Vector3 euler = targetRotation.eulerAngles;
        Vector3 currentEuler = transform.rotation.eulerAngles;
        
        switch (rotationMode)
        {
            case RotationMode.FreeRotate:
                // No constraints
                return targetRotation;
                
            case RotationMode.LockX:
                // Only Y and Z can rotate
                euler.x = currentEuler.x;
                break;
                
            case RotationMode.LockY:
                // Only X and Z can rotate
                euler.y = currentEuler.y;
                break;
                
            case RotationMode.LockZ:
                // Only X and Y can rotate
                euler.z = currentEuler.z;
                break;
                
            case RotationMode.LockXY:
                // Only Z can rotate
                euler.x = currentEuler.x;
                euler.y = currentEuler.y;
                break;
                
            case RotationMode.LockXZ:
                // Only Y can rotate (traditional billboard)
                euler.x = 0;
                euler.z = 0;
                break;
                
            case RotationMode.LockYZ:
                // Only X can rotate
                euler.y = currentEuler.y;
                euler.z = currentEuler.z;
                break;
        }
        
        return Quaternion.Euler(euler);
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Check if we're using pivot mode
        bool usePivot = (customPivot != null) || (pivotOffset != Vector3.zero);
        Vector3 pivotPoint = GetPivotPoint();
        
        if (usePivot)
        {
            // Draw the pivot point (large and red)
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pivotPoint, 0.12f);
            
            // Draw line from pivot to this object
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pivotPoint, transform.position);
            
            // Draw orbit circle around pivot
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // Orange
            float distance = Vector3.Distance(pivotPoint, transform.position);
            DrawCircle(pivotPoint, distance, 32);
            
            // Draw the pivot offset axes if using offset (not custom pivot)
            if (customPivot == null && pivotOffset != Vector3.zero && transform.parent != null)
            {
                // Draw coordinate axes at pivot point
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pivotPoint, pivotPoint + transform.parent.right * 0.3f);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pivotPoint, pivotPoint + transform.parent.up * 0.3f);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(pivotPoint, pivotPoint + transform.parent.forward * 0.3f);
            }
        }
        else
        {
            // Standard mode - draw pivot point at object position
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, 0.08f);
        }
        
        // Draw object position (smaller sphere)
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.06f);
        
        // Draw a wireframe cube to show the object's bounds/orientation
        Gizmos.color = Color.white;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.5f);
        Gizmos.matrix = Matrix4x4.identity;
        
        // ===== LOOKING RAY GIZMO =====
        if (showLookingRay)
        {
            // Draw looking direction (forward vector) - BOLD RAY
            Gizmos.color = rayColor;
            Vector3 rayStart = transform.position;
            Vector3 rayEnd = transform.position + transform.forward * rayLength;
            
            // Draw main ray (thicker effect by drawing multiple lines)
            Gizmos.DrawRay(rayStart, transform.forward * rayLength);
            Gizmos.DrawRay(rayStart + transform.right * 0.01f, transform.forward * rayLength);
            Gizmos.DrawRay(rayStart - transform.right * 0.01f, transform.forward * rayLength);
            Gizmos.DrawRay(rayStart + transform.up * 0.01f, transform.forward * rayLength);
            Gizmos.DrawRay(rayStart - transform.up * 0.01f, transform.forward * rayLength);
            
            // Draw ENHANCED arrow head for better visibility
            Vector3 arrowHead = rayEnd;
            float arrowSize = rayLength * 0.15f;
            Vector3 right = transform.right * arrowSize;
            Vector3 up = transform.up * arrowSize;
            
            Gizmos.color = Color.yellow;
            // 4-point arrow head
            Gizmos.DrawLine(arrowHead, arrowHead - transform.forward * arrowSize + right);
            Gizmos.DrawLine(arrowHead, arrowHead - transform.forward * arrowSize - right);
            Gizmos.DrawLine(arrowHead, arrowHead - transform.forward * arrowSize + up);
            Gizmos.DrawLine(arrowHead, arrowHead - transform.forward * arrowSize - up);
            
            // Draw arrow cone for even better visibility
            Gizmos.color = new Color(rayColor.r, rayColor.g, rayColor.b, 0.5f);
            Gizmos.DrawLine(arrowHead - transform.forward * arrowSize + right, arrowHead - transform.forward * arrowSize + up);
            Gizmos.DrawLine(arrowHead - transform.forward * arrowSize + up, arrowHead - transform.forward * arrowSize - right);
            Gizmos.DrawLine(arrowHead - transform.forward * arrowSize - right, arrowHead - transform.forward * arrowSize - up);
            Gizmos.DrawLine(arrowHead - transform.forward * arrowSize - up, arrowHead - transform.forward * arrowSize + right);
            
            // Draw end sphere
            Gizmos.color = rayColor;
            Gizmos.DrawSphere(arrowHead, 0.08f);
        }
        
        // ===== TARGET CONNECTION LINE =====
        if (showTargetLine && targetTransform != null)
        {
            Vector3 targetPos = targetTransform.position;
            Vector3 lookFromPoint = usePivot ? pivotPoint : transform.position;
            
            // Apply rotation mode constraints for visualization
            targetPos = ApplyRotationModeConstraints(lookFromPoint, targetPos);
            
            // Draw dashed line to target
            Gizmos.color = new Color(0f, 1f, 1f, 0.7f); // Cyan with transparency
            DrawDashedLine(lookFromPoint, targetPos, 0.2f);
            
            // Draw a small sphere at target position
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(targetPos, 0.12f);
            
            // Draw wireframe sphere around target
            Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(targetPos, 0.2f);
        }
        
        // ===== ROTATION MODE INDICATOR =====
        // Draw axis lock indicators
        DrawRotationModeGizmo();
    }
    
    void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
    
    void DrawDashedLine(Vector3 start, Vector3 end, float dashSize)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        direction.Normalize();
        
        float currentDistance = 0f;
        bool drawDash = true;
        
        while (currentDistance < distance)
        {
            float segmentLength = Mathf.Min(dashSize, distance - currentDistance);
            Vector3 segmentStart = start + direction * currentDistance;
            Vector3 segmentEnd = start + direction * (currentDistance + segmentLength);
            
            if (drawDash)
            {
                Gizmos.DrawLine(segmentStart, segmentEnd);
            }
            
            currentDistance += segmentLength;
            drawDash = !drawDash;
        }
    }
    
    void DrawRotationModeGizmo()
    {
        // Draw axis indicators showing which axes are locked/free
        Vector3 pos = transform.position;
        float axisLength = 0.5f;
        
        // X axis (Red)
        bool xLocked = rotationMode == RotationMode.LockX || rotationMode == RotationMode.LockXY || rotationMode == RotationMode.LockXZ;
        Gizmos.color = xLocked ? new Color(1f, 0f, 0f, 0.3f) : Color.red; // Dimmed if locked
        Gizmos.DrawLine(pos - transform.right * axisLength, pos + transform.right * axisLength);
        if (xLocked)
        {
            // Draw X to indicate locked
            float xSize = 0.1f;
            Gizmos.DrawLine(pos + transform.right * axisLength - transform.up * xSize, pos + transform.right * axisLength + transform.up * xSize);
            Gizmos.DrawLine(pos + transform.right * axisLength + transform.up * xSize, pos + transform.right * axisLength - transform.up * xSize);
        }
        
        // Y axis (Green)
        bool yLocked = rotationMode == RotationMode.LockY || rotationMode == RotationMode.LockXY || rotationMode == RotationMode.LockYZ;
        Gizmos.color = yLocked ? new Color(0f, 1f, 0f, 0.3f) : Color.green; // Dimmed if locked
        Gizmos.DrawLine(pos - transform.up * axisLength, pos + transform.up * axisLength);
        if (yLocked)
        {
            // Draw X to indicate locked
            float xSize = 0.1f;
            Gizmos.DrawLine(pos + transform.up * axisLength - transform.right * xSize, pos + transform.up * axisLength + transform.right * xSize);
            Gizmos.DrawLine(pos + transform.up * axisLength + transform.right * xSize, pos + transform.up * axisLength - transform.right * xSize);
        }
        
        // Z axis (Blue)
        bool zLocked = rotationMode == RotationMode.LockZ || rotationMode == RotationMode.LockXZ || rotationMode == RotationMode.LockYZ;
        Gizmos.color = zLocked ? new Color(0f, 0f, 1f, 0.3f) : Color.blue; // Dimmed if locked
        Gizmos.DrawLine(pos - transform.forward * axisLength, pos + transform.forward * axisLength);
        if (zLocked)
        {
            // Draw X to indicate locked
            float xSize = 0.1f;
            Gizmos.DrawLine(pos + transform.forward * axisLength - transform.right * xSize, pos + transform.forward * axisLength + transform.right * xSize);
            Gizmos.DrawLine(pos + transform.forward * axisLength + transform.right * xSize, pos + transform.forward * axisLength - transform.right * xSize);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // Draw additional info when selected
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, 0.2f);
        
        // Draw text info (this will show in scene view)
        if (targetTransform != null)
        {
            Vector3 midPoint = (transform.position + targetTransform.position) * 0.5f;
            float distance = Vector3.Distance(transform.position, targetTransform.position);
            
            // Unity's Handles class would be better for text, but this keeps it simple
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(midPoint, Vector3.one * 0.1f);
        }
    }
}