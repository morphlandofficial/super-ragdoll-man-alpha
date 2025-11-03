using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Battle Royale Manager - Manages AI ragdoll spawning and player lives system.
/// 
/// TWO MODES:
/// 
/// 1. BATTLE ROYALE MODE:
///    - Kill all ragdolls to win
///    - Player has limited (or infinite) lives
///    - If lives run out → Level ends with ZERO points
/// 
/// 2. INFINITE SPAWN MODE:
///    - Ragdolls spawn forever (no limit)
///    - Player survives and racks up points
///    - Player has limited (or infinite) lives
///    - If lives run out → Level ends with ZERO points
/// 
/// WAVE SYSTEM (Optional):
///    - Spawners can be organized into sequential waves
///    - Wave 2 doesn't spawn until Wave 1 is eliminated
///    - Each wave can trigger object activation/deactivation on completion
/// 
/// HOW TO USE:
/// 1. Create an empty GameObject called "Battle Royale Manager"
/// 2. Add this component to it
/// 3. Drag and drop your AIRagdollSpawner GameObjects into the Spawners array
/// 4. Choose your game mode (Battle Royale or Infinite Spawn)
/// 5. Configure lives system (Infinite or Limited)
/// 6. Optionally enable Wave System for sequential spawning
/// 7. Play!
/// </summary>
public class BattleRoyaleManager : MonoBehaviour
{
    /// <summary>
    /// Data structure for a single wave of enemies
    /// </summary>
    [System.Serializable]
    public class WaveData
    {
        [Tooltip("Wave number (for display and organization)")]
        public int waveNumber = 1;
        
        [Tooltip("Spawners that are part of this wave")]
        public List<GameObject> spawners = new List<GameObject>();
        
        [Tooltip("Objects to activate when this wave completes")]
        public List<GameObject> objectsToActivate = new List<GameObject>();
        
        [Tooltip("Objects to deactivate when this wave completes")]
        public List<GameObject> objectsToDeactivate = new List<GameObject>();
    }
    // ==================== STATIC FLAGS ====================
    // Public static flag that LevelFinishTrigger can check to force zero points
    public static bool ForceZeroPointsOnFinish { get; set; } = false;
    
    // Public static flag that prevents respawning when on last life
    public static bool PreventRespawning { get; set; } = false;
    
    public enum GameMode
    {
        BattleRoyale,   // Kill all ragdolls to win (spawners have limits)
        InfiniteSpawn   // Ragdolls spawn forever, survive as long as you can
    }
    
    public enum LivesMode
    {
        Infinite,       // Player can die unlimited times
        Limited         // Player has X lives, game over when lives run out
    }
    
    [Header("═══ GAME MODE ═══")]
    [Space(5)]
    
    [Tooltip("Battle Royale = Kill all ragdolls to win | Infinite Spawn = Survive endless waves")]
    [SerializeField] private GameMode gameMode = GameMode.BattleRoyale;
    
    [Header("═══ BATTLE ROYALE OPTIONS ═══")]
    [Space(5)]
    
    [Tooltip("If enabled, killing all ragdolls does NOT auto-finish the level - you must reach the finish trigger manually")]
    [SerializeField] private bool requireManualFinish = false;
    
    [Header("═══ LIVES SYSTEM ═══")]
    [Space(5)]
    
    [Tooltip("Infinite = Player can die unlimited times | Limited = Game over after X deaths")]
    [SerializeField] private LivesMode livesMode = LivesMode.Limited;
    
    [Tooltip("Number of lives (deaths) player has before game over (only used if Lives Mode = Limited)")]
    [SerializeField] private int maxLives = 3;
    
    [Header("═══ SPAWNER SETTINGS ═══")]
    [Space(5)]
    
    [Tooltip("GLOBAL max active ragdolls across ALL spawners (prevents lag from too many simultaneous ragdolls)")]
    [Range(10, 100)]
    [SerializeField] private int maxActiveRagdollsGlobal = 50;
    
    [Space(10)]
    
    [Tooltip("Array of AI ragdoll spawners to track (drag and drop spawner GameObjects here)")]
    [SerializeField] private GameObject[] spawnerObjects;
    
    [Header("═══ WAVE SYSTEM (Optional) ═══")]
    [Space(5)]
    
    [Tooltip("Enable wave-based spawning system (spawners activate sequentially as waves are eliminated)")]
    [SerializeField] private bool useWaveSystem = false;
    
    [Tooltip("Number of waves in the system")]
    [SerializeField] private int numberOfWaves = 1;
    
