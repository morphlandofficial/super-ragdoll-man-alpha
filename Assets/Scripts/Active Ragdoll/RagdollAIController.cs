using System.Collections.Generic;
using UnityEngine;

namespace ActiveRagdoll
{
    /// <summary>
    /// AI controller for ActiveRagdoll - uses Mimic-style navigation but feeds input to the ragdoll system.
    /// Reuses all the physics, movement, and animation systems from DefaultBehaviour.
    /// </summary>
    [RequireComponent(typeof(ActiveRagdoll))]
    [RequireComponent(typeof(InputModule))]
    public class RagdollAIController : MonoBehaviour
    {
        public enum AIMode
        {
            Idle,               // Stands still (optionally looks around)
            Roam,               // Wanders within boundary box
            Explore,            // Uses raycast pathfinding to explore
            Homing,             // Tracks and follows the player
            PathMovement,       // Navigates from spawn point to a user-set destination point
            ExploreThenAttack,  // Explores until player detected, then homes in
            RoamThenAttack,     // Roams until player detected, then homes in
            IdleThenAttack,     // Idles (with optional looking around), attacks when player spotted, returns after grab
            RaceToFinish,       // Automatically finds and races to the level finish flag
            RedLightGreenLight  // Squid Game mode - race to finish but stop during red lights
        }
        
        [Header("AI Mode")]
        [Tooltip("Current behavior mode of this AI")]
        public AIMode currentMode = AIMode.Explore;
        
        [Header("References")]
        [SerializeField] private ActiveRagdoll _activeRagdoll;
        [SerializeField] private InputModule _inputModule;
        private AnimationModule _animationModule;
        
        [Header("Audio")]
        private AudioClip _playerDetectionSound;
        private AudioSource _audioSource;
        private bool _hasPlayedDetectionSound = false; // Track if we've played sound for current detection
        private float _lastDetectionSoundTime = -999f; // Time when detection sound was last played
        private const float DETECTION_SOUND_COOLDOWN = 4f; // Cooldown between detection sounds (seconds)
        
        // Initialization delay to allow AIBoundaryBox to register AIs first
        private float _initializationTime = 0f;
        private const float INITIALIZATION_DELAY = 0.2f; // Wait 0.2 seconds before starting detection
        
        // PERFORMANCE: Raycast cooldown to prevent scanning every frame
        private float _lastRaycastTime = -999f;
        private const float RAYCAST_COOLDOWN = 0.25f; // Only raycast every 0.25 seconds (4x per second instead of 60x)
        
        // Simple player reference (updated each frame if needed)
        private Transform _currentPlayerInZone;
        
        [Header("AI Navigation")]
        [Tooltip("Height above ground for raycasting origin")]
        public float raycastHeight = 0.8f;
        [Tooltip("Number of rays to cast in a circle (360 degrees for Explore mode)")]
        public int totalRays = 15; // PERFORMANCE: Reduced from 30 to 15
        [Tooltip("Number of rays to group together")]
        public int raysPerGroup = 3;
        [Tooltip("Distance to detect obstacles")]
        public float raycastDistance = 8f;
        [Tooltip("Distance from target before choosing new target")]
        public float targetReachedDistance = 2f;
        [Tooltip("How far to sample into open space for target point")]
        [Range(0.3f, 1f)]
        public float targetDepthRatio = 0.7f;
        
        [Header("Movement")]
        [Tooltip("Whether AI should run (vs walk)")]
        public bool alwaysRun = true;
        [Tooltip("If alwaysRun is false, allow running only during chase/attack sequences")]
        [HideInInspector]
        public bool runOnlyWhenChasing = false;
        [Tooltip("Enable forward directional bias")]
        public bool forwardBiasEnabled = true;
        [Tooltip("Score multiplier for forward-facing targets")]
        [Range(1.0f, 2.0f)]
        public float forwardBiasMultiplier = 1.3f;
        
        [Header("Memory")]
        [Tooltip("Enable memory system to avoid revisiting explored areas")]
        public bool memoryEnabled = true;
        [Tooltip("Number of previous positions to remember")]
        public int memorySize = 20;
        [Tooltip("Radius around visited positions to consider as explored")]
        public float exploredRadius = 3f;
        [Tooltip("How strongly to avoid explored areas (higher = stronger avoidance)")]
        [Range(0f, 1f)]
        public float explorationBias = 0.7f;
        
        [Header("Exploration Randomness")]
        [Tooltip("Chance to choose a middling path instead of optimal")]
        [Range(0f, 0.2f)]
        public float suboptimalPathChance = 0.05f;
        
        [Header("Idle Mode Settings")]
        [Tooltip("SIMPLE: Gizmo radius around spawn point - ragdoll wanders in this circle, never leaves")]
        public float idlePaceRadius = 2.5f;
        [Tooltip("How long to pause at each spot while pacing (seconds)")]
        public float idleWaitTime = 2f;
        [Tooltip("Vision cone angle for idle detection (degrees) - only used in Idle Then Attack mode")]
        public float idleVisionConeAngle = 90f;
        [Tooltip("Can jump over gaps during idle mode")]
        [HideInInspector]
        public bool canJumpInIdleMode = false;
        
        [Header("Roam Mode Settings")]
        [Tooltip("How long to wait at each roam point (seconds)")]
        public float roamWaitTime = 3f;
        [Tooltip("Can jump over gaps during roam mode")]
        [HideInInspector]
        public bool canJumpInRoamMode = false;
        
        [Header("Explore Mode Settings")]
        [Tooltip("Explore mode ignores boundary box - can explore anywhere (controlled by spawner)")]
        [HideInInspector]
        public bool exploreIgnoresBoundary = false;
        [Tooltip("Can jump over gaps during explore mode")]
        [HideInInspector]
        public bool canJumpInExploreMode = false;
        
        [Header("Path Movement Settings")]
        [Tooltip("List of waypoints to navigate through in order")]
        [HideInInspector]
        public List<Vector3> pathWaypoints = new List<Vector3>();
        [Tooltip("Distance from waypoint to consider 'reached'")]
        [HideInInspector]
        public float pathReachedDistance = 1f;
        [Tooltip("After reaching final waypoint, return to Point A (spawn position) and stop")]
        [HideInInspector]
        public bool pathReturnToStartAndStop = false;
        [Tooltip("Loop through waypoints forever (overrides return to start)")]
        [HideInInspector]
        public bool pathLoopForever = false;
        [Tooltip("Number of cycles to complete before stopping (0 = infinite, ignored if Loop Forever is enabled)")]
        [HideInInspector]
        public int pathNumberOfCycles = 0;
        [Tooltip("After path completion, navigate to finish flag and trigger level complete")]
        [HideInInspector]
        public bool pathEndAtFinishTrigger = false;
        
        [Header("Path Then Attack Settings")]
        [Tooltip("Enable attack behavior during path movement (chase player if in 180° front vision)")]
        [HideInInspector]
        public bool pathThenAttackEnabled = false;
        [Tooltip("Detection radius for 180° front vision cone")]
        [HideInInspector]
        public float pathThenAttackDetectionRadius = 12f;
        [Tooltip("Lose radius - return to path when player exceeds this distance")]
        [HideInInspector]
        public float pathThenAttackLoseRadius = 15f;
        
        [Header("One Way Path Then Behavior Switch Settings")]
        [Tooltip("Complete path once, then permanently switch to a different AI mode")]
        [HideInInspector]
        public bool pathOneWayThenBehaviorSwitch = false;
        [Tooltip("AI mode to switch to after completing the path")]
        [HideInInspector]
        public AIMode pathFinalBehaviorMode = AIMode.Idle;
        
        // Internal tracking
        private int currentWaypointIndex = 1; // Start at 1 because 0 is spawn position
        private bool pathIsReturningToStart = false;
        private bool pathIsComplete = false;
        
        /// <summary>
        /// Get the current waypoint index (for respawn-at-last-point feature)
        /// </summary>
        public int GetCurrentWaypointIndex()
        {
            return currentWaypointIndex;
        }
        private int currentCycleCount = 0; // Track completed cycles
        private bool pathIsNavigatingToFinish = false; // Track if navigating to finish flag
        private Transform pathFinishFlagTransform; // Cached reference to finish flag (for path mode)
        private bool pathFinishFlagFound = false; // Track if we've found the finish flag
        
        // Path Then Attack state
        private bool pathThenAttackIsTracking = false; // Currently chasing player
        private int pathThenAttackSavedWaypointIndex = 1; // Waypoint to return to after chase
        
        // Return-to-path timeout system (kill AI if stuck trying to return after chase)
        private bool pathThenAttackReturningToPath = false; // Currently trying to return to path after chase
        private float timeAttemptingReturnToPath = 0f;
        private int returnTargetWaypointIndex = -1;
        private const float RETURN_TO_PATH_TIMEOUT_SECONDS = 10f;
        
        [Header("Race to Finish Settings")]
        private Transform finishFlagTransform; // Cached reference to the finish flag
        private bool finishFlagFound = false;
        
        [Header("Red Light Green Light Mode Settings")]
        [Tooltip("Reference to the Red Light Green Light Manager (auto-finds if null)")]
        [HideInInspector]
        public RedLightGreenLightManager redLightGreenLightManager;
        [Tooltip("Duration of anticipation stops (seconds)")]
        [HideInInspector]
        public float anticipationStopDuration = 2f;
        [Tooltip("Bell curve mean for anticipation timing")]
        [HideInInspector]
        public float anticipationMean = 10f;
        [Tooltip("Bell curve standard deviation for anticipation timing")]
        [HideInInspector]
        public float anticipationStdDev = 2.5f;
        [Tooltip("Minimum anticipation wait time")]
        [HideInInspector]
        public float anticipationMinTime = 0.5f;
        [Tooltip("Maximum anticipation wait time")]
        [HideInInspector]
        public float anticipationMaxTime = 15f;
        
        // Red Light Green Light internal state
        // private bool rlglIsMoving = false; // Unused
        private bool rlglIsInAnticipationStop = false;
        private float rlglAnticipationTimer = 0f;
        private float rlglNextAnticipationTime = 0f;
        private float rlglMovementStartTime = 0f;
        private bool rlglWasForcedRagdoll = false;
        private DefaultBehaviour _defaultBehaviour; // Reference to ragdoll control
        
        [Header("Homing Mode Settings")]
        [Tooltip("Always know player position vs detect via raycast")]
        public bool alwaysKnowPlayerPosition = true;
        [Tooltip("How close to get to player before stopping")]
        public float homingStopDistance = 0f;
        [Tooltip("Detection range for raycast-based player detection")]
        public float playerDetectionRange = 15f;
        [Tooltip("Try to grab player when in homing mode")]
        public bool grabPlayerInHomingMode = true;
        [Tooltip("Distance at which to extend arms forward")]
        public float armExtendDistance = 5f;
        
        [Header("Explore Then Attack Settings")]
        [Tooltip("PROXIMITY RADIUS: Distance to detect player (no line-of-sight check)")]
        public float exploreThenAttackDetectionRadius = 12f;
        [Tooltip("PROXIMITY RADIUS: Distance to lose player and return to exploring (should be > detection radius)")]
        public float exploreThenAttackLoseRadius = 15f;
        
        [Header("Roam Then Attack Settings")]
        [Tooltip("Detection radius for switching from Roam to Homing")]
        public float roamThenAttackDetectionRadius = 12f;
        [Tooltip("Radius to lose player and return to roaming (should be > detection radius)")]
        public float roamThenAttackLoseRadius = 15f;
        
        [Header("Idle Then Attack Settings")]
        [Tooltip("Detection range for raycast-based player detection")]
        public float idleThenAttackDetectionRange = 15f;
        [Tooltip("Distance from home point to consider returned")]
        public float idleThenAttackReturnDistance = 1f;
        
        [Header("Jump/Gap Detection")]
        [Tooltip("Enable automatic jumping over gaps")]
        public bool enableAutoJump = true;
        [Tooltip("How far ahead to check for gaps (meters)")]
        public float gapCheckDistance = 1.5f;
        [Tooltip("Maximum step height AI can walk over without jumping")]
        public float maxStepHeight = 0.5f;
        [Tooltip("Minimum gap depth to trigger jump")]
        public float minGapDepth = 1f;
        [Tooltip("Cooldown between jumps (seconds)")]
        public float jumpCooldown = 1f;
        
        [Header("Stuck Detection (for non-jumping AI)")]
        [Tooltip("AI will jump if stuck (only applies when enableAutoJump is false)")]
        [HideInInspector]
        public bool jumpIfStuck = false;
        [Tooltip("Time stuck before jumping (seconds)")]
        [HideInInspector]
        public float stuckTimeThreshold = 2f;
        
