using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

/// <summary>
/// FINAL SIMPLE VERSION
/// 
/// Player 1: Normal spawn, keyboard/mouse + controller 1
/// Players 2-4: Additional controllers spawn when they press START
/// </summary>
public class MultiplayerManagerSimple : MonoBehaviour
{
    [Header("Settings")]
    public bool enableMultiplayer = true;
    [Tooltip("Prevents additional players from joining, but still sets up Player 1's input correctly for multiplayer gamepads")]
    public bool preventAdditionalPlayers = false;
    public float playerSpawnOffset = 3f;
    public float viewportPadding = 0.01f;
    
    public static MultiplayerManagerSimple Instance { get; private set; }
    
    private List<PlayerData> players = new List<PlayerData>();
    private GameObject playerPrefab;
    private Transform spawnTransform;
    private bool multiplayerReady = false;
    private HashSet<Gamepad> usedGamepads = new HashSet<Gamepad>();
    
    // Player 3 waits for Player 4 before spawning (3-player mode not supported)
    private Gamepad waitingPlayer3Gamepad = null;
    
    private class PlayerData
    {
        public GameObject playerObject;
        public Camera playerCamera;
        public int playerIndex;
        public Gamepad assignedGamepad; // Track which gamepad this player uses (null for Player 1)
        public bool isRespawning = false; // Flag to prevent double-processing respawns
    }
    
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        if (!enableMultiplayer)
        {
            Debug.Log("[Multiplayer] Disabled");
            Destroy(this);
            return;
        }
        
        // Need 2+ controllers for multiplayer
        if (Gamepad.all.Count < 2)
        {
            Debug.Log($"[Multiplayer] Only {Gamepad.all.Count} controller(s) - single player mode");
            Destroy(this);
            return;
        }
        