    [Tooltip("Wave assignment for each spawner (index matches spawnerObjects array)")]
    [SerializeField] private int[] spawnerWaveAssignments = new int[0];
    
    [Tooltip("Wave configuration (each wave contains spawners and optional events)")]
    [SerializeField] private WaveData[] waves = new WaveData[0];
    
    [Header("═══ WAVE DEBUG INFO (Read-Only) ═══")]
    [Space(5)]
    
    [Tooltip("Current active wave number")]
    [SerializeField] private int currentWaveIndex = 0;
    
    [Tooltip("Ragdolls remaining in current wave")]
    [SerializeField] private int currentWaveRemaining = 0;
    
    [Tooltip("Total ragdolls in current wave")]
    [SerializeField] private int currentWaveTotalRagdolls = 0;
    
    [Header("═══ LIVES DEBUG INFO (Read-Only) ═══")]
    [Space(5)]
    
    [Tooltip("Current lives remaining")]
    [SerializeField] private int currentLives = 0;
    
    [Tooltip("Total deaths so far")]
    [SerializeField] private int totalDeaths = 0;
    
    [Tooltip("Has player run out of lives?")]
    [SerializeField] private bool gameOver = false;
    
    [Header("═══ RAGDOLL DEBUG INFO (Read-Only) ═══")]
    [Space(5)]
    
    [Tooltip("Total possible ragdolls that CAN spawn across all spawners (Battle Royale mode only)")]
    [SerializeField] private int totalPossibleRagdolls = 0;
    
    [Tooltip("Total ragdolls currently alive in the scene")]
    [SerializeField] private int totalCurrentlyAlive = 0;
    
    [Tooltip("Global active ragdoll count (across all spawners)")]
    [SerializeField] private int globalActiveCount = 0;
    
    [Tooltip("Total ragdolls that can still spawn (not yet spawned) (Battle Royale mode only)")]
    [SerializeField] private int totalCanStillSpawn = 0;
    
    [Tooltip("Total remaining ragdolls (alive + can still spawn) (Battle Royale mode only)")]
    [SerializeField] private int totalRemaining = 0;
    
    [Tooltip("Number of ragdolls eliminated in Battle Royale mode (or total kills in Infinite Spawn mode)")]
    [SerializeField] private int totalEliminated = 0;
    
    [Tooltip("Total kills across all time (works in both modes, counts respawning ragdolls)")]
    [SerializeField] private int totalKills = 0;
    
    [Tooltip("Has level been completed?")]
    [SerializeField] private bool levelComplete = false;
    
    [Tooltip("Have all ragdolls been eliminated? (Battle Royale mode only)")]
    [SerializeField] private bool allRagdollsEliminated = false;
    
    [Header("═══ VALIDATION ═══")]
    [Space(5)]
    
    [Tooltip("Show warning messages if spawner settings are incorrect")]
    [SerializeField] private bool showValidationWarnings = true;
    
    // Cached spawner components
    private List<AIRagdollSpawner> spawners = new List<AIRagdollSpawner>();
    
    // Wave system tracking
    private Dictionary<int, List<AIRagdollSpawner>> waveSpawners = new Dictionary<int, List<AIRagdollSpawner>>();
    private bool currentWaveComplete = false;
    
    // Cached references
    private LevelFinishTrigger levelFinishTrigger;
    private RespawnablePlayer player;
    
    private void Awake()
    {
        // Reset static flags at level start
        ForceZeroPointsOnFinish = false;
        PreventRespawning = false;
        
        // Initialize wave system if enabled
        if (useWaveSystem)
        {
            InitializeWaveSystem();
        }
        else
        {
            // Cache spawner components from GameObjects (non-wave mode)
            CacheSpawnerComponents();
        }
        
        // Find level finish trigger in scene
        levelFinishTrigger = FindFirstObjectByType<LevelFinishTrigger>();
        
        if (levelFinishTrigger == null)
        {
            Debug.LogError("[Battle Royale Manager] No LevelFinishTrigger found in scene! Cannot trigger level completion.");
        }
        
        // Find player
        player = FindFirstObjectByType<RespawnablePlayer>();
        
        if (player == null && gameMode != GameMode.InfiniteSpawn)
        {
            // Only warn if in Battle Royale mode (Infinite Spawn doesn't need player tracking)
            Debug.LogWarning("[Battle Royale Manager] No RespawnablePlayer found in scene. Player lives will not be tracked.");
        }
        
        // Initialize lives
        InitializeLives();
        
        // Configure spawners based on game mode
        ConfigureSpawnersForMode();
        
        // Validate spawner settings
        if (showValidationWarnings)
        {
            ValidateSpawnerSettings();
        }
        
        // Calculate initial totals
        CalculateTotals();
    }
    
