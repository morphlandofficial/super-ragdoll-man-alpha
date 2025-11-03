using UnityEngine;

/// <summary>
/// Add this component to a GameObject to mark it as a spawn point.
/// Spawns a player prefab at this location.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("The player prefab to spawn")]
    public GameObject playerPrefab;
    
    [HideInInspector]
    [Tooltip("Index of the costume to spawn with (set by editor)")]
    public int selectedCostumeIndex = 0;
    
    [HideInInspector]
    [Tooltip("Name of the selected costume (for display)")]
    public string selectedCostumeName = "";

    [Tooltip("Offset from this object's position where the player will spawn")]
    public Vector3 spawnOffset = Vector3.zero;

    [Tooltip("Should the player match this object's rotation when spawning?")]
    public bool useRotation = true;

    [Tooltip("Should spawn a player when the level starts?")]
    public bool spawnOnStart = true;
    
    [Tooltip("If true, will spawn even if a player already exists in the scene")]
    public bool forceSpawnIfExists = false;
    
    [Header("Race Settings")]
    [Tooltip("Enable race mode for this spawned player (only works if prefab has DefaultBehaviour with race support)")]
    public bool enableRaceMode = false;

    [Header("Gizmo Settings")]
    [Tooltip("Color of the spawn point gizmo in the editor")]
    public Color gizmoColor = Color.green;

    [Tooltip("Size of the spawn point gizmo")]
    public float gizmoSize = 1f;

    private GameObject currentPlayer;
    
    // Track if the prefab supports race mode (has DefaultBehaviour)
    private bool prefabSupportsRaceMode = false;
    
    private void OnValidate()
    {
        // Check if the assigned prefab has DefaultBehaviour (supports race mode)
        if (playerPrefab != null)
        {
            DefaultBehaviour defaultBehaviour = playerPrefab.GetComponent<DefaultBehaviour>();
            prefabSupportsRaceMode = (defaultBehaviour != null);
            
            // If race mode is enabled but prefab doesn't support it, warn the user
            if (enableRaceMode && !prefabSupportsRaceMode)
            {
                // Debug.LogWarning($"SpawnPoint ({gameObject.name}): Race mode enabled but player prefab '{playerPrefab.name}' doesn't have a DefaultBehaviour component!", this);
            }
        }
        else
        {
            prefabSupportsRaceMode = false;
        }
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            // Check if a player already exists in the scene
            if (!forceSpawnIfExists && PlayerAlreadyExists())
            {
                
                // Try to find and register the existing player with this spawn point
                RespawnablePlayer existingPlayer = FindFirstObjectByType<RespawnablePlayer>();
                if (existingPlayer != null)
                {
                    currentPlayer = existingPlayer.gameObject;
                    
                    // Only update spawn point if it doesn't have one yet
                    if (existingPlayer.spawnPoint == null)
                    {
                        existingPlayer.spawnPoint = this;
                    }
                    
                    // Apply race mode to existing player if enabled
                    if (enableRaceMode)
                    {
                        ApplyRaceModeToPlayer(existingPlayer.gameObject);
                    }
                }
                
                return;
            }
            
            SpawnPlayer();
        }
    }
    
    /// <summary>
    /// Check if a player character already exists in the scene
    /// </summary>
    private bool PlayerAlreadyExists()
    {
        // Check for RespawnablePlayer component (most reliable)
        RespawnablePlayer existingPlayer = FindFirstObjectByType<RespawnablePlayer>();
        if (existingPlayer != null && existingPlayer.gameObject.activeInHierarchy)
        {
            return true;
        }
        
        // Fallback: Check for ActiveRagdoll component
        ActiveRagdoll.ActiveRagdoll existingRagdoll = FindFirstObjectByType<ActiveRagdoll.ActiveRagdoll>();
        if (existingRagdoll != null && existingRagdoll.gameObject.activeInHierarchy)
        {
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Spawns a new player at this spawn point
    /// </summary>
    /// <param name="playerID">Player ID for multiplayer (0 = single player, 1+ = multiplayer)</param>
    public GameObject SpawnPlayer(int playerID = 0)
    {
        if (playerPrefab == null)
        {
// Debug.LogError("SpawnPoint: No player prefab assigned to " + gameObject.name);
            return null;
        }

        Vector3 spawnPos = GetSpawnPosition();
        Quaternion spawnRot = GetSpawnRotation();

        currentPlayer = Instantiate(playerPrefab, spawnPos, spawnRot);
        
        // Set player ID on the RespawnablePlayer component
        RespawnablePlayer respawnable = currentPlayer.GetComponent<RespawnablePlayer>();
        if (respawnable != null)
        {
            respawnable.playerID = playerID;
            respawnable.spawnPoint = this;
        }
        
        // Activate the selected costume (using player ID for multiplayer)
        ActivateSelectedCostume(currentPlayer, playerID);
        
        // CRITICAL: Refresh costume references after switching costumes
        // (Awake() ran during Instantiate with the default costume active)
        var activeRagdoll = currentPlayer.GetComponent<ActiveRagdoll.ActiveRagdoll>();
        if (activeRagdoll != null)
        {
            activeRagdoll.RefreshCostumeReferences();
            
            if (_debugMode)
                Debug.Log("<color=green>[SpawnPoint]</color> ✅ Refreshed costume references after spawn");
        }
        
        // CRITICAL: Refresh body part audio triggers for the active costume
        var audioController = currentPlayer.GetComponent<CharacterAudioController>();
        if (audioController != null)
        {
            audioController.RefreshBodyPartTriggers();
            
            if (_debugMode)
                Debug.Log("<color=green>[SpawnPoint]</color> 🔊 Refreshed body part audio triggers");
        }
        
        // CRITICAL: Refresh time rewind controller for the active costume
        var timeRewind = currentPlayer.GetComponent<TimeRewindController>();
        if (timeRewind != null)
        {
            timeRewind.RefreshCostumeReferences();
            
            if (_debugMode)
                Debug.Log("<color=green>[SpawnPoint]</color> ⏪ Refreshed time rewind references");
        }
        
        // CRITICAL: Setup collision listeners AFTER costume is fully initialized
        // This ensures physics points tracking works from the very first spawn
        var pointsCollector = currentPlayer.GetComponent<RagdollPointsCollector>();
        if (pointsCollector != null)
        {
            pointsCollector.SetupCollisionListeners();
            
            if (_debugMode)
                Debug.Log("<color=green>[SpawnPoint]</color> 💥 Setup collision listeners for physics points");
        }
        
        // Apply race mode setting to spawned player
        if (enableRaceMode)
        {
            ApplyRaceModeToPlayer(currentPlayer);
        }

        return currentPlayer;
    }
    
    /// <summary>
    /// Apply race mode setting to a player GameObject
    /// </summary>
    private void ApplyRaceModeToPlayer(GameObject player)
    {
        if (player == null) return;
        
        DefaultBehaviour defaultBehaviour = player.GetComponent<DefaultBehaviour>();
        if (defaultBehaviour != null)
        {
            // Use reflection to set the private _raceMode field
            System.Type type = defaultBehaviour.GetType();
            System.Reflection.FieldInfo raceModeField = type.GetField("_raceMode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (raceModeField != null)
            {
                raceModeField.SetValue(defaultBehaviour, true);
            }
            else
            {
                // Debug.LogWarning($"SpawnPoint ({gameObject.name}): Could not find _raceMode field in DefaultBehaviour!");
            }
        }
        else
        {
            // Debug.LogWarning($"SpawnPoint ({gameObject.name}): Race mode enabled but player doesn't have DefaultBehaviour component!");
        }
    }

    /// <summary>
    /// Activates the selected costume hierarchy and deactivates others
    /// </summary>
    /// <param name="character">The character GameObject to configure</param>
    /// <param name="playerID">Player ID for multiplayer costume tracking (0 = single player)</param>
    public void ActivateSelectedCostume(GameObject character, int playerID)
    {
        if (character == null) return;
        
        // Get all costume hierarchies (direct children that have Animated/Physical structure)
        System.Collections.Generic.List<GameObject> costumes = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in character.transform)
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
        
        // Deactivate all costumes
        foreach (GameObject costume in costumes)
        {
            costume.SetActive(false);
        }
        
        // Check if there's a stored costume index from costume cycling (player-specific)
        int costumeIndexToUse = selectedCostumeIndex;
        int storedIndex = CostumeCycler.GetStoredCostumeIndex(playerID);
        
        if (storedIndex >= 0) // If there's a valid stored index, use it (and keep it for future respawns)
        {
            costumeIndexToUse = storedIndex;
            // DON'T clear it - keep the costume persistent across respawns!
            if (_debugMode)
                Debug.Log($"<color=magenta>[SpawnPoint]</color> 🔄 Player {playerID} using persistent costume index: {costumeIndexToUse}");
        }
        
        // Activate the selected costume
        if (costumeIndexToUse >= 0 && costumeIndexToUse < costumes.Count)
        {
            costumes[costumeIndexToUse].SetActive(true);
            // Debug.Log($"<color=cyan>[SpawnPoint]</color> Spawned character with costume: {costumes[costumeIndexToUse].name}");
        }
        else if (costumes.Count > 0)
        {
            // Fallback to first costume if index is invalid
            costumes[0].SetActive(true);
            Debug.Log($"<color=yellow>[SpawnPoint]</color> Invalid costume index, using first costume: {costumes[0].name}");
        }
        
        // The character will auto-initialize through Awake/Start
        // ActiveRagdoll.Awake() will automatically find the active costume
    }
    
    /// <summary>
    /// Start a costume cycle respawn - EXACTLY like normal respawn, just with costume swap
    /// </summary>
    public void StartCostumeCycleRespawn(GameObject oldCharacter)
    {
        // Get player ID from old character before destroying
        int playerID = 0;
        if (oldCharacter != null)
        {
            RespawnablePlayer respawnable = oldCharacter.GetComponent<RespawnablePlayer>();
            if (respawnable != null)
            {
                playerID = respawnable.playerID;
            }
        }
        
        if (_debugMode)
            Debug.Log($"<color=cyan>[SpawnPoint]</color> 🔄 Costume cycle respawn for Player {playerID} (instant, like normal respawn)");
        
        // EXACTLY like RespawnablePlayer.Respawn():
        // 1. Deactivate old player
        if (oldCharacter != null)
        {
            oldCharacter.SetActive(false);
        }
        
        // 2. Spawn new player (with new costume) - pass player ID
        GameObject newPlayer = SpawnPlayer(playerID);
        
        // 3. CRITICAL: Notify multiplayer manager IMMEDIATELY so input gets set up
        if (newPlayer != null && MultiplayerManagerSimple.Instance != null)
        {
            MultiplayerManagerSimple.Instance.OnPlayerRespawned(oldCharacter, newPlayer);
        }
        
        // 4. Destroy old player
        if (oldCharacter != null)
        {
            Destroy(oldCharacter);
        }
        
        // 5. Wait just 1 frame, then unlock (ensures spawn completes)
        StartCoroutine(UnlockAfterFrame());
    }
    
    /// <summary>
    /// Unlock costume cycling after spawn completes (1 frame delay)
    /// </summary>
    private System.Collections.IEnumerator UnlockAfterFrame()
    {
        yield return null; // Wait 1 frame for spawn to complete
        CostumeCycler.UnlockCyclingFlag();
        
        if (_debugMode)
            Debug.Log("<color=green>[SpawnPoint]</color> ✅ Costume cycling unlocked!");
    }
    
    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;
    
    /// <summary>
    /// Get the position where the player should spawn
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        return transform.position + transform.TransformDirection(spawnOffset);
    }

    /// <summary>
    /// Get the rotation the player should have when spawning
    /// </summary>
    public Quaternion GetSpawnRotation()
    {
        return useRotation ? transform.rotation : Quaternion.identity;
    }

    // Draw a gizmo in the editor to visualize the spawn point
    private void OnDrawGizmos()
    {
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
        
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(spawnPos, spawnPos + transform.forward * gizmoSize);
    }
}


