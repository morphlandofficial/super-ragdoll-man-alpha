using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Add this component to the player prefab to make them respawnable.
/// When respawn is triggered, this player is deactivated and a fresh one is spawned.
/// </summary>
public class RespawnablePlayer : MonoBehaviour
{
    [Header("Player Identity")]
    [Tooltip("Player ID for multiplayer (1 = P1, 2 = P2, etc. 0 = single player)")]
    public int playerID = 0;
    
    [Header("Respawn Settings")]
    [Tooltip("The spawn point this player came from (set automatically)")]
    public SpawnPoint spawnPoint;
    
    [Header("Manual Respawn Input")]
    [Tooltip("Enable manual respawn with Q key / R1 button")]
    public bool allowManualRespawn = true;
    
    [Tooltip("Time (in seconds) that the respawn button must be held before respawning")]
    public float respawnHoldTime = 2f;
    
    [Header("Respawn Visual Effect")]
    [Tooltip("Enable pixelation effect while holding respawn button")]
    public bool enableRespawnPixelationEffect = true;
    
    [Tooltip("Minimum block count to reach (64 is the absolute minimum)")]
    public float minBlockCount = 64f;
    
    [Header("Respawn Audio Effect")]
    [Tooltip("Enable ragdoll loop sound while holding respawn button")]
    public bool enableRespawnLoopSound = true;
    
    // Static flag to communicate with points system - next respawn should skip penalty
    public static bool NextRespawnIsManual { get; set; } = false;
    
    // Reference to the audio controller
    private CharacterAudioController _audioController;
    
    // Track how long the respawn button has been held
    private float _respawnButtonHeldTime = 0f;
    
    // Pixelation effect references
    private Assets.Pixelation.Scripts.Pixelationv2 _pixelationEffect;
    private float _originalBlockCount = 0f;
    private bool _pixelationEffectActive = false;
    private bool _triedPixelationInit = false; // Track if we attempted initialization
    private bool _pixelationWasDisabled = false; // Track if pixelation should be turned back off
    
    // Track if we started the ragdoll sound
    private bool _respawnSoundActive = false;
    
    // Multiplayer support - specific gamepad assigned to this player
    private Gamepad _assignedGamepad = null;

    private void Start()
    {
        // Get reference to the audio controller
        _audioController = GetComponent<CharacterAudioController>();
        
        // Initialize pixelation effect
        if (enableRespawnPixelationEffect)
        {
            InitializePixelationEffect();
        }
    }
    
    private void InitializePixelationEffect()
    {
        // Find the active camera in the scene (similar to how datamosh effect works)
        Camera activeCamera = null;
        
        // First try to get camera from CameraModule (for DefaultBehaviour characters)
        var cameraModule = GetComponent<ActiveRagdoll.CameraModule>();
        if (cameraModule != null && cameraModule.Camera != null)
        {
            activeCamera = cameraModule.Camera.GetComponent<Camera>();
        }
        
        // If no CameraModule, search for active camera in scene (for TitlePlayerDefaultBehavior)
        if (activeCamera == null)
        {
            activeCamera = Camera.main;
        }
        
        // If Camera.main isn't set, find any enabled camera
        if (activeCamera == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera cam in cameras)
            {
                if (cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    activeCamera = cam;
                    break;
                }
            }
        }
        
        // Try to get the Pixelationv2 component from the active camera
        if (activeCamera != null)
        {
            _pixelationEffect = activeCamera.GetComponent<Assets.Pixelation.Scripts.Pixelationv2>();
            
            if (_pixelationEffect != null)
            {
                // Store the original block count value
                _originalBlockCount = _pixelationEffect.BlockCount;
                Debug.Log($"<color=cyan>[RespawnPixel]</color> INITIALIZED - Camera: {activeCamera.name}, Enabled: {_pixelationEffect.enabled}, BlockCount: {_originalBlockCount} (will check state when R1 pressed)");
            }
            else
            {
                Debug.Log($"<color=yellow>[RespawnPixel]</color> No Pixelationv2 component found on camera: {activeCamera?.name}");
            }
        }
        
