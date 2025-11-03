using UnityEngine;

public class BinaryPathMovement : MonoBehaviour
{
    [Header("Path Points")]
    [Tooltip("The end point of the path (relative to object's starting position)")]
    public Vector3 endPoint = Vector3.zero;
    
    [Header("Movement Settings")]
    [Tooltip("Speed of movement between points")]
    [Range(0.001f, 50f)]
    public float cycleSpeed = 2f;
    
    [Tooltip("Enable smooth easing as object approaches each point")]
    public bool useSmoothing = true;
    
    [Tooltip("Use local space (relative to parent) or world space")]
    public Space movementSpace = Space.World;
    
    [Header("GameObject Settings")]
    [Tooltip("Controls whether this GameObject is marked as Static (Static objects cannot move!)")]
    public bool isStatic = false;
    
    [Header("Trigger Settings")]
    [Tooltip("Enable trigger functionality - requires collision with trigger object")]
    public bool useTrigger = false;
    
    [Tooltip("Game object that will trigger this movement when player touches it")]
    public GameObject triggerObject;
    
    public enum TriggerMode
    {
        StartContinuous,        // Starts moving indefinitely
        StopPermanent,          // Stops movement permanently
        MoveToEndThenStop,      // Moves from A to B once, then stops
        HoldToMove,             // Moves only while player touches trigger
        HoldToStop,             // Stops only while player touches trigger
        HoldToReverse           // Reverses direction while player touches trigger
    }
    
    [Tooltip("How this movement responds to the trigger")]
    public TriggerMode triggerMode = TriggerMode.StartContinuous;
    
    [Header("Grip Trigger Settings")]
    [Tooltip("Enable grip trigger - movement responds when object is grabbed by player")]
    public bool useGripTrigger = false;
    
    [Tooltip("How this movement responds to being gripped (requires Grippable component on same object)")]
    public TriggerMode gripTriggerMode = TriggerMode.HoldToMove;
    
    [Header("Launcher Mode (for Grip Trigger)")]
    [Tooltip("When grabbed, automatically start full A->B->A cycle and force release before point B")]
    public bool launcherMode = false;
    
    [Tooltip("How close to end point before forcing release (0.9 = release at 90% of journey)")]
    [Range(0.7f, 0.99f)]
    public float launcherReleaseThreshold = 0.9f;
    
    [Tooltip("Gradually accelerate from slow start to full speed (feels heavier/mechanical)")]
    public bool useAcceleration = false;
    
    [Tooltip("How many seconds it takes to reach full speed from start")]
    [Range(0.1f, 5f)]
    public float accelerationTime = 2f;
    
    [Header("Cycle Settings")]
    [Tooltip("Start by moving to end point (false) or start point (true)")]
    public bool reverseDirection = false;
    
    [Tooltip("Randomize starting position along the path")]
    public bool randomizeStartPoint = false;
    
    [Tooltip("Pause duration between cycle completions")]
    public bool pauseBetweenCycles = false;
    
    [Tooltip("Duration of pause in seconds")]
    [Range(0.1f, 5f)]
    public float pauseDuration = 1f;
    
    [Tooltip("Snap back to origin instantly instead of smooth return")]
    public bool snapBackToOrigin = false;
    
    // Internal state
    private Vector3 basePosition; // Object's base position (stored on first Start())
    private Vector3 currentOffset; // This component's contribution to movement
    private bool movingToEnd = true;
    private bool isPaused = false;
    private float pauseTimer = 0f;
    private float journeyProgress = 0f;
    private bool isMovementActive = true; // Controls whether movement is running
    private bool hasBeenTriggered = false; // Track if trigger has been activated
    private bool playerTouchingTrigger = false; // Track if player is currently touching trigger
    private bool hasCompletedOneWayTrip = false; // For MoveToEndThenStop mode
    
    // Grip trigger state
    private bool playerGripping = false; // Track if player is currently gripping this object
    private bool hasBeenGripped = false; // Track if grip trigger has been activated
    // private bool hasCompletedGripTrip = false; // Unused // For MoveToEndThenStop mode with grip
    
