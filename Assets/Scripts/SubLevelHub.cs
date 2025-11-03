using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Tracks progress of child levels in a sub-level select scene (e.g., "_Race Level Select")
/// and reports the hub's overall achievement back to LevelManager.
/// Can unlock other levels based on aggregate child progress.
/// </summary>
public class SubLevelHub : MonoBehaviour
{
    public enum AchievementThreshold
    {
        AnyCompleted,           // At least one child has Bronze+
        MajorityBronze,         // More than half have Bronze+
        AllBronzeOrHigher,      // All children have Bronze+
        MajoritySilver,         // More than half have Silver+
        AllSilverOrHigher,      // All children have Silver+
        MajorityGold,           // More than half have Gold
        AllGold                 // All children have Gold
    }
    
    [Header("Hub Configuration")]
    [Tooltip("The scene name of THIS hub (e.g., '_Race Level Select')")]
    [SerializeField] private string hubSceneName;
    
    [Tooltip("Reference to the NPC Interaction Controller in this scene")]
    [SerializeField] private NPCInteractionController npcController;
    
    [Header("Achievement Thresholds")]
    [Tooltip("What aggregate achievement is Bronze?")]
    [SerializeField] private AchievementThreshold bronzeThreshold = AchievementThreshold.AllBronzeOrHigher;
    
    [Tooltip("What aggregate achievement is Silver?")]
    [SerializeField] private AchievementThreshold silverThreshold = AchievementThreshold.AllSilverOrHigher;
    
    [Tooltip("What aggregate achievement is Gold?")]
    [SerializeField] private AchievementThreshold goldThreshold = AchievementThreshold.AllGold;
    
    [Header("--- LEVEL UNLOCKING ---")]
    [Tooltip("Drag scene assets from Project window to unlock at Bronze")]
    [SerializeField] private Object[] unlockScenesAtBronze = new Object[0];
    
    [Tooltip("Drag scene assets from Project window to unlock at Silver")]
    [SerializeField] private Object[] unlockScenesAtSilver = new Object[0];
    
    [Tooltip("Drag scene assets from Project window to unlock at Gold")]
    [SerializeField] private Object[] unlockScenesAtGold = new Object[0];
    
    // Track what we've already unlocked to avoid duplicate unlocks
    private Achievement lastAwardedAchievement = Achievement.None;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    [Header("Child Level Status (Read-Only)")]
    [Tooltip("Extracted child level names (for debugging)")]
    [SerializeField] private string[] childLevelNames;
    
    [Tooltip("Detailed status of each child level")]
    [SerializeField] private ChildLevelStatus[] childLevelStatuses;
    
    private bool hasCheckedProgress = false;
    
    [System.Serializable]
    public class ChildLevelStatus
    {
        public string levelName;
        public bool hasBeenPlayed;
        public Achievement achievement;
        public float bestPhysicsScore;
        public float bestTime;
        public bool isUnlocked;
        
        public ChildLevelStatus(string name)
        {
            levelName = name;
        }
    }
    
    void Start()
    {
        // Auto-fill hub scene name if empty
        if (string.IsNullOrEmpty(hubSceneName))
        {
            hubSceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[SubLevelHub] Auto-detected hub scene name: '{hubSceneName}'");
        }
        
        // Auto-find NPC controller if not assigned
        if (npcController == null)
        {
            npcController = FindFirstObjectByType<NPCInteractionController>();
        }
        
        // Extract child level names from NPC controller
        ExtractChildLevelNames();
        
        // Register this hub with LevelManager so it knows our children AND unlock configuration
        if (LevelManager.Instance != null && childLevelNames != null && childLevelNames.Length > 0)
        {
            // Convert unlock scene assets to string arrays
            string[] bronzeUnlocks = ExtractSceneNames(unlockScenesAtBronze);
            string[] silverUnlocks = ExtractSceneNames(unlockScenesAtSilver);
            string[] goldUnlocks = ExtractSceneNames(unlockScenesAtGold);
            
            LevelManager.Instance.RegisterHub(hubSceneName, childLevelNames, bronzeUnlocks, silverUnlocks, goldUnlocks);
        }
        
        // Check progress on start (when returning from child level or loading hub)
        CheckAndUpdateHubProgress();
    }
    