    private void Start()
    {
        // Subscribe to player respawn event
        if (player != null)
        {
            // We'll use reflection to hook into the player's Respawn method
            // Or we can check for respawns each frame
        }
        
        // Log initialization
        string modeStr = gameMode == GameMode.BattleRoyale ? "BATTLE ROYALE" : "INFINITE SPAWN";
        string livesStr = livesMode == LivesMode.Infinite ? "INFINITE LIVES" : $"{currentLives} LIVES";
        string waveStr = useWaveSystem ? $", Waves: {waves.Length}" : "";
        Debug.Log($"<color=cyan>[Battle Royale Manager]</color> Initialized in {modeStr} mode with {livesStr}. Spawners: {spawners.Count}, Total ragdolls: {totalPossibleRagdolls}{waveStr}");
        
        if (useWaveSystem && waves.Length > 0)
        {
            Debug.Log($"<color=cyan>[Battle Royale Manager]</color> Starting WAVE 1 with {currentWaveTotalRagdolls} enemies!");
        }
    }
    
    private void Update()
    {
        // Skip if level already complete
        if (levelComplete) return;
        
        // Update all tracking values each frame
        CalculateTotals();
        
        // CHECK: If player is on last life, prevent respawning
        if (livesMode == LivesMode.Limited && currentLives <= 1 && !PreventRespawning)
        {
            PreventRespawning = true;
            Debug.Log($"<color=orange>[Battle Royale Manager]</color> ⚠️ LAST LIFE! Respawning disabled. Next death will trigger game over.");
        }
        
        // WAVE SYSTEM: Check for wave completion and transitions
        if (useWaveSystem)
        {
            CheckWaveCompletion();
        }
        
        // BATTLE ROYALE MODE: Check if all ragdolls eliminated
        if (gameMode == GameMode.BattleRoyale)
        {
            if (totalRemaining <= 0 && totalPossibleRagdolls > 0)
            {
                // Mark as eliminated (for tracking purposes)
                if (!allRagdollsEliminated)
                {
                    allRagdollsEliminated = true;
                    
                    // Check if manual finish is required
                    if (!requireManualFinish)
                    {
                        // Auto-finish when all ragdolls killed (default behavior)
                        OnVictory();
                    }
                    else
                    {
                        // Manual finish required - log message
                        Debug.Log($"<color=green>[Battle Royale Manager]</color> ✅ All {totalPossibleRagdolls} ragdolls eliminated! Now reach the finish trigger to complete the level.");
                    }
                }
            }
        }
        
        // INFINITE SPAWN MODE: No win condition, only lives matter
        // (Lives are checked via OnPlayerDeath)
    }
    
    /// <summary>
    /// Initialize lives system
    /// </summary>
    private void InitializeLives()
    {
        if (livesMode == LivesMode.Infinite)
        {
            currentLives = 999999; // Effectively infinite
        }
        else
        {
            currentLives = maxLives;
        }
        
        totalDeaths = 0;
        gameOver = false;
    }
    
    /// <summary>
    /// Configure spawners based on game mode
    /// </summary>
    private void ConfigureSpawnersForMode()
    {
        foreach (AIRagdollSpawner spawner in spawners)
        {
            if (spawner == null) continue;
            
            if (gameMode == GameMode.InfiniteSpawn)
            {
                // INFINITE SPAWN MODE: Override spawner settings
                spawner.limitTotalSpawns = false; // No spawn limit
                spawner.shouldRespawn = true; // Ragdolls respawn forever
                spawner.staysDeadIfShot = false; // Don't stay dead
                
                Debug.Log($"<color=yellow>[Battle Royale Manager]</color> Configured spawner '{spawner.gameObject.name}' for INFINITE SPAWN mode.");
            }
            else
            {
                // BATTLE ROYALE MODE: Keep spawner settings as-is
                // (User should have configured spawners manually)
            }
        }
    }
    
    /// <summary>
    /// Cache AIRagdollSpawner components from the spawner GameObjects array
    /// </summary>
    private void CacheSpawnerComponents()
    {
        spawners.Clear();
        
        if (spawnerObjects == null || spawnerObjects.Length == 0)
        {
            Debug.LogWarning("<color=yellow>[Battle Royale Manager]</color> No spawner objects assigned! Drag and drop spawner GameObjects into the Spawners array.");
            return;
        }
        
        foreach (GameObject spawnerObj in spawnerObjects)
        {
            if (spawnerObj == null)
            {
                Debug.LogWarning("<color=yellow>[Battle Royale Manager]</color> Null spawner object in array! Remove empty slots.");
                continue;
            }
            
            AIRagdollSpawner spawner = spawnerObj.GetComponent<AIRagdollSpawner>();
            
            if (spawner == null)
            {
                Debug.LogWarning($"<color=yellow>[Battle Royale Manager]</color> GameObject '{spawnerObj.name}' has no AIRagdollSpawner component!");
                continue;
            }
            
            spawners.Add(spawner);
        }
        
        Debug.Log($"<color=cyan>[Battle Royale Manager]</color> Cached {spawners.Count} spawner components.");
    }
    
