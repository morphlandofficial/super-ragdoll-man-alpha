using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Master level progression manager - persists across scenes and tracks all level progress.
/// Place this on a GameObject in the title screen scene.
/// It will automatically persist using DontDestroyOnLoad.
/// </summary>
public class LevelManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════
    // SINGLETON
    // ═══════════════════════════════════════════════════════
    
    public static LevelManager Instance { get; private set; }
    
    // ═══════════════════════════════════════════════════════
    // AUTO-DISCOVERY
    // ═══════════════════════════════════════════════════════
    
    [Header("--- Auto-Discovery from Title Screen ---")]
    [Tooltip("Parent GameObject containing all ProximitySceneLoader zones (e.g., EARTH)")]
    [SerializeField] private GameObject earthParent;
    
    [Tooltip("Auto-discover levels from EARTH hierarchy on start?")]
    [SerializeField] private bool autoDiscoverOnStart = true;
    
    [Header("--- Save System (Optional) ---")]
    [Tooltip("Enable saving progress to PlayerPrefs? Disable for fresh testing every run.")]
    [SerializeField] private bool enableSaveSystem = false;
    
    [Header("--- Debug Settings ---")]
    [Tooltip("Enable verbose debug logging? Disable for cleaner console.")]
    [SerializeField] private bool enableVerboseLogging = false;
    
    // ═══════════════════════════════════════════════════════
    // DATA STORAGE
    // ═══════════════════════════════════════════════════════
    
    [Header("--- Level Configuration ---")]
    [Tooltip("Manually configure which levels are locked/unlocked here")]
    [SerializeField] private List<LevelData> levelProgress = new List<LevelData>();
    
    // Dictionary for fast lookups (levelID → LevelData)
    private Dictionary<string, LevelData> levelDict = new Dictionary<string, LevelData>();
    
    // ═══════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════
    
    void Awake()
    {
        // Singleton setup with DontDestroyOnLoad
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Subscribe to scene loaded events to refresh portal links
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            if (enableVerboseLogging)
                Debug.Log($"[LevelManager] ===== AWAKE START ===== Inspector list has {levelProgress.Count} entries");
            
            // Load saved progress first (if save system enabled)
            if (enableSaveSystem)
            {
                LoadProgress();
            }
            else
            {
                // Build dictionary from Inspector list (no saves)
                levelDict.Clear();
                foreach (var data in levelProgress)
                {
                    if (!string.IsNullOrEmpty(data.levelID))
                    {
                        levelDict[data.levelID] = data;
                    }
                }
                if (enableVerboseLogging)
                    Debug.Log($"[LevelManager] Save system DISABLED. Using Inspector configuration only ({levelProgress.Count} levels).");
            }
            
            if (enableVerboseLogging)
                Debug.Log($"[LevelManager] After LoadProgress: {levelProgress.Count} levels, {levelDict.Count} in dictionary");
            
            // Auto-discover levels from EARTH hierarchy (only refreshes portal links, preserves unlock states)
            if (autoDiscoverOnStart)
            {
                RefreshPortalZoneLinks();
            }
            
            // Sync portal zone visibility with unlock states
            SyncPortalZoneVisibility();
            
            if (enableVerboseLogging)
                Debug.Log($"[LevelManager] ===== AWAKE END ===== Final count: {levelProgress.Count} levels");
        }
        else
        {
            // Another LevelManager exists - destroy this duplicate
            Destroy(gameObject);
            return;
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from scene loaded events
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
    
    /// <summary>
    /// Called whenever a scene is loaded - refreshes portal zone links for title screen
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] 🔄 Scene loaded: '{scene.name}' - Refreshing portal zone links...");
        
        // Find Earth parent dynamically (it gets recreated when scene loads)
        GameObject earth = GameObject.Find("Earth");
        if (earth != null)
        {
            earthParent = earth;
            if (enableVerboseLogging)
                Debug.Log($"[LevelManager] ✅ Found Earth parent in scene '{scene.name}'");
        }
        else
        {
            Debug.LogWarning($"[LevelManager] ⚠️ Could not find 'Earth' GameObject in scene '{scene.name}'");
        }
        
        // Refresh portal zone links (they get recreated when scene loads)
        RefreshPortalZoneLinks();
        
        // Re-sync visibility with current unlock states
        SyncPortalZoneVisibility();
    }
    
    /// <summary>
    /// Sync all portal zone GameObjects with their unlock states (RUNTIME ONLY)
    /// Shows unlocked levels, hides locked levels.
    /// Called automatically on Awake - do not call this in Editor!
    /// </summary>
    private void SyncPortalZoneVisibility()
    {
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] 🔄 SyncPortalZoneVisibility() - Syncing {levelProgress.Count} levels...");
        
        int unlockedCount = 0;
        int lockedCount = 0;
        
        foreach (var data in levelProgress)
        {
            if (data.portalZoneObject != null)
            {
                data.portalZoneObject.SetActive(data.isUnlocked);
                
                if (data.isUnlocked)
                {
                    unlockedCount++;
                    if (enableVerboseLogging)
                        Debug.Log($"[LevelManager] ✅ '{data.levelID}' portal ACTIVE (unlocked)");
                }
                else
                {
                    lockedCount++;
                    if (enableVerboseLogging)
                        Debug.Log($"[LevelManager] 🔒 '{data.levelID}' portal INACTIVE (locked)");
                }
            }
        }
        
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] 🔄 Sync complete: {unlockedCount} unlocked, {lockedCount} locked");
    }
    
    // ═══════════════════════════════════════════════════════
    // AUTO-DISCOVERY
    // ═══════════════════════════════════════════════════════
    
    /// <summary>
    /// Refresh portal zone links for existing levels (preserves unlock states).
    /// Called automatically on game start - only updates GameObject references.
    /// </summary>
    private void RefreshPortalZoneLinks()
    {
        if (earthParent == null)
        {
            Debug.LogWarning("[LevelManager] EARTH parent not assigned! Portal zones will not be linked. Assign EARTH GameObject in Inspector.");
            return;
        }
        
        if (levelProgress.Count == 0)
        {
            Debug.LogWarning("[LevelManager] Level list is empty! Click 'Discover Levels from EARTH' button in Inspector to populate.");
            return;
        }
        
        // Find all ProximitySceneLoader components
        ProximitySceneLoader[] loaders = earthParent.GetComponentsInChildren<ProximitySceneLoader>(true);
        ProximitySceneLoaderAnimated[] animatedLoaders = earthParent.GetComponentsInChildren<ProximitySceneLoaderAnimated>(true);
        
        int linkedCount = 0;
        int totalLoaders = loaders.Length + animatedLoaders.Length;
        
        // Process standard loaders - ONLY update portal links
        foreach (var loader in loaders)
        {
            string sceneName = GetSceneNameFromLoader(loader);
            if (!string.IsNullOrEmpty(sceneName) && levelDict.ContainsKey(sceneName))
            {
                LevelData data = levelDict[sceneName];
                if (data.portalZoneObject == null || data.portalZoneObject != loader.gameObject)
                {
                    data.portalZoneObject = loader.gameObject;
                    linkedCount++;
                }
            }
        }
        
        // Process animated loaders - ONLY update portal links
        foreach (var loader in animatedLoaders)
        {
            string sceneName = GetSceneNameFromAnimatedLoader(loader);
            if (!string.IsNullOrEmpty(sceneName) && levelDict.ContainsKey(sceneName))
            {
                LevelData data = levelDict[sceneName];
                if (data.portalZoneObject == null || data.portalZoneObject != loader.gameObject)
                {
                    data.portalZoneObject = loader.gameObject;
                    linkedCount++;
                }
            }
        }
        
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] 🔗 Linked {linkedCount}/{totalLoaders} portal zones (found {levelProgress.Count} configured levels)");
    }
    
    /// <summary>
    /// Discover all levels from ProximitySceneLoader components in EARTH hierarchy.
    /// Automatically links portal zone GameObjects to their corresponding levels.
    /// USE THIS BUTTON IN INSPECTOR - Creates new level entries with default unlock states.
    /// </summary>
    public void DiscoverLevelsFromEarth()
    {
        if (earthParent == null)
        {
            Debug.LogWarning("[LevelManager] EARTH parent not assigned! Cannot auto-discover levels. Assign the EARTH GameObject in the Inspector.");
            return;
        }
        
        // Find all ProximitySceneLoader components in children
        ProximitySceneLoader[] loaders = earthParent.GetComponentsInChildren<ProximitySceneLoader>(true);
        ProximitySceneLoaderAnimated[] animatedLoaders = earthParent.GetComponentsInChildren<ProximitySceneLoaderAnimated>(true);
        
        int discoveredCount = 0;
        int linkedCount = 0;
        
        // Process standard loaders
        foreach (var loader in loaders)
        {
            string sceneName = GetSceneNameFromLoader(loader);
            if (!string.IsNullOrEmpty(sceneName))
            {
                bool isNew = RegisterDiscoveredLevel(sceneName, loader.gameObject);
                if (isNew) discoveredCount++;
                linkedCount++;
            }
        }
        
        // Process animated loaders
        foreach (var loader in animatedLoaders)
        {
            string sceneName = GetSceneNameFromAnimatedLoader(loader);
            if (!string.IsNullOrEmpty(sceneName))
            {
                bool isNew = RegisterDiscoveredLevel(sceneName, loader.gameObject);
                if (isNew) discoveredCount++;
                linkedCount++;
            }
        }
        
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] ✅ Discovered {discoveredCount} new levels. Auto-linked {linkedCount} portal zones. Total levels: {levelProgress.Count}");
    }
    
    /// <summary>
    /// Extract scene name from ProximitySceneLoader using reflection
    /// </summary>
    private string GetSceneNameFromLoader(ProximitySceneLoader loader)
    {
        // Use reflection to access the private sceneToLoad field
        var field = typeof(ProximitySceneLoader).GetField("sceneToLoad", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            Object sceneAsset = field.GetValue(loader) as Object;
            if (sceneAsset != null)
            {
                return sceneAsset.name;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Extract scene name from ProximitySceneLoaderAnimated using reflection
    /// </summary>
    private string GetSceneNameFromAnimatedLoader(ProximitySceneLoaderAnimated loader)
    {
        // Use reflection to access the private sceneToLoad field
        var field = typeof(ProximitySceneLoaderAnimated).GetField("sceneToLoad", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            Object sceneAsset = field.GetValue(loader) as Object;
            if (sceneAsset != null)
            {
                return sceneAsset.name;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Register a discovered level and auto-link portal zone GameObject
    /// </summary>
    private bool RegisterDiscoveredLevel(string levelID, GameObject portalZone)
    {
        // Check if already exists
        if (levelDict.ContainsKey(levelID))
        {
            // Level exists - just update portal zone reference if not set
            LevelData existingData = levelDict[levelID];
            if (existingData.portalZoneObject == null)
            {
                existingData.portalZoneObject = portalZone;
                if (enableVerboseLogging)
                    Debug.Log($"[LevelManager] 🔗 Linked portal zone '{portalZone.name}' to existing level '{levelID}'");
            }
            return false; // Already registered (from save file or previous discovery)
        }
        
        // Create new level data with default settings
        LevelData newData = new LevelData
        {
            levelID = levelID,
            bestPhysicsScore = 0f,
            bestTime = 0f,
            highestAchievement = Achievement.None,
            isUnlocked = false, // Default: LOCKED (unlock via achievements or Inspector)
            hasBeenPlayed = false,
            portalZoneObject = portalZone // AUTO-ASSIGN portal zone!
        };
        
        levelProgress.Add(newData);
        levelDict[levelID] = newData;
        
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] ➕ New level '{levelID}' discovered and linked to portal '{portalZone.name}'");
        
        return true; // Successfully added new level
    }
    
    // ═══════════════════════════════════════════════════════
    // SAVE / LOAD (PlayerPrefs)
    // ═══════════════════════════════════════════════════════
    
    /// <summary>
    /// Load all level progress from PlayerPrefs and MERGE with Inspector configuration.
    /// Inspector is the source of truth for level list - saved data updates progress only.
    /// </summary>
    private void LoadProgress()
    {
        // Build dictionary from Inspector list first (source of truth)
        levelDict.Clear();
        foreach (var data in levelProgress)
        {
            if (!string.IsNullOrEmpty(data.levelID))
            {
                levelDict[data.levelID] = data;
            }
        }
        
        // Check if we have saved data
        int savedLevelCount = PlayerPrefs.GetInt("LevelCount", 0);
        
        if (savedLevelCount == 0)
        {
            // NO SAVED DATA - Keep Inspector configuration as-is
            if (enableVerboseLogging)
                Debug.Log($"[LevelManager] No saved progress found. Using Inspector configuration ({levelProgress.Count} levels).");
            return;
        }
        
        // MERGE SAVED DATA with Inspector configuration
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] Merging {savedLevelCount} saved levels with {levelProgress.Count} configured levels.");
        
        int mergedCount = 0;
        int unmatchedSaves = 0;
        
        for (int i = 0; i < savedLevelCount; i++)
        {
            string key = $"Level_{i}_";
            string savedLevelID = PlayerPrefs.GetString(key + "ID", "");
            
            // Check if this saved level exists in Inspector configuration
            if (levelDict.ContainsKey(savedLevelID))
            {
                // UPDATE existing Inspector entry with saved progress
                LevelData inspectorData = levelDict[savedLevelID];
                inspectorData.bestPhysicsScore = PlayerPrefs.GetFloat(key + "PhysicsScore", 0f);
                inspectorData.bestTime = PlayerPrefs.GetFloat(key + "Time", 0f);
                inspectorData.highestAchievement = (Achievement)PlayerPrefs.GetInt(key + "Achievement", 0);
                inspectorData.isUnlocked = PlayerPrefs.GetInt(key + "Unlocked", 0) == 1;
                inspectorData.hasBeenPlayed = PlayerPrefs.GetInt(key + "Played", 0) == 1;
                mergedCount++;
            }
            else
            {
                // Saved level doesn't exist in Inspector (maybe removed?)
                unmatchedSaves++;
                Debug.LogWarning($"[LevelManager] Saved level '{savedLevelID}' not found in Inspector configuration. Ignoring.");
            }
        }
        
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] ✅ Merged {mergedCount} saved levels. {unmatchedSaves} saved levels ignored (not in Inspector).");
    }
    
    /// <summary>
    /// Save all level progress to PlayerPrefs (only if save system enabled)
    /// </summary>
    public void SaveProgress()
    {
        if (!enableSaveSystem)
        {
            return; // Save system disabled - don't save
        }
        
        PlayerPrefs.SetInt("LevelCount", levelProgress.Count);
        
        for (int i = 0; i < levelProgress.Count; i++)
        {
            string key = $"Level_{i}_";
            LevelData data = levelProgress[i];
            
            PlayerPrefs.SetString(key + "ID", data.levelID);
            PlayerPrefs.SetFloat(key + "PhysicsScore", data.bestPhysicsScore);
            PlayerPrefs.SetFloat(key + "Time", data.bestTime);
            PlayerPrefs.SetInt(key + "Achievement", (int)data.highestAchievement);
            PlayerPrefs.SetInt(key + "Unlocked", data.isUnlocked ? 1 : 0);
            PlayerPrefs.SetInt(key + "Played", data.hasBeenPlayed ? 1 : 0);
        }
        
        PlayerPrefs.Save();
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] 💾 Saved progress for {levelProgress.Count} levels to PlayerPrefs.");
    }
    
    // ═══════════════════════════════════════════════════════
    // LEVEL PROGRESS UPDATES
    // ═══════════════════════════════════════════════════════
    
    /// <summary>
    /// Update level progress when a level is completed.
    /// Called by LevelGoalSettings when player finishes a level.
    /// </summary>
    public void UpdateLevelProgress(string levelID, float physicsScore, float time, Achievement achievement)
    {
        // Get or create level data
        LevelData data = GetOrCreateLevelData(levelID);
        
        // Mark as played
        data.hasBeenPlayed = true;
        
        // Update best scores (only if better)
        bool improved = false;
        
        if (physicsScore > data.bestPhysicsScore)
        {
            data.bestPhysicsScore = physicsScore;
            improved = true;
        }
        
        // For time: lower is better (0 = not recorded yet)
        if (time > 0 && (data.bestTime == 0 || time < data.bestTime))
        {
            data.bestTime = time;
            improved = true;
        }
        
        // Update achievement (only if higher)
        if (achievement > data.highestAchievement)
        {
            data.highestAchievement = achievement;
            improved = true;
        }
        
        // Check if this level is a child of any hub - if so, update that hub's achievement
        UpdateParentHubAchievements(levelID);
        
        // Save immediately
        SaveProgress();
    }
    
    /// <summary>
    /// Get level data for a specific level (returns null if not found)
    /// </summary>
    public LevelData GetLevelData(string levelID)
    {
        if (levelDict.TryGetValue(levelID, out LevelData data))
        {
            return data;
        }
        return null;
    }
    
    /// <summary>
    /// Get or create level data (used internally)
    /// </summary>
    private LevelData GetOrCreateLevelData(string levelID)
    {
        if (levelDict.TryGetValue(levelID, out LevelData data))
        {
            return data;
        }
        
        // Create new level data
        LevelData newData = new LevelData
        {
            levelID = levelID,
            bestPhysicsScore = 0f,
            bestTime = 0f,
            highestAchievement = Achievement.None,
            isUnlocked = false, // Default: LOCKED (unlock via achievements)
            hasBeenPlayed = false
        };
        
        levelProgress.Add(newData);
        levelDict[levelID] = newData;
        
        return newData;
    }
    
    // ═══════════════════════════════════════════════════════
    // LEVEL UNLOCKING (Future Use)
    // ═══════════════════════════════════════════════════════
    
    /// <summary>
    /// Check if a level is unlocked (for future gating)
    /// </summary>
    public bool IsLevelUnlocked(string levelID)
    {
        LevelData data = GetLevelData(levelID);
        return data == null || data.isUnlocked; // Default: unlocked if no data exists
    }
    
    /// <summary>
    /// Check if ALL levels are currently unlocked
    /// </summary>
    public bool AreAllLevelsUnlocked()
    {
        if (levelProgress.Count == 0)
        {
            return false; // No levels = not all unlocked
        }
        
        foreach (var data in levelProgress)
        {
            if (!data.isUnlocked)
            {
                return false; // Found a locked level
            }
        }
        
        return true; // All levels are unlocked!
    }
    
    /// <summary>
    /// Unlock a specific level (for progression gating)
    /// Also activates the portal zone GameObject if assigned
    /// </summary>
    public void UnlockLevel(string levelID)
    {
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] 🔓 UnlockLevel() called for: '{levelID}'");
        
        LevelData data = GetOrCreateLevelData(levelID);
        
        if (data.isUnlocked)
        {
            if (enableVerboseLogging)
                Debug.Log($"[LevelManager] ⚠️ Level '{levelID}' was already unlocked!");
        }
        
        data.isUnlocked = true;
        
        // Activate the portal zone GameObject (make it visible)
        if (data.portalZoneObject != null)
        {
            data.portalZoneObject.SetActive(true);
            if (enableVerboseLogging)
                Debug.Log($"[LevelManager] 🌟 Portal zone for '{levelID}' is now VISIBLE!");
        }
        else
        {
            Debug.LogWarning($"[LevelManager] ⚠️ Level '{levelID}' has no portal zone assigned!");
        }
        
        SaveProgress();
    }
    
    /// <summary>
    /// Unlock ALL levels (cheat code / debug)
    /// </summary>
    public void UnlockAllLevels()
    {
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] 🚨 UnlockAllLevels() called! This should ONLY happen from cheat code!");
        
        int unlockedCount = 0;
        
        foreach (var data in levelProgress)
        {
            if (!data.isUnlocked)
            {
                data.isUnlocked = true;
                
                // Activate portal zone at runtime
                if (data.portalZoneObject != null)
                {
                    data.portalZoneObject.SetActive(true);
                }
                
                unlockedCount++;
            }
        }
        
        Debug.Log($"[LevelManager] 🎉 CHEAT ACTIVATED! Unlocked {unlockedCount} levels. All {levelProgress.Count} levels are now accessible!");
        SaveProgress();
        
        // Notify music player to refresh (may trigger special "all unlocked" music)
        if (MusicPlayer.Instance != null)
        {
            MusicPlayer.Instance.RefreshMusic();
        }
        
        // Notify skybox randomizer to set special skybox
        SkyboxRandomizer skyboxRandomizer = FindFirstObjectByType<SkyboxRandomizer>();
        if (skyboxRandomizer != null)
        {
            skyboxRandomizer.SetSpecialSkybox();
        }
    }
    
    // ═══════════════════════════════════════════════════════
    // HUB MANAGEMENT
    // ═══════════════════════════════════════════════════════
    
    /// <summary>
    /// Register a hub and its children (called by SubLevelHub.Start())
    /// </summary>
    public void RegisterHub(string hubLevelID, string[] childLevelIDs, string[] unlocksAtBronze, string[] unlocksAtSilver, string[] unlocksAtGold)
    {
        LevelData hubData = GetOrCreateLevelData(hubLevelID);
        hubData.hubChildLevelIDs = childLevelIDs;
        hubData.hubUnlocksAtBronze = unlocksAtBronze;
        hubData.hubUnlocksAtSilver = unlocksAtSilver;
        hubData.hubUnlocksAtGold = unlocksAtGold;
        
        if (enableVerboseLogging)
        {
            Debug.Log($"[LevelManager] 📋 Registered hub '{hubLevelID}' with {childLevelIDs.Length} children");
            Debug.Log($"[LevelManager]   Unlocks: Bronze({unlocksAtBronze.Length}), Silver({unlocksAtSilver.Length}), Gold({unlocksAtGold.Length})");
        }
    }
    
    /// <summary>
    /// When a child level completes, check if it belongs to any hub and update that hub's achievement
    /// </summary>
    private void UpdateParentHubAchievements(string completedLevelID)
    {
        // Find all hubs that have this level as a child
        foreach (var levelData in levelProgress)
        {
            if (levelData.hubChildLevelIDs != null && System.Array.IndexOf(levelData.hubChildLevelIDs, completedLevelID) >= 0)
            {
                // This is a parent hub - recalculate its achievement
                if (enableVerboseLogging)
                    Debug.Log($"[LevelManager] 🔄 '{completedLevelID}' is a child of hub '{levelData.levelID}' - Recalculating hub achievement...");
                RecalculateHubAchievement(levelData);
            }
        }
    }
    
    /// <summary>
    /// Recalculate a hub's achievement based on its children's current progress
    /// </summary>
    private void RecalculateHubAchievement(LevelData hubData)
    {
        if (hubData.hubChildLevelIDs == null || hubData.hubChildLevelIDs.Length == 0)
        {
            Debug.LogWarning($"[LevelManager] Hub '{hubData.levelID}' has no registered children!");
            return;
        }
        
        // Count achievements of all children
        int noneCount = 0;
        int bronzeCount = 0;
        int silverCount = 0;
        int goldCount = 0;
        int totalChildren = hubData.hubChildLevelIDs.Length;
        
        foreach (string childID in hubData.hubChildLevelIDs)
        {
            LevelData childData = GetLevelData(childID);
            if (childData != null)
            {
                switch (childData.highestAchievement)
                {
                    case Achievement.None:
                        noneCount++;
                        break;
                    case Achievement.Bronze:
                        bronzeCount++;
                        break;
                    case Achievement.Silver:
                        silverCount++;
                        break;
                    case Achievement.Gold:
                        goldCount++;
                        break;
                }
            }
            else
            {
                noneCount++; // Child not found = treat as None
            }
        }
        
        // Calculate hub achievement using simple rules
        // (SubLevelHub has configurable thresholds, but we'll use sensible defaults here)
        Achievement newHubAchievement = CalculateHubAchievementSimple(noneCount, bronzeCount, silverCount, goldCount, totalChildren);
        
        Achievement oldAchievement = hubData.highestAchievement;
        
        if (newHubAchievement != oldAchievement)
        {
            hubData.highestAchievement = newHubAchievement;
            hubData.hasBeenPlayed = (newHubAchievement > Achievement.None);
            
            if (enableVerboseLogging)
            {
                Debug.Log($"[LevelManager] 🏆 Hub '{hubData.levelID}' achievement updated: {oldAchievement} → {newHubAchievement}");
                Debug.Log($"[LevelManager]   Children: None:{noneCount}, Bronze:{bronzeCount}, Silver:{silverCount}, Gold:{goldCount}");
            }
            
            // Trigger unlocks based on new achievement
            TriggerHubUnlocks(hubData, newHubAchievement);
        }
    }
    
    /// <summary>
    /// Trigger unlocks based on a hub's achievement
    /// </summary>
    private void TriggerHubUnlocks(LevelData hubData, Achievement achievement)
    {
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] 🔓 Checking hub '{hubData.levelID}' unlocks for {achievement}...");
        
        // Unlock at Gold
        if (achievement >= Achievement.Gold && hubData.hubUnlocksAtGold != null && hubData.hubUnlocksAtGold.Length > 0)
        {
            if (enableVerboseLogging)
                Debug.Log($"[LevelManager]   Processing GOLD unlocks ({hubData.hubUnlocksAtGold.Length})...");
            foreach (string sceneName in hubData.hubUnlocksAtGold)
            {
                if (!string.IsNullOrEmpty(sceneName))
                {
                    if (enableVerboseLogging)
                        Debug.Log($"[LevelManager]   🏆 GOLD - Unlocking: {sceneName}");
                    UnlockLevel(sceneName);
                }
            }
        }
        
        // Unlock at Silver
        if (achievement >= Achievement.Silver && hubData.hubUnlocksAtSilver != null && hubData.hubUnlocksAtSilver.Length > 0)
        {
            if (enableVerboseLogging)
                Debug.Log($"[LevelManager]   Processing SILVER unlocks ({hubData.hubUnlocksAtSilver.Length})...");
            foreach (string sceneName in hubData.hubUnlocksAtSilver)
            {
                if (!string.IsNullOrEmpty(sceneName))
                {
                    if (enableVerboseLogging)
                        Debug.Log($"[LevelManager]   🥈 SILVER - Unlocking: {sceneName}");
                    UnlockLevel(sceneName);
                }
            }
        }
        
        // Unlock at Bronze
        if (achievement >= Achievement.Bronze && hubData.hubUnlocksAtBronze != null && hubData.hubUnlocksAtBronze.Length > 0)
        {
            if (enableVerboseLogging)
                Debug.Log($"[LevelManager]   Processing BRONZE unlocks ({hubData.hubUnlocksAtBronze.Length})...");
            foreach (string sceneName in hubData.hubUnlocksAtBronze)
            {
                if (!string.IsNullOrEmpty(sceneName))
                {
                    if (enableVerboseLogging)
                        Debug.Log($"[LevelManager]   🥉 BRONZE - Unlocking: {sceneName}");
                    UnlockLevel(sceneName);
                }
            }
        }
        
        if (enableVerboseLogging)
            Debug.Log($"[LevelManager] ✅ Finished processing hub unlocks.");
    }
    
    /// <summary>
    /// Simple hub achievement calculation (matches default SubLevelHub behavior)
    /// </summary>
    private Achievement CalculateHubAchievementSimple(int noneCount, int bronzeCount, int silverCount, int goldCount, int totalChildren)
    {
        int completedCount = bronzeCount + silverCount + goldCount;
        int silverPlusCount = silverCount + goldCount;
        
        // ALL children have Gold = Hub gets Gold
        if (goldCount == totalChildren)
        {
            return Achievement.Gold;
        }
        
        // ALL children have Silver+ = Hub gets Silver
        if (silverPlusCount == totalChildren)
        {
            return Achievement.Silver;
        }
        
        // ALL children have Bronze+ = Hub gets Bronze
        if (completedCount == totalChildren)
        {
            return Achievement.Bronze;
        }
        
        // ANY child has been completed = Hub gets Bronze (partial)
        if (completedCount > 0)
        {
            return Achievement.Bronze;
        }
        
        // No children completed = Hub gets None
        return Achievement.None;
    }
    
    // ═══════════════════════════════════════════════════════
    // DEBUG / UTILITY
    // ═══════════════════════════════════════════════════════
    
    /// <summary>
    /// Reset all progress (for debugging)
    /// </summary>
    public void ResetAllProgress()
    {
        PlayerPrefs.DeleteAll();
        levelProgress.Clear();
        levelDict.Clear();
    }
    
    /// <summary>
    /// Get current scene name as level ID
    /// </summary>
    public static string GetCurrentLevelID()
    {
        return SceneManager.GetActiveScene().name;
    }
}

