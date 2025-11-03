using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawner for AI Ragdolls. This component sits on the spawner object itself.
/// It spawns AI ragdoll instances that will respawn back to this spawner when killed.
/// </summary>
public class AIRagdollSpawner : MonoBehaviour
{
    // Awareness detection type enum
    public enum AwarenessType
    {
        None,            // No player detection (for modes that don't need it)
        ProximityRadius, // Detects player within 360° radius (distance only)
        RaycastVision,   // Detects player within vision cone using raycast (angle + distance)
        BoundaryBox      // Detects player when they enter the boundary box area
    }
    
    // ==================== MASTER SETTINGS ====================
    [Header("═══ MASTER SETTINGS ═══")]
    [Space(5)]
    
    [HideInInspector]
    [Tooltip("Index of the costume to spawn with (set by custom editor)")]
    public int selectedCostumeIndex = 0;
    
    [HideInInspector]
    [Tooltip("Name of the selected costume (for display)")]
    public string selectedCostumeName = "";
    
    [Tooltip("(LEGACY) Custom material/skin for spawned ragdolls - deprecated in favor of costume system")]
    public Material ragdollSkin;
    
    [Tooltip("AI can jump over gaps and obstacles")]
    public bool canJump = true;
    
    [Tooltip("AI can run (applies run speed multiplier)")]
    public bool canRun = true;
    
    [Tooltip("If Can Run is disabled, allow running only during chase/attack sequences")]
    public bool runOnlyWhenChasing = false;
    
    [Tooltip("AI cannot grab the player or any objects")]
    public bool disableGrabbing = false;
    
    [Tooltip("Touching the player instantly kills both player and AI")]
    public bool contactKillsInstantly = false;
    
    [Tooltip("If AI grabs player, holding for X seconds kills both")]
    public bool grabKillsPlayer = true;
    
    [Tooltip("Number of AIs that must grab the player simultaneously to trigger kill (1 = any single AI can kill)")]
    [Range(1, 10)]
    public int grabsRequiredToKill = 1;
    
    [Tooltip("Time in seconds AI must hold player before kill (only if Grab Kills Player is enabled)")]
    [Range(1f, 10f)]
    public float grabKillDuration = 5f;
    
    [Tooltip("Starting vignette intensity when AI grabs player")]
    [Range(0f, 1f)]
    public float vignetteStartIntensity = 0.3f;
    
    [Tooltip("Maximum vignette intensity at end of grab duration")]
    [Range(0f, 1f)]
    public float vignetteMaxIntensity = 1f;
    
    [Tooltip("Base walking speed")]
    public float walkSpeed = 2f;
    
    [Tooltip("Run speed multiplier (2 = twice as fast when running)")]
    public float runSpeedMultiplier = 2f;
    
    [Tooltip("Jump force applied to ragdoll")]
    public float jumpForce = 400f;
    
    [Tooltip("Cooldown between jumps (seconds)")]
    public float jumpCooldown = 0.3f;
    
    [Tooltip("AI will jump if stuck against obstacle (only applies when Can Jump is disabled)")]
    public bool jumpIfStuck = false;
    
    [Tooltip("Time stuck before jumping (seconds)")]
    [Range(0.5f, 5f)]
    public float stuckTimeThreshold = 2f;
    
    [Tooltip("Air steering force for mid-air control")]
    public float airSteeringForce = 150f;
    
    [Tooltip("AI respawns after death. If false, stays dead on ground.")]
    public bool shouldRespawn = true;
    
    [Tooltip("If true, AI will not respawn when killed - corpse stays on field permanently (like Red Light Green Light mode)")]
    public bool staysDeadIfShot = false;
    
    [Tooltip("Time (in seconds) that ragdoll parts stay visible after death before respawning")]
    [Range(0.1f, 10f)]
    public float deathDelay = 3f;
    
    [Header("Health System")]
    [Tooltip("Number of shots required to kill this AI (any hit counts as 1 damage)")]
    [Range(1, 20)]
    public int shotsToKill = 1;
    
    [Tooltip("How AI detects the player (modes with attack behaviors only)")]
    public AwarenessType awarenessType = AwarenessType.ProximityRadius;
    
    [Tooltip("Detection radius for proximity-based awareness (360° around AI)")]
    [Range(1f, 50f)]
    public float awarenessRadius = 12f;
    
    [Tooltip("Lose radius - player must go this far before AI stops chasing (should be > detection radius)")]
    [Range(1f, 50f)]
    public float awarenessLoseRadius = 15f;
    
    [Tooltip("Vision cone angle for raycast-based awareness (degrees)")]
    [Range(10f, 360f)]
    public float awarenessVisionAngle = 90f;
    
    [Tooltip("Max distance for raycast vision detection")]
    [Range(1f, 50f)]
    public float awarenessVisionDistance = 15f;
    
    // ==================== SPAWNER SETTINGS ====================
    [Header("═══ SPAWNER SETTINGS ═══")]
    [Space(5)]
    
    [Tooltip("The AI ragdoll prefab to spawn (should NOT have this spawner component)")]
    public GameObject aiRagdollPrefab;
    
    [Tooltip("Offset from this object's position where the ragdoll will spawn")]
    public Vector3 spawnOffset = Vector3.zero;
    
    [Tooltip("Should the ragdoll match this object's rotation when spawning?")]
    public bool useRotation = true;
    
    [Tooltip("Should spawn ragdoll(s) when the level starts?")]
    public bool spawnOnStart = true;
    
    [Tooltip("Number of ragdolls to spawn at level start")]
    [Range(0, 10)]
    public int initialSpawnCount = 1;
    
    [Tooltip("If true, spawn all initial ragdolls at once. If false, use 2-second delay between spawns")]
    public bool spawnAllAtOnce = false;
    
    [Tooltip("Maximum number of active ragdolls from this spawner at any time")]
    [Range(1, 10)]
    public int maxActiveRagdolls = 1;
    
    [Tooltip("If true, limit the total number of spawns (spawner will stop after limit is reached)")]
    public bool limitTotalSpawns = false;
    
    [Tooltip("Maximum total spawns allowed (only used if limitTotalSpawns is true)")]
    public int maxTotalSpawns = 10;
    
    // ==================== AUDIO CONFIGURATION ====================
    [Header("═══ AUDIO SETTINGS ═══")]
    [Space(5)]
    
