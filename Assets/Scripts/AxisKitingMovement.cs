using UnityEngine;

/// <summary>
/// Moves an object along a locked axis with kiting behavior to maintain distance from a target.
/// Perfect for objects like the moon that should follow the player along one axis while keeping distance.
/// </summary>
public class AxisKitingMovement : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The target to maintain distance from (usually the player)")]
    [SerializeField] private Transform target;
    
    [Tooltip("Reference to the prefab to search for in the scene")]
    [SerializeField] private GameObject targetPrefab;
    
    [Tooltip("Automatically find the player if no target is set")]
    [SerializeField] private bool autoFindPlayer = true;
    
    [Tooltip("Search method when using prefab reference")]
    [SerializeField] private PrefabSearchMethod searchMethod = PrefabSearchMethod.ByName;
    
    [Header("Axis Locking")]
    [Tooltip("Which axis to allow movement on")]
    [SerializeField] private MovementAxis movementAxis = MovementAxis.X;
    
    [Tooltip("Lock rotation on all axes")]
    [SerializeField] private bool lockRotation = true;
    
    [Header("Kiting Behavior")]
    [Tooltip("The ideal distance to maintain from the target")]
    [SerializeField] private float targetDistance = 10f;
    
    [Tooltip("Distance threshold - object won't move if within this range")]
    [SerializeField] private float distanceThreshold = 2f;
    
    [Tooltip("How fast the object moves to maintain distance")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Tooltip("Smooth movement using interpolation")]
    [SerializeField] private bool smoothMovement = true;
    
    [Tooltip("Smoothing factor (lower = smoother but slower response)")]
    [SerializeField] [Range(0.01f, 1f)] private float smoothingFactor = 0.1f;
    
    [Header("Movement Constraints")]
    [Tooltip("Minimum position on the movement axis")]
    [SerializeField] private bool useMinConstraint = false;
    [SerializeField] private float minPosition = -100f;
    
    [Tooltip("Maximum position on the movement axis")]
    [SerializeField] private bool useMaxConstraint = false;
    [SerializeField] private float maxPosition = 100f;
    
    [Header("Advanced Settings")]
    [Tooltip("Use local space instead of world space")]
    [SerializeField] private bool useLocalSpace = false;
    
    [Tooltip("Reverse kiting direction (move away instead of following)")]
    [SerializeField] private bool reverseDirection = false;
    
    [Tooltip("Only move when target is moving")]
    [SerializeField] private bool onlyMoveWhenTargetMoves = false;
    
    [Tooltip("Minimum target velocity to trigger movement")]
    [SerializeField] private float minTargetVelocity = 0.1f;
    
    [Header("Debug Info")]
    [Tooltip("Show debug information")]
    [SerializeField] private bool showDebug = false;
    
    [Tooltip("Current distance to target (read-only)")]
    [SerializeField] private float currentDistance;
    
    [Tooltip("Is currently moving (read-only)")]
    [SerializeField] private bool isMoving;
    
    // Private variables
    private Vector3 lockedPosition;
    private Quaternion initialRotation;
    private Vector3 previousTargetPosition;
    private Vector3 targetVelocity;
    
    public enum MovementAxis
    {
        X,
        Y,
        Z
    }
    
    public enum PrefabSearchMethod
    {
        ByName,         // Find by matching prefab name
        ByTag,          // Find by tag
        FirstActive     // Find first active instance (less reliable)
    }
    
    private void Start()
    {
        // Store initial rotation if we're locking it
        if (lockRotation)
        {
            initialRotation = transform.rotation;
        }
        
        // Store initial locked position
        lockedPosition = useLocalSpace ? transform.localPosition : transform.position;
        
        // Try to find target using prefab reference first
        if (target == null && targetPrefab != null)
        {
            GameObject foundInstance = FindPrefabInstance(targetPrefab);
            if (foundInstance != null)
            {
                target = foundInstance.transform;
                Debug.Log($"AxisKitingMovement on {gameObject.name} found instance of prefab '{targetPrefab.name}': {foundInstance.name}");
            }
            else
            {
                Debug.LogWarning($"AxisKitingMovement on {gameObject.name} couldn't find active instance of prefab '{targetPrefab.name}'");
            }
        }
        
        // Auto-find player if still no target
        if (target == null && autoFindPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                Debug.Log($"AxisKitingMovement on {gameObject.name} auto-found player: {player.name}");
            }
            else
            {
                Debug.LogWarning($"AxisKitingMovement on {gameObject.name} couldn't find player with 'Player' tag");
            }
        }
        
        if (target != null)
        {
            previousTargetPosition = target.position;
        }
    }
    
    /// <summary>
    /// Find an active instance of the prefab in the scene
    /// </summary>
    private GameObject FindPrefabInstance(GameObject prefab)
    {
        if (prefab == null)
            return null;
        
        switch (searchMethod)
        {
            case PrefabSearchMethod.ByName:
                // Search by name matching the prefab name
                GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (GameObject obj in allObjects)
                {
                    // Check if the name matches (handles instances with (Clone) suffix)
                    if (obj.name == prefab.name || obj.name == prefab.name + "(Clone)")
                    {
                        return obj;
                    }
                }
                break;
                
            case PrefabSearchMethod.ByTag:
                // Use the prefab's tag to find instances
                try
                {
                    GameObject taggedObj = GameObject.FindGameObjectWithTag(prefab.tag);
                    if (taggedObj != null)
                    {
                        return taggedObj;
                    }
                }
                catch
                {
                    Debug.LogWarning($"Tag '{prefab.tag}' doesn't exist in tag manager");
                }
                break;
                
            case PrefabSearchMethod.FirstActive:
                // Less reliable - just find first object with matching name
                GameObject firstMatch = GameObject.Find(prefab.name);
                if (firstMatch == null)
                {
                    firstMatch = GameObject.Find(prefab.name + "(Clone)");
                }
                return firstMatch;
        }
        
        return null;
    }
    
    private void LateUpdate()
    {
        if (target == null)
            return;
        
        // Calculate target velocity
        targetVelocity = (target.position - previousTargetPosition) / Time.deltaTime;
        previousTargetPosition = target.position;
        
        // Check if target is moving (if required)
        if (onlyMoveWhenTargetMoves && targetVelocity.magnitude < minTargetVelocity)
        {
            isMoving = false;
            if (lockRotation)
            {
                transform.rotation = initialRotation;
            }
            return;
        }
        
        // Get current position
        Vector3 currentPos = useLocalSpace ? transform.localPosition : transform.position;
        
        // Calculate distance along the movement axis
        float axisDistance = GetAxisDistance();
        currentDistance = axisDistance;
        
        // Check if we need to move
        float distanceDifference = Mathf.Abs(axisDistance - targetDistance);
        
        if (distanceDifference > distanceThreshold)
        {
            isMoving = true;
            
            // Calculate target position on the axis
            float targetAxisPosition = GetTargetAxisPosition();
            
            // Move towards target position
            float currentAxisPosition = GetCurrentAxisPosition(currentPos);
            float newAxisPosition;
            
            if (smoothMovement)
            {
                newAxisPosition = Mathf.Lerp(currentAxisPosition, targetAxisPosition, smoothingFactor);
            }
            else
            {
                float direction = Mathf.Sign(targetAxisPosition - currentAxisPosition);
                newAxisPosition = currentAxisPosition + direction * moveSpeed * Time.deltaTime;
                
                // Clamp to not overshoot
                if (direction > 0)
                {
                    newAxisPosition = Mathf.Min(newAxisPosition, targetAxisPosition);
                }
                else
                {
                    newAxisPosition = Mathf.Max(newAxisPosition, targetAxisPosition);
                }
            }
            
            // Apply constraints
            if (useMinConstraint)
            {
                newAxisPosition = Mathf.Max(newAxisPosition, minPosition);
            }
            if (useMaxConstraint)
            {
                newAxisPosition = Mathf.Min(newAxisPosition, maxPosition);
            }
            
            // Update position on the movement axis only
            Vector3 newPosition = currentPos;
            switch (movementAxis)
            {
                case MovementAxis.X:
                    newPosition.x = newAxisPosition;
                    break;
                case MovementAxis.Y:
                    newPosition.y = newAxisPosition;
                    break;
                case MovementAxis.Z:
                    newPosition.z = newAxisPosition;
                    break;
            }
            
            // Apply new position
            if (useLocalSpace)
            {
                transform.localPosition = newPosition;
            }
            else
            {
                transform.position = newPosition;
            }
        }
        else
        {
            isMoving = false;
        }
        
        // Lock rotation if needed
        if (lockRotation)
        {
            transform.rotation = initialRotation;
        }
        
        // Debug visualization
        if (showDebug)
        {
            Debug.DrawLine(transform.position, target.position, isMoving ? Color.green : Color.yellow);
        }
    }
    
    /// <summary>
    /// Get the distance along the movement axis from target to this object
    /// </summary>
    private float GetAxisDistance()
    {
        Vector3 currentPos = useLocalSpace ? transform.localPosition : transform.position;
        Vector3 targetPos = useLocalSpace && target.parent != null ? target.localPosition : target.position;
        
        switch (movementAxis)
        {
            case MovementAxis.X:
                return Mathf.Abs(currentPos.x - targetPos.x);
            case MovementAxis.Y:
                return Mathf.Abs(currentPos.y - targetPos.y);
            case MovementAxis.Z:
                return Mathf.Abs(currentPos.z - targetPos.z);
            default:
                return 0f;
        }
    }
    
    /// <summary>
    /// Get the target position value on the movement axis
    /// </summary>
    private float GetTargetAxisPosition()
    {
        Vector3 targetPos = useLocalSpace && target.parent != null ? target.localPosition : target.position;
        Vector3 currentPos = useLocalSpace ? transform.localPosition : transform.position;
        
        float targetAxisValue = 0f;
        float currentAxisValue = 0f;
        
        switch (movementAxis)
        {
            case MovementAxis.X:
                targetAxisValue = targetPos.x;
                currentAxisValue = currentPos.x;
                break;
            case MovementAxis.Y:
                targetAxisValue = targetPos.y;
                currentAxisValue = currentPos.y;
                break;
            case MovementAxis.Z:
                targetAxisValue = targetPos.z;
                currentAxisValue = currentPos.z;
                break;
        }
        
        // Determine direction and apply distance
        float direction = currentAxisValue > targetAxisValue ? 1f : -1f;
        
        if (reverseDirection)
        {
            direction *= -1f;
        }
        
        return targetAxisValue + (direction * targetDistance);
    }
    
    /// <summary>
    /// Get current position value on the movement axis
    /// </summary>
    private float GetCurrentAxisPosition(Vector3 position)
    {
        switch (movementAxis)
        {
            case MovementAxis.X:
                return position.x;
            case MovementAxis.Y:
                return position.y;
            case MovementAxis.Z:
                return position.z;
            default:
                return 0f;
        }
    }
    
    /// <summary>
    /// Set the target to follow
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            previousTargetPosition = target.position;
        }
    }
    
    /// <summary>
    /// Set the target distance to maintain
    /// </summary>
    public void SetTargetDistance(float distance)
    {
        targetDistance = distance;
    }
    
    /// <summary>
    /// Set the movement axis
    /// </summary>
    public void SetMovementAxis(MovementAxis axis)
    {
        movementAxis = axis;
    }
    
    /// <summary>
    /// Enable or disable the component
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
    }
    
    /// <summary>
    /// Manually search for the prefab instance again (useful if target spawns after Start)
    /// </summary>
    public void RefreshTarget()
    {
        if (targetPrefab != null)
        {
            GameObject foundInstance = FindPrefabInstance(targetPrefab);
            if (foundInstance != null)
            {
                target = foundInstance.transform;
                previousTargetPosition = target.position;
                Debug.Log($"AxisKitingMovement on {gameObject.name} refreshed target: {foundInstance.name}");
            }
            else
            {
                Debug.LogWarning($"AxisKitingMovement on {gameObject.name} couldn't refresh target - prefab instance not found");
            }
        }
        else if (autoFindPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                previousTargetPosition = target.position;
                Debug.Log($"AxisKitingMovement on {gameObject.name} refreshed target via Player tag: {player.name}");
            }
        }
    }
    
    /// <summary>
    /// Set the prefab to search for
    /// </summary>
    public void SetTargetPrefab(GameObject prefab)
    {
        targetPrefab = prefab;
        RefreshTarget();
    }
    
    // Public getters
    public float CurrentDistance => currentDistance;
    public bool IsMoving => isMoving;
    public Transform Target => target;
    public MovementAxis CurrentAxis => movementAxis;
    public GameObject TargetPrefab => targetPrefab;
    
    private void OnDrawGizmos()
    {
        if (!showDebug || target == null)
            return;
        
        // Draw the target distance sphere/line
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        
        Vector3 gizmoPos = transform.position;
        Vector3 targetPos = target.position;
        
        // Draw line to target
        Gizmos.color = isMoving ? Color.green : Color.yellow;
        Gizmos.DrawLine(gizmoPos, targetPos);
        
        // Draw ideal distance indicator
        Gizmos.color = Color.cyan;
        Vector3 idealPos = gizmoPos;
        
        switch (movementAxis)
        {
            case MovementAxis.X:
                float dirX = Mathf.Sign(gizmoPos.x - targetPos.x);
                if (reverseDirection) dirX *= -1f;
                idealPos = new Vector3(targetPos.x + dirX * targetDistance, gizmoPos.y, gizmoPos.z);
                Gizmos.DrawWireSphere(idealPos, 0.5f);
                Gizmos.DrawLine(new Vector3(idealPos.x, idealPos.y - 1f, idealPos.z), 
                               new Vector3(idealPos.x, idealPos.y + 1f, idealPos.z));
                break;
                
            case MovementAxis.Y:
                float dirY = Mathf.Sign(gizmoPos.y - targetPos.y);
                if (reverseDirection) dirY *= -1f;
                idealPos = new Vector3(gizmoPos.x, targetPos.y + dirY * targetDistance, gizmoPos.z);
                Gizmos.DrawWireSphere(idealPos, 0.5f);
                Gizmos.DrawLine(new Vector3(idealPos.x - 1f, idealPos.y, idealPos.z), 
                               new Vector3(idealPos.x + 1f, idealPos.y, idealPos.z));
                break;
                
            case MovementAxis.Z:
                float dirZ = Mathf.Sign(gizmoPos.z - targetPos.z);
                if (reverseDirection) dirZ *= -1f;
                idealPos = new Vector3(gizmoPos.x, gizmoPos.y, targetPos.z + dirZ * targetDistance);
                Gizmos.DrawWireSphere(idealPos, 0.5f);
                Gizmos.DrawLine(new Vector3(idealPos.x, idealPos.y - 1f, idealPos.z), 
                               new Vector3(idealPos.x, idealPos.y + 1f, idealPos.z));
                break;
        }
        
        // Draw axis constraint lines
        if (useMinConstraint || useMaxConstraint)
        {
            Gizmos.color = Color.red;
            Vector3 constraintPos = gizmoPos;
            
            if (useMinConstraint)
            {
                switch (movementAxis)
                {
                    case MovementAxis.X:
                        constraintPos.x = minPosition;
                        break;
                    case MovementAxis.Y:
                        constraintPos.y = minPosition;
                        break;
                    case MovementAxis.Z:
                        constraintPos.z = minPosition;
                        break;
                }
                Gizmos.DrawWireSphere(constraintPos, 0.3f);
            }
            
            if (useMaxConstraint)
            {
                constraintPos = gizmoPos;
                switch (movementAxis)
                {
                    case MovementAxis.X:
                        constraintPos.x = maxPosition;
                        break;
                    case MovementAxis.Y:
                        constraintPos.y = maxPosition;
                        break;
                    case MovementAxis.Z:
                        constraintPos.z = maxPosition;
                        break;
                }
                Gizmos.DrawWireSphere(constraintPos, 0.3f);
            }
        }
    }
}

