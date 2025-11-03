using UnityEngine;
using ActiveRagdoll;

/// <summary>
/// Attach this to the player character.
/// If an AI ragdoll holds the player for a set duration, triggers a respawn.
/// Requires Grippable component to detect when being grabbed.
/// Also controls camera vignette intensity during grab.
/// </summary>
[RequireComponent(typeof(Grippable))]
public class AIGrabRespawnTrigger : MonoBehaviour
{
    [Header("Grab Respawn Settings")]
    [Tooltip("How long (in seconds) the AI must hold the player before triggering respawn")]
    [SerializeField] private float holdDurationToRespawn = 5f;
    
    [Tooltip("Number of AIs required to grab player simultaneously to trigger kill (set by spawner)")]
    [SerializeField] private int grabsRequiredToKill = 1;
    
    [Header("Vignette Settings")]
    [Tooltip("Enable vignette intensity control during grab")]
    [SerializeField] private bool controlVignette = true;
    
    [Tooltip("Starting vignette intensity when grab begins")]
    [Range(0f, 1f)]
    [SerializeField] private float vignetteStartIntensity = 0.3f;
    
    [Tooltip("Maximum vignette intensity at end of hold duration")]
    [Range(0f, 1f)]
    [SerializeField] private float vignetteMaxIntensity = 1f;
    
    [Tooltip("Curve for vignette intensity over time (0-1)")]
    [SerializeField] private AnimationCurve vignetteCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Audio")]
    [Tooltip("Sound to play when respawn is triggered by AI grab (2D)")]
    [SerializeField] private AudioClip aiGrabRespawnSound;
    