    [Header("Kill Sounds")]
    [Tooltip("Sound to play when AI instantly kills player on contact (2D)")]
    public AudioClip contactKillSound;
    
    [Tooltip("Sound to play when AI kills player by grabbing (2D)")]
    public AudioClip grabKillSound;
    
    [Tooltip("Sound to play when AI dies (3D spatial)")]
    public AudioClip aiDeathSound;
    
    [Header("═══ PERFORMANCE: COLLIDER OPTIMIZATION ═══")]
    [Space(5)]
    
    [Tooltip("EXPERIMENTAL: Disable colliders on limbs (arms/legs) to reduce physics cost by ~70%. Only keeps colliders on: Head, Torso, Hands, Feet. May cause visual glitches but massive performance boost.")]
    public bool optimizeColliders = false;
    
    [Tooltip("Sound to play when AI gets shot/hit by bullet (2D audio - always audible)")]
    public AudioClip bulletHitSound;
    
    [Tooltip("Sound to play when AI gets headshot (2D audio - always audible). If null, uses bulletHitSound")]
    public AudioClip headshotSound;
    
    [Header("Visual Effects")]
    [Tooltip("Material to apply to AI when it dies (optional - e.g., red unlit material)")]
    public Material deathMaterial;
    
    [Tooltip("Enable particle effects when this AI gets shot by player")]
    public bool enableBulletImpactEffect = true;
    
    [Tooltip("Particle effect for KILLING BLOW (e.g., explosion) - spawned at bullet impact point on death (optional)")]
    public GameObject bulletImpactEffectPrefab;
    
    [Tooltip("Particle effect for DAMAGE (non-lethal hits) - spawned at bullet impact point when ragdoll survives (optional)")]
    public GameObject bulletDamageEffectPrefab;
    
    [Header("AI Behavior Sounds")]
    [Tooltip("Sound to play when AI detects/notices the player (3D spatial) - plays once per detection")]
    public AudioClip playerDetectionSound;
    
    [Header("Movement & Impact Sounds")]
    [Tooltip("Array of impact sounds for body part collisions (3D spatial)")]
    public AudioClip[] impactSounds;
    
    [Tooltip("Array of jump sounds (3D spatial)")]
    public AudioClip[] jumpSounds;
    
    [Tooltip("Optional separate landing sounds (3D spatial) - if empty, uses impact sounds")]
    public AudioClip[] landingSounds;
    
    [Header("Atmospheric Sounds")]
    [Tooltip("Wind/whoosh sound when AI is falling (looping, 3D spatial)")]
    public AudioClip windSound;
    
    [Header("Debug Settings")]
    [Tooltip("Enable verbose debug logging (disable for cleaner console)")]
    public bool enableVerboseLogging = false;
    
    [Tooltip("Looping sound while AI is in ragdoll mode (3D spatial)")]
    public AudioClip ragdollLoopSound;
    
    // ==================== MODE SELECTION ====================
    [Header("═══ AI MODE SELECTION ═══")]
    [Space(5)]
    
    [Tooltip("AI behavior mode for spawned ragdolls")]
    public ActiveRagdoll.RagdollAIController.AIMode aiMode = ActiveRagdoll.RagdollAIController.AIMode.Explore;
    
    // ==================== MODE-SPECIFIC SETTINGS (Shown conditionally by custom editor) ====================
    
    // IDLE MODE
    [HideInInspector] public float idlePaceRadius = 2.5f;
    [HideInInspector] public float idleWaitTime = 2f;
    [HideInInspector] public float idleVisionConeAngle = 90f;
    
    // ROAM MODE
    [HideInInspector] public float roamWaitTime = 3f;
    
    // EXPLORE MODE
    // (No special settings - uses same boundary logic as other modes)
    
    // PATH MOVEMENT MODE
    [HideInInspector] public List<Vector3> pathWaypoints = new List<Vector3>() { Vector3.zero };
    [HideInInspector] public float pathReachedDistance = 1f;
    [HideInInspector] public bool returnToStartAndStop = false;
    [HideInInspector] public bool loopPathForever = false;
    [HideInInspector] public int numberOfCycles = 0;
    [HideInInspector] public bool endAtFinishTrigger = false;
    [HideInInspector] public bool pathThenAttackEnabled = false;
    [HideInInspector] public bool respawnAtLastPathPoint = false; // If true, AI respawns at last reached waypoint instead of original spawn
    
    // Internal: Track last waypoint for respawn-at-last-point feature
    private int cachedLastWaypointIndex = 0;
    [HideInInspector] public float pathThenAttackDetectionRadius = 12f;
    [HideInInspector] public float pathThenAttackLoseRadius = 15f;
    [HideInInspector] public bool pathOneWayThenBehaviorSwitch = false;
    [HideInInspector] public PathFinalBehaviorMode pathFinalBehaviorMode = PathFinalBehaviorMode.Idle;
    
    public enum PathFinalBehaviorMode
    {
        Idle,
        Roam,
        Explore,
        Homing
    }
    
    // HOMING MODE
    [HideInInspector] public GameObject targetObject;
    [HideInInspector] public bool alwaysKnowPlayerPosition = true;
    [HideInInspector] public float homingStopDistance = 0f;
    [HideInInspector] public bool grabPlayerInHomingMode = true;
    [HideInInspector] public float armExtendDistance = 5f;
    
    // EXPLORE THEN ATTACK MODE
    [HideInInspector] public float exploreThenAttackDetectionRadius = 12f;
    [HideInInspector] public float exploreThenAttackLoseRadius = 15f;
    
    // ROAM THEN ATTACK MODE
    [HideInInspector] public float roamThenAttackDetectionRadius = 12f;
    [HideInInspector] public float roamThenAttackLoseRadius = 15f;
    
    // IDLE THEN ATTACK MODE
    [HideInInspector] public float idleThenAttackDetectionRange = 15f;
    [HideInInspector] public float idleThenAttackReturnDistance = 1f;
    
    // SHARED SETTINGS (Used by multiple modes)
    [HideInInspector] public float gapCheckDistance = 1.5f;
    
    // GIZMO SETTINGS
    [HideInInspector] public Color gizmoColor = new Color(0f, 1f, 1f, 1f);
    [HideInInspector] public float gizmoSize = 1f;

    // Track spawned ragdolls
    private int activeRagdollCount = 0;
    private int totalSpawnsCount = 0;
    
    // Cached references
    private BattleRoyaleManager _cachedBattleManager;
    
