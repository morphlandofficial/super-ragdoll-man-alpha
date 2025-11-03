using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class RedLightGreenLightManager : MonoBehaviour
{
    [Header("Game State")]
    [SerializeField] private bool isGameActive = false;
    [SerializeField] private GameState currentState = GameState.Inactive;
    
    [Header("Phase Durations")]
    [Tooltip("Initial grace period - guaranteed safe movement time at game start")]
    [FormerlySerializedAs("initialGreenLightDuration")]
    [SerializeField] private float initialGracePeriodDuration = 10f;
    
    [Tooltip("Time players have to stop moving after red light triggers")]
    [SerializeField] private float redLightGracePeriod = 1f;
    
    [Tooltip("Total duration of red light phase")]
    [SerializeField] private float redLightDuration = 5f;
    
    [Header("Random Green Light Timing (Bell Curve)")]
    [Tooltip("Minimum random green light duration")]
    [SerializeField] private float minGreenLightDuration = 0.5f;
    
    [Tooltip("Maximum random green light duration")]
    [SerializeField] private float maxGreenLightDuration = 15f;
    
    [Tooltip("Target mean for bell curve (center point)")]
    [SerializeField] private float greenLightMean = 10f;
    
    [Tooltip("Standard deviation for bell curve (spread)")]
    [SerializeField] private float greenLightStdDev = 2.5f;
    
    [Header("Movement Detection")]
    [Tooltip("Maximum number of players to eliminate per red light phase")]
    [SerializeField] private int maxPlayersKilledPerRound = 5;
    
    [Tooltip("Delay between each elimination (seconds) - creates dramatic staggered effect")]
    [SerializeField] private float eliminationDelay = 0.3f;
    
    [Header("Movement Scoring Weights")]
    [Tooltip("Weight for distance traveled during grace period (biggest factor)")]
    [SerializeField] private float distanceWeight = 10f;
    
    [Tooltip("Weight for average speed during grace period")]
    [SerializeField] private float averageSpeedWeight = 1f;
    
    [Tooltip("Include angular velocity (rotation) in movement score calculation")]
    [SerializeField] private bool includeAngularVelocity = false;
    
    [Header("Player Detection")]
    [Tooltip("Tag to identify the player character")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("Manually assign player if needed")]
    [SerializeField] private GameObject manualPlayerReference;
    
    [Header("Audio")]
    [Tooltip("Audio clip for initial grace period (plays as a track until cut off)")]
    [SerializeField] private AudioClip initialGracePeriodTrack;
    
    [Tooltip("Track that plays during green light phases (loops until red light)")]
    [SerializeField] private AudioClip greenLightTrack;
    
    [Tooltip("Track that plays during red light phases (loops until green light)")]
    [SerializeField] private AudioClip redLightTrack;
    
    [Tooltip("Sound effect when players are eliminated")]
    [SerializeField] private AudioClip playerEliminationSound;
    
    [Tooltip("Victory track when only one player remains (plays until level finish)")]
    [SerializeField] private AudioClip victoryTrack;
    
    [Header("Audio Settings")]
    [SerializeField] private float trackVolume = 0.7f;
    [SerializeField] private float oneShotVolume = 1.0f;
    [SerializeField] private float eliminationVolume = 0.8f;
    
    [Header("Green Light Mode - Object Control")]
    [Tooltip("Objects to activate when green light starts")]
    [SerializeField] private GameObject[] greenLightActivateObjects = new GameObject[0];
    
    [Tooltip("Objects to deactivate when green light starts")]
    [SerializeField] private GameObject[] greenLightDeactivateObjects = new GameObject[0];
    
    [Header("Red Light Mode - Object Control")]
    [Tooltip("Objects to activate when red light starts")]
    [SerializeField] private GameObject[] redLightActivateObjects = new GameObject[0];
    
    [Tooltip("Objects to deactivate when red light starts")]
    [SerializeField] private GameObject[] redLightDeactivateObjects = new GameObject[0];
    
    [Header("Debug")]
    // [SerializeField] private bool showDebugMessages = false; // Unused after debug log cleanup
    [SerializeField] private bool showDebugGizmos = true;
    
    // Internal state
    private enum GameState
    {
        Inactive,
        InitialGracePeriod,
        GreenLight,
        RedLightGracePeriod,
        RedLightChecking,
        Victory // Only one contestant remains - they won!
    }
    
    // Audio sources
    private AudioSource trackAudioSource;
    private AudioSource oneShotAudioSource;
    
    private float stateTimer = 0f;
    private List<PlayerContestant> contestants = new List<PlayerContestant>();
    // private bool hasCompletedInitialPhases = false; // Unused - was for state tracking
    private List<PlayerContestant> pendingEliminations = new List<PlayerContestant>();
    private Coroutine eliminationCoroutine = null;
    private DefaultBehaviour lastPlayerBehaviour = null; // Track last player we disabled datamosh on
    private bool playerEliminatedThisRedLight = false; // Track if player was eliminated during current red light phase (immunity after respawn)
    
    // Helper class to track contestants
    private class PlayerContestant
    {
        public GameObject gameObject; // The main GameObject (root for player, root for AI)
        public Rigidbody rigidbody; // The rigidbody we track for movement (may be child, e.g., Torso)
        public bool isPlayer;
        public bool isAlive;
        public Vector3 lastPosition;
        public float distanceTraveled; // Total distance moved during grace period
        public float totalVelocity; // Sum of velocities for average calculation
        public int velocitySamples; // Number of velocity samples taken
        public float movementScore; // Final calculated score
        
        public PlayerContestant(GameObject go, Rigidbody rb, bool isPlayerCharacter)
        {
            gameObject = go; // Store the ROOT GameObject (has RespawnablePlayer for player, RespawnableAIRagdoll for AI)
            rigidbody = rb; // Store the rigidbody we track (may be Torso child)
            isPlayer = isPlayerCharacter;
            isAlive = true;
            lastPosition = rb.transform.position; // Use rigidbody position, not root position
            distanceTraveled = 0f;
            totalVelocity = 0f;
            velocitySamples = 0;
            movementScore = 0f;
        }
        
        public void ResetMovementTracking()
        {
            distanceTraveled = 0f;
            totalVelocity = 0f;
            velocitySamples = 0;
            movementScore = 0f;
        }
        
        public float GetAverageSpeed()
        {
            return velocitySamples > 0 ? totalVelocity / velocitySamples : 0f;
        }
    }
    
    private void Start()
    {
        // Setup audio sources
        SetupAudioSources();
        
    }
    
    private void OnEnable()
    {
        // If the game is already active, don't restart it
        if (isGameActive) return;
        
        // If audio sources aren't set up yet (OnEnable called before Start), set them up now
        if (trackAudioSource == null || oneShotAudioSource == null)
        {
            SetupAudioSources();
        }
        
        // Automatically start the game when this GameObject is activated
        // This allows the Button Activator to trigger the game by activating this GameObject
        
        // Small delay to ensure all scene objects are ready
        StartCoroutine(StartGameAfterDelay(0.5f));
    }
    
    private System.Collections.IEnumerator StartGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartGame();
    }
    
    private void SetupAudioSources()
    {
        // Check if audio sources already exist (prevent duplicates on re-enable)
        if (trackAudioSource != null && oneShotAudioSource != null)
        {
            return; // Already set up
        }
        
        // Create track audio source only if needed (for looping music/ambient tracks)
        if (trackAudioSource == null)
        {
            trackAudioSource = gameObject.AddComponent<AudioSource>();
            trackAudioSource.playOnAwake = false;
            trackAudioSource.loop = false; // We'll manually handle looping
            trackAudioSource.spatialBlend = 0f; // 2D audio
            trackAudioSource.volume = trackVolume;
        }
        
        // Create one-shot audio source only if needed (for sound effects)
        if (oneShotAudioSource == null)
        {
            oneShotAudioSource = gameObject.AddComponent<AudioSource>();
            oneShotAudioSource.playOnAwake = false;
            oneShotAudioSource.loop = false;
            oneShotAudioSource.spatialBlend = 0f; // 2D audio
            oneShotAudioSource.volume = oneShotVolume;
        }
    }
    
    private void Update()
    {
        if (!isGameActive) return;
        
        // Continuously ensure datamosh is disabled on any active player (handles respawns)
        EnsureDatamoshDisabled();
        
        stateTimer -= Time.deltaTime;
        
        // State machine
        switch (currentState)
        {
            case GameState.InitialGracePeriod:
                UpdateInitialGracePeriod();
                break;
                
            case GameState.GreenLight:
                UpdateGreenLight();
                break;
                
            case GameState.RedLightGracePeriod:
                UpdateRedLightGracePeriod();
                break;
                
            case GameState.RedLightChecking:
                UpdateRedLightChecking();
                break;
                
            case GameState.Victory:
                UpdateVictory();
                break;
        }
    }
    
    #region Public Methods
    
    /// <summary>
    /// Call this method to start the Red Light Green Light game
    /// This should be called by your Button Activator
    /// </summary>
    public void StartGame()
    {
        if (isGameActive)
        {
            return;
        }
        
        isGameActive = true;
        // hasCompletedInitialPhases = false;
        
        // Reset player elimination flag for new game
        playerEliminatedThisRedLight = false;
        
        // Find all contestants
        FindAllContestants();
        
        // Disable datamosh effect on player for RLGL mode
        DisablePlayerDatamoshEffect();
        
        // Start with initial grace period
        TransitionToState(GameState.InitialGracePeriod, initialGracePeriodDuration);
    }
    
    /// <summary>
    /// Stop the game
    /// </summary>
    public void StopGame()
    {
        isGameActive = false;
        currentState = GameState.Inactive;
    }
    
    /// <summary>
    /// Reset the game to initial state
    /// </summary>
    public void ResetGame()
    {
        StopGame();
        contestants.Clear();
    }
    
    #endregion
    
    #region State Updates
    
    private void UpdateInitialGracePeriod()
    {
        if (stateTimer <= 0f)
        {
            // Mark that we've completed initial phase
            // hasCompletedInitialPhases = true;
            
            // Go DIRECTLY to normal green light mode with random duration
            // The green light track will start playing automatically
            float nextGreenDuration = GetRandomGreenLightDuration();
            TransitionToState(GameState.GreenLight, nextGreenDuration);
        }
    }
    
    private void UpdateGreenLight()
    {
        if (stateTimer <= 0f)
        {
            // GREEN LIGHT IS OVER - SWITCH TO RED LIGHT
            // Reset all movement tracking for the new red light phase
            foreach (var contestant in contestants)
            {
                contestant.ResetMovementTracking();
            }
            
            // Reset player elimination flag for new red light phase
            playerEliminatedThisRedLight = false;
            
            TransitionToState(GameState.RedLightGracePeriod, redLightGracePeriod);
        }
    }
    
    private void UpdateRedLightGracePeriod()
    {
        // ACCUMULATE MOVEMENT SCORES for AI ragdolls during the grace period
        // Player has this 1 second to GET INTO ragdoll mode (no continuous check yet)
        AccumulateMovementScores();
        
        if (stateTimer <= 0f)
        {
            
            // Grace period is over - Calculate final scores and determine eliminations
            // THIS is when we check if player is in ragdoll (single check)
            CalculateFinalScores();
            DetermineEliminations();
            
            // Calculate how long eliminations will take
            float eliminationTime = pendingEliminations.Count > 0 ? 
                (pendingEliminations.Count - 1) * eliminationDelay + 0.5f : 0f; // Add 0.5s buffer
            
            // Stay in red light state for remaining duration OR long enough for eliminations
            float remainingRedLightTime = redLightDuration - redLightGracePeriod;
            float requiredTime = Mathf.Max(remainingRedLightTime, eliminationTime);
            
            TransitionToState(GameState.RedLightChecking, requiredTime);
            
            // Start staggered elimination process if there are any
            if (pendingEliminations.Count > 0)
            {
                eliminationCoroutine = StartCoroutine(ExecuteStaggeredEliminations());
            }
            // If no eliminations, eliminationCoroutine stays null
            // UpdateRedLightChecking() will handle the transition when timer expires
        }
    }
    
    private void AccumulateMovementScores()
    {
        // Accumulate movement data for AI contestants during grace period
        // Iterate backwards so we can safely remove dead contestants
        for (int i = contestants.Count - 1; i >= 0; i--)
        {
            var contestant = contestants[i];
            
            // Remove dead or destroyed contestants from list (performance optimization)
            if (!contestant.isAlive || contestant.gameObject == null || contestant.rigidbody == null)
            {
                contestants.RemoveAt(i);
                continue;
            }
            
            if (contestant.isPlayer) continue; // Player uses input detection, not movement
            
            // Skip if rigidbody is kinematic (dead ragdolls are made kinematic)
            if (contestant.rigidbody.isKinematic) continue;
            
            // Get current velocity (torso movement)
            float currentVelocity = contestant.rigidbody.linearVelocity.magnitude;
            
            // Accumulate distance traveled (velocity * time = distance)
            float distanceThisFrame = currentVelocity * Time.deltaTime;
            contestant.distanceTraveled += distanceThisFrame;
            
            // Accumulate velocity samples for average calculation
            contestant.totalVelocity += currentVelocity;
            contestant.velocitySamples++;
            
            // Add angular velocity to distance if enabled (body rotation/tumbling)
            if (includeAngularVelocity)
            {
                float angularVel = contestant.rigidbody.angularVelocity.magnitude;
                float angularDistance = angularVel * Time.deltaTime * 0.3f; // Smaller contribution
                contestant.distanceTraveled += angularDistance;
            }
        }
    }
    
    /// <summary>
    /// Calculate final scores:
    /// - Player: Binary (input = elimination, no input = safe)
    /// - AI: Normalized 1-100 scale (relative to each other)
    /// </summary>
    private void CalculateFinalScores()
    {
        // STEP 1: Calculate raw movement for AI ragdolls
        List<PlayerContestant> aiRagdolls = new List<PlayerContestant>();
        
        foreach (var contestant in contestants)
        {
            if (!contestant.isAlive) continue;
            
            // Check if GameObject was destroyed (fell off map, etc.)
            if (contestant.gameObject == null || contestant.rigidbody == null)
            {
                contestant.isAlive = false; // Mark as dead
                continue;
            }
            
            if (!contestant.isPlayer)
            {
                // AI: Calculate raw movement score
                float averageSpeed = contestant.GetAverageSpeed();
                float rawScore = (contestant.distanceTraveled * distanceWeight) + 
                                (averageSpeed * averageSpeedWeight);
                contestant.movementScore = rawScore; // Store raw score temporarily
                aiRagdolls.Add(contestant);
            }
        }
        
        // STEP 2: Normalize AI scores to 1-100 scale (relative ranking)
        if (aiRagdolls.Count > 0)
        {
            // Find min and max raw scores
            float minRaw = float.MaxValue;
            float maxRaw = float.MinValue;
            
            foreach (var ai in aiRagdolls)
            {
                if (ai.movementScore < minRaw) minRaw = ai.movementScore;
                if (ai.movementScore > maxRaw) maxRaw = ai.movementScore;
            }
            
            // Normalize each AI score to 1-100
            foreach (var ai in aiRagdolls)
            {
                if (aiRagdolls.Count == 1)
                {
                    // Only one AI - give them score 50 (middle)
                    ai.movementScore = 50f;
                }
                else if (Mathf.Approximately(minRaw, maxRaw))
                {
                    // All AI have same movement - give them all score 50
                    ai.movementScore = 50f;
                }
                else
                {
                    // Normalize: (raw - min) / (max - min) * 99 + 1 = 1 to 100
                    float normalized = ((ai.movementScore - minRaw) / (maxRaw - minRaw)) * 99f + 1f;
                    ai.movementScore = normalized;
                }
            }
        }
        
        // STEP 3: Handle player scoring (binary: input vs no input)
        foreach (var contestant in contestants)
        {
            // Check if GameObject was destroyed
            if (contestant.gameObject == null)
            {
                contestant.isAlive = false;
                continue;
            }
            
            if (!contestant.isAlive)
            {
                continue;
            }
            
            if (contestant.isPlayer)
            {
                // PLAYER: Check if they're CURRENTLY in ragdoll mode (holding TAB/ButtonNorth)
                bool isInRagdollMode = false;
                
                // Get the DefaultBehaviour component to check ragdoll state
                DefaultBehaviour playerBehaviour = contestant.gameObject.GetComponent<DefaultBehaviour>();
                if (playerBehaviour != null)
                {
                    isInRagdollMode = playerBehaviour.IsInRagdollMode;
                }
                
                if (isInRagdollMode)
                {
                    contestant.movementScore = 0f; // Safe - in ragdoll mode
                }
                else
                {
                    contestant.movementScore = float.MaxValue; // Eliminated - not in ragdoll mode
                }
            }
        }
    }
    
    private void UpdateRedLightChecking()
    {
        // CONTINUOUS RAGDOLL CHECK: Player MUST stay in ragdoll for entire red light duration
        // Even if they passed the initial check, releasing ragdoll = instant death
        CheckPlayerStillInRagdoll();
        
        // Check if eliminations are complete (coroutine finished)
        if (eliminationCoroutine == null && stateTimer <= 0f)
        {
            // Eliminations are done AND timer expired - check for victory or continue
            CheckForVictoryOrContinue();
        }
    }
    
    /// <summary>
    /// Continuously check if player is still in ragdoll mode during red light
    /// If they release ragdoll at any point - instant elimination!
    /// </summary>
    private void CheckPlayerStillInRagdoll()
    {
        // IMMUNITY CHECK: If player was already eliminated this red light phase, don't check again
        if (playerEliminatedThisRedLight)
        {
            return; // Player has immunity for remainder of this red light phase
        }
        
        // Find the player contestant (check ALL players, even if marked as not alive yet)
        PlayerContestant player = contestants.FirstOrDefault(c => c.isPlayer);
        
        if (player != null && player.gameObject != null)
        {
            // Skip check if player is already dead/being eliminated
            if (!player.isAlive)
            {
                return; // Player already eliminated, no need to check
            }
            
            // Get the DefaultBehaviour component to check ragdoll state
            DefaultBehaviour playerBehaviour = player.gameObject.GetComponent<DefaultBehaviour>();
            if (playerBehaviour != null)
            {
                bool isInRagdoll = playerBehaviour.IsInRagdollMode;
                
                // If player is NOT in ragdoll mode during red light - INSTANT ELIMINATION!
                if (!isInRagdoll)
                {
                    // Mark player as eliminated immediately
                    player.isAlive = false;
                    
                    // Set immunity flag so respawned player is safe for rest of this red light
                    playerEliminatedThisRedLight = true;
                    
                    // Eliminate the player instantly (regardless of queue status)
                    EliminatePlayer(player);
                    
                    // Debug.Log("[RedLightGreenLight] Player eliminated - immunity granted for rest of this red light phase");
                }
            }
        }
    }
    
    /// <summary>
    /// Check if game should transition to victory or continue to next green light
    /// </summary>
    private void CheckForVictoryOrContinue()
    {
        // Check if only one contestant remains
        int aliveCount = CountAliveContestants();
        
        if (aliveCount <= 1)
        {
            // VICTORY! Only one player left
            TransitionToState(GameState.Victory, float.MaxValue); // Infinite duration
        }
        else
        {
            // RED LIGHT IS OVER - BACK TO GREEN LIGHT
            // Reset player elimination flag so they can be caught again in next red light
            playerEliminatedThisRedLight = false;
            
            float nextGreenDuration = GetRandomGreenLightDuration();
            TransitionToState(GameState.GreenLight, nextGreenDuration);
        }
    }
    
    private void UpdateVictory()
    {
        // Victory state - do nothing, just let the winner reach the finish
        // The victory music plays until the level ends
    }
    
    #endregion
    
    #region State Management
    
    private void TransitionToState(GameState newState, float duration)
    {
        currentState = newState;
        stateTimer = duration;
        
        // Handle object activation/deactivation based on new state
        HandleObjectsForState(newState);
        
        // Play appropriate audio for new state
        PlayAudioForState(newState);
    }
    
    /// <summary>
    /// Activate/Deactivate objects based on current game state
    /// </summary>
    private void HandleObjectsForState(GameState state)
    {
        switch (state)
        {
            case GameState.GreenLight:
                // Activate green light objects
                foreach (GameObject obj in greenLightActivateObjects)
                {
                    if (obj != null) obj.SetActive(true);
                }
                // Deactivate green light objects
                foreach (GameObject obj in greenLightDeactivateObjects)
                {
                    if (obj != null) obj.SetActive(false);
                }
                break;
                
            case GameState.RedLightGracePeriod:
            case GameState.RedLightChecking:
                // Activate red light objects
                foreach (GameObject obj in redLightActivateObjects)
                {
                    if (obj != null) obj.SetActive(true);
                }
                // Deactivate red light objects
                foreach (GameObject obj in redLightDeactivateObjects)
                {
                    if (obj != null) obj.SetActive(false);
                }
                break;
        }
    }
    
    #endregion
    
    #region Player Detection
    
    private void FindAllContestants()
    {
        contestants.Clear();
        
        // SIMPLE: Just find the GameObject with RespawnablePlayer component
        RespawnablePlayer respawnScript = FindFirstObjectByType<RespawnablePlayer>();
        
        if (respawnScript != null)
        {
            GameObject player = respawnScript.gameObject; // The root GameObject with RespawnablePlayer
            Rigidbody rb = FindRigidbodyInHierarchy(player); // Find the Torso rigidbody in children
            
            if (rb != null)
            {
                contestants.Add(new PlayerContestant(player, rb, true));
            }
            else
            {
                // Debug.LogError($"[RedLightGreenLight] ❌ Player '{player.name}' has no Rigidbody in hierarchy!");
            }
        }
        else
        {
            // Debug.LogError($"[RedLightGreenLight] ❌ NO PLAYER FOUND! Tag='{playerTag}', Manual Reference={(manualPlayerReference != null ? manualPlayerReference.name : "null")}");
        }
        
        // Find AI Ragdolls set to "Red Light Green Light" mode
        ActiveRagdoll.RagdollAIController[] allAIControllers = FindObjectsByType<ActiveRagdoll.RagdollAIController>(FindObjectsSortMode.None);
        foreach (var aiController in allAIControllers)
        {
            if (aiController.currentMode == ActiveRagdoll.RagdollAIController.AIMode.RedLightGreenLight)
            {
                // Find the rigidbody on this AI ragdoll
                Rigidbody rb = FindRigidbodyInHierarchy(aiController.gameObject);
                if (rb != null)
                {
                    contestants.Add(new PlayerContestant(aiController.gameObject, rb, false));
                }
            }
        }
    }
    
    /// <summary>
    /// Find the MAIN rigidbody for a ragdoll character
    /// For Active Ragdolls, this should be the torso/hips which represents overall movement
    /// </summary>
    private Rigidbody FindRigidbodyInHierarchy(GameObject go)
    {
        // First, try to find the ActiveRagdoll component and get its physical torso
        ActiveRagdoll.ActiveRagdoll activeRagdoll = go.GetComponent<ActiveRagdoll.ActiveRagdoll>();
        if (activeRagdoll != null && activeRagdoll.PhysicalTorso != null)
        {
            Rigidbody torsoRb = activeRagdoll.PhysicalTorso.GetComponent<Rigidbody>();
            if (torsoRb != null)
            {
                return torsoRb;
            }
        }
        
        // Fallback: Search for rigidbody with "Hips" or "Torso" in name
        Rigidbody[] allRigidbodies = go.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in allRigidbodies)
        {
            string rbName = rb.gameObject.name.ToLower();
            if (rbName.Contains("hips") || rbName.Contains("torso") || rbName.Contains("spine"))
            {
                return rb;
            }
        }
        
        // Last resort: Check self first, then children, then parent
        Rigidbody selfRb = go.GetComponent<Rigidbody>();
        if (selfRb != null) return selfRb;
        
        Rigidbody childRb = go.GetComponentInChildren<Rigidbody>();
        if (childRb != null)
        {
            return childRb;
        }
        
        if (go.transform.parent != null)
        {
            Rigidbody parentRb = go.GetComponentInParent<Rigidbody>();
            if (parentRb != null) return parentRb;
        }
        
        return null;
    }
    
    /// <summary>
    /// Disable the datamosh visual effect on the player for RLGL mode
    /// This allows ragdoll mode to work without the visual glitch
    /// </summary>
    private void DisablePlayerDatamoshEffect()
    {
        // Find the player
        RespawnablePlayer respawnScript = FindFirstObjectByType<RespawnablePlayer>();
        if (respawnScript != null)
        {
            // Get the DefaultBehaviour component
            DefaultBehaviour playerBehaviour = respawnScript.GetComponent<DefaultBehaviour>();
            if (playerBehaviour != null)
            {
                playerBehaviour.DisableDatamoshEffect();
                lastPlayerBehaviour = playerBehaviour; // Track this player instance
            }
            else
            {
                // Debug.LogWarning("[RedLightGreenLight] ⚠ Player found but no DefaultBehaviour component!");
            }
        }
    }
    
    /// <summary>
    /// Continuously ensure datamosh is disabled on any active player (handles respawns)
    /// Only calls DisableDatamoshEffect() when a new player instance is detected
    /// Also re-adds respawned player to contestants list!
    /// </summary>
    private void EnsureDatamoshDisabled()
    {
        // Find the current active player
        RespawnablePlayer respawnScript = FindFirstObjectByType<RespawnablePlayer>();
        if (respawnScript != null)
        {
            DefaultBehaviour playerBehaviour = respawnScript.GetComponent<DefaultBehaviour>();
            
            // If this is a NEW player instance (different from last one), disable datamosh
            if (playerBehaviour != null && playerBehaviour != lastPlayerBehaviour)
            {
                playerBehaviour.DisableDatamoshEffect();
                lastPlayerBehaviour = playerBehaviour;
                // Debug.Log("[RedLightGreenLight] New player detected - datamosh disabled for respawned player");
                
                // RE-ADD THE NEW PLAYER TO CONTESTANTS LIST
                // Remove any old/destroyed player entries
                contestants.RemoveAll(c => c.isPlayer && (c.gameObject == null || c.gameObject != respawnScript.gameObject));
                
                // Add the new player instance with their Torso rigidbody
                GameObject newPlayer = respawnScript.gameObject;
                Rigidbody newRb = FindRigidbodyInHierarchy(newPlayer);
                
                if (newRb != null)
                {
                    contestants.Add(new PlayerContestant(newPlayer, newRb, true));
                    // Debug.Log($"[RedLightGreenLight] ✅ Re-added respawned player to contestants: {newPlayer.name}");
                }
                else
                {
                    // Debug.LogError($"[RedLightGreenLight] ❌ Respawned player '{newPlayer.name}' has no Torso rigidbody!");
                }
            }
        }
    }
    
    #endregion
    
    #region Movement Detection & Elimination
    
    /// <summary>
    /// Determine which players should be eliminated based on movement scores
    /// PLAYER and AI have SEPARATE elimination logic
    /// </summary>
    private void DetermineEliminations()
    {
        if (contestants.Count == 0)
        {
            return;
        }
        
        List<PlayerContestant> playersToEliminate = new List<PlayerContestant>();
        int availableKillSlots = maxPlayersKilledPerRound;
        
        // === PLAYER ELIMINATION (SEPARATE LOGIC) ===
        // Player elimination is BINARY: Ragdoll mode or not
        PlayerContestant player = contestants.FirstOrDefault(c => c.isPlayer && c.isAlive);
        
        if (player != null && player.rigidbody != null)
        {
            // Player has movementScore = float.MaxValue if NOT in ragdoll mode
            // Player has movementScore = 0 if IN ragdoll mode
            if (player.movementScore > 0f) // NOT in ragdoll mode
            {
                // 90% chance player is eliminated, 10% chance they get lucky!
                float randomChance = Random.Range(0f, 1f);
                
                if (randomChance <= 0.9f) // 90% chance
                {
                    playersToEliminate.Add(player);
                    availableKillSlots--; // Player takes up 1 kill slot
                    playerEliminatedThisRedLight = true; // Grant immunity for rest of this red light
                }
            }
        }
        
        // === AI RAGDOLL ELIMINATION (SEPARATE RANKING LOGIC) ===
        // Only consider AI ragdolls who moved (score > 0)
        List<PlayerContestant> aiContestants = contestants
            .Where(c => !c.isPlayer && c.isAlive && c.rigidbody != null && c.movementScore > 0f)
            .ToList();
        
        if (aiContestants.Count == 0 || availableKillSlots <= 0)
        {
            // No AI moved or no kill slots left - store what we have and return
            pendingEliminations = playersToEliminate;
            return;
        }
        
        // Sort AI by accumulated movement score - HIGHEST SCORE (most movement) FIRST
        aiContestants = aiContestants
            .OrderByDescending(c => c.movementScore)
            .ToList();
        
        // STEP 1: Always eliminate the AI WORST OFFENDER (highest movement score)
        if (aiContestants.Count > 0 && availableKillSlots > 0)
        {
            PlayerContestant worstAI = aiContestants[0];
            playersToEliminate.Add(worstAI);
            availableKillSlots--;
        }
        
        // STEP 2: If we can kill more AI, randomly select from bottom 50% of AI scores
        if (availableKillSlots > 0 && aiContestants.Count > 1)
        {
            // Get the bottom 50% (lowest movement scores - AI who stopped better)
            int bottom50Index = Mathf.CeilToInt(aiContestants.Count / 2f);
            List<PlayerContestant> bottom50Percent = aiContestants.Skip(bottom50Index).ToList();
            
            if (bottom50Percent.Count > 0)
            {
                // Randomly select from bottom 50% up to remaining kill slots
                int randomKills = Mathf.Min(availableKillSlots, bottom50Percent.Count);
                
                // Shuffle the bottom 50% list
                for (int i = 0; i < bottom50Percent.Count; i++)
                {
                    int randomIndex = Random.Range(i, bottom50Percent.Count);
                    var temp = bottom50Percent[i];
                    bottom50Percent[i] = bottom50Percent[randomIndex];
                    bottom50Percent[randomIndex] = temp;
                }
                
                // Take the first N from shuffled list
                for (int i = 0; i < randomKills; i++)
                {
                    playersToEliminate.Add(bottom50Percent[i]);
                }
            }
        }
        
        // STEP 3: Store combined eliminations (player + AI) for staggered execution
        pendingEliminations = playersToEliminate;
    }
    
    /// <summary>
    /// Execute eliminations one by one with a delay between each
    /// </summary>
    private IEnumerator ExecuteStaggeredEliminations()
    {
        int eliminationCount = 0;
        
        foreach (var player in pendingEliminations)
        {
            eliminationCount++;
            
            EliminatePlayer(player);
            
            // Wait before next elimination (unless this is the last one)
            if (eliminationCount < pendingEliminations.Count)
            {
                yield return new WaitForSeconds(eliminationDelay);
            }
        }
        
        // Clear pending eliminations
        pendingEliminations.Clear();
        
        // Mark coroutine as complete LAST (UpdateRedLightChecking checks this)
        eliminationCoroutine = null;
    }
    
    private int CountAliveContestants()
    {
        // Clean up dead/destroyed contestants while counting (performance optimization)
        contestants.RemoveAll(c => !c.isAlive || c.gameObject == null);
        
        int count = 0;
        foreach (var contestant in contestants)
        {
            if (contestant.isAlive) count++;
        }
        return count;
    }
    
    private void EliminatePlayer(PlayerContestant contestant)
    {
        contestant.isAlive = false;
        
        // Play elimination sound effect
        PlayEliminationSound();
        
        // Handle elimination differently for player vs AI
        if (contestant.isPlayer)
        {
            // PLAYER: Call Respawn() exactly like RespawnTrigger does
            RespawnablePlayer respawnScript = contestant.gameObject.GetComponent<RespawnablePlayer>();
            if (respawnScript != null)
            {
                respawnScript.Respawn(isManual: false); // Automatic respawn, penalty applied
            }
            else
            {
                // Debug.LogError($"[RedLightGreenLight] ❌ FATAL: Player GameObject '{contestant.gameObject.name}' does NOT have RespawnablePlayer!");
                // Debug.LogError($"[RedLightGreenLight] ❌ Cannot respawn - check FindAllContestants() logic!");
            }
        }
        else
        {
            // AI RAGDOLL: Their RespawnableAIRagdoll.Respawn() handles RLGL mode (stays as corpse)
            contestant.gameObject.SendMessage("Respawn", SendMessageOptions.DontRequireReceiver);
        }
    }
    
    #endregion
    
    #region Random Duration (Bell Curve)
    
    /// <summary>
    /// Generate random green light duration using normal distribution (bell curve)
    /// Values cluster around 8-12 seconds with rare extremes
    /// </summary>
    private float GetRandomGreenLightDuration()
    {
        // Box-Muller transform for normal distribution
        float u1 = Random.Range(0f, 1f);
        float u2 = Random.Range(0f, 1f);
        
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        float randNormal = greenLightMean + greenLightStdDev * randStdNormal;
        
        // Clamp to min/max range
        float duration = Mathf.Clamp(randNormal, minGreenLightDuration, maxGreenLightDuration);
        
        return duration;
    }
    
    #endregion
    
    #region Audio Management
    
    /// <summary>
    /// Play appropriate audio based on game state
    /// </summary>
    private void PlayAudioForState(GameState state)
    {
        switch (state)
        {
            case GameState.InitialGracePeriod:
                PlayTrack(initialGracePeriodTrack);
                break;
                
            case GameState.GreenLight:
                PlayTrack(greenLightTrack);
                break;
                
            case GameState.RedLightGracePeriod:
                PlayTrack(redLightTrack);
                break;
                
            case GameState.RedLightChecking:
                // Red light track continues playing
                break;
                
            case GameState.Victory:
                PlayTrack(victoryTrack);
                break;
                
            case GameState.Inactive:
                StopAllAudio();
                break;
        }
    }
    
    /// <summary>
    /// Play a track on the track audio source (stops previous track)
    /// </summary>
    private void PlayTrack(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }
        
        if (trackAudioSource == null)
        {
            // Debug.LogError("[RedLightGreenLight] Track audio source is null!");
            return;
        }
        
        // Stop current track if playing
        if (trackAudioSource.isPlaying)
        {
            trackAudioSource.Stop();
        }
        
        // Play new track
        trackAudioSource.clip = clip;
        trackAudioSource.volume = trackVolume;
        trackAudioSource.loop = true; // Loop the track until state changes
        trackAudioSource.Play();
    }
    
    /// <summary>
    /// Play a one-shot sound effect
    /// </summary>
    private void PlayOneShotSound(AudioClip clip)
    {
        if (clip == null) return;
        
        if (oneShotAudioSource == null)
        {
            // Debug.LogError("[RedLightGreenLight] One-shot audio source is null!");
            return;
        }
        
        oneShotAudioSource.PlayOneShot(clip, oneShotVolume);
    }
    
    /// <summary>
    /// Play elimination sound effect
    /// </summary>
    private void PlayEliminationSound()
    {
        if (playerEliminationSound == null) return;
        
        if (oneShotAudioSource == null)
        {
            // Debug.LogError("[RedLightGreenLight] One-shot audio source is null!");
            return;
        }
        
        oneShotAudioSource.PlayOneShot(playerEliminationSound, eliminationVolume);
    }
    
    /// <summary>
    /// Stop all audio
    /// </summary>
    private void StopAllAudio()
    {
        if (trackAudioSource != null && trackAudioSource.isPlaying)
        {
            trackAudioSource.Stop();
        }
        
        if (oneShotAudioSource != null && oneShotAudioSource.isPlaying)
        {
            oneShotAudioSource.Stop();
        }
    }
    
    #endregion
    
    #region Debug Visualization
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !isGameActive) return;
        
        // Draw state indicator above manager
        Vector3 gizmoPos = transform.position + Vector3.up * 3f;
        
        Gizmos.color = currentState switch
        {
            GameState.InitialGracePeriod => Color.green,
            GameState.GreenLight => Color.green,
            GameState.RedLightGracePeriod => Color.red,
            GameState.RedLightChecking => Color.red,
            GameState.Victory => Color.magenta,
            _ => Color.gray
        };
        
        Gizmos.DrawWireSphere(gizmoPos, 0.5f);
        
        // Draw lines to all contestants
        foreach (var contestant in contestants)
        {
            if (contestant.isAlive && contestant.gameObject != null)
            {
                Gizmos.color = contestant.isPlayer ? Color.cyan : Color.yellow;
                Gizmos.DrawLine(transform.position, contestant.gameObject.transform.position);
            }
        }
    }
    
    #endregion
}