    /// <summary>
    /// Validate that all spawners have correct settings for the current game mode
    /// </summary>
    private void ValidateSpawnerSettings()
    {
        if (gameMode == GameMode.BattleRoyale)
        {
            // Validate Battle Royale mode settings
            foreach (AIRagdollSpawner spawner in spawners)
            {
                if (spawner == null) continue;
                
                // Check if limitTotalSpawns is enabled
                if (!spawner.limitTotalSpawns)
                {
                    Debug.LogWarning($"<color=yellow>[Battle Royale Manager]</color> Spawner '{spawner.gameObject.name}' has limitTotalSpawns = false! " +
                        "For Battle Royale mode, this should be TRUE. The spawner will spawn infinitely otherwise.");
                }
                
                // Check if respawn is disabled (one of the two methods)
                // Note: Commented out to reduce console spam - developers should validate spawner settings in inspector
                // if (spawner.shouldRespawn && !spawner.staysDeadIfShot && spawner.limitTotalSpawns)
                // {
                //     Debug.LogWarning($"<color=yellow>[Battle Royale Manager]</color> Spawner '{spawner.gameObject.name}' may respawn infinitely. " +
                //         "Consider setting shouldRespawn=false OR staysDeadIfShot=true for proper Battle Royale behavior.", spawner.gameObject);
                // }
            }
        }
        else if (gameMode == GameMode.InfiniteSpawn)
        {
            // Infinite Spawn mode: Spawner settings are overridden automatically in ConfigureSpawnersForMode()
            // No validation needed - just inform the user
            Debug.Log($"<color=cyan>[Battle Royale Manager]</color> Infinite Spawn mode: Spawner settings will be overridden automatically.");
        }
    }
    
    /// <summary>
    /// Calculate all tracking values from spawners
    /// </summary>
    private void CalculateTotals()
    {
        if (useWaveSystem)
        {
            // Calculate totals for wave system (current wave only or all waves)
            CalculateWaveTotals();
        }
        else
        {
            // Calculate totals for non-wave mode (all spawners)
            totalPossibleRagdolls = 0;
            totalCurrentlyAlive = 0;
            totalCanStillSpawn = 0;
            
            foreach (AIRagdollSpawner spawner in spawners)
            {
                if (spawner == null) continue;
                
                // Get stats from spawner
                int spawnerMaxTotal = spawner.GetMaxTotalSpawns();
                int spawnerCurrentlyAlive = spawner.GetActiveRagdollCount();
                int spawnerTotalSpawned = spawner.GetTotalSpawnsCount();
                
                // Calculate possible spawns for this spawner
                int spawnerCanStillSpawn = Mathf.Max(0, spawnerMaxTotal - spawnerTotalSpawned);
                
                // Add to totals
                totalPossibleRagdolls += spawnerMaxTotal;
                totalCurrentlyAlive += spawnerCurrentlyAlive;
                totalCanStillSpawn += spawnerCanStillSpawn;
            }
            
            // Calculate remaining and eliminated
            totalRemaining = totalCurrentlyAlive + totalCanStillSpawn;
            totalEliminated = totalPossibleRagdolls - totalRemaining;
        }
    }
    
    // ==================== WAVE SYSTEM METHODS ====================
    
