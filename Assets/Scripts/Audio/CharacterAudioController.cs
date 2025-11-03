using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Handles all audio for a character including impact sounds, jumps, and footsteps.
/// Attach to your player/character GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CharacterAudioController : MonoBehaviour
{
    [Header("Audio Setup")]
    [Tooltip("Audio Mixer Group (should be SFX)")]
    [SerializeField] private AudioMixerGroup mixerGroup;
    
    [SerializeField] private AudioSource audioSource;
    
    [Header("3D Audio Settings")]
    [Tooltip("0 = 2D (location independent, for player), 1 = 3D (spatial, for AI)")]
    [Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 0f;

    [Header("Impact Sounds")]
    [Tooltip("Array of impact sounds for any body part collision. Will play random variation.")]
    [SerializeField] private AudioClip[] impactSounds;
    
    [Tooltip("Minimum velocity magnitude to trigger impact sound")]
    [SerializeField] private float minImpactVelocity = 2f;
    
    [Tooltip("Maximum velocity for scaling (impacts above this use max volume)")]
    [SerializeField] private float maxImpactVelocity = 15f;
    
    [Tooltip("Volume range for impacts (min to max based on force)")]
    [SerializeField] private Vector2 impactVolumeRange = new Vector2(0.3f, 1f);
    
    [Tooltip("Cooldown between impact sounds to prevent spam")]
    [SerializeField] private float impactCooldown = 0.15f;
    
    [Header("Impact Sound Variations")]
    [Tooltip("Randomize pitch for variety")]
    [SerializeField] private bool randomizePitch = true;
    
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);

    [Header("Jump Sounds")]
    [Tooltip("Sounds to play when jumping")]
    [SerializeField] private AudioClip[] jumpSounds;
    
    [SerializeField] private float jumpSoundVolume = 0.8f;

    [Header("Respawn Sounds")]
    [Tooltip("Sounds to play when manually respawning (Q key / R1 button)")]
    [SerializeField] private AudioClip[] respawnSounds;
    
    [SerializeField] private float respawnSoundVolume = 0.8f;

    [Header("Bullet Fire Sounds")]
    [Tooltip("Sounds to play when firing a bullet")]
    [SerializeField] private AudioClip[] bulletFireSounds;
    
    [SerializeField] private float bulletFireSoundVolume = 0.7f;

    [Header("Bullet Hit Sounds")]
    [Tooltip("Sounds to play when a bullet hits something")]
    [SerializeField] private AudioClip[] bulletHitSounds;
    
    [SerializeField] private float bulletHitSoundVolume = 0.8f;
    
    [Header("Gun Pickup Sounds")]
    [Tooltip("Sound to play when picking up a gun (enables shooting)")]
    [SerializeField] private AudioClip[] gunPickupSounds;
    
    [SerializeField] private float gunPickupSoundVolume = 1.0f;

    [Header("Landing Sounds (Optional)")]
    [Tooltip("Optional separate landing sounds. If empty, uses impact sounds.")]
    [SerializeField] private AudioClip[] landingSounds;
    
    [SerializeField] private float minLandingVelocity = 3f;

    [Header("Ground Detection")]
    [Tooltip("Layers considered as 'ground' for impact sounds")]
    [SerializeField] private LayerMask groundLayers = -1;
    
    [Tooltip("Tags that trigger impact sounds (leave empty for all)")]
    [SerializeField] private string[] validTags;

    [Header("Wind/Speed Sound (Falling)")]
    [Tooltip("Wind/whoosh sound that plays when falling (looping sound)")]
    [SerializeField] private AudioClip windSound;
    
    [Tooltip("Minimum downward velocity to be considered 'falling' (negative Y)")]
    [SerializeField] private float minFallingVelocity = 3f;
    
    [Tooltip("Downward velocity for maximum wind volume")]
    [SerializeField] private float maxFallingVelocity = 15f;
    
    [Tooltip("Maximum volume for wind sound")]
    [SerializeField] private float maxWindVolume = 0.7f;
    
    [Tooltip("How quickly wind volume fades out when stopping (higher = faster)")]
    [SerializeField] private float windVolumeFadeOutSpeed = 3f;
    
    [Tooltip("How long to fall before triggering wind sound (prevents tiny bumps)")]
    [SerializeField] private float fallingBufferTime = 0.5f;
    
    [Tooltip("Time to fade from 0 to max volume while falling")]
    [SerializeField] private float windFadeInDuration = 4f;
    
    [Tooltip("Distance to check for ground (raycast length)")]
    [SerializeField] private float groundCheckDistance = 0.3f;

    [Header("Ragdoll Mode Sound")]
    [Tooltip("Looping sound that plays while ragdoll mode is active (Tab/Button North held)")]
    [SerializeField] private AudioClip ragdollLoopSound;
    
    [Tooltip("Volume for ragdoll loop sound")]
    [SerializeField] private float ragdollLoopVolume = 0.6f;

    [Header("Grab Sound (Being Held by AI)")]
    [Tooltip("Looping sound that plays when being grabbed/held by an AI ragdoll")]
    [SerializeField] private AudioClip grabbedSound;
    
    [Tooltip("Maximum volume for grab sound")]
    [SerializeField] private float maxGrabVolume = 0.8f;
    
    [Tooltip("Time to fade from 0 to max volume while being held (should match grab respawn duration)")]
    [SerializeField] private float grabFadeInDuration = 5f;

    [Header("Debug")]
    // [SerializeField] private bool showDebugLogs = false; // Unused after debug log cleanup // Set to false for release

    private Rigidbody rb;
    private float lastImpactTime = -999f;
    private bool wasGrounded = false;
    
    // Wind sound system
    private AudioSource windAudioSource;
    private float targetWindVolume = 0f;
    private float currentWindVolume = 0f;
    private bool isFalling = false;
    private float fallingTimer = 0f;
    private float fallingDuration = 0f; // How long we've been in falling state
    private ActiveRagdoll.InputModule inputModule;
    private TimeRewindController timeRewindController;
    
    // Ragdoll mode sound system
    private AudioSource ragdollAudioSource;
    private bool isRagdollActive = false;
    
    // Grab sound system (being held by AI)
    private AudioSource grabAudioSource;
    private bool isBeingGrabbed = false;
    private float grabbedDuration = 0f; // How long we've been grabbed
    private float targetGrabVolume = 0f;
    private float currentGrabVolume = 0f;
    private ActiveRagdoll.Grippable grippable;

    private void Awake()
    {
        // ALWAYS try to get PhysicalTorso rigidbody first for ActiveRagdoll characters
        var activeRagdoll = GetComponent<ActiveRagdoll.ActiveRagdoll>();
        if (activeRagdoll != null && activeRagdoll.PhysicalTorso != null)
        {
            rb = activeRagdoll.PhysicalTorso;
        }
        else
        {
            // Fallback: Try to find Rigidbody on self
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
            }
            else
            {
                // Debug.LogWarning($"<color=red>[Audio Init]</color> {gameObject.name}: No Rigidbody found! Wind sound won't work.");
            }
        }
        
        // Try to find InputModule for ground detection
        inputModule = GetComponent<ActiveRagdoll.InputModule>();
        // if (inputModule != null)
        // {
        // }
        
        // Try to find TimeRewindController (to disable wind sound during time reverse)
        timeRewindController = GetComponent<TimeRewindController>();
        // if (timeRewindController != null)
        // {
        // }
        
        // Create AudioSource if not assigned
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        ConfigureAudioSource();
        
        // Setup wind sound system
        SetupWindSound();
        
        // Setup ragdoll sound system
        SetupRagdollSound();
        
        // Setup grab sound system
        SetupGrabSound();
        
        // Auto-setup body part collision triggers for ragdoll characters
        SetupBodyPartCollisionTriggers();
        
        // Summary
    }
    
    private void Update()
    {
        // Debug: Check if system is actually working
        if (false && Time.frameCount % 300 == 0) // showDebugLogs // Every ~5 seconds at 60fps
        {
        }
        
        // DIAGNOSTIC: Log if wind sound system is not working
        // DISABLED FOR PERFORMANCE
        /*if (Time.frameCount == 60) // Check at ~1 second
        {
            if (rb == null)
            {
                // Debug.LogWarning($"<color=red>[{gameObject.name} Audio]</color> ❌ Rigidbody is NULL! Wind sound won't work.");
            }
            if (windSound == null)
            {
                // Debug.LogWarning($"<color=yellow>[{gameObject.name} Audio]</color> ⚠️ Wind Sound AudioClip not assigned! Assign it in Inspector under 'Wind/Speed Sound (Falling)'");
            }
            if (windAudioSource == null && windSound != null)
            {
                // Debug.LogWarning($"<color=orange>[{gameObject.name} Audio]</color> Wind AudioSource is NULL but windSound is assigned!");
            }
        }*/
        
        if (rb != null && windSound != null)
        {
            UpdateFallingState();
            UpdateWindSound();
        }
        
        UpdateRagdollSound();
        UpdateGrabSound();
    }
    
    private void UpdateFallingState()
    {
        // CRITICAL: If time reverse is active, immediately stop wind sound and exit
        if (timeRewindController != null && timeRewindController.IsRewinding)
        {
            if (isFalling)
            {
                // if (false) // showDebugLogs

            }
            
            // Force everything off immediately
            isFalling = false;
            fallingTimer = 0f;
            fallingDuration = 0f;
            targetWindVolume = 0f;
            currentWindVolume = 0f;
            
            // Hard stop the audio source
            if (windAudioSource != null && windAudioSource.isPlaying)
            {
                windAudioSource.Stop();
            }
            
            return; // Don't process falling logic while rewinding
        }
        
        // Check if character is grounded
        bool isGrounded = CheckIsGrounded();
        
        // Check if falling down (negative Y velocity)
        float downwardVelocity = -rb.linearVelocity.y; // Positive = falling down
        bool isFallingDown = downwardVelocity > minFallingVelocity;
        
        // DIAGNOSTIC: Log falling detection details (commented out to reduce spam)
        // if (showDebugLogs && Time.frameCount % 60 == 0) // Every second
        // {
        //              $"Grounded:{isGrounded} | " +
        //              $"Velocity.y:{rb.linearVelocity.y:F2} | " +
        //              $"DownwardVel:{downwardVelocity:F2} | " +
        //              $"IsFallingDown:{isFallingDown} (need >{minFallingVelocity}) | " +
        //              $"FallTimer:{fallingTimer:F2} (need >{fallingBufferTime}) | " +
        //              $"Falling:{isFalling}");
        // }
        
        // Determine falling state with buffer
        if (!isGrounded && isFallingDown)
        {
            // Airborne and moving downward
            fallingTimer += Time.deltaTime;
            
            if (fallingTimer >= fallingBufferTime)
            {
                // Now in falling state
                if (!isFalling)
                {
                    // Just started falling
                    isFalling = true;
                    fallingDuration = 0f;
                    // if (false) // showDebugLogs

                }
                
                // Increment falling duration
                fallingDuration += Time.deltaTime;
                
                // Calculate max volume based on fall speed
                float fallSpeedPercent = Mathf.InverseLerp(minFallingVelocity, maxFallingVelocity, downwardVelocity);
                float maxVolume = Mathf.Lerp(0f, maxWindVolume, fallSpeedPercent);
                
                // Fade in from 0 to max volume over windFadeInDuration seconds
                float fadeInPercent = Mathf.Clamp01(fallingDuration / windFadeInDuration);
                targetWindVolume = maxVolume * fadeInPercent;
                
                // if (showDebugLogs && fallingDuration < windFadeInDuration)
            }
        }
        else
        {
            // On ground or not falling - reset
            if (isFalling && false) // showDebugLogs
            
            isFalling = false;
            fallingTimer = 0f;
            fallingDuration = 0f;
            targetWindVolume = 0f;
        }
    }
    
    private bool CheckIsGrounded()
    {
        // Try to use InputModule's ground detection first (most accurate)
        if (inputModule != null)
        {
            return inputModule.IsOnFloor;
        }
        
        // Fallback: Simple raycast downward from rigidbody position
        return Physics.Raycast(rb.position, Vector3.down, groundCheckDistance, groundLayers);
    }
    
    private void SetupBodyPartCollisionTriggers()
    {
        // CRITICAL: Include inactive children (true) to add triggers to ALL costumes!
        // Costumes might be inactive when this runs, but we need triggers on all of them
        Rigidbody[] childRigidbodies = GetComponentsInChildren<Rigidbody>(true);
        
        int addedCount = 0;
        foreach (Rigidbody childRb in childRigidbodies)
        {
            // Skip the main character rigidbody (on this GameObject)
            if (childRb.gameObject == gameObject)
                continue;
            
            // Add BodyPartCollisionTrigger if it doesn't already exist
            if (childRb.GetComponent<BodyPartCollisionTrigger>() == null)
            {
                childRb.gameObject.AddComponent<BodyPartCollisionTrigger>();
                addedCount++;
            }
        }
        
        // if (addedCount > 0)
        // {
        // }
        // else
        // {
        //     Debug.LogWarning("CharacterAudioController: No child rigidbodies found. Impact sounds may not work for ragdoll bodies.");
        // }
    }
    
    /// <summary>
    /// Refresh body part audio triggers after costume swap (call this after activating a new costume)
    /// </summary>
    public void RefreshBodyPartTriggers()
    {
        // Refresh all active body part triggers so they re-find the audio controller
        var triggers = GetComponentsInChildren<BodyPartCollisionTrigger>(false); // Only active ones
        foreach (var trigger in triggers)
        {
            trigger.RefreshAudioController();
        }
    }

    private void ConfigureAudioSource()
    {
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = spatialBlend; // Use inspector value (0 = 2D for player, 1 = 3D for AI)
        
        // Assign mixer group
        if (mixerGroup != null)
        {
            audioSource.outputAudioMixerGroup = mixerGroup;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Check if this is a valid collision target
        if (!IsValidCollision(collision.gameObject))
        {
            // if (false) // showDebugLogs

            return;
        }

        // Calculate impact velocity
        float impactVelocity = collision.relativeVelocity.magnitude;

        // if (false) // showDebugLogs



        // Check if we should play landing sound (falling down onto something)
        bool isLanding = rb.linearVelocity.y < -minLandingVelocity && !wasGrounded;
        
        if (isLanding && landingSounds.Length > 0)
        {
            PlayLandingSound(impactVelocity);
        }
        else if (impactVelocity >= minImpactVelocity)
        {
            PlayImpactSound(impactVelocity);
        }
        else if (false) // showDebugLogs
        {
        }

        wasGrounded = true;
    }

    private void OnCollisionStay(Collision collision)
    {
        // Track grounded state
        if (IsValidCollision(collision.gameObject))
        {
            wasGrounded = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (IsValidCollision(collision.gameObject))
        {
            wasGrounded = false;
        }
    }

    private bool IsValidCollision(GameObject other)
    {
        // Check ground layers
        if (groundLayers != -1 && (groundLayers & (1 << other.layer)) == 0)
            return false;

        // Check valid tags if specified
        if (validTags.Length > 0)
        {
            bool hasValidTag = false;
            foreach (string tag in validTags)
            {
                if (other.CompareTag(tag))
                {
                    hasValidTag = true;
                    break;
                }
            }
            if (!hasValidTag) return false;
        }

        return true;
    }

    private void PlayImpactSound(float velocity)
    {
        // Check cooldown
        if (Time.time - lastImpactTime < impactCooldown)
            return;

        if (impactSounds.Length == 0)
            return;

        // Calculate volume based on impact force
        float normalizedForce = Mathf.InverseLerp(minImpactVelocity, maxImpactVelocity, velocity);
        float volume = Mathf.Lerp(impactVolumeRange.x, impactVolumeRange.y, normalizedForce);

        // Pick random sound
        AudioClip clip = impactSounds[Random.Range(0, impactSounds.Length)];

        // Play with optional pitch variation
        PlaySound(clip, volume);

        lastImpactTime = Time.time;

        // if (false) // showDebugLogs


    }

    private void PlayLandingSound(float velocity)
    {
        if (landingSounds.Length == 0)
            return;

        // Calculate volume based on landing force
        float normalizedForce = Mathf.InverseLerp(minLandingVelocity, maxImpactVelocity, velocity);
        float volume = Mathf.Lerp(impactVolumeRange.x, impactVolumeRange.y, normalizedForce);

        // Pick random landing sound
        AudioClip clip = landingSounds[Random.Range(0, landingSounds.Length)];

        PlaySound(clip, volume);

        lastImpactTime = Time.time;

        // if (false) // showDebugLogs


    }

    /// <summary>
    /// Call this method when the character jumps
    /// </summary>
    public void PlayJumpSound()
    {
        if (jumpSounds.Length == 0)
        {
            // if (false) // showDebugLogs

            //     Debug.LogWarning("CharacterAudioController: No jump sounds assigned!");
            return;
        }

        // Pick random jump sound
        AudioClip clip = jumpSounds[Random.Range(0, jumpSounds.Length)];
        
        PlaySound(clip, jumpSoundVolume);

        // if (false) // showDebugLogs


    }

    /// <summary>
    /// Call this method when firing a bullet
    /// </summary>
    public void PlayBulletFireSound()
    {
        if (bulletFireSounds == null || bulletFireSounds.Length == 0)
        {
            return;
        }

        // Pick random bullet fire sound
        AudioClip clip = bulletFireSounds[Random.Range(0, bulletFireSounds.Length)];
        
        PlaySound(clip, bulletFireSoundVolume);
    }

    /// <summary>
    /// Call this method when a bullet hits something
    /// </summary>
    public void PlayBulletHitSound()
    {
        if (bulletHitSounds == null || bulletHitSounds.Length == 0)
        {
            return;
        }

        // Pick random bullet hit sound
        AudioClip clip = bulletHitSounds[Random.Range(0, bulletHitSounds.Length)];
        
        PlaySound(clip, bulletHitSoundVolume);
    }
    
    /// <summary>
    /// Play gun pickup sound (when character picks up a gun and gains shooting ability)
    /// </summary>
    public void PlayGunPickupSound()
    {
        if (gunPickupSounds == null || gunPickupSounds.Length == 0)
        {
            // Debug.LogWarning("<color=yellow>[Audio]</color> No gun pickup sounds assigned!");
            return;
        }

        // Pick random gun pickup sound
        AudioClip clip = gunPickupSounds[Random.Range(0, gunPickupSounds.Length)];
        
        PlaySound(clip, gunPickupSoundVolume);
    }
    
    /// <summary>
    /// Play gun pickup sound with a custom clip (overload for custom sounds)
    /// </summary>
    public void PlayGunPickupSound(AudioClip customClip)
    {
        if (customClip == null)
        {
            PlayGunPickupSound(); // Fall back to default
            return;
        }
        
        PlaySound(customClip, gunPickupSoundVolume);
    }

    /// <summary>
    /// Call this method when the character manually respawns (Q key / R1 button)
    /// Uses PlayClipAtPoint so the sound persists even if the GameObject is destroyed
    /// </summary>
    public void PlayRespawnSound()
    {
        if (respawnSounds.Length == 0)
        {
            // if (false) // showDebugLogs

            //     Debug.LogWarning("CharacterAudioController: No respawn sounds assigned!");
            return;
        }

        // Pick random respawn sound
        AudioClip clip = respawnSounds[Random.Range(0, respawnSounds.Length)];
        
        // Use PlayClipAtPoint to create a temporary audio source that survives GameObject destruction
        // This is necessary because the player is destroyed immediately after respawning
        // Use Vector3.zero for 2D audio (position doesn't matter for 2D sounds)
        AudioSource.PlayClipAtPoint(clip, Vector3.zero, respawnSoundVolume);

        // if (false) // showDebugLogs


    }

    /// <summary>
    /// Play a sound with optional pitch randomization
    /// </summary>
    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null)
            return;

        // Set pitch
        if (randomizePitch)
        {
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        }
        else
        {
            audioSource.pitch = 1f;
        }

        // Play sound
        audioSource.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Called by BodyPartCollisionTrigger components to play impact sounds
    /// </summary>
    public void PlayImpactSoundFromBodyPart(float velocity, GameObject collisionObject)
    {
        // Debug: Always log if debug is on
        // if (false) // showDebugLogs

        
        // Check if it's a valid collision
        if (!IsValidCollision(collisionObject))
        {
            // if (false) // showDebugLogs

            return;
        }
        
        // Check cooldown
        if (Time.time - lastImpactTime < impactCooldown)
        {
            // if (false) // showDebugLogs

            return;
        }
        
        // if (false) // showDebugLogs

        
        
        // Play impact sound if velocity is high enough
        if (velocity >= minImpactVelocity)
        {
            PlayImpactSound(velocity);
        }
        else if (false) // showDebugLogs
        {
        }
    }
    
    /// <summary>
    /// Manually trigger an impact sound (useful for footstep animations)
    /// </summary>
    public void TriggerFootstepSound()
    {
        if (impactSounds.Length == 0)
            return;

        AudioClip clip = impactSounds[Random.Range(0, impactSounds.Length)];
        PlaySound(clip, 0.6f); // Medium volume for manual footsteps
    }

    /// <summary>
    /// Play a custom one-shot sound through this character's audio source
    /// </summary>
    public void PlayCustomSound(AudioClip clip, float volume = 1f)
    {
        PlaySound(clip, volume);
    }

    #region Wind Sound System
    
    private void SetupWindSound()
    {
        if (windSound == null)
            return;
        
        // Create a separate AudioSource for wind sound (looping)
        windAudioSource = gameObject.AddComponent<AudioSource>();
        windAudioSource.clip = windSound;
        windAudioSource.loop = true;
        windAudioSource.playOnAwake = false;
        windAudioSource.volume = 0f;
        windAudioSource.spatialBlend = spatialBlend; // Use same spatial blend as main audio source
        
        // Assign mixer group
        if (mixerGroup != null)
        {
            windAudioSource.outputAudioMixerGroup = mixerGroup;
        }
        
        // Start playing (at 0 volume)
        windAudioSource.Play();
        
        // if (false) // showDebugLogs

        
    }
    
    private void UpdateWindSound()
    {
        if (windAudioSource == null || windSound == null)
            return;
        
        // Safety check: If time reverse is active, force wind sound off
        if (timeRewindController != null && timeRewindController.IsRewinding)
        {
            currentWindVolume = 0f;
            targetWindVolume = 0f;
            if (windAudioSource.isPlaying)
            {
                windAudioSource.Stop();
            }
            return;
        }
        
        // When falling, fade in gradually (handled by UpdateFallingState setting targetWindVolume)
        // When stopping, fade out quickly using windVolumeFadeOutSpeed
        if (targetWindVolume > currentWindVolume)
        {
            // Fading in - just set directly (UpdateFallingState handles the gradual increase)
            currentWindVolume = targetWindVolume;
        }
        else
        {
            // Fading out - use fade out speed
            currentWindVolume = Mathf.Lerp(currentWindVolume, targetWindVolume, Time.deltaTime * windVolumeFadeOutSpeed);
        }
        
        windAudioSource.volume = currentWindVolume;
        
        // Stop/start audio source based on volume to save performance
        if (currentWindVolume < 0.01f && windAudioSource.isPlaying)
        {
            windAudioSource.Stop();
        }
        else if (currentWindVolume >= 0.01f && !windAudioSource.isPlaying)
        {
            windAudioSource.Play();
        }
    }
    
    /// <summary>
    /// Manually set wind volume based on falling speed (optional - system auto-detects falling by default)
    /// </summary>
    /// <param name="downwardVelocity">Downward velocity (positive = falling)</param>
    public void SetWindFromFallSpeed(float downwardVelocity)
    {
        if (windSound == null)
            return;
        
        if (downwardVelocity < minFallingVelocity)
        {
            targetWindVolume = 0f;
        }
        else
        {
            float fallSpeedPercent = Mathf.InverseLerp(minFallingVelocity, maxFallingVelocity, downwardVelocity);
            targetWindVolume = Mathf.Lerp(0f, maxWindVolume, fallSpeedPercent);
        }
    }
    
    #endregion

    #region Ragdoll Mode Sound System
    
    private void SetupRagdollSound()
    {
        if (ragdollLoopSound == null)
            return;
        
        // Create a separate AudioSource for ragdoll sound (looping)
        ragdollAudioSource = gameObject.AddComponent<AudioSource>();
        ragdollAudioSource.clip = ragdollLoopSound;
        ragdollAudioSource.loop = true;
        ragdollAudioSource.playOnAwake = false;
        ragdollAudioSource.volume = 0f;
        ragdollAudioSource.spatialBlend = spatialBlend; // Use same spatial blend as main audio source
        
        // Assign mixer group
        if (mixerGroup != null)
        {
            ragdollAudioSource.outputAudioMixerGroup = mixerGroup;
        }
        
        // if (false) // showDebugLogs

        
    }
    
    private void SetupGrabSound()
    {
        if (grabbedSound == null)
            return;
        
        // Create a separate AudioSource for grab sound (looping)
        grabAudioSource = gameObject.AddComponent<AudioSource>();
        grabAudioSource.clip = grabbedSound;
        grabAudioSource.loop = true;
        grabAudioSource.playOnAwake = false;
        grabAudioSource.volume = 0f;
        grabAudioSource.spatialBlend = spatialBlend; // Use same spatial blend as main audio source
        
        // Assign mixer group
        if (mixerGroup != null)
        {
            grabAudioSource.outputAudioMixerGroup = mixerGroup;
        }
        
        // Try to find Grippable component and subscribe to events
        grippable = GetComponentInChildren<ActiveRagdoll.Grippable>();
        if (grippable != null)
        {
            grippable.OnGripped += OnGripped;
            grippable.OnReleased += OnReleased;
        }
        else
        {
            // Debug.LogWarning($"<color=yellow>[Grab Audio]</color> No Grippable component found on {gameObject.name} - grab sound won't work!");
        }
    }
    
    private void UpdateRagdollSound()
    {
        if (ragdollAudioSource == null || ragdollLoopSound == null)
            return;
        
        // Simple binary on/off - no fade
        if (isRagdollActive)
        {
            // Start playing if not already
            if (!ragdollAudioSource.isPlaying)
            {
                ragdollAudioSource.volume = ragdollLoopVolume;
                ragdollAudioSource.Play();
                // if (false) // showDebugLogs

            }
        }
        else
        {
            // Stop immediately if playing
            if (ragdollAudioSource.isPlaying)
            {
                ragdollAudioSource.Stop();
                // if (false) // showDebugLogs

            }
        }
    }
    
    /// <summary>
    /// Start playing the ragdoll mode sound (call when entering ragdoll mode)
    /// </summary>
    public void StartRagdollSound()
    {
        if (ragdollLoopSound == null)
            return;
        
        isRagdollActive = true;
        
        // if (false) // showDebugLogs

        
    }
    
    /// <summary>
    /// Stop playing the ragdoll mode sound (call when exiting ragdoll mode)
    /// </summary>
    public void StopRagdollSound()
    {
        isRagdollActive = false;
        
        // if (false) // showDebugLogs

        
    }
    
    #endregion

    #region Grab Sound System (Being Held by AI)
    
    private void UpdateGrabSound()
    {
        if (grabAudioSource == null || grabbedSound == null)
            return;
        
        // Update grab duration and volume fade-in
        if (isBeingGrabbed)
        {
            grabbedDuration += Time.deltaTime;
            
            // Calculate fade-in progress (0 to 1)
            float fadeInProgress = Mathf.Clamp01(grabbedDuration / grabFadeInDuration);
            
            // Target volume based on fade-in
            targetGrabVolume = maxGrabVolume * fadeInProgress;
            
            // Smoothly transition to target volume
            currentGrabVolume = Mathf.Lerp(currentGrabVolume, targetGrabVolume, Time.deltaTime * 2f);
            grabAudioSource.volume = currentGrabVolume;
            
            // Start playing if not already
            if (!grabAudioSource.isPlaying)
            {
                grabAudioSource.Play();
            }
        }
        else
        {
            // Fade out when released
            targetGrabVolume = 0f;
            currentGrabVolume = Mathf.Lerp(currentGrabVolume, 0f, Time.deltaTime * 5f);
            grabAudioSource.volume = currentGrabVolume;
            
            // Stop when volume reaches near-zero
            if (currentGrabVolume < 0.01f && grabAudioSource.isPlaying)
            {
                grabAudioSource.Stop();
                currentGrabVolume = 0f;
            }
        }
    }
    
    /// <summary>
    /// Called by Grippable when player is grabbed
    /// </summary>
    private void OnGripped()
    {
        isBeingGrabbed = true;
        grabbedDuration = 0f;
    }
    
    /// <summary>
    /// Called by Grippable when player is released
    /// </summary>
    private void OnReleased()
    {
        isBeingGrabbed = false;
    }
    
    /// <summary>
    /// Clean up event subscriptions
    /// </summary>
    private void OnDestroy()
    {
        if (grippable != null)
        {
            grippable.OnGripped -= OnGripped;
            grippable.OnReleased -= OnReleased;
        }
    }
    
    #endregion

    #region Public Setters (for runtime adjustments)

    public void SetMinImpactVelocity(float velocity)
    {
        minImpactVelocity = velocity;
    }

    public void SetMaxImpactVelocity(float velocity)
    {
        maxImpactVelocity = velocity;
    }

    public void SetImpactCooldown(float cooldown)
    {
        impactCooldown = cooldown;
    }

    #endregion
}