    // Launcher mode state
    private ActiveRagdoll.Grippable cachedGrippable; // Cached reference to Grippable component
    private bool launcherJourneyActive = false; // True when launcher is doing A->B->A cycle
    private bool launcherHasReleasedThisCycle = false; // Track if we've force-released this cycle
    private float gripDisableTimer = 0f; // Timer to temporarily disable gripping
    private const float GRIP_DISABLE_DURATION = 2f; // How long to disable gripping after forced release
    private System.Collections.Generic.List<ActiveRagdoll.Gripper> activeGrippers = new System.Collections.Generic.List<ActiveRagdoll.Gripper>(); // Grippers currently holding us
    
    // Acceleration state
    private float accelerationElapsedTime = 0f; // Tracks how long we've been accelerating
    
    
    // For combining multiple components on same object
    private BinaryPathMovement[] allMovementComponents;
    private bool isFirstComponent = false;
    
    private void Awake()
    {
        Debug.Log($"[BinaryPath] {gameObject.name}: Awake - useGripTrigger={useGripTrigger}, launcherMode={launcherMode}, threshold={launcherReleaseThreshold}");
        
        // Sync the static flag with the inspector toggle
        SyncStaticFlag();
        
        // Setup trigger if enabled
        if (useTrigger)
        {
            if (triggerObject != null)
            {
                // Get or add the trigger listener component
                BinaryPathTriggerListener listener = triggerObject.GetComponent<BinaryPathTriggerListener>();
                if (listener == null)
                {
                    listener = triggerObject.AddComponent<BinaryPathTriggerListener>();
                }
                
                // Register this movement with the trigger
                listener.RegisterMovement(this);
            }
            else
            {
// Debug.LogWarning($"BinaryPathMovement on {gameObject.name}: Trigger enabled but no trigger object assigned!");
            }
            
            // Set initial movement state based on trigger mode
            switch (triggerMode)
            {
                case TriggerMode.StartContinuous:
                case TriggerMode.MoveToEndThenStop:
                case TriggerMode.HoldToMove:
                case TriggerMode.HoldToStop:
                case TriggerMode.HoldToReverse:
                    isMovementActive = false; // Wait for trigger
                    break;
                case TriggerMode.StopPermanent:
                    isMovementActive = true; // Start moving, will be stopped by trigger
                    break;
            }
        }
        
        // Setup grip trigger if enabled
        if (useGripTrigger)
        {
            cachedGrippable = GetComponent<ActiveRagdoll.Grippable>();
            if (cachedGrippable != null)
            {
                // Subscribe to grip events
                cachedGrippable.OnGripped += OnGripped;
                cachedGrippable.OnReleased += OnGripReleased;
            }
            else
            {
                // Debug.LogWarning($"BinaryPathMovement on {gameObject.name}: Grip trigger enabled but no Grippable component found on this object!");
            }
            
            // Set initial movement state based on grip trigger mode (unless launcher mode overrides)
            if (launcherMode)
            {
                // Launcher mode: wait for grab, then do full cycle
                isMovementActive = false;
            }
            else
            {
                // Normal grip trigger modes
                switch (gripTriggerMode)
                {
                    case TriggerMode.StartContinuous:
                    case TriggerMode.MoveToEndThenStop:
                    case TriggerMode.HoldToMove:
                    case TriggerMode.HoldToStop:
                    case TriggerMode.HoldToReverse:
                        isMovementActive = false; // Wait for grip
                        break;
                    case TriggerMode.StopPermanent:
                        isMovementActive = true; // Start moving, will be stopped by grip
                        break;
                }
            }
        }
    }
    
