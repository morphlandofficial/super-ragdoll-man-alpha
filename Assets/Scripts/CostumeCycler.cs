using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles runtime costume cycling for player characters.
/// Attach to the Default Character prefab.
/// Requires costume cycling to be unlocked via cheat code.
/// </summary>
[RequireComponent(typeof(ActiveRagdoll.ActiveRagdoll))]
public class CostumeCycler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _costumeCycleCooldown = 0.2f;
    
    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;
    
    private ActiveRagdoll.ActiveRagdoll _activeRagdoll;
    private GameObject[] _costumes;
    private int _currentCostumeIndex = 0;
    private bool _cyclingUnlocked = false;
    
    // Button state tracking
    private bool _buttonWestWasPressed = false;
    private Gamepad _assignedGamepad; // Specific gamepad for this player (not Gamepad.current!)
    private int _playerID = 0; // Player ID for multiplayer costume tracking
    
    // Cooldown tracking (static so it persists across character respawns)
    private static float _lastCycleTime = -999f;
    private static bool _isCurrentlyCycling = false;
    
    private void Start()
    {
        _activeRagdoll = GetComponent<ActiveRagdoll.ActiveRagdoll>();
        
        // Get player ID from RespawnablePlayer component
        RespawnablePlayer respawnable = GetComponent<RespawnablePlayer>();
        if (respawnable != null)
        {
            _playerID = respawnable.playerID;
        }
        
        // Get assigned gamepad from MultiplayerGamepadController (for multiplayer)
        MultiplayerGamepadController gamepadController = GetComponent<MultiplayerGamepadController>();
        if (gamepadController != null && gamepadController.assignedGamepad != null)
        {
            _assignedGamepad = gamepadController.assignedGamepad;
            if (_debugMode)
                Debug.Log($"<color=cyan>[CostumeCycler]</color> Player {_playerID} using assigned gamepad");
        }
        else
        {
            // Single player mode - use Gamepad.current (will be set in Update)
            if (_debugMode)
                Debug.Log($"<color=cyan>[CostumeCycler]</color> Single player mode - will use Gamepad.current");
        }
        
        // Find all costumes
        FindCostumes();
        
        // Check if cycling is unlocked from cheat code
        _cyclingUnlocked = CheatCodeDetector.IsCostumeCyclingUnlocked();
        
        // Determine which costume is currently active
        DetectActiveCostume();
        
        if (_debugMode)
        {
            Debug.Log($"[CostumeCycler] Player {_playerID} initialized with {_costumes.Length} costumes. Starting costume: {(_costumes.Length > 0 ? _costumes[_currentCostumeIndex].name : "None")}");
            Debug.Log($"[CostumeCycler] Player {_playerID} cycling unlocked: {_cyclingUnlocked}");
        }
        
        // Log status on start
        if (_cyclingUnlocked && _debugMode)
        {
            Debug.Log($"<color=green>[CostumeCycler]</color> 🎨 Player {_playerID} costume cycling is UNLOCKED! Hold ButtonNorth (Tab/Y) + Press ButtonWest (X/Square) to cycle. Selection persists across respawns!");
        }
        else if (_debugMode)
        {
            Debug.Log($"<color=yellow>[CostumeCycler]</color> 🔒 Player {_playerID} costume cycling is LOCKED. Enter cheat code to unlock.");
        }
    }
    
    private void Update()
    {
        // CRITICAL: Re-check for assigned gamepad if not set (for Player 1 in multiplayer)
        if (_assignedGamepad == null)
        {
            MultiplayerGamepadController gamepadController = GetComponent<MultiplayerGamepadController>();
            if (gamepadController != null && gamepadController.assignedGamepad != null)
            {
                _assignedGamepad = gamepadController.assignedGamepad;
                if (_debugMode)
                    Debug.Log($"<color=cyan>[CostumeCycler]</color> Player {_playerID} dynamically found assigned gamepad");
            }
        }
        
        // Use assigned gamepad if available (multiplayer), otherwise Gamepad.current (single player)
        Gamepad gamepad = _assignedGamepad != null ? _assignedGamepad : Gamepad.current;
        
        // Only allow cycling if unlocked
        if (!_cyclingUnlocked)
        {
            // Check if player is trying to cycle while locked
            if (gamepad != null && gamepad.buttonNorth.isPressed && gamepad.buttonWest.wasPressedThisFrame)
            {
                Debug.Log($"<color=red>[CostumeCycler]</color> 🔒 Player {_playerID} COSTUME CYCLING IS LOCKED! Enter cheat code to unlock (Hold ButtonNorth + L1-R1-L1-R1-L1-R1)");
            }
            return; // Don't process any cycling input
        }
        
        if (gamepad == null)
            return;
        
        // BLOCK INPUT if currently cycling (respawn in progress)
        if (_isCurrentlyCycling)
        {
            if (_debugMode)
                Debug.Log($"<color=orange>[CostumeCycler]</color> ⏳ Player {_playerID} respawn in progress... please wait!");
            return;
        }
        
        // Check for ButtonNorth held + ButtonWest press
        bool buttonNorthHeld = gamepad.buttonNorth.isPressed;
        bool buttonWestPressed = gamepad.buttonWest.isPressed;
        
        // Check cooldown
        bool cooldownReady = (Time.time - _lastCycleTime) >= _costumeCycleCooldown;
        
        // Only cycle if ButtonNorth is held down AND ButtonWest is pressed AND cooldown is ready
        if (buttonNorthHeld && buttonWestPressed && !_buttonWestWasPressed && cooldownReady)
        {
            // DEBUG: Confirm which player and gamepad triggered this
            Debug.Log($"<color=cyan>[CostumeCycler]</color> 🎮 Player {_playerID} triggered costume change with gamepad: {gamepad?.name ?? "NULL"} (Assigned: {_assignedGamepad?.name ?? "NULL"}, Current: {Gamepad.current?.name ?? "NULL"})");
            
            CycleToNextCostume();
            _isCurrentlyCycling = true; // Lock further cycles
            _lastCycleTime = Time.time; // Start cooldown
        }
        else if (buttonNorthHeld && buttonWestPressed && !cooldownReady && _debugMode)
        {
            Debug.Log($"<color=yellow>[CostumeCycler]</color> ⏱️ Player {_playerID} cooldown active! Wait {(_costumeCycleCooldown - (Time.time - _lastCycleTime)):F1}s");
        }
        
        _buttonWestWasPressed = buttonWestPressed;
    }
    
    /// <summary>
    /// Find all costume hierarchies in the character
    /// </summary>
    private void FindCostumes()
    {
        System.Collections.Generic.List<GameObject> costumeList = new System.Collections.Generic.List<GameObject>();
        
        foreach (Transform child in transform)
        {
            // Skip camera and other systems
            if (child.name.Contains("Camera") || child.name.Contains("System"))
                continue;
            
            // Check if it has Animated/Physical structure (costume hierarchy)
            bool hasAnimated = child.Find("Animated") != null;
            bool hasPhysical = child.Find("Physical") != null;
            
            if (hasAnimated && hasPhysical)
            {
                costumeList.Add(child.gameObject);
            }
        }
        
        _costumes = costumeList.ToArray();
    }
    
    /// <summary>
    /// Detect which costume is currently active
    /// </summary>
    private void DetectActiveCostume()
    {
        for (int i = 0; i < _costumes.Length; i++)
        {
            if (_costumes[i].activeInHierarchy)
            {
                _currentCostumeIndex = i;
                return;
            }
        }
        
        // If no costume is active, activate the first one
        if (_costumes.Length > 0)
        {
            _costumes[0].SetActive(true);
            _currentCostumeIndex = 0;
        }
    }
    
    /// <summary>
    /// Cycle to the next costume by respawning with new costume
    /// </summary>
    public void CycleToNextCostume()
    {
        if (_costumes == null || _costumes.Length <= 1)
        {
            if (_debugMode)
                Debug.Log($"[CostumeCycler] Player {_playerID} not enough costumes to cycle!");
            return;
        }
        
        // Calculate next costume index
        int nextCostumeIndex = (_currentCostumeIndex + 1) % _costumes.Length;
        
        Debug.Log($"<color=cyan>[CostumeCycler]</color> 👕 Player {_playerID} cycling to costume: {_costumes[nextCostumeIndex].name} (will persist across respawns)");
        
        // Store the next costume index for THIS PLAYER for ALL future respawns (persistent)
        SetNextCostumeIndex(nextCostumeIndex, _playerID);
        
        // Trigger respawn through RespawnablePlayer
        var respawnable = GetComponent<RespawnablePlayer>();
        if (respawnable != null && respawnable.spawnPoint != null)
        {
            // Set flag so respawn doesn't count as death/manual respawn
            RespawnablePlayer.NextRespawnIsManual = false;
            
            // Use coroutine to properly sequence the respawn
            StartCoroutine(RespawnWithDelay(respawnable));
        }
        else
        {
            Debug.LogWarning("[CostumeCycler] Cannot cycle costume - no SpawnPoint found!");
        }
    }
    
    /// <summary>
    /// Respawn with new costume - instant, just like normal respawn
    /// </summary>
    private System.Collections.IEnumerator RespawnWithDelay(RespawnablePlayer respawnable)
    {
        Debug.Log("<color=cyan>[CostumeCycler]</color> 🔄 Starting costume cycle respawn...");
        
        // Tell SpawnPoint to handle the entire respawn sequence (instant, no cooldown)
        respawnable.spawnPoint.StartCostumeCycleRespawn(gameObject);
        
        // This GameObject will be destroyed by the SpawnPoint after spawning the new one
        yield break;
    }
    
    /// <summary>
    /// Store the costume index persistently for ALL future respawns (player-specific)
    /// </summary>
    private void SetNextCostumeIndex(int index, int playerID)
    {
        // Store with player-specific key so each player has their own costume preference
        string costumeKey = $"Player_{playerID}_CostumeIndex";
        string flagKey = $"Player_{playerID}_HasCostume";
        
        PlayerPrefs.SetInt(costumeKey, index);
        PlayerPrefs.SetInt(flagKey, 1); // Flag that this player has a stored value
        PlayerPrefs.Save();
        
        if (_debugMode)
            Debug.Log($"<color=cyan>[CostumeCycler]</color> Stored Player {playerID} costume index: {index}");
    }
    
    /// <summary>
    /// Get the stored costume index for a specific player (called by SpawnPoint)
    /// Returns -1 if no stored index
    /// </summary>
    public static int GetStoredCostumeIndex(int playerID)
    {
        string costumeKey = $"Player_{playerID}_CostumeIndex";
        string flagKey = $"Player_{playerID}_HasCostume";
        
        if (PlayerPrefs.GetInt(flagKey, 0) == 1)
        {
            return PlayerPrefs.GetInt(costumeKey, 0);
        }
        return -1; // No stored index for this player
    }
    
    /// <summary>
    /// Clear the stored costume indices for ALL players (for when resetting/starting new session)
    /// </summary>
    public static void ClearStoredCostumeIndex()
    {
        bool hadAnyStored = false;
        
        // Clear costume preferences for all possible players (1-4)
        for (int playerID = 0; playerID <= 4; playerID++)
        {
            string costumeKey = $"Player_{playerID}_CostumeIndex";
            string flagKey = $"Player_{playerID}_HasCostume";
            
            if (PlayerPrefs.GetInt(flagKey, 0) == 1)
            {
                hadAnyStored = true;
            }
            
            PlayerPrefs.DeleteKey(costumeKey);
            PlayerPrefs.DeleteKey(flagKey);
        }
        
        PlayerPrefs.Save();
        
        if (hadAnyStored)
        {
            Debug.Log("<color=cyan>[CostumeCycler]</color> 🔄 All player costume preferences cleared - will use default costumes on next spawn");
        }
    }
    
    /// <summary>
    /// Check if costume cycling has been used this session
    /// </summary>
    public static bool HasStoredCostume()
    {
        return PlayerPrefs.GetInt("HasStoredCostumeIndex", 0) == 1;
    }
    
    /// <summary>
    /// Unlock costume cycling (called by CheatCodeDetector)
    /// </summary>
    public void UnlockCostumeCycling()
    {
        _cyclingUnlocked = true;
        Debug.Log($"<color=green>[CostumeCycler]</color> 🎨 Player {_playerID} costume cycling UNLOCKED! Hold ButtonNorth (Tab/Y) + Press ButtonWest (X/Square) to cycle costumes. Your selection will persist across respawns!");
    }
    
    /// <summary>
    /// Get the name of the currently active costume
    /// </summary>
    public string GetCurrentCostumeName()
    {
        if (_costumes != null && _currentCostumeIndex < _costumes.Length)
            return _costumes[_currentCostumeIndex].name;
        return "None";
    }
    
    /// <summary>
    /// Get the index of the currently active costume
    /// </summary>
    public int GetCurrentCostumeIndex()
    {
        return _currentCostumeIndex;
    }
    
    /// <summary>
    /// Unlock the costume cycling flag (called by SpawnPoint after cooldown)
    /// </summary>
    public static void UnlockCyclingFlag()
    {
        _isCurrentlyCycling = false;
        Debug.Log("<color=green>[CostumeCycler]</color> ✅ Costume cycling unlocked - ready for next change!");
    }
}

