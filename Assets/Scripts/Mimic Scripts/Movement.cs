using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MimicSpace
{
    /// <summary>
    /// This is a very basic movement script, if you want to replace it
    /// Just don't forget to update the Mimic's velocity vector with a Vector3(x, 0, z)
    /// </summary>
    public class Movement : MonoBehaviour
    {
        [Header("Controls")]
        [Tooltip("Use player input instead of AI")]
        public bool manualControl = false;
        [Tooltip("Body Height from ground")]
        [Range(0.5f, 5f)]
        public float height = 0.8f;
        public float speed = 5f;
        Vector3 velocity = Vector3.zero;
        public float velocityLerpCoef = 4f;
        Mimic myMimic;

        [Header("AI Exploration")]
        [Tooltip("Number of rays to cast in a circle (must be divisible by raysPerGroup)")]
        public int totalRays = 120;
        [Tooltip("Number of rays to group together")]
        public int raysPerGroup = 6;
        [Tooltip("Distance to detect obstacles")]
        public float raycastDistance = 10f;
        [Tooltip("Distance from target before choosing new target")]
        public float targetReachedDistance = 2f;
        [Tooltip("How far to sample into open space for target point")]
        [Range(0.3f, 1f)]
        public float targetDepthRatio = 0.7f;
        [Tooltip("Time interval between automatic environment snapshots (0 = disabled)")]
        public float snapshotInterval = 0f;
        
        [Header("Forward Movement")]
        [Tooltip("Prevent backtracking - ignore targets behind current direction")]
        public bool forwardBiasEnabled = true;
        [Tooltip("Score multiplier for forward-facing targets (1.0 = no bias, 1.5 = 50% bonus)")]
        [Range(1.0f, 2.0f)]
        public float forwardBiasMultiplier = 1.3f;
        [Tooltip("Minimum distance from previous target to avoid ping-ponging")]
        public float minDistanceFromPreviousTarget = 3f;
        [Tooltip("Time stuck before allowing 360° emergency scan")]
        public float emergencyBacktrackTime = 2f;
        
        [Header("Exploration Memory")]
        [Tooltip("Enable memory system to avoid revisiting explored areas")]
        public bool memoryEnabled = true;
        [Tooltip("Number of previous positions to remember")]
        public int memorySize = 25;
        [Tooltip("Radius around visited positions to consider as explored")]
        public float exploredRadius = 4f;
        [Tooltip("How strongly to avoid explored areas (higher = stronger avoidance)")]
        [Range(0f, 1f)]
        public float explorationBias = 0.7f;
        
        [Header("Exploration Randomness")]
        [Tooltip("Chance to choose a middling path instead of optimal (adds unpredictability)")]
        [Range(0f, 0.2f)]
        public float suboptimalPathChance = 0.05f; // 5% chance
        
        [Header("Player Detection & Chase")]
        [Tooltip("Mimic will detect and chase anything with a MimicTarget component attached")]
        public int minRaysToDetectPlayer = 1;
        [Tooltip("Time between snapshots during cooldown mode")]
        public float cooldownSnapshotInterval = 2f;
        [Tooltip("Search radius during cooldown (smaller = stay close to last known location)")]
        public float cooldownRadius = 5f;
        [Tooltip("Enable verbose debug logging for detection troubleshooting")]
        public bool debugDetection = false;
        
        [Header("Proximity Homing Mode")]
        [Tooltip("Distance at which homing mode engages (relentless distance-based tracking)")]
        public float homingEngageDistance = 3f;
        [Tooltip("Snapshot interval during homing mode (for environmental awareness checks)")]
        public float homingSnapshotFrequency = 0.1f;
        [Tooltip("Distance at which mimic is considered in contact (won't lose player)")]
        public float contactProximityDistance = 1.5f;
        
        [Header("Containment Boundary")]
        [Tooltip("Enable boundary containment - Mimic will never cross this box area")]
        public bool enableBoundary = false;
        [Tooltip("Center position of the containment box (in world space)")]
        public Vector3 boundaryCenter = Vector3.zero;
        [Tooltip("Scale of the containment box (width X, height Y, length Z in meters)")]
        public Vector3 boundaryScale = new Vector3(50f, 10f, 50f);
        [Tooltip("Rotation of the containment box (Euler angles in degrees)")]
        public Vector3 boundaryRotation = Vector3.zero;

        // Spatial awareness
        Vector3 currentTarget;
        Vector3 previousTarget;
        bool hasTarget = false;
        Vector3[] environmentSnapshot;  // Stores hit points from last raycast
        float lastSnapshotTime = 0f;
        
        // Memory system
        List<Vector3> visitedPositions;
        
        // Player detection
        bool isChasing = false;
        bool isProximityHoming = false; // Close-range direct homing to player
        bool isInContactProximity = false; // Very close - maintaining lock even without ray detection
        bool isCooldown = false; // Searching around after losing player
        bool isRetreating = false; // Moving away from kill location after respawn triggered
        
        // Public properties to expose states for other components (e.g., MimicRespawnTrigger)
        public bool IsProximityHoming { get { return isProximityHoming; } }
        public bool IsChasing { get { return isChasing; } }
        int cooldownSnapshotCount = 0;
        Vector3 cooldownSearchCenter; // Location where we lost the player
        List<Vector3> cooldownSearchedDirections; // Directions already checked during cooldown
        GameObject detectedPlayer = null;
        int playerDetectionRayCount = 0;
        
        // Retreat mode tracking
        Vector3 retreatFromPosition; // Position to retreat away from
        float retreatStartTime;
        float retreatDuration = 10f; // How long to stay in retreat mode
        List<Vector3> retreatWaypoints; // Positions visited during THIS retreat (prevents backtracking)
        float retreatWaypointRadius = 3f; // How close to consider "already been here"
        
        // bool isStuck = false; // Unused - only assigned, never read
        float stuckTimer = 0f;

        private void Start()
        {
            myMimic = GetComponent<Mimic>();
            environmentSnapshot = new Vector3[totalRays];
            previousTarget = transform.position; // Initialize to current position
            visitedPositions = new List<Vector3>();
            cooldownSearchedDirections = new List<Vector3>();
            retreatWaypoints = new List<Vector3>();
            
            // Take initial snapshot and find first target (allow full 360° on start)
            FindNewTarget(true);
        }
        
        /// <summary>
        /// PUBLIC METHOD: Called by MimicKillNotifier when player is killed.
        /// Forces Mimic into RETREAT mode to move away from the kill location.
        /// </summary>
        public void TriggerRetreat(Vector3 killPosition)
        {
            
            // Enter retreat mode
            isRetreating = true;
            retreatFromPosition = killPosition;
            retreatStartTime = Time.time;
            
            // Exit all other states
            isChasing = false;
            isProximityHoming = false;
            isInContactProximity = false;
            isCooldown = false;
            detectedPlayer = null;
            
            
            // Clear memory to allow free movement
            if (visitedPositions != null)
            {
                visitedPositions.Clear();
            }
            
            // Clear retreat waypoints and add starting position
            if (retreatWaypoints != null)
            {
                retreatWaypoints.Clear();
            }
            retreatWaypoints.Add(transform.position); // Don't go back to where we started
            
            
            // Force immediate new target selection
            FindNewTarget(emergencyMode: true, cooldownMode: false, retreatMode: true);
            
        }

        void Update()
        {
            if (manualControl)
            {
                // Original manual control
                velocity = Vector3.Lerp(velocity, new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized * speed, velocityLerpCoef * Time.deltaTime);
            }
            else
            {
                // ======= STATE MACHINE: RETREAT → EXPLORE → HUNT → COOLDOWN → EXPLORE =======
                
                // Check retreat mode timer
                if (isRetreating)
                {
                    float retreatTimeElapsed = Time.time - retreatStartTime;
                    if (retreatTimeElapsed >= retreatDuration)
                    {
                        // Retreat complete - return to exploration
                        isRetreating = false;
                    }
                }
                
                bool needsNewTarget = false;
                
                if (!hasTarget)
                {
                    // No target set - need new snapshot
                    needsNewTarget = true;
                }
                else
                {
                    float distanceToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                                               new Vector3(currentTarget.x, 0, currentTarget.z));
                    
                    // ===== RETREAT MODE =====
                    if (isRetreating)
                    {
                        // 1. Reached target - find new retreat point
                        if (distanceToTarget < targetReachedDistance)
                        {
                            // Add current position to retreat waypoints to avoid backtracking
                            retreatWaypoints.Add(transform.position);
                            needsNewTarget = true;
                        }
                        // 2. Stuck - find alternative retreat path
                        else if (velocity.magnitude < 0.1f)
                        {
                            stuckTimer += Time.deltaTime;
                            if (stuckTimer > 1.5f)
                            {
                                needsNewTarget = true;
                                // isStuck = true;
                            }
                        }
                        // 3. Path blocked - reroute
                        else
                        {
                            RaycastHit hit;
                            Vector3 directionToTarget = (currentTarget - transform.position).normalized;
                            if (Physics.Raycast(transform.position, directionToTarget, out hit, 1.5f))
                            {
                                needsNewTarget = true;
                            }
                            
                            stuckTimer = 0f;
                            // isStuck = false;
                        }
                    }
                    // ===== EXPLORE MODE =====
                    else if (!isChasing && !isCooldown)
                    {
                        // 1. Reached target
                        if (distanceToTarget < targetReachedDistance)
                        {
                            needsNewTarget = true;
                        }
                        // 2. Stuck for too long (emergency)
                        else if (velocity.magnitude < 0.1f)
                        {
                            stuckTimer += Time.deltaTime;
                            if (stuckTimer > 1.5f)
                            {
                                needsNewTarget = true;
                                // isStuck = true;
                            }
                        }
                        // 3. Path blocked
                        else
                        {
                            RaycastHit hit;
                            Vector3 directionToTarget = (currentTarget - transform.position).normalized;
                            if (Physics.Raycast(transform.position, directionToTarget, out hit, 1.5f))
                            {
                                needsNewTarget = true;
                            }
                            
                            stuckTimer = 0f;
                            // isStuck = false;
                        }
                        
                        // 4. Time-based snapshot (optional)
                        if (snapshotInterval > 0f && Time.time - lastSnapshotTime >= snapshotInterval)
                        {
                            needsNewTarget = true;
                        }
                    }
                    // ===== HUNT MODE =====
                    else if (isChasing)
                    {
                        // PROXIMITY HOMING: Increased snapshot frequency when close to player
                        if (isProximityHoming)
                        {
                            if (Time.time - lastSnapshotTime >= homingSnapshotFrequency)
                            {
                                needsNewTarget = true;
                            }
                        }
                        // NORMAL HUNT: Take snapshot when we reach the last known player position
                        else if (distanceToTarget < targetReachedDistance)
                        {
                            needsNewTarget = true;
                        }
                        // Or if stuck/blocked
                        else if (velocity.magnitude < 0.1f)
                        {
                            stuckTimer += Time.deltaTime;
                            if (stuckTimer > 1.5f)
                            {
                                needsNewTarget = true;
                            }
                        }
                        else
                        {
                            stuckTimer = 0f;
                        }
                    }
                    // ===== COOLDOWN MODE =====
                    else if (isCooldown)
                    {
                        // Time-based snapshots for 5 attempts to find player
                        if (Time.time - lastSnapshotTime >= cooldownSnapshotInterval)
                        {
                            needsNewTarget = true;
                            cooldownSnapshotCount++;
                            
                            if (cooldownSnapshotCount >= 5)
                            {
                                // Cooldown complete, return to exploration
                                isCooldown = false;
                                cooldownSnapshotCount = 0;
                            }
                        }
                    }
                }
                
                // Take new snapshot if needed
                if (needsNewTarget)
                {
                    bool emergencyMode = stuckTimer >= emergencyBacktrackTime;
                    bool cooldownMode = isCooldown;
                    bool retreatMode = isRetreating;
                    FindNewTarget(emergencyMode, cooldownMode, retreatMode);
                    lastSnapshotTime = Time.time;
                }
                
                // ===== PROXIMITY HOMING MODE =====
                // When close to player, RELENTLESSLY home in - distance-based tracking (NOT ray-based)
                if (isChasing && detectedPlayer != null)
                {
                    float distanceToPlayer = Vector3.Distance(transform.position, detectedPlayer.transform.position);
                    
                    // ENGAGE homing mode when within distance
                    if (distanceToPlayer <= homingEngageDistance)
                    {
                        if (!isProximityHoming)
                        {
                            isProximityHoming = true;
                        }
                        
                        // UPDATE TARGET EVERY FRAME to player's exact position (relentless homing)
                        Vector3 playerPos = detectedPlayer.transform.position;
                        RaycastHit playerGroundCheck;
                        if (Physics.Raycast(playerPos + Vector3.up * 5f, -Vector3.up, out playerGroundCheck, 100f, -1, QueryTriggerInteraction.Ignore))
                        {
                            currentTarget = new Vector3(playerPos.x, playerGroundCheck.point.y, playerPos.z);
                        }
                        else
                        {
                            currentTarget = new Vector3(playerPos.x, transform.position.y, playerPos.z);
                        }
                        currentTarget = ClampToBoundary(currentTarget);
                        hasTarget = true;
                    }
                    else
                    {
                        // DISENGAGE homing mode only if player physically moves beyond distance
                        if (isProximityHoming)
                        {
                            isProximityHoming = false;
                            // Don't clear detectedPlayer - stay in hunt mode
                        }
                    }
                }
                else if (isProximityHoming)
                {
                    // Player object lost - exit homing mode
                    isProximityHoming = false;
                }
                
                // Navigate toward current target
                if (hasTarget)
                {
                    Vector3 directionToTarget = (currentTarget - transform.position).normalized;
                    directionToTarget.y = 0; // Keep on horizontal plane
                    
                    velocity = Vector3.Lerp(velocity, directionToTarget * speed, velocityLerpCoef * Time.deltaTime);
                }
            }

            // Assigning velocity to the mimic to assure great leg placement
            myMimic.velocity = velocity;

            transform.position = transform.position + velocity * Time.deltaTime;
            RaycastHit groundHit;
            Vector3 destHeight = transform.position;
            // Ground detection: Ignore triggers to avoid detecting player's sphere collider
            if (Physics.Raycast(transform.position + Vector3.up * 5f, -Vector3.up, out groundHit, 100f, -1, QueryTriggerInteraction.Ignore))
                destHeight = new Vector3(transform.position.x, groundHit.point.y + height, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, destHeight, velocityLerpCoef * Time.deltaTime);
        }

        /// <summary>
        /// Takes a snapshot of the environment and calculates a target position to navigate to
        /// </summary>
        /// <param name="emergencyMode">If true, allows 360° scan including backward directions</param>
        /// <param name="cooldownMode">If true, searching around after losing player (360° scan, no forward bias)</param>
        /// <param name="retreatMode">If true, moving away from kill location (prioritizes furthest targets)</param>
        void FindNewTarget(bool emergencyMode = false, bool cooldownMode = false, bool retreatMode = false)
        {
            Vector3 origin = transform.position;
            float angleStep = 360f / totalRays;
            int numberOfGroups = totalRays / raysPerGroup;

            // Add current position to visited memory
            if (memoryEnabled)
            {
                visitedPositions.Add(origin);
                // Keep list at max size
                if (visitedPositions.Count > memorySize)
                {
                    visitedPositions.RemoveAt(0); // Remove oldest
                }
            }

            // Get current movement direction for forward bias
            Vector3 currentMovementDirection = velocity.magnitude > 0.1f ? velocity.normalized : transform.forward;

            // Store distances and hit points for each ray
            float[] rayDistances = new float[totalRays];
            Vector3[] rayDirections = new Vector3[totalRays];
            
            // Player detection
            playerDetectionRayCount = 0;
            MimicTarget detectedTarget = null;
            Vector3 playerPositionSum = Vector3.zero;
            int totalHitsAcrossAllRays = 0; // Debug counter

            // ENVIRONMENTAL SNAPSHOT: Cast all rays in a horizontal circle
            for (int i = 0; i < totalRays; i++)
            {
                float angle = i * angleStep;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                rayDirections[i] = direction;
                
                // Get ALL hits along this ray (including triggers for player detection sphere)
                RaycastHit[] hits = Physics.RaycastAll(origin, direction, raycastDistance, -1, QueryTriggerInteraction.Collide);
                
                if (hits.Length > 0)
                {
                    totalHitsAcrossAllRays += hits.Length; // Track total hits
                    
                    // Find closest hit for environment mapping
                    RaycastHit closestHit = hits[0];
                    float closestDistance = hits[0].distance;
                    
                    foreach (RaycastHit hit in hits)
                    {
                        if (hit.distance < closestDistance)
                        {
                            closestDistance = hit.distance;
                            closestHit = hit;
                        }
                    }
                    
                    rayDistances[i] = closestDistance;
                    environmentSnapshot[i] = closestHit.point;
                    
                    // SIMPLE PLAYER DETECTION: Check if ray hit the player's sphere collider
                    foreach (RaycastHit hit in hits)
                    {
                        MimicTarget target = hit.collider.GetComponent<MimicTarget>();
                        if (target != null && hit.collider.isTrigger) // Should be the detection sphere
                        {
                            // IGNORE AI RAGDOLLS: Only detect the actual player, not AI ragdolls
                            ActiveRagdoll.RagdollAIController aiController = target.GetComponent<ActiveRagdoll.RagdollAIController>();
                            if (aiController != null)
                            {
                                // This is an AI ragdoll, not the player - skip it
                                continue;
                            }
                            // Simple check: is there a wall directly blocking the view?
                            // Cast from mimic to player center, ignoring triggers
                            Vector3 playerCenter = target.transform.position;
                            Vector3 directionToPlayer = (playerCenter - origin).normalized;
                            float distanceToPlayer = Vector3.Distance(origin, playerCenter);
                            
                            bool wallBlocking = false;
                            RaycastHit wallCheck;
                            if (Physics.Raycast(origin, directionToPlayer, out wallCheck, distanceToPlayer - 0.5f, -1, QueryTriggerInteraction.Ignore))
                            {
                                // Something solid between us - check if it's a wall (not ground)
                                bool isWall = Vector3.Dot(wallCheck.normal, Vector3.up) < 0.7f;
                                if (isWall)
                                {
                                    wallBlocking = true;
                                    if (debugDetection)
                                    {
                                    }
                                }
                            }
                            
                            if (!wallBlocking)
                            {
                                // DETECTED!
                                playerDetectionRayCount++;
                                detectedTarget = target;
                                playerPositionSum += hit.point;
                                
                                if (debugDetection)
                                {
                                }
                            }
                            
                            break; // Found target component, done checking this ray
                        }
                    }
                }
                else
                {
                    // No hit means maximum open space
                    rayDistances[i] = raycastDistance;
                    environmentSnapshot[i] = origin + direction * raycastDistance;
                }

                // Debug visualization will be set after we know if player was detected
                // Store for now, will draw all rays after detection check
            }
            
            // ======= STATE MACHINE LOGIC =======
            bool playerDetectedThisFrame = (detectedTarget != null) && (playerDetectionRayCount >= minRaysToDetectPlayer);
            
            // RETREAT MODE: Ignore player detection entirely
            if (retreatMode)
            {
                playerDetectedThisFrame = false;
            }
            
            // Visualize all rays based on current state
            // BUG FIX: Check isChasing to prevent blue flash when transitioning to cooldown
            Color rayColor;
            if (retreatMode)
                rayColor = new Color(0.5f, 0f, 1f); // Magenta/Purple - retreating from kill
            else if (isInContactProximity)
                rayColor = Color.white; // Contact proximity - locked on even without ray detection
            else if (isProximityHoming)
                rayColor = Color.yellow; // Proximity homing - intense close-range pursuit
            else if (playerDetectedThisFrame || isChasing)
                rayColor = Color.red; // Player detected or hunting
            else if (isCooldown)
                rayColor = new Color(1f, 0.5f, 0f); // Cooldown searching
            else
                rayColor = Color.cyan; // Normal exploration
                
            for (int i = 0; i < totalRays; i++)
            {
                Debug.DrawRay(origin, rayDirections[i] * rayDistances[i], rayColor, 2f);
            }
            
            // === PLAYER DETECTED → HUNT MODE ===
            if (playerDetectedThisFrame)
            {
                // Any mode → Hunt mode
                if (isCooldown)
                {
                    isCooldown = false;
                    cooldownSnapshotCount = 0;
                }
                
                if (!isChasing)
                {
                }
                
                isChasing = true;
                isInContactProximity = false; // Reset contact proximity flag (rays are detecting normally)
                detectedPlayer = detectedTarget.gameObject;
                
                // Clear exploration memory
                if (visitedPositions != null)
                {
                    visitedPositions.Clear();
                }
                
                // Set target to ground beneath player
                Vector3 playerPos = detectedTarget.transform.position;
                RaycastHit playerGroundCheck;
                if (Physics.Raycast(playerPos + Vector3.up * 5f, -Vector3.up, out playerGroundCheck, 100f, -1, QueryTriggerInteraction.Ignore))
                {
                    currentTarget = new Vector3(playerPos.x, playerGroundCheck.point.y, playerPos.z);
                }
                else
                {
                    currentTarget = new Vector3(playerPos.x, origin.y, playerPos.z);
                }
                
                // BOUNDARY: Clamp target to stay within bounds (even when chasing player)
                currentTarget = ClampToBoundary(currentTarget);
                
                hasTarget = true;
                
                Debug.DrawLine(origin, currentTarget, Color.red, 2f);
                Debug.DrawRay(currentTarget, Vector3.up * 3f, Color.red, 2f);
                
                return; // Skip exploration logic
            }
            
            // === NO PLAYER DETECTED ===
            // Hunt mode → Cooldown mode (but check homing/contact proximity first)
            // IMPORTANT: Skip this logic if we're in RETREAT mode (retreat overrides everything)
            if (isChasing && !playerDetectedThisFrame && !isRetreating)
            {
                // HOMING MODE: Never lose player while in homing mode (distance-based, not ray-based)
                if (isProximityHoming)
                {
                    // Don't exit hunt mode - homing mode handles tracking in Update()
                    return;
                }
                
                // CONTACT PROXIMITY: If very close to player, don't lose them even if rays miss
                isInContactProximity = false;
                if (detectedPlayer != null)
                {
                    float distanceToPlayer = Vector3.Distance(origin, detectedPlayer.transform.position);
                    if (distanceToPlayer <= contactProximityDistance)
                    {
                        isInContactProximity = true;
                        
                        // Keep hunting - update target to player's current position
                        Vector3 playerPos = detectedPlayer.transform.position;
                        RaycastHit playerGroundCheck;
                        if (Physics.Raycast(playerPos + Vector3.up * 5f, -Vector3.up, out playerGroundCheck, 100f, -1, QueryTriggerInteraction.Ignore))
                        {
                            currentTarget = new Vector3(playerPos.x, playerGroundCheck.point.y, playerPos.z);
                        }
                        else
                        {
                            currentTarget = new Vector3(playerPos.x, origin.y, playerPos.z);
                        }
                        currentTarget = ClampToBoundary(currentTarget);
                        hasTarget = true;
                        return; // Stay in hunt mode
                    }
                }
                
                // Not in homing or contact proximity - lose the player
                if (!isInContactProximity)
                {
                    isChasing = false;
                    isCooldown = true;
                    cooldownSearchCenter = transform.position;
                    cooldownSearchedDirections.Clear(); // Reset for new cooldown session
                    detectedPlayer = null;
                }
            }
            
            // Cooldown mode → Continue cooldown (already handled in Update())
            // IMPORTANT: Skip this if we're in RETREAT mode
            if (isCooldown && !playerDetectedThisFrame && !isRetreating)
            {
                // Cooldown exit to Explore is handled in Update() when count reaches 5
            }
            
            // RETREAT MODE: Override all other states and just flee
            if (isRetreating)
            {
                // Skip all the exploration/hunt logic below and let the retreat target selection handle it
                // This is handled at the end of this function
            }
            
            // If we reach here, we're in EXPLORE, COOLDOWN, or RETREAT mode and need to find a direction

            // RETREAT MODE: Skip forward bias and memory checks (just flee)
            // COOLDOWN MODE: Different search strategy with smaller radius
            float searchRadius = cooldownMode ? cooldownRadius : raycastDistance;
            
            // Find the best ray group (most open space) with forward bias and exploration preference
            float bestGroupScore = 0f;
            int bestGroupIndex = 0;
            
            // Track all group scores for potential suboptimal selection (EXPLORE mode only)
            List<KeyValuePair<int, float>> groupScores = new List<KeyValuePair<int, float>>();

            for (int group = 0; group < numberOfGroups; group++)
            {
                // Calculate middle ray direction for this group
                int middleRayIndex = group * raysPerGroup + (raysPerGroup / 2);
                Vector3 groupDirection = rayDirections[middleRayIndex];
                
                // DIRECTIONAL BIAS: Apply score multiplier to forward-facing groups
                // This gives forward movement a soft preference without hard-blocking backward options
                float directionBiasMultiplier = 1f;
                if (forwardBiasEnabled && !emergencyMode && !cooldownMode && !retreatMode)
                {
                    float alignment = Vector3.Dot(groupDirection, currentMovementDirection);
                    
                    if (alignment >= 0)
                    {
                        // Forward-facing group - apply bonus multiplier
                        directionBiasMultiplier = forwardBiasMultiplier;
                    }
                    else if (alignment < -0.7f)
                    {
                        // Very backward-facing (>135°) - apply penalty
                        directionBiasMultiplier = 0.5f; // 50% penalty
                    }
                    // Side-facing groups (alignment -0.7 to 0) get no modifier
                }
                
                float groupTotalDistance = 0f;

                // Sum up distances for this group (clamped to search radius in cooldown mode)
                for (int i = 0; i < raysPerGroup; i++)
                {
                    int rayIndex = (group * raysPerGroup + i) % totalRays;
                    float distance = cooldownMode ? Mathf.Min(rayDistances[rayIndex], searchRadius) : rayDistances[rayIndex];
                    groupTotalDistance += distance;
                }

                // Calculate candidate target position for this group
                Vector3 candidatePosition = CalculateTargetFromGroup(origin, group, rayDistances, rayDirections, cooldownMode ? searchRadius : -1f);
                
                // BOUNDARY CHECK: Penalize targets outside the boundary
                float boundaryPenalty = 1f;
                if (enableBoundary && !IsWithinBoundary(candidatePosition))
                {
                    boundaryPenalty = 0.1f; // Heavily penalize out-of-bounds targets
                }
                
                // RETREAT MODE: Heavily prioritize distance FROM kill location AND avoid backtracking
                float retreatScore = 1f;
                if (retreatMode)
                {
                    // Calculate distance from kill location
                    float distanceFromKillLocation = Vector3.Distance(
                        new Vector3(candidatePosition.x, 0, candidatePosition.z),
                        new Vector3(retreatFromPosition.x, 0, retreatFromPosition.z)
                    );
                    
                    // Heavily boost score based on distance (further = MUCH better)
                    // Use exponential scaling to strongly favor furthest points
                    retreatScore = 1f + (distanceFromKillLocation * 2f); // Double weight for distance
                    
                    // ANTI-BACKTRACKING: Penalize targets near previous retreat waypoints
                    if (retreatWaypoints != null && retreatWaypoints.Count > 0)
                    {
                        // Find closest retreat waypoint to this candidate position
                        float closestWaypointDistance = float.MaxValue;
                        foreach (Vector3 waypoint in retreatWaypoints)
                        {
                            float distance = Vector3.Distance(
                                new Vector3(candidatePosition.x, 0, candidatePosition.z),
                                new Vector3(waypoint.x, 0, waypoint.z)
                            );
                            if (distance < closestWaypointDistance)
                            {
                                closestWaypointDistance = distance;
                            }
                        }
                        
                        // If candidate is near a previous waypoint, heavily penalize it
                        if (closestWaypointDistance < retreatWaypointRadius)
                        {
                            // Closer to waypoint = worse penalty
                            float penalty = closestWaypointDistance / retreatWaypointRadius; // 0 (very close) to 1 (at radius edge)
                            retreatScore *= penalty * 0.1f; // Reduce score to 10% or less
                        }
                    }
                }
                
                // COOLDOWN MODE: Prioritize unexplored directions within the local area
                float explorationScore = 1f;
                if (cooldownMode)
                {
                    // Check if this direction has been searched before in this cooldown session
                    bool alreadySearched = false;
                    foreach (Vector3 searchedDir in cooldownSearchedDirections)
                    {
                        float similarity = Vector3.Dot(groupDirection, searchedDir.normalized);
                        if (similarity > 0.85f) // Within ~30 degrees
                        {
                            alreadySearched = true;
                            break;
                        }
                    }
                    
                    // Heavily favor unsearched directions
                    if (alreadySearched)
                    {
                        explorationScore = 0.2f; // Reduce score for already-searched directions
                    }
                    else
                    {
                        explorationScore = 2.0f; // Boost score for new directions
                    }
                    
                    // Also check if the target would take us outside the cooldown radius
                    float distanceFromCenter = Vector3.Distance(new Vector3(candidatePosition.x, 0, candidatePosition.z), 
                                                                  new Vector3(cooldownSearchCenter.x, 0, cooldownSearchCenter.z));
                    if (distanceFromCenter > cooldownRadius)
                    {
                        explorationScore *= 0.3f; // Penalize targets outside the search area
                    }
                }
                // EXPLORE MODE: Check if this target leads to unexplored territory
                else if (memoryEnabled && !emergencyMode && !retreatMode)
                {
                    explorationScore = CalculateExplorationScore(candidatePosition);
                }
                
                // Combine distance score with all modifiers
                float finalScore = groupTotalDistance * explorationScore * boundaryPenalty * retreatScore * directionBiasMultiplier;

                // Store this group's score (for potential suboptimal selection in EXPLORE mode)
                if (!cooldownMode && !retreatMode && !isChasing)
                {
                    groupScores.Add(new KeyValuePair<int, float>(group, finalScore));
                }

                // Check if this is the best group so far
                if (finalScore > bestGroupScore)
                {
                    bestGroupScore = finalScore;
                    bestGroupIndex = group;
                }
            }
            
            // EXPLORE MODE ONLY: Random chance to choose a middling path instead of optimal
            if (!emergencyMode && !cooldownMode && !retreatMode && !isChasing && groupScores.Count > 3)
            {
                float randomRoll = Random.value; // 0.0 to 1.0
                if (randomRoll < suboptimalPathChance)
                {
                    // Sort groups by score (descending)
                    groupScores.Sort((a, b) => b.Value.CompareTo(a.Value));
                    
                    // Choose a group from the middle third (not best, not worst)
                    int middleStart = groupScores.Count / 3;
                    int middleEnd = (groupScores.Count * 2) / 3;
                    int middleRangeSize = middleEnd - middleStart;
                    
                    if (middleRangeSize > 0)
                    {
                        int randomMiddleIndex = middleStart + Random.Range(0, middleRangeSize);
                        bestGroupIndex = groupScores[randomMiddleIndex].Key;
                        bestGroupScore = groupScores[randomMiddleIndex].Value;
                        
                    }
                }
            }

            // CALCULATE TARGET POSITION: Average position of the best ray group
            Vector3 candidateTarget = CalculateTargetFromGroup(origin, bestGroupIndex, rayDistances, rayDirections, cooldownMode ? searchRadius : -1f);
            
            // COOLDOWN MODE: Track this direction as searched
            if (cooldownMode)
            {
                int middleRayIndex = bestGroupIndex * raysPerGroup + (raysPerGroup / 2);
                Vector3 searchedDirection = rayDirections[middleRayIndex];
                cooldownSearchedDirections.Add(searchedDirection);
            }
            
            // AVOID PREVIOUS TARGET: Check if candidate is too close to previous target
            if (!emergencyMode && Vector3.Distance(candidateTarget, previousTarget) < minDistanceFromPreviousTarget)
            {
                // Try to find alternative target that's not near previous
                bool foundAlternative = false;
                float secondBestDistance = 0f;
                int secondBestIndex = 0;
                
                for (int group = 0; group < numberOfGroups; group++)
                {
                    if (group == bestGroupIndex) continue; // Skip the one we already found
                    
                    // Apply directional bias (same as above)
                    float altDirectionBias = 1f;
                    int middleRayIndex = group * raysPerGroup + (raysPerGroup / 2);
                    Vector3 groupDirection = rayDirections[middleRayIndex];
                    if (forwardBiasEnabled && !cooldownMode && !retreatMode)
                    {
                        float alignment = Vector3.Dot(groupDirection, currentMovementDirection);
                        if (alignment >= 0)
                            altDirectionBias = forwardBiasMultiplier;
                        else if (alignment < -0.7f)
                            altDirectionBias = 0.5f;
                    }
                    
                    float groupTotalDistance = 0f;
                    for (int i = 0; i < raysPerGroup; i++)
                    {
                        int rayIndex = (group * raysPerGroup + i) % totalRays;
                        groupTotalDistance += rayDistances[rayIndex];
                    }
                    
                    // Apply directional bias to alternative score
                    float altScore = groupTotalDistance * altDirectionBias;
                    
                    // Calculate this group's target position
                    Vector3 altTarget = CalculateTargetFromGroup(origin, group, rayDistances, rayDirections, cooldownMode ? searchRadius : -1f);
                    
                    // Check if far enough from previous target
                    if (Vector3.Distance(altTarget, previousTarget) >= minDistanceFromPreviousTarget)
                    {
                        if (altScore > secondBestDistance)
                        {
                            secondBestDistance = altScore;
                            secondBestIndex = group;
                            candidateTarget = altTarget;
                            foundAlternative = true;
                        }
                    }
                }
                
                if (foundAlternative)
                {
                }
            }

            // Store current target as previous before setting new one
            if (hasTarget)
                previousTarget = currentTarget;

            // BOUNDARY: Clamp target to stay within bounds
            candidateTarget = ClampToBoundary(candidateTarget);

            // Set the new target
            currentTarget = candidateTarget;
            hasTarget = true;

            // Visualize the target
            Debug.DrawLine(origin, currentTarget, Color.cyan, 2f);
            Debug.DrawRay(currentTarget, Vector3.up * 2f, Color.magenta, 2f);
            
            // Draw sphere at target location
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                Vector3 dir1 = Quaternion.Euler(0, angle, 0) * Vector3.forward * 0.5f;
                Vector3 dir2 = Quaternion.Euler(0, angle + 22.5f, 0) * Vector3.forward * 0.5f;
                Debug.DrawLine(currentTarget + dir1, currentTarget + dir2, Color.magenta, 2f);
            }

            float distanceToTarget = Vector3.Distance(origin, currentTarget);
            string modeLabel = emergencyMode ? "[EMERGENCY 360°]" : "[EXPLORE]";
            
            // Calculate exploration info
            string explorationInfo = "";
            if (memoryEnabled && !emergencyMode)
            {
                float explorationScore = CalculateExplorationScore(currentTarget);
                explorationInfo = $" | Novelty: {explorationScore:P0} ({visitedPositions.Count}/{memorySize})";
            }
            
        }
        
        /// <summary>
        /// Helper method to calculate target position from a ray group
        /// </summary>
        /// <param name="maxDistance">Optional max distance to clamp rays (for cooldown mode). -1 = no limit.</param>
        Vector3 CalculateTargetFromGroup(Vector3 origin, int groupIndex, float[] rayDistances, Vector3[] rayDirections, float maxDistance = -1f)
        {
            Vector3 targetPosition = Vector3.zero;
            int raysInGroup = 0;

            for (int i = 0; i < raysPerGroup; i++)
            {
                int rayIndex = (groupIndex * raysPerGroup + i) % totalRays;
                
                // Clamp distance if maxDistance is specified (cooldown mode)
                float effectiveDistance = rayDistances[rayIndex];
                if (maxDistance > 0f)
                {
                    effectiveDistance = Mathf.Min(effectiveDistance, maxDistance);
                }
                
                // Calculate point along this ray at targetDepthRatio distance
                float targetDistance = effectiveDistance * targetDepthRatio;
                Vector3 pointAlongRay = origin + rayDirections[rayIndex] * targetDistance;
                
                targetPosition += pointAlongRay;
                raysInGroup++;
            }

            // Average the positions to get center of open space
            targetPosition /= raysInGroup;
            targetPosition.y = origin.y; // Keep target at same height
            
            return targetPosition;
        }

        /// <summary>
        /// Transforms a world position to the boundary's local space
        /// </summary>
        Vector3 WorldToLocalBoundary(Vector3 worldPosition)
        {
            // Translate to boundary center
            Vector3 relative = worldPosition - boundaryCenter;
            
            // Apply inverse rotation
            Quaternion rotation = Quaternion.Euler(boundaryRotation);
            Vector3 localPosition = Quaternion.Inverse(rotation) * relative;
            
            return localPosition;
        }
        
        /// <summary>
        /// Transforms a local position to world space
        /// </summary>
        Vector3 LocalToWorldBoundary(Vector3 localPosition)
        {
            // Apply rotation
            Quaternion rotation = Quaternion.Euler(boundaryRotation);
            Vector3 rotated = rotation * localPosition;
            
            // Translate from boundary center
            return rotated + boundaryCenter;
        }
        
        /// <summary>
        /// Clamps a position to stay within the boundary box
        /// </summary>
        Vector3 ClampToBoundary(Vector3 position)
        {
            if (!enableBoundary)
                return position;
            
            // Transform to boundary's local space
            Vector3 localPos = WorldToLocalBoundary(position);
            
            // Clamp to box bounds in local space
            Vector3 halfScale = boundaryScale * 0.5f;
            localPos.x = Mathf.Clamp(localPos.x, -halfScale.x, halfScale.x);
            localPos.y = Mathf.Clamp(localPos.y, -halfScale.y, halfScale.y);
            localPos.z = Mathf.Clamp(localPos.z, -halfScale.z, halfScale.z);
            
            // Transform back to world space
            return LocalToWorldBoundary(localPos);
        }
        
        /// <summary>
        /// Checks if a position is within the boundary box
        /// </summary>
        bool IsWithinBoundary(Vector3 position)
        {
            if (!enableBoundary)
                return true;
            
            // Transform to boundary's local space
            Vector3 localPos = WorldToLocalBoundary(position);
            
            // Check if within box bounds in local space
            Vector3 halfScale = boundaryScale * 0.5f;
            
            return Mathf.Abs(localPos.x) <= halfScale.x &&
                   Mathf.Abs(localPos.y) <= halfScale.y &&
                   Mathf.Abs(localPos.z) <= halfScale.z;
        }
        
        /// <summary>
        /// Calculates exploration score for a candidate position
        /// Returns higher scores for unexplored areas, lower for visited areas
        /// </summary>
        float CalculateExplorationScore(Vector3 candidatePosition)
        {
            if (visitedPositions.Count == 0)
                return 1f; // No history yet, full score

            // Find the closest visited position
            float closestDistance = float.MaxValue;
            foreach (Vector3 visitedPos in visitedPositions)
            {
                float distance = Vector3.Distance(
                    new Vector3(candidatePosition.x, 0, candidatePosition.z),
                    new Vector3(visitedPos.x, 0, visitedPos.z)
                );
                if (distance < closestDistance)
                    closestDistance = distance;
            }

            // Calculate score based on distance from visited areas
            // If within exploredRadius: reduce score
            // If far from all visited areas: full score
            if (closestDistance >= exploredRadius)
            {
                return 1f; // Fully unexplored
            }
            else
            {
                // Gradient: from explorationBias (0-1) at distance 0, to 1.0 at exploredRadius
                float normalizedDistance = closestDistance / exploredRadius;
                return Mathf.Lerp(explorationBias, 1f, normalizedDistance);
            }
        }

        // Visualize spatial awareness in Scene view
        private void OnDrawGizmos()
        {
            // ===== BOUNDARY VISUALIZATION (always show, even when not playing) =====
            if (enableBoundary)
            {
                Vector3 halfScale = boundaryScale * 0.5f;
                Quaternion rotation = Quaternion.Euler(boundaryRotation);
                
                // Define 8 corners of the box in local space
                Vector3[] localCorners = new Vector3[8]
                {
                    new Vector3(-halfScale.x, -halfScale.y, -halfScale.z), // 0: bottom front left
                    new Vector3( halfScale.x, -halfScale.y, -halfScale.z), // 1: bottom front right
                    new Vector3( halfScale.x, -halfScale.y,  halfScale.z), // 2: bottom back right
                    new Vector3(-halfScale.x, -halfScale.y,  halfScale.z), // 3: bottom back left
                    new Vector3(-halfScale.x,  halfScale.y, -halfScale.z), // 4: top front left
                    new Vector3( halfScale.x,  halfScale.y, -halfScale.z), // 5: top front right
                    new Vector3( halfScale.x,  halfScale.y,  halfScale.z), // 6: top back right
                    new Vector3(-halfScale.x,  halfScale.y,  halfScale.z)  // 7: top back left
                };
                
                // Transform corners to world space
                Vector3[] worldCorners = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    worldCorners[i] = LocalToWorldBoundary(localCorners[i]);
                }
                
                // Draw bottom face
                Gizmos.color = new Color(1f, 0f, 1f, 0.8f); // Magenta
                Gizmos.DrawLine(worldCorners[0], worldCorners[1]);
                Gizmos.DrawLine(worldCorners[1], worldCorners[2]);
                Gizmos.DrawLine(worldCorners[2], worldCorners[3]);
                Gizmos.DrawLine(worldCorners[3], worldCorners[0]);
                
                // Draw top face
                Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
                Gizmos.DrawLine(worldCorners[4], worldCorners[5]);
                Gizmos.DrawLine(worldCorners[5], worldCorners[6]);
                Gizmos.DrawLine(worldCorners[6], worldCorners[7]);
                Gizmos.DrawLine(worldCorners[7], worldCorners[4]);
                
                // Draw vertical edges
                Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
                Gizmos.DrawLine(worldCorners[0], worldCorners[4]);
                Gizmos.DrawLine(worldCorners[1], worldCorners[5]);
                Gizmos.DrawLine(worldCorners[2], worldCorners[6]);
                Gizmos.DrawLine(worldCorners[3], worldCorners[7]);
                
                // Draw center marker
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(boundaryCenter, 0.5f);
                
                // Draw axis indicators at center
                float axisLength = Mathf.Min(halfScale.x, halfScale.y, halfScale.z) * 0.3f;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(boundaryCenter, boundaryCenter + rotation * Vector3.right * axisLength);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(boundaryCenter, boundaryCenter + rotation * Vector3.up * axisLength);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(boundaryCenter, boundaryCenter + rotation * Vector3.forward * axisLength);
            }
            
            if (!Application.isPlaying) return;

            // ===== STATE VISUALIZATION =====
            // Draw detection radius colored by mode
            if (isRetreating)
            {
                // RETREAT MODE: Purple/Magenta
                Gizmos.color = new Color(0.5f, 0f, 1f);
                Gizmos.DrawWireSphere(transform.position, raycastDistance);
                
                // Purple sphere around mimic
                Gizmos.color = new Color(0.5f, 0f, 1f, 0.2f);
                Gizmos.DrawSphere(transform.position, 2f);
                
                // Draw line FROM kill location (showing retreat direction)
                Gizmos.color = new Color(1f, 0f, 1f, 0.8f);
                Gizmos.DrawLine(retreatFromPosition, transform.position);
                
                // Mark kill location with X
                Gizmos.color = Color.red;
                float crossSize = 0.5f;
                Gizmos.DrawLine(retreatFromPosition + Vector3.left * crossSize, retreatFromPosition + Vector3.right * crossSize);
                Gizmos.DrawLine(retreatFromPosition + Vector3.forward * crossSize, retreatFromPosition + Vector3.back * crossSize);
                Gizmos.DrawLine(retreatFromPosition + Vector3.up * crossSize, retreatFromPosition + Vector3.down * crossSize);
                Gizmos.DrawWireSphere(retreatFromPosition, 0.5f);
                
                // Retreat timer progress bar
                float retreatProgress = (Time.time - retreatStartTime) / retreatDuration;
                Vector3 barStart = transform.position + Vector3.up * 4f;
                Vector3 barEnd = barStart + Vector3.right * 3f;
                Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
                Gizmos.DrawLine(barStart, barEnd);
                Gizmos.color = new Color(0.5f, 0f, 1f, 0.8f);
                Gizmos.DrawLine(barStart, Vector3.Lerp(barStart, barEnd, retreatProgress));
                
                // Draw retreat waypoints (breadcrumb trail showing where Mimic has fled)
                if (retreatWaypoints != null && retreatWaypoints.Count > 0)
                {
                    for (int i = 0; i < retreatWaypoints.Count; i++)
                    {
                        // Fade older waypoints
                        float age = (float)i / retreatWaypoints.Count;
                        Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f + age * 0.5f);
                        Gizmos.DrawWireSphere(retreatWaypoints[i], retreatWaypointRadius);
                        Gizmos.DrawSphere(retreatWaypoints[i], 0.3f);
                        
                        // Draw line between waypoints to show path
                        if (i > 0)
                        {
                            Gizmos.color = new Color(0.5f, 0f, 1f, 0.6f);
                            Gizmos.DrawLine(retreatWaypoints[i - 1], retreatWaypoints[i]);
                        }
                    }
                }
            }
            else if (isChasing)
            {
                // CONTACT PROXIMITY: White when locked on without ray detection
                if (isInContactProximity)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(transform.position, raycastDistance);
                    
                    // Pulsing white sphere (very close - locked on)
                    Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
                    Gizmos.DrawSphere(transform.position, 2.5f);
                    
                    // Draw contact proximity boundary
                    Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
                    Gizmos.DrawWireSphere(transform.position, contactProximityDistance);
                    
                    // Line to player
                    if (detectedPlayer != null)
                    {
                        Gizmos.color = Color.white;
                        Gizmos.DrawLine(transform.position, detectedPlayer.transform.position);
                        
                        // Draw lock-on target on player
                        Vector3 playerPos = detectedPlayer.transform.position;
                        float lockSize = 0.8f;
                        Gizmos.DrawWireSphere(playerPos, lockSize);
                        Gizmos.DrawWireSphere(playerPos, lockSize * 0.5f);
                    }
                }
                // HOMING MODE: Yellow when very close
                else if (isProximityHoming)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(transform.position, raycastDistance);
                    
                    // Pulsing yellow sphere
                    Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
                    Gizmos.DrawSphere(transform.position, 2f);
                    
                    // Line to player
                    if (detectedPlayer != null)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(transform.position, detectedPlayer.transform.position);
                        
                        // Draw crosshair on player
                        Vector3 playerPos = detectedPlayer.transform.position;
                        float crossSize = 0.5f;
                        Gizmos.DrawLine(playerPos + Vector3.left * crossSize, playerPos + Vector3.right * crossSize);
                        Gizmos.DrawLine(playerPos + Vector3.forward * crossSize, playerPos + Vector3.back * crossSize);
                        Gizmos.DrawLine(playerPos + Vector3.up * crossSize, playerPos + Vector3.down * crossSize);
                    }
                }
                else
                {
                    // NORMAL HUNT MODE: Red
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(transform.position, raycastDistance);
                    
                    // Red sphere around mimic
                    Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                    Gizmos.DrawSphere(transform.position, 2f);
                    
                    // Line to player if detected
                    if (detectedPlayer != null)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawLine(transform.position, detectedPlayer.transform.position);
                    }
                }
            }
            else if (isCooldown)
            {
                // COOLDOWN MODE: Show the smaller search radius
                Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
                Gizmos.DrawWireSphere(transform.position, cooldownRadius);
                
                // Orange sphere around mimic
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Gizmos.DrawSphere(transform.position, 2f);
                
                // Search area boundary at cooldown center
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
                Gizmos.DrawWireSphere(cooldownSearchCenter, cooldownRadius);
                
                // Cooldown progress (5 dots)
                for (int i = 0; i < 5; i++)
                {
                    Gizmos.color = i < cooldownSnapshotCount ? new Color(1f, 0.5f, 0f, 0.8f) : new Color(1f, 0.5f, 0f, 0.2f);
                    Vector3 dotPos = transform.position + Vector3.up * 4f + Vector3.right * (i - 2) * 0.5f;
                    Gizmos.DrawSphere(dotPos, 0.15f);
                }
                
                // Visualize searched directions during cooldown
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
                foreach (Vector3 searchedDir in cooldownSearchedDirections)
                {
                    Vector3 endpoint = transform.position + searchedDir * cooldownRadius * 0.5f;
                    Gizmos.DrawLine(transform.position, endpoint);
                    Gizmos.DrawSphere(endpoint, 0.3f);
                }
            }
            else
            {
                // EXPLORE MODE: Cyan
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, raycastDistance);
            }
            
            // Draw current target
            if (hasTarget)
            {
                if (isChasing)
                    Gizmos.color = Color.red;
                else if (isCooldown)
                    Gizmos.color = new Color(1f, 0.5f, 0f);
                else
                    Gizmos.color = Color.magenta;
                    
                Gizmos.DrawWireSphere(currentTarget, 0.5f);
                Gizmos.DrawLine(transform.position, currentTarget);
            }
            
            // EXPLORE MODE: Show memory trail
            if (!isChasing && !isCooldown && memoryEnabled && visitedPositions != null && visitedPositions.Count > 0)
            {
                for (int i = 0; i < visitedPositions.Count; i++)
                {
                    float age = (float)i / visitedPositions.Count;
                    Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.2f + age * 0.4f);
                    Gizmos.DrawSphere(visitedPositions[i], 0.15f);
                }
            }
            
            // Draw emergency mode indicator
            if (stuckTimer >= emergencyBacktrackTime)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 1.5f);
            }
        }
        
        /// <summary>
        /// Validates settings and provides helpful warnings
        /// </summary>
        private void OnValidate()
        {
            // Ensure totalRays is divisible by raysPerGroup
            if (totalRays % raysPerGroup != 0)
            {
// Debug.LogWarning($"[Movement] totalRays ({totalRays}) must be divisible by raysPerGroup ({raysPerGroup}). Adjusting...", this);
                totalRays = (totalRays / raysPerGroup) * raysPerGroup;
            }
        }
    }

}