    private void Start()
    {
        // Get all movement components on this object
        allMovementComponents = GetComponents<BinaryPathMovement>();
        isFirstComponent = (allMovementComponents.Length > 0 && allMovementComponents[0] == this);
        
        // Always store base position in local space (works with parent movement)
        if (isFirstComponent)
        {
            basePosition = transform.localPosition;
        }
        else
        {
            // Get base position from first component
            basePosition = allMovementComponents[0].basePosition;
        }
        
        // Apply reverse direction if enabled
        movingToEnd = !reverseDirection;
        
        // Randomize start point if enabled
        if (randomizeStartPoint)
        {
            journeyProgress = Random.Range(0f, 1f);
            
            // Calculate initial offset based on random progress
            Vector3 targetOffset = movingToEnd ? endPoint : Vector3.zero;
            Vector3 startOffset = movingToEnd ? Vector3.zero : endPoint;
            float t = useSmoothing ? SmoothStep(journeyProgress) : journeyProgress;
            currentOffset = Vector3.Lerp(startOffset, targetOffset, t);
        }
        else
        {
            // Calculate initial offset normally
            if (reverseDirection)
            {
                currentOffset = endPoint;
            }
            else
            {
                currentOffset = Vector3.zero;
            }
        }
    }
    
    private void Update()
    {
        // Launcher mode: Handle grip disable timer
        if (launcherMode && gripDisableTimer > 0f)
        {
            gripDisableTimer -= Time.deltaTime;
            if (gripDisableTimer <= 0f)
            {
                // Timer expired - re-enable gripping
                if (cachedGrippable != null)
                {
                    cachedGrippable.enabled = true;
                    Debug.Log($"[BinaryPath] {gameObject.name}: Grip timer expired, re-enabled Grippable");
                }
            }
        }
        
        // Handle hold modes - update movement state based on player touching trigger
        if (useTrigger)
        {
            switch (triggerMode)
            {
                case TriggerMode.HoldToMove:
                    isMovementActive = playerTouchingTrigger;
                    break;
                case TriggerMode.HoldToStop:
                    isMovementActive = !playerTouchingTrigger;
                    break;
                // HoldToReverse is handled in OnTriggered/OnTriggerReleased
            }
        }
        
        // Handle grip hold modes - update movement state based on player gripping (NOT for launcher mode)
        if (useGripTrigger && !launcherMode)
        {
            switch (gripTriggerMode)
            {
                case TriggerMode.HoldToMove:
                    isMovementActive = playerGripping;
                    break;
                case TriggerMode.HoldToStop:
                    isMovementActive = !playerGripping;
                    break;
                // HoldToReverse is handled in OnGripped/OnGripReleased
            }
        }
        
        // Launcher mode: once journey starts, it continues regardless of grip state
        // (isMovementActive is set true in OnGripped and stays true until cycle completes)
        
        // Don't update if movement is not active (waiting for trigger or stopped)
        if (!isMovementActive)
        {
            if (launcherMode && launcherJourneyActive)
            {
                Debug.LogWarning($"[BinaryPath] {gameObject.name}: isMovementActive=FALSE but launcherJourneyActive=TRUE! This shouldn't happen!");
            }
            return;
        }
        
        // Handle pause state
        if (isPaused)
        {
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= pauseDuration)
            {
                isPaused = false;
                pauseTimer = 0f;
            }
            return;
        }
        
        // Calculate this component's offset contribution
        Vector3 targetOffset = movingToEnd ? endPoint : Vector3.zero;
        Vector3 startOffset = movingToEnd ? Vector3.zero : endPoint;
        
        // Update progress with optional acceleration (launcher mode only)
        float speedMultiplier = 1f;
        if (useGripTrigger && launcherMode && useAcceleration && movingToEnd && launcherJourneyActive)
        {
            // Track acceleration time
            accelerationElapsedTime += Time.deltaTime;
            
            // Smooth acceleration from 0 to full speed over accelerationTime
            // Using ease-in curve for smooth mechanical feel
            float accelerationProgress = Mathf.Clamp01(accelerationElapsedTime / accelerationTime);
            speedMultiplier = Mathf.SmoothStep(0.1f, 1f, accelerationProgress); // 0.1 = slow start (not zero to avoid getting stuck)
            
            // Debug every 0.5 seconds
            if (accelerationElapsedTime % 0.5f < Time.deltaTime)
            {
                Debug.Log($"[BinaryPath] {gameObject.name}: Acceleration {accelerationElapsedTime:F2}s/{accelerationTime:F2}s, Speed={speedMultiplier:F2}x");
            }
        }
        