        // Boundary Box Reference (managed by AIBoundaryBox in scene)
        private AIBoundaryBox boundaryBox = null;
        
        // Internal state
        private Vector3 currentTarget;
        private bool hasTarget = false;
        private Vector3[] environmentSnapshot;
        private List<Vector3> visitedPositions;
        private Vector3 currentMovementDirection;
        private Vector3 spawnPosition;
        private float roamWaitTimer = 0f;
        private Transform playerTransform;
        private float lastJumpTime = -999f;
        
        // Stuck detection (for Explore mode and Jump If Stuck)
        private float stuckTimer = 0f;
        private bool isStuck = false;
        private Rigidbody _rigidbody;
        
        // Jump If Stuck tracking (separate from Explore mode stuck detection)
        private Vector3 lastPosition;
        private float jumpIfStuckTimer = 0f;
        private const float STUCK_MOVEMENT_THRESHOLD = 0.1f; // Minimum distance moved to not be stuck
        
        // Spawn Protection (prevent jumping immediately after spawn)
        private float spawnTime = 0f;
        private const float NO_JUMP_AFTER_SPAWN_DURATION = 2f; // Seconds after spawn where jumping is disabled
        
        // Idle mode state (pacing around small area)
        private float idleWaitTimer = 0f;
        
        // Explore Then Attack mode state
        private bool exploreThenAttackIsTracking = false;
        
        // Roam Then Attack mode state
        private bool roamThenAttackIsTracking = false;
        
        // Idle Then Attack mode state
        private enum IdleThenAttackState { Idle, Homing, Returning }
        private IdleThenAttackState idleThenAttackState = IdleThenAttackState.Idle;
        private Vector3 idleThenAttackStartPosition;
        private Quaternion idleThenAttackStartRotation;
        
        private void OnValidate()
        {
            if (_activeRagdoll == null) _activeRagdoll = GetComponent<ActiveRagdoll>();
            if (_inputModule == null) _inputModule = GetComponent<InputModule>();
        }
        
        /// <summary>
        /// Sets the target transform to chase in Homing mode.
        /// Tries to get PhysicalTorso if target is an ActiveRagdoll, otherwise uses root transform.
        /// </summary>
        private void SetTargetTransform(GameObject target)
        {
            if (target == null)
            {
                playerTransform = null;
                return;
            }
            
            // Try to get ActiveRagdoll component (works for both player and other ragdolls)
            ActiveRagdoll targetRagdoll = target.GetComponent<ActiveRagdoll>();
            if (targetRagdoll != null && targetRagdoll.PhysicalTorso != null)
            {
                // Track the physical torso (actual moving body)
                playerTransform = targetRagdoll.PhysicalTorso.transform;
            }
            else
            {
                // Fallback to root transform
                playerTransform = target.transform;
            }
        }
        
        /// <summary>
        /// Public method to change the target at runtime (called by spawner)
        /// </summary>
        public void SetTarget(GameObject newTarget)
        {
            SetTargetTransform(newTarget);
        }
        
        /// <summary>
        /// Sets the player detection sound (called by spawner)
        /// </summary>
        public void SetPlayerDetectionSound(AudioClip detectionSound)
        {
            _playerDetectionSound = detectionSound;
        }
        
        /// <summary>
        /// MULTIPLAYER: Find the closest active player to this AI.
        /// Used for "commit on detection" - AI picks closest player once and sticks with them.
        /// Called at spawn and when target is lost (respawn/death).
        /// </summary>
        private RespawnablePlayer FindClosestPlayer()
        {
            // Find all active players in the scene
            RespawnablePlayer[] allPlayers = FindObjectsByType<RespawnablePlayer>(FindObjectsSortMode.None);
            
            if (allPlayers.Length == 0)
            {
                // No players found
                return null;
            }
            
            if (allPlayers.Length == 1)
            {
                // Only one player - no need to calculate distance
                return allPlayers[0].gameObject.activeInHierarchy ? allPlayers[0] : null;
            }
            
            // Multiple players - find closest one
            RespawnablePlayer closestPlayer = null;
            float closestDistance = Mathf.Infinity;
            Vector3 myPosition = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            
            foreach (RespawnablePlayer player in allPlayers)
            {
                // Skip inactive players
                if (player == null || !player.gameObject.activeInHierarchy)
                    continue;
                
                // Calculate distance to this player
                Vector3 playerPos = player.transform.position;
                float distance = Vector3.Distance(myPosition, playerPos);
                
                // Update closest if this player is nearer
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayer = player;
                }
            }
            
            return closestPlayer;
        }
        
        /// <summary>
        /// Plays the player detection sound once (only if not already played for current detection and cooldown has passed)
        /// </summary>
        private void PlayPlayerDetectionSound()
        {
            // If using boundary box awareness, the AIBoundaryBox handles the detection sound
            // Skip playing individual AI detection sounds to avoid duplicates
            if (boundaryBox != null)
            {
                return;
            }
            
            // Check if cooldown has passed
            if (Time.time - _lastDetectionSoundTime < DETECTION_SOUND_COOLDOWN)
            {
                return;
            }
            
            if (_playerDetectionSound != null && _audioSource != null && !_hasPlayedDetectionSound)
            {
                _audioSource.PlayOneShot(_playerDetectionSound);
                _hasPlayedDetectionSound = true;
                _lastDetectionSoundTime = Time.time; // Record the time sound was played
            }
        }
        
        /// <summary>
        /// Resets the detection sound flag (called when player is lost)
        /// </summary>
        private void ResetDetectionSound()
        {
            _hasPlayedDetectionSound = false;
        }
        
        /// <summary>
        /// FULL BRAIN RESET: Clears all AI state and returns to base behavior.
        /// Called when player leaves detection zone or respawns.
        /// </summary>
        private void FullBrainReset()
        {
            // Clear player tracking
            playerTransform = null;
            
            // Reset all mode tracking flags
            exploreThenAttackIsTracking = false;
            roamThenAttackIsTracking = false;
            pathThenAttackIsTracking = false;
            idleThenAttackState = IdleThenAttackState.Idle;
            
            // Reset return-to-path timeout system
            pathThenAttackReturningToPath = false;
            timeAttemptingReturnToPath = 0f;
            returnTargetWaypointIndex = -1;
            
            // Reset spawn protection (for respawns)
            spawnTime = Time.time;
            
            // Clear navigation targets
            hasTarget = false;
            currentTarget = Vector3.zero;
            
            // Clear memory
            if (visitedPositions != null)
            {
                visitedPositions.Clear();
            }
            
            // Reset stuck detection
            stuckTimer = 0f;
            jumpIfStuckTimer = 0f;
            
            // Reset movement state
            if (_inputModule != null)
            {
                _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                _inputModule.OnRunDelegates?.Invoke(false);
                
                // Retract arms
                if (grabPlayerInHomingMode)
                {
                    _inputModule.OnLeftArmDelegates?.Invoke(0f);
                    _inputModule.OnRightArmDelegates?.Invoke(0f);
                }
            }
            
            // Reset detection sound
            ResetDetectionSound();
        }
        
        private void Start()
        {
            if (_activeRagdoll == null) _activeRagdoll = GetComponent<ActiveRagdoll>();
            if (_inputModule == null) _inputModule = GetComponent<InputModule>();
            
            // IMPORTANT: Use _activeRagdoll.Input instead of _inputModule directly
            // This ensures we're using the same InputModule that ActiveRagdoll uses
            _inputModule = _activeRagdoll.Input;
            _animationModule = GetComponent<AnimationModule>();
            
            // Get Rigidbody for stuck detection (use PhysicalTorso's rigidbody)
            if (_activeRagdoll.PhysicalTorso != null)
            {
                _rigidbody = _activeRagdoll.PhysicalTorso.GetComponent<Rigidbody>();
            }
            
            // Setup audio source for detection sounds (3D spatial audio)
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 1f; // 3D spatial audio
            _audioSource.volume = 1f;
            _audioSource.minDistance = 1f;
            _audioSource.maxDistance = 50f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            
            // Record initialization time to delay detection (allows AIBoundaryBox to register this AI first)
            _initializationTime = Time.time;
            
            // Record spawn time for jump protection
            spawnTime = Time.time;
            
            environmentSnapshot = new Vector3[totalRays];
            visitedPositions = new List<Vector3>();
            currentMovementDirection = transform.forward;
            spawnPosition = transform.position;
            
            // Initialize Idle Then Attack start position/rotation
            if (_activeRagdoll.PhysicalTorso != null)
            {
                idleThenAttackStartPosition = _activeRagdoll.PhysicalTorso.position;
                idleThenAttackStartRotation = _activeRagdoll.PhysicalTorso.rotation;
            }
            else
            {
                idleThenAttackStartPosition = transform.position;
                idleThenAttackStartRotation = transform.rotation;
            }
            
            // Initialize stuck detection
            lastPosition = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            
            // Auto-find CLOSEST player by default (spawner can override via SetTarget())
            // MULTIPLAYER: Finds closest player instead of just first player
            RespawnablePlayer player = FindClosestPlayer();
            if (player != null)
            {
                SetTargetTransform(player.gameObject);
                Debug.Log($"<color=cyan>[AI]</color> {gameObject.name} committed to targeting {player.gameObject.name} (closest player at spawn)");
            }
            
            // Take initial snapshot if not in idle mode
            if (currentMode != AIMode.Idle)
            {
                FindNewTarget(true);
            }
        }
        
        private void Update()
        {
            UpdateNavigation();
        }
        
        // LateUpdate removed - no longer needed without grabbing functionality
        
        /// <summary>
        /// Gets the player's physical Torso position (the actual body, not root GameObject)
        /// </summary>
        private Vector3 GetPlayerTorsoPosition(Transform player)
        {
            if (player == null) return Vector3.zero;
            
            ActiveRagdoll playerRagdoll = player.GetComponent<ActiveRagdoll>();
            if (playerRagdoll != null && playerRagdoll.PhysicalTorso != null)
            {
                return playerRagdoll.PhysicalTorso.position;
            }
            return player.position;
        }
        
        #region Boundary Box Management
        
        /// <summary>
        /// Set the boundary box for this AI (called by AIBoundaryBox)
        /// </summary>
        public void SetBoundaryBox(AIBoundaryBox box)
        {
            boundaryBox = box;
        }
        
        /// <summary>
        /// Called by AIBoundaryBox when player enters the box
        /// </summary>
        public void OnPlayerEnteredBoundaryBox()
        {
            // AI will naturally detect player through IsPlayerInDetectionZone()
            // This method is here for future extensions if needed
        }
        
        /// <summary>
        /// Called by AIBoundaryBox when player exits the box
        /// </summary>
        public void OnPlayerExitedBoundaryBox()
        {
            // AI will naturally lose player through IsPlayerInDetectionZone()
            // This method is here for future extensions if needed
        }
        
        #endregion
        
        /// <summary>
        /// SIMPLE: Checks if player is currently in this AI's detection zone.
        /// Detection zone depends on awareness settings (radius, raycast, or boundary box).
        /// Returns player transform if in zone, null otherwise.
        /// </summary>
        private Transform IsPlayerInDetectionZone()
        {
            // Skip detection during initialization period (allows AIBoundaryBox to register AIs first)
            if (Time.time - _initializationTime < INITIALIZATION_DELAY)
            {
                return null;
            }
            
            // Find player (simple, no caching, no timers)
            // MULTIPLAYER: If player is lost/respawned, find CLOSEST active player and commit
            if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
            {
                // Clear old reference
                playerTransform = null;
                
                // Try to find CLOSEST active player
                RespawnablePlayer player = FindClosestPlayer();
                if (player != null && player.gameObject.activeInHierarchy)
                {
                    playerTransform = player.transform;
                    Debug.Log($"<color=yellow>[AI]</color> {gameObject.name} re-acquired target: {player.gameObject.name} (closest player after loss)");
                }
                else
                {
                    // No active player - return null
                    return null;
                }
            }
            
            // Double-check the player is still valid before using it
            if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
            {
                playerTransform = null;
                return null;
            }
            
            Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            Vector3 playerPos = GetPlayerTorsoPosition(playerTransform);
            
            // CHECK DETECTION ZONE
            // If using boundary box, ONLY check if player is inside the box
            // Otherwise use distance radius
            
            if (boundaryBox != null)
            {
                // BOUNDARY BOX MODE: Player must be inside the box
                return boundaryBox.IsPositionInsideBox(playerPos) ? playerTransform : null;
            }
            else
            {
                // RADIUS MODE: Check distance to player
                // Determine which radius to use based on current mode
                float detectionRadius = 10f; // Default
                
                switch (currentMode)
                {
                    case AIMode.ExploreThenAttack:
                        detectionRadius = exploreThenAttackDetectionRadius;
                        break;
                    case AIMode.RoamThenAttack:
                        detectionRadius = roamThenAttackDetectionRadius;
                        break;
                    case AIMode.IdleThenAttack:
                        detectionRadius = idleThenAttackDetectionRange;
                        break;
                    case AIMode.PathMovement:
                        if (pathThenAttackEnabled)
                            detectionRadius = pathThenAttackDetectionRadius;
                        break;
                    case AIMode.Homing:
                        // Always detect in homing mode
                        return playerTransform;
                }
                
                // Simple distance check
                float distance = Vector3.Distance(
                    new Vector3(currentPos.x, 0, currentPos.z),
                    new Vector3(playerPos.x, 0, playerPos.z)
                );
                
                return distance <= detectionRadius ? playerTransform : null;
            }
        }
        