    // Audio
    private AudioSource _audioSource;

    private void Awake()
    {
        // Setup audio source for contact kill sounds
        SetupAudio();
        
        // Cache BattleRoyaleManager reference once
        _cachedBattleManager = FindFirstObjectByType<BattleRoyaleManager>();
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            // Spawn initial ragdolls after small delay to ensure scene setup is complete
            Invoke(nameof(SpawnInitialRagdolls), 0.1f);
        }
    }
    
    /// <summary>
    /// Setup audio source for playing contact kill sounds
    /// </summary>
    private void SetupAudio()
    {
        // Create a dedicated AudioSource for contact kill sounds
        _audioSource = gameObject.AddComponent<AudioSource>();
        
        // Configure as 3D spatial audio (proximity-based)
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 1f; // 3D spatial audio
        _audioSource.volume = 1f;
        _audioSource.minDistance = 1f;
        _audioSource.maxDistance = 50f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
    }
    
    /// <summary>
    /// Spawns the initial batch of ragdolls at level start
    /// </summary>
    private void SpawnInitialRagdolls()
    {
        int spawnCount = Mathf.Min(initialSpawnCount, maxActiveRagdolls);
        
        if (spawnCount <= 0) return;
        
        if (spawnAllAtOnce)
        {
            // Spawn all ragdolls immediately
            for (int i = 0; i < spawnCount; i++)
            {
                SpawnRagdoll();
            }
        }
        else
        {
            // Spawn first ragdoll immediately
            SpawnRagdoll();
            
            // Stagger remaining spawns with 2 second intervals
            for (int i = 1; i < spawnCount; i++)
            {
                float delay = i * 2f; // 2 seconds between each spawn
                Invoke(nameof(SpawnRagdoll), delay);
            }
            
        }
    }

    /// <summary>
    /// Spawns a new AI ragdoll at this spawn point
    /// </summary>
    public GameObject SpawnRagdoll()
    {
        if (aiRagdollPrefab == null)
        {
            // Debug.LogError($"AIRagdollSpawner ({gameObject.name}): No AI ragdoll prefab assigned!");
            return null;
        }

        if (activeRagdollCount >= maxActiveRagdolls)
        {
            // Debug.LogWarning($"AIRagdollSpawner ({gameObject.name}): Max active ragdolls ({maxActiveRagdolls}) reached.");
            return null;
        }
        
        // Check total spawn limit (if enabled)
        if (limitTotalSpawns && totalSpawnsCount >= maxTotalSpawns)
        {
            return null;
        }
        
        // Check global ragdoll limit (use cached reference)
        if (_cachedBattleManager != null && !_cachedBattleManager.CanSpawnRagdoll())
        {
            // Global limit reached - cannot spawn
            return null;
        }

        Vector3 spawnPos = GetSpawnPosition();
        Quaternion spawnRot = GetSpawnRotation();

        GameObject newRagdoll = Instantiate(aiRagdollPrefab, spawnPos, spawnRot);
        
        // PERFORMANCE: Optionally disable limb colliders for massive performance boost
        if (optimizeColliders)
        {
            OptimizeRagdollColliders(newRagdoll);
        }
        
        // Activate the selected costume
        ActivateSelectedCostume(newRagdoll);
        
        // CRITICAL: Refresh costume references after switching costumes
        // (Awake() ran during Instantiate with the default costume active)
        var activeRagdoll = newRagdoll.GetComponent<ActiveRagdoll.ActiveRagdoll>();
        if (activeRagdoll != null)
        {
            activeRagdoll.RefreshCostumeReferences();
            if (enableVerboseLogging)
                Debug.Log("<color=green>[AI Spawner]</color> ✅ Refreshed costume references after spawn");
        }
        
        // CRITICAL: Refresh body part audio triggers for the active costume
        var characterAudio = newRagdoll.GetComponent<CharacterAudioController>();
        if (characterAudio != null)
        {
            characterAudio.RefreshBodyPartTriggers();
            if (enableVerboseLogging)
                Debug.Log("<color=green>[AI Spawner]</color> 🔊 Refreshed body part audio triggers");
        }
        
        // CRITICAL: Refresh time rewind controller for the active costume (if AI has it)
        var timeRewind = newRagdoll.GetComponent<TimeRewindController>();
        if (timeRewind != null)
        {
            timeRewind.RefreshCostumeReferences();
            if (enableVerboseLogging)
                Debug.Log("<color=green>[AI Spawner]</color> ⏪ Refreshed time rewind references");
        }
        
        // (LEGACY) Apply custom skin if assigned and no costume system
        if (ragdollSkin != null)
        {
            ApplySkinToRagdoll(newRagdoll);
        }
        
        // Register this spawner with the ragdoll
        RespawnableAIRagdoll respawnable = newRagdoll.GetComponent<RespawnableAIRagdoll>();
        if (respawnable != null)
        {
            // ⚠️ ALWAYS set spawner reference, even if respawning is disabled
            // The spawner is needed to apply death effects (joints, material, etc.)
            // The respawn logic will check shouldRespawn to decide whether to spawn a replacement
            respawnable.spawner = this;
            
            respawnable.killsPlayer = contactKillsInstantly; // Pass the contact kills setting
            respawnable.grabKillsPlayer = grabKillsPlayer; // Pass the grab kills setting
            respawnable.staysDeadIfShot = staysDeadIfShot; // Pass the stays dead if shot setting
            respawnable.SetDeathDelay(deathDelay); // Pass the death delay setting
            
            // Pass health system settings
            respawnable.SetMaxHealth(shotsToKill);
            
            activeRagdollCount++;
            totalSpawnsCount++;
            
            // Notify BattleRoyaleManager of spawn (use cached reference)
            if (_cachedBattleManager != null)
            {
                _cachedBattleManager.NotifyRagdollSpawned();
                // Debug.Log($"<color=cyan>[Spawner]</color> Notified BattleRoyaleManager of spawn. Global count now: {_cachedBattleManager.GetGlobalActiveCount()}/{_cachedBattleManager.GetGlobalMaxRagdolls()}");
            }
            else
            {
                Debug.LogWarning($"<color=yellow>[Spawner]</color> {gameObject.name}: _cachedBattleManager is NULL! Cannot notify spawn.");
            }
        }
        else
        {
            Debug.LogError($"<color=red>[SPAWNER ERROR!]</color> Spawner '{gameObject.name}' is using prefab '{aiRagdollPrefab.name}' which is MISSING the RespawnableAIRagdoll component! This AI will be UNKILLABLE! Please add the component to the prefab.");
        }
        
        // Configure AI controller with spawner settings
        ActiveRagdoll.RagdollAIController aiController = newRagdoll.GetComponent<ActiveRagdoll.RagdollAIController>();
        if (aiController != null)
        {
            aiController.currentMode = aiMode;
            
            // Movement settings
            aiController.alwaysRun = canRun; // Use master canRun setting
            aiController.runOnlyWhenChasing = runOnlyWhenChasing; // Run only during chase if Can Run is disabled
            
            // Idle mode settings
            aiController.idlePaceRadius = idlePaceRadius;
            aiController.idleWaitTime = idleWaitTime;
            aiController.idleVisionConeAngle = idleVisionConeAngle;
            aiController.canJumpInIdleMode = canJump; // Use master canJump setting
            
            // Roam mode settings
            aiController.roamWaitTime = roamWaitTime;
            aiController.canJumpInRoamMode = canJump; // Use master canJump setting
            
            // Explore mode settings
            aiController.exploreIgnoresBoundary = false; // Always respect boundaries (if AIBoundaryBox exists in scene)
            aiController.canJumpInExploreMode = canJump; // Use master canJump setting
            
            // Path Movement settings
            // Create a new waypoint list with spawn position as Point 0
            List<Vector3> fullPath = new List<Vector3>();
            fullPath.Add(spawnPos); // Spawn position is always Point 0
            fullPath.AddRange(pathWaypoints); // User-defined waypoints follow
            
            aiController.pathWaypoints = fullPath;
            aiController.pathReachedDistance = pathReachedDistance;
            aiController.pathReturnToStartAndStop = returnToStartAndStop;
            aiController.pathLoopForever = loopPathForever;
            aiController.pathNumberOfCycles = numberOfCycles;
            aiController.pathEndAtFinishTrigger = endAtFinishTrigger;
            
            // Path Then Attack settings (also uses master awareness settings)
            aiController.pathThenAttackEnabled = pathThenAttackEnabled;
            if (pathThenAttackEnabled && awarenessType != AwarenessType.None)
            {
                // Use awareness settings for PathThenAttack detection
                if (awarenessType == AwarenessType.ProximityRadius)
                {
                    aiController.pathThenAttackDetectionRadius = awarenessRadius;
                    aiController.pathThenAttackLoseRadius = awarenessLoseRadius;
                }
                else // RaycastVision
                {
                    aiController.pathThenAttackDetectionRadius = awarenessVisionDistance;
                    aiController.pathThenAttackLoseRadius = awarenessVisionDistance + 3f;
                }
            }
            else
            {
                // Use default values if awareness is disabled
                aiController.pathThenAttackDetectionRadius = pathThenAttackDetectionRadius;
                aiController.pathThenAttackLoseRadius = pathThenAttackLoseRadius;
            }
            
            // One Way Path Then Behavior Switch settings
            aiController.pathOneWayThenBehaviorSwitch = pathOneWayThenBehaviorSwitch;
            // Convert enum to AI mode
            aiController.pathFinalBehaviorMode = ConvertPathFinalBehaviorToAIMode(pathFinalBehaviorMode);
            
            // Homing mode settings
            aiController.alwaysKnowPlayerPosition = alwaysKnowPlayerPosition;
            aiController.homingStopDistance = homingStopDistance;
            aiController.grabPlayerInHomingMode = !disableGrabbing && grabPlayerInHomingMode; // Respect master disable grabbing
            aiController.armExtendDistance = armExtendDistance;
            
            // ===== AWARENESS SETTINGS (Master-controlled detection) =====
            // Apply awareness settings based on type selected in Master Settings
            if (awarenessType == AwarenessType.ProximityRadius)
            {
                // Proximity-based detection (360° radius)
                aiController.exploreThenAttackDetectionRadius = awarenessRadius;
                aiController.exploreThenAttackLoseRadius = awarenessLoseRadius;
                aiController.roamThenAttackDetectionRadius = awarenessRadius;
                aiController.roamThenAttackLoseRadius = awarenessLoseRadius;
                aiController.idleThenAttackDetectionRange = awarenessRadius; // Also used for idle mode
            }
            else if (awarenessType == AwarenessType.RaycastVision)
            {
                // Raycast-based detection (vision cone)
                aiController.idleVisionConeAngle = awarenessVisionAngle;
                aiController.idleThenAttackDetectionRange = awarenessVisionDistance;
                aiController.exploreThenAttackDetectionRadius = awarenessVisionDistance;
                aiController.exploreThenAttackLoseRadius = awarenessVisionDistance + 3f; // Add buffer
                aiController.roamThenAttackDetectionRadius = awarenessVisionDistance;
                aiController.roamThenAttackLoseRadius = awarenessVisionDistance + 3f;
            }
            else if (awarenessType == AwarenessType.BoundaryBox)
            {
                // Boundary box detection is handled by AIBoundaryBox GameObjects in the scene
                // The AIBoundaryBox will detect when the player enters and notify registered AIs
                // Use large detection radii as a fallback if no AIBoundaryBox exists
                aiController.exploreThenAttackDetectionRadius = 50f; // Large fallback
                aiController.exploreThenAttackLoseRadius = 52f;
                aiController.roamThenAttackDetectionRadius = 50f;
                aiController.roamThenAttackLoseRadius = 52f;
                aiController.idleThenAttackDetectionRange = 50f;
            }
            // If None, use default values (won't detect player)
            
            aiController.idleThenAttackReturnDistance = idleThenAttackReturnDistance;
            
            // Jump/Gap detection
            aiController.enableAutoJump = canJump; // Use master canJump setting
            aiController.gapCheckDistance = gapCheckDistance;
            
            // Stuck detection (only if canJump is disabled)
            aiController.jumpIfStuck = !canJump && jumpIfStuck; // Only enable if jumping is disabled
            aiController.stuckTimeThreshold = stuckTimeThreshold;
            
            // Player detection sound
            aiController.SetPlayerDetectionSound(playerDetectionSound);
            
            // Set boundary settings
            // Boundary is required for: Roam modes, BoundaryBox awareness
            if (aiMode == ActiveRagdoll.RagdollAIController.AIMode.Roam || 
                aiMode == ActiveRagdoll.RagdollAIController.AIMode.RoamThenAttack ||
                awarenessType == AwarenessType.BoundaryBox)
            {
                // Boundary boxes are now handled by AIBoundaryBox GameObjects in the scene
            }
            else
            {
                // Boundary boxes are now handled by AIBoundaryBox GameObjects in the scene
            }
            
            // NOTE: The old boundary system has been replaced by AIBoundaryBox GameObjects
            // AIBoundaryBox components auto-discover and register AIs spawned inside their bounds
            
            // Set the target object (if specified)
            if (targetObject != null)
            {
                aiController.SetTarget(targetObject);
            }
            
        }
        
        // Configure CharacterAudioController for 3D spatial audio and pass all audio clips
        CharacterAudioController audioController = newRagdoll.GetComponent<CharacterAudioController>();
        if (audioController != null)
        {
            // Use reflection to set all audio fields
            System.Type audioType = audioController.GetType();
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            
            // Set spatial blend to 1.0 (3D audio)
            System.Reflection.FieldInfo spatialBlendField = audioType.GetField("spatialBlend", flags);
            if (spatialBlendField != null)
            {
                spatialBlendField.SetValue(audioController, 1f); // 1.0 = Full 3D spatial audio
            }
            
            // Pass all audio clips from spawner to the AI's audio controller
            if (impactSounds != null && impactSounds.Length > 0)
            {
                System.Reflection.FieldInfo impactSoundsField = audioType.GetField("impactSounds", flags);
                if (impactSoundsField != null) impactSoundsField.SetValue(audioController, impactSounds);
            }
            
            if (jumpSounds != null && jumpSounds.Length > 0)
            {
                System.Reflection.FieldInfo jumpSoundsField = audioType.GetField("jumpSounds", flags);
                if (jumpSoundsField != null) jumpSoundsField.SetValue(audioController, jumpSounds);
            }
            
            if (landingSounds != null && landingSounds.Length > 0)
            {
                System.Reflection.FieldInfo landingSoundsField = audioType.GetField("landingSounds", flags);
                if (landingSoundsField != null) landingSoundsField.SetValue(audioController, landingSounds);
            }
            
            if (windSound != null)
            {
                System.Reflection.FieldInfo windSoundField = audioType.GetField("windSound", flags);
                if (windSoundField != null) windSoundField.SetValue(audioController, windSound);
            }
            
            if (ragdollLoopSound != null)
            {
                System.Reflection.FieldInfo ragdollLoopSoundField = audioType.GetField("ragdollLoopSound", flags);
                if (ragdollLoopSoundField != null) ragdollLoopSoundField.SetValue(audioController, ragdollLoopSound);
            }
        }
        
        // Set death sound on RespawnableAIRagdoll (always override prefab's sound)
        respawnable.deathSound = aiDeathSound; // Will be null if not set, clearing prefab's sound
        
        // Set bullet hit sound on RespawnableAIRagdoll (always override prefab's sound)
        respawnable.bulletHitSound = bulletHitSound; // Will be null if not set, clearing prefab's sound
        
        // Set headshot sound on RespawnableAIRagdoll (always override prefab's sound)
        respawnable.headshotSound = headshotSound; // Will be null if not set, uses bulletHitSound as fallback
        
        // Set death material on RespawnableAIRagdoll (always override prefab's material)
        respawnable.deathMaterial = deathMaterial; // Will be null if not set, clearing prefab's material
        
        // Set bullet impact effect settings on RespawnableAIRagdoll
        respawnable.enableBulletImpactEffect = enableBulletImpactEffect;
        respawnable.bulletImpactEffectPrefab = bulletImpactEffectPrefab;
        respawnable.bulletDamageEffectPrefab = bulletDamageEffectPrefab;
        
        // Configure movement characteristics via DefaultBehaviour
        DefaultBehaviour defaultBehaviour = newRagdoll.GetComponent<DefaultBehaviour>();
        if (defaultBehaviour != null)
        {
            // Use reflection to set private fields
            System.Type type = defaultBehaviour.GetType();
            
            // Walk speed
            System.Reflection.FieldInfo walkSpeedField = type.GetField("_walkSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (walkSpeedField != null) walkSpeedField.SetValue(defaultBehaviour, walkSpeed);
            
            // Run speed multiplier
            System.Reflection.FieldInfo runSpeedField = type.GetField("_runSpeedMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (runSpeedField != null) runSpeedField.SetValue(defaultBehaviour, runSpeedMultiplier);
            
            // Jump force
            System.Reflection.FieldInfo jumpForceField = type.GetField("_jumpForce", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (jumpForceField != null) jumpForceField.SetValue(defaultBehaviour, jumpForce);
            
            // Jump cooldown
            System.Reflection.FieldInfo jumpCooldownField = type.GetField("_jumpCooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (jumpCooldownField != null) jumpCooldownField.SetValue(defaultBehaviour, jumpCooldown);
            
            // Air steering force
            System.Reflection.FieldInfo airSteeringField = type.GetField("_baseAirSteeringForce", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (airSteeringField != null) airSteeringField.SetValue(defaultBehaviour, airSteeringForce);
            
        }
        else
        {
            // Debug.LogWarning($"AIRagdollSpawner ({gameObject.name}): Spawned prefab doesn't have DefaultBehaviour component!");
        }

        // Log spawn info
        string spawnInfo = $"<color=cyan>[AI Spawner]</color> Spawned AI ragdoll in {aiMode} mode at {gameObject.name} (Active: {activeRagdollCount}/{maxActiveRagdolls}";
        if (limitTotalSpawns)
        {
            spawnInfo += $", Total: {totalSpawnsCount}/{maxTotalSpawns}";
        }
        spawnInfo += ")";
        
        return newRagdoll;
    }
    
    /// <summary>
    /// Called by AI to save its last waypoint index before dying (for respawn-at-last-point feature)
    /// </summary>
    public void CacheLastWaypoint(int waypointIndex)
    {
        cachedLastWaypointIndex = waypointIndex;
    }
    
    /// <summary>
    /// Called by RespawnableAIRagdoll IMMEDIATELY when shot/killed (before death delay)
    /// This decrements the active count instantly so wave completion detects properly
    /// </summary>
    public void NotifyRagdollDeath()
    {
        // Decrement count (with safety check)
        if (activeRagdollCount > 0)
        {
            activeRagdollCount--;
        }
        else
        {
            // Debug.LogWarning($"<color=yellow>[AI Spawner]</color> {gameObject.name}: Count already at 0, preventing negative count!");
        }
        
        // Notify BattleRoyaleManager of death (use cached reference)
        if (_cachedBattleManager != null)
        {
            _cachedBattleManager.NotifyRagdollDied();
        }
    }
    
    /// <summary>
    /// Called AFTER death delay to spawn replacement ragdoll
    /// Separated from NotifyRagdollDeath so counting happens immediately but spawning is delayed
    /// </summary>
    public void SpawnReplacementRagdoll()
    {
        // Only spawn if respawning is enabled and we're under max count
        if (shouldRespawn && activeRagdollCount < maxActiveRagdolls)
        {
            // Check total spawn limit (if enabled)
            if (!limitTotalSpawns || totalSpawnsCount < maxTotalSpawns)
            {
                SpawnRagdoll();
            }
        }
    }
    
    /// <summary>
    /// [LEGACY] Called by RespawnableAIRagdoll when it's about to be destroyed
    /// Now calls both new methods for backward compatibility
    /// </summary>
    public void NotifyRagdollDestroyed()
    {
        NotifyRagdollDeath();
        SpawnReplacementRagdoll();
    }
    
    /// <summary>
    /// Called by RespawnableAIRagdoll when it kills the player on contact
    /// Plays the contact kill sound from the spawner (persistent object)
    /// </summary>
    public void PlayContactKillSound()
    {
        if (contactKillSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(contactKillSound);
        }
        else if (contactKillSound == null)
        {
            // Debug.LogWarning($"<color=yellow>[AI Spawner]</color> Contact kill happened but no sound assigned on {gameObject.name}");
        }
    }

    /// <summary>
    /// Get the position where the ragdoll should spawn
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        // Check if we should respawn at last path point (Path Movement mode only)
        if (respawnAtLastPathPoint && 
            aiMode == ActiveRagdoll.RagdollAIController.AIMode.PathMovement && 
            cachedLastWaypointIndex > 0 && 
            cachedLastWaypointIndex < pathWaypoints.Count)
        {
            // Spawn at the cached waypoint position
            return pathWaypoints[cachedLastWaypointIndex];
        }
        
        // Default: spawn at spawner position + offset
        return transform.position + transform.TransformDirection(spawnOffset);
    }

    /// <summary>
    /// Get the rotation the ragdoll should have when spawning
    /// </summary>
    public Quaternion GetSpawnRotation()
    {
        return useRotation ? transform.rotation : Quaternion.identity;
    }

    // Draw a gizmo in the editor to visualize the spawn point
    private void OnDrawGizmos()
    {
        // ===== BOUNDARY VISUALIZATION =====
        // Boundary boxes are now handled by AIBoundaryBox GameObjects in the scene
        // The AIBoundaryBox component draws its own gizmos
        
        // ===== AWARENESS RADIUS VISUALIZATION =====
        // Show awareness radius for modes that detect the player
        bool showAwareness = (aiMode == ActiveRagdoll.RagdollAIController.AIMode.ExploreThenAttack ||
                             aiMode == ActiveRagdoll.RagdollAIController.AIMode.RoamThenAttack ||
                             aiMode == ActiveRagdoll.RagdollAIController.AIMode.IdleThenAttack ||
                             (aiMode == ActiveRagdoll.RagdollAIController.AIMode.PathMovement && pathThenAttackEnabled));
        
        if (showAwareness && awarenessType != AwarenessType.None)
        {
            Vector3 awarenessSpawnPos = GetSpawnPosition();
            
            if (awarenessType == AwarenessType.ProximityRadius)
            {
                // Draw detection radius (360° sphere)
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange for detection
                DrawCircleGizmo(awarenessSpawnPos, awarenessRadius, 32);
                
                // Draw lose radius
                Gizmos.color = new Color(1f, 0f, 0f, 0.2f); // Red for lose radius
                DrawCircleGizmo(awarenessSpawnPos, awarenessLoseRadius, 32);
                
                // Label
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(awarenessSpawnPos + Vector3.right * awarenessRadius, $"Detection: {awarenessRadius}m");
                #endif
            }
            else if (awarenessType == AwarenessType.RaycastVision)
            {
                // Draw vision cone
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f); // Cyan-ish for vision
                Vector3 forward = useRotation ? transform.forward : Vector3.forward;
                
                // Draw vision cone arc
                float halfAngle = awarenessVisionAngle * 0.5f;
                int segments = 20;
                
                for (int i = 0; i < segments; i++)
                {
                    float angle1 = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                    float angle2 = Mathf.Lerp(-halfAngle, halfAngle, (i + 1) / (float)segments);
                    
                    Vector3 dir1 = Quaternion.Euler(0, angle1, 0) * forward;
                    Vector3 dir2 = Quaternion.Euler(0, angle2, 0) * forward;
                    
                    Gizmos.DrawLine(awarenessSpawnPos, awarenessSpawnPos + dir1 * awarenessVisionDistance);
                    Gizmos.DrawLine(awarenessSpawnPos + dir1 * awarenessVisionDistance, 
                                   awarenessSpawnPos + dir2 * awarenessVisionDistance);
                }
                
                // Label
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(awarenessSpawnPos + forward * awarenessVisionDistance, 
                    $"Vision: {awarenessVisionAngle}° / {awarenessVisionDistance}m");
                #endif
            }
            else if (awarenessType == AwarenessType.BoundaryBox)
            {
                // Boundary Box awareness is handled by AIBoundaryBox GameObjects in the scene
                // No gizmo drawn here - the AIBoundaryBox component draws its own
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(awarenessSpawnPos + Vector3.up * 2f, "Detection: See AIBoundaryBox in scene");
                #endif
            }
        }
        
        // ===== SPAWN POINT VISUALIZATION =====
        Gizmos.color = gizmoColor;
        Vector3 spawnPos = GetSpawnPosition();
        
        // Draw a wireframe sphere at the spawn position
        Gizmos.DrawWireSphere(spawnPos, gizmoSize * 0.5f);
        
        // Draw an arrow showing the forward direction
        if (useRotation)
        {
            Vector3 forward = transform.forward * gizmoSize;
            Gizmos.DrawLine(spawnPos, spawnPos + forward);
            Gizmos.DrawWireSphere(spawnPos + forward, gizmoSize * 0.1f);
        }
        
        // Draw a little "AI" marker
        Gizmos.DrawWireCube(spawnPos + Vector3.up * gizmoSize * 0.8f, Vector3.one * gizmoSize * 0.2f);
        
        // ===== IDLE PACE RADIUS VISUALIZATION =====
        // Show the idle pace radius for Idle and IdleThenAttack modes
        if (aiMode == ActiveRagdoll.RagdollAIController.AIMode.Idle || 
            aiMode == ActiveRagdoll.RagdollAIController.AIMode.IdleThenAttack)
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f); // Gray circle = home area
            DrawCircleGizmo(spawnPos, idlePaceRadius, 32);
            
            // Draw home point marker
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(spawnPos, 0.3f);
        }
        
        // ===== PATH MOVEMENT VISUALIZATION =====
        // Show the path with multiple waypoints for Path Movement mode
        if (aiMode == ActiveRagdoll.RagdollAIController.AIMode.PathMovement && pathWaypoints != null && pathWaypoints.Count > 0)
        {
            // Build full path: spawn position (Point 0) + user waypoints
            List<Vector3> fullPath = new List<Vector3>();
            fullPath.Add(spawnPos); // Point 0 is always spawn position
            fullPath.AddRange(pathWaypoints);
            
            // Draw all waypoints including spawn as Point 0
            for (int i = 0; i < fullPath.Count; i++)
            {
                Vector3 waypoint = fullPath[i];
                
                if (i == 0)
                {
                    // Point 0 (spawn position) - WHITE with label
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(waypoint, 0.5f);
                    Gizmos.DrawSphere(waypoint, 0.3f);
                }
                else if (i == 1)
                {
                    // Point 1 (first user waypoint) - GREEN
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(waypoint, 0.5f);
                    Gizmos.DrawSphere(waypoint, 0.3f);
                }
                else
                {
                    // Remaining waypoints - YELLOW
                    Gizmos.color = new Color(1f, 1f, 0f, 0.7f);
                    Gizmos.DrawWireSphere(waypoint, 0.5f);
                    Gizmos.DrawSphere(waypoint, 0.3f);
                }
            }
            
            // Draw path lines connecting all waypoints
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f); // Semi-transparent green
            for (int i = 0; i < fullPath.Count - 1; i++)
            {
                Gizmos.DrawLine(fullPath[i], fullPath[i + 1]);
            }
            
            // Draw return path if "Return to Start and Stop" is enabled
            if (returnToStartAndStop && fullPath.Count > 1)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // Cyan for return path
                Gizmos.DrawLine(fullPath[fullPath.Count - 1], spawnPos);
            }
            
            // Draw loop indicator if "Loop Forever" is enabled
            if (loopPathForever && fullPath.Count > 1)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.5f); // Magenta for loop
                Gizmos.DrawLine(fullPath[fullPath.Count - 1], fullPath[0]); // Loop back to Point 0
            }
        }
    }
    
    // Helper method to draw a horizontal circle
    private void DrawCircleGizmo(Vector3 center, float radius, int segments)
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

    private void OnDrawGizmosSelected()
    {
        // Draw a more visible gizmo when selected
        Gizmos.color = gizmoColor;
        Vector3 spawnPos = GetSpawnPosition();
        
        Gizmos.DrawSphere(spawnPos, gizmoSize * 0.3f);
        
        // Draw coordinate axes
        Gizmos.color = Color.red;
        Gizmos.DrawLine(spawnPos, spawnPos + transform.right * gizmoSize);
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(spawnPos, spawnPos + transform.up * gizmoSize);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(spawnPos, spawnPos + transform.forward * gizmoSize);
    }
    
    /// <summary>
    /// Activates the selected costume hierarchy and DESTROYS others for performance
    /// </summary>
    private void ActivateSelectedCostume(GameObject ragdoll)
    {
        if (ragdoll == null) return;
        
        // Get all costume hierarchies (direct children that have Animated/Physical structure)
        List<GameObject> costumes = new List<GameObject>();
        foreach (Transform child in ragdoll.transform)
        {
            // Skip camera and other systems
            if (child.name.Contains("Camera") || child.name.Contains("System"))
                continue;
            
            // Check if it has Animated/Physical structure (costume hierarchy)
            bool hasAnimated = child.Find("Animated") != null;
            bool hasPhysical = child.Find("Physical") != null;
            
            if (hasAnimated && hasPhysical)
            {
                costumes.Add(child.gameObject);
            }
        }
        
        // If no costumes found, skip costume activation (legacy prefab without costume system)
        if (costumes.Count == 0)
        {
            return;
        }
        
        // Determine which costume to keep
        GameObject costumeToKeep = null;
        if (selectedCostumeIndex >= 0 && selectedCostumeIndex < costumes.Count)
        {
            costumeToKeep = costumes[selectedCostumeIndex];
        }
        else if (costumes.Count > 0)
        {
            costumeToKeep = costumes[0]; // Fallback to first costume
        }
        
        // PERFORMANCE FIX: Destroy all unused costumes instead of just deactivating
        // This frees memory and reduces Unity's object tracking overhead
        int destroyedCount = 0;
        foreach (GameObject costume in costumes)
        {
            if (costume != costumeToKeep)
            {
                Destroy(costume);
                destroyedCount++;
            }
        }
        
        // Ensure the selected costume is active
        if (costumeToKeep != null)
        {
            costumeToKeep.SetActive(true);
            if (enableVerboseLogging)
                Debug.Log($"<color=cyan>[AI Spawner - Costume Optimization]</color> Kept costume '{costumeToKeep.name}', destroyed {destroyedCount} unused costumes");
        }
    }
    
    /// <summary>
    /// (LEGACY) Applies the custom skin material to all renderers in the ragdoll
    /// </summary>
    private void ApplySkinToRagdoll(GameObject ragdoll)
    {
        if (ragdollSkin == null) return;
        
        // Get all SkinnedMeshRenderers and MeshRenderers in the ragdoll
        SkinnedMeshRenderer[] skinnedRenderers = ragdoll.GetComponentsInChildren<SkinnedMeshRenderer>();
        MeshRenderer[] meshRenderers = ragdoll.GetComponentsInChildren<MeshRenderer>();
        
        // Apply material to all skinned mesh renderers
        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            if (renderer != null)
            {
                renderer.material = ragdollSkin;
            }
        }
        
        // Apply material to all mesh renderers
        foreach (MeshRenderer renderer in meshRenderers)
        {
            if (renderer != null)
            {
                renderer.material = ragdollSkin;
            }
        }
    }
    
    /// <summary>
    /// Converts PathFinalBehaviorMode enum to RagdollAIController.AIMode
    /// </summary>
    private ActiveRagdoll.RagdollAIController.AIMode ConvertPathFinalBehaviorToAIMode(PathFinalBehaviorMode finalMode)
    {
        switch (finalMode)
        {
            case PathFinalBehaviorMode.Idle:
                return ActiveRagdoll.RagdollAIController.AIMode.Idle;
            case PathFinalBehaviorMode.Roam:
                return ActiveRagdoll.RagdollAIController.AIMode.Roam;
            case PathFinalBehaviorMode.Explore:
                return ActiveRagdoll.RagdollAIController.AIMode.Explore;
            case PathFinalBehaviorMode.Homing:
                return ActiveRagdoll.RagdollAIController.AIMode.Homing;
            default:
                return ActiveRagdoll.RagdollAIController.AIMode.Idle;
        }
    }
    
    // ==================== PUBLIC API FOR BATTLE ROYALE MANAGER ====================
    
    /// <summary>
    /// Get the maximum total spawns for this spawner (used by BattleRoyaleManager)
    /// </summary>
    public int GetMaxTotalSpawns()
    {
        if (limitTotalSpawns)
        {
            return maxTotalSpawns;
        }
        else
        {
            // If limitTotalSpawns is disabled, return maxActiveRagdolls as a fallback
            // (Battle Royale Manager will warn about this in validation)
            return maxActiveRagdolls;
        }
    }
    
    /// <summary>
    /// Get the current number of active ragdolls from this spawner (used by BattleRoyaleManager)
    /// </summary>
    public int GetActiveRagdollCount()
    {
        return activeRagdollCount;
    }
    
    /// <summary>
    /// Get the total number of ragdolls spawned so far (used by BattleRoyaleManager)
    /// </summary>
    public int GetTotalSpawnsCount()
    {
        return totalSpawnsCount;
    }
    
    /// <summary>
    /// PERFORMANCE OPTIMIZATION: Disable colliders on limbs (arms/legs) while keeping essential colliders.
    /// Keeps colliders on: Head, Torso (Hips/Spine/Chest), Hands, Feet
    /// Disables colliders on: Neck, Upper/Lower Arms, Upper/Lower Legs
    /// Result: ~70% fewer colliders = huge physics performance boost
    /// </summary>
    private void OptimizeRagdollColliders(GameObject ragdoll)
    {
        var activeRagdoll = ragdoll.GetComponent<ActiveRagdoll.ActiveRagdoll>();
        if (activeRagdoll == null)
        {
            Debug.LogWarning($"<color=yellow>[AI Spawner]</color> Cannot optimize colliders: No ActiveRagdoll component found on {ragdoll.name}");
            return;
        }
        
        // Wait for physics animator to be initialized
        if (activeRagdoll.AnimatedAnimator == null)
        {
            Debug.LogWarning($"<color=yellow>[AI Spawner]</color> Cannot optimize colliders: Physical animator not initialized yet on {ragdoll.name}");
            return;
        }
        
        // Get all rigidbodies in the physical ragdoll
        Rigidbody[] rigidbodies = activeRagdoll.Rigidbodies;
        if (rigidbodies == null || rigidbodies.Length == 0)
        {
            Debug.LogWarning($"<color=yellow>[AI Spawner]</color> Cannot optimize colliders: No rigidbodies found on {ragdoll.name}");
            return;
        }
        
        int disabledCount = 0;
        int keptCount = 0;
        
        // Check each rigidbody and disable colliders on limbs
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb == null) continue;
            
            // Determine which bone this rigidbody represents by checking all human bones
            HumanBodyBones boneType = GetBoneTypeForRigidbody(rb, activeRagdoll);
            
            // Decide whether to keep or disable colliders based on bone type
            bool shouldDisable = false;
            
            switch (boneType)
            {
                // KEEP these - essential for gameplay
                case HumanBodyBones.Hips:
                case HumanBodyBones.Spine:
                case HumanBodyBones.Chest:
                case HumanBodyBones.UpperChest:
                case HumanBodyBones.Head:
                case HumanBodyBones.LeftHand:
                case HumanBodyBones.RightHand:
                case HumanBodyBones.LeftFoot:
                case HumanBodyBones.RightFoot:
                    keptCount++;
                    break;
                
                // DISABLE these - limb segments
                case HumanBodyBones.Neck:
                case HumanBodyBones.LeftUpperArm:
                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.RightUpperArm:
                case HumanBodyBones.RightLowerArm:
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.RightUpperLeg:
                case HumanBodyBones.RightLowerLeg:
                case HumanBodyBones.LeftToes:
                case HumanBodyBones.RightToes:
                    shouldDisable = true;
                    break;
                
                default:
                    // Unknown bone - disable to be safe
                    shouldDisable = true;
                    break;
            }
            
            if (shouldDisable)
            {
                // Disable all colliders on this rigidbody
                Collider[] colliders = rb.GetComponents<Collider>();
                foreach (Collider col in colliders)
                {
                    if (col != null)
                    {
                        col.enabled = false;
                        disabledCount++;
                    }
                }
            }
        }
        
        if (enableVerboseLogging)
        {
            Debug.Log($"<color=green>[AI Spawner - Collider Optimization]</color> {ragdoll.name}: Kept {keptCount} body parts, disabled {disabledCount} colliders on limbs");
        }
    }
    
    /// <summary>
    /// Helper method to identify which bone type a rigidbody represents
    /// </summary>
    private HumanBodyBones GetBoneTypeForRigidbody(Rigidbody rb, ActiveRagdoll.ActiveRagdoll activeRagdoll)
    {
        // Try to match this rigidbody's transform to a bone transform from the animator
        Animator physicalAnimator = activeRagdoll.AnimatedAnimator; // Physical animator
        
        if (physicalAnimator == null || !physicalAnimator.isHuman)
            return HumanBodyBones.LastBone;
        
        // Check all human body bones
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            HumanBodyBones bone = (HumanBodyBones)i;
            Transform boneTransform = physicalAnimator.GetBoneTransform(bone);
            
            if (boneTransform != null && boneTransform == rb.transform)
            {
                return bone;
            }
        }
        
        return HumanBodyBones.LastBone; // Unknown
    }
}