// ═══════════════════════════════════════════════════════
// DATA STRUCTURES
// ═══════════════════════════════════════════════════════

/// <summary>
/// Achievement levels for each level
/// </summary>
public enum Achievement
{
    None = 0,
    Bronze = 1,
    Silver = 2,
    Gold = 3
}

/// <summary>
/// Persistent data for each level
/// </summary>
[System.Serializable]
public class LevelData
{
    public string levelID;              // Scene name (unique identifier)
    public float bestPhysicsScore;      // Highest physics points achieved
    public float bestTime;              // Best completion time (seconds)
    public Achievement highestAchievement; // None, Bronze, Silver, Gold
    public bool isUnlocked;             // For level gating (future)
    public bool hasBeenPlayed;          // Has player attempted this level?
    
    [Header("Visual Control")]
    [Tooltip("The GameObject containing the ProximitySceneLoader (will be hidden when locked)")]
    public GameObject portalZoneObject; // The zone object to show/hide
    
    [Header("Hub Configuration (Auto-Populated)")]
    [Tooltip("If this is a hub, these are its child level IDs")]
    public string[] hubChildLevelIDs = new string[0];
    
    [Tooltip("What this hub unlocks at Bronze")]
    public string[] hubUnlocksAtBronze = new string[0];
    
    [Tooltip("What this hub unlocks at Silver")]
    public string[] hubUnlocksAtSilver = new string[0];
    
    [Tooltip("What this hub unlocks at Gold")]
    public string[] hubUnlocksAtGold = new string[0];
}