        journeyProgress += Time.deltaTime * cycleSpeed * speedMultiplier;
        
        // Launcher mode: Force release just before reaching point B
        if (launcherMode && launcherJourneyActive)
        {
            if (movingToEnd && !launcherHasReleasedThisCycle)
            {
                // Debug every frame to see progress (A -> B)
                if (journeyProgress > 0.5f) // Only log after 50% to avoid spam
                {
                    Debug.Log($"[BinaryPath] {gameObject.name}: A->B Progress={journeyProgress:F3}, Threshold={launcherReleaseThreshold}");
                }
                
                if (journeyProgress >= launcherReleaseThreshold)
                {
                    Debug.Log($"[BinaryPath] {gameObject.name}: THRESHOLD REACHED! Calling ForceReleaseAllGrippers()");
                    // Force release and disable grabability to create launch effect
                    ForceReleaseAllGrippers();
                    launcherHasReleasedThisCycle = true;
                    Debug.Log($"[BinaryPath] {gameObject.name}: ForceReleaseAllGrippers() completed");
                }
            }
            else if (!movingToEnd)
            {
                // Debug return journey (B -> A)
                if (journeyProgress > 0.5f)
                {
                    Debug.Log($"[BinaryPath] {gameObject.name}: B->A Return Progress={journeyProgress:F3}");
                }
            }
        }
        