        Debug.Log($"[Multiplayer] {Gamepad.all.Count} controllers detected - multiplayer available!");
    }
    
    private void Start()
    {
        StartCoroutine(SetupMultiplayer());
    }
    
    private IEnumerator SetupMultiplayer()
    {
        // CRITICAL: Find Player 1 as soon as they spawn (check every frame, don't wait)
        RespawnablePlayer player1 = null;
        int maxAttempts = 100; // Safety limit (10 seconds at 0.1s per attempt)
        int attempts = 0;
        
        while (player1 == null && attempts < maxAttempts)
        {
            player1 = FindFirstObjectByType<RespawnablePlayer>();
            if (player1 == null)
            {
                yield return new WaitForSeconds(0.1f); // Check every 0.1 seconds
                attempts++;
            }
        }
        
        if (player1 == null)
        {
            Debug.LogError("[Multiplayer] Player 1 not found after waiting!");
            yield break;
        }
        
        Debug.Log($"[Multiplayer] Found Player 1 after {attempts * 0.1f:F1} seconds");
        
        // Get spawn info
        SpawnPoint sp = FindFirstObjectByType<SpawnPoint>();
        if (sp != null)
        {
            playerPrefab = sp.playerPrefab;
            spawnTransform = sp.transform;
        }
        
        if (playerPrefab == null)
        {
            Debug.LogError("[Multiplayer] No player prefab found!");
            yield break;
        }
        
        // CRITICAL: Lock Player 1 to gamepad IMMEDIATELY to prevent Player 2's gamepad from affecting them
        Gamepad player1Gamepad = Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
        
        // Assign Player 1 ID
        player1.playerID = 1;
        Debug.Log("[Multiplayer] Assigned Player 1 ID = 1");
        
        // Lock Player 1 to their specific gamepad (prevent Gamepad.current confusion)
        if (player1Gamepad != null)
        {
            // Remove PlayerInput component (uses Gamepad.current)
            PlayerInput playerInput = player1.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                Destroy(playerInput);
                Debug.Log("[Multiplayer] Removed Player 1's PlayerInput - switching to assigned gamepad");
            }
            
            // Add MultiplayerGamepadController with assigned gamepad
            MultiplayerGamepadController gamepadController = player1.gameObject.AddComponent<MultiplayerGamepadController>();
            gamepadController.assignedGamepad = player1Gamepad;
            gamepadController.allowKeyboardInput = true; // Player 1 can use keyboard + controller 1
            Debug.Log("[Multiplayer] ✅ Player 1 locked to Gamepad.all[0] + Keyboard - no more Gamepad.current confusion!");
            
            // CRITICAL: Set assigned gamepad on RespawnablePlayer for respawn button detection (R1)
            player1.SetAssignedGamepad(player1Gamepad);
            Debug.Log("[Multiplayer] Set Player 1's assigned gamepad for R1 respawn detection");
            
            // Notify PlayerInputManager that setup is complete
            PlayerInputManager inputManager = player1.GetComponent<PlayerInputManager>();
            if (inputManager != null)
            {
                inputManager.OnMultiplayerSetupComplete();
            }
            
            // Mark first gamepad as used
            usedGamepads.Add(player1Gamepad);
        }
        
        // Register Player 1 in our tracking
        Camera player1Cam = player1.GetComponentInChildren<Camera>();
        PlayerData p1Data = new PlayerData
        {
            playerObject = player1.gameObject,
            playerCamera = player1Cam,
            playerIndex = 0,
            assignedGamepad = player1Gamepad
        };
        players.Add(p1Data);
        
        // MULTIPLAYER: Disable pixelation effect on Player 1's camera (makes UI text readable)
        if (player1Cam != null)
        {
            CameraVisualController.DisablePixelationOnCamera(player1Cam);
        }
        
        // CRITICAL: Notify MultiplayerUIManager to create UI for Player 1
        MultiplayerUIManager uiManager = FindFirstObjectByType<MultiplayerUIManager>();
        if (uiManager != null)
        {
            uiManager.OnPlayerJoined(player1.gameObject, 1); // Player 1 has ID = 1
        }
        
        multiplayerReady = true;
        
        if (preventAdditionalPlayers)
        {
            Debug.Log($"[Multiplayer] Ready! Player 1 locked to controller 1. Additional players PREVENTED from joining (single-player mode).");
        }
        else
        {
            Debug.Log($"[Multiplayer] Ready! Player 1 locked to controller 1. Press JUMP (A button) on controller 2+ to join!");
        }
    }
    
    private void Update()
    {
        if (!multiplayerReady) return;
        
        // Check for respawned players and re-setup them
        CheckForRespawnedPlayers();
        
        // SINGLE-PLAYER LOCK: Skip additional player join detection if prevented
        if (preventAdditionalPlayers)
        {
            return; // Player 1 is set up, but no more players can join
        }
        
        // Check for additional controllers pressing JUMP to join
        for (int i = 1; i < Gamepad.all.Count && players.Count < 4; i++)
        {
            Gamepad gamepad = Gamepad.all[i];
            
            // Skip if already used (or waiting as Player 3)
            if (usedGamepads.Contains(gamepad) || gamepad == waitingPlayer3Gamepad)
            {
                continue;
            }
            
            // Check if JUMP button (A / ButtonSouth) was pressed
            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                // Special handling for 3rd player (must wait for 4th)
                if (players.Count == 2 && waitingPlayer3Gamepad == null)
                {
                    // Register Player 3 but don't spawn yet
                    waitingPlayer3Gamepad = gamepad;
                    usedGamepads.Add(gamepad);
                    Debug.Log("[Multiplayer] Player 3 controller registered - waiting for Player 4 to join (3-player mode not supported)");
                }
                else if (players.Count == 2 && waitingPlayer3Gamepad != null)
                {
                    // Player 4 joined - spawn both Player 3 and Player 4
                    Debug.Log("[Multiplayer] Player 4 joined - spawning Players 3 & 4 together!");
                    StartCoroutine(SpawnPlayersThreeAndFour(waitingPlayer3Gamepad, gamepad));
                }
                else
                {
                    // Normal spawn (Player 2, or if somehow we skipped to 4 players)
                    StartCoroutine(SpawnPlayer(gamepad));
                }
            }
        }
    }
    
    private void CheckForRespawnedPlayers()
    {
        // Get all current RespawnablePlayer objects in the scene
        RespawnablePlayer[] allPlayers = FindObjectsByType<RespawnablePlayer>(FindObjectsSortMode.None);
        
        // Check if any player has been destroyed/respawned
        for (int i = 0; i < players.Count; i++)
        {
            PlayerData playerData = players[i];
            
            // Skip if already being processed via callback
            if (playerData.isRespawning)
            {
                continue;
            }
            
            // If player object is null or inactive, they've been destroyed/respawned
            if (playerData.playerObject == null || !playerData.playerObject.activeInHierarchy)
            {
                // Find the NEW player that's not in our list yet
                GameObject newPlayerObject = null;
                
                foreach (RespawnablePlayer player in allPlayers)
                {
                    bool isTracked = false;
                    
                    // Check if this player is already tracked
                    foreach (PlayerData pd in players)
                    {
                        if (pd.playerObject == player.gameObject && pd.playerObject != null)
                        {
                            isTracked = true;
                            break;
                        }
                    }
                    
                    // If not tracked, this is the newly spawned player
                    if (!isTracked)
                    {
                        newPlayerObject = player.gameObject;
                        break;
                    }
                }
                
                if (newPlayerObject != null)
                {
                    Debug.Log($"[Multiplayer] Player {i + 1} respawned - reconfiguring...");
                    
                    // Re-setup this player with a small delay
                    StartCoroutine(SetupRespawnedPlayerDelayed(newPlayerObject, playerData));
                    
                    // Only handle one respawn per frame to avoid confusion
                    return;
                }
            }
        }
    }
    
    private IEnumerator SetupRespawnedPlayerDelayed(GameObject newPlayerObject, PlayerData oldPlayerData)
    {
        // Wait a frame for the new player to fully initialize
        yield return new WaitForEndOfFrame();
        
        SetupRespawnedPlayer(newPlayerObject, oldPlayerData);
    }
    
    /// <summary>
    /// Public method for external systems (like SpawnPoint) to notify us of a respawn
    /// </summary>
    public void OnPlayerRespawned(GameObject oldPlayer, GameObject newPlayer)
    {
        if (oldPlayer == null || newPlayer == null)
        {
            Debug.LogWarning("[Multiplayer] OnPlayerRespawned called with null player!");
            return;
        }
        
        // Find which player this was
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].playerObject == oldPlayer)
            {
                Debug.Log($"[Multiplayer] Player {i + 1} respawn detected via callback - reconfiguring immediately...");
                
                // Mark as respawning to prevent polling system from double-processing
                players[i].isRespawning = true;
                
                SetupRespawnedPlayer(newPlayer, players[i]);
                
                // Clear flag after processing
                players[i].isRespawning = false;
                
                return;
            }
        }
        
        // CRITICAL: If player not found in list, might be Player 1 respawning before SetupMultiplayer finished
        // Set up Player 1 with multiplayer input immediately
        if (players.Count == 0 && Gamepad.all.Count >= 2)
        {
            Debug.Log("[Multiplayer] Player 1 respawned before setup completed - configuring as multiplayer player...");
            
            RespawnablePlayer respawnable = newPlayer.GetComponent<RespawnablePlayer>();
            if (respawnable != null)
            {
                respawnable.playerID = 1;
            }
            
            // Remove PlayerInput and add MultiplayerGamepadController
            PlayerInput playerInput = newPlayer.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                Destroy(playerInput);
            }
            
            Gamepad player1Gamepad = Gamepad.all[0];
            MultiplayerGamepadController gamepadController = newPlayer.AddComponent<MultiplayerGamepadController>();
            gamepadController.assignedGamepad = player1Gamepad;
            gamepadController.allowKeyboardInput = true;
            
            // MULTIPLAYER: Disable pixelation effect on early respawn
            Camera earlyRespawnCam = newPlayer.GetComponentInChildren<Camera>();
            if (earlyRespawnCam != null)
            {
                CameraVisualController.DisablePixelationOnCamera(earlyRespawnCam);
            }
            
            Debug.Log("[Multiplayer] Configured early respawn with Gamepad.all[0] + Keyboard");
            return;
        }
        
        Debug.LogWarning($"[Multiplayer] OnPlayerRespawned: Could not find old player in tracked players list (players.Count={players.Count}, oldPlayer={oldPlayer?.name})");
    }
    
    private void SetupRespawnedPlayer(GameObject newPlayerObject, PlayerData oldPlayerData)
    {
        int playerIndex = oldPlayerData.playerIndex;
        
        // Update player data
        oldPlayerData.playerObject = newPlayerObject;
        oldPlayerData.playerCamera = newPlayerObject.GetComponentInChildren<Camera>();
        
        // MULTIPLAYER: Disable pixelation effect on respawned player's camera (makes UI text readable)
        if (oldPlayerData.playerCamera != null)
        {
            CameraVisualController.DisablePixelationOnCamera(oldPlayerData.playerCamera);
        }
        
        // CRITICAL: Re-assign player ID
        RespawnablePlayer respawnable = newPlayerObject.GetComponent<RespawnablePlayer>();
        if (respawnable != null)
        {
            respawnable.playerID = playerIndex + 1; // Player 1 = ID 1, etc.
            Debug.Log($"[Multiplayer] Reassigned Player {playerIndex + 1} ID = {playerIndex + 1} after respawn");
        }
        
        if (playerIndex == 0)
        {
            // Player 1 respawned
            // Only lock to gamepad if multiplayer mode is active (2+ controllers detected)
            if (Gamepad.all.Count >= 2)
            {
                Debug.Log("[Multiplayer] Player 1 respawned in multiplayer - re-locking to gamepad...");
                
                // Remove PlayerInput if it exists and add MultiplayerGamepadController
                PlayerInput playerInput = newPlayerObject.GetComponent<PlayerInput>();
                if (playerInput != null)
                {
                    Destroy(playerInput);
                    Debug.Log("[Multiplayer] Removed PlayerInput from respawned Player 1");
                }
                
                // Re-add MultiplayerGamepadController with assigned gamepad
                MultiplayerGamepadController gamepadController = newPlayerObject.GetComponent<MultiplayerGamepadController>();
                if (gamepadController == null && oldPlayerData.assignedGamepad != null)
                {
                    gamepadController = newPlayerObject.AddComponent<MultiplayerGamepadController>();
                    gamepadController.assignedGamepad = oldPlayerData.assignedGamepad;
                    gamepadController.allowKeyboardInput = true; // Player 1 can use keyboard
                    Debug.Log("[Multiplayer] ✅ Re-added MultiplayerGamepadController to Player 1 with assigned gamepad + keyboard");
                }
                else if (gamepadController != null)
                {
                    // Already has gamepad controller, just make sure gamepad is assigned and keyboard enabled
                    gamepadController.assignedGamepad = oldPlayerData.assignedGamepad;
                    gamepadController.allowKeyboardInput = true;
                    Debug.Log("[Multiplayer] ✅ Player 1 already has MultiplayerGamepadController, reassigned gamepad + keyboard");
                }
                
                // Notify PlayerInputManager that setup is complete
                PlayerInputManager inputManager = newPlayerObject.GetComponent<PlayerInputManager>();
                if (inputManager != null)
                {
                    inputManager.OnMultiplayerSetupComplete();
                }
                
                // CRITICAL: Notify MultiplayerUIManager to update camera reference for respawn
                MultiplayerUIManager uiManager = FindFirstObjectByType<MultiplayerUIManager>();
                if (uiManager != null)
                {
                    uiManager.OnPlayerJoined(newPlayerObject, playerIndex + 1);
                }
                
                // Set the assigned gamepad on RespawnablePlayer for respawn button detection
                if (respawnable != null)
                {
                    respawnable.SetAssignedGamepad(oldPlayerData.assignedGamepad);
                }
            }
            else
            {
                // Single player mode - don't lock input
                Debug.Log("[Multiplayer] Player 1 respawned in single-player mode - input unchanged");
            }
        }
        else
        {
            // Players 2-4 - remove PlayerInput and add gamepad controller
            PlayerInput playerInput = newPlayerObject.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                Destroy(playerInput);
            }
            
            MultiplayerGamepadController gamepadController = newPlayerObject.GetComponent<MultiplayerGamepadController>();
            if (gamepadController == null)
            {
                gamepadController = newPlayerObject.AddComponent<MultiplayerGamepadController>();
            }
            gamepadController.assignedGamepad = oldPlayerData.assignedGamepad;
            
            // Notify PlayerInputManager that setup is complete
            PlayerInputManager inputManager = newPlayerObject.GetComponent<PlayerInputManager>();
            if (inputManager != null)
            {
                inputManager.OnMultiplayerSetupComplete();
            }
            
            // CRITICAL: Notify MultiplayerUIManager to update camera reference for respawn
            MultiplayerUIManager uiManager = FindFirstObjectByType<MultiplayerUIManager>();
            if (uiManager != null)
            {
                uiManager.OnPlayerJoined(newPlayerObject, playerIndex + 1);
            }
            
            // Disable audio listener on respawned player (only Player 1 should have audio)
            Camera playerCam = newPlayerObject.GetComponentInChildren<Camera>();
            if (playerCam != null)
            {
                AudioListener audioListener = playerCam.GetComponent<AudioListener>();
                if (audioListener != null)
                {
                    audioListener.enabled = false;
                }
            }
            
            // Set the assigned gamepad on RespawnablePlayer for respawn button detection
            if (respawnable != null)
            {
                respawnable.SetAssignedGamepad(oldPlayerData.assignedGamepad);
                
                // CRITICAL: Set the spawn point so they can respawn again!
                SpawnPoint sp = FindFirstObjectByType<SpawnPoint>();
                if (sp != null)
                {
                    respawnable.spawnPoint = sp;
                }
            }
            
            Debug.Log($"[Multiplayer] Player {playerIndex + 1} respawned with gamepad {playerIndex + 1}");
        }
        
        // Update split screen
        UpdateSplitScreen();
        
        // Disable all audio listeners except Player 1's
        DisableExtraAudioListeners();
    }
    
    private void DisableExtraAudioListeners()
    {
        // Find all audio listeners in the scene
        AudioListener[] allListeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        
        int disabledCount = 0;
        foreach (AudioListener listener in allListeners)
        {
            // Check if this listener belongs to Player 1
            bool isPlayer1 = false;
            if (players.Count > 0 && players[0].playerObject != null)
            {
                // Check if this listener is a child of Player 1
                if (listener.transform.IsChildOf(players[0].playerObject.transform))
                {
                    isPlayer1 = true;
                }
            }
            
            // Disable if not Player 1's listener
            if (!isPlayer1)
            {
                listener.enabled = false;
                disabledCount++;
            }
        }
        
        if (disabledCount > 0)
        {
            Debug.Log($"[Multiplayer] Disabled {disabledCount} extra AudioListener(s)");
        }
    }
    
    /// <summary>
    /// Spawn both Player 3 and Player 4 together (skips 3-player mode)
    /// </summary>
    private IEnumerator SpawnPlayersThreeAndFour(Gamepad player3Gamepad, Gamepad player4Gamepad)
    {
        Debug.Log("[Multiplayer] Spawning Players 3 & 4 together for 4-player quad split!");
        
        // Mark both gamepads as used
        usedGamepads.Add(player3Gamepad);
        usedGamepads.Add(player4Gamepad);
        
        // Spawn Player 3 (index 2)
        yield return StartCoroutine(SpawnPlayerInternal(player3Gamepad, 2));
        
        // Spawn Player 4 (index 3)
        yield return StartCoroutine(SpawnPlayerInternal(player4Gamepad, 3));
        
        // Clear the waiting flag
        waitingPlayer3Gamepad = null;
        
        // Update split screen to 4-player layout
        UpdateSplitScreen();
        
        Debug.Log("[Multiplayer] ✅ All 4 players active - quad split screen enabled!");
    }
    
    private IEnumerator SpawnPlayer(Gamepad gamepad)
    {
        if (players.Count >= 4)
        {
            Debug.LogWarning("[Multiplayer] Max 4 players!");
            yield break;
        }
        
        // Use the current player count as the player index
        int playerIndex = players.Count;
        yield return StartCoroutine(SpawnPlayerInternal(gamepad, playerIndex));
    }
    
    /// <summary>
    /// Internal spawn logic that accepts a specific player index
    /// </summary>
    private IEnumerator SpawnPlayerInternal(Gamepad gamepad, int playerIndex)
    {
        if (players.Count >= 4)
        {
            Debug.LogWarning("[Multiplayer] Max 4 players!");
            yield break;
        }
        
        usedGamepads.Add(gamepad); // Mark as used immediately
        
        // playerIndex is passed as a parameter (for Player 3+4, they spawn together but have indices 2 and 3)
        
        // Spawn new player
        Vector3 offset = spawnTransform.right * (playerIndex * playerSpawnOffset);
        GameObject newPlayer = Instantiate(playerPrefab, spawnTransform.position + offset, spawnTransform.rotation);
        
        // CRITICAL: Set assigned gamepad on RespawnablePlayer IMMEDIATELY (before Update() can run!)
        RespawnablePlayer respawnable = newPlayer.GetComponent<RespawnablePlayer>();
        if (respawnable != null)
        {
            respawnable.playerID = playerIndex + 1; // Assign player ID first
            respawnable.SetAssignedGamepad(gamepad); // Set gamepad IMMEDIATELY to prevent Gamepad.current fallback
            Debug.Log($"[Multiplayer] ⚡ Set Player {playerIndex + 1}'s gamepad IMMEDIATELY after spawn (prevents R1 crossfire)");
        }
        
        // CRITICAL: Remove PlayerInput component entirely for players 2-4
        // We'll manually read their gamepad instead
        PlayerInput playerInput = newPlayer.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            Destroy(playerInput);
            Debug.Log($"[Multiplayer] Removed PlayerInput from Player {players.Count + 1} - will use manual gamepad input");
        }
        
        // Add custom gamepad input handler
        MultiplayerGamepadController gamepadController = newPlayer.AddComponent<MultiplayerGamepadController>();
        gamepadController.assignedGamepad = gamepad;
        
        // Notify PlayerInputManager that setup is complete
        PlayerInputManager inputManager = newPlayer.GetComponent<PlayerInputManager>();
        if (inputManager != null)
        {
            inputManager.OnMultiplayerSetupComplete();
        }
        
        // Player ID already set above (line 532-533)
        if (respawnable != null)
        {
            Debug.Log($"[Multiplayer] Assigned Player {playerIndex + 1} ID = {playerIndex + 1}");
            
            // CRITICAL: Set the spawn point so they can respawn!
            SpawnPoint sp = FindFirstObjectByType<SpawnPoint>();
            if (sp != null)
            {
                respawnable.spawnPoint = sp;
                Debug.Log($"[Multiplayer] Set spawn point for Player {playerIndex + 1}");
                
                // CRITICAL: Activate the costume from the spawn point (so Player 2+ gets the right costume on join)
                sp.ActivateSelectedCostume(newPlayer, playerIndex + 1);
                Debug.Log($"[Multiplayer] ✅ Activated spawn point costume for Player {playerIndex + 1}");
                
                // Refresh costume references after switching costumes
                var activeRagdoll = newPlayer.GetComponent<ActiveRagdoll.ActiveRagdoll>();
                if (activeRagdoll != null)
                {
                    activeRagdoll.RefreshCostumeReferences();
                }
                
                var audioController = newPlayer.GetComponent<CharacterAudioController>();
                if (audioController != null)
                {
                    audioController.RefreshBodyPartTriggers();
                }
                
                var timeRewind = newPlayer.GetComponent<TimeRewindController>();
                if (timeRewind != null)
                {
                    timeRewind.RefreshCostumeReferences();
                }
                
                // CRITICAL: Setup collision listeners AFTER costume is fully initialized
                var pointsCollector = newPlayer.GetComponent<RagdollPointsCollector>();
                if (pointsCollector != null)
                {
                    pointsCollector.SetupCollisionListeners();
                    Debug.Log($"[Multiplayer] 💥 Setup collision listeners for Player {playerIndex + 1} physics points");
                }
            }
            else
            {
                Debug.LogError("[Multiplayer] No spawn point found - respawning may not work!");
            }
        }
        
        // CRITICAL: Wait one frame for camera to fully initialize after costume activation
        // (Same timing issue as costume - camera module needs a frame to set up)
        yield return null;
        
        // CRITICAL: Copy gun/ammo state from Player 1 if they already have a gun
        // (Prevents issue where Player 2 joins after Player 1 collected guns)
        if (playerIndex > 0 && players.Count > 0) // Player 2+ joining
        {
            CopyGunStateFromPlayer1(newPlayer);
        }
        
        // Get camera and disable audio listener (only Player 1 should have audio)
        Camera playerCam = newPlayer.GetComponentInChildren<Camera>();
        if (playerCam != null)
        {
            AudioListener audioListener = playerCam.GetComponent<AudioListener>();
            if (audioListener != null)
            {
                audioListener.enabled = false;
                Debug.Log($"[Multiplayer] Disabled AudioListener on Player {players.Count + 1}'s camera");
            }
            
            // MULTIPLAYER: Disable pixelation effect (makes UI text readable)
            CameraVisualController.DisablePixelationOnCamera(playerCam);
        }
        
        // Register player
        PlayerData data = new PlayerData
        {
            playerObject = newPlayer,
            playerCamera = playerCam,
            playerIndex = playerIndex,
            assignedGamepad = gamepad
        };
        players.Add(data);
        
        Debug.Log($"[Multiplayer] Player {players.Count} joined with controller {playerIndex + 1}!");
        
        // CRITICAL: Notify MultiplayerUIManager to create UI for this player
        Debug.Log($"<color=cyan>[Multiplayer]</color> Looking for MultiplayerUIManager to create UI for Player {playerIndex + 1}...");
        MultiplayerUIManager uiManager = FindFirstObjectByType<MultiplayerUIManager>();
        if (uiManager != null)
        {
            Debug.Log($"<color=green>[Multiplayer]</color> Found MultiplayerUIManager - calling OnPlayerJoined for Player {playerIndex + 1}");
            uiManager.OnPlayerJoined(newPlayer, playerIndex + 1);
        }
        else
        {
            Debug.LogError("<color=red>[Multiplayer]</color> MultiplayerUIManager NOT FOUND! Can't create per-player UI!");
        }
        
        // Update split screen
        UpdateSplitScreen();
        
        // Disable all audio listeners except Player 1's
        DisableExtraAudioListeners();
    }
    
    private void LockPlayer1ToGamepadOnly()
    {
        if (players.Count == 0) return;
        
        PlayerData player1Data = players[0];
        PlayerInput player1Input = player1Data.playerObject.GetComponent<PlayerInput>();
        
        if (player1Input != null && player1Data.assignedGamepad != null)
        {
            Debug.Log($"[Multiplayer] Before unpairing - Player 1 has {player1Input.user.pairedDevices.Count} paired devices:");
            foreach (var device in player1Input.user.pairedDevices)
            {
                Debug.Log($"  - {device.name} ({device.GetType().Name})");
            }
            
            // Unpair ALL devices using UnpairDevice instead
            var pairedDevices = new System.Collections.Generic.List<InputDevice>(player1Input.user.pairedDevices);
            foreach (var device in pairedDevices)
            {
                player1Input.user.UnpairDevice(device);
                Debug.Log($"[Multiplayer] Unpaired device: {device.name}");
            }
            
            // Pair ONLY gamepad 0
            InputUser.PerformPairingWithDevice(player1Data.assignedGamepad, player1Input.user);
            Debug.Log($"[Multiplayer] Paired Player 1 to: {player1Data.assignedGamepad.name}");
            
            player1Input.neverAutoSwitchControlSchemes = true;
            
            // Explicitly switch to Gamepad control scheme
            try
            {
                player1Input.SwitchCurrentControlScheme("Gamepad", player1Data.assignedGamepad);
                Debug.Log("[Multiplayer] Switched control scheme to Gamepad");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Multiplayer] Failed to switch control scheme: {e.Message}");
            }
            
            Debug.Log($"[Multiplayer] After pairing - Player 1 has {player1Input.user.pairedDevices.Count} paired devices:");
            foreach (var device in player1Input.user.pairedDevices)
            {
                Debug.Log($"  - {device.name} ({device.GetType().Name})");
            }
            Debug.Log($"[Multiplayer] Current control scheme: {player1Input.currentControlScheme}");
            
            // Set the assigned gamepad on RespawnablePlayer for respawn button detection
            RespawnablePlayer respawnable = player1Data.playerObject.GetComponent<RespawnablePlayer>();
            if (respawnable != null)
            {
                respawnable.SetAssignedGamepad(player1Data.assignedGamepad);
                Debug.Log("[Multiplayer] Set Player 1's assigned gamepad for respawn detection");
            }
            
            Debug.Log("[Multiplayer] Player 1 locked to gamepad 0 only (keyboard/mouse disabled)");
        }
    }
    
    /// <summary>
    /// Copy gun/ammo state from Player 1 to a newly joined player
    /// This ensures players joining late don't miss out on gun pickups
    /// </summary>
    private void CopyGunStateFromPlayer1(GameObject newPlayer)
    {
        // Get Player 1's data
        if (players.Count == 0 || players[0].playerObject == null)
        {
            Debug.LogWarning("[Multiplayer] Cannot copy gun state - Player 1 not found");
            return;
        }
        
        GameObject player1 = players[0].playerObject;
        
        // Get both player's DefaultBehaviour components
        DefaultBehaviour player1Behaviour = player1.GetComponent<DefaultBehaviour>();
        DefaultBehaviour newPlayerBehaviour = newPlayer.GetComponent<DefaultBehaviour>();
        
        if (player1Behaviour == null || newPlayerBehaviour == null)
        {
            // Players don't have DefaultBehaviour (might be using TitlePlayerDefaultBehavior)
            return;
        }
        
        // Check if Player 1 has shooting enabled
        if (player1Behaviour.IsShootingEnabled())
        {
            Debug.Log($"<color=cyan>[Multiplayer]</color> Player 1 has gun - copying gun state to new player...");
            
            // Copy hand items (get visible items from Player 1)
            string leftHandItem = player1Behaviour.GetActiveLeftHandItem();
            string rightHandItem = player1Behaviour.GetActiveRightHandItem();
            
            if (!string.IsNullOrEmpty(leftHandItem) || !string.IsNullOrEmpty(rightHandItem))
            {
                newPlayerBehaviour.ShowSpecificHandItems(leftHandItem, rightHandItem);
                Debug.Log($"<color=green>[Multiplayer]</color> Copied hand items - Left: {leftHandItem}, Right: {rightHandItem}");
            }
            
            // Copy shooting state (enabled + ammo)
            int player1Ammo = player1Behaviour.GetCurrentAmmo();
            bool player1HasInfiniteAmmo = player1Behaviour.HasInfiniteAmmo();
            
            if (player1HasInfiniteAmmo)
            {
                newPlayerBehaviour.EnableShooting(); // Infinite ammo
                Debug.Log($"<color=green>[Multiplayer]</color> ✅ New player given gun with infinite ammo (matching Player 1)");
            }
            else
            {
                newPlayerBehaviour.EnableShooting(player1Ammo);
                Debug.Log($"<color=green>[Multiplayer]</color> ✅ New player given gun with {player1Ammo} ammo (matching Player 1)");
            }
        }
        else
        {
            Debug.Log($"<color=yellow>[Multiplayer]</color> Player 1 has no gun - new player spawns without gun");
        }
    }
    
    private void UpdateSplitScreen()
    {
        int count = players.Count;
        
        if (count == 1)
        {
            // Single player - full screen
            SetViewport(players[0].playerCamera, new Rect(0, 0, 1, 1));
        }
        else if (count == 2)
        {
            // Two players - horizontal split (top/bottom)
            float h = 0.5f - viewportPadding / 2f;
            SetViewport(players[0].playerCamera, new Rect(0, 0.5f + viewportPadding / 2f, 1, h));
            SetViewport(players[1].playerCamera, new Rect(0, 0, 1, h));
        }
        else if (count == 3)
        {
            // 3-player mode NOT SUPPORTED - maintain 2-player layout while waiting for Player 4
            Debug.LogWarning("[Multiplayer] 3-player mode not supported - waiting for Player 4 to join");
            float h = 0.5f - viewportPadding / 2f;
            SetViewport(players[0].playerCamera, new Rect(0, 0.5f + viewportPadding / 2f, 1, h));
            SetViewport(players[1].playerCamera, new Rect(0, 0, 1, h));
        }
        else if (count == 4)
        {
            // Four players - quad split (2x2 grid)
            float w = 0.5f - viewportPadding / 2f;
            float h = 0.5f - viewportPadding / 2f;
            
            SetViewport(players[0].playerCamera, new Rect(0, 0.5f + viewportPadding / 2f, w, h)); // Top-left
            SetViewport(players[1].playerCamera, new Rect(0.5f + viewportPadding / 2f, 0.5f + viewportPadding / 2f, w, h)); // Top-right
            SetViewport(players[2].playerCamera, new Rect(0, 0, w, h)); // Bottom-left
            SetViewport(players[3].playerCamera, new Rect(0.5f + viewportPadding / 2f, 0, w, h)); // Bottom-right
        }
        
        Debug.Log($"[Multiplayer] Split screen updated for {count} players");
    }
    
    private void SetViewport(Camera cam, Rect rect)
    {
        if (cam != null)
        {
            cam.rect = rect;
        }
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