    /// <summary>
    /// Initialize the wave system and prepare all waves
    /// </summary>
    private void InitializeWaveSystem()
    {
        waveSpawners.Clear();
        spawners.Clear();
        currentWaveIndex = 0;
        currentWaveComplete = false;
        
        if (waves == null || waves.Length == 0)
        {
            Debug.LogWarning("<color=yellow>[Battle Royale Manager]</color> Wave system enabled but no waves configured!");
            return;
        }
        
        // Cache spawner components for each wave
        for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
        {
            WaveData wave = waves[waveIndex];
            List<AIRagdollSpawner> spawnerList = new List<AIRagdollSpawner>();
            
            if (wave.spawners == null || wave.spawners.Count == 0)
            {
                Debug.LogWarning($"<color=yellow>[Battle Royale Manager]</color> Wave {waveIndex + 1} has no spawners assigned!");
                continue;
            }
            
            foreach (GameObject spawnerObj in wave.spawners)
            {
                if (spawnerObj == null)
                {
                    Debug.LogWarning($"<color=yellow>[Battle Royale Manager]</color> Null spawner in Wave {waveIndex + 1}!");
                    continue;
                }
                
                AIRagdollSpawner spawner = spawnerObj.GetComponent<AIRagdollSpawner>();
                
                if (spawner == null)
                {
                    Debug.LogWarning($"<color=yellow>[Battle Royale Manager]</color> GameObject '{spawnerObj.name}' in Wave {waveIndex + 1} has no AIRagdollSpawner component!");
                    continue;
                }
                
                spawnerList.Add(spawner);
                spawners.Add(spawner); // Also add to main spawners list
            }
            
            waveSpawners[waveIndex] = spawnerList;
        }
        
        // Disable all spawners except Wave 1
        for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
        {
            if (waveIndex == 0)
            {
                // Wave 1: Keep enabled (spawns immediately)
                continue;
            }
            
            // Other waves: Disable spawning until activated
            if (waveSpawners.ContainsKey(waveIndex))
            {
                foreach (AIRagdollSpawner spawner in waveSpawners[waveIndex])
                {
                    if (spawner != null)
                    {
                        // Disable spawning by setting spawnOnStart to false
                        spawner.spawnOnStart = false;
                        spawner.gameObject.SetActive(false); // Disable entire spawner GameObject
                    }
                }
            }
        }
        
        Debug.Log($"<color=cyan>[Battle Royale Manager]</color> Wave system initialized with {waves.Length} waves. Total spawners: {spawners.Count}");
    }
    