        if (journeyProgress >= 1f)
        {
            // Reached the target point
            journeyProgress = 1f;
            currentOffset = targetOffset;
            
            Debug.Log($"[BinaryPath] {gameObject.name}: Reached target point! movingToEnd={movingToEnd}, launcherMode={launcherMode}, launcherJourneyActive={launcherJourneyActive}");
            
            // Handle MoveToEndThenStop mode for regular trigger
            if (useTrigger && triggerMode == TriggerMode.MoveToEndThenStop && movingToEnd)
            {
                Debug.Log($"[BinaryPath] {gameObject.name}: Regular trigger MoveToEndThenStop - stopping");
                // Stop movement at end point
                hasCompletedOneWayTrip = true;
                isMovementActive = false;
            }
            // Handle MoveToEndThenStop mode for grip trigger - stop at end while held
            else if (useGripTrigger && !launcherMode && gripTriggerMode == TriggerMode.MoveToEndThenStop && movingToEnd && playerGripping)
            {
                Debug.Log($"[BinaryPath] {gameObject.name}: Grip trigger MoveToEndThenStop - stopping");
                // Stop at end point while player is holding
                // hasCompletedGripTrip = false;
                isMovementActive = false;
            }
            else
            {
                // Handle return behavior
                if (movingToEnd)
                {
                    Debug.Log($"[BinaryPath] {gameObject.name}: At Point B, starting return to Point A");
                    // We've reached the end, now go back
                    movingToEnd = false;
                    
                    if (snapBackToOrigin)
                    {
                        // Snap instantly back to start
                        currentOffset = Vector3.zero;
                        
                        // Launcher mode: Stop at A after snap back
                        if (launcherMode && launcherJourneyActive)
                        {
                            Debug.Log($"[BinaryPath] {gameObject.name}: Snapped back to Point A! Launcher journey complete");
                            // Journey complete - reset state
                            launcherHasReleasedThisCycle = false;
                            launcherJourneyActive = false;
                            isMovementActive = false; // Stop and wait for next grab
                            movingToEnd = true; // Reset for next cycle
                            Debug.Log($"[BinaryPath] {gameObject.name}: Launcher reset complete, ready for next grab");
                        }
                        else
                        {
                            movingToEnd = true; // Reset to move to end again (continuous loop)
                            
                            // Apply pause if enabled
                            if (pauseBetweenCycles)
                            {
                                isPaused = true;
                            }
                        }
                    }
                    
                    journeyProgress = 0f;
                }
                else
                {
                    // We've returned to start (point A)
                    
                    // Launcher mode: Stop movement and reset for next cycle
                    if (launcherMode && launcherJourneyActive)
                    {
                        Debug.Log($"[BinaryPath] {gameObject.name}: Back at Point A! Launcher journey complete");
                        // Journey complete - reset state (gripping will be re-enabled by timer)
                        launcherHasReleasedThisCycle = false;
                        launcherJourneyActive = false;
                        isMovementActive = false; // Stop and wait for next grab
                        Debug.Log($"[BinaryPath] {gameObject.name}: Launcher reset complete, ready for next grab");
                    }
                    
                    // Special handling for grip trigger MoveToEndThenStop - stop at start and reset
                    if (useGripTrigger && gripTriggerMode == TriggerMode.MoveToEndThenStop && !playerGripping)
                    {
                        isMovementActive = false;
                        movingToEnd = true;
                        journeyProgress = 0f;
                        // hasCompletedGripTrip = false; // Reset state
                    }
                    else
                    {
                        movingToEnd = true;
                        journeyProgress = 0f;
                        
                        // Apply pause if enabled
                        if (pauseBetweenCycles)
                        {
                            isPaused = true;
                        }
                    }
                }
            }
        }
        else
        {
            // Calculate current offset
            float t = useSmoothing ? SmoothStep(journeyProgress) : journeyProgress;
            currentOffset = Vector3.Lerp(startOffset, targetOffset, t);
        }
    }
    
    private void LateUpdate()
    {
        // Only the first component applies the combined position
        if (!isFirstComponent)
            return;
        
        // Sum all offsets from all movement components
        Vector3 combinedOffset = Vector3.zero;
        foreach (BinaryPathMovement component in allMovementComponents)
        {
            combinedOffset += component.currentOffset;
        }
        
        // Always apply in local space (works with parent movement)
        transform.localPosition = basePosition + combinedOffset;
    }
    
    /// <summary>
    /// Called by trigger listener when player touches the trigger object
    /// </summary>
    public void OnTriggered()
    {
        playerTouchingTrigger = true;
        
        // Don't process if already triggered in certain modes
        if (hasBeenTriggered && (triggerMode == TriggerMode.StartContinuous || 
                                  triggerMode == TriggerMode.StopPermanent || 
                                  triggerMode == TriggerMode.MoveToEndThenStop))
        {
            return;
        }
        
        hasBeenTriggered = true;
        
        switch (triggerMode)
        {
            case TriggerMode.StartContinuous:
                isMovementActive = true;
                break;
                
            case TriggerMode.StopPermanent:
                isMovementActive = false;
                break;
                
            case TriggerMode.MoveToEndThenStop:
                if (!hasCompletedOneWayTrip)
                {
                    isMovementActive = true;
                }
                break;
                
            case TriggerMode.HoldToMove:
                break;
                
            case TriggerMode.HoldToStop:
                break;
                
            case TriggerMode.HoldToReverse:
                // Reverse direction when player touches
                movingToEnd = !movingToEnd;
                journeyProgress = 0f;
                break;
        }
    }
    
    /// <summary>
    /// Called by trigger listener when player stops touching the trigger object
    /// </summary>
    public void OnTriggerReleased()
    {
        playerTouchingTrigger = false;
        
        // Reverse back when releasing trigger in HoldToReverse mode
        if (triggerMode == TriggerMode.HoldToReverse)
        {
            movingToEnd = !movingToEnd;
            journeyProgress = 0f;
        }
        else if (triggerMode == TriggerMode.HoldToMove || triggerMode == TriggerMode.HoldToStop)
        {
        }
    }
    
    /// <summary>
    /// Called by Grippable when player grabs this object
    /// </summary>
    private void OnGripped()
    {
        playerGripping = true;
        Debug.Log($"[BinaryPath] {gameObject.name}: OnGripped called. launcherMode={launcherMode}, launcherJourneyActive={launcherJourneyActive}");
        
        // Launcher mode: Track which grippers are currently gripping us
        if (launcherMode)
        {
            // Snapshot all grippers that currently have active joints (they're gripping something)
            ActiveRagdoll.Gripper[] allGrippers = FindObjectsOfType<ActiveRagdoll.Gripper>();
            foreach (ActiveRagdoll.Gripper gripper in allGrippers)
            {
                ConfigurableJoint joint = gripper.GetComponent<ConfigurableJoint>();
                if (joint != null && !activeGrippers.Contains(gripper))
                {
                    activeGrippers.Add(gripper);
                    Debug.Log($"[BinaryPath] {gameObject.name}: Tracked gripper {gripper.gameObject.name} (has joint, likely gripping us)");
                }
            }
            Debug.Log($"[BinaryPath] {gameObject.name}: Now tracking {activeGrippers.Count} active grippers");
        }
        
        // Launcher mode: Start the journey!
        if (launcherMode && !launcherJourneyActive)
        {
            // "Here we go, this is happening no matter what now"
            launcherJourneyActive = true;
            isMovementActive = true;
            movingToEnd = true;
            journeyProgress = 0f;
            launcherHasReleasedThisCycle = false;
            accelerationElapsedTime = 0f; // Reset acceleration timer for new journey
            Debug.Log($"[BinaryPath] {gameObject.name}: LAUNCHER JOURNEY STARTED! Threshold={launcherReleaseThreshold}");
            return; // Don't process normal grip modes
        }
        
        // Normal grip trigger modes
        switch (gripTriggerMode)
        {
            case TriggerMode.StartContinuous:
                if (!hasBeenGripped)
                {
                    isMovementActive = true;
                    hasBeenGripped = true;
                }
                break;
                
            case TriggerMode.StopPermanent:
                if (!hasBeenGripped)
                {
                    isMovementActive = false;
                    hasBeenGripped = true;
                }
                break;
                
            case TriggerMode.MoveToEndThenStop:
                // Start moving to end point
                movingToEnd = true;
                journeyProgress = 0f;
                isMovementActive = true;
                // hasCompletedGripTrip = false;
                break;
                
            case TriggerMode.HoldToMove:
                break;
                
            case TriggerMode.HoldToStop:
                break;
                
            case TriggerMode.HoldToReverse:
                // Reverse direction when player grips
                movingToEnd = !movingToEnd;
                journeyProgress = 0f;
                break;
        }
    }
    
    /// <summary>
    /// Called by Grippable when player releases grip on this object
    /// </summary>
    private void OnGripReleased()
    {
        playerGripping = false;
        
        // Clear tracked grippers (manual release)
        activeGrippers.Clear();
        
        // Launcher mode: Ignore manual releases, journey continues
        if (launcherMode && launcherJourneyActive)
        {
            return;
        }
        
        // Normal grip trigger modes
        // Reverse back when releasing grip in HoldToReverse mode
        if (gripTriggerMode == TriggerMode.HoldToReverse)
        {
            movingToEnd = !movingToEnd;
            journeyProgress = 0f;
        }
        else if (gripTriggerMode == TriggerMode.MoveToEndThenStop)
        {
            // Always return to start when released
            movingToEnd = false;
            journeyProgress = 0f;
            isMovementActive = true;
        }
        else if (gripTriggerMode == TriggerMode.HoldToMove || gripTriggerMode == TriggerMode.HoldToStop)
        {
        }
    }
    
    // Smooth easing function for deceleration effect
    private float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
    
    /// <summary>
    /// Forces all grippers to release by disabling them temporarily
    /// </summary>
    private void ForceReleaseAllGrippers()
    {
        Debug.Log($"[BinaryPath] {gameObject.name}: ForceReleaseAllGrippers() START - {activeGrippers.Count} tracked grippers");
        
        // Only disable the grippers that we tracked as gripping this object
        int disabledCount = 0;
        foreach (ActiveRagdoll.Gripper gripper in activeGrippers)
        {
            if (gripper != null && gripper.enabled)
            {
                Debug.Log($"[BinaryPath] {gameObject.name}: Disabling tracked gripper {gripper.gameObject.name}");
                // Disable the gripper (this will trigger OnDisable which calls UnGrip)
                gripper.enabled = false;
                
                // Re-enable it after a short delay using coroutine
                StartCoroutine(ReEnableGripperAfterDelay(gripper, 0.1f));
                disabledCount++;
            }
        }
        
        // Clear the list
        activeGrippers.Clear();
        
        Debug.Log($"[BinaryPath] {gameObject.name}: Disabled {disabledCount} grippers");
        
        // Disable the Grippable component temporarily to prevent immediate re-grip
        if (cachedGrippable != null)
        {
            Debug.Log($"[BinaryPath] {gameObject.name}: Disabling Grippable component for {GRIP_DISABLE_DURATION} seconds");
            cachedGrippable.enabled = false;
            gripDisableTimer = GRIP_DISABLE_DURATION; // Start timer to re-enable
        }
        else
        {
            Debug.LogWarning($"[BinaryPath] {gameObject.name}: cachedGrippable is null!");
        }
    }
    
    /// <summary>
    /// Re-enables a gripper after a short delay
    /// </summary>
    private System.Collections.IEnumerator ReEnableGripperAfterDelay(ActiveRagdoll.Gripper gripper, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (gripper != null)
        {
            gripper.enabled = true;
            Debug.Log($"[BinaryPath] {gameObject.name}: Re-enabled gripper on {gripper.gameObject.name}");
        }
    }
    
    
    
    // Visualize the path in the editor
    private void OnDrawGizmosSelected()
    {
        Vector3 start, end;
        
        if (Application.isPlaying)
        {
            // During play, show this component's contribution in world space
            Vector3 worldBasePos = transform.parent != null ? 
                transform.parent.TransformPoint(basePosition) : basePosition;
            
            start = worldBasePos;
            
            // EndPoint interpretation depends on movementSpace setting
            if (movementSpace == Space.Self && transform.parent != null)
            {
                end = worldBasePos + transform.parent.TransformDirection(endPoint);
            }
            else
            {
                end = worldBasePos + endPoint;
            }
        }
        else
        {
            // In editor, use current position as start
            start = transform.position;
            
            // Show end point based on movementSpace setting
            if (movementSpace == Space.Self && transform.parent != null)
            {
                end = transform.parent.TransformPoint(transform.localPosition + endPoint);
            }
            else if (transform.parent != null)
            {
                // World space endPoint, but need to show relative to current parent position
                Vector3 localStart = transform.localPosition;
                end = transform.parent.TransformPoint(localStart) + endPoint;
            }
            else
            {
                end = start + endPoint;
            }
        }
        
        // Draw start point
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(start, 0.2f);
        
        // Draw end point (relative to start)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(end, 0.2f);
        
        // Draw line between points
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, end);
    }
    
    /// <summary>
    /// Synchronizes the isStatic toggle with the GameObject's actual static flag
    /// </summary>
    private void SyncStaticFlag()
    {
        if (gameObject.isStatic != isStatic)
        {
            gameObject.isStatic = isStatic;
            
            if (isStatic)
            {
                // Debug.LogWarning($"BinaryPathMovement on {gameObject.name}: GameObject marked as Static - movement will not work! Uncheck 'Is Static' to enable movement.");
            }
        }
    }
    
    /// <summary>
    /// Called when values change in the inspector - syncs the static flag
    /// </summary>
    private void OnValidate()
    {
        // Only sync in editor mode to avoid runtime issues
        if (!Application.isPlaying)
        {
            SyncStaticFlag();
        }
    }
    
    /// <summary>
    /// Clean up event subscriptions when destroyed
    /// </summary>
    private void OnDestroy()
    {
        if (useGripTrigger)
        {
            ActiveRagdoll.Grippable grippable = GetComponent<ActiveRagdoll.Grippable>();
            if (grippable != null)
            {
                grippable.OnGripped -= OnGripped;
                grippable.OnReleased -= OnGripReleased;
            }
        }
    }
}