    [Tooltip("Volume for grab respawn sound (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;
    
    [Header("Debug")]
    [Tooltip("Show debug messages in console")]
    // [SerializeField] private bool showDebugLogs = false; // Unused after debug log cleanup
    
    private Grippable grippable;
    private RespawnablePlayer respawnablePlayer;
    private AudioSource audioSource;
    
    // Vignette control
    private Kino.Vignette vignetteComponent;
    private float originalVignetteIntensity = 0f;
    private bool foundVignette = false;
    
    private bool isBeingGrabbed = false;
    private float grabbedDuration = 0f;
    private bool hasTriggeredRespawn = false;
    
    // Track ALL AIs currently grabbing (to check grab count threshold)
    private System.Collections.Generic.List<RespawnableAIRagdoll> currentGrabbingAIs = new System.Collections.Generic.List<RespawnableAIRagdoll>();
    private bool anyAIKillsOnGrab = false; // True if ANY grabbing AI has grabKillsPlayer enabled
    
    private void Awake()
    {
        
        // Get required components
        // First try to find Grippable on this GameObject
        grippable = GetComponent<Grippable>();
        
        // If not found, search in children (e.g., PhysicalTorso)
        if (grippable == null)
        {
            grippable = GetComponentInChildren<Grippable>();
        }
        
        respawnablePlayer = GetComponent<RespawnablePlayer>();
        
        // DIAGNOSTIC: Check all components
        
        if (respawnablePlayer == null)
        {
            // Debug.LogError($"<color=red>[AI Grab Respawn]</color> No RespawnablePlayer found on {gameObject.name}! This component requires RespawnablePlayer.");
            enabled = false;
            return;
        }
        
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound
        audioSource.volume = soundVolume;
        
        // Subscribe to grip events
        if (grippable != null)
        {
            grippable.OnGripped += OnPlayerGripped;
            grippable.OnReleased += OnPlayerReleased;
        }
        else
        {
            // Debug.LogWarning($"<color=yellow>[AI Grab Respawn - INIT]</color> ✗ No Grippable component found on {gameObject.name} or its children! AI grab detection will NOT work. Add a Grippable component.");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (grippable != null)
        {
            grippable.OnGripped -= OnPlayerGripped;
            grippable.OnReleased -= OnPlayerReleased;
        }
    }
    
    private void Update()
    {
        // Refresh the list of grabbing AIs every frame to get accurate count
        currentGrabbingAIs = FindAllGrabbingAIs();
        int currentGrabCount = currentGrabbingAIs.Count;
        
        // Check if enough AIs are grabbing and if any of them kills on grab
        anyAIKillsOnGrab = false;
        foreach (var ai in currentGrabbingAIs)
        {
            if (ai != null && ai.grabKillsPlayer)
            {
                anyAIKillsOnGrab = true;
                
                // Get settings from first valid AI's spawner
                if (ai.spawner != null)
                {
                    grabsRequiredToKill = ai.spawner.grabsRequiredToKill;
                    holdDurationToRespawn = ai.spawner.grabKillDuration;
                }
                break;
            }
        }
        
        // Only run if enough AIs are grabbing and at least one kills on grab
        bool enoughGrabbers = currentGrabCount >= grabsRequiredToKill;
        if (!enoughGrabbers || !anyAIKillsOnGrab || hasTriggeredRespawn)
        {
            // Not enough grabbers or none kill - reset timer AND vignette
            if (!enoughGrabbers)
            {
                grabbedDuration = 0f;
                
                // If no AIs are grabbing at all, reset everything
                if (currentGrabCount == 0)
                {
                    isBeingGrabbed = false;
                    hasTriggeredRespawn = false;
                }
                
                // Reset vignette intensity when no longer being grabbed
                if (controlVignette && foundVignette && vignetteComponent != null)
                {
                    vignetteComponent.intensity = originalVignetteIntensity;
                }
            }
            return;
        }
        
        // Increment grab duration
        grabbedDuration += Time.deltaTime;
        
        // Update vignette intensity
        if (controlVignette && foundVignette && vignetteComponent != null)
        {
            UpdateVignetteIntensity();
        }
        
        // Check if hold duration reached (only if respawn is enabled)
        if (grabbedDuration >= holdDurationToRespawn)
        {
            TriggerRespawn();
        }
    }
    
    private void OnPlayerGripped()
    {
        isBeingGrabbed = true;
        hasTriggeredRespawn = false;
        
        // Find all AIs currently grabbing
        currentGrabbingAIs = FindAllGrabbingAIs();
        
        // Get settings from first valid AI's spawner (if available)
        foreach (var ai in currentGrabbingAIs)
        {
            if (ai != null && ai.spawner != null)
            {
                vignetteStartIntensity = ai.spawner.vignetteStartIntensity;
                vignetteMaxIntensity = ai.spawner.vignetteMaxIntensity;
                aiGrabRespawnSound = ai.spawner.grabKillSound;
                grabsRequiredToKill = ai.spawner.grabsRequiredToKill;
                break; // Use first valid spawner's settings
            }
        }
        
        // Find and initialize vignette
        if (controlVignette)
        {
            FindAndInitializeVignette();
        }
    }
    
    private void OnPlayerReleased()
    {
        // Restore original vignette intensity
        if (controlVignette && foundVignette && vignetteComponent != null)
        {
            vignetteComponent.intensity = originalVignetteIntensity;
        }
        
        // Check if there are still AIs grabbing (Update() will handle the count check)
        // We don't immediately stop tracking - Update() continuously refreshes the list
    }
    
    private void TriggerRespawn()
    {
        
        if (hasTriggeredRespawn)
        {
            // Debug.LogWarning($"<color=yellow>[AI Grab - RESPAWN]</color> Already triggered respawn, ignoring duplicate call.");
            return;
        }
        
        hasTriggeredRespawn = true;
        
        
        // Play sound
        PlayAIGrabRespawnSound();
        
        // Kill ALL AI ragdolls that are holding the player
        foreach (var ai in currentGrabbingAIs)
        {
            if (ai != null)
            {
                ai.Respawn(); // Kill each grabbing AI
            }
        }
        
        // Trigger player respawn
        if (respawnablePlayer != null)
        {
            respawnablePlayer.Respawn();
        }
        else
        {
            // Debug.LogError($"<color=red>[AI Grab - RESPAWN]</color> ✗ RespawnablePlayer is NULL! Cannot respawn.");
        }
        
        // Reset state
        isBeingGrabbed = false;
        grabbedDuration = 0f;
    }
    
    /// <summary>
    /// Find ALL AI ragdolls currently holding the player by checking ConfigurableJoints
    /// </summary>
    private System.Collections.Generic.List<RespawnableAIRagdoll> FindAllGrabbingAIs()
    {
        System.Collections.Generic.List<RespawnableAIRagdoll> grabbingAIs = new System.Collections.Generic.List<RespawnableAIRagdoll>();
        System.Collections.Generic.HashSet<RespawnableAIRagdoll> foundAIs = new System.Collections.Generic.HashSet<RespawnableAIRagdoll>(); // Prevent duplicates
        
        // Find all ConfigurableJoints in the scene
        ConfigurableJoint[] allJoints = FindObjectsByType<ConfigurableJoint>(FindObjectsSortMode.None);
        
        // Get all rigidbodies in the player (for ragdoll characters)
        Rigidbody[] playerRigidbodies = GetComponentsInChildren<Rigidbody>();
        
        foreach (ConfigurableJoint joint in allJoints)
        {
            // Check if this joint is connected to any of the player's rigidbodies
            if (joint.connectedBody != null)
            {
                foreach (Rigidbody playerRb in playerRigidbodies)
                {
                    if (joint.connectedBody == playerRb)
                    {
                        // This joint is connected to the player!
                        // The joint is on the AI's hand, so find the AI root
                        RespawnableAIRagdoll aiRagdoll = joint.GetComponentInParent<RespawnableAIRagdoll>();
                        if (aiRagdoll != null && !foundAIs.Contains(aiRagdoll))
                        {
                            grabbingAIs.Add(aiRagdoll);
                            foundAIs.Add(aiRagdoll); // Mark as found to prevent duplicates
                        }
                    }
                }
            }
        }
        
        return grabbingAIs;
    }
    
    // ==================== AUDIO METHODS ====================
    
    /// <summary>
    /// Play AI grab respawn sound (2D)
    /// </summary>
    private void PlayAIGrabRespawnSound()
    {
        if (aiGrabRespawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(aiGrabRespawnSound, soundVolume);
        }
    }
    
    // ==================== VIGNETTE METHODS ====================
    
    /// <summary>
    /// Find THIS PLAYER'S camera (not global Camera.main) and check for Vignette component
    /// MULTIPLAYER-AWARE: Searches within the player's hierarchy to find their specific camera
    /// </summary>
    private void FindAndInitializeVignette()
    {
        // Reset state
        foundVignette = false;
        vignetteComponent = null;
        
        // MULTIPLAYER FIX: Find the camera that belongs to THIS player (child of player GameObject)
        // This prevents Player 1's vignette from being triggered when Player 2 is grabbed
        Camera playerCamera = GetComponentInChildren<Camera>();
        
        // Fallback 1: Try to find camera by name "Active Ragdoll Camera" in children
        if (playerCamera == null)
        {
            Transform cameraTransform = transform.Find("Active Ragdoll Camera");
            if (cameraTransform != null)
            {
                playerCamera = cameraTransform.GetComponent<Camera>();
            }
        }
        
        // Fallback 2: Search all children for any active camera
        if (playerCamera == null)
        {
            Camera[] childCameras = GetComponentsInChildren<Camera>();
            foreach (Camera cam in childCameras)
            {
                if (cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    playerCamera = cam;
                    break;
                }
            }
        }
        
        // Fallback 3: If no camera in player hierarchy, try Camera.main (single-player mode)
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            Debug.LogWarning($"<color=yellow>[AIGrabVignette]</color> No camera found in player hierarchy - falling back to Camera.main (single-player mode?)");
        }
        
        // Check if camera has Vignette component
        if (playerCamera != null)
        {
            vignetteComponent = playerCamera.GetComponent<Kino.Vignette>();
            
            if (vignetteComponent != null)
            {
                foundVignette = true;
                originalVignetteIntensity = vignetteComponent.intensity;
                Debug.Log($"<color=cyan>[AIGrabVignette]</color> Found player camera '{playerCamera.name}' with Vignette component (original intensity: {originalVignetteIntensity})");
            }
            else
            {
                Debug.LogWarning($"<color=yellow>[AIGrabVignette]</color> Camera '{playerCamera.name}' found but no Vignette component attached.");
            }
        }
        else
        {
            Debug.LogWarning($"<color=red>[AIGrabVignette]</color> No camera found for player {gameObject.name}!");
        }
    }
    
    /// <summary>
    /// Update vignette intensity based on grab duration
    /// </summary>
    private void UpdateVignetteIntensity()
    {
        // Calculate normalized grab progress (0 to 1)
        float progress = Mathf.Clamp01(grabbedDuration / holdDurationToRespawn);
        
        // Apply curve
        float curveValue = vignetteCurve.Evaluate(progress);
        
        // Lerp between start and max intensity
        float targetIntensity = Mathf.Lerp(vignetteStartIntensity, vignetteMaxIntensity, curveValue);
        
        // Apply to vignette
        vignetteComponent.intensity = targetIntensity;
    }
    
    // ==================== PUBLIC API ====================
    
    /// <summary>
    /// Get the current grab duration
    /// </summary>
    public float GetGrabDuration() => grabbedDuration;
    
    /// <summary>
    /// Check if player is currently being grabbed
    /// </summary>
    public bool IsGrabbed() => isBeingGrabbed;
    
    /// <summary>
    /// Check if vignette component was found and is being controlled
    /// </summary>
    public bool IsControllingVignette() => foundVignette && vignetteComponent != null;
}