    /// <summary>
    /// Convert Object[] array of scene assets to string[] of scene names
    /// </summary>
    private string[] ExtractSceneNames(Object[] sceneAssets)
    {
        if (sceneAssets == null || sceneAssets.Length == 0)
        {
            return new string[0];
        }
        
        List<string> sceneNames = new List<string>();
        foreach (var sceneAsset in sceneAssets)
        {
            if (sceneAsset != null)
            {
                sceneNames.Add(sceneAsset.name);
            }
        }
        
        return sceneNames.ToArray();
    }
    
    /// <summary>
    /// Extract the child level scene names from the NPCInteractionController
    /// </summary>
    void ExtractChildLevelNames()
    {
        if (npcController == null)
        {
            Debug.LogError("[SubLevelHub] ❌ NPCInteractionController not assigned! Cannot extract child levels.");
            return;
        }
        
        Debug.Log($"[SubLevelHub] 🔍 Attempting to extract child levels from NPC controller: {npcController.name}");
        
        // Use reflection to access the levelScenes array (it's private)
        var field = typeof(NPCInteractionController).GetField("levelScenes", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field == null)
        {
            Debug.LogError("[SubLevelHub] ❌ Could not find 'levelScenes' field via reflection!");
            return;
        }
        
        Debug.Log($"[SubLevelHub] ✅ Found 'levelScenes' field via reflection");
        
#if UNITY_EDITOR
        Object[] levelScenes = field.GetValue(npcController) as Object[];
        
        if (levelScenes == null)
        {
            Debug.LogError("[SubLevelHub] ❌ levelScenes field is NULL!");
            return;
        }
        
        Debug.Log($"[SubLevelHub] 📋 levelScenes array has {levelScenes.Length} elements");
        
        if (levelScenes.Length == 0)
        {
            Debug.LogWarning("[SubLevelHub] ⚠️ levelScenes array is EMPTY! Did you assign child scenes in NPCInteractionController?");
            childLevelNames = new string[0];
            return;
        }
        
        // Count valid scenes
        int validCount = 0;
        foreach (var scene in levelScenes)
        {
            if (scene != null)
            {
                validCount++;
                Debug.Log($"[SubLevelHub]   ✓ Found scene: {scene.name}");
            }
            else
            {
                Debug.LogWarning($"[SubLevelHub]   ⚠️ Found NULL scene at index");
            }
        }
        
        if (validCount == 0)
        {
            Debug.LogWarning("[SubLevelHub] ⚠️ No valid scenes found! All scene slots are NULL.");
            childLevelNames = new string[0];
            return;
        }
        
        // Extract scene names
        childLevelNames = new string[validCount];
        int index = 0;
        
        foreach (var scene in levelScenes)
        {
            if (scene != null)
            {
                childLevelNames[index] = scene.name;
                index++;
            }
        }
        
        Debug.Log($"[SubLevelHub] ✅ Successfully extracted {childLevelNames.Length} child level names!");
        
#else
        // In build, we need a different approach
        Debug.LogWarning("[SubLevelHub] ⚠️ Cannot extract child levels in build using reflection on SceneAssets.");
        // TODO: Implement build-time solution
        childLevelNames = new string[0];
#endif
    }
    
