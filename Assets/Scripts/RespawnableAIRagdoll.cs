using UnityEngine;
using System.Collections;

/// <summary>
/// Add this component to spawned AI ragdolls to make them respawnable.
/// When respawn is triggered, this ragdoll is destroyed and the spawner creates a new one.
/// </summary>
public class RespawnableAIRagdoll : MonoBehaviour
{
    [Header("Respawn Settings")]
    [Tooltip("The spawner this ragdoll came from (set automatically)")]
    public AIRagdollSpawner spawner;
    
    [Tooltip("If true, this AI will not respawn when killed - corpse stays on the field like in Red Light Green Light mode")]
    public bool staysDeadIfShot = false;
    
    [Header("Kill Player Settings")]
    [Tooltip("If true, touching the player will kill both the player and this AI ragdoll")]
    public bool killsPlayer = false;
    
    [Tooltip("If true, grabbing and holding the player for 5 seconds will kill both player and this AI")]
    public bool grabKillsPlayer = true;
    
    [Header("Audio")]
    [Tooltip("Sound to play when this AI dies")]
    public AudioClip deathSound;
    
    [Tooltip("Sound to play when this AI gets shot/hit by bullet (set by spawner)")]
    public AudioClip bulletHitSound;
    
    [Tooltip("Sound to play when this AI gets headshot (set by spawner) - if null, uses bulletHitSound")]
    public AudioClip headshotSound;
    
    [Header("Visual Effects")]
    [Tooltip("Material to apply when this AI dies (set by spawner)")]
    public Material deathMaterial;
    
    [Tooltip("Enable particle effect when shot (set by spawner)")]
    public bool enableBulletImpactEffect = true;
    
    [Tooltip("Particle effect prefab to spawn at bullet impact point (set by spawner)")]
    public GameObject bulletImpactEffectPrefab;
    
    private AudioSource _audioSource;
    private bool _hasRespawned = false; // Prevent duplicate respawn calls
    private ActiveRagdoll.ActiveRagdoll _activeRagdoll;
    private float _deathDelay = 3f; // Set by spawner, default to 3 seconds
    
    // PERFORMANCE: Cache renderers to avoid GetComponentsInChildren on every death
    private Renderer[] _cachedRenderers;
    
    // PERFORMANCE: Cache BattleRoyaleManager reference to avoid FindFirstObjectByType on every death
    private static BattleRoyaleManager _cachedBattleManager;
    
    // HEALTH SYSTEM
    private int _currentHealth = 1; // Current health points
    private int _maxHealth = 1; // Maximum health (shots to kill)
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        _activeRagdoll = GetComponent<ActiveRagdoll.ActiveRagdoll>();
        
        // PERFORMANCE: Cache all renderers once at start
        _cachedRenderers = GetComponentsInChildren<Renderer>();
        
        // PERFORMANCE: Cache BattleRoyaleManager once (static, shared across all ragdolls)
        if (_cachedBattleManager == null)
        {
            _cachedBattleManager = FindFirstObjectByType<BattleRoyaleManager>();
        }
        