        private void UpdateNavigation()
        {
            switch (currentMode)
            {
                case AIMode.Idle:
                    UpdateIdleMode();
                    break;
                case AIMode.Roam:
                    UpdateRoamMode();
                    break;
                case AIMode.Explore:
                    UpdateExploreMode();
                    break;
                case AIMode.Homing:
                    UpdateHomingMode();
                    break;
                case AIMode.PathMovement:
                    UpdatePathMovementMode();
                    break;
                case AIMode.RaceToFinish:
                    UpdateRaceToFinishMode();
                    break;
                case AIMode.RedLightGreenLight:
                    UpdateRedLightGreenLightMode();
                    break;
                case AIMode.ExploreThenAttack:
                    UpdateExploreThenAttackMode();
                    break;
                case AIMode.RoamThenAttack:
                    UpdateRoamThenAttackMode();
                    break;
                case AIMode.IdleThenAttack:
                    UpdateIdleThenAttackMode();
                    break;
            }
        }
        
        private void UpdateIdleMode()
        {
            // SIMPLE IDLE MODE: Ragdoll wanders in a small circle around its "home point" (where it spawned)
            // It picks a random spot nearby → walks there → pauses → picks new spot → repeat
            // NO BOUNDARY BOX - only uses home point + idle pace radius
            
            // Check if waiting at current position
            if (idleWaitTimer > 0f)
            {
                idleWaitTimer -= Time.deltaTime;
                _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                _inputModule.OnRunDelegates?.Invoke(false);
                return;
            }
            
            // Check if need new target or reached current target
            Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            float distanceToTarget = Vector3.Distance(
                new Vector3(currentPos.x, 0, currentPos.z),
                new Vector3(currentTarget.x, 0, currentTarget.z)
            );
            
            if (!hasTarget || distanceToTarget < 0.5f)
            {
                // Pick new idle target and wait
                FindIdleTarget();
                idleWaitTimer = idleWaitTime;
                _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                _inputModule.OnRunDelegates?.Invoke(false);
            }
            else
            {
                // Move toward idle target (walk, don't run)
                MoveTowardsTarget();
            }
        }
        
        private void UpdateRoamMode()
        {
            // Check if waiting at current position
            if (roamWaitTimer > 0f)
            {
                roamWaitTimer -= Time.deltaTime;
                _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                return;
            }
            
            // Check if we need a new target
            bool needsNewTarget = false;
            
            if (!hasTarget)
            {
                needsNewTarget = true;
            }
            else
            {
                // Use PhysicalTorso position for accurate distance calculation
                Vector3 physicalPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                Vector3 currentPos = new Vector3(physicalPos.x, 0, physicalPos.z);
                Vector3 targetPos = new Vector3(currentTarget.x, 0, currentTarget.z);
                float distanceToTarget = Vector3.Distance(currentPos, targetPos);
                
                if (distanceToTarget < targetReachedDistance)
                {
                    needsNewTarget = true;
                    roamWaitTimer = roamWaitTime; // Wait before moving again
                }
            }
            
            if (needsNewTarget)
            {
                FindRoamTarget();
            }
            
            // Navigate toward current target
            if (hasTarget && roamWaitTimer <= 0f)
            {
                MoveTowardsTarget();
            }
        }
        
        private void UpdateExploreMode()
        {
            // EXPLORE MODE: Intelligent pathfinding with 360° raycasting and memory
            // Respects boundary box unless exploreIgnoresBoundary is true
            
            // Check if we need a new target
            bool needsNewTarget = false;
            
            if (!hasTarget)
            {
                needsNewTarget = true;
            }
            else
            {
                // Use PhysicalTorso position for accurate distance calculation
                Vector3 physicalPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                Vector3 currentPos = new Vector3(physicalPos.x, 0, physicalPos.z);
                Vector3 targetPos = new Vector3(currentTarget.x, 0, currentTarget.z);
                float distanceToTarget = Vector3.Distance(currentPos, targetPos);
                
                // 1. Reached target
                if (distanceToTarget < targetReachedDistance)
                {
                    needsNewTarget = true;
                }
                // 2. Stuck for too long (emergency) - like Mimic
                else if (_rigidbody != null && _rigidbody.linearVelocity.magnitude < 0.1f)
                {
                    stuckTimer += Time.deltaTime;
                    if (stuckTimer > 1.5f)
                    {
                        needsNewTarget = true;
                        isStuck = true;
                    }
                }
                // 3. Path blocked - like Mimic
                else
                {
                    Vector3 directionToTarget = (currentTarget - physicalPos).normalized;
                    RaycastHit hit;
                    if (Physics.Raycast(physicalPos + Vector3.up * 0.5f, directionToTarget, out hit, 1.5f))
                    {
                        // Hit something close - path might be blocked
                        needsNewTarget = true;
                    }
                    
                    // Reset stuck timer if moving
                    stuckTimer = 0f;
                    isStuck = false;
                }
            }
            
            if (needsNewTarget)
            {
                // PERFORMANCE: Only raycast if cooldown has elapsed
                if (Time.time - _lastRaycastTime >= RAYCAST_COOLDOWN)
                {
                    FindNewTarget(isStuck); // Pass emergency mode if stuck
                    _lastRaycastTime = Time.time;
                }
            }
            
            // Navigate toward current target
            if (hasTarget)
            {
                MoveTowardsTarget();
            }
        }
        
        private void UpdateExploreThenAttackMode()
        {
            // SIMPLE: Is player in detection zone? Yes → Chase. No → Explore.
            
            Transform detectedPlayer = IsPlayerInDetectionZone();
            
            if (detectedPlayer != null)
            {
                // Player is IN detection zone → CHASE MODE
                if (!exploreThenAttackIsTracking)
                {
                    // Just entered zone → start tracking
                    exploreThenAttackIsTracking = true;
                    PlayPlayerDetectionSound();
                }
                
                // Chase player - USE TORSO POSITION
                Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                Vector3 playerTorsoPos = GetPlayerTorsoPosition(detectedPlayer);
                currentTarget = playerTorsoPos;
                hasTarget = true;
                
                // Extend arms if grabbing enabled
                if (grabPlayerInHomingMode)
                {
                    float distanceToPlayer = Vector3.Distance(currentPos, playerTorsoPos);
                    if (distanceToPlayer <= armExtendDistance)
                    {
                        _inputModule.OnLeftArmDelegates?.Invoke(1f);
                        _inputModule.OnRightArmDelegates?.Invoke(1f);
                    }
                    else
                    {
                        _inputModule.OnLeftArmDelegates?.Invoke(0f);
                        _inputModule.OnRightArmDelegates?.Invoke(0f);
                    }
                }
                
                MoveTowardsTarget();
            }
            else
            {
                // Player is NOT in detection zone → EXPLORE MODE
                if (exploreThenAttackIsTracking)
                {
                    // Player left zone - FULL BRAIN RESET
                    FullBrainReset();
                }
                
                // Explore normally
                UpdateExploreMode();
            }
        }
        
        private void UpdateRoamThenAttackMode()
        {
            // SIMPLE: Is player in detection zone? Yes → Chase. No → Roam.
            
            Transform detectedPlayer = IsPlayerInDetectionZone();
            
            if (detectedPlayer != null)
            {
                // Player is IN detection zone → CHASE MODE
                if (!roamThenAttackIsTracking)
                {
                    // Just entered zone → start tracking
                    roamThenAttackIsTracking = true;
                    PlayPlayerDetectionSound();
                }
                
                // Chase player - USE TORSO POSITION
                Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                Vector3 playerTorsoPos = GetPlayerTorsoPosition(detectedPlayer);
                currentTarget = playerTorsoPos;
                hasTarget = true;
                
                // Extend arms if grabbing enabled
                if (grabPlayerInHomingMode)
                {
                    float distanceToPlayer = Vector3.Distance(currentPos, playerTorsoPos);
                    if (distanceToPlayer <= armExtendDistance)
                    {
                        _inputModule.OnLeftArmDelegates?.Invoke(1f);
                        _inputModule.OnRightArmDelegates?.Invoke(1f);
                    }
                    else
                    {
                        _inputModule.OnLeftArmDelegates?.Invoke(0f);
                        _inputModule.OnRightArmDelegates?.Invoke(0f);
                    }
                }
                
                MoveTowardsTarget();
            }
            else
            {
                // Player is NOT in detection zone → ROAM MODE
                if (roamThenAttackIsTracking)
                {
                    // Player left zone - FULL BRAIN RESET
                    FullBrainReset();
                }
                
                // Roam normally
                UpdateRoamMode();
            }
        }
        