    /// <summary>
    /// Check the progress of all child levels and update this hub's achievement in LevelManager
    /// </summary>
    public void CheckAndUpdateHubProgress()
    {
        if (LevelManager.Instance == null)
        {
            Debug.LogWarning("[SubLevelHub] LevelManager not found!");
            return;
        }
        
        if (childLevelNames == null || childLevelNames.Length == 0)
        {
            Debug.LogWarning("[SubLevelHub] No child levels to check!");
            return;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[SubLevelHub] '{hubSceneName}' - Checking progress of {childLevelNames.Length} child levels...");
        }
        
        // Initialize status array
        childLevelStatuses = new ChildLevelStatus[childLevelNames.Length];
        
        // Query each child level's achievement
        int noneCount = 0;
        int bronzeCount = 0;
        int silverCount = 0;
        int goldCount = 0;
        int playedCount = 0;
        
        for (int i = 0; i < childLevelNames.Length; i++)
        {
            string childLevel = childLevelNames[i];
            childLevelStatuses[i] = new ChildLevelStatus(childLevel);
            
            var levelData = LevelManager.Instance.GetLevelData(childLevel);
            
            if (levelData != null)
            {
                // Populate status info
                childLevelStatuses[i].hasBeenPlayed = levelData.hasBeenPlayed;
                childLevelStatuses[i].achievement = levelData.highestAchievement;
                childLevelStatuses[i].bestPhysicsScore = levelData.bestPhysicsScore;
                childLevelStatuses[i].bestTime = levelData.bestTime;
                childLevelStatuses[i].isUnlocked = levelData.isUnlocked;
                
                if (levelData.hasBeenPlayed)
                {
                    playedCount++;
                }
                
                switch (levelData.highestAchievement)
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
                
                if (showDebugLogs)
                {
                    Debug.Log($"[SubLevelHub]   • '{childLevel}' = {levelData.highestAchievement} | " +
                              $"Score: {levelData.bestPhysicsScore:F0} | Time: {levelData.bestTime:F2}s | " +
                              $"Played: {levelData.hasBeenPlayed} | Unlocked: {levelData.isUnlocked}");
                }
            }
            else
            {
                if (showDebugLogs)
                {
                    Debug.LogWarning($"[SubLevelHub]   • '{childLevel}' = NOT FOUND in LevelManager!");
                }
            }
        }
        
        // Calculate hub's overall achievement based on children and thresholds
        Achievement hubAchievement = EvaluateHubAchievement(noneCount, bronzeCount, silverCount, goldCount);
        
        if (showDebugLogs)
        {
            Debug.Log($"[SubLevelHub] Summary: {playedCount}/{childLevelNames.Length} played | " +
                      $"None: {noneCount}, Bronze: {bronzeCount}, Silver: {silverCount}, Gold: {goldCount}");
            Debug.Log($"[SubLevelHub] Calculated hub achievement: {hubAchievement}");
        }
        
        // Report hub's achievement to LevelManager
        // (Use a fake time/score since hub doesn't have gameplay)
        float fakeScore = 0f;
        float fakeTime = 0f;
        
        LevelManager.Instance.UpdateLevelProgress(hubSceneName, fakeScore, fakeTime, hubAchievement);
        
        // Handle unlocking based on achievement (only if this is a NEW achievement)
        if (hubAchievement > lastAwardedAchievement)
        {
            UnlockLevelsBasedOnAchievement(hubAchievement);
            lastAwardedAchievement = hubAchievement;
        }
        
        hasCheckedProgress = true;
    }
    
    /// <summary>
    /// Evaluate hub achievement based on configured thresholds
    /// </summary>
    Achievement EvaluateHubAchievement(int noneCount, int bronzeCount, int silverCount, int goldCount)
    {
        int totalLevels = childLevelNames.Length;
        
        // Check Gold threshold first
        if (CheckThreshold(goldThreshold, noneCount, bronzeCount, silverCount, goldCount, totalLevels))
        {
            return Achievement.Gold;
        }
        
        // Check Silver threshold
        if (CheckThreshold(silverThreshold, noneCount, bronzeCount, silverCount, goldCount, totalLevels))
        {
            return Achievement.Silver;
        }
        
        // Check Bronze threshold
        if (CheckThreshold(bronzeThreshold, noneCount, bronzeCount, silverCount, goldCount, totalLevels))
        {
            return Achievement.Bronze;
        }
        
        // No threshold met
        return Achievement.None;
    }
    