        // Set up collision detection on all rigidbodies if killsPlayer is enabled
        if (killsPlayer)
        {
            SetupCollisionDetection();
        }
    }

    /// <summary>
    /// SIMPLIFIED DAMAGE SYSTEM: Each hit = 1 damage, any body part
    /// </summary>
    public void TakeDamage(Collider hitCollider, bool isHeadshot)
    {
        // Already dead or respawning
        if (_hasRespawned)
        {
            // Debug.LogWarning($"<color=red>[IMMUNE!]</color> {gameObject.name} is already dead (_hasRespawned = true)! Ignoring damage.");
            return;
        }
        
        // Reduce health by 1
        _currentHealth--;
        
        // Play bullet hit sound
        PlayBulletHitSound(isHeadshot);
        
        // Check if health reached 0
        if (_currentHealth <= 0)
        {
            // Die immediately
            Respawn();
        }
    }
    
    /// <summary>
    /// Triggers a respawn - destroys this ragdoll and tells the spawner to create a new one
    /// UNLESS in Red Light Green Light mode, where bodies stay on the field
    /// </summary>
    public void Respawn()
    {
        // Prevent duplicate respawn calls (Destroy is not immediate)
        if (_hasRespawned)
        {
            return;
        }
        
        // Mark as respawned immediately to prevent duplicate calls
        _hasRespawned = true;
        
        // Award kill points when actually dying
        RagdollPointsSystem pointsSystem = RagdollPointsSystem.Instance;
        if (pointsSystem != null)
        {
            // Simple point award - no headshot bonus in simplified system
            pointsSystem.AddKillPoints(false);
        }
        
        // Notify Battle Royale Manager of kill (use cached reference)
        if (_cachedBattleManager != null)
        {
            _cachedBattleManager.OnRagdollKilled();
        }
        
        // ⚡ IMMEDIATELY notify spawner that this ragdoll is "dead" for counting purposes
        // This ensures wave completion and counters update instantly, not after death delay
        if (spawner != null)
        {
            spawner.NotifyRagdollDeath();
        }
        
        // ⚠️ CRITICAL: Disable AI controller and control scripts IMMEDIATELY so AI stops moving/attacking
        ActiveRagdoll.RagdollAIController aiController = GetComponent<ActiveRagdoll.RagdollAIController>();
        if (aiController != null)
        {
            aiController.enabled = false;
        }
        DefaultBehaviour defaultBehaviour = GetComponent<DefaultBehaviour>();
        if (defaultBehaviour != null) defaultBehaviour.enabled = false;
        
        // Disable AnimationModule and PhysicsModule to stop error spam
        ActiveRagdoll.AnimationModule animModule = GetComponent<ActiveRagdoll.AnimationModule>();
        if (animModule != null) animModule.enabled = false;
        ActiveRagdoll.PhysicsModule physModule = GetComponent<ActiveRagdoll.PhysicsModule>();
        if (physModule != null) physModule.enabled = false;
        
        // Check if this AI is in Red Light Green Light mode
        bool isInRLGLMode = aiController != null && aiController.currentMode == ActiveRagdoll.RagdollAIController.AIMode.RedLightGreenLight;
        
        if (spawner != null)
        {
            
            // Cache last waypoint for respawn-at-last-point feature (Path Movement mode only)
            if (aiController != null && aiController.currentMode == ActiveRagdoll.RagdollAIController.AIMode.PathMovement)
            {
                spawner.CacheLastWaypoint(aiController.GetCurrentWaypointIndex());
            }
            
            // Play death sound if available
            if (deathSound != null && _audioSource != null)
            {
                // Play sound at this position (detached from the object)
                AudioSource.PlayClipAtPoint(deathSound, transform.position);
            }
            
            // Apply death material if available
            if (deathMaterial != null)
            {
                ApplyDeathMaterial();
            }
            
            if (isInRLGLMode || staysDeadIfShot)
            {
                // RED LIGHT GREEN LIGHT MODE or STAYS DEAD IF SHOT: Leave body on field, don't respawn
                // Use coroutine to let ragdoll fall naturally before freezing
                StartCoroutine(RLGLDeathSequence());
            }
            else
            {
                // NORMAL MODE: Respawn as usual
                
                // Release grip immediately so dead ragdoll can't hold player
                ReleaseGrip();
                
                // Destroy all joints to make body parts separate
                // Parts will maintain their current physics/velocity and fall apart naturally
                if (_activeRagdoll != null)
                {
                    ConfigurableJoint[] joints = _activeRagdoll.Joints;
                    if (joints != null)
                    {
                        foreach (ConfigurableJoint joint in joints)
                        {
                            if (joint != null)
                            {
                                Destroy(joint); // Destroy joint - parts now separate!
                            }
                        }
                    }
                }
                
                // Start coroutine to delay destruction and respawn
                StartCoroutine(DestroyAfterDelay(_deathDelay));
            }
        }
        else
        {
            Debug.LogError($"<color=red>[Respawn ERROR!]</color> {gameObject.name}: Cannot apply death effects - NO SPAWNER ASSIGNED! Death material, joint destruction, and respawn will not work!");
        }
    }
    
    /// <summary>
    /// Waits for specified delay, then spawns replacement and destroys this ragdoll
    /// </summary>
    private System.Collections.IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Notify spawner to create replacement ragdoll (if respawning is enabled)
        if (spawner != null)
        {
            spawner.SpawnReplacementRagdoll();
        }
        
        // Destroy this ragdoll
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Red Light Green Light death sequence - lets ragdoll fall naturally then freezes it
    /// </summary>
    private System.Collections.IEnumerator RLGLDeathSequence()
    {
        // STEP 1: Release grip immediately so dead ragdoll can't hold player
        ReleaseGrip();
        
        // STEP 2: Destroy all joints to make body parts separate
        if (_activeRagdoll != null)
        {
            ConfigurableJoint[] joints = _activeRagdoll.Joints;
            if (joints != null)
            {
                foreach (ConfigurableJoint joint in joints)
                {
                    if (joint != null)
                    {
                        Destroy(joint); // Destroy joint - parts now separate!
                    }
                }
            }
        }
        
        // STEP 3: Ensure all rigidbodies are active and can fall with gravity
        Rigidbody[] allRigidbodies = GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in allRigidbodies)
        {
            if (rb != null)
            {
                rb.isKinematic = false;      // Make sure it's not kinematic
                rb.useGravity = true;        // Enable gravity
                rb.detectCollisions = true;  // Enable collisions so it can hit the ground
            }
        }
        
        // STEP 4: Wait 5 seconds for ragdoll to fall and settle on the ground
        yield return new WaitForSeconds(5f);
        
        // STEP 5: Now freeze the ragdoll in place (performance optimization)
        
        // Disable ALL colliders to stop trigger spam
        Collider[] allColliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in allColliders)
        {
            if (col != null) col.enabled = false;
        }
        
        // Freeze all rigidbodies
        foreach (Rigidbody rb in allRigidbodies)
        {
            if (rb != null)
            {
                rb.Sleep();                  // Put to sleep
                rb.detectCollisions = false; // Disable collision detection
                rb.isKinematic = true;       // Make kinematic (frozen)
            }
        }
        
        // ⚡ PERFORMANCE: Disable ALL animators (they keep updating even when frozen!)
        Animator[] allAnimators = GetComponentsInChildren<Animator>();
        foreach (Animator anim in allAnimators)
        {
            if (anim != null) anim.enabled = false;
        }
        
        // ⚡ PERFORMANCE: Destroy unused costume hierarchies to free memory
        // Only the active costume is visible, the other 3 are dead weight!
        foreach (Transform child in transform)
        {
            // Skip camera and systems
            if (child.name.Contains("Camera") || child.name.Contains("System"))
                continue;
            
            // If this is an inactive costume hierarchy, destroy it
            if (!child.gameObject.activeSelf)
            {
                Destroy(child.gameObject);
            }
        }
        
        // Destroy all collision forwarder components (cleanup memory leak)
        AIRagdollCollisionForwarder[] forwarders = GetComponentsInChildren<AIRagdollCollisionForwarder>();
        foreach (AIRagdollCollisionForwarder forwarder in forwarders)
        {
            if (forwarder != null) Destroy(forwarder);
        }
        
        // Spawn replacement ragdoll now that corpse is frozen (if respawning is enabled)
        // The corpse stays on the field while a new ragdoll spawns at the spawner!
        if (spawner != null)
        {
            spawner.SpawnReplacementRagdoll();
            // Debug.Log($"<color=orange>[RLGL Death]</color> Corpse frozen. Spawning replacement ragdoll.");
        }
    }
    
    /// <summary>
    /// Set the death delay from the spawner
    /// </summary>
    public void SetDeathDelay(float delay)
    {
        _deathDelay = delay;
    }
    
    /// <summary>
    /// Set the maximum health (shots to kill) from the spawner
    /// </summary>
    public void SetMaxHealth(int health)
    {
        _maxHealth = Mathf.Max(1, health); // Minimum 1 health
        _currentHealth = _maxHealth; // Start at full health
        _hasRespawned = false; // Reset respawn flag (in case of GameObject reuse)
    }
    
    /// <summary>
    /// Release any grip the AI ragdoll currently has (disable Gripper components on hands)
    /// </summary>
    private void ReleaseGrip()
    {
        if (_activeRagdoll == null) return;
        
        // Get left and right hand bones
        Transform leftHand = _activeRagdoll.GetPhysicalBone(HumanBodyBones.LeftHand);
        Transform rightHand = _activeRagdoll.GetPhysicalBone(HumanBodyBones.RightHand);
        
        // Disable Gripper components on hands (this automatically calls UnGrip)
        if (leftHand != null)
        {
            ActiveRagdoll.Gripper leftGripper = leftHand.GetComponent<ActiveRagdoll.Gripper>();
            if (leftGripper != null)
            {
                leftGripper.enabled = false;
            }
        }
        
        if (rightHand != null)
        {
            ActiveRagdoll.Gripper rightGripper = rightHand.GetComponent<ActiveRagdoll.Gripper>();
            if (rightGripper != null)
            {
                rightGripper.enabled = false;
            }
        }
    }
    
    /// <summary>
    /// Sets up collision detection on all ragdoll rigidbodies
    /// </summary>
    private void SetupCollisionDetection()
    {
        // Add collision forwarder to all rigidbodies in the ragdoll
        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>();
        
        foreach (Rigidbody rb in rigidbodies)
        {
            // Add a component that forwards collision events back to this script
            AIRagdollCollisionForwarder forwarder = rb.gameObject.AddComponent<AIRagdollCollisionForwarder>();
            forwarder.parentRagdoll = this;
        }
        
    }
    
    /// <summary>
    /// Play the bullet hit sound when this AI gets shot (called by DefaultBehaviour when bullet hits)
    /// OPTIMIZED: Removed debug logging to reduce performance overhead
    /// </summary>
    /// <param name="isHeadshot">If true, plays headshot sound instead of regular hit sound</param>
    public void PlayBulletHitSound(bool isHeadshot = false)
    {
        // Determine which sound to play
        AudioClip soundToPlay = null;
        
        if (isHeadshot && headshotSound != null)
        {
            // Headshot with custom headshot sound
            soundToPlay = headshotSound;
        }
        else if (bulletHitSound != null)
        {
            // Regular hit or headshot with no custom sound (fallback to bullet hit sound)
            soundToPlay = bulletHitSound;
        }
        
        if (soundToPlay == null) return;
        
        // Play sound as 2D audio (no spatial falloff - always hear it at full volume)
        if (_audioSource != null)
        {
            _audioSource.spatialBlend = 0f; // 2D audio
            _audioSource.PlayOneShot(soundToPlay);
        }
        else
        {
            // Fallback: Use PlayClipAtPoint with Vector3.zero for 2D audio
            AudioSource.PlayClipAtPoint(soundToPlay, Vector3.zero);
        }
    }
    
    /// <summary>
    /// Apply death material to all renderers (OPTIMIZED: uses cached renderers and sharedMaterial)
    /// </summary>
    private void ApplyDeathMaterial()
    {
        if (deathMaterial == null || _cachedRenderers == null) return;
        
        // PERFORMANCE: Use cached renderers array instead of searching hierarchy
        foreach (Renderer renderer in _cachedRenderers)
        {
            if (renderer == null) continue; // Safety check in case renderer was destroyed
            
            // PERFORMANCE: Use sharedMaterial to avoid creating material instances
            // This is safe because death material is always the same red material
            renderer.sharedMaterial = deathMaterial;
        }
    }
    
    /// <summary>
    /// Called by AIRagdollCollisionForwarder when any part of the ragdoll collides with something
    /// </summary>
    public void OnRagdollCollision(Collision collision)
    {
        // Only process if kills player is enabled and haven't already respawned
        if (!killsPlayer || _hasRespawned) return;
        
        // Check if we collided with the player
        RespawnablePlayer player = collision.gameObject.GetComponent<RespawnablePlayer>();
        if (player == null)
        {
            player = collision.gameObject.GetComponentInParent<RespawnablePlayer>();
        }
        
        if (player != null)
        {
            
            // Notify spawner to play contact kill sound (spawner is persistent so audio won't be cut off)
            if (spawner != null)
            {
                spawner.PlayContactKillSound();
            }
            
            // Kill both the player and this AI ragdoll
            player.Respawn(); // Player respawns
            Respawn();        // This AI respawns
        }
    }
}

/// <summary>
/// Helper component that forwards collision events from ragdoll body parts to the main RespawnableAIRagdoll
/// </summary>
public class AIRagdollCollisionForwarder : MonoBehaviour
{
    [HideInInspector]
    public RespawnableAIRagdoll parentRagdoll;
    
    private void OnCollisionEnter(Collision collision)
    {
        if (parentRagdoll != null)
        {
            parentRagdoll.OnRagdollCollision(collision);
        }
    }
}