        private void UpdateHomingMode()
        {
            // Actively search for player by tag (more robust than cached references)
            Transform currentPlayerTransform = IsPlayerInDetectionZone();
            
            if (!currentPlayerTransform)
            {
                // No target found, idle
                _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                _inputModule.OnRunDelegates?.Invoke(false);
                return;
            }
            
            // Update the legacy playerTransform for other systems that might need it
            if (playerTransform != currentPlayerTransform)
            {
                playerTransform = currentPlayerTransform;
                // Player reference updated
                // Debug.Log($"[{gameObject.name}] 🎯 Updated player reference in Homing mode");
            }
            
            // Get player TORSO position for accurate tracking
            Vector3 playerPos = GetPlayerTorsoPosition(currentPlayerTransform);
            // Use PhysicalTorso position for accurate distance calculation
            Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            float distanceToPlayer = Vector3.Distance(
                new Vector3(currentPos.x, 0, currentPos.z),
                new Vector3(playerPos.x, 0, playerPos.z)
            );
            
            // Check if player is in range
            bool canSeePlayer = alwaysKnowPlayerPosition;
            if (!alwaysKnowPlayerPosition)
            {
                // Use raycast detection from PhysicalTorso position
                Vector3 rayOrigin = currentPos + Vector3.up * raycastHeight;
                Vector3 directionToPlayer = (playerPos - currentPos).normalized;
                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, directionToPlayer, out hit, playerDetectionRange))
                {
                    if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
                    {
                        canSeePlayer = true;
                    }
                }
            }
            
            if (canSeePlayer)
            {
                // Set player position as target
                currentTarget = playerPos;
                
                // BOUNDARY: Clamp target to stay within bounds (even when chasing player)
                currentTarget = ClampToBoundary(currentTarget);
                
                hasTarget = true;
                
                // Visual debug: draw line to player (from PhysicalTorso)
                Debug.DrawLine(currentPos + Vector3.up, playerPos + Vector3.up, Color.yellow, 0.1f);
                
                // Simple arm extension: extend when within armExtendDistance
                if (grabPlayerInHomingMode)
                {
                    if (distanceToPlayer <= armExtendDistance)
                    {
                        // Extend both arms forward
                        _inputModule.OnLeftArmDelegates?.Invoke(1f);
                        _inputModule.OnRightArmDelegates?.Invoke(1f);
                        Debug.DrawLine(currentPos, playerPos, Color.magenta, 0.1f);
                    }
                    else
                    {
                        // Retract arms
                        _inputModule.OnLeftArmDelegates?.Invoke(0f);
                        _inputModule.OnRightArmDelegates?.Invoke(0f);
                    }
                }
                
                // Move toward player until reaching homingStopDistance
                if (distanceToPlayer > homingStopDistance)
                {
                    MoveTowardsTarget();
                }
                else
                {
                    // Close enough, stop moving
                    _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                }
            }
            else
            {
                // Can't see player, retract arms and explore
                if (grabPlayerInHomingMode)
                {
                    _inputModule.OnLeftArmDelegates?.Invoke(0f);
                    _inputModule.OnRightArmDelegates?.Invoke(0f);
                }
                
                // Can't see player, explore to find them
                UpdateExploreMode();
            }
        }
        
        private void UpdatePathMovementMode()
        {
            // PATH MOVEMENT: Navigate through a series of waypoints in order
            // NO BOUNDARY BOX - ragdoll can navigate anywhere to reach waypoints
            // OPTIONAL: Path Then Attack - chase player if they enter 180° front vision
            
            Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            Vector3 currentForward = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.transform.forward : transform.forward;
            currentForward.y = 0;
            currentForward.Normalize();
            
            // NAVIGATING TO FINISH FLAG (after path completion)
            if (pathIsNavigatingToFinish)
            {
                // Try to find the finish flag if we haven't already
                if (!pathFinishFlagFound)
                {
                    LevelFinishTrigger finishTrigger = FindFirstObjectByType<LevelFinishTrigger>();
                    if (finishTrigger != null)
                    {
                        pathFinishFlagTransform = finishTrigger.transform;
                        pathFinishFlagFound = true;
                    }
                    else
                    {
                        // Stop moving if no finish flag
                        _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                        _inputModule.OnRunDelegates?.Invoke(false);
                        return;
                    }
                }
                
                // Navigate to the finish flag
                if (pathFinishFlagTransform != null)
                {
                    Vector3 finishPos = pathFinishFlagTransform.position;
                    
                    // Set finish flag as target
                    currentTarget = finishPos;
                    hasTarget = true;
                    
                    // Visual debug: draw line to finish flag
                    Debug.DrawLine(currentPos + Vector3.up, finishPos + Vector3.up, Color.cyan, 0.1f);
                    
                    // Move toward finish flag
                    MoveTowardsTarget();
                }
                else
                {
                    // Lost reference to finish flag
                    pathFinishFlagFound = false;
                }
                
                return; // Don't process normal waypoint logic
            }
            
            // ========== PATH THEN ATTACK BEHAVIOR ==========
            // If enabled, AI will chase player when they enter 180° front vision cone
            if (pathThenAttackEnabled)
            {
                // Actively search for player by tag (more robust than cached references)
                Transform currentPlayerTransform = IsPlayerInDetectionZone();
                
                if (!currentPlayerTransform)
                {
                    // No player found, stop tracking and return to path
                    if (pathThenAttackIsTracking)
                    {
                        // Player lost
                    // Debug.Log($"[{gameObject.name}] Lost player (not found by tag) in PathThenAttack, returning to path");
                        pathThenAttackIsTracking = false;
                        ResetDetectionSound();
                        
                        // Retract arms
                        if (grabPlayerInHomingMode)
                        {
                            _inputModule.OnLeftArmDelegates?.Invoke(0f);
                            _inputModule.OnRightArmDelegates?.Invoke(0f);
                        }
                        
                        // Find nearest waypoint to resume from
                        int nearestWaypointIndex = FindNearestWaypointIndex(currentPos);
                        currentWaypointIndex = nearestWaypointIndex;
                        
                        // ACTIVATE return-to-path timeout system
                        pathThenAttackReturningToPath = true;
                        returnTargetWaypointIndex = nearestWaypointIndex;
                        timeAttemptingReturnToPath = 0f;
                    }
                }
                else
                {
                    // Update the legacy playerTransform for other systems that might need it
                    if (playerTransform != currentPlayerTransform)
                    {
                        playerTransform = currentPlayerTransform;
                        // Player reference updated
                // Debug.Log($"[{gameObject.name}] 🎯 Updated player reference in PathThenAttack mode");
                    }
                }
                
                if (currentPlayerTransform)
                {
                // Calculate distance to player TORSO
                Vector3 playerPos = GetPlayerTorsoPosition(currentPlayerTransform);
                float distanceToPlayer = Vector3.Distance(
                    new Vector3(currentPos.x, 0, currentPos.z),
                    new Vector3(playerPos.x, 0, playerPos.z)
                );
                
                // Check if player is in front 180° vision cone
                Vector3 directionToPlayer = (playerPos - currentPos).normalized;
                directionToPlayer.y = 0;
                directionToPlayer.Normalize();
                
                float angleToPlayer = Vector3.Angle(currentForward, directionToPlayer);
                bool playerInFrontVision = angleToPlayer <= 90f; // 180° cone = 90° on each side
                
                // Hysteresis: Different enter/exit radii to prevent flickering
                if (pathThenAttackIsTracking)
                {
                    // Currently chasing - check lose radius
                    if (distanceToPlayer > pathThenAttackLoseRadius)
                    {
                        // Lost player, return to path
                        pathThenAttackIsTracking = false;
                        ResetDetectionSound(); // Reset sound flag for next detection
                        
                        // Retract arms
                        if (grabPlayerInHomingMode)
                        {
                            _inputModule.OnLeftArmDelegates?.Invoke(0f);
                            _inputModule.OnRightArmDelegates?.Invoke(0f);
                        }
                        
                        // Find nearest waypoint to resume from
                        int nearestWaypointIndex = FindNearestWaypointIndex(currentPos);
                        currentWaypointIndex = nearestWaypointIndex;
                        
                        // ACTIVATE return-to-path timeout system
                        pathThenAttackReturningToPath = true;
                        returnTargetWaypointIndex = nearestWaypointIndex;
                        timeAttemptingReturnToPath = 0f;
                    }
                }
                else
                {
                    // Currently following path - check detection radius and front vision
                    if (distanceToPlayer <= pathThenAttackDetectionRadius && playerInFrontVision)
                    {
                        // Detected player in front vision cone, start chasing
                        pathThenAttackIsTracking = true;
                        pathThenAttackSavedWaypointIndex = currentWaypointIndex; // Save current waypoint
                        PlayPlayerDetectionSound(); // Play detection sound when patrol spots player
                        
                        // Clear return-to-path flag (we're now chasing, not returning)
                        pathThenAttackReturningToPath = false;
                        timeAttemptingReturnToPath = 0f;
                        returnTargetWaypointIndex = -1;
                    }
                }
                
                // Execute behavior based on tracking state
                if (pathThenAttackIsTracking)
                {
                    // CHASE BEHAVIOR - Homing towards player with grab mechanics
                    currentTarget = playerPos;
                    hasTarget = true;
                    
                    // Visual debug: draw line to player
                    Debug.DrawLine(currentPos + Vector3.up, playerPos + Vector3.up, Color.red, 0.1f);
                    
                    // Arm extension when within range
                    if (grabPlayerInHomingMode)
                    {
                        if (distanceToPlayer <= armExtendDistance)
                        {
                            // Extend both arms forward
                            _inputModule.OnLeftArmDelegates?.Invoke(1f);
                            _inputModule.OnRightArmDelegates?.Invoke(1f);
                            Debug.DrawLine(currentPos, playerPos, Color.magenta, 0.1f);
                        }
                        else
                        {
                            // Retract arms
                            _inputModule.OnLeftArmDelegates?.Invoke(0f);
                            _inputModule.OnRightArmDelegates?.Invoke(0f);
                        }
                    }
                    
                    // Move toward player until reaching homingStopDistance
                    if (distanceToPlayer > homingStopDistance)
                    {
                        MoveTowardsTarget();
                    }
                    else
                    {
                        // Close enough, stop moving
                        _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                    }
                    
                    return; // Don't process normal path navigation while chasing
                }
                } // End if (playerTransform != null)
            }
            // ========== END PATH THEN ATTACK BEHAVIOR ==========
            
            // Check if we have waypoints
            if (pathWaypoints == null || pathWaypoints.Count == 0)
            {
                // No waypoints, stop moving
                _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                _inputModule.OnRunDelegates?.Invoke(false);
                return;
            }
            
            // Check if path is complete (only relevant if NOT looping forever and NOT navigating to finish)
            if (pathIsComplete && !pathLoopForever && !pathEndAtFinishTrigger)
            {
                // Path finished, stay stopped
                _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                _inputModule.OnRunDelegates?.Invoke(false);
                return;
            }
            
            // RETURNING TO START MODE
            if (pathIsReturningToStart)
            {
                // Navigate back to Point 0 (spawn position, which is pathWaypoints[0])
                Vector3 point0 = pathWaypoints[0];
                float distanceToStart = Vector3.Distance(
                    new Vector3(currentPos.x, 0, currentPos.z),
                    new Vector3(point0.x, 0, point0.z)
                );
                
                // Check if we've reached Point 0
                if (distanceToStart <= pathReachedDistance)
                {
                    // Reached Point 0!
                    pathIsComplete = true;
                    
                    // Check if we should navigate to finish flag
                    if (pathEndAtFinishTrigger)
                    {
                        pathIsNavigatingToFinish = true;
                        return; // Will navigate to finish on next frame
                    }
                    else
                    {
                        // Just stop
                        _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                        _inputModule.OnRunDelegates?.Invoke(false);
                        return;
                    }
                }
                
                // Navigate back to Point 0
                currentTarget = point0;
                hasTarget = true;
                Debug.DrawLine(currentPos + Vector3.up, point0 + Vector3.up, Color.cyan, 0.1f);
                MoveTowardsTarget();
                return;
            }
            
            // NORMAL WAYPOINT NAVIGATION
            // Check if we've completed all waypoints
            if (currentWaypointIndex >= pathWaypoints.Count)
            {
                // Reached end of waypoint list - completed one cycle
                currentCycleCount++;
                
                // ONE WAY PATH THEN BEHAVIOR SWITCH takes priority - complete once and switch
                if (pathOneWayThenBehaviorSwitch)
                {
                    currentMode = pathFinalBehaviorMode;
                    
                    // Initialize the new mode if needed
                    if (pathFinalBehaviorMode == AIMode.Explore)
                    {
                        FindNewTarget(false); // Take initial snapshot for explore mode
                    }
                    else if (pathFinalBehaviorMode == AIMode.Roam)
                    {
                        FindRoamTarget(); // Find initial roam target
                    }
                    else if (pathFinalBehaviorMode == AIMode.Idle)
                    {
                        FindIdleTarget(); // Find initial idle target
                    }
                    // Homing mode will auto-initialize in UpdateHomingMode
                    
                    return; // Exit path mode, will use new mode on next frame
                }
                
                // Determine if we should continue looping
                bool shouldContinue = false;
                
                if (pathLoopForever)
                {
                    // Infinite loop mode
                    shouldContinue = true;
                }
                else if (pathNumberOfCycles > 0 && currentCycleCount < pathNumberOfCycles)
                {
                    // Limited cycles mode - continue if we haven't reached the target
                    shouldContinue = true;
                }
                
                if (shouldContinue)
                {
                    // Loop back to the beginning
                    currentWaypointIndex = 0;
                }
                else if (pathReturnToStartAndStop)
                {
                    // Start returning to Point A (spawn position)
                    pathIsReturningToStart = true;
                    return; // Will handle return on next frame
                }
                else
                {
                    // Path cycles complete!
                    pathIsComplete = true;
                    
                    // Check if we should navigate to finish flag
                    if (pathEndAtFinishTrigger)
                    {
                        pathIsNavigatingToFinish = true;
                        return; // Will navigate to finish on next frame
                    }
                    else
                    {
                        // Just stop at the last waypoint
                        _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                        _inputModule.OnRunDelegates?.Invoke(false);
                        return;
                    }
                }
            }
            
            // Get current waypoint
            Vector3 currentWaypoint = pathWaypoints[currentWaypointIndex];
            float distanceToWaypoint = Vector3.Distance(
                new Vector3(currentPos.x, 0, currentPos.z),
                new Vector3(currentWaypoint.x, 0, currentWaypoint.z)
            );
            
            // Check if we've reached the current waypoint
            if (distanceToWaypoint <= pathReachedDistance)
            {
                // Reached waypoint! Move to next one
                currentWaypointIndex++;
                
                // If we were returning to path and reached the target waypoint, clear the flag
                if (pathThenAttackReturningToPath && currentWaypointIndex > returnTargetWaypointIndex)
                {
                    // Successfully returned to path!
                    pathThenAttackReturningToPath = false;
                    timeAttemptingReturnToPath = 0f;
                    returnTargetWaypointIndex = -1;
                }
            }
            else
            {
                // Still trying to reach waypoint
                // ONLY check timeout if we're specifically returning to path after a chase
                if (pathThenAttackReturningToPath && currentWaypointIndex == returnTargetWaypointIndex)
                {
                    // Increment return timer
                    timeAttemptingReturnToPath += Time.deltaTime;
                    
                    // Check if timeout exceeded
                    if (timeAttemptingReturnToPath >= RETURN_TO_PATH_TIMEOUT_SECONDS)
                    {
                        // AI is stuck trying to return to path! Kill it and let the spawner respawn it
                        // Debug.Log($"[{gameObject.name}] STUCK! Could not return to path (waypoint {returnTargetWaypointIndex}) in {RETURN_TO_PATH_TIMEOUT_SECONDS} seconds. Respawning...");
                        
                        RespawnableAIRagdoll respawnable = GetComponent<RespawnableAIRagdoll>();
                        if (respawnable != null)
                        {
                            respawnable.Respawn();
                        }
                        return; // Don't continue navigation after triggering respawn
                    }
                }
            }
            
            // Set current waypoint as target (NO boundary clamping - can go anywhere)
            currentTarget = currentWaypoint;
            hasTarget = true;
            
            // Visual debug: draw line to current waypoint
            Debug.DrawLine(currentPos + Vector3.up, currentWaypoint + Vector3.up, Color.green, 0.1f);
            
            // Move toward current waypoint
            MoveTowardsTarget();
        }
        
        private void UpdateRaceToFinishMode()
        {
            // RACE TO FINISH: Automatically find and navigate to the level finish flag
            // Simple, direct navigation - no boundaries, no obstacles considered
            
            // Try to find the finish flag if we haven't already
            if (!finishFlagFound)
            {
                LevelFinishTrigger finishTrigger = FindFirstObjectByType<LevelFinishTrigger>();
                if (finishTrigger != null)
                {
                    finishFlagTransform = finishTrigger.transform;
                    finishFlagFound = true;
                }
                else
                {
                    // Stop moving if no finish flag
                    _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                    _inputModule.OnRunDelegates?.Invoke(false);
                    return;
                }
            }
            
            // Navigate to the finish flag
            if (finishFlagTransform != null)
            {
                Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                Vector3 finishPos = finishFlagTransform.position;
                
                // Set finish flag as target
                currentTarget = finishPos;
                hasTarget = true;
                
                // Visual debug: draw line to finish flag
                Debug.DrawLine(currentPos + Vector3.up, finishPos + Vector3.up, Color.cyan, 0.1f);
                
                // Move toward finish flag (always running in race mode!)
                MoveTowardsTarget();
            }
            else
            {
                // Lost reference to finish flag
                finishFlagFound = false;
            }
        }
        
        private void UpdateRedLightGreenLightMode()
        {
            // RED LIGHT GREEN LIGHT MODE: Race to finish but stop during red lights
            // Uses bell curve anticipation to predict red lights
            // Goes full ragdoll if caught moving during red light
            
            // Find Red Light Green Light Manager if not assigned
            if (redLightGreenLightManager == null)
            {
                redLightGreenLightManager = FindFirstObjectByType<RedLightGreenLightManager>();
                if (redLightGreenLightManager == null)
                {
                    // Fall back to race to finish behavior
                    UpdateRaceToFinishMode();
                    return;
                }
            }
            
            // Get DefaultBehaviour reference for ragdoll control (cache it)
            if (_defaultBehaviour == null)
            {
                _defaultBehaviour = GetComponent<DefaultBehaviour>();
            }
            
            // Try to find the finish flag if we haven't already
            if (!finishFlagFound)
            {
                LevelFinishTrigger finishTrigger = FindFirstObjectByType<LevelFinishTrigger>();
                if (finishTrigger != null)
                    {
                        finishFlagTransform = finishTrigger.transform;
                        finishFlagFound = true;
                    }
                    else
                    {
                        _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                        _inputModule.OnRunDelegates?.Invoke(false);
                    return;
                }
            }
            
            // Check manager state
            bool isGreenLight = IsGreenLightActive();
            bool isRedLight = IsRedLightActive();
            
            Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            
            // === RED LIGHT DETECTION & RESPONSE ===
            if (isRedLight)
            {
                // RED LIGHT IS ACTIVE - MUST GO FULL RAGDOLL IMMEDIATELY
                if (!rlglWasForcedRagdoll)
                {
                    // Just entered red light while moving - CAUGHT!
                    ForceRagdollMode(true);
                    rlglWasForcedRagdoll = true;
                    // rlglIsMoving = false;
                    rlglIsInAnticipationStop = false;
                }
                
                // Stay stopped during red light
                _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                _inputModule.OnRunDelegates?.Invoke(false);
                return;
            }
            else if (rlglWasForcedRagdoll)
            {
                // Red light ended - recover from forced ragdoll
                ForceRagdollMode(false);
                rlglWasForcedRagdoll = false;
                rlglMovementStartTime = Time.time; // Reset movement timer
                rlglNextAnticipationTime = Time.time + GetRandomAnticipationTime();
            }
            
            // === GREEN LIGHT BEHAVIOR ===
            if (isGreenLight)
            {
                // Initialize next anticipation time if this is the first green light
                if (rlglNextAnticipationTime == 0f)
                {
                    rlglNextAnticipationTime = Time.time + GetRandomAnticipationTime();
                }
                
                // Handle anticipation stops
                if (rlglIsInAnticipationStop)
                {
                    // Currently in anticipation stop
                    rlglAnticipationTimer -= Time.deltaTime;
                    
                    if (rlglAnticipationTimer <= 0f)
                    {
                        // Anticipation stop over - resume moving
                        rlglIsInAnticipationStop = false;
                        rlglMovementStartTime = Time.time;
                        rlglNextAnticipationTime = Time.time + GetRandomAnticipationTime();
                    }
                    else
                    {
                        // Stay stopped during anticipation
                        _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                        _inputModule.OnRunDelegates?.Invoke(false);
                        return;
                    }
                }
                else
                {
                    // Check if it's time for an anticipation stop (and not during initial grace period)
                    if (Time.time >= rlglNextAnticipationTime && rlglNextAnticipationTime > 0f)
                    {
                        // TIME TO STOP AND ANTICIPATE
                        rlglIsInAnticipationStop = true;
                        rlglAnticipationTimer = anticipationStopDuration;
                        
                        _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                        _inputModule.OnRunDelegates?.Invoke(false);
                        return;
                    }
                }
                
                // MOVE TOWARDS FINISH FLAG
                if (finishFlagTransform != null)
                {
                    Vector3 finishPos = finishFlagTransform.position;
                    
                    // Set finish flag as target
                    currentTarget = finishPos;
                    hasTarget = true;
                    
                    // Visual debug: draw line to finish flag
                    Debug.DrawLine(currentPos + Vector3.up, finishPos + Vector3.up, Color.magenta, 0.1f);
                    
                    // Move toward finish flag
                    // rlglIsMoving = true;
                    MoveTowardsTarget();
                }
            }
            else
            {
                // Game hasn't started yet or is in inactive state - just wait
                _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                _inputModule.OnRunDelegates?.Invoke(false);
            }
        }
        
        /// <summary>
        /// Check if green light is currently active
        /// </summary>
        private bool IsGreenLightActive()
        {
            if (redLightGreenLightManager == null) return false;
            
            // Access the manager's state via reflection to check if it's green light
            System.Type managerType = redLightGreenLightManager.GetType();
            System.Reflection.FieldInfo stateField = managerType.GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (stateField != null)
            {
                object stateValue = stateField.GetValue(redLightGreenLightManager);
                string stateName = stateValue.ToString();
                
                return stateName == "InitialGracePeriod" || stateName == "GreenLight";
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if red light is currently active
        /// </summary>
        private bool IsRedLightActive()
        {
            if (redLightGreenLightManager == null) return false;
            
            // Access the manager's state via reflection
            System.Type managerType = redLightGreenLightManager.GetType();
            System.Reflection.FieldInfo stateField = managerType.GetField("currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (stateField != null)
            {
                object stateValue = stateField.GetValue(redLightGreenLightManager);
                string stateName = stateValue.ToString();
                
                return stateName == "RedLightGracePeriod" || stateName == "RedLightChecking";
            }
            
            return false;
        }
        
        /// <summary>
        /// Generate random anticipation time using bell curve (same as manager)
        /// </summary>
        private float GetRandomAnticipationTime()
        {
            // Box-Muller transform for normal distribution
            float u1 = Random.Range(0f, 1f);
            float u2 = Random.Range(0f, 1f);
            
            float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
            float randNormal = anticipationMean + anticipationStdDev * randStdNormal;
            
            // Clamp to min/max range
            float duration = Mathf.Clamp(randNormal, anticipationMinTime, anticipationMaxTime);
            
            return duration;
        }
        
        /// <summary>
        /// Force ragdoll mode on/off
        /// </summary>
        private void ForceRagdollMode(bool enable)
        {
            if (_defaultBehaviour == null) return;
            
            // Use reflection to call the RagdollInput method on DefaultBehaviour
            System.Type behaviourType = _defaultBehaviour.GetType();
            System.Reflection.MethodInfo ragdollMethod = behaviourType.GetMethod("RagdollInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (ragdollMethod != null)
            {
                ragdollMethod.Invoke(_defaultBehaviour, new object[] { enable });
            }
        }
        
        private void UpdateIdleThenAttackMode()
        {
            // SIMPLIFIED IDLE THEN ATTACK: Idle → Homing → Return Home
            // NO BOUNDARY BOX - only uses home point + vision cone
            
            Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            Vector3 currentForward = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.transform.forward : transform.forward;
            currentForward.y = 0;
            currentForward.Normalize();
            
            switch (idleThenAttackState)
            {
                case IdleThenAttackState.Idle:
                    // IDLE STATE: Pace around home point, watch for player
                    UpdateIdleMode();
                    
                    // Actively search for player by tag (more robust than cached references)
                    Transform idlePlayerTransform = IsPlayerInDetectionZone();
                    
                    // Update the legacy playerTransform for other systems that might need it
                    if (idlePlayerTransform && playerTransform != idlePlayerTransform)
                    {
                        playerTransform = idlePlayerTransform;
                        // Player reference updated
                // Debug.Log($"[{gameObject.name}] 🎯 Updated player reference in IdleThenAttack (Idle state)");
                    }
                    
                    // Watch for player in vision cone
                    if (idlePlayerTransform)
                    {
                        // Get player TORSO position for accurate detection
                        Vector3 playerPos = GetPlayerTorsoPosition(idlePlayerTransform);
                        float distanceToPlayer = Vector3.Distance(currentPos, playerPos);
                        
                        if (distanceToPlayer <= idleThenAttackDetectionRange)
                        {
                            // Check if player is in vision cone
                            Vector3 directionToPlayer = (playerPos - currentPos).normalized;
                            directionToPlayer.y = 0;
                            directionToPlayer.Normalize();
                            
                            float angle = Vector3.Angle(currentForward, directionToPlayer);
                            
                            if (angle <= idleVisionConeAngle / 2f)
                            {
                                // Raycast to check line of sight
                                RaycastHit hit;
                                if (Physics.Raycast(currentPos + Vector3.up * raycastHeight, directionToPlayer, out hit, idleThenAttackDetectionRange))
                                {
                                    if (hit.transform == idlePlayerTransform || hit.transform.IsChildOf(idlePlayerTransform))
                                    {
                                        // Player spotted! Switch to HOMING MODE
                                        idleThenAttackState = IdleThenAttackState.Homing;
                                        PlayPlayerDetectionSound(); // Play detection sound
                                    }
                                }
                            }
                        }
                    }
                    break;
                    
                case IdleThenAttackState.Homing:
                    // HOMING STATE: Normal homing behavior - no boundaries, can jump, follows anywhere
                    // Actively search for player by tag (more robust than cached references)
                    Transform homingPlayerTransform = IsPlayerInDetectionZone();
                    
                    if (!homingPlayerTransform)
                    {
                        // Player lost, return home
                        // Player lost
                    // Debug.Log($"[{gameObject.name}] Lost player (not found by tag) in homing, returning home");
                        idleThenAttackState = IdleThenAttackState.Returning;
                        ResetDetectionSound(); // Reset sound flag for next detection
                        
                        // Retract arms
                        if (grabPlayerInHomingMode)
                        {
                            _inputModule.OnLeftArmDelegates?.Invoke(0f);
                            _inputModule.OnRightArmDelegates?.Invoke(0f);
                        }
                        break;
                    }
                    
                    // Update the legacy playerTransform for other systems that might need it
                    if (playerTransform != homingPlayerTransform)
                    {
                        playerTransform = homingPlayerTransform;
                        // Player reference updated
                // Debug.Log($"[{gameObject.name}] 🎯 Updated player reference in IdleThenAttack (Homing state)");
                    }
                    
                    Vector3 playerPosition = homingPlayerTransform.position;
                    float distToPlayer = Vector3.Distance(currentPos, playerPosition);
                    
                    // Check if we can see player
                    bool canSeePlayer = alwaysKnowPlayerPosition;
                    if (!alwaysKnowPlayerPosition)
                    {
                        Vector3 rayOrigin = currentPos + Vector3.up * raycastHeight;
                        Vector3 directionToPlayer = (playerPosition - currentPos).normalized;
                        RaycastHit hit;
                        if (Physics.Raycast(rayOrigin, directionToPlayer, out hit, playerDetectionRange))
                        {
                            if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform))
                            {
                                canSeePlayer = true;
                            }
                        }
                    }
                    
                    if (!canSeePlayer)
                    {
                        // Lost sight of player, return home
                        idleThenAttackState = IdleThenAttackState.Returning;
                        ResetDetectionSound(); // Reset sound flag for next detection
                        
                        // Retract arms
                        if (grabPlayerInHomingMode)
                        {
                            _inputModule.OnLeftArmDelegates?.Invoke(0f);
                            _inputModule.OnRightArmDelegates?.Invoke(0f);
                        }
                        break;
                    }
                    
                    // Normal homing behavior - set player as target
                    currentTarget = playerPosition;
                    hasTarget = true;
                    
                    // Arm extension when close
                    if (grabPlayerInHomingMode && distToPlayer <= armExtendDistance)
                    {
                        _inputModule.OnLeftArmDelegates?.Invoke(1f);
                        _inputModule.OnRightArmDelegates?.Invoke(1f);
                    }
                    else if (grabPlayerInHomingMode)
                    {
                        _inputModule.OnLeftArmDelegates?.Invoke(0f);
                        _inputModule.OnRightArmDelegates?.Invoke(0f);
                    }
                    
                    // Move toward player (no stop distance, always pursue)
                    MoveTowardsTarget();
                    break;
                    
                case IdleThenAttackState.Returning:
                    // Return to start position
                    float distToStart = Vector3.Distance(currentPos, idleThenAttackStartPosition);
                    
                    // Retract arms
                    if (grabPlayerInHomingMode)
                    {
                        _inputModule.OnLeftArmDelegates?.Invoke(0f);
                        _inputModule.OnRightArmDelegates?.Invoke(0f);
                    }
                    
                    if (distToStart <= idleThenAttackReturnDistance)
                    {
                        // Reached home point! Return to idle pacing behavior
                        idleThenAttackState = IdleThenAttackState.Idle;
                        
                        // Reset idle behavior so it starts fresh
                        hasTarget = false; // Clear old target
                        idleWaitTimer = 0f; // Start immediately, will pick new spot
                        
                        _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                    }
                    else
                    {
                        // Navigate back to start
                        currentTarget = idleThenAttackStartPosition;
                        hasTarget = true;
                        MoveTowardsTarget();
                    }
                    break;
            }
        }
        
        private void MoveTowardsTarget()
        {
            // Use PhysicalTorso position for accurate calculations
            Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            Vector3 directionToTarget = (currentTarget - currentPos).normalized;
            directionToTarget.y = 0; // Keep on horizontal plane
            
            // Check for gaps ahead and jump if needed
            // Check if jumping is enabled for current mode
            bool canJump = enableAutoJump && _inputModule.IsOnFloor;
            
            // Disable jumping based on mode-specific settings
            if (currentMode == AIMode.Idle && !canJumpInIdleMode)
            {
                canJump = false;
            }
            else if (currentMode == AIMode.Roam && !canJumpInRoamMode)
            {
                canJump = false;
            }
            else if (currentMode == AIMode.Explore && !canJumpInExploreMode)
            {
                canJump = false;
            }
            else if (currentMode == AIMode.IdleThenAttack)
            {
                // In Idle Then Attack: use idle jump setting when idle/returning, always allow when homing
                if (idleThenAttackState != IdleThenAttackState.Homing && !canJumpInIdleMode)
                {
                    canJump = false;
                }
                // When in Homing state, always allow jumping (no restrictions)
            }
            
            if (canJump)
            {
                CheckForGapAndJump(directionToTarget);
            }
            
            // BOUNDARY SAFETY: If we're at the boundary edge and target is outside, stop moving
            if (boundaryBox != null && !exploreIgnoresBoundary)
            {
                Vector3 nextPosition = currentPos + directionToTarget * 0.5f; // Look ahead slightly
                if (!IsWithinBoundary(nextPosition))
                {
                    // Next step would take us outside - pick a new target
                    hasTarget = false;
                    _inputModule.OnMoveDelegates?.Invoke(Vector2.zero);
                    _inputModule.OnRunDelegates?.Invoke(false);
                    return;
                }
            }
            
            // Convert 3D direction to 2D input for InputModule
            Vector2 moveInput = ConvertDirectionToInput(directionToTarget);
            
            // Feed movement input to InputModule
            _inputModule.OnMoveDelegates?.Invoke(moveInput);
            
            // Tell it to run if enabled (or if runOnlyWhenChasing is enabled and currently chasing)
            bool isChasing = IsCurrentlyChasing();
            bool shouldRun = alwaysRun || (runOnlyWhenChasing && isChasing);
            _inputModule.OnRunDelegates?.Invoke(shouldRun);
            
            // ===== STUCK DETECTION (for non-jumping AI) =====
            if (jumpIfStuck && !enableAutoJump && _inputModule.IsOnFloor)
            {
                // Track if AI is trying to move
                bool tryingToMove = moveInput.magnitude > 0.1f;
                
                if (tryingToMove)
                {
                    // Check if position has changed significantly
                    float distanceMoved = Vector3.Distance(currentPos, lastPosition);
                    
                    if (distanceMoved < STUCK_MOVEMENT_THRESHOLD)
                    {
                        // Not moving much despite trying - increment stuck timer
                        jumpIfStuckTimer += Time.deltaTime;
                        
                        // If stuck for too long, force a jump
                        if (jumpIfStuckTimer >= stuckTimeThreshold && Time.time - lastJumpTime > jumpCooldown)
                        {
                            // Trigger jump via InputModule
                            _inputModule.OnJumpDelegates?.Invoke();
                            lastJumpTime = Time.time;
                            jumpIfStuckTimer = 0f; // Reset timer after jumping
                        }
                    }
                    else
                    {
                        // Making progress - reset stuck timer
                        jumpIfStuckTimer = 0f;
                    }
                }
                else
                {
                    // Not trying to move - reset stuck timer
                    jumpIfStuckTimer = 0f;
                }
                
                // Update last position for next frame
                lastPosition = currentPos;
            }
            
            // Update current movement direction for next frame
            if (directionToTarget.magnitude > 0.1f)
            {
                currentMovementDirection = directionToTarget;
            }
        }
        
        /// <summary>
        /// Detects gaps ahead and triggers jump if necessary
        /// Only jumps over actual gaps/cliffs, not slopes
        /// </summary>
        private void CheckForGapAndJump(Vector3 moveDirection)
        {
            // Skip if jump is on cooldown
            if (Time.time - lastJumpTime < jumpCooldown)
                return;
            
            // Check multiple points ahead for gaps
            float[] checkDistances = { 0.5f, 1.0f, 1.5f };
            bool gapDetected = false;
            
            // Use PhysicalTorso position for accurate gap detection
            Vector3 currentPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            Vector3 startPos = currentPos + Vector3.up * 0.5f; // Check from waist height
            
            foreach (float distance in checkDistances)
            {
                // Only check up to our configured gap check distance
                if (distance > gapCheckDistance)
                    break;
                
                Vector3 checkPoint = startPos + moveDirection * distance;
                
                // Raycast straight down from check point
                RaycastHit hit;
                bool hitGround = Physics.Raycast(checkPoint, Vector3.down, out hit, 10f, ~0, QueryTriggerInteraction.Ignore);
                
                if (!hitGround)
                {
                    // No ground detected - likely a cliff
                    gapDetected = true;
                    Debug.DrawLine(checkPoint, checkPoint + Vector3.down * 10f, Color.red, 0.5f);
                    break;
                }
                else
                {
                    float dropHeight = (startPos.y - hit.point.y);
                    
                    // If dropHeight is negative, we're going UP (ramp/slope) - this is NOT a gap!
                    if (dropHeight < 0)
                    {
                        // Upward slope - safe to walk, visualize as green
                        Debug.DrawLine(checkPoint, hit.point, Color.green, 0.1f);
                        continue;
                    }
                    
                    // Small step down - safe to walk over
                    if (dropHeight <= maxStepHeight)
                    {
                        Debug.DrawLine(checkPoint, hit.point, Color.green, 0.1f);
                        continue;
                    }
                    
                    // Significant drop detected - this is a gap!
                    if (dropHeight >= minGapDepth)
                    {
                        gapDetected = true;
                        Debug.DrawLine(checkPoint, hit.point, Color.yellow, 0.2f);
                        break;
                    }
                    
                    // Medium drop (between step and gap) - treat as safe
                    Debug.DrawLine(checkPoint, hit.point, Color.green, 0.1f);
                }
            }
            
            // If gap detected, check if target is beyond the gap
            if (gapDetected)
            {
                // Check if our target is on the other side of the gap
                Vector3 targetPos = new Vector3(currentTarget.x, 0, currentTarget.z);
                Vector3 currentPos2D = new Vector3(currentPos.x, 0, currentPos.z);
                float distanceToTarget = Vector3.Distance(currentPos2D, targetPos);
                
                // If target is reasonably far (beyond the gap), jump!
                if (distanceToTarget > gapCheckDistance)
                {
                    TriggerJump();
                }
            }
        }
        
        /// <summary>
        /// Triggers a jump by invoking the jump delegate
        /// </summary>
        private void TriggerJump()
        {
            // Spawn protection: Don't allow jumping for the first few seconds after spawn
            if (Time.time - spawnTime < NO_JUMP_AFTER_SPAWN_DURATION)
                return;
            
            // Make sure we're on the ground and cooldown has passed
            if (!_inputModule.IsOnFloor)
                return;
            
            if (Time.time - lastJumpTime < jumpCooldown)
                return;
            
            // Invoke the jump delegate (DefaultBehaviour is listening to this)
            _inputModule.OnJumpDelegates?.Invoke();
            
            lastJumpTime = Time.time;
            
        }
        
        private void FindIdleTarget()
        {
            // IDLE MODE: Pick a random point within small radius of HOME POINT (spawn position)
            // Home point = where ragdoll first spawned
            // It will never go further than idlePaceRadius from this home point
            
            Vector2 randomCircle = Random.insideUnitCircle * idlePaceRadius;
            Vector3 randomPoint = spawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            // Raycast down to find ground level
            RaycastHit hit;
            if (Physics.Raycast(randomPoint + Vector3.up * 10f, Vector3.down, out hit, 20f))
            {
                currentTarget = hit.point;
            }
            else
            {
                // Fallback to horizontal plane
                currentTarget = new Vector3(randomPoint.x, transform.position.y, randomPoint.z);
            }
            
            hasTarget = true;
            Debug.DrawLine(transform.position, currentTarget, Color.gray, 2f);
        }
        
        private void FindRoamTarget()
        {
            // Pick a random point within the boundary box
            // Roam mode ALWAYS uses boundary box (centered on spawn point)
            
            Vector3 randomPoint;
            
            if (boundaryBox != null)
            {
                // Pick random point within the boundary box bounds
                Bounds bounds = boundaryBox.GetWorldBounds();
                
                // Random position within the bounds
                randomPoint = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    bounds.center.y, // Keep on ground level
                    Random.Range(bounds.min.z, bounds.max.z)
                );
            }
            else
            {
                // Fallback: pick point near current position (shouldn't happen in Roam mode)
                Vector2 randomCircle = Random.insideUnitCircle * 10f;
                randomPoint = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            }
            
            // Raycast down to find ground level
            RaycastHit hit;
            if (Physics.Raycast(randomPoint + Vector3.up * 10f, Vector3.down, out hit, 20f))
            {
                currentTarget = hit.point;
            }
            else
            {
                // Fallback to horizontal plane
                currentTarget = new Vector3(randomPoint.x, transform.position.y, randomPoint.z);
            }
            
            // BOUNDARY: Clamp target to stay within bounds (redundant but safe)
            currentTarget = ClampToBoundary(currentTarget);
            
            hasTarget = true;
            Debug.DrawLine(transform.position, currentTarget, Color.yellow, 2f);
        }
        
        /// <summary>
        /// Converts a 3D world direction to a 2D input vector relative to the camera/character
        /// </summary>
        private Vector2 ConvertDirectionToInput(Vector3 worldDirection)
        {
            // Get the character's forward and right vectors (on the horizontal plane)
            Vector3 characterForward = transform.forward;
            characterForward.y = 0;
            characterForward.Normalize();
            
            Vector3 characterRight = transform.right;
            characterRight.y = 0;
            characterRight.Normalize();
            
            // Project the desired direction onto the character's axes
            float forwardAmount = Vector3.Dot(worldDirection, characterForward);
            float rightAmount = Vector3.Dot(worldDirection, characterRight);
            
            return new Vector2(rightAmount, forwardAmount);
        }
        
        // ========== BOUNDARY CONTAINMENT METHODS ==========
        
        /// <summary>
        /// Clamps a position to stay within the boundary box
        /// </summary>
        Vector3 ClampToBoundary(Vector3 position)
        {
            if (boundaryBox == null)
                return position;
            
            return boundaryBox.ClampPositionToBox(position);
        }
        
        /// <summary>
        /// Checks if a position is within the boundary box
        /// </summary>
        bool IsWithinBoundary(Vector3 position)
        {
            if (boundaryBox == null)
                return true;
            
            return boundaryBox.IsPositionInsideBox(position);
        }
        
        /// <summary>
        /// Find a new navigation target using raycast-based pathfinding (Mimic-style)
        /// </summary>
        private void FindNewTarget(bool emergencyMode = false)
        {
            // Use PhysicalTorso position for accurate raycasting origin
            Vector3 torsoPos = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
            Vector3 origin = torsoPos + Vector3.up * raycastHeight;
            float angleStep = 360f / totalRays; // 360 degree circle for full spatial awareness
            int numberOfGroups = totalRays / raysPerGroup;
            
            // Get the character's current forward direction
            Vector3 characterForward = _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.transform.forward : transform.forward;
            characterForward.y = 0;
            characterForward.Normalize();
            
            // Add current position to memory
            if (memoryEnabled)
            {
                visitedPositions.Add(transform.position);
                if (visitedPositions.Count > memorySize)
                {
                    visitedPositions.RemoveAt(0);
                }
            }
            
            // Store ray distances and directions
            float[] rayDistances = new float[totalRays];
            Vector3[] rayDirections = new Vector3[totalRays];
            
            // Cast all rays in 360-degree circle for full spatial awareness (like Mimic)
            for (int i = 0; i < totalRays; i++)
            {
                // Calculate angle in full circle (0 to 360 degrees)
                float angle = i * angleStep;
                
                // Create direction relative to character's forward (0 degrees = forward)
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * characterForward;
                rayDirections[i] = direction;
                
                RaycastHit hit;
                if (Physics.Raycast(origin, direction, out hit, raycastDistance, -1, QueryTriggerInteraction.Ignore))
                {
                    rayDistances[i] = hit.distance;
                    environmentSnapshot[i] = hit.point;
                }
                else
                {
                    rayDistances[i] = raycastDistance;
                    environmentSnapshot[i] = origin + direction * raycastDistance;
                }
            }
            
            // Find the best group
            float bestGroupScore = 0f;
            int bestGroupIndex = 0;
            List<KeyValuePair<int, float>> groupScores = new List<KeyValuePair<int, float>>();
            
            for (int group = 0; group < numberOfGroups; group++)
            {
                int middleRayIndex = group * raysPerGroup + (raysPerGroup / 2);
                Vector3 groupDirection = rayDirections[middleRayIndex];
                
                // Calculate directional bias
                float directionBias = 1f;
                if (forwardBiasEnabled && !emergencyMode)
                {
                    float alignment = Vector3.Dot(groupDirection, currentMovementDirection);
                    if (alignment >= 0)
                        directionBias = forwardBiasMultiplier;
                    else if (alignment < -0.7f)
                        directionBias = 0.5f;
                }
                
                // Sum distances for this group
                float groupTotalDistance = 0f;
                for (int i = 0; i < raysPerGroup; i++)
                {
                    int rayIndex = (group * raysPerGroup + i) % totalRays;
                    groupTotalDistance += rayDistances[rayIndex];
                }
                
                // Calculate target position for this group
                Vector3 candidatePosition = CalculateTargetFromGroup(origin, group, rayDistances, rayDirections);
                
                // Check exploration score
                float explorationScore = 1f;
                if (memoryEnabled && !emergencyMode)
                {
                    explorationScore = CalculateExplorationScore(candidatePosition);
                }
                
                // Calculate final score
                float finalScore = groupTotalDistance * directionBias * explorationScore;
                
                // Store for potential suboptimal selection
                groupScores.Add(new KeyValuePair<int, float>(group, finalScore));
                
                if (finalScore > bestGroupScore)
                {
                    bestGroupScore = finalScore;
                    bestGroupIndex = group;
                }
            }
            
            // 5% chance to pick a middling option (organic behavior)
            if (!emergencyMode && groupScores.Count > 3 && Random.value < suboptimalPathChance)
            {
                groupScores.Sort((a, b) => b.Value.CompareTo(a.Value));
                int middleStart = groupScores.Count / 3;
                int middleEnd = (groupScores.Count * 2) / 3;
                int middleRangeSize = middleEnd - middleStart;
                
                if (middleRangeSize > 0)
                {
                    int randomMiddleIndex = middleStart + Random.Range(0, middleRangeSize);
                    bestGroupIndex = groupScores[randomMiddleIndex].Key;
                }
            }
            
            // Set the target
            currentTarget = CalculateTargetFromGroup(origin, bestGroupIndex, rayDistances, rayDirections);
            currentTarget.y = transform.position.y; // Keep at same height
            
            // BOUNDARY: Validate target is within bounds (only if not ignoring boundary)
            if (!exploreIgnoresBoundary && boundaryBox != null)
            {
                // Check if target is within boundary
                if (!IsWithinBoundary(currentTarget))
                {
                    // Target is outside boundary - try other groups instead of clamping
                    // Sort groups by score and find first one that's within boundary
                    groupScores.Sort((a, b) => b.Value.CompareTo(a.Value));
                    
                    bool foundValidTarget = false;
                    for (int i = 0; i < groupScores.Count; i++)
                    {
                        Vector3 candidateTarget = CalculateTargetFromGroup(origin, groupScores[i].Key, rayDistances, rayDirections);
                        candidateTarget.y = transform.position.y;
                        
                        if (IsWithinBoundary(candidateTarget))
                        {
                            currentTarget = candidateTarget;
                            foundValidTarget = true;
                            break;
                        }
                    }
                    
                    // If no valid targets found, stay in place or pick a random point inside boundary
                    if (!foundValidTarget)
                    {
                        // Pick a random point within the boundary box as fallback (stay away from edges)
                        if (boundaryBox != null)
                        {
                            Bounds bounds = boundaryBox.GetWorldBounds();
                            Vector3 size = bounds.size * 0.8f; // Stay 20% away from edges
                            Vector3 safeMin = bounds.center - size * 0.5f;
                            Vector3 safeMax = bounds.center + size * 0.5f;
                            
                            currentTarget = new Vector3(
                                Random.Range(safeMin.x, safeMax.x),
                                transform.position.y,
                                Random.Range(safeMin.z, safeMax.z)
                            );
                        }
                        else
                        {
                            // No boundary box, pick near current position
                            Vector2 randomCircle = Random.insideUnitCircle * 5f;
                            currentTarget = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
                        }
                    }
                }
            }
            
            hasTarget = true;
            
            // Draw debug line
            Debug.DrawLine(transform.position, currentTarget, Color.cyan, 2f);
        }
        
        private Vector3 CalculateTargetFromGroup(Vector3 origin, int groupIndex, float[] rayDistances, Vector3[] rayDirections)
        {
            Vector3 targetPosition = Vector3.zero;
            int raysInGroup = 0;
            
            for (int i = 0; i < raysPerGroup; i++)
            {
                int rayIndex = (groupIndex * raysPerGroup + i) % totalRays;
                float targetDistance = rayDistances[rayIndex] * targetDepthRatio;
                Vector3 pointAlongRay = origin + rayDirections[rayIndex] * targetDistance;
                
                targetPosition += pointAlongRay;
                raysInGroup++;
            }
            
            targetPosition /= raysInGroup;
            return targetPosition;
        }
        
        private float CalculateExplorationScore(Vector3 candidatePosition)
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
        
        /// <summary>
        /// Finds the nearest waypoint to a given position (for Path Then Attack return logic)
        /// </summary>
        private int FindNearestWaypointIndex(Vector3 position)
        {
            if (pathWaypoints == null || pathWaypoints.Count == 0)
                return 1; // Default to first waypoint after spawn
            
            int nearestIndex = 1; // Start at 1 (skip spawn point)
            float nearestDistance = float.MaxValue;
            
            // Check all waypoints except spawn point (index 0)
            for (int i = 1; i < pathWaypoints.Count; i++)
            {
                float distance = Vector3.Distance(
                    new Vector3(position.x, 0, position.z),
                    new Vector3(pathWaypoints[i].x, 0, pathWaypoints[i].z)
                );
                
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }
            
            return nearestIndex;
        }
        
        private void OnDrawGizmos()
        {
            // Draw mode indicator
            Color modeColor = GetModeColor();
            Gizmos.color = modeColor;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);
            
            if (!Application.isPlaying) return;
            
            // Draw current target
            if (hasTarget)
            {
                Gizmos.color = modeColor;
                Gizmos.DrawWireSphere(currentTarget, 0.5f);
                Gizmos.DrawLine(transform.position, currentTarget);
            }
            
            // Draw "HOME POINT" circle for idle mode (ragdoll stays within this gray circle)
            if (currentMode == AIMode.Idle)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f); // Gray circle = home area
                DrawCircle(spawnPosition, idlePaceRadius, 16);
                
                // Draw home point marker
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(spawnPosition, 0.3f);
            }
            
            // Draw PATH for Path Movement mode (multiple waypoints)
            // Note: pathWaypoints[0] is always the spawn position (Point 0)
            if (currentMode == AIMode.PathMovement && pathWaypoints != null && pathWaypoints.Count > 0)
            {
                Vector3 currentPos = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                
                // Draw all waypoints (including Point 0 which is spawn position)
                for (int i = 0; i < pathWaypoints.Count; i++)
                {
                    Vector3 waypoint = pathWaypoints[i];
                    
                    // Special coloring for Point 0 (spawn position)
                    if (i == 0)
                    {
                        // Point 0 is always white (spawn position)
                        if (i == currentWaypointIndex)
                            Gizmos.color = new Color(1f, 1f, 1f, 1f); // Bright white when current target
                        else if (i < currentWaypointIndex)
                            Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.7f); // Gray when completed
                        else
                            Gizmos.color = Color.white; // White when upcoming
                    }
                    // Color based on status for other waypoints
                    else if (i < currentWaypointIndex)
                    {
                        // Completed waypoint
                        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Gray
                    }
                    else if (i == currentWaypointIndex)
                    {
                        // Current target waypoint
                        Gizmos.color = Color.green; // Bright green
                    }
                    else
                    {
                        // Upcoming waypoint
                        Gizmos.color = new Color(1f, 1f, 0f, 0.7f); // Yellow
                    }
                    
                    Gizmos.DrawWireSphere(waypoint, 0.5f);
                    Gizmos.DrawSphere(waypoint, 0.3f);
                }
                
                // Draw path lines connecting all waypoints
                Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Semi-transparent green
                for (int i = 0; i < pathWaypoints.Count - 1; i++)
                {
                    Gizmos.DrawLine(pathWaypoints[i], pathWaypoints[i + 1]);
                }
                
                // Draw line from current position to current waypoint
                if (currentWaypointIndex < pathWaypoints.Count)
                {
                    Gizmos.color = new Color(0f, 1f, 0f, 0.7f); // Brighter green for active segment
                    Gizmos.DrawLine(currentPos, pathWaypoints[currentWaypointIndex]);
                }
                
                // Draw return path if "Return to Start and Stop" is enabled
                if (pathReturnToStartAndStop && pathWaypoints.Count > 1)
                {
                    Gizmos.color = new Color(0f, 1f, 1f, 0.4f); // Cyan for return path
                    Gizmos.DrawLine(pathWaypoints[pathWaypoints.Count - 1], pathWaypoints[0]); // Back to Point 0
                    
                    // If currently returning, draw active return line
                    if (pathIsReturningToStart)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(currentPos, pathWaypoints[0]); // Returning to Point 0
                    }
                }
                
                // Draw loop indicator if "Loop Forever" is enabled
                if (pathLoopForever && pathWaypoints.Count > 1)
                {
                    Gizmos.color = new Color(1f, 0f, 1f, 0.4f); // Magenta for loop
                    Gizmos.DrawLine(pathWaypoints[pathWaypoints.Count - 1], pathWaypoints[0]); // Loop back to Point 0
                }
                
                // Draw One Way Path Then Behavior Switch indicator
                if (pathOneWayThenBehaviorSwitch && pathWaypoints.Count > 0)
                {
                    Vector3 lastWaypoint = pathWaypoints[pathWaypoints.Count - 1];
                    
                    // Draw final waypoint with special color indicating mode switch
                    Color switchColor = GetModeColor(pathFinalBehaviorMode);
                    Gizmos.color = switchColor;
                    
                    // Larger sphere at final waypoint to indicate mode switch point
                    Gizmos.DrawWireSphere(lastWaypoint, 1.2f);
                    Gizmos.DrawSphere(lastWaypoint + Vector3.up * 0.5f, 0.4f);
                    
                    // Draw text label indicator (small marker above final waypoint)
                    Gizmos.color = new Color(switchColor.r, switchColor.g, switchColor.b, 0.8f);
                    Gizmos.DrawLine(lastWaypoint, lastWaypoint + Vector3.up * 2f);
                    Gizmos.DrawWireSphere(lastWaypoint + Vector3.up * 2f, 0.3f);
                }
            }
            
            // Draw PATH MOVEMENT → FINISH visualization (when navigating to finish after path completion)
            if (currentMode == AIMode.PathMovement && pathIsNavigatingToFinish && pathFinishFlagTransform != null)
            {
                Vector3 currentPos = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                Vector3 finishPos = pathFinishFlagTransform.position;
                
                // Draw finish flag position (yellow-green)
                Gizmos.color = new Color(0.5f, 1f, 0f); // Yellow-green
                Gizmos.DrawWireSphere(finishPos, 1f);
                Gizmos.DrawSphere(finishPos, 0.5f);
                
                // Draw line from current position to finish (bright yellow-green)
                Gizmos.color = new Color(0.5f, 1f, 0f, 0.8f); // Bright yellow-green
                Gizmos.DrawLine(currentPos, finishPos);
            }
            
            // Draw PATH THEN ATTACK visualization (if enabled)
            if (currentMode == AIMode.PathMovement && pathThenAttackEnabled)
            {
                Vector3 torsoPos = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                Vector3 forward = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.transform.forward : transform.forward;
                forward.y = 0;
                forward.Normalize();
                
                // Draw 180° front vision cone (detection radius)
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // Orange, semi-transparent
                DrawHalfCircle(torsoPos, forward, pathThenAttackDetectionRadius, 32);
                
                // Draw lose radius (lighter orange)
                Gizmos.color = new Color(1f, 0.7f, 0f, 0.15f); // Light orange
                DrawHalfCircle(torsoPos, forward, pathThenAttackLoseRadius, 32);
                
                // Draw forward direction indicator
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f); // Orange
                Gizmos.DrawLine(torsoPos, torsoPos + forward * pathThenAttackDetectionRadius * 0.5f);
                
                // If tracking, draw line to player and grab radius
                if (pathThenAttackIsTracking && playerTransform != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(torsoPos, playerTransform.position);
                    
                    // Draw grab distance when tracking
                    if (grabPlayerInHomingMode)
                    {
                        Gizmos.color = new Color(1f, 0f, 1f, 0.3f); // Magenta for arm extend range
                        DrawCircle(torsoPos, armExtendDistance, 24);
                    }
                }
            }
            
            // Draw RACE TO FINISH visualization
            if (currentMode == AIMode.RaceToFinish && finishFlagTransform != null)
            {
                Vector3 currentPos = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                Vector3 finishPos = finishFlagTransform.position;
                
                // Draw finish flag position (bright cyan)
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(finishPos, 1f);
                Gizmos.DrawSphere(finishPos, 0.5f);
                
                // Draw spawn position (white)
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(spawnPosition, 0.3f);
                
                // Draw direct line from spawn to finish
                Gizmos.color = new Color(0f, 1f, 1f, 0.3f); // Semi-transparent cyan
                Gizmos.DrawLine(spawnPosition, finishPos);
                
                // Draw line from current position to finish (brighter)
                Gizmos.color = new Color(0f, 1f, 1f, 0.8f); // Bright cyan
                Gizmos.DrawLine(currentPos, finishPos);
            }
            
            // Draw memory trail for explore mode
            if (currentMode == AIMode.Explore && memoryEnabled && visitedPositions != null && visitedPositions.Count > 0)
            {
                for (int i = 0; i < visitedPositions.Count; i++)
                {
                    float age = (float)i / visitedPositions.Count;
                    Gizmos.color = new Color(modeColor.r, modeColor.g, modeColor.b, 0.2f + age * 0.4f);
                    Gizmos.DrawSphere(visitedPositions[i], 0.15f);
                }
            }
            
            // Draw 360-degree vision circle for Explore mode (like Mimic)
            if (currentMode == AIMode.Explore && Application.isPlaying)
            {
                // Draw a full circle to show 360-degree raycast awareness
                Gizmos.color = new Color(modeColor.r, modeColor.g, modeColor.b, 0.15f);
                Vector3 torsoPos = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                DrawCircle(torsoPos, raycastDistance, 48);
                
                // Draw forward indicator
                Vector3 forward = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.transform.forward : transform.forward;
                forward.y = 0;
                forward.Normalize();
                Gizmos.color = new Color(modeColor.r, modeColor.g, modeColor.b, 0.3f);
                Gizmos.DrawLine(torsoPos, torsoPos + forward * raycastDistance * 0.3f);
            }
            
            // Draw player detection for homing mode
            if (currentMode == AIMode.Homing && playerTransform != null)
            {
                if (!alwaysKnowPlayerPosition)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                    Gizmos.DrawLine(transform.position + Vector3.up * raycastHeight, playerTransform.position);
                    Gizmos.DrawWireSphere(transform.position, playerDetectionRange);
                }
                
                // Draw grab distance radius if grabbing is enabled
                if (grabPlayerInHomingMode)
                {
                    Gizmos.color = new Color(1f, 0f, 1f, 0.3f); // Magenta for arm extend range
                    DrawCircle(transform.position, armExtendDistance, 24);
                }
            }
            
            // Draw detection radii for Explore Then Attack mode
            if (currentMode == AIMode.ExploreThenAttack)
            {
                Vector3 torsoPos = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                
                // Detection radius (when AI engages)
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange
                DrawCircle(torsoPos, exploreThenAttackDetectionRadius, 32);
                
                // Lose radius (when AI disengages)
                Gizmos.color = new Color(1f, 0.7f, 0f, 0.2f); // Light orange
                DrawCircle(torsoPos, exploreThenAttackLoseRadius, 32);
                
                // Draw 360-degree vision circle for exploration phase
                if (!exploreThenAttackIsTracking)
                {
                    Gizmos.color = new Color(0f, 1f, 1f, 0.15f); // Cyan
                    DrawCircle(torsoPos, raycastDistance, 48);
                }
                
                // Draw memory trail
                if (memoryEnabled && visitedPositions != null && visitedPositions.Count > 0)
                {
                    for (int i = 0; i < visitedPositions.Count; i++)
                    {
                        float age = (float)i / visitedPositions.Count;
                        Gizmos.color = new Color(0f, 1f, 1f, 0.2f + age * 0.4f); // Cyan memory dots
                        Gizmos.DrawSphere(visitedPositions[i], 0.15f);
                    }
                }
                
                // Draw grab distance when tracking
                if (exploreThenAttackIsTracking && grabPlayerInHomingMode)
                {
                    Gizmos.color = new Color(1f, 0f, 1f, 0.3f); // Magenta for arm extend range
                    DrawCircle(torsoPos, armExtendDistance, 24);
                }
            }
            
            // Draw detection radii for Roam Then Attack mode
            if (currentMode == AIMode.RoamThenAttack)
            {
                Vector3 torsoPos = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                
                // Detection radius (when AI engages)
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange
                DrawCircle(torsoPos, roamThenAttackDetectionRadius, 32);
                
                // Lose radius (when AI disengages)
                Gizmos.color = new Color(1f, 0.7f, 0f, 0.2f); // Light orange
                DrawCircle(torsoPos, roamThenAttackLoseRadius, 32);
                
                // Roam area is shown by boundary box (no separate roam radius)
                
                // Draw grab distance when tracking
                if (roamThenAttackIsTracking && grabPlayerInHomingMode)
                {
                    Gizmos.color = new Color(1f, 0f, 1f, 0.3f); // Magenta for arm extend range
                    DrawCircle(torsoPos, armExtendDistance, 24);
                }
            }
            
            // Draw HOME POINT and VISION CONE for Idle Then Attack mode (NO boundary box)
            if (currentMode == AIMode.IdleThenAttack)
            {
                Vector3 torsoPos = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.position : transform.position;
                Vector3 forward = _activeRagdoll != null && _activeRagdoll.PhysicalTorso != null ? _activeRagdoll.PhysicalTorso.transform.forward : transform.forward;
                forward.y = 0;
                forward.Normalize();
                
                // Draw home point pace area when idle
                if (idleThenAttackState == IdleThenAttackState.Idle)
                {
                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f); // Gray circle = home area
                    DrawCircle(spawnPosition, idlePaceRadius, 16);
                    
                    // Draw home point marker
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(spawnPosition, 0.3f);
                    
                    // Draw vision cone (90 degrees)
                    Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.2f); // Light gray
                    float halfAngle = idleVisionConeAngle / 2f;
                    
                    // Draw cone edges
                    Vector3 leftEdge = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward;
                    Vector3 rightEdge = Quaternion.AngleAxis(halfAngle, Vector3.up) * forward;
                    
                    Gizmos.DrawLine(torsoPos, torsoPos + leftEdge * idleThenAttackDetectionRange);
                    Gizmos.DrawLine(torsoPos, torsoPos + rightEdge * idleThenAttackDetectionRange);
                    Gizmos.DrawLine(torsoPos, torsoPos + forward * idleThenAttackDetectionRange);
                }
                
                // Draw line to home point when returning
                if (idleThenAttackState == IdleThenAttackState.Returning)
                {
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // Orange
                    Gizmos.DrawLine(torsoPos, idleThenAttackStartPosition);
                }
                
                // Draw line to player when homing
                if (idleThenAttackState == IdleThenAttackState.Homing && playerTransform != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(torsoPos, playerTransform.position);
                }
            }
        }
        
        private Color GetModeColor()
        {
            return GetModeColor(currentMode);
        }
        
        /// <summary>
        /// Get the color for a specific AI mode (for gizmos and visualization)
        /// </summary>
        private Color GetModeColor(AIMode mode)
        {
            switch (mode)
            {
                case AIMode.Idle: return Color.gray;
                case AIMode.Roam: return Color.yellow;
                case AIMode.Explore: return Color.cyan;
                case AIMode.Homing: return Color.red;
                case AIMode.PathMovement: return Color.green;
                case AIMode.RaceToFinish: return Color.cyan; // Cyan for racing
                case AIMode.RedLightGreenLight: return Color.magenta; // Magenta for RLGL mode
                case AIMode.ExploreThenAttack: return exploreThenAttackIsTracking ? Color.red : Color.cyan; // Red when tracking, cyan when exploring
                case AIMode.RoamThenAttack: return roamThenAttackIsTracking ? Color.red : Color.yellow; // Red when tracking, yellow when roaming
                case AIMode.IdleThenAttack: 
                    return idleThenAttackState == IdleThenAttackState.Homing ? Color.red : 
                           idleThenAttackState == IdleThenAttackState.Returning ? new Color(1f, 0.5f, 0f) : // Orange when returning
                           Color.gray; // Gray when idle
                default: return Color.white;
            }
        }
        
        /// <summary>
        /// Checks if the AI is currently in a chase/attack state
        /// </summary>
        private bool IsCurrentlyChasing()
        {
            bool result = false;
            
            switch (currentMode)
            {
                case AIMode.Homing:
                    result = true; // Always chasing in homing mode
                    break;
                    
                case AIMode.ExploreThenAttack:
                    result = exploreThenAttackIsTracking;
                    break;
                    
                case AIMode.RoamThenAttack:
                    result = roamThenAttackIsTracking;
                    break;
                    
                case AIMode.IdleThenAttack:
                    result = idleThenAttackState == IdleThenAttackState.Homing;
                    break;
                    
                case AIMode.PathMovement:
                    result = pathThenAttackIsTracking;
                    break;
                    
                default:
                    result = false; // Not in a chase mode
                    break;
            }
            
            return result;
        }
        
        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
        
        /// <summary>
        /// Draws a 180° half-circle (front vision cone) for Path Then Attack mode
        /// </summary>
        private void DrawHalfCircle(Vector3 center, Vector3 forward, float radius, int segments)
        {
            // Draw half-circle arc (180 degrees)
            float angleStep = 180f / segments;
            
            // Start from -90° (left edge of front half-circle)
            float startAngle = -90f;
            Vector3 prevPoint = center + Quaternion.AngleAxis(startAngle, Vector3.up) * forward * radius;
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = startAngle + (i * angleStep);
                Vector3 newPoint = center + Quaternion.AngleAxis(angle, Vector3.up) * forward * radius;
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
            
            // Draw left and right edge lines from center to arc
            Vector3 leftEdge = Quaternion.AngleAxis(-90f, Vector3.up) * forward;
            Vector3 rightEdge = Quaternion.AngleAxis(90f, Vector3.up) * forward;
            Gizmos.DrawLine(center, center + leftEdge * radius);
            Gizmos.DrawLine(center, center + rightEdge * radius);
        }
    }
}