    /// <summary>
    /// Check if a specific achievement threshold is met
    /// </summary>
    bool CheckThreshold(AchievementThreshold threshold, int noneCount, int bronzeCount, int silverCount, int goldCount, int totalLevels)
    {
        int completedCount = bronzeCount + silverCount + goldCount;
        int silverPlusCount = silverCount + goldCount;
        int majority = Mathf.CeilToInt(totalLevels / 2f);
        
        switch (threshold)
        {
            case AchievementThreshold.AnyCompleted:
                return completedCount > 0;
                
            case AchievementThreshold.MajorityBronze:
                return completedCount >= majority;
                
            case AchievementThreshold.AllBronzeOrHigher:
                return completedCount == totalLevels;
                
            case AchievementThreshold.MajoritySilver:
                return silverPlusCount >= majority;
                
            case AchievementThreshold.AllSilverOrHigher:
                return silverPlusCount == totalLevels;
                
            case AchievementThreshold.MajorityGold:
                return goldCount >= majority;
                
            case AchievementThreshold.AllGold:
                return goldCount == totalLevels;
                
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Unlock levels based on achieved hub achievement
    /// </summary>
    void UnlockLevelsBasedOnAchievement(Achievement achievement)
    {
        if (LevelManager.Instance == null) return;
        
        if (showDebugLogs)
        {
            Debug.Log($"[SubLevelHub] 🎯 Hub Achievement earned: {achievement}");
            Debug.Log($"[SubLevelHub] Bronze array size: {unlockScenesAtBronze.Length}");
            Debug.Log($"[SubLevelHub] Silver array size: {unlockScenesAtSilver.Length}");
            Debug.Log($"[SubLevelHub] Gold array size: {unlockScenesAtGold.Length}");
        }
        
        // Unlock levels at Gold (if specified)
        if (achievement >= Achievement.Gold && unlockScenesAtGold.Length > 0)
        {
            if (showDebugLogs) Debug.Log($"[SubLevelHub] Processing GOLD unlocks...");
            
            foreach (Object sceneAsset in unlockScenesAtGold)
            {
                if (sceneAsset != null)
                {
                    string sceneName = sceneAsset.name;
                    if (showDebugLogs) Debug.Log($"[SubLevelHub] 🏆 GOLD - Unlocking: {sceneName}");
                    LevelManager.Instance.UnlockLevel(sceneName);
                }
            }
        }
        
        // Unlock levels at Silver (if specified)
        if (achievement >= Achievement.Silver && unlockScenesAtSilver.Length > 0)
        {
            if (showDebugLogs) Debug.Log($"[SubLevelHub] Processing SILVER unlocks...");
            
            foreach (Object sceneAsset in unlockScenesAtSilver)
            {
                if (sceneAsset != null)
                {
                    string sceneName = sceneAsset.name;
                    if (showDebugLogs) Debug.Log($"[SubLevelHub] 🥈 SILVER - Unlocking: {sceneName}");
                    LevelManager.Instance.UnlockLevel(sceneName);
                }
            }
        }
        
        // Unlock levels at Bronze (if specified)
        if (achievement >= Achievement.Bronze && unlockScenesAtBronze.Length > 0)
        {
            if (showDebugLogs) Debug.Log($"[SubLevelHub] Processing BRONZE unlocks...");
            
            foreach (Object sceneAsset in unlockScenesAtBronze)
            {
                if (sceneAsset != null)
                {
                    string sceneName = sceneAsset.name;
                    if (showDebugLogs) Debug.Log($"[SubLevelHub] 🥉 BRONZE - Unlocking: {sceneName}");
                    LevelManager.Instance.UnlockLevel(sceneName);
                }
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[SubLevelHub] ✅ Finished processing hub achievement unlocks.");
        }
    }
    
    
    /// <summary>
    /// Manually trigger a progress check (useful for debugging or external calls)
    /// </summary>
    [ContextMenu("Force Check Progress")]
    public void ForceCheckProgress()
    {
        hasCheckedProgress = false;
        CheckAndUpdateHubProgress();
    }
    
    /// <summary>
    /// Get all child level status info (read-only)
    /// </summary>
    public ChildLevelStatus[] GetChildLevelStatuses()
    {
        return childLevelStatuses;
    }
    
    /// <summary>
    /// Get the hub's calculated achievement based on current child progress
    /// </summary>
    public Achievement GetHubAchievement()
    {
        if (LevelManager.Instance == null || childLevelNames == null)
        {
            return Achievement.None;
        }
        
        int noneCount = 0;
        int bronzeCount = 0;
        int silverCount = 0;
        int goldCount = 0;
        
        foreach (string childLevel in childLevelNames)
        {
            var levelData = LevelManager.Instance.GetLevelData(childLevel);
            
            if (levelData != null)
            {
                switch (levelData.highestAchievement)
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
        }
        
        return EvaluateHubAchievement(noneCount, bronzeCount, silverCount, goldCount);
    }
}

