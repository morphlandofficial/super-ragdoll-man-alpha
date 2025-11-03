using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages PlayerInput component to prevent Gamepad.current conflicts in multiplayer.
/// Attach this to the player prefab.
/// In multiplayer, waits for MultiplayerManagerSimple to replace PlayerInput with MultiplayerGamepadController.
/// </summary>
[DefaultExecutionOrder(-100)] // Run BEFORE everything else
public class PlayerInputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    private bool isMultiplayer = false;
    private bool hasBeenSetup = false;
    
    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        
        if (playerInput == null)
        {
            Debug.LogWarning("[PlayerInputManager] No PlayerInput component found!");
            return;
        }
        
        // Check if multiplayer is active (2+ controllers detected)
        isMultiplayer = (Gamepad.all.Count >= 2);
        
        if (isMultiplayer)
        {
            // In multiplayer: Disable PlayerInput to prevent Gamepad.current conflicts
            // BUT keep this script enabled to monitor for MultiplayerGamepadController
            playerInput.enabled = false;
            Debug.Log($"[PlayerInputManager] Multiplayer detected ({Gamepad.all.Count} controllers) - PlayerInput disabled, waiting for setup...");
        }
        else
        {
            // Single player - keep PlayerInput enabled (uses Gamepad.current + keyboard + mouse)
            Debug.Log("[PlayerInputManager] Single player mode - PlayerInput enabled");
            hasBeenSetup = true;
        }
    }
    
    /// <summary>
    /// Called by MultiplayerManagerSimple when it has added MultiplayerGamepadController
    /// </summary>
    public void OnMultiplayerSetupComplete()
    {
        hasBeenSetup = true;
        Debug.Log("[PlayerInputManager] Multiplayer setup complete - player has MultiplayerGamepadController");
    }
    
    private void OnEnable()
    {
        // CRITICAL: Also check on enable (in case component gets re-enabled somehow)
        if (isMultiplayer && hasBeenSetup && playerInput != null && playerInput.enabled)
        {
            playerInput.enabled = false;
            Debug.Log("[PlayerInputManager] Re-disabled PlayerInput on OnEnable (multiplayer mode)");
        }
    }
    
    private void Update()
    {
        // CRITICAL: Check if playerInput still exists (may have been destroyed by MultiplayerManager)
        if (playerInput == null)
        {
            // PlayerInput was destroyed - this is normal in multiplayer
            // Disable this script since we don't need to monitor anymore
            enabled = false;
            return;
        }
        
        // CRITICAL: If multiplayer and not yet setup, check if we need emergency recovery
        if (isMultiplayer && !hasBeenSetup)
        {
            // Check if MultiplayerGamepadController has been added
            MultiplayerGamepadController gamepadController = GetComponent<MultiplayerGamepadController>();
            if (gamepadController != null)
            {
                // Setup complete!
                hasBeenSetup = true;
                Debug.Log("[PlayerInputManager] ✅ Detected MultiplayerGamepadController - setup complete!");
            }
            else
            {
                // Still waiting for MultiplayerManagerSimple to add controller
                // Warn if it's taking too long (might be a level-specific issue)
                if (Time.time > 2f) // After 2 seconds
                {
                    Debug.LogWarning($"[PlayerInputManager] ⚠️ Player has NO input! PlayerInput disabled, waiting for MultiplayerGamepadController... (Time: {Time.time:F1}s)");
                }
            }
        }
        
        // CRITICAL: Continuously enforce that PlayerInput stays disabled in multiplayer AFTER setup
        // (in case something else tries to enable it)
        if (isMultiplayer && hasBeenSetup && playerInput.enabled)
        {
            playerInput.enabled = false;
            Debug.LogWarning("[PlayerInputManager] ⚠️ PlayerInput was re-enabled by something - forcing it disabled again!");
        }
        
        // Debug: Show input state every few seconds
        if (Time.frameCount % 300 == 0) // Every ~5 seconds at 60fps
        {
            MultiplayerGamepadController gamepadController = GetComponent<MultiplayerGamepadController>();
            RespawnablePlayer respawnable = GetComponent<RespawnablePlayer>();
            int playerID = respawnable != null ? respawnable.playerID : 0;
            
            if (isMultiplayer)
            {
                bool hasGamepadController = gamepadController != null;
                string gamepadName = hasGamepadController && gamepadController.assignedGamepad != null 
                    ? gamepadController.assignedGamepad.name 
                    : "NULL";
                Debug.Log($"[PlayerInputManager] Player {playerID}: Multiplayer mode | PlayerInput: {(playerInput != null && playerInput.enabled ? "ENABLED ⚠️" : "disabled ✅")} | MultiplayerGamepadController: {(hasGamepadController ? "✅" : "❌")} | Gamepad: {gamepadName} | Setup: {(hasBeenSetup ? "✅" : "⏳ WAITING")}");
            }
            else
            {
                Debug.Log($"[PlayerInputManager] Player {playerID}: Single-player mode | PlayerInput: {(playerInput != null && playerInput.enabled ? "enabled ✅" : "DISABLED ⚠️")}");
            }
        }
    }
}