    /// <summary>
    /// Check if current wave is complete and activate next wave
    /// </summary>
    private void CheckWaveCompletion()
    {
        if (waves == null || waves.Length == 0) return;
        if (currentWaveIndex >= waves.Length) return; // All waves complete
        if (currentWaveComplete) return; // Already processed completion
        
        // Check if current wave has any ragdolls remaining
        if (currentWaveRemaining <= 0 && currentWaveTotalRagdolls > 0)
        {
            // Current wave is complete!
            currentWaveComplete = true;
            
            int completedWaveNum = currentWaveIndex + 1;
            Debug.Log($"<color=green>[Battle Royale Manager]</color> 🎉 WAVE {completedWaveNum} COMPLETE!");
            
            // Trigger wave completion events
            TriggerWaveCompletionEvents(currentWaveIndex);
            
            // Check if there's a next wave
            if (currentWaveIndex + 1 < waves.Length)
            {
                // Activate next wave after a short delay
                Invoke(nameof(ActivateNextWave), 2f); // 2 second delay before next wave
            }
            else
            {
                // All waves complete!
                Debug.Log($"<color=green>[Battle Royale Manager]</color> 🏆 ALL WAVES COMPLETE!");
                
                // In Battle Royale mode, trigger victory
                if (gameMode == GameMode.BattleRoyale)
                {
                    if (!requireManualFinish)
                    {
                        OnVictory();
                    }
                    else
                    {
                        Debug.Log($"<color=green>[Battle Royale Manager]</color> Now reach the finish trigger to complete the level.");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Activate the next wave of spawners
    /// </summary>
    private void ActivateNextWave()
    {
        currentWaveIndex++;
        currentWaveComplete = false;
        
        if (currentWaveIndex >= waves.Length)
        {
            Debug.LogError("[Battle Royale Manager] Tried to activate wave beyond array bounds!");
            return;
        }
        
        int nextWaveNum = currentWaveIndex + 1;
        Debug.Log($"<color=cyan>[Battle Royale Manager]</color> 🚨 Starting WAVE {nextWaveNum}!");
        
        // Enable and activate spawners for this wave
        if (waveSpawners.ContainsKey(currentWaveIndex))
        {
            foreach (AIRagdollSpawner spawner in waveSpawners[currentWaveIndex])
            {
                if (spawner != null)
                {
                    // Re-enable spawner GameObject
                    spawner.gameObject.SetActive(true);
                    
                    // Force spawn initial ragdolls using reflection
                    System.Type type = spawner.GetType();
                    System.Reflection.MethodInfo spawnMethod = type.GetMethod("SpawnInitialRagdolls", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (spawnMethod != null)
                    {
                        spawnMethod.Invoke(spawner, null);
                    }
                }
            }
        }
        
        // Recalculate totals for new wave
        CalculateWaveTotals();
        
        Debug.Log($"<color=cyan>[Battle Royale Manager]</color> Wave {nextWaveNum} activated with {currentWaveTotalRagdolls} enemies!");
    }
    
    /// <summary>
    /// Trigger object activation/deactivation events for completed wave
    /// </summary>
    private void TriggerWaveCompletionEvents(int waveIndex)
    {
        if (waveIndex < 0 || waveIndex >= waves.Length) return;
        
        WaveData wave = waves[waveIndex];
        
        // Activate objects
        if (wave.objectsToActivate != null && wave.objectsToActivate.Count > 0)
        {
            foreach (GameObject obj in wave.objectsToActivate)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    Debug.Log($"<color=cyan>[Battle Royale Manager]</color> Activated: {obj.name}");
                }
            }
        }
        
        // Deactivate objects
        if (wave.objectsToDeactivate != null && wave.objectsToDeactivate.Count > 0)
        {
            foreach (GameObject obj in wave.objectsToDeactivate)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    Debug.Log($"<color=cyan>[Battle Royale Manager]</color> Deactivated: {obj.name}");
                }
            }
        }
    }
    
    /// <summary>
    /// Calculate totals for wave system (current wave stats + total stats)
    /// </summary>
    private void CalculateWaveTotals()
    {
        // Calculate CURRENT WAVE stats
        currentWaveTotalRagdolls = 0;
        int currentWaveAlive = 0;
        int currentWaveCanStillSpawn = 0;
        
        if (currentWaveIndex < waves.Length && waveSpawners.ContainsKey(currentWaveIndex))
        {
            foreach (AIRagdollSpawner spawner in waveSpawners[currentWaveIndex])
            {
                if (spawner == null) continue;
                
                // Get stats from spawner
                int spawnerMaxTotal = spawner.GetMaxTotalSpawns();
                int spawnerCurrentlyAlive = spawner.GetActiveRagdollCount();
                int spawnerTotalSpawned = spawner.GetTotalSpawnsCount();
                
                // Calculate possible spawns for this spawner
                int spawnerCanStillSpawn = Mathf.Max(0, spawnerMaxTotal - spawnerTotalSpawned);
                
                // Add to current wave totals
                currentWaveTotalRagdolls += spawnerMaxTotal;
                currentWaveAlive += spawnerCurrentlyAlive;
                currentWaveCanStillSpawn += spawnerCanStillSpawn;
            }
        }
        
        currentWaveRemaining = currentWaveAlive + currentWaveCanStillSpawn;
        
        // Calculate OVERALL stats (all waves)
        totalPossibleRagdolls = 0;
        totalCurrentlyAlive = 0;
        totalCanStillSpawn = 0;
        
        foreach (AIRagdollSpawner spawner in spawners)
        {
            if (spawner == null) continue;
            
            // Get stats from spawner
            int spawnerMaxTotal = spawner.GetMaxTotalSpawns();
            int spawnerCurrentlyAlive = spawner.GetActiveRagdollCount();
            int spawnerTotalSpawned = spawner.GetTotalSpawnsCount();
            
            // Calculate possible spawns for this spawner
            int spawnerCanStillSpawn = Mathf.Max(0, spawnerMaxTotal - spawnerTotalSpawned);
            
            // Add to totals
            totalPossibleRagdolls += spawnerMaxTotal;
            totalCurrentlyAlive += spawnerCurrentlyAlive;
            totalCanStillSpawn += spawnerCanStillSpawn;
        }
        
        // Calculate remaining and eliminated
        totalRemaining = totalCurrentlyAlive + totalCanStillSpawn;
        totalEliminated = totalPossibleRagdolls - totalRemaining;
    }
    
    /// <summary>
    /// Called when player wins (all ragdolls eliminated in Battle Royale mode)
    /// </summary>
    private void OnVictory()
    {
        levelComplete = true;
        
        Debug.Log($"<color=green>[Battle Royale Manager]</color> 🏆 VICTORY! All {totalPossibleRagdolls} ragdolls eliminated!");
        
        // Make sure the flag is false (normal scoring)
        ForceZeroPointsOnFinish = false;
        
        // Trigger level finish with normal scoring
        TriggerLevelFinish();
    }
    
    /// <summary>
    /// Called when player runs out of lives (game over)
    /// </summary>
    private void OnGameOver()
    {
        levelComplete = true;
        gameOver = true;
        
        Debug.Log($"<color=red>[Battle Royale Manager]</color> ☠️ GAME OVER! Out of lives ({totalDeaths} deaths).");
        
        // Set static flag so LevelFinishTrigger knows to force zero points
        ForceZeroPointsOnFinish = true;
        
        // Trigger level finish
        TriggerLevelFinish();
    }
    
    /// <summary>
    /// Trigger the level finish
    /// </summary>
    private void TriggerLevelFinish()
    {
        if (levelFinishTrigger == null)
        {
            Debug.LogError("[Battle Royale Manager] No LevelFinishTrigger found! Cannot complete level.");
            return;
        }
        
        // Trigger level finish using reflection
        System.Type type = levelFinishTrigger.GetType();
        System.Reflection.MethodInfo completeMethod = type.GetMethod("CompleteLevel", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (completeMethod != null)
        {
            completeMethod.Invoke(levelFinishTrigger, null);
            Debug.Log($"<color=cyan>[Battle Royale Manager]</color> Level finish triggered!");
        }
        else
        {
            Debug.LogError("[Battle Royale Manager] Could not find CompleteLevel() method on LevelFinishTrigger!");
        }
    }
    
    /// <summary>
    /// Called when player dies but respawning is blocked (final death)
    /// </summary>
    public void OnFinalDeath()
    {
        // Skip if level already complete
        if (levelComplete) return;
        
        currentLives = 0;
        totalDeaths++;
        
        Debug.Log($"<color=red>[Battle Royale Manager]</color> ☠️ FINAL DEATH! Lives: {currentLives}/{maxLives}");
        
        // Trigger game over with a small delay to let the ragdoll effect happen naturally
        Invoke(nameof(OnGameOver), 1.5f);
    }
    
    /// <summary>
    /// Called when player dies/respawns (for non-final deaths)
    /// </summary>
    public void OnPlayerDeath()
    {
        // Skip if level already complete
        if (levelComplete) return;
        
        // Skip if infinite lives
        if (livesMode == LivesMode.Infinite) return;
        
        // Decrement lives
        currentLives--;
        totalDeaths++;
        
        Debug.Log($"<color=orange>[Battle Royale Manager]</color> Player died! Lives remaining: {currentLives}/{maxLives}");
    }
    
    /// <summary>
    /// Called when a ragdoll is killed (works in both modes, counts all kills)
    /// </summary>
    public void OnRagdollKilled()
    {
        totalKills++;
        
        // In Infinite Spawn mode, also update totalEliminated to show kill count
        if (gameMode == GameMode.InfiniteSpawn)
        {
            totalEliminated = totalKills;
        }
    }
    
    // ==================== PUBLIC API ====================
    
    /// <summary>
    /// Get the total number of possible ragdolls across all spawners
    /// </summary>
    public int GetTotalPossibleRagdolls()
    {
        return totalPossibleRagdolls;
    }
    
    /// <summary>
    /// Get the total number of ragdolls currently alive
    /// </summary>
    public int GetTotalCurrentlyAlive()
    {
        return totalCurrentlyAlive;
    }
    
    /// <summary>
    /// Get the total number of ragdolls that can still spawn
    /// </summary>
    public int GetTotalCanStillSpawn()
    {
        return totalCanStillSpawn;
    }
    
    /// <summary>
    /// Get the total remaining ragdolls (alive + can still spawn)
    /// </summary>
    public int GetTotalRemaining()
    {
        return totalRemaining;
    }
    
    /// <summary>
    /// Get the total number of ragdolls eliminated (Battle Royale) or killed (Infinite Spawn)
    /// </summary>
    public int GetTotalEliminated()
    {
        return totalEliminated;
    }
    
    /// <summary>
    /// Get the total number of kills (works in both modes, counts all kills including respawning ragdolls)
    /// </summary>
    public int GetTotalKills()
    {
        return totalKills;
    }
    
    /// <summary>
    /// Check if level is complete
    /// </summary>
    public bool IsLevelComplete()
    {
        return levelComplete;
    }
    
    /// <summary>
    /// Check if game over (ran out of lives)
    /// </summary>
    public bool IsGameOver()
    {
        return gameOver;
    }
    
    /// <summary>
    /// Get current lives remaining
    /// </summary>
    public int GetCurrentLives()
    {
        return currentLives;
    }
    
    /// <summary>
    /// Get max lives
    /// </summary>
    public int GetMaxLives()
    {
        return maxLives;
    }
    
    /// <summary>
    /// Get total deaths
    /// </summary>
    public int GetTotalDeaths()
    {
        return totalDeaths;
    }
    
    /// <summary>
    /// Get the current game mode
    /// </summary>
    public GameMode GetGameMode()
    {
        return gameMode;
    }
    
    /// <summary>
    /// Get the current lives mode
    /// </summary>
    public LivesMode GetLivesMode()
    {
        return livesMode;
    }
    
    /// <summary>
    /// Check if manual finish is required (Battle Royale mode only)
    /// </summary>
    public bool IsManualFinishRequired()
    {
        return requireManualFinish;
    }
    
    /// <summary>
    /// Check if all ragdolls have been eliminated (Battle Royale mode only)
    /// </summary>
    public bool AreAllRagdollsEliminated()
    {
        return gameMode == GameMode.BattleRoyale && totalRemaining <= 0 && totalPossibleRagdolls > 0;
    }
    
    /// <summary>
    /// Get the list of spawners being tracked
    /// </summary>
    public List<AIRagdollSpawner> GetSpawners()
    {
        return spawners;
    }
    
    // ==================== GLOBAL RAGDOLL LIMIT API ====================
    
    /// <summary>
    /// Check if spawning is allowed (checks global max active ragdoll limit)
    /// Call this before spawning to respect the global limit
    /// </summary>
    public bool CanSpawnRagdoll()
    {
        return globalActiveCount < maxActiveRagdollsGlobal;
    }
    
    /// <summary>
    /// Notify manager that a ragdoll was spawned (increments global count)
    /// </summary>
    public void NotifyRagdollSpawned()
    {
        globalActiveCount++;
        // Debug.Log($"<color=green>[BattleRoyaleManager]</color> Ragdoll spawned! Global count: {globalActiveCount}/{maxActiveRagdollsGlobal}");
    }
    
    /// <summary>
    /// Notify manager that a ragdoll died (decrements global count)
    /// </summary>
    public void NotifyRagdollDied()
    {
        if (globalActiveCount > 0)
        {
            globalActiveCount--;
            // Debug.Log($"<color=orange>[BattleRoyaleManager]</color> Ragdoll died. Global count: {globalActiveCount}/{maxActiveRagdollsGlobal}");
        }
        else
        {
            Debug.LogWarning($"<color=red>[BattleRoyaleManager]</color> NotifyRagdollDied() called but globalActiveCount is already 0!");
        }
    }
    
    /// <summary>
    /// Get current global active ragdoll count
    /// </summary>
    public int GetGlobalActiveCount()
    {
        return globalActiveCount;
    }
    
    /// <summary>
    /// Get global max ragdoll limit
    /// </summary>
    public int GetGlobalMaxRagdolls()
    {
        return maxActiveRagdollsGlobal;
    }
    
    // ==================== WAVE SYSTEM PUBLIC API ====================
    
    /// <summary>
    /// Check if wave system is enabled
    /// </summary>
    public bool IsWaveSystemEnabled()
    {
        return useWaveSystem;
    }
    
    /// <summary>
    /// Get the current wave index (0-based)
    /// </summary>
    public int GetCurrentWaveIndex()
    {
        return currentWaveIndex;
    }
    
    /// <summary>
    /// Get the current wave number (1-based, for display)
    /// </summary>
    public int GetCurrentWaveNumber()
    {
        return currentWaveIndex + 1;
    }
    
    /// <summary>
    /// Get the total number of waves
    /// </summary>
    public int GetTotalWaves()
    {
        return waves != null ? waves.Length : 0;
    }
    
    /// <summary>
    /// Get the number of ragdolls remaining in the current wave
    /// </summary>
    public int GetCurrentWaveRemaining()
    {
        return currentWaveRemaining;
    }
    
    /// <summary>
    /// Get the total number of ragdolls in the current wave
    /// </summary>
    public int GetCurrentWaveTotalRagdolls()
    {
        return currentWaveTotalRagdolls;
    }
    
    /// <summary>
    /// Check if all waves are complete
    /// </summary>
    public bool AreAllWavesComplete()
    {
        if (!useWaveSystem) return false;
        return currentWaveIndex >= waves.Length - 1 && currentWaveRemaining <= 0;
    }
    
    // ==================== GIZMOS ====================
    
    private void OnDrawGizmos()
    {
        // Draw a battle royale icon at the manager's position
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        // Draw crosshairs
        Gizmos.DrawLine(transform.position + Vector3.left, transform.position + Vector3.right);
        Gizmos.DrawLine(transform.position + Vector3.forward, transform.position + Vector3.back);
        
        // Draw lines to all spawners
        if (spawnerObjects != null && spawnerObjects.Length > 0)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // Orange
            foreach (GameObject spawnerObj in spawnerObjects)
            {
                if (spawnerObj != null)
                {
                    Gizmos.DrawLine(transform.position, spawnerObj.transform.position);
                }
            }
        }
    }
}