        // Mark that we've attempted initialization
        _triedPixelationInit = true;
    }

    private void Update()
    {
        // Try to initialize pixelation effect if not done yet (only try once)
        if (enableRespawnPixelationEffect && _pixelationEffect == null && !_triedPixelationInit)
        {
            InitializePixelationEffect();
        }
        
        // Check for manual respawn input
        if (allowManualRespawn)
        {
            bool respawnButtonHeld = false;
            
            // If we have a specific gamepad assigned (multiplayer), ONLY check that gamepad
            if (_assignedGamepad != null)
            {
                // Multiplayer mode - check ONLY the assigned gamepad
                if (_assignedGamepad.rightShoulder.isPressed)
                {
                    // Only respawn if ButtonNorth (ragdoll) is NOT held
                    bool ragdollHeld = _assignedGamepad.buttonNorth.isPressed;
                    if (!ragdollHeld)
                    {
                        respawnButtonHeld = true;
                    }
                }
            }
            else
            {
                // Single player mode - check keyboard OR any gamepad
                // Check for Q key on keyboard
                if (Keyboard.current != null && Keyboard.current.qKey.isPressed)
                {
                    respawnButtonHeld = true;
                }
                
                // Check for R1 button on gamepad (rightShoulder = RB on Xbox, R1 on PlayStation)
                // BUT: Don't respawn if ButtonNorth (ragdoll) is held (for cheat code detection)
                if (Gamepad.current != null && Gamepad.current.rightShoulder.isPressed)
                {
                    // Only respawn if ButtonNorth (ragdoll) is NOT held
                    bool ragdollHeld = Gamepad.current.buttonNorth.isPressed;
                    if (!ragdollHeld)
                    {
                        respawnButtonHeld = true;
                    }
                }
            }
            
            // If button is held, increment the timer
            if (respawnButtonHeld)
            {
                _respawnButtonHeldTime += Time.deltaTime;
                
                // Start ragdoll loop sound on first frame of hold
                if (_respawnButtonHeldTime > 0f && !_respawnSoundActive && enableRespawnLoopSound && _audioController != null)
                {
                    _audioController.StartRagdollSound();
                    _respawnSoundActive = true;
                }
                
                // Update pixelation effect as button is held
                if (enableRespawnPixelationEffect && _pixelationEffect != null)
                {
                    // On first frame of hold, remember current state (may have changed since init)
                    if (!_pixelationEffectActive)
                    {
                        // Check if we should restore pixelation based on:
                        // 1. Multiplayer mode: ALWAYS turn OFF (for UI readability)
                        // 2. Single-player mode: Check ACTUAL COMPONENT STATE (after CameraVisualController has applied settings)
                        bool isMultiplayerMode = (UnityEngine.InputSystem.Gamepad.all.Count >= 2);
                        
                        if (isMultiplayerMode)
                        {
                            // MULTIPLAYER: Always turn pixelation OFF
                            _pixelationWasDisabled = true;
                            Debug.Log($"<color=green>[RespawnPixel]</color> R1 HOLD START - MULTIPLAYER MODE, will force pixelation OFF on release");
                        }
                        else
                        {
                            // SINGLE-PLAYER: Check the LEVEL SETTINGS (source of truth from CameraVisualController)
                            bool levelSettingsSayEnabled = CameraVisualController.ShouldPixelationBeEnabled();
                            _pixelationWasDisabled = !levelSettingsSayEnabled; // Respect level settings, not current component state
                            
                            if (_pixelationWasDisabled)
                            {
                                Debug.Log($"<color=green>[RespawnPixel]</color> R1 HOLD START - Level settings have pixelation OFF, will keep it OFF on release");
                            }
                            else
                            {
                                Debug.Log($"<color=green>[RespawnPixel]</color> R1 HOLD START - Level settings have pixelation ON, will restore it ON on release");
                            }
                        }
                    }
                    
                    // Temporarily enable the effect (even if it was disabled)
                    if (!_pixelationEffect.enabled)
                    {
                        _pixelationEffect.enabled = true;
                        Debug.Log($"<color=green>[RespawnPixel]</color> ENABLED pixelation for R1 hold effect");
                    }
                    
                    // Calculate progress (0 to 1) over the hold time
                    float progress = Mathf.Clamp01(_respawnButtonHeldTime / respawnHoldTime);
                    
                    // Use exponential curve to reach minimum faster (power of 3 for aggressive drop)
                    float curvedProgress = Mathf.Pow(progress, 0.5f); // Square root for faster initial drop
                    
                    // Lerp from original to absolute minimum (64)
                    float newBlockCount = Mathf.Lerp(_originalBlockCount, minBlockCount, curvedProgress);
                    _pixelationEffect.BlockCount = newBlockCount;
                    
                    // Only log first time to avoid spam
                    if (!_pixelationEffectActive)
                    {
                        Debug.Log($"<color=green>[RespawnPixel]</color> R1 HOLD - BlockCount: {_originalBlockCount} → {newBlockCount}, Progress: {progress:F2}");
                    }
                    
                    _pixelationEffectActive = true;
                }
                
                // Check if held long enough to trigger respawn
                if (_respawnButtonHeldTime >= respawnHoldTime)
                {
                    // Stop ragdoll loop sound before playing respawn sound
                    StopRespawnLoopSound();
                    
                    // Play manual respawn sound through audio controller
                    if (_audioController != null)
                    {
                        _audioController.PlayRespawnSound();
                    }
                    
                    Respawn(isManual: true);
                    
                    // Reset the timer after respawning
                    _respawnButtonHeldTime = 0f;
                    
                    // Reset pixelation effect (will happen automatically on respawn, but do it here too)
                    ResetPixelationEffect();
                }
            }
            else if (_respawnButtonHeldTime > 0f)
            {
                // Button JUST released (was held, now not) - reset everything ONCE
                Debug.Log($"<color=yellow>[RespawnPixel]</color> R1 RELEASED - HeldTime was: {_respawnButtonHeldTime:F2}s");
                _respawnButtonHeldTime = 0f;
                ResetPixelationEffect();
                StopRespawnLoopSound();
            }
        }
    }
    
    private void ResetPixelationEffect()
    {
        // Reset pixelation effect to original value and state
        if (_pixelationEffectActive && _pixelationEffect != null)
        {
            Debug.Log($"<color=yellow>[RespawnPixel]</color> RESET CALLED - Current enabled: {_pixelationEffect.enabled}, WasDisabled: {_pixelationWasDisabled}, BlockCount: {_pixelationEffect.BlockCount}");
            
            // Reset the block count
            if (_originalBlockCount > 0f)
            {
                _pixelationEffect.BlockCount = _originalBlockCount;
                Debug.Log($"<color=yellow>[RespawnPixel]</color> BlockCount reset to: {_originalBlockCount}");
            }
            
            // CRITICAL: Turn off pixelation if it was originally disabled
            if (_pixelationWasDisabled)
            {
                _pixelationEffect.enabled = false;
                Debug.Log($"<color=red>[RespawnPixel]</color> DISABLED pixelation (was originally disabled)");
            }
            else
            {
                Debug.Log($"<color=cyan>[RespawnPixel]</color> Pixelation stays ENABLED (was originally enabled)");
            }
            
            _pixelationEffectActive = false;
        }
        else
        {
            if (!_pixelationEffectActive)
            {
                Debug.Log($"<color=gray>[RespawnPixel]</color> Reset called but effect was not active");
            }
            if (_pixelationEffect == null)
            {
                Debug.Log($"<color=gray>[RespawnPixel]</color> Reset called but _pixelationEffect is null");
            }
        }
    }
    
    private void StopRespawnLoopSound()
    {
        // Stop the ragdoll loop sound if it's active
        if (_respawnSoundActive && _audioController != null)
        {
            _audioController.StopRagdollSound();
            _respawnSoundActive = false;
        }
    }
    
    private void OnDisable()
    {
        // Make sure to stop the sound when the player is disabled/destroyed
        StopRespawnLoopSound();
    }
    
    /// <summary>
    /// Set the assigned gamepad for multiplayer (called by MultiplayerManager)
    /// </summary>
    public void SetAssignedGamepad(Gamepad gamepad)
    {
        _assignedGamepad = gamepad;
    }

    /// <summary>
    /// Triggers a respawn - deactivates this player and spawns a new one
    /// </summary>
    /// <param name="isManual">If true, the respawn will not incur a penalty</param>
    public void Respawn(bool isManual = false)
    {
        if (spawnPoint != null)
        {
            // Check if respawning is blocked (last life in Battle Royale/Infinite Spawn mode)
            if (!isManual && BattleRoyaleManager.PreventRespawning)
            {
                // LAST LIFE - Don't respawn!
                // Notify Battle Royale Manager of final death
                BattleRoyaleManager battleRoyaleManager = FindFirstObjectByType<BattleRoyaleManager>();
                if (battleRoyaleManager != null)
                {
                    battleRoyaleManager.OnFinalDeath();
                }
                
                Debug.Log("<color=red>[Respawnable Player]</color> Respawning blocked - final death! Staying ragdolled...");
                
                // Don't respawn - let the player stay ragdolled
                // The Battle Royale Manager will trigger level finish after a delay
                return;
            }
            
            // Not blocked - track the death if there's a Battle Royale Manager
            if (!isManual)
            {
                BattleRoyaleManager battleRoyaleManager = FindFirstObjectByType<BattleRoyaleManager>();
                if (battleRoyaleManager != null)
                {
                    battleRoyaleManager.OnPlayerDeath();
                }
            }
            
            // Set static flag so points system knows not to apply penalty
            NextRespawnIsManual = isManual;
            
            // Disable shooting before respawning (so new player starts without gun)
            DefaultBehaviour behaviour = GetComponent<DefaultBehaviour>();
            if (behaviour != null)
            {
                behaviour.DisableShooting();
            }
            
            // Reset all guns in the scene before respawning
            GunPickup.ResetAllGuns();
            
            // Deactivate this player immediately
            gameObject.SetActive(false);
            
            // Spawn new player with same player ID
            GameObject newPlayer = spawnPoint.SpawnPlayer(playerID);
            
            // Transfer player ID to new player (in case spawn didn't set it)
            if (newPlayer != null)
            {
                RespawnablePlayer newRespawnable = newPlayer.GetComponent<RespawnablePlayer>();
                if (newRespawnable != null)
                {
                    newRespawnable.playerID = playerID;
                }
            }
            
            // Destroy old player
            Destroy(gameObject);
            
        }
        else
        {
// Debug.LogError("RespawnablePlayer: Cannot respawn - no spawn point assigned!");
        }
    }
}
